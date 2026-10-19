// Pseudo-families xa-<family>: the ASCII 0x20-0x7E code points NOT in the family's cmap (the first run tested only
// mapped code points, table-written unmapped ones and the cp1252 punctuation). Same rep face; lines gid -1.
const fs = require("fs"), path = require("path");
const root = __dirname, fam = path.join(root, "fam");
const skip = new Set(["Shree-Ban7-00xA", "Shree-Tel7-00xA"]);   // not in the task's family list (13- and 7-glyph faces)
const out = [];
for (const f of fs.readdirSync(fam).filter(f => f.endsWith(".base.json") && !f.startsWith("xa-"))) {
  const name = f.slice(0, -".base.json".length);
  if (skip.has(name)) continue;
  const B = JSON.parse(fs.readFileSync(path.join(fam, f), "utf8"));
  const have = new Set(B.cps.map(c => parseInt(c.cp.slice(2), 16)));
  const miss = [];
  for (let v = 0x20; v <= 0x7e; v++) if (!have.has(v)) miss.push(v);
  if (!miss.length) continue;
  const hex = v => "U+" + v.toString(16).toUpperCase().padStart(4, "0");
  fs.writeFileSync(path.join(fam, "xa-" + name + ".base.json"), JSON.stringify(B, null, 2));
  fs.writeFileSync(path.join(fam, "xa-" + name + ".lines.json"), JSON.stringify(miss.map(v => ({ cp: hex(v), gid: -1, kind: "unmapped-punct" })), null, 2));
  out.push({ name: "xa-" + name, face: B.repFace, cps: miss.map(hex) });
}
fs.writeFileSync(path.join(root, "xa-list.json"), JSON.stringify(out, null, 1));
for (const o of out) console.log(o.name, o.face, o.cps.join(" "));
