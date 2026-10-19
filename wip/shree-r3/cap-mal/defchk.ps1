$src = @"
using System; using System.Runtime.InteropServices;
public static class Shm {
 [DllImport("kernel32", SetLastError=true, CharSet=CharSet.Ansi)] public static extern IntPtr OpenFileMappingA(uint a, bool i, string n);
 [DllImport("kernel32", SetLastError=true)] public static extern IntPtr MapViewOfFile(IntPtr h, uint a, uint hi, uint lo, UIntPtr n);
 [DllImport("kernel32")] public static extern bool UnmapViewOfFile(IntPtr p);
 [DllImport("kernel32")] public static extern bool CloseHandle(IntPtr h);
 public static byte[] Read(string name, int n) { IntPtr h = OpenFileMappingA(4, false, name); if (h == IntPtr.Zero) return null; IntPtr v = MapViewOfFile(h, 4, 0, 0, (UIntPtr)n); var b = new byte[n]; Marshal.Copy(v, b, 0, n); UnmapViewOfFile(v); CloseHandle(h); return b; }
}
"@
Add-Type -TypeDefinition $src
$before = [IO.File]::ReadAllBytes($args[0])
$now = [Shm]::Read('SL8_DEFAULTS_DATA', $before.Length)
if ($now -eq $null) { 'SL8_DEFAULTS_DATA not open' } else { $d = 0; for ($i = 0; $i -lt $before.Length; $i++) { if ($now[$i] -ne $before[$i]) { $d++ } }; "SL8_DEFAULTS_DATA: $d of $($before.Length) bytes differ from this morning's before dump" }
if ($args.Length -gt 1) { [IO.File]::WriteAllBytes($args[1], $now); "saved current to " + $args[1] }
