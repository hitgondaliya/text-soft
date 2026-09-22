using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

// KbdCapture — read every key of a vendor keyboard from the vendor's own code.
//
// Each script has a composer DLL (S8GujU64.dll for Gujarati in Unicode) whose
// "Tutor" exports answer the one question a keyboard layout is: on this
// keyboard, what does this key type? That is the same answer the vendor's own
// typing gives, for every key - including the keys no published chart prints.
//
// Nothing here touches the keyboard or the screen, so it runs while the machine
// is in use.
//
//   dotnet run --project tools/KbdCapture -- --script guj [--kbd INSCRIPT.GUJ]
//                                            [--form 1-4] [--out <file.json>]

string shree = @"C:\Shree-Lipi nxt";
string script = "guj";
string? kbd = null;
int form = 2;
string? outPath = null;
bool quiet = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--shree": shree = args[++i]; break;
        case "--script": script = args[++i]; break;
        case "--kbd": kbd = args[++i]; break;
        case "--form": form = int.Parse(args[++i]); break;
        case "--out": outPath = args[++i]; break;
        case "--quiet": quiet = true; break;
    }
}

// S8 + script + U (Unicode output) + 64
string dllName = "S8" + char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant() + "U64.dll";
string dllPath = Path.Combine(shree, dllName);
if (!File.Exists(dllPath)) { Console.Error.WriteLine($"no composer for '{script}': {dllPath}"); return 1; }

SetDllDirectory(shree);
Directory.SetCurrentDirectory(shree);
IntPtr lib = LoadLibrary(dllPath);
if (lib == IntPtr.Zero) { Console.Error.WriteLine($"LoadLibrary failed: {Marshal.GetLastWin32Error()}"); return 1; }

var getName = Bind<GetKbdName>(lib, "TutorGetKbdName");
var mapped = Bind<GetMappedCode>(lib, "TutorGetMappedCodeW");
var loadKbd = Bind<LoadKbd>(lib, "LoadKbdOverlay");

// Every argument gets a page of its own: the ones that are pointers then have
// somewhere safe to write, and a wrong guess cannot take the process down.
var page = new IntPtr[9];
for (int i = 1; i <= 8; i++)
{
    page[i] = Marshal.AllocHGlobal(4096);
    for (int k = 0; k < 4096; k++) Marshal.WriteByte(page[i], k, 0);
}

string Loaded()
{
    var buf = new byte[512];
    getName(buf, 256);
    int end = Array.IndexOf(buf, (byte)0);
    return Encoding.ASCII.GetString(buf, 0, end < 0 ? 32 : end);
}

string before = Loaded();
if (!quiet) Console.WriteLine($"{dllName}: keyboard loaded is '{before}'");

// SET_LANG_LAYOUT and SET_LANG_FONT have no sensible shape as functions. Look
// at what is actually at those addresses: if they are exported variables, the
// keyboard is chosen by writing into them rather than by calling anything.
if (args.Contains("--inspect"))
{
    foreach (string name in new[] { "SET_LANG_LAYOUT", "SET_LANG_FONT" })
    {
        IntPtr p = GetProcAddress(lib, name);
        var bytes = new byte[64];
        Marshal.Copy(p, bytes, 0, 64);
        string ascii = new(bytes.Select(x => x is >= 32 and < 127 ? (char)x : '.').ToArray());
        Console.WriteLine($"{name} @0x{p.ToInt64():X}");
        Console.WriteLine($"   hex   {string.Join(' ', bytes.Take(32).Select(x => x.ToString("X2")))}");
        Console.WriteLine($"   ascii {ascii[..32]}");
    }
    return 0;
}

if (kbd is not null)
{
    string[] forms =
    {
        Path.GetFileNameWithoutExtension(kbd),
        kbd,
        kbd.ToLowerInvariant(),
        Path.Combine(shree, kbd),
    };
    // Their own host declares LoadKbdOverlay(lpszKbdOverlay) - one string. The
    // only question left is whether it wants it narrow or wide.
    bool wide = args.Contains("--wide");
    string text = forms[Math.Clamp(form - 1, 0, forms.Length - 1)];
    IntPtr name = wide ? Marshal.StringToHGlobalUni(text) : Marshal.StringToHGlobalAnsi(text);
    int r = loadKbd(name, page[2], page[3], page[4]);
    if (!quiet) Console.WriteLine($"LoadKbdOverlay({(wide ? "wide" : "ansi")} '{text}') returned {r}; keyboard is now '{Loaded()}'");
}

