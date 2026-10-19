// Build compact text digests from summary.json and fam/<family>.json for the structured answer.
const fs = require("fs");
const path = require("path");
const root = __dirname;
const S = JSON.parse(fs.readFileSync(path.join(root, "summary.json"), "utf8"));
const out = { invisible: [], alternatives: [], ascii: [], stats: [] };

function short(v) {
  return (v || "")
    .replace(/BCD[A-Z]EE\+/g, "")
    .replace(/\/TrueType code /g, " code ")
    .replace(/\/Type0 code /g, " CID ");
}

for (const f of S.families) {
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", f.family + ".json"), "utf8"));
  const cps = F.codepoints;
  out.stats.push(`${f.family} (${f.repFace}, ${f.faces} faces, same cmap: ${f.allFacesSameCmap}): mapped ${f.mapped}, unmapped tested ${f.testedUnmapped}; Word ok ${f.wordOk}, RichEdit typed ok ${f.richeditTypedOk}, RTF ok ${f.richeditRtfOk}; failing ${f.failing.length}`);
  const inv = [];
  const alts = [];
  for (const c of cps.filter(c => c.fails)) {
    const w = c.word.ok ? "Word ok" : `Word: ${short(c.word.verdict)}`;
    const rt = c.richedit.typed.ok ? "RE typed ok" : `RE typed: ${c.richedit.typed.verdict}`;
    const rr = c.richedit.rtf.ok ? "RE RTF ok" : `RE RTF: ${c.richedit.rtf.verdict}`;
    const tabs = (c.tables || []).map(t => `${t.table} writes it in ${t.pairsWritingIt} pairs${t.examples.length ? " e.g. " + t.examples.slice(0, 3).join(", ") : ""}${t.postRulesWritingIt.length ? " + " + t.postRulesWritingIt.length + " post rules" : ""}`).join("; ");
    inv.push(`${c.cp}${c.byte ? " (byte " + c.byte + ")" : ""} [${c.kind}${c.gid != null ? ", gid " + c.gid : ""}] draws: ${c.draws} | ${w} | ${rt} | ${rr} | tables: ${tabs || "no wired table"}`);
    const a = Array.isArray(c.alternatives) ? c.alternatives.map(x => `${x.cp} (Word PDF ${x.wordPdf}, F1 ${x.wordInkF1}; EMF ${x.emf}; RE ${x.richeditTyped} F1 ${x.richeditTypedF1})`).join(", ") : "none";
    alts.push(`${c.cp}: ${a}`);
  }
  out.invisible.push(`== ${f.family} (${f.repFace}) wiring main=${F.wiring.main} worktree=${F.wiring.worktree}\n` + (inv.length ? inv.join("\n") : "none"));
  out.alternatives.push(`== ${f.family}: ` + (alts.length ? alts.join("; ") : "no failing code points"));
  out.ascii.push(`== ${f.family}\n` + f.asciiAndPunct.join("\n"));
}
fs.writeFileSync(path.join(root, "digest-invisible.txt"), out.invisible.join("\n\n"));
fs.writeFileSync(path.join(root, "digest-alternatives.txt"), out.alternatives.join("\n"));
fs.writeFileSync(path.join(root, "digest-ascii.txt"), out.ascii.join("\n\n"));
fs.writeFileSync(path.join(root, "digest-stats.txt"), out.stats.join("\n"));
console.log(out.stats.join("\n"));
