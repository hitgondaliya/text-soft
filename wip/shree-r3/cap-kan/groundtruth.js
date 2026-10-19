// Build kan7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as pun7/ori7/tel7-groundtruth.json).
// (adapted from ../cap-tel/groundtruth.js)
const fs = require('fs');
const { expected, norm, key } = require('./expected');
const vendor = require('./vendor-rows.json');
const probeSets = [['capp/P-results.json', 'P']];
const probe = probeSets.flatMap(([f]) => require('./' + f));
const OUT = __dirname + '/kan7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = new Set([0x65, 0x45, 0x77, 0x57, 0xD1, 0x63, 0x43, 0x78, 0x58, 0xD2, 0x72, 0x52, 0x74, 0x54, 0x46, 0x67, 0x47, 0x68, 0x48, 0x6A, 0x79, 0x59, 0x55, 0x4A, 0x75, 0x66, 0x6B, 0xE3, 0x69, 0x49, 0x6F, 0x4F, 0x71, 0x6C, 0x4C, 0xE5, 0xE1, 0xE0]);
const SIGNC = new Set([0x61, 0x73, 0x6E, 0x26, 0x7A, 0xE2, 0x62, 0x6D, 0x70, 0x40, 0xD9, 0xDA, 0xF0, 0xF1, 0xF2, 0xF3]);
const TAILC = new Set([0x3C, 0x3E]);
const LINK = 0x64, HAL = 0x44, REPH = 0x4B, NUK = 0xD3, LEN = 0x76, RA = 0x6B, SHRI = 0xD8;
const INVISIBLE = new Set([0x5E, 0x5C]); // ZWJ, ZWNJ: no glyph is right
// codes only the 7 composer types (the Unicode composer writes nothing for them): glyph pieces, fonts/only7.png
const ONLY7 = { 0xD7: 'D7 -> 23 (a small subjoined hook under the letter)', 0x86: '86 -> 2D (an empty zero-width glyph)', 0xE6: 'E6 -> 89 (a low bar)', 0xE7: 'E7 -> 87 (a raised tick)', 0xE8: 'E8 -> 88 (a raised double tick)', 0xEA: 'EA -> 8B alone, 9B after a letter (a right-hand curve)', 0xEB: 'EB -> 7D', 0xEE: 'EE -> 9A' };
// codes the 7 composer types NOTHING for, although the Unicode composer writes them
const NEVER7 = { 0xD9: 'ೢ (code D9)', 0xDA: 'ೣ (code DA)', 0xE9: 'ಌ (code E9)', 0xDE: '॥ (code DE)', 0xF0: 'ೈ (precomposed code F0, TYPEWRIT \\)', 0xF1: 'ೀ (precomposed code F1, TYPEWRIT |)', 0xF2: 'ೇ (precomposed code F2, TYPEWRIT y)', 0xF3: 'ೋ (precomposed code F3, TYPEWRIT ~)' };

