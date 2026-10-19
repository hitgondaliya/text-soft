using System.Runtime.InteropServices;
using System.Text;

// probe <kbd-full-path> <out.tsv> [--legacy 7] [--script Dev]
// probe --list <listfile>            (listfile: lines "kbdpath<TAB>outpath")
// For every key '!'..'~' on layers 0..2, ask the Unicode composer and the legacy
// composer what the key types, and write both raw answers (hex) to a TSV.
// Each keyboard is captured twice; "#stable 1" means both passes agreed (the
// composers keep their keyboard in SLNXT's shared SL8_* mappings, so another
// process loading a keyboard at the same moment would show up as a mismatch).
string shree = @"C:\Shree-Lipi nxt";
string fam = "7";
string script = "Dev";
for (int i = 0; i < args.Length; i++) { if (args[i] == "--legacy") fam = args[++i]; else if (args[i] == "--script") script = args[++i]; }

var jobs = new List<(string Kbd, string Out)>();
int li = Array.IndexOf(args, "--list");
if (li >= 0)
{
    foreach (var line in File.ReadAllLines(args[li + 1]))
    {
        var p = line.Split('\t');
        if (p.Length == 2) jobs.Add((p[0], Path.GetFullPath(p[1])));
    }
}
else jobs.Add((args[0], Path.GetFullPath(args[1])));
foreach (var j in jobs)
    if (j.Out.StartsWith(shree, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("refusing to write under Shree"); return 2; }

SetDllDirectory(shree);
Directory.SetCurrentDirectory(shree);
IntPtr u = LoadLibrary(Path.Combine(shree, "S8" + script + "U64.dll"));
IntPtr l = LoadLibrary(Path.Combine(shree, "S8" + script + fam + "64.dll"));
if (u == IntPtr.Zero || l == IntPtr.Zero) { Console.Error.WriteLine($"load failed {Marshal.GetLastWin32Error()}"); return 1; }

var uMapped = Bind<GetMappedCode>(u, "TutorGetMappedCodeW");
IntPtr lp = GetProcAddress(l, "TutorGetMappedCodeW");
if (lp == IntPtr.Zero) lp = GetProcAddress(l, "TutorGetMappedCode");
var lMapped = Marshal.GetDelegateForFunctionPointer<GetMappedCode>(lp);
var uLoad = Bind<LoadKbd>(u, "LoadKbdOverlay");
var lLoad = Bind<LoadKbd>(l, "LoadKbdOverlay");

var page = new IntPtr[9];
for (int i = 1; i <= 8; i++)
{
    page[i] = Marshal.AllocHGlobal(4096);
    for (int k = 0; k < 4096; k++) Marshal.WriteByte(page[i], k, 0);
}

foreach (var (kbd, outPath) in jobs)
{
    if (kbd != "-")
    {
        IntPtr nm = Marshal.StringToHGlobalAnsi(kbd);
        uLoad(nm, page[2], page[3], page[4]);
        lLoad(nm, page[2], page[3], page[4]);
    }
    string a = Capture();
    string b = Capture();
    var sb = new StringBuilder();
    sb.Append($"#stable\t{(a == b ? 1 : 0)}\n");
    sb.Append(a);
    File.WriteAllText(outPath, sb.ToString());
}
return 0;

string Capture()
{
    var sb = new StringBuilder();
    for (int layer = 0; layer <= 2; layer++)
    for (char key = '!'; key <= '~'; key++)
    {
        string uh = Ask(uMapped, key, layer, true, out int uu);
        string lh = Ask(lMapped, key, layer, fam != "7", out int lu);
        if (uu <= 0 && lu <= 0) continue;
        sb.Append($"{layer}\t{(int)key:X2}\t{uu}\t{uh}\t{lu}\t{lh}\n");
    }
    return sb.ToString();
}

string Ask(GetMappedCode f, char key, int layer, bool wide, out int units)
{
    for (int k = 0; k < 128; k++) Marshal.WriteByte(page[7], k, 0);
    units = f(new IntPtr(key), page[2], page[3], page[4], page[5], new IntPtr(layer), page[7], page[8]);
    if (units <= 0 || units > 60) return "";
    var parts = new List<string>();
    if (wide)
        for (int i = 0; i < units; i++) parts.Add(((ushort)Marshal.ReadInt16(page[7], i * 2)).ToString("X4"));
    else
        for (int i = 0; i < units; i++) parts.Add(Marshal.ReadByte(page[7], i).ToString("X2"));
    return string.Join(' ', parts);
}

static T Bind<T>(IntPtr lib, string name) where T : Delegate
{
    IntPtr p = GetProcAddress(lib, name);
    if (p == IntPtr.Zero) throw new EntryPointNotFoundException(name);
    return Marshal.GetDelegateForFunctionPointer<T>(p);
}

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern IntPtr LoadLibrary(string path);
[DllImport("kernel32", SetLastError = true)]
static extern IntPtr GetProcAddress(IntPtr module, string name);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
static extern bool SetDllDirectory(string path);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
