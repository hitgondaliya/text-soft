using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// IlPeek — dump the public surface of a .NET assembly (types, methods, and any
// P/Invoke DllImports) without running it. Used to read Shree-Lipi's embedded
// SLConvert.dll / SLConvData.dll converter wrappers.
//
//   dotnet run --project tools/IlPeek -- <assembly.dll> [--pinvoke]

if (args.Length < 1) { Console.Error.WriteLine("usage: ilpeek <assembly> [--pinvoke]"); return 1; }
bool pinvokeOnly = args.Contains("--pinvoke");

using var fs = File.OpenRead(args[0]);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

string TypeName(TypeDefinition t) =>
    (t.Namespace.IsNil ? "" : md.GetString(t.Namespace) + ".") + md.GetString(t.Name);

foreach (var th in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(th);
    string tn = TypeName(t);
    if (tn is "<Module>") continue;

    var methods = new List<string>();
    foreach (var mh in t.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        string mn = md.GetString(m.Name);
        var attrs = m.Attributes;
        bool isPublic = (attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;

        // P/Invoke import?
        var imp = m.GetImport();
        if (!imp.Module.IsNil)
        {
            var mod = md.GetModuleReference(imp.Module);
            string dll = md.GetString(mod.Name);
            string ep = imp.Name.IsNil ? mn : md.GetString(imp.Name);
            methods.Add($"    [DllImport(\"{dll}\", EntryPoint=\"{ep}\")] {mn}()  {imp.Attributes}");
            continue;
        }
        if (pinvokeOnly) continue;
        // Parameter names say what a delegate expects when the signature blob
        // would only say "IntPtr" - lpszKbdOverlay is worth more than pointer.
        var names = m.GetParameters()
                     .Select(ph => md.GetParameter(ph))
                     .Where(p => !p.Name.IsNil && p.SequenceNumber > 0)
                     .Select(p => md.GetString(p.Name));
        if (isPublic) methods.Add($"    {(attrs.HasFlag(MethodAttributes.Static) ? "static " : "")}{mn}({string.Join(", ", names)})");
    }

    if (pinvokeOnly && methods.Count == 0) continue;
    if (methods.Count == 0 && pinvokeOnly) continue;
    Console.WriteLine(tn);
    foreach (var s in methods) Console.WriteLine(s);
}
return 0;