function orderClass(tag, codes) {
  if (/^reph-first/.test(tag)) return 'alt:reph struck before the letter (the Unicode composer writes a full ರ, the 7 composer the arkavattu EC BEFORE the letter)';
  if (/ reph before /.test(tag)) return 'alt:reph struck before the vowel sign (the 7 composer writes the arkavattu between the letter and its sign: ಕು = 50 DC EC E1, and loses the sign shape for ಾ ಿ: 50 DC DD, 50 51)';
  if (/ reph inside$/.test(tag)) return 'alt:reph struck between the sign and the length mark (ಕ ಿ 4B 76 = 51 EC E0: the length mark after the arkavattu)';
  const iR = codes.indexOf(REPH);
  if (iR > 0 && codes.slice(iR + 1).some(c => TAILC.has(c)) === false && codes.slice(0, iR).some(c => TAILC.has(c))) return 'canonical:tail struck before the reph (accepted, ranked after the reph before the tail: EC after ED/40 draws the arkavattu behind the anusvara)';
  if (codes[0] === RA && /^ರ \S+ before link /.test(tag)) return 'alt:ರ + sign + link + C (ರ with the subjoined C, the non-arkavattu form; see ರ + link + C)';
  for (let i = 0; i + 2 < codes.length; i++) if (codes[i] === RA && codes[i + 1] === LINK && CONS.has(codes[i + 2]) && (i === 0 || codes[i - 1] !== LINK)) return 'alt:ರ + link + C (ರ with the subjoined C, C3 DC + sub: the non-arkavattu form, Unicode ರ್‍C; plain Unicode ರ್C means the arkavattu, which is the reph code 4B after C)';
  if (/ before link /.test(tag)) return 'canonical:sign struck before the link (C1 + sign + link + C2: the same bytes as the sign struck after the cluster, except ೃ/ೄ, which then come BEFORE the subjoined form: ಕ್ಖೃ = 50 DC EA 24 56 instead of 50 DC 56 24 EA; secondary)';
  if (codes.includes(HAL)) return 'canonical:halant (code 44 = virama + ZWNJ: the visible virama glyph 85, no conjunct)';
  if (/^vendor-slot|vendor-slot/.test(tag)) return 'canonical:vendor-slot';
  if (codes.includes(LINK)) return 'canonical:link (conjunct typed with the link code 64: C1 + head + the subjoined form of C2 after it)';
  return 'canonical';
}
// explicit composer behaviours (no other probe to compare with)
function rule7(codes, tag, bytes) {
  const iL = codes.lastIndexOf(LINK);
  if (codes[0] === SHRI && codes.length > 1 && !(codes.length === 2 && (TAILC.has(codes[1]) || codes[1] === 0x20))) return 'quirk:ಶ್ರೀ-ligature-code-D8-with-a-sign-link-halant-reph-or-nukta (not a syllable: the 7 composer writes 5D and drops the rest, the Unicode composer garbles ಶ್ರೀ)';
  if (iL >= 0 && iL === codes.length - 1 && codes.length > 1) return 'quirk:7-composer-drops-the-pending-virama (C + link 64 at the end: the 7 composer writes the letter with NO virama, ಕ್ = 50 DC = ಕ; the visible virama is the halant code 44)';
  if (iL >= 0 && (codes[iL + 1] === 0x20 || INVISIBLE.has(codes[iL + 1])) && !CONS.has(codes[iL + 2])) return 'quirk:7-composer-drops-the-pending-virama (C + link 64 before a space or a joiner: no virama glyph in 7)';
  for (let i = 0; i + 1 < codes.length; i++) if (codes[i] === LINK && INVISIBLE.has(codes[i + 1])) return 'quirk:7-composer-ignores-ZWJ/ZWNJ-after-the-link (C + link + ZWJ/ZWNJ + C = the plain conjunct C1 + subjoined C2 in 7)';
  for (let i = 0; i + 1 < codes.length; i++) if (codes[i] === LINK && codes[i + 1] === 0xE5) return 'quirk:link-before-ೞ (no subjoined ೞ in any Kannada 7 face: the 7 composer drops it, and with a sign after it drops letters too: ೞ್ೞೄ = EB 24)';
  for (let i = 0; i + 1 < codes.length; i++) if (codes[i] === LINK && codes[i + 1] === 0xE1) return 'quirk:link-before-ಕ್ಷ (C + link + E1: both composers drop ಕ್ಷ)';
  const iH = codes.indexOf(HAL);
  if (iH > 0 && codes.slice(1, iH).some(c => SIGNC.has(c) || c === LEN)) return 'quirk:vowel-sign-before-the-halant (C + sign + 44: not text; the 7 composer writes only the halant glyph 85 or drops the sign)';
  if (iL > 0 && codes.slice(1, iL).some(c => SIGNC.has(c)) && !CONS.has(codes[iL + 1])) return 'quirk:vowel-sign-before-a-pending-link (C + sign + link: not text)';
  const iN = codes.indexOf(NUK);
  if (iN > 0 && SIGNC.has(codes[iN + 1]) && nuktaGarbled(codes, bytes)) return 'quirk:7-composer-garbles-nukta-before-a-sign (the letter drawn twice: ಕ಼ಿ = 50 51, ಖ಼ಾ = 53 54 82 DD; or the nukta lost: ಕ಼ೊ = 50 DC E6 E3)';
  if (codes.includes(LEN) && codes.filter(c => c === LEN).length > 1) return 'quirk:two-length-marks';
  return null;
}
// a probe whose code the 7 composer ignored: same bytes as the probe without that code, while the Unicode composer did write it
const probeBy = new Map(probe.filter(r => r.u != null && r.bytes != null).map(r => [r.codes, r]));
function dropped(codes, bytes, u) {
  for (const c of codes) if (NEVER7[c] !== undefined) return 'quirk:7-composer-drops-' + NEVER7[c];
  for (let j = 0; j < codes.length; j++) {
    if (INVISIBLE.has(codes[j])) continue;
    const red = codes.filter((_, k) => k !== j).map(h).join(' ');
    const o = probeBy.get(red);
    if (o && o.bytes === bytes && norm(o.u) !== norm(u)) return 'quirk:7-composer-dropped-code-' + h(codes[j]);
  }
  return null;
}
const S2I = { 0x61: 'ಆ', 0x73: 'ಇ', 0x6E: 'ಉ', 0x26: 'ಊ', 0x7A: 'ಋ', 0xE2: 'ೠ', 0x62: 'ಎ', 0x6D: 'ಐ', 0x70: 'ಒ', 0x40: 'ಔ' };
// C + nukta + sign: garbled when the nukta glyph (82, or DE before ಾ on ಮ) is lost, or when the bare letter AND the
// letter's sign form are both written (ಕ಼ಿ = 50 51, ಖ಼ಾ = 53 54 82 DD: the letter drawn twice)
function nuktaGarbled(codes, bytes) {
  const iN = codes.indexOf(NUK);
  const b = bytes.split(' ');
  if (!b.includes('82') && !b.includes('DE')) return true;
  const bare = probeBy.get(codes.slice(0, iN).map(h).join(' '));
  const signed = probeBy.get(codes.filter((_, k) => k !== iN).map(h).join(' '));
  if (!bare || !signed) return false;
  const b0 = bare.bytes.split(' ')[0], s0 = signed.bytes.split(' ')[0];
  return b0 !== s0 && b.includes(b0) && b.includes(s0);
}
const loneSign = codes => codes.every(c => SIGNC.has(c) || c === LEN || TAILC.has(c)) && codes.some(c => SIGNC.has(c) || c === LEN);

