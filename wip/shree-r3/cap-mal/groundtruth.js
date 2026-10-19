// Build mal7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as pun7/kan7-groundtruth.json).
// (adapted from ../cap-kan/groundtruth.js)
// node groundtruth.js   (probe sets: capp/P-results.json and, when present, capw/W-results.json)
const fs = require('fs');
const { expected, norm, key, C: CMAP } = require('./expected');
const vendor = require('./vendor-rows.json');
const probeSets = [['capp/P-results.json', 'P'], ['capw/W-results.json', 'W'], ['capxx/X-results.json', 'X']].filter(([f]) => fs.existsSync(__dirname + '/' + f));
const probe = probeSets.flatMap(([f, p]) => require('./' + f).map(r => ({ ...r, set: p })));
const OUT = __dirname + '/mal7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = new Set(Object.keys(CMAP).map(Number));
const POSTC = new Set([0x6C, 0x6A, 0x4A, 0x6B, 0x6F, 0xD0, 0xE9, 0xEA, 0xEB, 0x4F]);
const PRECOMP = new Set([0xDC, 0xDD, 0xDE, 0xDF]);
const PREC = new Set([0x73, 0x53, 0x59]);
const TAILC = new Set([0x41, 0xD6]);
const YV = new Set([0x55, 0x49]);
const E = 0x73, EE = 0x53, RA = 0x59, LINK = 0x64, LINK2 = 0xE2, HAL = 0x44, ZWJ = 0xF1, ZWNJS = new Set([0xDB, 0xF2]), SHRI = 0xE3, KSSA = 0xD5;
const INDV = new Set([0x4C, 0xD2, 0x54, 0x4B, 0x57, 0xE6, 0xE4, 0xE5, 0x74, 0xD1, 0xF0, 0x51]);
// codes the 7 composer types NOTHING for, although the Unicode composer writes them
const NEVER7 = { 0xDC: 'ോ (precomposed code DC)', 0xDD: 'ൊ (precomposed code DD)', 0xDE: 'ൈ (precomposed code DE)', 0xDF: 'ൌ (precomposed code DF)', 0xE4: 'ഌ (code E4)', 0xE5: 'ൡ (code E5)', 0xE6: 'ൠ (code E6)', 0xE8: 'ഽ (code E8)', 0xE9: 'ൄ (code E9)', 0xEA: 'ൢ (code EA)', 0xEB: 'ൣ (code EB)', 0xEC: 'ൿ (code EC)', 0xEE: '• (code EE)', 0xF0: 'ഐ (code F0; ഐ is 73 74 = 52 49)', 0xF4: '൳ (code F4)', 0xF5: '൴ (code F5)', 0xF6: '൵ (code F6)', 0xF7: '൹ (code F7)', 0xF8: '൰ (code F8)', 0xF9: '൱ (code F9)', 0xFA: '൲ (code FA)', 0x5B: '[ (code 5B)', 0x5D: '] (code 5D)', 0xDB: 'ZWNJ (code DB)', 0xF1: 'ZWJ (code F1)', 0xF2: 'ZWNJ (code F2)' };
// codes only the 7 composer types (the Unicode composer writes nothing for them)
const ONLY7 = { 0x86: '86 -> 2D (an empty zero-width glyph)', 0xED: 'ED -> 9E (no glyph in any Malayalam 7 face)' };

