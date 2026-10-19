// Resume pass (round 3, second session):
//  1. merge the xa-<family> runs (ASCII 0x20-0x7E code points NOT in the cmap; Word PDF+EMF and RichEdit typed+RTF) into fam/<family>.json
//  2. re-resolve "draws" for the step-3 scope (ASCII, cp1252 punctuation, every failing code point) with one rule order:
//     blank glyph (font) > Bangla family-differing code (fontCheck of ban7-groundtruth.json + rendered check bangla-check.png)
//     > composer single-byte truth (where the family uses the composer's encoding) > reverse tables > piece of a composer
//     multi-byte answer > piece of a forward-table pair > unidentified. The old string is kept as drawsOld.
//  3. asInput: what each wired forward table does with an ASCII or cp1252-punctuation INPUT character (pair or pass-through).
//  4. rebuild summary.json family entries and the digests final-invisible.txt, final-ascii.txt, final-alternatives.txt.
const fs = require("fs"), path = require("path");
const root = __dirname, famDir = path.join(root, "fam");
const REPO = "C:/Users/harde/Desktop/Windows Font Software";
const MAIN = REPO + "/assets/config/mappings", WT = REPO + "/.claude/worktrees/wf_607a9ebd-c6c-2/assets/config/mappings";
const TRUTH = "C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/624671f5-ee6d-402b-b810-05b52f236677/scratchpad/shree7";

const hex = v => "U+" + v.toString(16).toUpperCase().padStart(4, "0");
const cpsOf = s => [...s].map(c => c.codePointAt(0));
const cpsStr = s => cpsOf(s).map(hex).join(" ");
const isMarker = c => c === 0x200C || c === 0x200D || c === 0x25CC || (c >= 0xFDD0 && c <= 0xFDEF);
const core = s => cpsOf(s).filter(c => !isMarker(c));
const coreKey = s => core(s).join(",");
const isIndic = c => c >= 0x0900 && c <= 0x0DFF;
const isIndicDigit = c => isIndic(c) && (c & 0x7F) >= 0x66 && (c & 0x7F) <= 0x6F;
const PUNCT = [0x2013, 0x2014, 0x2018, 0x2019, 0x201A, 0x201B, 0x201C, 0x201D, 0x201E, 0x2022, 0x2026, 0x00B7, 0x00A0, 0x00AD];
const inScope = (cp, fails) => (cp >= 0x20 && cp <= 0x7E) || PUNCT.includes(cp) || fails;
const readJson = p => JSON.parse(fs.readFileSync(p, "utf8").replace(/^\uFEFF/, ""));
const show = s => core(s).length ? String.fromCodePoint(...core(s)) : s;

function classify(meaning, cp) {
  const cs = core(meaning);
  if (cs.length === 0) return "nothing (" + (meaning.length ? cpsStr(meaning) : "empty") + ")";
  if (cs.length === 1 && cs[0] === cp) return "itself";
  if (cs.length === 1 && isIndicDigit(cs[0])) return `script digit ${show(meaning)} (${hex(cs[0])})`;
  if (cs.some(isIndic)) return `Indic ${show(meaning)} (${cs.map(hex).join(" ")})`;
  return `look-alike ${show(meaning)} (${cs.map(hex).join(" ")})`;
}

