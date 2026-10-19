const fs=require("fs"); const d=process.argv[2];
let p=fs.readFileSync(d+"/Program.cs","utf8");
p=p.replace('var fwd = new FontConverter(LoadTable(Path.Combine(MapDir, $"unicode-to-{fam.Id}.json")));',
 'string revPath = File.Exists(Arg(1, "")) ? Arg(1, "") : Path.Combine(MapDir, $"{fam.Id}-to-unicode.json");\n        // the forward table beside the reverse one (the same build)\n        var fwd = new FontConverter(LoadTable(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(revPath))!, $"unicode-to-{fam.Id}.json")));');
fs.writeFileSync(d+"/Program.cs",p);
let r=fs.readFileSync(d+"/Reader.cs","utf8");
const anchor='        R($"ਿ({Cluster}|[{Rules.Bases}])", "$1ਿ"';
const add='        R("ਿ((?:੍[ਰਹਵਯ])+)", "$1ਿ", "An i-sign the composer wrote between a letter and its subjoined form (48 71 62, ਕ੍ਯ typed with the i-sign after the letter) belongs to the whole cluster: after the subjoined form."),\n';
if (!r.includes(anchor)) throw "no anchor"; r=r.replace(anchor, add+anchor); fs.writeFileSync(d+"/Reader.cs",r);
