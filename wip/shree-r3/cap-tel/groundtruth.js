// Build tel7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as ori7/pun7-groundtruth.json).
const fs = require('fs');
const { expected, norm, key, fixRaVattu } = require('./expected');
const vendor = require('./vendor-rows.json');
const probeSets = [['capp/P-results.json', 'P'], ['capr/R-results.json', 'R']];
const probe = probeSets.flatMap(([f]) => require('./' + f));
const OUT = __dirname + '/tel7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = new Set([0x65, 0x45, 0x77, 0x57, 0xD1, 0x63, 0x43, 0x78, 0x58, 0xD2, 0x72, 0x52, 0x74, 0x54, 0x46, 0x67, 0x47, 0x68, 0x48, 0x6A, 0x79, 0x59, 0x55, 0x4A, 0x75, 0x66, 0x6B, 0x4B, 0x69, 0x49, 0x6F, 0x4F, 0x71, 0x6C, 0x4C, 0x5E]);
const SIGNC = new Set([0x61, 0x73, 0x76, 0x6E, 0x26, 0x7A, 0x5F, 0x62, 0xD3, 0x6D, 0x70, 0x24, 0x40]);
const TAILC = new Set([0x3C, 0x3E, 0xE0]);
const RAV = new Set([0xD7, 0xED]);
const INVISIBLE = new Set([0xF2, 0xF3]); // ZWJ, ZWNJ: no glyph is right
// codes only the 7 composer types (the Unicode composer writes nothing for them)
const ONLY7 = { 0xE1: 'E1 -> 93 (a third ra-vattu hook; every vendor keyboard has an E1 key; the Unicode composer writes nothing)', 0x86: '86 -> 2D', 0xDB: 'DB -> 94', 0xE7: 'E7 -> 3D', 0xE9: 'E9 -> 2B', 0xEA: 'EA -> 3B', 0xEB: 'EB -> 25', 0xEE: 'EE -> AC' };
// codes the 7 composer types NOTHING for (alone), although the Unicode composer writes them
const NEVER7 = { 0xD9: 'ఌ (code D9)', 0xDA: 'ౡ (code DA)', 0xF1: 'ౣ (code F1)', 0xF4: '॥ (code F4)', 0xDC: '₹ (code DC)', 0xDD: '× (code DD)', 0xDE: '÷ (code DE)', 0xFB: '— (code FB; 7B types —)', 0xFD: '– (code FD; 7D types –)', 0x5B: '[ (code 5B)', 0x5D: '] (code 5D)', 0x5F: 'ౄ (code 5F; on a letter the 7 composer drops it, alone it writes the carrier + 97)' };

