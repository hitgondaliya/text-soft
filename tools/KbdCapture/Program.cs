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
int form = 4;   // full path with extension - the form that actually switches
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
string family = "U";
for (int i = 0; i < args.Length; i++) if (args[i] == "--family") family = args[++i];
// U is the Unicode composer; 7, X and G are the legacy font families, which
// answer with the bytes those fonts draw instead of Unicode.
string dllName = "S8" + char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant() + family + "64.dll";
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

// Their own host (Compose64.exe) runs InitializeComposeDLL, then SetHook, then
// LoadKeyboard. Which of those the composer actually needs before it will take
// a different keyboard is what --pre sweeps.
string? pre = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--pre") pre = args[++i];
if (pre is not null)
{
    foreach (string step in pre.Split(','))
    {
        try
        {
            switch (step)
            {
                case "control0": Bind<Control>(lib, "ControlDll")(IntPtr.Zero); break;
                case "control1": Bind<Control>(lib, "ControlDll")(new IntPtr(1)); break;
                case "hook":
                    Bind<Hook>(lib, "SetHook")(page[1], IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    break;
                case "hookname":
                    Bind<Hook>(lib, "SetHook")(Marshal.StringToHGlobalAnsi(dllName), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    break;
                case "ilreset": Bind<Control>(lib, "ILRESET")(IntPtr.Zero); break;
            }
            if (!quiet) Console.WriteLine($"  pre-step '{step}' ok");
        }
        catch (Exception ex) { if (!quiet) Console.WriteLine($"  pre-step '{step}' failed: {ex.GetType().Name}"); }
    }
}

if (kbd is not null)
{
    string? exact = null;
    for (int i = 0; i < args.Length; i++) if (args[i] == "--name") exact = args[++i];
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
    string text = exact ?? forms[Math.Clamp(form - 1, 0, forms.Length - 1)];
    // ANSI, and the FULL path with extension: that is the one form the composer takes.
    IntPtr name = wide ? Marshal.StringToHGlobalUni(text) : Marshal.StringToHGlobalAnsi(text);
    int r = loadKbd(name, page[2], page[3], page[4]);
    if (!quiet) Console.WriteLine($"LoadKbdOverlay({(wide ? "wide" : "ansi")} '{text}') returned {r}; keyboard is now '{Loaded()}'");
}

// The composer's built-in keyboard is only one of twenty-one. But a keyboard
// file holds key -> internal code, and TutorGetAkshar looks like code ->
// character: with that table every keyboard file decodes, without ever having
// to make the composer switch.
if (args.Contains("--akshar"))
{
    var akshar = Bind<GetAkshar>(lib, "TutorGetAkshar");
    Console.WriteLine("code -> character, as the composer names it:");
    int named = 0;
    for (int code = 0; code <= 255; code++)
    {
        for (int k = 0; k < 64; k++) { Marshal.WriteByte(page[2], k, 0); Marshal.WriteByte(page[3], k, 0); }
        int units;
        try { units = akshar(new IntPtr(code), page[2], page[3]); }
        catch (Exception ex) { Console.WriteLine($"  {code:X2}: {ex.GetType().Name}"); break; }

        var written = new List<string>();
        foreach (int slot in new[] { 2, 3 })
        {
            var bytes = new byte[24];
            Marshal.Copy(page[slot], bytes, 0, 24);
            if (bytes.Any(b => b != 0))
            {
                string text = Encoding.Unicode.GetString(bytes).TrimEnd('\0').Trim();
                written.Add($"arg{slot}='{text}' [{string.Join(' ', bytes.Take(6).Select(b => b.ToString("X2")))}]");
            }
        }
        if (units != 0 || written.Count > 0)
        {
            named++;
            if (named <= 40) Console.WriteLine($"  {code:X2}: returned {units}  {string.Join("  ", written)}");
        }
    }
    Console.WriteLine($"codes that answered: {named}");
    return 0;
}

// The third layer. The published Modular chart documents exactly one entry for
// it - Ctrl+Space then 'v' is ૠ - which is enough of an oracle to recognise the
// layer when a call shape finally produces it.
if (args.Contains("--alter"))
{
    var alterCode = Bind<AlterCode>(lib, "TutorGetAlterCode");
    int slot = 0, value = 0;
    string probe = "v";
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--altslot") slot = int.Parse(args[++i]);
        else if (args[i] == "--altval") value = Convert.ToInt32(args[++i], 16);
        else if (args[i] == "--altkey") probe = args[++i];
    }

    if (slot == 0)
    {
        Console.WriteLine("TutorGetAlterCode per key:");
        foreach (char c in "abcdefghijklmnopqrstuvwxyz")
            Console.Write($" {c}={alterCode(c):X2}");
        Console.WriteLine();
        return 0;
    }

    var call = new IntPtr[9];
    for (int i = 1; i <= 8; i++) call[i] = page[i];
    call[slot] = new IntPtr(value);
    foreach (char c in probe)
    {
        call[1] = new IntPtr(c);
        for (int k = 0; k < 32; k++) Marshal.WriteByte(page[7], k, 0);
        int units = mapped(call[1], call[2], call[3], call[4], call[5], call[6], call[7], call[8]);
        var bytes = new byte[24];
        Marshal.Copy(page[7], bytes, 0, 24);
        string text = Encoding.Unicode.GetString(bytes).TrimEnd('\0').Trim();
        Console.WriteLine($"ALT slot {slot} = 0x{value:X} : key '{c}' -> {units} '{text}'");
    }
    return 0;
}

// Rosetta: the same keyboard read twice, once by the Unicode composer and once
// by a legacy-font one. The key is the join - whatever key 'k' types, the two
// answers are the same character in two encodings, which is exactly the pair a
// mapping table is made of, straight from the vendor.
if (args.Contains("--rosetta"))
{
    string legacyFamily = "X";
    for (int i = 0; i < args.Length; i++) if (args[i] == "--legacy") legacyFamily = args[++i];

    string otherName = "S8" + char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant() + legacyFamily + "64.dll";
    IntPtr other = LoadLibrary(Path.Combine(shree, otherName));
    if (other == IntPtr.Zero) { Console.Error.WriteLine($"cannot load {otherName}"); return 1; }

    // the Shree-Lipi 7 composers answer in bytes and drop the W from the name
    IntPtr proc = GetProcAddress(other, "TutorGetMappedCodeW");
    if (proc == IntPtr.Zero) proc = GetProcAddress(other, "TutorGetMappedCode");
    if (proc == IntPtr.Zero) { Console.Error.WriteLine($"{otherName} has no mapped-code export"); return 1; }
    var legacyMapped = Marshal.GetDelegateForFunctionPointer<GetMappedCode>(proc);

    var legacyLoad = Marshal.GetDelegateForFunctionPointer<LoadKbd>(GetProcAddress(other, "LoadKbdOverlay"));
    if (kbd is not null)
        legacyLoad(Marshal.StringToHGlobalAnsi(Path.Combine(shree, kbd)), page[2], page[3], page[4]);

    var pairs = new SortedDictionary<string, string>(StringComparer.Ordinal);
    // A keyboard is more than the keys printed on it. Layer 0 is what the keycaps
    // say; the vendor reaches two more layers with Ctrl+Space and Ctrl+Shift+1, and
    // that is where the characters a script needs rarely - and a table is most
    // likely to be missing - actually live.
    for (int layer = 0; layer <= 2; layer++)
    for (char key = '!'; key <= '~'; key++)
    {
        string? uni = Press(key, layer);
        if (uni is null) continue;

        for (int k = 0; k < 32; k++) Marshal.WriteByte(page[7], k, 0);
        int units = legacyMapped(new IntPtr(key), page[2], page[3], page[4], page[5],
                                 new IntPtr(layer), page[7], page[8]);
        if (units <= 0) continue;

        // The two legacy families answer in different widths. The Ex composers
        // write UTF-16 code units, like the Unicode one; the Shree-Lipi 7 ones
        // write the font's bytes, one per unit - and reading those as UTF-16
        // glues each pair of bytes into one nonsense character ("$L" came back as
        // U+244C), which then went into the tables as a character no font has.
        var bytes = new byte[Math.Min(units * 2, 30)];
        Marshal.Copy(page[7], bytes, 0, bytes.Length);
        string legacy = legacyFamily == "7"
            ? new string(bytes.Take(units).Select(b => (char)b).ToArray()).TrimEnd('\0')
            : Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        if (legacy.Length == 0 || legacy == uni) continue;
        if (!pairs.ContainsKey(uni)) pairs[uni] = legacy;
    }

    Console.WriteLine($"{Path.GetFileNameWithoutExtension(kbd ?? before)}: {pairs.Count} pairs");
    if (outPath is not null)
        File.WriteAllText(outPath, JsonSerializer.Serialize(new { script, family = legacyFamily, pairs },
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            new UTF8Encoding(false));
    else
        foreach (var (u, l) in pairs.Take(20))
            Console.WriteLine($"  {u} = {string.Join(' ', l.Select(c => ((int)c).ToString("X4")))}");
    return 0;
}

// Press every printable key and write down what the composer says it types.
// The sixth argument is the layer: 0 is the printed keyboard (the key's own
// character carries the shift state), 1 is the third layer the vendor reaches
// with Ctrl+Space. Their Modular chart documents one entry for it - Ctrl+Space
// then 'v' is ૠ - and that is exactly what layer 1 returns.
string? Press(char key, int layer)
{
    for (int k = 0; k < 32; k++) Marshal.WriteByte(page[7], k, 0);
    int units = mapped(new IntPtr(key), page[2], page[3], page[4], page[5],
                       new IntPtr(layer), page[7], page[8]);
    if (units <= 0) return null;

    var bytes = new byte[Math.Min(units * 2, 30)];
    Marshal.Copy(page[7], bytes, 0, bytes.Length);
    string text = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    if (text.Length == 0) return null;

    // A lone vowel sign is shown sitting on a carrier - a space or a dot - the
    // way a chart prints it on a dotted circle. The carrier is not part of the
    // character.
    if (text.Any(c => c is >= 'ऀ' and <= '෿'))
        text = new string(text.Where(c => c > '~').ToArray());
    // Some keys come back as the tutor's own placeholder instead of text at all
    // ("Lk" for the virama). Two or more bare Latin letters is never a keyboard's
    // answer in an Indian script, so say nothing rather than something wrong.
    else if (text.Length > 1 && text.All(char.IsAsciiLetter))
        return null;

    return text.Trim().Length == 0 ? null : text.Trim();
}

var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
var alter = new SortedDictionary<string, string>(StringComparer.Ordinal);
for (char key = '!'; key <= '~'; key++)
{
    if (Press(key, 0) is { } normal) keys[key.ToString()] = normal;
    if (Press(key, 1) is { } third && third != keys.GetValueOrDefault(key.ToString()))
        alter[key.ToString()] = third;
}

if (!quiet)
{
    Console.WriteLine($"keys that type something: {keys.Count}");
    foreach (var row in new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" })
        Console.WriteLine("  " + string.Join("  ", row.Select(c => $"{c}={(keys.TryGetValue(c.ToString(), out var v) ? v : "·")}")));
}

// Score the capture against a published chart for the same keyboard. The charts
// are the oracle: a load that really switched the keyboard scores near 100, one
// that did nothing keeps scoring against whatever was loaded before.
string? chart = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--score") chart = args[++i];
if (chart is not null)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(chart));
    var printed = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
    {
        string k = p.GetProperty("Key").GetString() ?? "";
        string u = p.GetProperty("Uni").GetString() ?? "";
        if (k.Length == 1 && u.Length > 0 && !printed.ContainsKey(k)) printed[k] = u;
    }

    // the chart writes the third layer as Ø + key, the same mark our layouts use
    var thirdLayer = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
    {
        string k = p.GetProperty("Key").GetString() ?? "";
        string u = p.GetProperty("Uni").GetString() ?? "";
        if (k.Length == 2 && k[0] == 'Ø' && u.Length > 0) thirdLayer[k[1..]] = u;
    }
    if (thirdLayer.Count > 0)
    {
        int hit = thirdLayer.Count(t => alter.TryGetValue(t.Key, out var mine) && mine == t.Value);
        Console.WriteLine($"  third layer (Ctrl+Space): {hit}/{thirdLayer.Count} same");
        foreach (var (k, u) in thirdLayer.Take(4))
            Console.WriteLine($"    Ctrl+Space {k}: chart {u} / composer {alter.GetValueOrDefault(k, "-")}");
    }

    int same = 0, differ = 0, absent = 0;
    var examples = new List<string>();
    foreach (var (k, u) in printed)
    {
        if (!keys.TryGetValue(k, out string? mine)) { absent++; continue; }
        if (mine == u) same++;
        else
        {
            differ++;
            if (examples.Count < 6) examples.Add($"{k}: chart {u} / composer {mine}");
        }
    }
    int total = same + differ + absent;
    Console.WriteLine($"SCORE {Path.GetFileNameWithoutExtension(chart)}: {same}/{total} " +
                      $"({(total == 0 ? 0 : 100.0 * same / total):F0}%) same, {differ} differ, {absent} not captured");
    foreach (string e in examples) Console.WriteLine("    " + e);
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
    var doc2 = new { script, keyboard = kbd ?? before, dll = dllName, keys, alter };
    File.WriteAllText(outPath, JsonSerializer.Serialize(doc2, new JsonSerializerOptions
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


[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int Control(IntPtr arg);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int Hook(IntPtr a1, IntPtr a2, IntPtr a3, IntPtr a4);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetAkshar(IntPtr code, IntPtr a2, IntPtr a3);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int AlterCode(IntPtr code);
