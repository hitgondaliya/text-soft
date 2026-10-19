// Digests from fam/<family>.json after finalize.js: final-invisible.txt, final-ascii.txt, final-alternatives.txt
// (families whose text is identical are merged as "A = B = C").
const fs = require("fs"), path = require("path");
const root = __dirname;
const S = JSON.parse(fs.readFileSync(path.join(root, "summary.json"), "utf8"));
const PUNCT = [0x2013, 0x2014, 0x2018, 0x2019, 0x201A, 0x201B, 0x201C, 0x201D, 0x201E, 0x2022, 0x2026, 0x00B7, 0x00A0, 0x00AD];
const v = c => parseInt(c.cp.slice(2), 16);
const label = c => { const x = v(c); return x > 0x20 && x < 0x7F ? String.fromCharCode(x) : c.cp; };

function shortDraws(d) {
  if (!d) return "?";
  let s = d.replace(/ \(tables disagree:.*$/, "").replace(/ \(disputed:.*$/, "").replace(/ \[[^\]]*\]/g, "");
  if (/^same outline as (U\+[0-9A-F]{4}), which draws: (.*)$/.test(s)) { const m = /^same outline as (U\+[0-9A-F]{4}), which draws: (.*)$/.exec(s); return `=${m[1]}:${shortDraws(m[2])}`; }
  if (/^not in the font's cmap/.test(s)) return "-";
  if (/^nothing: the glyph has no drawable outline/.test(s)) return "nothing (malformed glyph)";
  if (/^nothing: blank glyph/.test(s)) return "blank";
  if (/^nothing/.test(s)) return "blank";
  if (/^unidentified glyph/.test(s)) return "?";
  s = s.replace(/^Indic /, "").replace(/^script digit /, "").replace(/^look-alike /, "~").replace(/ by eye \(the Shri ligature shape\)/, " (eye)");
  s = s.replace(/^(zero-width )?piece of (\S+) \([^)]*\)/, (m, z, x) => `${z ? "0w-" : ""}piece of ${x}`);
  s = s.replace(/ \(U\+[0-9A-F]{4}[^)]*\)/, "").replace(/ \(a copy of [^)]*\)/, " (copy)").replace(/ \(left piece of ([^;)]*)[^)]*\)/, " (piece of $1)").replace(/ - the composer writes.*$/, "");
  return s;
}
function merge(items) {
  const g = new Map();
  for (const it of items) { const k = it.body; if (!g.has(k)) g.set(k, []); g.get(k).push(it.fam); }
  return [...g.entries()].map(([k, f]) => `${f.join(" = ")}: ${k}`);
}
const LEGEND = "LEGEND. C1 = C1 control code point: Word keeps the Shree font and the code but GDI draws NO ink (advance kept; Word's PDF export shows the WinAnsi bullet glyph for 81 8D 8F 90 9D, .notdef for 9E); RichEdit typed and RTF: no ink, advance kept. CS238 = Word draws it; RichEdit (typed and RTF alike) sets charset 238 on it, GDI binds Arial and the Latin letter is drawn instead. CM = Word draws it; RichEdit moves it to Cambria Math. SHY = U+00AD. NOTMAP = not in the font's cmap (Word falls back to another font; RichEdit as stated). NOTMAP-ASCII = ASCII not in the cmap (Word draws the real character in Cambria). MALFORMED = glyph with no drawable outline. OTHER = as stated. [table counts] = pairs of each wired forward table writing the code point (main checkout unless wt:).";
function cls(c) {
  const w = c.word, t = c.richedit.typed.verdict;
  if (c.kind === "unmapped-ascii") return "NOTMAP-ASCII";
  if (c.kind !== "mapped") return "NOTMAP";
  if (w.emf.includes("NO ink")) return "C1";
  if (c.cp === "U+00AD") return "SHY";
  if (/GDI binds it to Arial/.test(t)) return "CS238";
  if (/Cambria Math/.test(t)) return "CM";
  if (c.glyphMalformed) return "MALFORMED";
  return "OTHER";
}
// collapse runs of consecutive code points with the same text: "U+0120-U+012F=? [no table writes it]"
function ranges(items) {
  const out = [];
  for (const it of items) {
    const last = out[out.length - 1];
    if (last && last.rest === it.rest && it.v === last.to + 1) { last.to = it.v; continue; }
    out.push({ from: it.v, to: it.v, rest: it.rest });
  }
  const h = x => "U+" + x.toString(16).toUpperCase().padStart(4, "0");
  return out.map(r => (r.from === r.to ? h(r.from) : `${h(r.from)}-${h(r.to)}`) + r.rest);
}
function tabs(c) {
  const t = (c.tables || []).filter(t => t.pairsWritingIt > 0 || (t.postRulesWritingIt || []).length > 0)
    .map(t => `${t.table.replace("unicode-to-shree-", "").replace("main:", "").replace("worktree:", "wt:")} ${t.pairsWritingIt}${(t.postRulesWritingIt || []).length ? "+" + t.postRulesWritingIt.length + "rule" : ""}`);
  return t.length ? t.join(",") : "no table writes it";
}
function reShort(r) {
  const v = r.verdict;
  if (r.ok) return "ok";
  if (/\.notdef box/.test(v)) return ".notdef box";
  if (/Shree glyph of U\+001F/.test(v) || /real character drawn by SHREE-(BAN|ASS)-/.test(v)) return "draws ॥ (GDI default char 0x1F)";
  if (/real character drawn by Arial/.test(v)) return "moved to Arial, the real character";
  if (/no ink, advance 0/.test(v) || /advance 0 px/.test(v)) return "hidden (no advance)";
  if (/^no ink/.test(v)) return "blank, advance kept";
  if (/blank as designed/.test(v)) return "blank (as the font)";
  return v.replace(/^not in font cmap; /, "");
}

