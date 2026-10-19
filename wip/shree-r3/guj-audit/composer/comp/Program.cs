using System.Runtime.InteropServices;
using System.Text;

// comp - Shree-Lipi Gujarati composer probe for the r3 guj audit.
//   comp dump <dir>                 read-only snapshot of every SL8_* shared mapping (first 64 KB each)
//   comp cap <listfile>             listfile lines "kbdpath<TAB>outpath": load the keyboard into
//                                   S8GujU64 and S8Guj764, ask every key '!'..'~' on layers 0,1,2
//                                   twice, write a TSV (layer key uUnits uHex lUnits lHex)
//   comp restore <bin>              write <bin> back over SL8_DLL_DATA (4096 bytes) and verify
// Composer DLLs share SLNXT's live SL8_DLL_DATA, so only one comp process may run at a time.
string shree = @"C:\Shree-Lipi nxt";
if (args.Length == 0) { Console.Error.WriteLine("usage"); return 1; }

string[] names = { "SL8_DEFAULTS_DATA", "SL8_DLL_DATA", "SL8_LOCK_DATA", "SL8_SHORTCUTS_DATA", "SL8_VFCCODES_DATA",
                   "SL8_VFCEQLM_DATA", "SL8_VFCLAYEXPNS_DATA", "SL8_VFCMFL_DATA", "SL8_VFCPHANTOMDETAILS_DATA", "SL8_VFCPHANTOM_DATA" };

if (args[0] == "dump")
{
    Directory.CreateDirectory(args[1]);
    foreach (var n in names)
    foreach (var pre in new[] { "", "Local\\" })
    {
        IntPtr h = OpenFileMappingA(4, false, pre + n);
        if (h == IntPtr.Zero) { Console.WriteLine($"{pre}{n}: absent"); continue; }
        int size = 65536;
        IntPtr v = MapViewOfFile(h, 4, 0, 0, UIntPtr.Zero);
        if (v == IntPtr.Zero) { CloseHandle(h); Console.WriteLine($"{pre}{n}: no view"); continue; }
        var q = new MEMORY_BASIC_INFORMATION();
        VirtualQuery(v, ref q, (IntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
        size = (int)Math.Min((long)q.RegionSize, 1 << 20);
        var b = new byte[size];
        Marshal.Copy(v, b, 0, size);
        UnmapViewOfFile(v); CloseHandle(h);
        string file = Path.Combine(args[1], (pre.Length > 0 ? "Local_" : "") + n + ".bin");
        File.WriteAllBytes(file, b);
        Console.WriteLine($"{pre}{n}: {size} bytes, {b.Count(x => x != 0)} nonzero");
    }
    return 0;
}

if (args[0] == "restore")
{
    // comp restore <bin> [mappingName] : write the dump back (first 4096 bytes)
    byte[] want = File.ReadAllBytes(args[1]);
    string map = args.Length > 2 ? args[2] : "SL8_DLL_DATA";
    IntPtr h = OpenFileMappingA(2 | 4, false, map);
    if (h == IntPtr.Zero) { Console.Error.WriteLine("no " + map); return 3; }
    IntPtr v = MapViewOfFile(h, 2 | 4, 0, 0, (UIntPtr)4096);
    var cur = new byte[4096];
    Marshal.Copy(v, cur, 0, 4096);
    int changed = 0; for (int i = 0; i < Math.Min(4096, want.Length); i++) if (cur[i] != want[i]) changed++;
    Marshal.Copy(want, 0, v, Math.Min(4096, want.Length));
    var now = new byte[4096];
    Marshal.Copy(v, now, 0, 4096);
    UnmapViewOfFile(v); CloseHandle(h);
    int diff = 0; for (int i = 0; i < Math.Min(4096, want.Length); i++) if (now[i] != want[i]) diff++;
    Console.WriteLine($"{map}: {changed} bytes put back; " + (diff == 0 ? "RESTORED: equals " + args[1] : "NOT restored, " + diff + " bytes differ"));
    return diff == 0 ? 0 : 4;
}

if (args[0] != "cap") { Console.Error.WriteLine("unknown mode"); return 1; }

var jobs = new List<(string Kbd, string Out)>();
foreach (var line in File.ReadAllLines(args[1]))
{
    var p = line.Split('\t');
    if (p.Length == 2) jobs.Add((p[0], Path.GetFullPath(p[1])));
}
foreach (var j in jobs)
    if (j.Out.StartsWith(shree, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("refusing to write under Shree"); return 2; }

SetDllDirectory(shree);
Directory.SetCurrentDirectory(shree);
IntPtr u = LoadLibrary(Path.Combine(shree, "S8GujU64.dll"));
IntPtr l = LoadLibrary(Path.Combine(shree, "S8Guj764.dll"));
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
    IntPtr nm = Marshal.StringToHGlobalAnsi(kbd);
    int r1 = uLoad(nm, page[2], page[3], page[4]);
    int r2 = lLoad(nm, page[2], page[3], page[4]);
    string a = Capture();
    string b = Capture();
    var sb = new StringBuilder();
    sb.Append($"#kbd\t{kbd}\n#load\t{r1}\t{r2}\n#stable\t{(a == b ? 1 : 0)}\n");
    sb.Append(a);
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"{Path.GetFileName(kbd)} load {r1}/{r2} stable {(a == b ? 1 : 0)}");
}
return 0;

string Capture()
{
    var sb = new StringBuilder();
    for (int layer = 0; layer <= 2; layer++)
    for (char key = '!'; key <= '~'; key++)
    {
        string uh = Ask(uMapped, key, layer, true, out int uu);
        string lh = Ask(lMapped, key, layer, false, out int lu);
        if (uu <= 0 && lu <= 0) continue;
        sb.Append($"{layer}\t{(int)key:X2}\t{uu}\t{uh}\t{lu}\t{lh}\n");
    }
    return sb.ToString();
}

string Ask(GetMappedCode f, char key, int layer, bool wide, out int units)
{
    for (int k = 0; k < 256; k++) Marshal.WriteByte(page[7], k, 0);
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

[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string path);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)] static extern IntPtr OpenFileMappingA(uint access, bool inherit, string name);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr MapViewOfFile(IntPtr h, uint access, uint hi, uint lo, UIntPtr n);
[DllImport("kernel32")] static extern bool UnmapViewOfFile(IntPtr p);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32")] static extern IntPtr VirtualQuery(IntPtr addr, ref MEMORY_BASIC_INFORMATION info, IntPtr len);

[StructLayout(LayoutKind.Sequential)]
struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect; public IntPtr RegionSize; public uint State; public uint Protect; public uint Type; }

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
