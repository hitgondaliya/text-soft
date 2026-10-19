const fs = require("fs");
const f = __dirname + "/tool/Emf.cs";
let s = fs.readFileSync(f, "utf8");
function rep(a, b) { if (!s.includes(a)) throw new Error("anchor not found: " + a); s = s.replace(a, b); }
// EmfAnalyze: accept tag=family
rep(`        foreach (var fam in fams)
        {
            string dir = Path.Combine(Program.Root, "emf", fam);`,
`        foreach (var famArg in fams)
        {
            string tag = famArg.Contains('=') ? famArg.Split('=')[0] : famArg;
            string fam = famArg.Contains('=') ? famArg.Split('=')[1] : famArg;
            string dir = Path.Combine(Program.Root, "emf", tag);`);
rep(`File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".emf.json"), outArr.ToJsonString(Program.JsonOpt));`,
    `File.WriteAllText(Path.Combine(Program.Root, "fam", tag + ".emf.json"), outArr.ToJsonString(Program.JsonOpt));`);
rep(`Console.WriteLine($"{fam}: {outArr.Count} lines; labelOk=`, `Console.WriteLine($"{tag}: {outArr.Count} lines; labelOk=`);
// EmfPlay: accept tag=family
rep(`        string fam = args[0];
        var lines = Lines.Load(fam);
        string outDir = Path.Combine(Program.Root, "emfplay", fam);`,
`        string tag = args[0].Contains('=') ? args[0].Split('=')[0] : args[0];
        string fam = args[0].Contains('=') ? args[0].Split('=')[1] : args[0];
        var lines = Lines.Load(fam);
        string outDir = Path.Combine(Program.Root, "emfplay", tag);`);
rep(`            string f = Path.Combine(Program.Root, "emf", fam, $"{i + 1:D4}.emf");`, `            string f = Path.Combine(Program.Root, "emf", tag, $"{i + 1:D4}.emf");`);
fs.writeFileSync(f, s);
console.log("patched");