function orderClass(tag, codes) {
  const iLink = codes.indexOf(0x64);
  for (let i = 0; i < codes.length; i++) if (RAV.has(codes[i]) && !CONS.has(codes[i + 1])) return 'alt:pending ra-vattu (D7/ED struck after the letter with no letter after it: the 7 composer writes the hook AFTER the letter, C 7B, and drops a sign struck after it; both composers attach D7 to the NEXT letter)';
  if (/ before link /.test(tag)) return codes[1] === 0x6D ? 'canonical:ై struck before the link (C1 + ై + link + C2: the only 4-code way to type C1్C2ై; the composer puts ై behind the cluster)' : 'alt:sign struck before the link (same bytes as the sign struck after the cluster, except ృ: 96 before the vattu instead of A0 96 after it)';
  if (codes.includes(0xED)) return 'canonical:ra-vattu ED struck before the letter (always the second hook 5B; no vendor key holds ED, they hold D7, with which the composer itself picks 7B or 5B)';
  if (codes.includes(0x44)) return 'canonical:halant (code 44 = virama + ZWNJ: the visible virama glyph, no conjunct)';
  if (/^vendor-slot/.test(tag)) return 'canonical:vendor-slot';
  if (iLink >= 0) return 'canonical:link (conjunct typed with the link code 64: C1 + the subjoined form of C2)';
  if (codes.some(c => RAV.has(c))) return 'canonical:ra-vattu D7 struck before the letter (the vendor order: the composer writes the hook 7B or 5B BEFORE the letter, the ligatures 88 ్త్ర / 89 ్ట్ర / 7D శ్రీ)';
  return 'canonical';
}
// explicit 7-composer behaviours (no other probe to compare with)
function rule7(codes, tag) {
  const iL = codes.indexOf(0x64);
  if (iL >= 0 && !CONS.has(codes[iL + 1])) return 'quirk:7-composer-drops-the-pending-virama (C + link 64 at a word end, before a space or a joiner: the 7 composer writes the letter with no virama; the visible virama is the halant code 44)';
  for (let i = 0; i < codes.length - 1; i++) if (codes[i] === 0x64 && codes[i + 1] === 0x6B) return 'quirk:7-composer-drops-ర-after-the-link (C + link + ర types no ra-vattu in 7; the 7 ra-vattu is the code D7 struck BEFORE the letter)';
  if (codes[0] === 0xD8 && codes.slice(1).some(c => SIGNC.has(c) || RAV.has(c))) return 'quirk:శ్రీ-ligature-code-D8-with-a-second-sign-or-ra-vattu (not a real syllable)';
  if (codes.includes(0x5F)) return 'quirk:7-composer-has-no-ౄ (code 5F: on a letter it writes nothing, on a cluster only the spacer A0, alone the carrier + 97; no Telugu Shree face has a ౄ glyph)';
  return null;
}
// a probe whose code the 7 composer ignored: same bytes as the probe without that code, while the Unicode composer did write it
const probeBy = new Map(probe.filter(r => r.u != null && r.bytes != null).map(r => [r.codes, r]));
function dropped(codes, bytes, u) {
  for (const c of codes) if (NEVER7[c] !== undefined && c !== 0x5F) return 'quirk:7-composer-drops-' + NEVER7[c];
  for (let j = 0; j < codes.length; j++) {
    if (INVISIBLE.has(codes[j])) continue;
    const red = codes.filter((_, k) => k !== j).map(h).join(' ');
    const o = probeBy.get(red);
    if (o && o.bytes === bytes && norm(key(fixRaVattu(o.u))) !== norm(u)) return 'quirk:7-composer-dropped-code-' + h(codes[j]) + (codes[j] === 0x5F ? ' (ౄ)' : RAV.has(codes[j - 1]) && SIGNC.has(codes[j]) ? ' (a vowel sign struck after the ra-vattu)' : '');
  }
  return null;
}
const LONE_SIGN_7 = 'E6 61 E6 61';
const S2I = { 0x61: 'ఆ', 0x73: 'ఇ', 0x76: 'ఈ', 0x6E: 'ఉ', 0x26: 'ఊ', 0x7A: 'ఋ', 0x5F: 'ౠ', 0x62: 'ఎ', 0xD3: 'ఏ', 0x6D: 'ఐ', 0x70: 'ఒ', 0x24: 'ఓ', 0x40: 'ఔ' };

