// Failing code points per family, grouped by failure class, with how many pairs each wired forward table writes there.
const fs = require("fs");
const path = require("path");
const root = __dirname;
const S = JSON.parse(fs.readFileSync(path.join(root, "summary.json"), "utf8"));
const lines = [];
function cls(c) {
  const w = c.word, t = c.richedit.typed.verdict, r = c.richedit.rtf.verdict;
  if (c.kind !== "mapped") return "NOT IN CMAP";
  if (w.emf.includes("NO ink")) return "C1 CONTROL (Word EMF/GDI: no ink; Word PDF: font's U+2022 glyph or .notdef; RichEdit: blank, advance kept)";
  if (c.cp === "U+00AD") return "SOFT HYPHEN";
  if (t.includes("GDI binds it to Arial")) return "RICHEDIT CS238 (Word ok; RichEdit sets charset 238, GDI binds Arial, draws the Latin letter)";
  if (t.includes("Cambria Math")) return "RICHEDIT CAMBRIA MATH (Word ok; RichEdit switches the font to Cambria Math)";
  return "OTHER";
}
for (const f of S.families) {
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", f.family + ".json"), "utf8"));
  const g = {};
  for (const c of F.codepoints.filter(x => x.fails)) {
    const k = cls(c);
    const d = c.draws.replace(/ \[reverse [^\]]+\]/g, "").replace(/ \[composer [^\]]+\]/g, "").split("; ")[0]
      .replace(/^Indic /, "").replace(/^look-alike /, "~").replace(/ \(U\+[0-9A-F ]+\)$/, "")
      .replace(/^unidentified glyph.*/, "?").replace(/^nothing: blank glyph.*/, "blank").replace(/^not in the font's cmap.*/, "-");
    const tabs = (c.tables || []).filter(t => t.pairsWritingIt > 0 || t.postRulesWritingIt.length > 0)
      .map(t => `${t.table.replace("unicode-to-shree-", "").replace("main:", "").replace("worktree:", "wt:")} ${t.pairsWritingIt}${t.postRulesWritingIt.length ? "+" + t.postRulesWritingIt.length + "r" : ""}`).join(",");
    let extra = "";
    if (k === "SOFT HYPHEN") extra = ` W:${c.word.ok ? "ok" : (c.word.pdfInk.startsWith("ink = glyph of U+002D") ? "PDF shows '-' glyph" : c.word.pdfInk === "no ink" ? "PDF no ink" : c.word.verdict)}${c.word.emf.startsWith("fallback") ? " EMF " + c.word.emf : ""} RE:${c.richedit.typed.ok ? "ok" : c.richedit.typed.verdict.replace(/\(font advance.*\)/, "")}`;
    if (k === "NOT IN CMAP") extra = ` W:${c.word.pdf.replace("fallback:", "fb ")}/${c.word.emf.replace("fallback:", "fb ")} RE:${c.richedit.typed.verdict.replace("not in font cmap; ", "")}`;
    if (k === "OTHER") extra = ` W:${c.word.ok ? "ok" : c.word.verdict} RE:${c.richedit.typed.verdict}`;
    (g[k] = g[k] || []).push(`${c.cp}=${d}${tabs ? " [" + tabs + "]" : " [no table writes it]"}${extra}`);
  }
  lines.push(`### ${f.family} (${f.repFace}; tables main=${F.wiring.main}${F.wiring.worktree ? ", wt=" + F.wiring.worktree : ""})\n` +
    Object.entries(g).map(([k, v]) => `  ${k}: ${v.join("; ")}`).join("\n"));
}
fs.writeFileSync(path.join(root, "grouped-invisible.txt"), lines.join("\n"));
console.log(lines.join("\n").length);