const isLoneSign = codes => codes.every(c => POSTC.has(c) || PREC.has(c) || YV.has(c) || TAILC.has(c) || PRECOMP.has(c) || c === LINK || c === HAL || c === LINK2 || c === 0x20 || c === 0x2E) && codes.some(c => !(c === 0x20 || c === 0x2E));
function orderClass(tag, codes) {
  if (codes[0] === LINK) return 'alt:piece with a leading link (not text: the pieces of a three-consonant cluster; the Unicode composer drops the leading virama)';
  if (codes.includes(LINK2)) return 'quirk:E2-virama (code E2 is on no vendor keyboard: the Unicode composer writes ്, the 7 composer writes NO virama glyph, and a control byte 08 before a ligature: ക E2 ക = 08 A8)';
  for (let i = 0; i + 1 < codes.length; i++) if (CONS.has(codes[i]) && (codes[i + 1] === E || codes[i + 1] === EE || codes[i + 1] === RA) && !(i + 2 < codes.length && (CONS.has(codes[i + 2]) || codes[i + 1] === E && codes[i + 2] === E && CONS.has(codes[i + 3])))) return 'alt:pre-base sign struck after its letter (logical order: the Unicode composer writes it on a carrier, the 7 composer writes the sign glyph AFTER the letter)';
  for (let i = 0; i + 1 < codes.length; i++) if (codes[i] === RA && (codes[i + 1] === E || codes[i + 1] === EE)) return 'alt:ra sign struck before െ/േ (the Unicode composer writes "*്ര" on a carrier; the 7 composer writes 4E before 52/53: ്ര െ ക = 4E 52 57 against െ ്ര ക = 52 4E 57)';
  for (let i = 0; i + 2 < codes.length; i++) if (CONS.has(codes[i]) && codes[i + 1] === LINK && codes[i + 2] === ZWJ) return 'alt:link + ZWJ (the Unicode 5.0 chillu spelling C്‍: the 7 composer ignores the ZWJ and writes C + the chandrakkala 56 - ത്‍ and മ്‍ write nothing at all; the atomic chillu codes write ൺ A6 ൻ A2 ർ A1 ൽ A4 ൾ A5)';
  if (codes.includes(HAL)) return 'canonical:halant (code 44 = virama + ZWNJ: the chandrakkala 56)';
  if (/vendor-slot/.test(tag)) return 'canonical:vendor-slot';
  if (codes.includes(LINK)) return 'canonical:link (conjunct typed with the link code 64: a ligature, C1 + the subjoined form of C2, or C1 + the chandrakkala 56 + C2)';
  return 'canonical';
}
// split a visual-order code sequence into syllables: a pre-base sign / ra sign opens a syllable unless the syllable is
// still waiting for its consonant; a consonant opens one unless it follows the link; vowels, chillus and others stand alone
function segment(codes) {
  const out = []; let cur = null;
  const open = () => { cur = { codes: [], hasCons: false, waiting: false }; out.push(cur); };
  for (let k = 0; k < codes.length; k++) {
    const c = codes[k];
    if (PREC.has(c)) { if (!cur || !cur.waiting || cur.hasCons) open(); cur.waiting = true; cur.codes.push(c); continue; }
    if (CONS.has(c)) { if (!cur || !(cur.waiting && !cur.hasCons) && cur.codes[cur.codes.length - 1] !== LINK) open(); cur.hasCons = true; cur.codes.push(c); continue; }
    if (INDV.has(c) || (c >= 0x20 && c < 0x40 && !TAILC.has(c))) { if (!(c === 0x74 && cur && cur.waiting && !cur.hasCons)) open(); cur.hasCons = true; cur.codes.push(c); continue; }
    if (!cur) open();
    cur.codes.push(c);
  }
  return out.map(x => x.codes);
}
// explicit composer behaviours
function rule7(codes, tag, bytes) {
  for (let i = 1; i + 2 < codes.length; i++) if (codes[i] === LINK && CONS.has(codes[i - 1]) && (codes[i + 1] === E || codes[i + 1] === EE || codes[i + 1] === RA) && (CONS.has(codes[i + 2]) || codes[i + 2] === E)) return 'quirk:7-composer-link-before-a-pre-base-sign (C1 + link + െ/േ/്ര + C2: the composer joins the cluster across the sign and loses the sign: ക ് െ ക = 57 A8, where െ ക ് ക = 52 A8)';
  for (let i = 0; i + 2 < codes.length; i++) if ((codes[i] === 0xD7 || codes[i] === 0x61) && codes[i + 1] === LINK && (codes[i + 2] === ZWJ || ZWNJS.has(codes[i + 2]))) return 'quirk:7-composer-loses-ത്/മ്-before-a-joiner (ത or മ + link + ZWJ/ZWNJ: the 7 composer writes NOTHING for the letter and its virama - ത്‌ക = 57, ത്‍ = no answer; every other letter gives C + 56)';
  const iL = codes.lastIndexOf(LINK);
  for (const sy of segment(codes)) { const j = sy.lastIndexOf(LINK); if (j === sy.length - 1 && j > 0) { if (sy.slice(0, j).some(c => POSTC.has(c) || TAILC.has(c) || YV.has(c) || PRECOMP.has(c))) return 'quirk:vowel-sign-before-a-pending-link (C + sign + link 64: both composers drop the link - കു + 64 = കു / 57 4F; the samvruthokaram കു്‌ is the halant 44: 57 4F 56)'; if (PREC.has(sy[0])) return 'quirk:pre-base-sign-before-a-pending-link (െ + C + link: not text; the Unicode composer writes ക്െ)'; } }
  if (codes[0] === SHRI && codes.length > 1 && !(codes.length === 2 && (codes[1] === 0x20))) return 'quirk:ശ്രീ-code-E3-with-something-after-it (not a syllable: the Unicode composer drops ശ്രീ, the 7 composer writes E8 7D + the rest)';
  if (codes[0] === RA && codes.length === 4 && CONS.has(codes[1]) && codes[2] === LINK && codes[3] === 0x43) return 'quirk:7-composer-ra-sign-on-C്ശ (ra sign + C1 + link + ശ: the 7 composer writes 4E C1 E8 - the ra sign AND the ശ്ര ligature E8, and loses the chandrakkala; ശ്ര alone is E8)';
  for (let i = 0; i + 1 < codes.length; i++) if (codes[i] === EE && codes[i + 2] === 0x4F && CONS.has(codes[i + 1])) return 'quirk:not-text (േ + C + ൗ: no Malayalam vowel; the Unicode composer writes േ + ൗ)';
  for (let i = 0; i + 2 < codes.length; i++) if (codes[i] === EE && CONS.has(codes[i + 1]) && YV.has(codes[i + 2]) && codes[i + 3] === 0x4F) return 'quirk:not-text (േ + C + ്യ/്വ + ൗ)';
  if (codes.some(c => ZWNJS.has(c)) && !(codes.indexOf(LINK) >= 0 && ZWNJS.has(codes[codes.indexOf(LINK) + 1]))) return null;
  return null;
}
// the Unicode composer drops whatever follows ്യ / ്വ (except ൢ ൣ): ക ്യ ാ = "ക്യ" while the 7 composer writes 57 7C 4C
function droppedAfterYV(codes, u, exp) {
  const i = codes.findIndex(c => YV.has(c));
  if (i < 0 || i === codes.length - 1) return false;
  const head = codes.slice(0, i + 1);
  const cands = [head, head.filter(c => !PREC.has(c)), codes.filter((c, k) => k <= i || PREC.has(c)), codes.filter((c, k) => k <= i || CONS.has(c) || c === LINK), codes.filter((c, k) => k <= i || CONS.has(c)), codes.filter((c, k) => (k <= i && !PREC.has(c)) || CONS.has(c))];
  return cands.some(x => norm(u) === norm(expected(x)));
}
const probeBy = new Map(probe.filter(r => r.u != null && r.bytes != null).map(r => [r.codes, r]));
function dropped(codes, bytes, u) {
  for (let j = 0; j < codes.length; j++) { const c = codes[j]; if (ZWNJS.has(c) || c === ZWJ) continue; if (NEVER7[c] !== undefined) return 'quirk:7-composer-drops-' + NEVER7[c]; }
  for (let j = 0; j < codes.length; j++) {
    if (ZWNJS.has(codes[j]) || codes[j] === ZWJ) continue;
    const red = codes.filter((_, k) => k !== j).map(h).join(' ');
    const o = probeBy.get(red);
    if (o && o.bytes === bytes && norm(o.u) !== norm(u)) return 'quirk:7-composer-dropped-code-' + h(codes[j]);
  }
  return null;
}
// the link spelling of a letter that has its own sign / ligature code: C + link + ര/യ/വ = C + chandrakkala + the full
// letter (the signs are 59 / 55 / 49), ക + link + ഷ = the chandrakkala form (the ക്ഷ code D5 types the ligature 83)
function linkAlt(codes, bytes) {
  for (let i = 0; i + 2 < codes.length; i++) {
    if (!(CONS.has(codes[i]) && codes[i + 1] === LINK)) continue;
    const c2 = codes[i + 2];
    const b = bytes.split(' ');
    if ((c2 === 0x67 || c2 === 0x75 || c2 === 0x69) && b.some((x, k) => x === '56' && b[k + 1] === { 0x67: '71', 0x75: '70', 0x69: '76' }[c2])) return 'alt:link + ' + { 0x67: 'ര', 0x75: 'യ', 0x69: 'വ' }[c2] + ' (the 7 composer writes C + the chandrakkala 56 + the full letter; the ' + { 0x67: 'ra sign ്ര is code 59 struck before the letter (4E)', 0x75: 'ya sign ്യ is code 55 (7C)', 0x69: 'va sign ്വ is code 49 (7E)' }[c2] + ' - the Unicode composer writes the same ' + { 0x67: 'C്ര', 0x75: 'C്യ', 0x69: 'C്വ' }[c2] + ' for both)';
    if (codes[i] === 0x66 && c2 === 0x24) return 'alt:ക + link + ഷ (the chandrakkala form 57 56 78; the ക്ഷ code D5 on every vendor keyboard types the ligature 83)';
  }
  return null;
}
const pairs = [];
let quirks = 0, byCodes = 0;
function classify(codes, u, bytes, tag, isVendor) {
  const uRead = key(u);
  const exp = expected(codes);
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  if (o7 !== undefined) return { q: 'quirk:7-only-code: ' + ONLY7[o7] };
  if (bytes.split(' ').some(b => parseInt(b, 16) < 0x20)) return { q: 'quirk:7-composer-wrote-a-control-byte (' + bytes + ': 07 = the phonetic keyboards\' carrier, 08 = a backspace before a ligature, 01-1F = control codes echoed)' };
  if (isLoneSign(codes)) return { q: 'quirk:lone-sign (no letter: the Unicode composer shows it on a carrier, the 7 composer writes the bare sign glyph)' };
  if (/[A-Za-z]/.test(uRead) || /[\u0000-\u001F]/.test(uRead)) return { q: 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(u) };
  const kind = isVendor ? 'vendor-key' : orderClass(tag, codes);
  if (kind.startsWith('quirk:')) return { q: kind };
  if (kind.startsWith('alt:')) {
    // filed under what the codes mean when the Unicode composer wrote a carrier
    return { kind, unicode: /[*.]/.test(uRead) ? exp : uRead };
  }
  const r7 = rule7(codes, tag, bytes);
  if (r7) return { q: r7 };
  if (norm(uRead) !== norm(exp)) {
    if (codes.includes(RA) && codes.includes(KSSA) && norm(uRead) === norm(exp.replace('ക്ഷ', 'ഷ'))) return { kind: 'canonical:by-codes (the Unicode composer drops the ക of ക്ഷ under the ra sign: ്ര ക്ഷ = "ഷ്ര"; the 7 composer writes 4E 83; filed under what the codes mean)', unicode: exp, uComposer: u };
    if (droppedAfterYV(codes, uRead, exp)) return { kind: 'canonical:by-codes (the Unicode composer drops whatever follows ്യ/്വ: ക ്യ ാ = "ക്യ"; the 7 composer writes every code, 57 7C 4C; filed under what the codes mean)', unicode: exp, uComposer: u };
    if (isVendor) return { q: 'quirk:vendor-key-types-other-than-its-codes (phonetic mode, a consonant key typing its virama form, or a composer join): U ' + JSON.stringify(u) + ' for codes ' + JSON.stringify(exp) };
    return { q: 'quirk:U-composer-wrote-' + JSON.stringify(uRead) + '-for-' + JSON.stringify(exp) };
  }
  const d = dropped(codes, bytes, uRead);
  if (d) return { q: d };
  const la = linkAlt(codes, bytes);
  if (la) return { kind: la, unicode: uRead };
  return { kind, unicode: uRead };
}
for (const r of vendor) {
  const codes = parse(r.codes);
  const row = { unicode: key(r.unicode), bytes: r.bytes, source: 'vendor ' + r.source + ': codes ' + r.codes, kind: 'vendor-key' };
  const c = classify(codes, r.uRaw, r.bytes, '', true);
  if (c.q) { row.kind = c.q; quirks++; }
  else { if (c.kind !== 'vendor-key') row.kind = c.kind.startsWith('alt:') ? c.kind : 'vendor-key ' + c.kind.replace(/^canonical:?/, '').trim(); row.unicode = key(c.unicode); if (c.uComposer) { row.uComposer = c.uComposer; byCodes++; } }
  if (r.phonetic && row.kind.startsWith('vendor-key')) row.kind = 'vendor-key (phonetic keyboard)';
  pairs.push(row);
}
const evidence = [];
for (const r of probe) {
  if (r.u == null || r.bytes == null) continue;
  const codes = parse(r.codes);
  const row = { unicode: key(r.u), bytes: r.bytes, source: `probe ${r.file.replace('.MAL', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: 'canonical' };
  const c = classify(codes, r.u, r.bytes, r.tag, false);
  if (c.q) { row.kind = c.q; quirks++; if (key(r.u) !== r.u) row.uComposer = r.u; }
  else { row.kind = c.kind; row.unicode = key(c.unicode); if (row.unicode !== r.u) row.uComposer = r.u; if (c.uComposer) byCodes++; }
  if (r.set === 'W' && row.kind.startsWith('canonical')) row.kind = row.kind.replace(/^canonical/, 'canonical:join');
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) main-order canonical or vendor key; (2) vendor keys; (3) fewer bytes; (4) more probes.
const groups = new Map();
for (const p of pairs) { if (!groups.has(p.unicode)) groups.set(p.unicode, []); groups.get(p.unicode).push(p); }
const truth = {};
for (const [u, list] of groups) {
  const ok = list.filter(p => !p.kind.startsWith('alt:') && !p.kind.startsWith('quirk:'));
  if (!ok.length) continue;
  const score = new Map();
  for (const p of ok) {
    const s = score.get(p.bytes) || { bytes: p.bytes, vendor: 0, can: 0, link: 0, n: 0 };
    if (p.kind.startsWith('canonical:link')) s.link++;
    s.n++;
    if (p.kind.startsWith('vendor-key')) s.vendor++;
    if (p.kind.startsWith('canonical')) s.can++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) => b.vendor - a.vendor || (b.can > 0) - (a.can > 0) || (b.link > 0) - (a.link > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  truth[u] = { preferred: ranked[0].bytes, accepted: ranked.map(x => x.bytes), alternatives: [...new Set(list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes))].filter(b => !ranked.some(x => x.bytes === b)) };
}

const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1').replace(/^(quirk:U-composer-carrier-or-placeholder-in-answer).*/, '$1').replace(/^(quirk:7-composer-wrote-a-control-byte).*/, '$1'); kinds[k] = (kinds[k] || 0) + 1; }

// every byte the 7 composer writes, against the font code set (fonts/family-check.json: all 7 faces have the same 189 codes)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['S0501890.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js + ttf.js): (3,1) cmap of every installed Malayalam Shree-Lipi 7 face - WMAL7001/2.TTF (Shree-Mal7-001/002), S0501890/S0502890/S0507890/S0508890/S0520890.TTF (SHREE-MAL7-0501/0502/0507/0508/0520). No 4/5/6 Malayalam face (SHREE-MAL-xxxx) is installed on this PC.',
  result: 'All seven faces have the same 189 codes on the same code points and the same glyph ids (outlines and advances differ: they are different designs). ONE table serves every 7 face.',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
  fontCodesNeverWrittenByTheComposer: [...set7].filter(b => !used.has(b)).sort().join(' '),
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, moreThanOneAccepted: Object.values(truth).filter(t => t.accepted.length > 1).length, withAlternatives: Object.values(truth).filter(t => t.alternatives.length).length, quirks, rowsFiledByCodes: byCodes, vendorKeyboards: 15, vendorRows: vendor.length, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, probeSets: probeSets.map(([f, p]) => p + ' ' + f).join(', '), byKind: kinds, fontCheck,
  notProbed: 'A conjunct slot holds at most 4 codes and the Tutor answers one key at a time with no state, so these are not in the truth (derived in mal7-composer-gaps.json): ൊ ോ ൌ ൈ on a two-consonant cluster (73 C1 64 C2 6C = 5 codes); the ra sign + a cluster + a sign or tail (59 C1 64 C2 + s); a cluster + ്യ/്വ + a sign; C1്C2്C3 (5 codes; the pieces "link C2 link C3" are probed); the dot reph ൎ U+0D4E (no code: no key on any vendor keyboard types it) and the candrabindu ഁ U+0D01, ഀ U+0D00, ഻ ഼ U+0D3B/0D3C, ൔ ൕ ൖ (chillu m/y/llla), ഩ ഺ (no codes).' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Malayalam bytes (fonts SHREE-MAL7-0501/0502/0507/0508/0520 = S05xx890.TTF and Shree-Mal7-001/002 = WMAL7001/2.TTF: the same 189 codes on the same glyphs in all seven; no 4/5/6 Malayalam face is installed), answered by Shree\'s own composers S8MalU64.dll (TutorGetMappedCodeW, Unicode) and S8Mal764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code (bytes 80-9F are raw bytes: in a document they sit on their Windows-1252 code points). Every probe set and every vendor keyboard was captured twice in separate processes: byte-identical. '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 15 shipped .MAL keyboards loaded from its ORIGINAL path (the composers switch typing mode by name: ENG/PHONETIC/ISMENG/NEWROMA/ROMA are phonetic); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Malayalam, which is VISUAL order: the pre-base signs െ (73 -> 52) and േ (53 -> 53), ൈ = 73 73 (52 52), and the ra sign ്ര (59 -> 4E) are struck BEFORE the letter or cluster, the pre-base sign before the ra sign (െ ്ര ക = 52 4E 57); then the consonant; then ്യ (55 -> 7C) / ്വ (49 -> 7E); then the post-base sign (ാ 4C ി 5D ീ 7D ു 4F ൂ 50 ൃ 51 ൗ 54; ൊ = 52 C 4C, ോ = 53 C 4C, ൌ = 52 C 54); then ം 55 / ഃ 40. Conjuncts through the link code 64: one of 55 ligatures (ക്ക A8, ക്ത A9, ന്ത CD, റ്റ E3 ...), C1 + a subjoined form of C2 (ട 9B, ത C5, ദ C9, ധ 9C, ന D4, പ D7, മ DC, ല/ള E6, സ F5, and the doubling stroke FF for ച്ച ബ്ബ വ്വ), or C1 + the chandrakkala 56 + C2; a pending link = the chandrakkala (ക് = 57 56). Chillus are atomic codes (ർ A1 ൽ A4 ൺ A6 ൻ A2 ൾ A5; ൿ has no glyph). canonical:halant = code 44 (virama + ZWNJ: 56). canonical:by-codes = the Unicode composer dropped the sign after ്യ/്വ, the row is filed under what the codes mean. '
  + 'preferred: vendor key, then canonical, then the link spelling (യ്യ: E1 over യ + ya sign 70 7C; വ്വ: 76 FF over വ + va sign 76 7E - the only strings with two canonical spellings), then fewer bytes, then more probes; accepted keeps every canonical spelling. '
  + 'alt:* = the composer answer for another typing order or another visual form (a pre-base sign struck after its letter; the ra sign struck before െ/േ; C + link + ര/യ/വ = the chandrakkala form, not the sign; ക + link + ഷ; C + link + ZWJ, the Unicode 5.0 chillu, which the 7 composer writes as C + 56; pieces with a leading link) - kept, not used as truth. quirk:* = the Unicode composer wrote something other than the codes mean, the 7 composer dropped a code (the precomposed DC DD DE DF, ഌ ൡ ൠ ഽ ൄ ൢ ൣ ൿ, ZWJ/ZWNJ, F0 ഐ, the fractions), a code only the 7 composer types (86, ED), a control byte (E2 link: 08; phonetic carrier 07), a lone sign, ശ്രീ E3 with something after it, the ra sign on C1്ശ - kept for evidence, not used. '
  + 'Unicode keys are NFC (െ+ാ = ൊ, േ+ാ = ോ, െ+ൗ = ൌ). Rules with examples: mal7-patterns.txt. Strings a table needs that the composer cannot be asked in one key (5+ codes): mal7-composer-gaps.json (derived, not truth).';

for (const r of [...probe, ...require('./capx/E-results.json')]) {
  if (r.bytes == null) continue;
  const codes = parse(r.codes);
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  if (r.u == null || o7 !== undefined) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: expected(codes), bytes: r.bytes, why: o7 !== undefined ? 'quirk:7-only-code: ' + ONLY7[o7] : 'the Unicode composer gives no answer', source: `probe ${r.file.replace('.MAL', '')} key ${r.key}` });
}
fs.writeFileSync(__dirname + '/mal7-only7-evidence.json', JSON.stringify({ about: 'Probes the Unicode composer cannot vouch for although the 7 composer typed something: codes only S8Mal764 types (86 -> 2D, ED -> 9E), ശ്രീ E3 alone (E8 7D), and control codes echoed. unicodeByCodes = what the other codes mean - an inference, not a composer answer.', rows: evidence }, null, 1));
fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8MalU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Mal764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