const inv = [], asc = [], alt = [];
for (const sf of S.families) {
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", sf.family + ".json"), "utf8"));
  const face = F.repFace;
  // invisible
  const g = {};
  for (const c of F.codepoints.filter(x => x.fails)) {
    const k = cls(c);
    let extra = "";
    if (k === "SHY") extra = ` Word:${c.word.ok ? "ok" : c.word.pdfInk.startsWith("ink = glyph of U+002D") ? "screen ok, PDF shows the font's '-' glyph" : c.word.emf === "shree-font, same code" && c.word.pdfInk === "no ink" ? "screen draws it, PDF draws nothing (the font's '-' is blank)" : c.word.emf.startsWith("fallback") ? c.word.emf.replace("fallback:", "falls back to ") : c.word.verdict} RichEdit:${reShort(c.richedit.typed)}`;
    if (k === "OTHER" && c.glyphEmpty && c.word.emf === "shree-font, same code") extra = ` Word:screen blank as the font, PDF shows ${c.word.pdfInk.replace(/^ink = /, "").replace(/ although.*$/, "")} RichEdit:${reShort(c.richedit.typed)}`;
    if (k === "NOTMAP" || k === "NOTMAP-ASCII") extra = ` Word:${k === "NOTMAP-ASCII" ? "Cambria" : c.word.emf.includes("NO ink") ? "Shree font kept, GDI draws nothing" : c.word.emf.replace("fallback:", "")} RichEdit:${reShort(c.richedit.typed)}${reShort(c.richedit.rtf) !== reShort(c.richedit.typed) ? "/RTF " + reShort(c.richedit.rtf) : ""}`;
    if (k === "OTHER" && !extra) extra = ` Word:${c.word.ok ? "ok" : c.word.verdict.replace("PDF: expected-glyph; ", "PDF ")} RichEdit:${reShort(c.richedit.typed)}`;
    (g[k] = g[k] || []).push({ v: parseInt(c.cp.slice(2), 16), rest: `=${k.startsWith("NOTMAP") ? "-" : shortDraws(c.draws)} [${tabs(c)}]${extra}` });
  }
  inv.push({ fam: sf.family, body: `(${F.wiring.main ? "tables " + F.wiring.main + (F.wiring.worktree ? ", wt " + F.wiring.worktree : "") : "no table wired"}) ` + Object.entries(g).map(([k, l]) => `${k}: ${ranges(l.sort((a, b) => a.v - b.v)).join("; ")}`).join(" | ") });
  // alternatives
  const alts = F.codepoints.filter(x => x.fails && x.kind === "mapped").map(c => {
    const a = Array.isArray(c.alternatives) ? c.alternatives : [];
    const good = a.filter(x => x.wordPdf === "expected-glyph" && x.richeditTypedF1 >= 0.9 && !/^U\+00[01][0-9A-F]$/.test(x.cp));
    const bad = a.filter(x => !good.includes(x));
    return good.length || bad.length ? `${c.cp} -> ${good.map(x => x.cp + " (identical outline, drawn by Word and RichEdit)").concat(bad.map(x => x.cp + (/^U\+00(0|1)/.test(x.cp) ? " (a C0 control: unusable)" : " (not drawn either)"))).join(", ")}` : null;
  }).filter(Boolean);
  const none = F.codepoints.filter(x => x.fails && (x.kind !== "mapped" || !Array.isArray(x.alternatives) || !x.alternatives.length)).map(x => x.cp);
  alt.push({ fam: sf.family, body: (alts.length ? alts.join("; ") + "; " : "") + (none.length ? "none for " + none.join(" ") : "") });
  // ascii + punct
  const groups = {};
  const cps = F.codepoints.filter(c => (v(c) >= 0x20 && v(c) <= 0x7E) || PUNCT.includes(v(c))).sort((a, b) => v(a) - v(b));
  for (const c of cps) {
    const d = shortDraws(c.draws);
    let k;
    if (c.kind !== "mapped") k = "not in cmap";
    else if (d === "blank" || d.startsWith("nothing")) k = "nothing (blank)";
    else if (d === "itself" || /^itself/.test(d)) k = "itself";
    else if (/^script digit/.test(c.draws)) k = "script digit";
    else if (/piece of/.test(d)) k = "piece";
    else if (d === "?") k = "unidentified";
    else k = "glyph";
    const lab = label(c);
    (groups[k] = groups[k] || []).push(k === "glyph" || k === "script digit" || k === "piece" ? `${lab}=${d.replace(/^0w-piece of /, "0w:").replace(/^piece of /, "")}` : lab);
  }
  const order = ["itself", "script digit", "glyph", "piece", "nothing (blank)", "unidentified", "not in cmap"];
  asc.push({ fam: sf.family, body: order.filter(k => groups[k]).map(k => `${k}: ${groups[k].join(" ")}`).join(" || ") });
}
// what a converted text shows when its Unicode INPUT holds an ASCII or cp1252 punctuation character
const inp = [];
for (const sf of S.families) {
  const F = JSON.parse(fs.readFileSync(path.join(root, "fam", sf.family + ".json"), "utf8"));
  const blank = [], missing = [], punctOther = [], lettersOther = [], pairs = [];
  const byCp = new Map(F.codepoints.map(c => [c.cp, c]));
  const drawnAs = codes => codes.split(" ").filter(Boolean).map(h => { const o = byCp.get(h); if (!o) return "-"; const d = shortDraws(o.draws); return d === "itself" || /^itself/.test(d) ? String.fromCodePoint(parseInt(h.slice(2), 16)) : o.kind !== "mapped" ? "(not in cmap)" : d === "blank" ? "(blank)" : d.replace(/^0w-piece of /, "0w:").replace(/^piece of /, "piece:"); }).join(" ");
  for (const c of F.codepoints.filter(c => c.asInput).sort((a, b) => v(a) - v(b))) {
    const x = v(c), lab = label(c);
    const pr = c.asInput.filter(a => /^pair/.test(a.does));
    if (pr.length) { pairs.push(`${lab}->${pr.map(a => { const out = a.does.replace("pair -> ", ""); return out + (c.asInput.length > 1 ? "(" + a.table.split(":")[0].replace("worktree", "wt") + ")" : "") + " [drawn: " + drawnAs(out) + "]"; }).join(" / ")}`); if (pr.length === c.asInput.length) continue; }
    if (x === 0x20) continue;
    if (c.kind !== "mapped") { missing.push(lab); continue; }
    const d = shortDraws(c.draws);
    if (d === "blank" || d.startsWith("nothing")) { blank.push(lab); continue; }
    if (/^itself/.test(d) || /^script digit/.test(c.draws)) continue;
    if (/[A-Za-z0-9]/.test(lab) && lab.length === 1) lettersOther.push(lab); else punctOther.push(`${lab}=${d.replace(/^0w-piece of /, "0w:").replace(/^piece of /, "piece:")}`);
  }
  inp.push({ fam: sf.family, body: `${F.wiring.main ? "" : "(no table wired) "}copied through and drawn as NOTHING: ${blank.join(" ") || "none"} || copied through but NOT IN THE CMAP: ${missing.join(" ") || "none"} || punctuation copied through that draws another glyph: ${punctOther.join(" ") || "none"} || letters copied through that draw an Indic glyph: ${lettersOther.length} || pairs for these inputs: ${pairs.join(" ") || "none (everything is copied through)"}` });
}
fs.writeFileSync(path.join(root, "final-input.txt"), ["What the converter writes when the Unicode input holds an ASCII or cp1252 punctuation character: FontConverter copies a character that has no pair through unchanged (src/FontSoftware.Core/Conversion/FontConverter.cs Convert(), the 'No mapping: pass the character through' branch), so the legacy font then draws its own glyph at that code. Tables are the wired forward tables (main checkout; Bangla/Assamese also the worktree table).", ...merge(inp)].join("\n"));
fs.writeFileSync(path.join(root, "final-invisible.txt"), [LEGEND, ...merge(inv)].join("\n"));
fs.writeFileSync(path.join(root, "final-ascii.txt"), merge(asc).join("\n"));
fs.writeFileSync(path.join(root, "final-alternatives.txt"), merge(alt).join("\n"));
for (const f of ["final-invisible.txt", "final-ascii.txt", "final-alternatives.txt"]) console.log(f, fs.statSync(path.join(root, f)).size);