// Press every printable key, unshifted and shifted, and write down what the
// composer says it types.
var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
for (char key = '!'; key <= '~'; key++)
{
    for (int k = 0; k < 32; k++) Marshal.WriteByte(page[7], k, 0);
    int units = mapped(key, page[2], page[3], page[4], page[5], page[6], page[7], page[8]);
    if (units <= 0) continue;

    var bytes = new byte[Math.Min(units * 2, 30)];
    Marshal.Copy(page[7], bytes, 0, bytes.Length);
    // A key that types a matra comes back with a leading space - the dotted-circle
    // carrier a lone vowel sign is shown on, not part of the character.
    string text = Encoding.Unicode.GetString(bytes).TrimEnd('\0').TrimStart(' ');
    if (text.Length == 0) continue;
    keys[key.ToString()] = text;
}

if (!quiet)
{
    Console.WriteLine($"keys that type something: {keys.Count}");
    foreach (var row in new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" })
        Console.WriteLine("  " + string.Join("  ", row.Select(c => $"{c}={(keys.TryGetValue(c.ToString(), out var v) ? v : "·")}")));
}

// Which of our own keyboards is this, and do we type it the same way?
if (args.Contains("--compare"))
{
    string repo = FindRoot();
    var bundle = new FontSoftware.Core.Config.ConfigLoader(Path.Combine(repo, "assets", "config")).Load();
    string prefix = script.ToLowerInvariant() switch { "guj" => "gu", "dev" => "hi", "ban" => "bn", _ => script };

    (string Name, int Same, int Total) best = ("", -1, 0);
    foreach (var language in bundle.Languages)
        foreach (var reference in language.Layouts)
        {
            FontSoftware.Core.Models.KeyboardLayout ours;
            try { ours = bundle.GetLayout(reference); } catch { continue; }

            int same = 0, total = 0;
            foreach (var (key, text) in keys)
            {
                string? mine = Ours(ours, key[0]);
                if (mine is null) continue;
                total++;
                if (mine == text) same++;
            }
            if (total > 20 && same > best.Same) best = (language.Name + " / " + reference.Name, same, total);
        }

    Console.WriteLine($"closest of our keyboards: {best.Name} - same on {best.Same} of {best.Total} keys");
}

if (outPath is not null)
{
    var doc = new { script, keyboard = kbd ?? before, dll = dllName, keys };
    File.WriteAllText(outPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }), new UTF8Encoding(false));
    if (!quiet) Console.WriteLine($"written: {outPath}");
}

return 0;

static T Bind<T>(IntPtr lib, string name) where T : Delegate
{
    IntPtr p = GetProcAddress(lib, name);
    if (p == IntPtr.Zero) throw new EntryPointNotFoundException(name);
    return Marshal.GetDelegateForFunctionPointer<T>(p);
}
/// <summary>What one of our own layouts puts on a key, matching the composer's notation.</summary>
static string? Ours(FontSoftware.Core.Models.KeyboardLayout layout, char key)
{
    foreach (var m in layout.Keys)
    {
        int vk = m.VirtualKey;
        (char bas, char shift) = vk switch
        {
            >= 'A' and <= 'Z' => (char.ToLowerInvariant((char)vk), (char)vk),
            >= '0' and <= '9' => ((char)vk, ")!@#$%^&*("[vk - '0']),
            186 => (';', ':'), 187 => ('=', '+'), 188 => (',', '<'), 189 => ('-', '_'),
            190 => ('.', '>'), 191 => ('/', '?'), 192 => ('`', '~'), 219 => ('[', '{'),
            220 => ((char)92, '|'), 221 => (']', '}'), 222 => ((char)39, '"'),
            _ => ('\0', '\0'),
        };
        if (key == bas && !string.IsNullOrEmpty(m.Base)) return m.Base;
        if (key == shift && !string.IsNullOrEmpty(m.Shift)) return m.Shift;
    }
    return null;
}

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern IntPtr LoadLibrary(string path);

[DllImport("kernel32", SetLastError = true)]
static extern IntPtr GetProcAddress(IntPtr module, string name);

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern bool SetDllDirectory(string path);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetKbdName(byte[] buffer, int length);

/// <summary>The key goes in the first argument, the text comes back in the seventh, the count is returned.</summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);

