const fs = require("fs");
const f = __dirname + "/tool/RichEdit.cs";
let s = fs.readFileSync(f, "utf8");
const BS = String.fromCharCode(92);
const a = 'var bytes = Encoding.ASCII.GetBytes(ProductRtf(s, face) + "' + BS + '0");';
if (!s.includes(a)) throw new Error("anchor 1 not found");
s = s.replace(a,
  'string rtfText = ProductRtf(s, face);\n' +
  '                    string vr = Environment.GetEnvironmentVariable("WV_RTFVAR");\n' +
  '                    if (vr == "fcharset0") rtfText = rtfText.Replace(@"{' + BS + 'f0' + BS + 'fnil ", @"{' + BS + 'f0' + BS + 'fnil' + BS + 'fcharset0 ");\n' +
  '                    if (vr == "fcharset2") rtfText = rtfText.Replace(@"{' + BS + 'f0' + BS + 'fnil ", @"{' + BS + 'f0' + BS + 'fnil' + BS + 'fcharset2 ");\n' +
  '                    var bytes = Encoding.ASCII.GetBytes(rtfText + "' + BS + '0");');
const b = 'File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".richedit.json"), outArr.ToJsonString(Program.JsonOpt));';
if (!s.includes(b)) throw new Error("anchor 2 not found");
s = s.replace(b,
  'string vsuf = Environment.GetEnvironmentVariable("WV_RTFVAR") is string vv ? "." + vv : "";\n' +
  '        File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".richedit" + vsuf + ".json"), outArr.ToJsonString(Program.JsonOpt));');
fs.writeFileSync(f, s);
console.log("patched");
