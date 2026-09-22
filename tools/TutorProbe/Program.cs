using System.Runtime.InteropServices;
using System.Text;

// TutorProbe — ask Shree-Lipi's own composer what each key types.
//
// Every script has a composer DLL (S8GujU64.dll for Gujarati in Unicode) and it
// exports a "Tutor" API: the typing tutor has to be able to ask "on this
// keyboard, what does this key produce?" - which is exactly the question our
// keyboard layouts answer. Asking the vendor's code beats reading the vendor's
// documentation: it is the same answer their typing gives, for every key,
// including the ones no chart prints.
//
// Nothing here touches the keyboard or the screen, so it runs while the machine
// is in use.
//
//   dotnet run --project tools/TutorProbe -- [--dll S8GujU64.dll] [--kbd INSCRIPT.GUJ]

string shree = @"C:\Shree-Lipi nxt";
string dll = "S8GujU64.dll";
string kbd = "INSCRIPT.GUJ";
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--shree": shree = args[++i]; break;
        case "--dll": dll = args[++i]; break;
        case "--kbd": kbd = args[++i]; break;
    }
}

string path = Path.Combine(shree, dll);
Console.WriteLine($"loading {path}");
SetDllDirectory(shree);
Directory.SetCurrentDirectory(shree);

IntPtr h = LoadLibrary(path);
if (h == IntPtr.Zero) { Console.Error.WriteLine($"LoadLibrary failed: {Marshal.GetLastWin32Error()}"); return 1; }
Console.WriteLine($"loaded at 0x{h.ToInt64():X}");

foreach (string name in new[] { "TutorGetKbdName", "TutorGetMappedCodeW", "TutorGetAlterCode",
                                "TutorGetAkshar", "GetAksharCountW", "LoadKbdOverlay",
                                "SET_LANG_LAYOUT", "SET_LANG_FONT", "ControlDll" })
    Console.WriteLine($"  {name,-22} {(GetProcAddress(h, name) == IntPtr.Zero ? "MISSING" : "ok")}");

// ---- what is loaded right now? ----
var getName = Get<TutorGetKbdName>(h, "TutorGetKbdName");
var buf = new byte[512];
Try("TutorGetKbdName(buf, 256)", () =>
{
    int r = getName(buf, 256);
    return $"returned {r}, buffer = '{Ascii(buf)}' / wide '{Wide(buf)}'";
});

// GetAksharCountW takes a pointer, not an int - left alone until its shape is known
var count = Get<GetAksharCountW>(h, "GetAksharCountW");

// ---- what does a key produce? ----
// The function returns a 16-bit value (the disassembly ends in movzx eax,ax),
// so the mapped character comes back directly rather than through a buffer.
var mapped = Get<TutorGetMappedCodeWP>(h, "TutorGetMappedCodeW");

// Any of the eight arguments could be a pointer, and a null one takes the
// process down. Hand every argument a page of its own zeroed memory instead:
// the ones that are really integers just see a large number, the ones that are
// really pointers have somewhere safe to write, and nothing crashes.
var pages = new IntPtr[9];
for (int i = 1; i <= 8; i++)
{
    pages[i] = Marshal.AllocHGlobal(4096);
    for (int k = 0; k < 4096; k++) Marshal.WriteByte(pages[i], k, 0);
}

Console.WriteLine("\nTutorGetMappedCodeW, one argument carrying the key, the rest pointers:");
foreach (char key in "kabqzfgu")
{
    for (int i = 1; i <= 8; i++) for (int k = 0; k < 64; k++) Marshal.WriteByte(pages[i], k, 0);
    int r = mapped(key, pages[2], pages[3], pages[4], pages[5], pages[6], pages[7], pages[8]);
    var wrote = new List<string>();
    for (int i = 2; i <= 8; i++)
    {
        var bytes = new byte[24];
        Marshal.Copy(pages[i], bytes, 0, 24);
        if (bytes.Any(x => x != 0)) wrote.Add($"arg{i}=[" + Hex(bytes, 8) + "] '" + Wide(bytes) + "'");
    }
    Console.WriteLine($"  key '{key}' -> returned {r}   " + string.Join("   ", wrote));
}