const pairs = [];
let quirks = 0;
for (const r of vendor) {
  const row = { unicode: key(r.unicode), bytes: r.bytes, source: 'vendor ' + r.source + (r.codes ? ': codes ' + r.codes : ''), kind: 'vendor-key' };
  let q = null;
  if (r.codes) {
    const codes = parse(r.codes);
    const exp = expected(codes);
    if (/[*]|Lk|[A-Za-z]/.test(r.unicode)) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(r.uRaw);
    else if (r.bytes.startsWith(LONE_SIGN_7)) q = 'quirk:7-composer-carrier-for-a-lone-sign (E6 61 E6 61)';
    else if (codes.length === 1 && S2I[codes[0]] !== undefined && norm(r.unicode) === norm(S2I[codes[0]])) row.kind = 'vendor-key (a vowel-sign key alone types the independent vowel in vendor mode)';
    else if (norm(exp) === norm(r.unicode)) q = rule7(codes, '') || dropped(codes, r.bytes, r.unicode);
    else q = 'quirk:vendor-key-types-other-than-its-codes (phonetic mode, or the ై key alone): U ' + JSON.stringify(r.uRaw) + ' for codes ' + JSON.stringify(exp);
    if (!q && codes.some((c, i) => RAV.has(c) && i > 0 && !CONS.has(codes[i + 1]))) q = 'alt:pending ra-vattu (vendor slot C + D7: the hook after the letter)';
    if (!q && codes.length === 1 && RAV.has(codes[0])) q = 'alt:lone ra-vattu key (D7 alone = 7B, the hook waiting for the next letter; the Unicode composer writes *్ర)';
  }
  if (q) { row.kind = q; if (q.startsWith('quirk:')) quirks++; }
  pairs.push(row);
}
const evidence = [];
for (const r of probe) {
  if (r.u == null || r.bytes == null) continue;
  let codes = parse(r.codes), u = r.u, bytes = r.bytes;
  if (/\+term$/.test(r.tag)) { codes = codes.slice(0, -1); u = u.replace(/ $/, ''); bytes = bytes.replace(/ ?\b20$/, ''); if (!bytes) continue; }
  const uRead = key(fixRaVattu(u));
  const exp = expected(codes);
  const row = { unicode: uRead, bytes, source: `probe ${r.file.replace('.TEL', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: orderClass(r.tag, codes) };
  if (uRead !== u) row.uComposer = u;
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  let q = null;
  if (o7 !== undefined) q = 'quirk:7-only-code: ' + ONLY7[o7];
  else if (/Lk|[\u0000-\u001F]/.test(uRead) || (/[*]/.test(uRead) && codes.some(c => RAV.has(c)))) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(u);
  else if (bytes.startsWith(LONE_SIGN_7) || (SIGNC.has(codes[0]) && codes.length <= 2)) q = 'quirk:lone-vowel-sign (the 7 composer writes the carrier E6 61 E6 61; the Unicode composer a space or nothing; a tail after it is dropped by the Unicode composer)';
  else if (norm(uRead) !== norm(exp)) q = 'quirk:U-composer-wrote-' + JSON.stringify(uRead) + '-for-' + JSON.stringify(exp);
  else q = rule7(codes, r.tag) || dropped(codes, bytes, uRead);
  if (q) { row.kind = q; quirks++; }
  if (q && (q.startsWith('quirk:7-only') || /U-composer-wrote/.test(q) && codes.includes(0x5E))) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: exp, bytes, why: q, source: row.source });
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) vendor keys; (2) canonical other than ED; (3) canonical; (4) fewer bytes; (5) more probes.
const groups = new Map();
for (const p of pairs) { if (!groups.has(p.unicode)) groups.set(p.unicode, []); groups.get(p.unicode).push(p); }
const truth = {};
for (const [u, list] of groups) {
  const ok = list.filter(p => !p.kind.startsWith('alt:') && !p.kind.startsWith('quirk:'));
  if (!ok.length) continue;
  const score = new Map();
  for (const p of ok) {
    const s = score.get(p.bytes) || { bytes: p.bytes, vendor: 0, can: 0, d7: 0, n: 0 };
    s.n++;
    if (p.kind.startsWith('vendor-key')) s.vendor++;
    if (p.kind.startsWith('canonical')) s.can++;
    if (p.kind.startsWith('canonical') && !/ra-vattu ED/.test(p.kind)) s.d7++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) => b.vendor - a.vendor || (b.d7 > 0) - (a.d7 > 0) || (b.can > 0) - (a.can > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  truth[u] = { preferred: ranked[0].bytes, accepted: ranked.map(x => x.bytes), alternatives: [...new Set(list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes))].filter(b => !ranked.some(x => x.bytes === b)) };
}

const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1').replace(/^(quirk:U-composer-carrier-or-placeholder-in-answer).*/, '$1'); kinds[k] = (kinds[k] || 0) + 1; }

// Composer gaps: Unicode strings a table needs that Shree's 7 composer never spells directly, with the spelling
// derived from its own answers (marked derived, NOT in the truth).
const gaps = [];
for (const [u, t] of Object.entries(truth)) {
  const m = /^(.*)్\u200C$/.exec(u);
  if (m && !truth[m[1] + '్']) gaps.push({ unicode: m[1] + '్', derived: t.preferred, from: u, why: 'word-final virama: the pending link (C + 64 + space / end) makes the 7 composer write the letter with NO virama; the only visible-virama spelling is the halant code 44, whose Unicode answer is C + ్ + ZWNJ. A Unicode C్ at a word end looks exactly like that.' });
}

// every byte the 7 composer writes, against the font code sets (fonts/family-check.json)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['WTEL7001.TTF'].codeList.split(' ')), set456 = new Set(fam.faces['M09000F0.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js + ttf.js): (3,1) cmap of every installed Telugu Shree face - 7: WTEL7001.TTF (Shree-Tel7-001), WTEL7002.TTF (Shree-Tel7-002), WTEL701T/702T.TTF (7-code "T" faces), M0900890/N0902890.TTF (SHREE-TEL7-Apoorva-0900 regular/bold), M1642890/N1644890.TTF (SHREE-TEL7-Asmitha-1642 regular/bold); 4/5/6: M09000F0/N09020F0.TTF (SHREE-TEL-Apoorva-0900), M16420F0/N16440F0.TTF (SHREE-TEL-Asmitha-1642). Per code: glyph id, advance, sha1 of the glyf bytes.',
  sameGlyph: 'Codes that draw the identical outline with the identical advance (fonts/same-glyph-codes.json): 7B = 93 and 90 = A0 in every face (93 is the glyph of the 7-only code E1, so a 93 ra-vattu looks exactly like the 7B Shree writes); B6 = E6 in the Apoorva faces only.',
  composerBytesThatDrawTheWrongGlyph: 'ః = 40 and ఁ = B6 (both talakattu ticks in every face; the visarga glyph is 5D, the candrabindu glyph 97): see tel7-groundtruth-glyphfix.json',
  result: 'All ten full faces have the same 216 codes on the same code points with the same glyph ids. The same design in the two families is identical code by code (outline bytes and advance): M0900890 = M09000F0, N0902890 = N09020F0, M1642890 = M16420F0, N1644890 = N16440F0 (216/216 each). Shree-Tel7-001/002 have no 4/5/6 twin (other designs, same code set and glyph ids). Bytes 80 81 8D 8E 8F 9D 9E have no glyph in any face; 90 sits on U+0090, every other byte 80-9F on its Windows-1252 code point. ONE table serves both families.',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b)).sort(),
  bytesInTruthNotIn456: [...used.keys()].filter(b => set7.has(b) && !set456.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, moreThanOneAccepted: Object.values(truth).filter(t => t.accepted.length > 1).length, withAlternatives: Object.values(truth).filter(t => t.alternatives.length).length, quirks, vendorKeyboards: 16, vendorRows: vendor.length, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, probeSets: probeSets.map(([f, p]) => p + ' ' + f).join(', '), composerGaps: gaps.length, byKind: kinds, fontCheck,
  notProbed: 'A conjunct slot holds at most 4 codes and a slot reference inside a slot is ignored (shown on Punjabi this round), so these are not in the truth: three-consonant clusters C1్C2్C3 (5 codes: C link C link C) other than C1్C2్ర (C link C D7) and క్ష + ్C; C1్C2 + sign + tail; C1్C2 + ై + tail; C1్C2్ర + a sign; ై + tail on a cluster. The probes with ై or the halant 44 at the end needed a space after them (the 7 composer leaves ై/44 pending), so those rows had one code fewer to spend.' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Telugu bytes (fonts SHREE-TEL7-Apoorva-0900 M0900890/N0902890.TTF, SHREE-TEL7-Asmitha-1642 M1642890/N1644890.TTF, Shree-Tel7-001/002 WTEL7001/2.TTF; the 4/5/6 fonts SHREE-TEL-Apoorva/Asmitha M09000F0/N09020F0/M16420F0/N16440F0.TTF have the same code set and identical glyphs), answered by Shree\'s own composers S8TelU64.dll (TutorGetMappedCodeW, Unicode) and S8Tel764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code (bytes 80-9F are raw bytes: in a document they sit on their Windows-1252 code points, except 90 which the fonts map at U+0090). Every probe set and every vendor keyboard was captured twice in separate processes: identical (the only difference is the random unit count the 7 composer returns for "no answer"). '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 16 shipped .TEL keyboards loaded from its ORIGINAL path (the composers switch typing mode by name: ENG/PHONETIC answer a consonant with its virama form; a vowel-sign key alone types the independent vowel); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Telugu: the consonant (which the 7 composer writes with its talakattu, క = 4D E6 FC), then the vowel sign (which replaces the talakattu: కి = 4D EC FC, కా = 4D 3E), then ం ః ఁ; the ra-vattu ్ర is the code D7 struck BEFORE the letter it belongs to (the vendor charts\' order: TYPEWRIT K = D7 first, ITR Ctrl+Space g first, INDICA/APPLE slots D7 + C), and the 7 composer writes its hook BEFORE the letter (క్ర = 7B 4D E6 FC, క్రి = 7B 4D EC FC; 5B before a letter with ై: క్రై = 5B 4D F0 FC 4F; on త/ట after a link the combined glyphs 88/89: స్త్ర = E7 DC 88, ష్ట్ర = E7 D9 89; శ్రీ = 7D) - the same place the శ్రీ ligature 7D draws it; conjuncts through the link code 64 (C1 + the subjoined form of C2, క్క = 4D E6 FC 50; a vowel sign on the cluster changes C1: క్కి = 4D EC FC 50); ై = the e-shape of the letter + 4F (కై = 4D F0 FC 4F; on a cluster typed ై first: క్కై = 4D F0 FC 4F 50); canonical:halant = code 44 (Unicode virama + ZWNJ: the visible virama glyph, క్‌ = 4D 8A FC); canonical:ra-vattu ED = always the second hook 5B (accepted, ranked after D7). The Unicode composer writes the ra-vattu as "*్ర" where it was struck and leaves it for the NEXT consonant (క + D7 + క = "క*్రక" = కక్ర; the 7 composer writes 4D E6 FC 7B 4D E6 FC, the hook before the second క); uComposer keeps that raw answer, unicode is it read as ్ర behind the cluster that follows. '
  + 'preferred: vendor key, then canonical other than ED, then fewer bytes (క్ష: the code 5E = 84 E6 FC, C + link + ష = 4D E6 FC DB; both accepted), then more probes; accepted keeps every canonical spelling. '
  + 'alt:* = the composer answer for another typing order (the pending ra-vattu: D7 struck after a letter with no letter after it, the hook written AFTER the letter, C 7B - visually on the wrong side, and a sign struck after it is dropped; a sign struck before the link: same bytes except ృ) - kept, not used as truth. quirk:* = the Unicode composer wrote something other than the codes mean (it drops క్ష after the link and a tail after a lone sign, and drops ZWJ/ZWNJ after the link), or the 7 composer dropped a code the Unicode composer wrote (the pending virama, ర after the link, a vowel sign struck after the ra-vattu, ౄ on a letter, ఌ ౡ ౣ ॥ ₹ × ÷ [ ]), a code only the 7 composer types (E1 -> 93 and 86 DB E7 E9 EA EB EE), or a lone vowel sign (7 carrier E6 61 E6 61) - kept for evidence, not used. '
  + 'Unicode keys are what the Unicode composer wrote (ై filed as U+0C48; ె + ౖ is joined to it). Rules with examples: tel7-patterns.txt. Strings a table needs that the composer never spells directly: tel7-composer-gaps.json (derived, not truth).';

fs.writeFileSync(__dirname + '/tel7-only7-evidence.json', JSON.stringify({ about: 'Probes the Unicode composer cannot vouch for although the 7 composer typed something: codes only S8Tel764 types (E1 = a third ra-vattu hook 93 on every vendor keyboard; 86 DB E7 E9 EA EB EE), and క్ష (5E) after the link (the Unicode composer writes nothing for it; the 7 composer writes the subjoined క్ష 3C: క్క్ష = 4D E6 FC 3C). unicodeByCodes = what the codes mean (E1 read as nothing) - an inference, not a composer answer. bytes = the 7 composer answer.', rows: evidence }, null, 1));
fs.writeFileSync(__dirname + '/tel7-composer-gaps.json', JSON.stringify({ about: 'Unicode strings with no direct Shree 7 composer spelling, with a spelling derived from the composer\'s own answers. NOT part of the truth.', gaps }, null, 1));
fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8TelU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Tel764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
// Shree's 7 composer writes 40 for ః and B6 for ఁ; in every installed Telugu face (7 and 4/5/6) these bytes draw a
// talakattu tick (B6 has the very outline of the talakattu E6 in Apoorva), checked by ttf.js and by Windows GDI
// (fonts/check-marks*.png, fonts/gdi-marks.png); the visarga glyph is 5D and the candrabindu glyph 97. 40/B6 appear in
// the truth only for ః/ఁ (1:1). tel7-groundtruth-glyphfix.json = the same truth with 40 -> 5D and B6 -> 97.
const fixB = b => b.split(' ').map(x => x === '40' ? '5D' : x === 'B6' ? '97' : x).join(' ');
const truthFix = {};
for (const [u, t] of Object.entries(truth)) truthFix[u] = { preferred: fixB(t.preferred), accepted: t.accepted.map(fixB), alternatives: t.alternatives.map(fixB) };
fs.writeFileSync(__dirname + '/tel7-groundtruth-glyphfix.json', JSON.stringify({ about: 'tel7-groundtruth.json with the two bytes the Shree 7 composer gets visibly wrong replaced: ః 40 -> 5D (the visarga glyph), ఁ B6 -> 97 (the candrabindu glyph). 40 and B6 draw a talakattu tick in every installed Telugu Shree face (fonts/gdi-marks.png). Everything else is byte for byte the composer.', composers: { unicode: 'C:/Shree-Lipi nxt/S8TelU64.dll', legacy: 'C:/Shree-Lipi nxt/S8Tel764.dll' }, truth: truthFix }, null, 0));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
