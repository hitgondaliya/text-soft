// The Telugu tables in the shared Shree tests: ShreeNonIndicTests (the non-Indic sweep, the look-alikes, the faces'
// code U+0090) and ShreeFontCmapTests (every code written is in every face's cmap). Text edits, idempotent.
// Usage: node patch-tests.js <tests dir>
const fs = require("fs"), path = require("path");
const dir = process.argv[2];
const NL = String.fromCharCode(10), Q = String.fromCharCode(34), A = String.fromCharCode(39), BS = String.fromCharCode(92);
const SHY = String.fromCharCode(0xAD);
const q = s => Q + s + Q;
function edit(file, pairs) {
  const p = path.join(dir, file);
  let s = fs.readFileSync(p, "utf8");
  const nl = s.includes("\r" + NL) ? "\r" + NL : NL;
  for (const [fromLines, toLines] of pairs) {
    const f = fromLines.join(nl) + nl, t = toLines.join(nl) + nl;
    if (s.includes(t)) continue;
    if (!s.includes(f)) throw new Error(file + ": anchor not found: " + fromLines[0]);
    s = s.replace(f, t);
  }
  fs.writeFileSync(p, s);
  console.log("edited " + file);
}
const tel = (u) => u;   // Telugu text below is literal
edit("ShreeNonIndicTests.cs", [
  [[`        { ${q("mangal")}, ${q("shree-dev7")} }, { ${q("mangal")}, ${q("shree-dev456")} }, { ${q("mangal")}, ${q("shree-san7")} },`],
   [`        { ${q("mangal")}, ${q("shree-dev7")} }, { ${q("mangal")}, ${q("shree-dev456")} }, { ${q("mangal")}, ${q("shree-san7")} },`,
    `        { ${q("gautami")}, ${q("shree-tel7")} }, { ${q("gautami")}, ${q("shree-tel7w")} },`]],
  [[`        if (encoding == ${q("shree-san7")}) set.Remove(${A}${SHY}${A});`],
   [`        if (encoding == ${q("shree-san7")}) set.Remove(${A}${SHY}${A});`,
    `        // The Telugu faces map byte 90 at U+0090 (a blank, the same glyph as A0).`,
    `        if (encoding is ${q("shree-tel7")} or ${q("shree-tel7w")}) set.Add(${A}${BS}u0090${A});`]],
  [[`        string letter = encoding.StartsWith(${q("shree-guj")}) ? ${q("ક")} : ${q("क")}, alone = to.ConvertText(letter);`],
   [`        string letter = encoding.StartsWith(${q("shree-guj")}) ? ${q("ક")} : encoding.StartsWith(${q("shree-tel")}) ? ${q("క")} : ${q("क")}, alone = to.ConvertText(letter);`]],
  [[`    [InlineData(${q("mangal")}, ${q("shree-san7")}, ${q("राम…")}, ${q("5A 70 58 22 2E 2E 2E")})]`],
   [`    [InlineData(${q("mangal")}, ${q("shree-san7")}, ${q("राम…")}, ${q("5A 70 58 22 2E 2E 2E")})]`,
    `    [InlineData(${q("gautami")}, ${q("shree-tel7")}, ${q("“తెలుగు”")}, ${q("22 22 99 F0 FE CB 24 56 E6 FC 24 27 27")})]`,
    `    [InlineData(${q("gautami")}, ${q("shree-tel7w")}, ${q("తెలుగు… •")}, ${q("99 F0 FE CB 24 56 E6 FC 24 2E 2E 2E 20 B7")})]`]],
]);
edit("ShreeFontCmapTests.cs", [
  [[`        { ${q("mangal")}, ${q("shree-san7")}, ${q("^SHREE-SAN7-")}, ${q("shree-san7-truth.json")} },`],
   [`        { ${q("mangal")}, ${q("shree-san7")}, ${q("^SHREE-SAN7-")}, ${q("shree-san7-truth.json")} },`,
    `        { ${q("gautami")}, ${q("shree-tel7")}, ${q("^SHREE-TEL7?-")}, ${q("shree-tel7-truth.json")} },`,
    `        { ${q("gautami")}, ${q("shree-tel7w")}, ${q("^Shree-Tel7-00[12]$")}, ${q("shree-tel7-truth.json")} },`]],
]);