Console.WriteLine("\nanything written into the argument pages:");
for (int i = 1; i <= 8; i++)
{
    var bytes = new byte[32];
    Marshal.Copy(pages[i], bytes, 0, 32);
    if (bytes.Any(x => x != 0)) Console.WriteLine($"  page {i}: {Hex(bytes, 16)}  wide '{Wide(bytes)}'");
}

// ---- can the keyboard be switched? ----
// One shape per run: a wrong guess is an access violation, which .NET cannot
// catch, so the caller loops over --load 1..4 in separate processes.
int loadShape = 0;
for (int i = 0; i < args.Length; i++) if (args[i] == "--load") loadShape = int.Parse(args[++i]);
if (loadShape > 0)
{
    string[] forms = { Path.GetFileNameWithoutExtension(kbd), kbd, kbd.ToLowerInvariant(), Path.Combine(shree, kbd) };
    string text = forms[Math.Clamp(loadShape - 1, 0, forms.Length - 1)];
    IntPtr which = Marshal.StringToHGlobalAnsi(text);
    var load = Get<LoadKbdOverlayP>(h, "LoadKbdOverlay");
    Console.WriteLine( LoadKbdOverlay with '{text}'");
    int r = load(which, pages[2], pages[3], pages[4]);
    Console.WriteLine(   returned {r}");
    Array.Clear(buf);
    getName(buf, 256);
    Console.WriteLine($"  keyboard now: '{Ascii(buf)}'");
    foreach (char probe in "kafq")
    {
        for (int k = 0; k < 16; k++) Marshal.WriteByte(pages[7], k, 0);
        int m2 = mapped(probe, pages[2], pages[3], pages[4], pages[5], pages[6], pages[7], pages[8]);
        var ob = new byte[16]; Marshal.Copy(pages[7], ob, 0, 16);
        Console.WriteLine(   key '{probe}' -> {m2} '{Wide(ob)}'");
    }
    return 0;
}

var alter = Get<TutorGetAlterCode>(h, "TutorGetAlterCode");
Console.WriteLine();
foreach (char key in "abck")
    Try($"TutorGetAlterCode('{key}')", () => { int r = alter(key); return $"0x{r:X4} '{Show(r)}'"; });

return 0;

static string Show(int code) => code is > 0x20 and < 0x110000 ? char.ConvertFromUtf32(code) : "";

static string Ascii(byte[] b)
{
    int n = Array.IndexOf(b, (byte)0);
    return Encoding.ASCII.GetString(b, 0, n < 0 ? Math.Min(b.Length, 64) : n);
}

static string Wide(byte[] b)
{
    var s = Encoding.Unicode.GetString(b);
    int n = s.IndexOf('\0');
    return n < 0 ? s : s[..n];
}

static string Hex(byte[] b, int n) => string.Join(' ', b.Take(n).Select(x => x.ToString("X2")));

static T Get<T>(IntPtr lib, string name) where T : Delegate
{
    IntPtr p = GetProcAddress(lib, name);
    if (p == IntPtr.Zero) throw new EntryPointNotFoundException(name);
    return Marshal.GetDelegateForFunctionPointer<T>(p);
}

static void Try(string what, Func<string> call)
{
    try { Console.WriteLine($"{what,-40} -> {call()}"); }
    catch (Exception ex) { Console.WriteLine($"{what,-40} -> FAILED {ex.GetType().Name}: {ex.Message}"); }
}

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern IntPtr LoadLibrary(string path);

[DllImport("kernel32", SetLastError = true)]
static extern IntPtr GetProcAddress(IntPtr module, string name);

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern bool SetDllDirectory(string path);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
delegate int TutorGetKbdName(byte[] buffer, int length);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
delegate int GetAksharCountW(int which);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
delegate int LoadKbdOverlay([MarshalAs(UnmanagedType.LPStr)] string keyboard);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int TutorGetMappedCodeW(int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int TutorGetAlterCode(int code);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int TutorGetMappedCodeWP(IntPtr a1, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr a7, IntPtr a8);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int LoadKbdOverlayP(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
