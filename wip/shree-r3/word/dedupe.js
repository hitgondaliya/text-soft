// Merge families whose digest text is identical (after removing the face name) into one entry.
const fs = require("fs");
const path = require("path");
const root = __dirname;
function blocks(file, splitter) {
  const t = fs.readFileSync(path.join(root, file), "utf8");
  return splitter(t);
}
function merge(items) {
  // items: [{fam, face, body}]
  const groups = new Map();
  for (const it of items) {
    const key = it.body.split(it.face).join("<face>");
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(it.fam);
  }
  return [...groups.entries()].map(([k, fams]) => `${fams.join(" = ")}: ${k}`);
}
const S = JSON.parse(fs.readFileSync(path.join(root, "summary.json"), "utf8"));
const faceOf = Object.fromEntries(S.families.map(f => [f.family, f.repFace]));
// grouped-invisible: "### FAM (...)\n  lines"
const inv = blocks("grouped-invisible.txt", t => t.split(/^### /m).filter(Boolean).map(b => {
  const fam = b.split(" ")[0];
  return { fam, face: faceOf[fam], body: b.slice(b.indexOf("\n") + 1).replace(/\n\s+/g, " || ").trim() };
}));
const asc = blocks("compact-ascii.txt", t => t.split("\n").filter(Boolean).map(l => {
  const fam = l.split(":")[0];
  return { fam, face: faceOf[fam], body: l.slice(fam.length + 2) };
}));
fs.writeFileSync(path.join(root, "final-invisible.txt"), merge(inv).join("\n"));
fs.writeFileSync(path.join(root, "final-ascii.txt"), merge(asc).join("\n"));
console.log(fs.statSync(path.join(root, "final-invisible.txt")).size, fs.statSync(path.join(root, "final-ascii.txt")).size);
