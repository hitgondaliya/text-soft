const fs = require("fs");
const f = __dirname + "/tool/RichEdit.cs";
let s = fs.readFileSync(f, "utf8");
const BS = String.fromCharCode(92);
const a = '                    var bytes = Encoding.ASCII.GetBytes(rtfText + "' + BS + '0");';
if (!s.includes(a)) throw new Error("anchor not found");
s = s.replace(a,
  '                    if (vr == "fcharset0fs") rtfText = rtfText.Replace(@"{' + BS + 'f0' + BS + 'fnil ", @"{' + BS + 'f0' + BS + 'fnil' + BS + 'fcharset0 ");\n' +
  '                    if (vr == "fcharset2fs") rtfText = rtfText.Replace(@"{' + BS + 'f0' + BS + 'fnil ", @"{' + BS + 'f0' + BS + 'fnil' + BS + 'fcharset2 ");\n' +
  '                    if (vr != null && vr.EndsWith("fs")) rtfText = rtfText.Replace(@"' + BS + 'ltrch' + BS + 'fcs0' + BS + 'f0 ", @"' + BS + 'ltrch' + BS + 'fcs0' + BS + 'f0' + BS + 'fs112 ");\n' +
  a);
// keep crops of variant runs apart from the product run
const c = 'img.Save(Path.Combine(crops, $"{line.Cp:X4}.re-{mode}.png")';
if (!s.includes(c)) throw new Error("crop anchor not found");
s = s.replace(c, 'img.Save(Path.Combine(crops, $"{line.Cp:X4}.re-{mode}{(Environment.GetEnvironmentVariable("WV_RTFVAR") is string vq ? "-" + vq : "")}.png")');
fs.writeFileSync(f, s);
console.log("patched");