// ---------- tables
const WIRING = {
  "SHREE-GUJ7": ["shree-guj7", null], "Shree-Guj7-00x": ["shree-guj7", null], "SHREE-GUJ": ["shree-guj456", null],
  "SHREE-DEV7": ["shree-dev7", null], "Shree-Dev7-00x": ["shree-dev7", null], "SHREE-DEV": ["shree-dev456", null],
  "SHREE-SAN7": ["shree-san7", null],
  "SHREE-BAN7": ["shree-ban7", "shree-ban7"], "Shree-Ban7-00x": ["shree-ban7", "shree-ban7"], "SHREE-BAN": ["shree-ban7", "shree-ban456"],
  "SHREE-ASS7": ["shree-ban7", "shree-ass7"], "SHREE-ASS": ["shree-ban7", "shree-ass456"], "Shree-Ass7-00x": ["shree-ban7", "shree-ass7w"],
  "SHREE-ORI7": ["shree-ori7", null], "SHREE-ORI": ["shree-ori7", null], "Shree-Ori7-00x": ["shree-ori7", null],
  "SHREE-PUN7": ["shree-pun7", null], "SHREE-PUN": ["shree-pun7", null],
  "SHREE-TAM7": ["shree-tam7", null], "SHREE-TAM": ["shree-tam7", null], "Shree-Tam7-00x": ["shree-tam7", null],
  "SHREE-TEL7": ["shree-tel7", null], "SHREE-TEL": ["shree-tel7", null], "Shree-Tel7-00x": ["shree-tel7", null],
  "SHREE-KAN7": ["shree-kan7", null], "SHREE-KAN": ["shree-kan7", null], "Shree-Kan7-00x": ["shree-kan7", null],
  "SHREE-MAL7": ["shree-mal7", null], "Shree-Mal7-00x": ["shree-mal7", null],
};
const tcache = {};
function table(where, name) {
  const k = where + ":" + name;
  if (!tcache[k]) {
    const j = readJson(path.join(where === "main" ? MAIN : WT, name + ".json"));
    tcache[k] = { label: k, pairs: j.pairs.filter(p => p.from && !p.unmapped), dropped: new Set(j.pairs.filter(p => p.from && p.unmapped).map(p => p.from)), post: j.postRules || [], pre: j.preRules || [] };
  }
  return tcache[k];
}
const fwdTables = fam => { const [m, w] = WIRING[fam] || [null, null]; const r = []; if (m) r.push(table("main", "unicode-to-" + m)); if (w) r.push(table("worktree", "unicode-to-" + w)); return r; };
const revTables = fam => { const [m, w] = WIRING[fam] || [null, null]; const r = []; if (w) r.push(table("worktree", w + "-to-unicode")); if (m) r.push(table("main", m + "-to-unicode")); return r; };

// ---------- composer truth
const truthKey = fam => { const u = fam.toUpperCase(); if (u.includes("GUJ")) return "guj7"; if (u.includes("SAN")) return "san7"; if (u.includes("DEV")) return "dev7"; if (u.includes("BAN") || u.includes("ASS")) return "ban7"; return null; };
const truths = {};
function truth(key) {
  if (!key) return null;
  if (truths[key]) return truths[key];
  const T = readJson(path.join(TRUTH, key + "-groundtruth.json"));
  const single = new Map(), multi = new Map();
  for (const [u, v] of Object.entries(T.truth)) {
    const seqs = [v.preferred, ...(v.accepted || []).filter(a => a !== v.preferred)];
    seqs.forEach((s, i) => {
      if (!s) return;
      const toks = s.split(" ");
      if (toks.length === 1) { if (!single.has(toks[0])) single.set(toks[0], []); single.get(toks[0]).push({ u, pref: i === 0 }); }
      else if (i === 0) for (const t of new Set(toks)) {
        const cur = multi.get(t), cand = { u, bytes: s, n: core(u).length, len: toks.length };
        if (!cur || cand.n < cur.n || (cand.n === cur.n && cand.len < cur.len)) multi.set(t, cand);
      }
    });
  }
  return (truths[key] = { single, multi, fontCheck: T.counts && T.counts.fontCheck });
}
// codes whose 4/5/6 glyph differs from the 7 glyph (family-diff-456-vs-7.json): the 7 composer does not name them there
const EXCLUDE = { "SHREE-GUJ": [0x91, 0x92, 0x93, 0x94, 0xB6], "SHREE-DEV": [0x91, 0x92, 0xB3, 0xB6] };
const BAN_DIFF = [0x25, 0x40, 0x96, 0x97, 0xA5, 0x8C, 0xA8, 0xFF, 0xAF, 0xA4];
// Bangla family-differing codes in the step-3 scope: fontCheck (ban7-groundtruth.json counts.fontCheck, rendered) and
// bangla-check.png / bangla-check2.png (this session, GDI+ render of every Bangla family's face): 4/5/6 layout in
// SHREE-BAN, SHREE-ASS and Shree-Ass7-00x; swapped in SHREE-BAN7, SHREE-ASS7 and Shree-Ban7-00x.
function banOverride(fam, by) {
  const g = ["SHREE-BAN", "SHREE-ASS"].includes(fam) ? "456" : ["SHREE-BAN7", "SHREE-ASS7", "Shree-Ban7-00x"].includes(fam) ? "7" : fam === "Shree-Ass7-00x" ? "aw" : null;
  if (!g) return null;
  const hookTxt = "Indic half-ন hook (left piece of ন্ট ন্ঠ ন্ড ন্দ; not a letter by itself)";
  switch (by) {
    case 0x25: return g === "7" ? "zero-width below mark (not %)" : "itself";
    case 0x40: return g === "7" ? hookTxt + " - the composer writes ঃ here, which is right only in the 4/5/6 layout" : "Indic ঃ (U+0983)";
    case 0x96: return g === "7" ? "Indic ঃ (U+0983)" : hookTxt;
    case 0x97: return g === "7" ? "spacing ৺-like sign (U+09FA look-alike; the subscript ্ন is at U+00A5 here)" : "Indic zero-width subscript ্ন (U+09CD U+09A8)";
    case 0x8C: return g === "456" ? "Indic ধ্ম (U+09A7 U+09CD U+09AE)" : "Indic ব (U+09AC) (a copy of ব; this family has no ধ্ম glyph)";
    case 0xA5: return g === "7" ? "Indic zero-width subscript ্ন (U+09CD U+09A8)" : "spacing ৺-like sign (U+09FA look-alike)";
    case 0xA8: return g === "456" ? "Indic ব্ঝ (U+09AC U+09CD U+099D)" : "Indic দ (U+09A6) (a copy of দ)";
    case 0xFF: return g === "456" ? null : "Indic ড়্গ (U+09DC U+09CD U+0997) (a spacing ligature)";
  }
  return null;
}

