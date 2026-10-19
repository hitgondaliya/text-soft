using System.Runtime.InteropServices;
string outDir = args.Length > 0 ? args[0] : ".";
string[] names = { "SL8_DEFAULTS_DATA", "SL8_DLL_DATA", "SL8_LOCK_DATA", "SL8_SHORTCUTS_DATA", "SL8_VFCCODES_DATA", "SL8_VFCEQLM_DATA",
    "SL8_VFCLAYEXPNS_DATA", "SL8_VFCMFL_DATA", "SL8_VFCPHANTOMDETAILS_DATA", "SL8_VFCPHANTOM_DATA" };
foreach (string prefix in new[] { "", "Local\\", "Global\\" })
foreach (string n in names)
{
    IntPtr h = OpenFileMappingA(4 /*FILE_MAP_READ*/, false, prefix + n);
    if (h == IntPtr.Zero) { if (prefix == "") Console.WriteLine($"{prefix}{n}: not open ({Marshal.GetLastWin32Error()})"); continue; }
    IntPtr v = MapViewOfFile(h, 4, 0, 0, UIntPtr.Zero);
    var mbi = new MEMORY_BASIC_INFORMATION();
    VirtualQuery(v, out mbi, (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
    long size = (long)mbi.RegionSize;
    var b = new byte[size];
    Marshal.Copy(v, b, 0, (int)size);
    int last = Array.FindLastIndex(b, x => x != 0);
    File.WriteAllBytes(Path.Combine(outDir, prefix.Replace("\\", "_") + n + ".bin"), b.AsSpan(0, last + 1).ToArray());
    Console.WriteLine($"{prefix}{n}: region {size} bytes, used up to {last + 1}");
    UnmapViewOfFile(v); CloseHandle(h);
}
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)] static extern IntPtr OpenFileMappingA(uint access, bool inherit, string name);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr MapViewOfFile(IntPtr h, uint access, uint hi, uint lo, UIntPtr n);
[DllImport("kernel32")] static extern bool UnmapViewOfFile(IntPtr p);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32")] static extern UIntPtr VirtualQuery(IntPtr a, out MEMORY_BASIC_INFORMATION m, UIntPtr len);
[StructLayout(LayoutKind.Sequential)] struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect; public uint pad; public UIntPtr RegionSize; public uint State; public uint Protect; public uint Type; public uint pad2; }