const pairs = [];
let quirks = 0;
for (const r of vendor) {
  const row = { unicode: key(r.unicode), bytes: r.bytes, source: 'vendor ' + r.source + (r.codes ? ': codes ' + r.codes : ''), kind: 'vendor-key' };
  let q = null;
  if (r.codes) {
    const codes = parse(r.codes);
    const exp = expected(codes);
    if (codes.length === 1 && codes[0] === REPH) q = 'alt:lone reph key (4B alone: the Unicode composer writes it on the carrier ಕ, ರ್ಕ; the 7 composer the arkavattu EC with nothing to sit on)';
    else if (/[A-Za-z]/.test(r.unicode)) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(r.uRaw);
    else if (loneSign(codes)) q = 'quirk:lone-vowel-sign-key (the 7 composer writes the INDEPENDENT vowel glyph for a sign key alone: ೆ -> 47 = ಎ; the Unicode composer the sign)';
    else if (codes.length === 1 && S2I[codes[0]] !== undefined && norm(r.unicode) === norm(S2I[codes[0]])) q = 'quirk:vendor-key-types-other-than-its-codes (phonetic mode: a vowel-sign key alone types the independent vowel)';
    else if (norm(exp) === norm(r.unicode)) q = rule7(codes, '', r.bytes) || dropped(codes, r.bytes, r.unicode);
    else q = 'quirk:vendor-key-types-other-than-its-codes (phonetic mode, a consonant key typing its virama form, or a composer join): U ' + JSON.stringify(r.uRaw) + ' for codes ' + JSON.stringify(exp);
    if (!q && codes.some(c => ONLY7[c] !== undefined)) q = 'quirk:7-only-code: ' + ONLY7[codes.find(c => ONLY7[c] !== undefined)];
  }
  if (q) { row.kind = q; if (q.startsWith('quirk:')) quirks++; }
  pairs.push(row);
}
const evidence = [];
for (const r of probe) {
  if (r.u == null || r.bytes == null) continue;
  const codes = parse(r.codes), u = r.u, bytes = r.bytes;
  const uRead = key(u);
  const exp = expected(codes);
  const row = { unicode: uRead, bytes, source: `probe ${r.file.replace('.KAN', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: orderClass(r.tag, codes) };
  if (uRead !== u) row.uComposer = u;
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  let q = null;
  if (o7 !== undefined) q = 'quirk:7-only-code: ' + ONLY7[o7];
  else if (/[A-Za-z]|[\u0000-\u001F]/.test(uRead.replace(/[ -~]/g, m => /[A-Za-z]/.test(m) ? m : ''))) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(u);
  else if (loneSign(codes) || (codes[0] === 0x20 || codes[0] === 0x23) && loneSign(codes.slice(1))) q = 'quirk:lone-vowel-sign (the 7 composer writes the independent vowel glyph or a carrier; the Unicode composer the sign)';
  else if (norm(uRead) !== norm(exp)) q = 'quirk:U-composer-wrote-' + JSON.stringify(uRead) + '-for-' + JSON.stringify(exp);
  else q = rule7(codes, r.tag, bytes) || dropped(codes, bytes, uRead);
  if (!q && SIGNC.has(codes[0])) q = 'quirk:starts-with-a-vowel-sign (no letter before it: the 7 composer writes the INDEPENDENT vowel glyph, ೈ = 49 = ಐ, ೃ = 4D E1 = ಋ; vendor slots like AVENUE ೈ್ಚ are typed after a letter)';
  if (!q && (codes.includes(0x28) || codes.includes(0x29)) && /[\[\]]/.test(uRead)) q = 'quirk:U-composer-writes-[-]-for-codes-28/29 (the 7 byte 28/29 draws ( ) in every Kannada face; no face has [ ])';
  if (!q && bytes.split(' ').some(b => parseInt(b, 16) < 0x20)) q = 'quirk:7-composer-wrote-a-control-byte (link + ೞ + sign = 08: no Kannada face has a glyph there; ೞ has no subjoined form)';
  if (q && /^alt:reph struck before the letter/.test(row.kind) && /U-composer-wrote/.test(q)) { q = null; row.uComposer = u; row.unicode = key(exp); } // filed under what the codes mean (ರ್ + syllable), not the full ರ the Unicode composer wrote // keep the alt label (reph-first: the U composer writes a full ರ)
  if (q) { row.kind = q; quirks++; }
  if (q && q.startsWith('quirk:7-only')) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: exp, bytes, why: q, source: row.source });
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) vendor keys; (2) canonical other than the secondary orders; (3) canonical; (4) fewer bytes; (5) more probes.
const groups = new Map();
for (const p of pairs) { if (!groups.has(p.unicode)) groups.set(p.unicode, []); groups.get(p.unicode).push(p); }
const truth = {};
for (const [u, list] of groups) {
  const ok = list.filter(p => !p.kind.startsWith('alt:') && !p.kind.startsWith('quirk:'));
  if (!ok.length) continue;
  const score = new Map();
  for (const p of ok) {
    const s = score.get(p.bytes) || { bytes: p.bytes, vendor: 0, can: 0, main: 0, n: 0 };
    s.n++;
    if (p.kind.startsWith('vendor-key')) s.vendor++;
    if (p.kind.startsWith('canonical')) s.can++;
    if (p.kind.startsWith('canonical') && !/tail struck before the reph|sign struck before the link/.test(p.kind)) s.main++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) => (b.main > 0) - (a.main > 0) || b.vendor - a.vendor || (b.can > 0) - (a.can > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  // a secondary order (tail before reph, sign before link) is accepted only where no main-order or vendor spelling exists
  const hasMain = ranked.some(x => x.main > 0 || x.vendor > 0);
  const acc = hasMain ? ranked.filter(x => x.main > 0 || x.vendor > 0) : ranked;
  const second = ranked.filter(x => !acc.includes(x)).map(x => x.bytes);
  truth[u] = { preferred: acc[0].bytes, accepted: acc.map(x => x.bytes), alternatives: [...new Set([...second, ...list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes)])].filter(b => !acc.some(x => x.bytes === b)) };
}

const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1').replace(/^(quirk:U-composer-carrier-or-placeholder-in-answer).*/, '$1'); kinds[k] = (kinds[k] || 0) + 1; }

// Composer gaps: Unicode strings a table needs that Shree's 7 composer never spells directly, with the spelling
// derived from its own answers (marked derived, NOT in the truth).
const gaps = [];
for (const [u, t] of Object.entries(truth)) {
  const m = /^(.*)್\u200C$/.exec(u);
  if (m && !truth[m[1] + '್']) gaps.push({ unicode: m[1] + '್', derived: t.preferred, from: u, why: 'word-final virama: the pending link (C + 64 at the end / before a space) makes the 7 composer write the letter with NO virama; the only visible-virama spelling is the halant code 44, whose Unicode answer is C + ್ + ZWNJ. A Unicode C್ at a word end looks exactly like that.' });
}
// two-code signs on a cluster (C1 64 C2 + sign + 76 = 5 codes): derived = the cluster with ಿ/ೆ/ೊ + E0 (the rule every
// single consonant follows: C + ಿ/ೆ/ೊ + 76 = the ಿ/ೆ/ೊ bytes + E0, checked on every consonant in the truth)
let lenRule = 0, lenRuleOk = 0;
for (const b of Object.values(require('./expected').C)) {
  for (const [s, s2] of [['ಿ', 'ೀ'], ['ೆ', 'ೇ'], ['ೊ', 'ೋ']]) { const a = truth[key(b + s)], z = truth[key(b + s2)]; if (a && z) { lenRule++; if (z.preferred === a.preferred + ' E0') lenRuleOk++; } }
}
for (const [u, t] of Object.entries(truth)) {
  if (!/್/.test(u)) continue;
  for (const [s, s2] of [['ಿ', 'ೀ'], ['ೆ', 'ೇ'], ['ೊ', 'ೋ']]) {
    if (u.endsWith(s) && /^[\u0C95-\u0CB9\u0CDE]್[\u0C95-\u0CB9\u0CDE]/.test(u) && !truth[key(u.slice(0, -1) + s2)] && u.length === 4) gaps.push({ unicode: key(u.slice(0, -1) + s2), derived: t.preferred + ' E0', from: u, why: `two-code sign on a cluster (C1 link C2 + ${s} + 76 = 5 codes, more than a slot holds); derived by the single-consonant rule C + ${s} + 76 = (C + ${s}) + E0 (${lenRuleOk}/${lenRule} consonants follow it)` });
  }
}

// every byte the 7 composer writes, against the font code sets (fonts/family-check.json)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['WKAN7001.TTF'].codeList.split(' ')), setS7 = new Set(fam.faces['S0850890.TTF'].codeList.split(' ')), set456 = new Set(fam.faces['S08500F0.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js + ttf.js): (3,1) cmap of every installed Kannada Shree face - 7: WKAN7001/2.TTF (Shree-Kan7-001/002), S0850890..S0855890 + S0899890 (SHREE-KAN7-0850..0855/0899); 4/5/6: S08500F0..S08550F0 + S08990F0 (SHREE-KAN-0850..0855/0899). Per code: glyph id, advance, sha1 of the glyf bytes.',
  result: 'All 16 faces have the same 215 codes on the same code points. SHREE-KAN7-08xx and SHREE-KAN-08xx of the same number are identical code by code (outline bytes and advance, 215/215 for all seven designs). Shree-Kan7-001/002 are other designs with the same code set (glyph ids differ in 164 codes, the order of glyphs in the file, not the code meaning). ONE table serves both families.',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b) && !setS7.has(b) && !set456.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
  bytesInTruthNotIn456: [...used.keys()].filter(b => setS7.has(b) && !set456.has(b)).sort(),
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, moreThanOneAccepted: Object.values(truth).filter(t => t.accepted.length > 1).length, withAlternatives: Object.values(truth).filter(t => t.alternatives.length).length, quirks, vendorKeyboards: 21, vendorRows: vendor.length, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, probeSets: probeSets.map(([f, p]) => p + ' ' + f).join(', '), composerGaps: gaps.length, lengthMarkRule: `${lenRuleOk}/${lenRule}`, byKind: kinds, fontCheck,
  notProbed: 'A conjunct slot holds at most 4 codes and a slot reference inside a slot is ignored, so these are not in the truth: three-consonant clusters C1್C2್C3 (5 codes; their pieces "link C2 link C3" are probed, P set, and C1 + those pieces concatenate); a two-code vowel sign (ೀ = ಿ + 76, ೇ = ೆ + 76, ೋ = ೊ + 76) on a cluster (5 codes; derived in kan7-composer-gaps.json); C1್C2 + sign + tail + reph; reph + C1್C2 + two-code sign.' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Kannada bytes (fonts SHREE-KAN7-0850..0855/0899 = S0850890..S0855890/S0899890.TTF and Shree-Kan7-001/002 = WKAN7001/2.TTF; the 4/5/6 fonts SHREE-KAN-0850..0855/0899 = S08500F0..S08550F0/S08990F0.TTF have the same code set and identical glyphs), answered by Shree\'s own composers S8KanU64.dll (TutorGetMappedCodeW, Unicode) and S8Kan764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code (bytes 80-9F are raw bytes: in a document they sit on their Windows-1252 code points, except 8D and 90 which the fonts map at U+008D/U+0090; 80 81 8E 8F 9D 9E have no glyph). Every probe set and every vendor keyboard was captured twice in separate processes: byte-identical. '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 21 shipped .KAN keyboards loaded from its ORIGINAL path (the composers switch typing mode by name: ENG/PHONETIC/NEWROMA/ROMA/KGP* are phonetic); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Kannada: the consonant (which the 7 composer writes with its talakattu head DC: ಕ = 50 DC), the nukta D3, then the vowel sign (which replaces or follows the head: ಕಾ = 50 DD, ಕಿ = 51 one glyph, ಕು = 50 DC E1, ಪು = B1 DC E2, ಕೆ = 50 E6, ಕೈ = 50 E6 E7, ಕೌ = 50 E8), the length mark 76 after ಿ ೆ ೊ (ಕೀ = 51 E0, ಕೇ = 50 E6 E0, ಕೋ = 50 E6 E3 E0), then the reph 4B (the arkavattu EC, struck AFTER its syllable: ರ್ಕಾ = 50 DD EC), then ಂ ಃ (ರ್ಕಾಂ = 50 DD EC ED); conjuncts through the link code 64 (C1 + head + the subjoined form of C2: ಕ್ಕ = 50 DC 52; a vowel sign goes on C1 and the subjoined form follows: ಕ್ಕಿ = 51 52, ಕ್ಕು = 50 DC E1 52; some subjoined forms fuse with ೃ/ೈ: ಕ್ತೃ = 50 DC F5, ಕ್ತೈ = 50 E6 F4); canonical:halant = code 44 (Unicode virama + ZWNJ: the visible virama glyph 85 replacing the head, ಕ್‌ = 50 85); canonical:tail struck before the reph = accepted, ranked second. '
  + 'preferred: canonical in the main order, then vendor key, then fewer bytes (ಕ್ಷ: the code E1 = FB DC, C + link + ಷ = 50 DC D2; both accepted), then more probes; accepted keeps every canonical spelling. '
  + 'alt:* = the composer answer for another typing order or another visual form (the reph struck before the letter or before the sign; ರ + link + C = ರ with the subjoined C, C3 DC 52, the non-arkavattu form; the lone reph key) - kept, not used as truth. quirk:* = the Unicode composer wrote something other than the codes mean (it drops the length mark 76 after a letter or a cluster, keeps ೢ/ೣ before the link and puts ರ್ after the letter with ೢ/ೣ, drops ಕ್ಷ after the link), or the 7 composer dropped a code the Unicode composer wrote (the pending virama, ZWJ/ZWNJ, the precomposed F0-F3, ೢ ೣ ಌ ॥), a code only the 7 composer types (D7 86 E6 E7 E8 EA EB EE), the nukta before ಿ/ೊ (garbled by the 7 composer), the ಶ್ರೀ code D8 with anything after it, or a lone vowel sign (7 writes the independent vowel glyph) - kept for evidence, not used. '
  + 'Unicode keys are what the Unicode composer wrote, in NFC (ಿ+ೕ = ೀ, ೆ+ೕ = ೇ, ೆ+ೂ = ೊ, ೊ+ೕ = ೋ, ೆ+ೖ = ೈ). Rules with examples: kan7-patterns.txt. Strings a table needs that the composer never spells directly: kan7-composer-gaps.json (derived, not truth).';

// codes the Unicode composer has no answer for at all (u null) but the 7 composer types: the P set's single codes and
// the explore set E (capx/E-results.json: every code alone, after/before ಕ, after ಪ, ಕ + link + code, code + link + ಕ)
for (const r of [...probe, ...require('./capx/E-results.json')]) {
  if (r.bytes == null) continue;
  const codes = parse(r.codes);
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  if (r.u == null || o7 !== undefined && !evidence.some(e => e.codes === r.codes)) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: expected(codes), bytes: r.bytes, why: o7 !== undefined ? 'quirk:7-only-code: ' + ONLY7[o7] : 'the Unicode composer gives no answer', source: `probe ${r.file.replace('.KAN', '')} key ${r.key}` });
}
fs.writeFileSync(__dirname + '/kan7-only7-evidence.json', JSON.stringify({ about: 'Probes the Unicode composer cannot vouch for although the 7 composer typed something: codes only S8Kan764 types (D7 -> 23, 86 -> 2D, E6 -> 89, E7 -> 87, E8 -> 88, EA -> 8B/9B, EB -> 7D, EE -> 9A; glyphs in fonts/only7.png). unicodeByCodes = what the other codes mean (the 7-only code read as nothing) - an inference, not a composer answer. bytes = the 7 composer answer.', rows: evidence }, null, 1));
fs.writeFileSync(__dirname + '/kan7-composer-gaps.json', JSON.stringify({ about: 'Unicode strings with no direct Shree 7 composer spelling, with a spelling derived from the composer\'s own answers. NOT part of the truth.', gaps }, null, 1));
fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8KanU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Kan764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
// GLYPH CHECK (fonts/dhi-*.png, fonts/gunitakshara.png, fonts/punct-check.png, fonts/all-codes-3faces.png; all 16 faces
// agree): Shree's 7 composer writes ಧಿ as 97, which draws the AVAGRAHA in every Kannada face (byte 97 = U+2014 -> the
// "S" glyph), and ಽ as 98, which draws ಧಿ: the two bytes are swapped in the composer. kan7-groundtruth-glyphfix.json =
// the same truth with 97 <-> 98 swapped in every spelling (ಧಿ ಧೀ ಧಿಂ ಧ್Cಿ ... -> 98, ಽ -> 97; the lone nukta ಼ keeps 98).
// The other symbol bytes the composer writes with no right glyph have no fix in these fonts (— AC empty, – B6 a ಫ
// variant, ÷ 8A a curve, ₹ 3C no glyph): kept as the composer writes them, listed in glyphCheck.
{
  const swap = (u, b) => b.split(' ').map(x => x === '97' ? '98' : (x === '98' && u === 'ಽ') ? '97' : x).join(' ');
  const truthFix = {}; let changed = 0;
  for (const [u, t] of Object.entries(truth)) { const f = { preferred: swap(u, t.preferred), accepted: t.accepted.map(b => swap(u, b)), alternatives: t.alternatives.map(b => swap(u, b)) }; if (f.preferred !== t.preferred) changed++; truthFix[u] = f; }
  const glyphCheck = { swapped9798: 'ಧಿ family -> 97 (draws ಽ) and ಽ -> 98 (draws ಧಿ) in the composer; ' + changed + ' truth strings change in the glyphfix file', noGlyphInAnyFace: { '—': 'AC (an empty 100-unit glyph)', '–': 'B6 (draws a ಫ variant)', '÷': '8A (a zero-width curve)', '₹': '3C (no glyph)', '[ ]': 'the Unicode composer writes [ ] for codes 28/29, which draw ( ); no face has [ ]' }, checkedRight: '× 8D, । 3E, ‘ 22, ’ 27, digits 30-39, every consonant + vowel sign (fonts/gunitakshara.png) except ಧಿ/ಧೀ' };
  fs.writeFileSync(__dirname + '/kan7-groundtruth-glyphfix.json', JSON.stringify({ about: 'kan7-groundtruth.json with the two bytes Shree\'s 7 composer swaps replaced: every 97 (written for ಧಿ, but 97 draws the avagraha ಽ in every installed Kannada face) -> 98, and ಽ 98 -> 97. Everything else is byte for byte the composer. ' + glyphCheck.swapped9798, composers: { unicode: 'C:/Shree-Lipi nxt/S8KanU64.dll', legacy: 'C:/Shree-Lipi nxt/S8Kan764.dll' }, glyphCheck, truth: truthFix }, null, 0));
  const full = JSON.parse(fs.readFileSync(OUT, 'utf8')); full.counts.glyphCheck = glyphCheck; fs.writeFileSync(OUT, JSON.stringify(full, null, 1));
  console.log('glyphfix changed', changed);
}