// glyphs checked by eye this session (check4.png): the composer's single-byte answer is a quirk there
const EYE = {
  "SHREE-SAN7 U+006E": "Indic zero-width subscript ्न (U+094D U+0928) [reverse main:shree-san7-to-unicode; eye check check4.png: a mark under the previous letter, not a quote; the San composer writes ” as 6E, a quirk]",
};
function resolve(fam, c) {
  const cp = parseInt(c.cp.slice(2), 16);
  if (c.kind !== "mapped") return { draws: c.draws, src: "not in cmap" };
  if (c.glyphEmpty) return { draws: c.draws, src: "font: blank glyph" };
  const by = c.byte ? parseInt(c.byte, 16) : -1;
  const revs = [];
  for (const t of revTables(fam)) {
    const p = t.pairs.find(p => cpsOf(p.from).length === 1 && cpsOf(p.from)[0] === cp);
    if (p) revs.push({ src: "reverse " + t.label.replace(/^(main|worktree):/, "$1:"), m: p.to });
  }
  const isBan = truthKey(fam) === "ban7";
  if (isBan && BAN_DIFF.includes(by)) {
    const o = banOverride(fam, by);
    if (o) {
      const key = (o.match(/^.*?\(U\+[0-9A-F]{4}(?: U\+[0-9A-F]{4})*\)/) || [o])[0];
      const disagree = revs.filter(r => classify(r.m, cp) !== key).map(r => `${r.src} says ${classify(r.m, cp)}`);
      return { draws: `${o} [Bangla family-differing code ${c.byte}: fontCheck + rendered check]` + (disagree.length ? ` (tables disagree: ${disagree.join("; ")})` : ""), src: "family-differing code (rendered)" };
    }
  }
  const eye = EYE[fam + " " + c.cp];
  if (eye) return { draws: eye, src: "eye check (rendered)" };
  const tk = truthKey(fam), T = truth(tk);
  const applies = T && by >= 0 && !(EXCLUDE[fam] || []).includes(by) && !(isBan && BAN_DIFF.includes(by));
  const bhex = by >= 0 ? by.toString(16).toUpperCase().padStart(2, "0") : null;
  if (applies && T.single.has(bhex)) {
    const list = T.single.get(bhex).slice().sort((a, b) => (b.pref - a.pref) || (core(a.u).length - core(b.u).length));
    // the shortest meaning among the preferred answers (a longer string written as this one byte is a composer quirk)
    const prefs = list.filter(x => x.pref); const pool = prefs.length ? prefs : list;
    const best = pool.slice().sort((a, b) => core(a.u).length - core(b.u).length)[0];
    const cls = classify(best.u, cp);
    const disagree = revs.filter(r => coreKey(r.m) !== coreKey(best.u) && classify(r.m, cp) !== cls).map(r => `${r.src} says ${classify(r.m, cp)}`);
    return { draws: `${cls} [composer ${tk}: byte ${bhex} = ${pool.map(x => x.u).slice(0, 3).join(" | ")}]` + (disagree.length ? ` (tables disagree: ${disagree.join("; ")})` : ""), src: "composer" };
  }
  if (revs.length) {
    const cls = revs.map(r => classify(r.m, cp));
    const uniq = [...new Set(revs.map(r => coreKey(r.m)))];
    if (uniq.length === 1) return { draws: `${cls[0]} [${revs.map(r => r.src).join(", ")}]`, src: "reverse table" };
    return { draws: `${cls[0]} [${revs[0].src}] (disputed: ${revs.slice(1).map((r, i) => `${r.src} says ${cls[i + 1]}`).join("; ")})`, src: "reverse table (disputed)" };
  }
  const vis = /^itself \(visual: matches (.*?) at F1 ([0-9.]+)/.exec(c.draws || "");
  if (vis && parseFloat(vis[2]) >= 0.9) return { draws: c.draws, src: "visual (Latin font match)" };
  const zw = c.advance != null && c.advance <= 20 ? "zero-width " : "";
  if (applies && T.multi.has(bhex)) {
    const m = T.multi.get(bhex);
    return { draws: `${zw}piece of ${show(m.u)} (${core(m.u).map(hex).join(" ")}) [composer ${tk} writes it as ${m.bytes}]`, src: "piece (composer)" };
  }
  const ch = String.fromCodePoint(cp);
  let best = null;
  for (const t of fwdTables(fam)) for (const p of t.pairs) if (p.to.includes(ch)) {
    const n = core(p.from).length;
    if (n && (!best || n < best.n || (n === best.n && p.to.length < best.p.to.length))) best = { n, p, t };
  }
  if (best) return { draws: `${zw}piece of ${show(best.p.from)} (${core(best.p.from).map(hex).join(" ")}) [${best.t.label} writes ${cpsStr(best.p.to)}]`, src: "piece (forward table)" };
  const e2 = eyeLast(fam, cp);
  if (e2) return { draws: `${e2} [eye check, eye-*.png; no table pair, no composer truth]`, src: "eye check (rendered)" };
  if (/^itself \(visual/.test(c.draws || "")) return { draws: c.draws, src: "visual (weak Latin match)" };
  return { draws: c.draws, src: "unidentified" };
}

// Glyphs with no table pair and no composer truth, looked at this session (eye-0/50/100/150.png: the glyph by
// code point in the family's face beside the same character in Arial).
function eyeLast(fam, cp) {
  const F = fam.toUpperCase();
  const is = (...k) => k.some(x => F.includes(x));
  if ([0x21, 0x25, 0x28, 0x29, 0x2B, 0x2C, 0x2D, 0x2F, 0x3A, 0x3B, 0x3D, 0x3F].includes(cp) && !is("BAN", "ASS", "DEV", "SAN")) return "itself";
  if (cp === 0x2A) return is("ORI") ? "look-alike ✻ (U+273B, an eight-petal asterisk)" : fam === "Shree-Guj7-00x" ? "itself" : "look-alike ★ (U+2605 black star)";
  if (cp === 0xB7) return "look-alike ● (U+25CF, a large bullet; the font's U+00B7 is not a middle dot)";
  if (cp === 0x3C && is("ORI", "PUN")) return "look-alike ₹ (U+20B9 rupee sign)";
  if (cp === 0x40 && is("PUN")) return "look-alike ऽ (U+093D avagraha shape, S-like)";
  if ((cp === 0x75 || cp === 0x77) && is("PUN")) return "unnamed below-base sign (Gurmukhi, " + (cp === 0x75 ? "single hook below" : "double hook below") + ")";
  if (is("DEV")) {
    if (cp === 0x9D) return "unnamed small high bar (a mark, not a letter)";
    if (cp === 0x152) return "unnamed diagonal stroke at the baseline (looks like the ्र rakar sign)";
    if (cp === 0x2014) return "unnamed caret-shaped stroke at the baseline (looks like the caret form of the ्र rakar used under ट ड)";
  }
  if (is("GUJ")) { if (cp === 0x7E) return "unnamed Gujarati-like glyph"; if (cp === 0x90) return "unnamed small high bar (a mark, not a letter)"; }
  if (is("BAN", "ASS")) { if (cp === 0x5E) return "unnamed Bangla conjunct glyph (জ-based, like জ্জ্ব)"; if (cp === 0x7E) return "unnamed Bangla piece (a hook)"; }
  if (is("KAN")) { if (cp === 0x7D || cp === 0x201D) return "unnamed Kannada-like glyph"; if ([0x152, 0x192, 0x201E].includes(cp)) return "unnamed below-base piece (Kannada)"; }
  if (is("MAL") && cp === 0x153) return "unnamed Malayalam-like glyph";
  if (is("ORI") && cp === 0x23) return "unnamed small low hook (a piece, not #)";
  if (is("TAM")) { if (cp === 0x153) return "unnamed Tamil-like glyph"; if (cp === 0x178) return "Indic ஸ்ரீ (U+0BB8 U+0BCD U+0BB0 U+0BC0) by eye (the Shri ligature shape)"; }
  if (is("TEL") && cp === 0x161) return "unnamed Telugu-like glyph";
  return null;
}

// ---------- RichEdit / Word verdicts for unmapped lines (Report.cs logic, !inCmap branch)
function wordUnmapped(w, e, play) {
  const ink = w ? w.inkPx : -1;
  const pdfInk = ink === 0 ? "no ink" : w && w.bestGid === 0 && w.bestScore >= 0.9 ? "ink = the font's .notdef box (gid 0)" : w && w.iouFallback >= 0.9 ? `ink = the real character in ${w.pdf.split(":").pop()}` : w && w.bestScore >= 0.9 ? `ink = Shree glyph of ${w.bestGidCps}` : "ink unidentified";
  const emfV = e ? e.emf : "no-data";
  const codes = (w && w.runs || []).flatMap(r => r.codes.map(cc => `${r.font}/${r.subtype} code ${cc.code} -> ${cc.match}`));
  return { ok: false, verdict: `not in font cmap; PDF: ${w ? w.pdf : "no-data"}; ${pdfInk}; EMF: ${emfV}`, pdf: w ? w.pdf : "no-data", pdfCodes: codes.join("; "), pdfInk, pdfInkF1: null, emf: emfV, emfPlaybackInk: play ? play.blackInkPx : -1, emfOuts: e ? e.outs : null, advanceUnitsPdf: w ? w.advanceUnits : null, advanceUnitsEmf: e ? e.advanceUnits : null };
}
function reUnmapped(m, hexcp, face, expFace) {
  if (!m) return { verdict: "no-data", ok: false };
  const faces = m.faces.map(x => x.split(":")[1]);
  const parts = ["not in font cmap"];
  if (m.landed !== hexcp.slice(2)) parts.push(`text stored as [${m.landed}]`);
  const faceOk = faces.length > 0 && faces.every(fc => fc.split("/")[0].toLowerCase() === face.toLowerCase());
  if (!faceOk || faces.some(fc => fc !== expFace)) parts.push(`font set to ${[...new Set(faces)].join(",")}` + (m.gdiBindsOtherTo ? ` (GDI binds it to ${m.gdiBindsOtherTo})` : ""));
  if (m.inkPx === 0) parts.push(`no ink, advance ${m.advancePx} px`);
  else if (m.bestGid === 0 && m.bestScore >= 0.9) parts.push("ink = the font's .notdef box (gid 0)");
  else if (m.bestGid === 0 && m.bestScore >= 0.8) parts.push(`ink = the font's .notdef box (gid 0; F1 ${m.bestScore.toFixed(2)}, crop checked by eye)`);
  else if (m.iouOtherFace >= 0.9 && !((m.gdiBindsOtherTo || "").toLowerCase() === face.toLowerCase() && m.bestScore >= 0.9)) parts.push(`ink = the real character drawn by ${m.gdiBindsOtherTo || m.otherFace}`);
  else if (m.bestScore >= 0.9) parts.push(`ink = Shree glyph of ${m.bestGidCps}` + (m.bestGidCps === "U+001F" ? " (GDI's default character: usFirstCharIndex 0x20 - 1 = 0x1F, which these fonts map to ॥)" : ""));
  else parts.push("ink unidentified");
  return { verdict: parts.join("; "), ok: false, landed: m.landed, faces: faces.join(","), inkPx: m.inkPx, f1Expected: null, advancePx: m.advancePx };
}

// ---------- main
const S = readJson(path.join(root, "summary.json"));
const xaList = readJson(path.join(root, "xa-list.json"));
const changes = [];
const famObjs = {};
for (const sf of S.families) {
  const fam = sf.family;
  const fp = path.join(famDir, fam + ".json");
  const F = readJson(fp);
  // 1. merge the xa run (idempotent: drop earlier merged lines first)
  F.codepoints = F.codepoints.filter(c => c.kind !== "unmapped-ascii");
  const xa = xaList.find(x => x.name === "xa-" + fam);
  if (xa) {
    const W = readJson(path.join(famDir, xa.name + ".word.json")), E = readJson(path.join(famDir, xa.name + ".emf.json"));
    const P = readJson(path.join(root, "emfplay", xa.name, "emfplay.json")), R = readJson(path.join(famDir, xa.name + ".richedit.json"));
    for (const h of xa.cps) {
      const cp = parseInt(h.slice(2), 16), ch = String.fromCodePoint(cp);
      const w = W.find(x => x.cp === h), e = E.find(x => x.cp === h), pl = P.find(x => x.cp === h), r = R.find(x => x.cp === h);
      const word = wordUnmapped(w, e, pl);
      const typed = reUnmapped(r && r.typed, h, F.repFace, F.richeditFontFace), rtf = reUnmapped(r && r.rtf, h, F.repFace, F.richeditFontFace);
      F.codepoints.push({
        cp: h, gid: null, kind: "unmapped-ascii", byte: cp.toString(16).toUpperCase(), glyphEmpty: null, glyphMalformed: null, advance: null, sameGlyphAs: null, sameOutlineAs: null,
        word, richedit: { typed, rtf },
        draws: "not in the font's cmap (another font draws the real character, or nothing)",
        fails: true,
        tables: fwdTables(fam).map(t => ({ table: t.label.replace("main:", "main:").replace("worktree:", "worktree:"), pairsWritingIt: t.pairs.filter(p => p.to.includes(ch)).length, examples: t.pairs.filter(p => p.to.includes(ch)).slice(0, 8).map(p => `${p.from} (${cpsStr(p.from)}) -> ${cpsStr(p.to)}`), postRulesWritingIt: t.post.filter(p => (p.replace || "").includes(ch)).map(p => `${p.pattern} => ${p.replace}`) })),
        alternatives: "none",
        evidence: { docx: `fam/${xa.name}.docx`, pdf: `fam/${xa.name}.word.pdf`, emf: `emf/${xa.name}/`, crops: `crops/${xa.name}/` },
      });
    }
  }
  // RichEdit verdicts of the first session's unmapped lines, re-derived from the raw run with the same rules as the
  // xa lines (adds: .notdef box at F1 0.8-0.9, crop checked by eye; the ॥ default-character explanation)
  const rawRe = fs.existsSync(path.join(famDir, fam + ".richedit.json")) ? readJson(path.join(famDir, fam + ".richedit.json")) : [];
  for (const c of F.codepoints.filter(c => c.kind === "unmapped-punct" || c.kind === "unmapped-written")) {
    const r = rawRe.find(x => x.cp === c.cp);
    if (!r) continue;
    if (c.richeditOld === undefined) c.richeditOld = c.richedit;
    c.richedit = { typed: reUnmapped(r.typed, c.cp, F.repFace, F.richeditFontFace), rtf: reUnmapped(r.rtf, c.cp, F.repFace, F.richeditFontFace) };
  }
  // 2. draws for the step-3 scope
  for (const c of F.codepoints) {
    const cp = parseInt(c.cp.slice(2), 16);
    if (!inScope(cp, c.fails) || c.kind !== "mapped") continue;
    if (c.drawsOld === undefined) c.drawsOld = c.draws;
    const r = resolve(fam, { ...c, draws: c.drawsOld });
    if (r.draws !== c.drawsOld) changes.push(`${fam} ${c.cp}: ${c.drawsOld}  ==>  ${r.draws}`);
    c.draws = r.draws; c.drawsSource = r.src;
  }
  const byCp = new Map(F.codepoints.map(c => [c.cp, c]));
  // an unidentified glyph whose outline is identical to another code point's: name it after that one
  for (const c of F.codepoints) {
    if (c.drawsSource !== "unidentified" || !Array.isArray(c.sameOutlineAs) || !c.sameOutlineAs.length) continue;
    const o = byCp.get(c.sameOutlineAs[0]);
    if (o && o.draws && !/^unidentified/.test(o.draws)) {
      c.draws = `same outline as ${o.cp}, which draws: ${o.draws}`; c.drawsSource = "identical outline";
      changes.push(`${fam} ${c.cp}: ${c.drawsOld}  ==>  ${c.draws}`);
    }
  }
  // 3. asInput: what each wired forward table does with this character in the Unicode INPUT (FontConverter copies a
  // character with no pair through unchanged, FontConverter.cs Convert(): "No mapping: pass the character through")
  const inputCps = []; for (let v = 0x20; v <= 0x7E; v++) inputCps.push(v); inputCps.push(...PUNCT);
  for (const v of inputCps) {
    const c = byCp.get(hex(v)); if (!c) continue;
    const ch = String.fromCodePoint(v);
    delete c.asciiInput;
    c.asInput = fwdTables(fam).map(t => {
      const p = t.pairs.find(p => p.from === ch);
      return { table: t.label, does: p ? `pair -> ${cpsStr(p.to) || "nothing"}` : t.dropped.has(ch) ? "dropped (unmapped pair)" : "no pair: copied through as itself" };
    });
  }
  F.notes = Object.assign({}, F.notes, {
    drawsResolution: "draws for ASCII, cp1252 punctuation and failing code points: blank glyph (font) > Bangla family-differing code (fontCheck + rendered) > composer single-byte truth (families of the composer's encoding; SHREE-GUJ minus 91-94,B6 and SHREE-DEV minus 91,92,B3,B6) > reverse tables (worktree first for Bangla) > piece of a composer multi-byte answer > piece of a forward-table pair > unidentified. drawsOld = the first session's candidates string.",
    unmappedAscii: xa ? `ASCII not in the cmap, tested this session: ${xa.cps.join(" ")} (kind unmapped-ascii)` : "every ASCII 0x20-0x7E is in the cmap",
  });
  fs.writeFileSync(fp, JSON.stringify(F, null, 2));
  famObjs[fam] = F;
}

// 4. summary entries
for (const sf of S.families) {
  const F = famObjs[sf.family], cps = F.codepoints;
  sf.mapped = cps.filter(c => c.kind === "mapped").length;
  sf.testedUnmapped = cps.filter(c => c.kind !== "mapped").length;
  sf.wordOk = cps.filter(c => c.word.ok).length;
  sf.richeditTypedOk = cps.filter(c => c.richedit.typed.ok === true).length;
  sf.richeditRtfOk = cps.filter(c => c.richedit.rtf.ok === true).length;
  sf.failing = cps.filter(c => c.fails).map(c => ({ cp: c.cp, kind: c.kind, word: c.word.ok ? "ok" : c.word.verdict, richeditTyped: c.richedit.typed.verdict, richeditRtf: c.richedit.rtf.verdict, draws: c.draws }));
  sf.asciiAndPunct = cps.filter(c => { const v = parseInt(c.cp.slice(2), 16); return (v >= 0x20 && v <= 0x7E) || PUNCT.includes(v); })
    .sort((a, b) => parseInt(a.cp.slice(2), 16) - parseInt(b.cp.slice(2), 16))
    .map(c => { const v = parseInt(c.cp.slice(2), 16); return `${c.cp} ${v >= 0x20 && v < 0x7F ? "'" + String.fromCharCode(v) + "' " : ""}: ${c.draws}`; });
  if (["Shree-Ban7-00xA", "Shree-Tel7-00xA"].includes(sf.family)) sf.note = "not in the task's family list (WBAN701T/702T, WTEL701T/702T: 13- and 7-glyph companion faces); measured with the rest, ASCII outside their cmap not tested";
}
fs.writeFileSync(path.join(root, "draws-changes.txt"), changes.join("\n"));
fs.writeFileSync(path.join(root, "summary.json"), JSON.stringify(S, null, 2));
console.log("families", S.families.length, "draws changed", changes.length);
