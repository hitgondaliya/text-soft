using System.Runtime.InteropServices;
// shmlab step...   step = "lib:<dll>" | "load:<dll>|<kbdpath>" | "ask:<dll>|<keys>" | "dump:<file>"
// After every step prints which 406-byte tables of SL8_DLL_DATA changed (read-only view).
string shree = @"C:\Shree-Lipi nxt";
SetDllDirectory(shree);
Directory.SetCurrentDirectory(shree);
var libs = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
var page = new IntPtr[9];
for (int i = 1; i <= 8; i++) { page[i] = Marshal.AllocHGlobal(4096); for (int k = 0; k < 4096; k++) Marshal.WriteByte(page[i], k, 0); }
byte[] prev = Snap();
Console.WriteLine("start header " + Hex(prev, 0, 11));
foreach (string step in args)
{
    int c = step.IndexOf(':');
    string op = step[..c], arg = step[(c + 1)..];
    if (op == "lib") Lib(arg);
    else if (op == "load")
    {
        var p = arg.Split('|');
        IntPtr lib = Lib(p[0]);
        var load = Marshal.GetDelegateForFunctionPointer<LoadKbd>(GetProcAddress(lib, "LoadKbdOverlay"));
        IntPtr nm = Marshal.StringToHGlobalAnsi(p[1]);
        int r = load(nm, page[2], page[3], page[4]);
        Console.WriteLine($"{step} -> {r}");
    }
    else if (op == "ask")
    {
        var p = arg.Split('|');
        IntPtr lib = Lib(p[0]);
        IntPtr fp = GetProcAddress(lib, "TutorGetMappedCodeW");
        bool wide = fp != IntPtr.Zero;
        if (!wide) fp = GetProcAddress(lib, "TutorGetMappedCode");
        var f = Marshal.GetDelegateForFunctionPointer<GetMappedCode>(fp);
        foreach (char key in p[1])
        {
            for (int k = 0; k < 128; k++) Marshal.WriteByte(page[7], k, 0);
            int n = f(new IntPtr(key), page[2], page[3], page[4], page[5], IntPtr.Zero, page[7], page[8]);
            var b = new byte[Math.Max(0, Math.Min(n, 30) * (wide ? 2 : 1))];
            Marshal.Copy(page[7], b, 0, b.Length);
            Console.WriteLine($"  {p[0]} '{key}' -> {n}: {(wide ? string.Join(" ", Enumerable.Range(0, b.Length / 2).Select(i => BitConverter.ToUInt16(b, i * 2).ToString("X4"))) : Convert.ToHexString(b))}");
        }
    }
    else if (op == "dump") { File.WriteAllBytes(arg, Snap()); }
    byte[] now = Snap();
    Report(step, prev, now);
    prev = now;
}
return 0;

IntPtr Lib(string name)
{
    if (libs.TryGetValue(name, out var h)) return h;
    h = LoadLibrary(Path.Combine(shree, name));
    if (h == IntPtr.Zero) throw new Exception("load failed " + name);
    libs[name] = h;
    Console.WriteLine($"lib {name}");
    return h;
}
void Report(string step, byte[] a, byte[] b)
{
    var parts = new List<string>();
    if (!a.AsSpan(0, 11).SequenceEqual(b.AsSpan(0, 11))) parts.Add("header " + Hex(a, 0, 11) + " -> " + Hex(b, 0, 11));
    int[] offs = { 0x0B, 0x1A7, 0x33D };
    for (int t = 0; t < 3; t++)
    {
        int n = 0; for (int i = 0; i < 406; i++) if (a[offs[t] + i] != b[offs[t] + i]) n++;
        if (n > 0) parts.Add($"table{t} {n} bytes");
    }
    for (int i = 0; i < 4096; i++)
    {
        bool inTab = (i >= 0x0B && i < 0x0B + 406) || (i >= 0x1A7 && i < 0x1A7 + 812) || i < 11;
        if (!inTab && a[i] != b[i]) parts.Add($"@{i:X}:{a[i]:X2}->{b[i]:X2}");
    }
    Console.WriteLine($"   [{step}] " + (parts.Count == 0 ? "no change" : string.Join(", ", parts)));
}
static string Hex(byte[] b, int o, int n) => string.Join(" ", b.Skip(o).Take(n).Select(x => x.ToString("X2")));
static byte[] Snap()
{
    IntPtr h = OpenFileMappingA(4, false, "SL8_DLL_DATA");
    if (h == IntPtr.Zero) throw new Exception("no SL8_DLL_DATA");
    IntPtr v = MapViewOfFile(h, 4, 0, 0, (UIntPtr)4096);
    var b = new byte[4096]; Marshal.Copy(v, b, 0, 4096);
    UnmapViewOfFile(v); CloseHandle(h);
    return b;
}
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)] static extern IntPtr OpenFileMappingA(uint access, bool inherit, string name);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr MapViewOfFile(IntPtr h, uint access, uint hi, uint lo, UIntPtr n);
[DllImport("kernel32")] static extern bool UnmapViewOfFile(IntPtr p);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string path);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
