// Compact per-family lines for the structured answer: failing code points, what Word / RichEdit do, what the tables write there.
const fs = require("fs");
const path = require("path");
const root = __dirname;
const S = JSON.parse(fs.readFileSync(path.join(root, "summary.json"), "utf8"));
const inv = [], alt = [], asc = [];

function wordShort(c) {
  if (c.word.ok) return "W ok";
  const v = c.word;
  let p = v.pdf.startsWith("fallback") ? "PDF " + v.pdf.replace("fallback:", "fallback ") :
    v.pdfInk.startsWith("ink = glyph of U+2022") ? "PDF bullet(U+2022 glyph)" :
    v.pdfInk.includes(".notdef") ? "PDF .notdef box" :
    v.pdfInk.startsWith("ink = glyph of U+002D") ? "PDF '-' glyph" :
    v.pdfInk === "no ink" ? "PDF no ink" :
    v.pdfInk.startsWith("ink = expected") || v.pdfInk.startsWith("no ink (glyph is blank") ? "PDF ok" : "PDF " + v.pdfInk;
  let e = v.emf.includes("NO ink") ? "EMF no ink" : v.emf.startsWith("fallback") ? "EMF " + v.emf.replace("fallback:", "fallback ") : v.emf === "shree-font, same code" ? "EMF ok" : "EMF " + v.emf;
  return "W " + p + ", " + e;
}
function reShort(r) {
  if (r.ok) return "ok";
  const v = r.verdict;
  if (v.includes("GDI binds it to Arial")) return "cs238->Arial draws Latin";
  if (v.includes("Cambria Math")) return "->Cambria Math";
  if (v.startsWith("no ink, advance")) return "blank+advance";
  if (v.includes("no ink, advance 0")) return "hidden (0 advance)";
  if (v.includes(".notdef")) return ".notdef box";
  if (v.includes("not in font cmap")) return v.replace("not in font cmap; ", "unmapped: ");
  return v;
}
for (const f of S.families) {
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", f.family + ".json"), "utf8"));
  const items = [];
  for (const c of F.codepoints.filter(x => x.fails)) {
    const t = (c.tables || []).map(t => `${t.table.replace("unicode-to-shree-", "")}:${t.pairsWritingIt}${t.postRulesWritingIt.length ? "+" + t.postRulesWritingIt.length + "rule" : ""}`).join(",");
    const ex = (c.tables || []).flatMap(t => t.examples.slice(0, 2)).slice(0, 2).join("; ");
    const reT = reShort(c.richedit.typed), reR = reShort(c.richedit.rtf);
    const re = reT === reR ? "RE " + reT : `RE typed ${reT}, RTF ${reR}`;
    const d = c.draws.replace(/ \[reverse [^\]]+\]/g, "").replace(/ \[composer [^\]]+\]/g, "").split("; ")[0];
    items.push(`${c.cp}${c.kind !== "mapped" ? "(" + c.kind + ")" : ""} {${d}} ${wordShort(c)}; ${re}; tables ${t || "none wired"}${ex ? " e.g. " + ex : ""}`);
  }
  inv.push(`${f.family} [${f.repFace}; Word ok ${f.wordOk}/${f.mapped + f.testedUnmapped}, RE typed ok ${f.richeditTypedOk}, RTF ok ${f.richeditRtfOk}]: ` + (items.length ? items.join(" | ") : "none"));
  const alts = F.codepoints.filter(x => x.fails).map(c => `${c.cp}: ${Array.isArray(c.alternatives) ? c.alternatives.map(a => a.cp + (a.wordPdf === "expected-glyph" && a.richeditTypedF1 >= 0.9 ? " (visible in Word+RichEdit)" : " (not usable)")).join(", ") : "none"}`);
  alt.push(`${f.family}: ${alts.join("; ")}`);
  // ASCII/punct grouped by kind of drawing
  const groups = {};
  for (const line of f.asciiAndPunct) {
    const m = line.match(/^(U\+[0-9A-F]{4})(?: '(.)')? : (.*)$/);
    if (!m) continue;
    let d = m[3];
    let k;
    if (d.startsWith("itself")) k = "itself";
    else if (d.startsWith("nothing")) k = "nothing (blank)";
    else if (d.startsWith("not in")) k = "not in cmap";
    else if (d.startsWith("script digit")) k = "script digit";
    else if (d.startsWith("unidentified")) k = "unidentified";
    else k = "glyph";
    const short = d.replace(/ \[reverse [^\]]+\]/g, "").replace(/ \[composer [^\]]+\]/g, "").split("; ")[0].replace(/^(Indic|look-alike|script digit) /, "").replace(/ \(U\+[0-9A-F ]+\)$/, "");
    const label = m[2] !== undefined && m[2] !== " " ? m[2] : m[1];
    (groups[k] = groups[k] || []).push(k === "glyph" || k === "script digit" ? `${label}=${short}` : label);
  }
  asc.push(`${f.family}: ` + Object.entries(groups).map(([k, v]) => `${k}: ${v.join(" ")}`).join(" || "));
}
fs.writeFileSync(path.join(root, "compact-invisible.txt"), inv.join("\n"));
fs.writeFileSync(path.join(root, "compact-alternatives.txt"), alt.join("\n"));
fs.writeFileSync(path.join(root, "compact-ascii.txt"), asc.join("\n"));
console.log(inv.join("\n").length, alt.join("\n").length, asc.join("\n").length);
