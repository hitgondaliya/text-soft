using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// ResX — list and extract EMBEDDED managed resources from a .NET assembly.
// Used to pull the SLConvert.dll / SLConvData.dll converter wrappers that
// Shree-Lipi's TransNXT.exe embeds (as resource "TransNXT.lib.SLConvert.dll").
//
//   dotnet run --project tools/ResX -- "C:\Shree-Lipi nxt Plus\TransNXT.exe" <outDir> [filter]

if (args.Length < 2) { Console.Error.WriteLine("usage: resx <assembly> <outDir> [nameFilter]"); return 1; }
string asm = args[0], outDir = args[1];
string? filter = args.Length > 2 ? args[2] : null;
Directory.CreateDirectory(outDir);

using var fs = File.OpenRead(asm);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

var corHeader = pe.PEHeaders.CorHeader;
if (corHeader is null) { Console.Error.WriteLine("not a managed assembly"); return 1; }
int resDirRva = corHeader.ResourcesDirectory.RelativeVirtualAddress;

Console.WriteLine($"Manifest resources in {Path.GetFileName(asm)}:");
foreach (var handle in md.ManifestResources)
{
    var res = md.GetManifestResource(handle);
    string name = md.GetString(res.Name);
    // Implementation.IsNil => embedded in this PE
    if (!res.Implementation.IsNil) { Console.WriteLine($"  (external) {name}"); continue; }

    var section = pe.GetSectionData(resDirRva);
    var blob = section.GetReader();
    blob.Offset = (int)res.Offset;
    int len = blob.ReadInt32();               // length-prefixed
    byte[] data = blob.ReadBytes(len);

    bool save = filter is null || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    string kind = data.Length > 2 && data[0] == 'M' && data[1] == 'Z' ? "PE/dll" : "data";
    Console.WriteLine($"  {(save ? "*" : " ")} {name}  ({len} bytes, {kind})");
    if (save)
    {
        // strip resource wrapper prefix to a clean file name
        string file = name;
        File.WriteAllBytes(Path.Combine(outDir, file), data);
    }
}
Console.WriteLine($"\nSaved matching resources to {outDir}");
return 0;
