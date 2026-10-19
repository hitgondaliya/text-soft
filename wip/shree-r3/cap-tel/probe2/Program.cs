using System.Runtime.InteropServices;
using System.Text;

// probe2 --list <listfile> [--legacy 7] [--script Pun]   (listfile: lines "kbdpath<TAB>outpath")
// probe2 --put <dumpfile>   : write <dumpfile> (zero-padded to 4096 bytes) back into SL8_DLL_DATA, read back, verify.
//                             Loads no composer. Used once, to restore SLNXT's block to the dump taken before the capture.
// Same as tsk-ban\probe (U composer + legacy composer, every key '!'..'~' on layers 0..2, two passes -> "#stable"),
// plus: after every LoadKbdOverlay, table 0 of SLNXT's shared SL8_DLL_DATA (offset 0x0B, 406 bytes) is compared
// with the keyboard file -> "#loaded 1" when the load really took (a too-long path or bad name silently fails).
string shree = @"C:\Shree-Lipi nxt";
string fam = "7";
int maxLayer = 2;
string script = "Pun";
for (int i = 0; i < args.Length; i++) { if (args[i] == "--legacy") fam = args[++i]; else if (args[i] == "--script") script = args[++i]; else if (args[i] == "--layers") maxLayer = int.Parse(args[++i]); }

int pi = Array.IndexOf(args, "--put");
if (pi >= 0)
{
    var want = new byte[4096];
    var src = File.ReadAllBytes(args[pi + 1]);
    Array.Copy(src, want, Math.Min(src.Length, 4096));
    IntPtr h = OpenFileMappingA(2 /*FILE_MAP_WRITE*/, false, "SL8_DLL_DATA");
    if (h == IntPtr.Zero) { Console.Error.WriteLine("no SL8_DLL_DATA " + Marshal.GetLastWin32Error()); return 3; }
    IntPtr v = MapViewOfFile(h, 2, 0, 0, (UIntPtr)4096);
    Marshal.Copy(want, 0, v, 4096);
    UnmapViewOfFile(v); CloseHandle(h);
    var now = Snap();
    int d = 0; for (int i = 0; i < 4096; i++) if (now[i] != want[i]) d++;
    Console.WriteLine(d == 0 ? "PUT OK: SL8_DLL_DATA identical to " + args[pi + 1] : "PUT MISMATCH " + d + " bytes");
    return d == 0 ? 0 : 4;
}

var jobs = new List<(string Kbd, string Out)>();
int li = Array.IndexOf(args, "--list");
foreach (var line in File.ReadAllLines(args[li + 1]))
{
    var p = line.Split('\t');
    if (p.Length == 2) jobs.Add((p[0], Path.GetFullPath(p[1])));
}
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
    string loaded = "-";
    if (kbd != "-")
    {
        IntPtr nm = Marshal.StringToHGlobalAnsi(kbd);
        uLoad(nm, page[2], page[3], page[4]);
        lLoad(nm, page[2], page[3], page[4]);
        Marshal.FreeHGlobal(nm);
        var file = File.ReadAllBytes(kbd);
        var shm = Snap();
        int d = 0; for (int i = 0; i < 406; i++) if (i >= file.Length || shm[0x0B + i] != file[i]) d++;
        loaded = d == 0 ? "1" : "0\t" + d;
    }
    string a = Capture();
    string b = Capture();
    var sb = new StringBuilder();
    sb.Append($"#stable\t{(a == b ? 1 : 0)}\n");
    sb.Append($"#loaded\t{loaded}\n");
    sb.Append(a);
    File.WriteAllText(outPath, sb.ToString());
}
return 0;

string Capture()
{
    var sb = new StringBuilder();
    for (int layer = 0; layer <= maxLayer; layer++)
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

static byte[] Snap()
{
    IntPtr h = OpenFileMappingA(4, false, "SL8_DLL_DATA");
    if (h == IntPtr.Zero) throw new Exception("no SL8_DLL_DATA");
    IntPtr v = MapViewOfFile(h, 4, 0, 0, (UIntPtr)4096);
    var b = new byte[4096]; Marshal.Copy(v, b, 0, 4096);
    UnmapViewOfFile(v); CloseHandle(h);
    return b;
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
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)] static extern IntPtr OpenFileMappingA(uint access, bool inherit, string name);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr MapViewOfFile(IntPtr h, uint access, uint hi, uint lo, UIntPtr n);
[DllImport("kernel32")] static extern bool UnmapViewOfFile(IntPtr p);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
