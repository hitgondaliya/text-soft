const fs=require("fs"); const d=process.argv[2];
let r=fs.readFileSync(d+"/Reader.cs","utf8");
const anchor='        R("ੱਂ", "ਁ",';
const add='        R("([ੇੈੋੌ])\\1", "$1", "A vowel sign twice in a row draws one sign: the composer\'s own broken answer for ਠ/ਨ + subjoined + a top sign + bindi (ਠ੍ਰੇਂ written 53 79 BE 7A, the subjoined form lost) reads as the one sign it shows."),\n';
if (!r.includes(anchor)) throw "no anchor"; r=r.replace(anchor, add+anchor); fs.writeFileSync(d+"/Reader.cs",r);
let f=fs.readFileSync(d+"/Fonts.cs","utf8");
f=f.replace("var missing = written.Where(c => !cmap.ContainsKey(c) && c != ' ' && c != '\n' && c != '\r' && c != '\t').ToList();",
            "// whitespace passes through every table unreported, by design (FontConverter): it is not a glyph the table writes\n            var missing = written.Where(c => !cmap.ContainsKey(c) && !char.IsWhiteSpace((char)c)).ToList();");
fs.writeFileSync(d+"/Fonts.cs",f);
let b=fs.readFileSync(d+"/Build.cs","utf8");
b=b.replace("var q1252 = Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Concat(written.Select(cp => (char)cp))",
            "var q1252 = Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Where(ch => fwd.ConvertText(ch.ToString()).Length > 0).Concat(written.Select(cp => (char)cp)).Where(ch => !char.IsWhiteSpace(ch))");
fs.writeFileSync(d+"/Build.cs",b);
