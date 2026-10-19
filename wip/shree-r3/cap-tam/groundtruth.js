// Build tam7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as pun7/ori7/tel7/kan7-groundtruth.json).
// (adapted from ../cap-kan/groundtruth.js; Tamil rules rewritten)
const fs = require('fs');
const { expected, norm, key, C, PRECOMP, OLD } = require('./expected');
const vendor = require('./vendor-rows.json');
const probeSets = [['capp/P-results.json', 'P'], ['capx/E-results.json', 'E'], ['capw/W-results.json', 'W']];
const probe = [];
{ const seen = new Set(); for (const [f] of probeSets) for (const r of require('./' + f)) { if (seen.has(r.codes)) continue; seen.add(r.codes); probe.push(r); } }
// Round 2 (W): two or three syllables in one keystroke (gen-w.js). wjoin.js: the 7 composer wrote exactly its answers for
// the syllables alone, joined, in 21,224 of 21,224 W probes; the Unicode composer did not in 4,424 (it keeps a pre-base
// sign pending and puts it on the LAST letter of the keystroke: ெ க + கா -> ககொ). So a W row is filed under the
// syllables' own Unicode answers joined (units from seq-w.json), and the Unicode composer's answer is kept in uComposer.
const W_UNITS = new Map(require('./seq-w.json').map(s => [s.codes.map(c => c.toString(16).toUpperCase().padStart(2, '0')).join(' '), s.units]));
const SINGLE = new Map();
for (const [f] of probeSets) for (const r of require('./' + f)) if (!SINGLE.has(r.codes)) SINGLE.set(r.codes, r);
function wJoin(r) {
  const units = W_UNITS.get(r.codes);
  if (!units) return null;
  const parts = units.map(u => SINGLE.get(u.map(c => c.toString(16).toUpperCase().padStart(2, '0')).join(' ')));
  if (parts.some(p => !p || p.u == null || p.bytes == null)) return null;
  // a unit's Unicode = what the codes mean when the Unicode composer agrees; the ெ C ள unit is canonical:au (C + ௌ),
  // which the Unicode composer misreads as Cளெ; any other disagreement makes the W row unusable
  const us = [];
  for (let i = 0; i < units.length; i++) {
    const exp = expected(units[i]);
    if (norm(parts[i].u) === norm(exp)) us.push(parts[i].u);
    else if (units[i].length === 3 && units[i][0] === 0x76 && CONS.has(units[i][1]) && units[i][2] === 0x26) us.push(exp);
    else return null;
  }
  return { u: us.join('').normalize('NFC'), bytes: parts.map(p => p.bytes).join(' '), parts, syllables: us.map(x => x.normalize('NFC')) };
}
const OUT = __dirname + '/tam7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = new Set(Object.keys(C).map(Number));
const GRANTHA = new Set([0x24, 0xD0, 0x51, 0xD2, 0xE3, 0xD7, 0xD8]);
const NATIVE = new Set([...CONS].filter(c => !GRANTHA.has(c)));
const PRE = new Set([0x76, 0x62, 0x75, 0xD3]);
const POSTS = new Set([0x74, 0x68, 0x6A, 0x5D, 0x5E, 0x5F, 0x40, 0x03, 0x81, 0x82, 0x84, 0x85, 0xA1, 0xA2, 0xA3]);
const PULLI = 0x66;
const TAILS = new Set([0x77, 0x20, 0xD1]);
const INDV = new Set([0x46, 0x54, 0x48, 0x4A, 0x3E, 0xD6, 0x56, 0x42, 0x55, 0x7A, 0x5A, 0xA6]);
const PREC = new Set(Object.keys(PRECOMP).map(Number));
const INVISIBLE = new Set([0xA4, 0xA5, 0xEF]); // ZWNJ, ZWJ: no glyph is right
const VEDIC = new Set([0x01, 0x02, 0x06, 0xDA, 0xDB, 0xDE]);
const ALTI = new Set([0x81, 0x82, 0x84, 0x85]);
// codes only the 7 composer types (the Unicode composer writes nothing for them)
const ONLY7 = { 0x03: '03 -> the native ூ ligature (க + 03 = 54 = கூ; alone no answer)', 0xE0: 'E0 -> 40', 0x86: '86 -> 2D (an empty 5-unit glyph)', 0x80: '80 -> B7 93 (dotted circle + pulli dot)', 0x83: '83 -> B7 93', 0x57: '57 -> 60 = சூ (the same byte as ச + ூ 40)' };
// every other code the Unicode composer does not answer alone while the 7 composer types something (single-code probes):
// symbols (× DC -> 23, ÷ DD -> 24, — 7B -> 97, – 7D -> 96, | D9 -> 1C, ` 60 -> 19, ¢ F3 -> 07, ^ F5 -> 0F, \ FE -> 17) and unnamed codes
for (const r of require('./capp/P-results.json')) {
  if (r.codes.includes(' ') || r.bytes == null) continue;
  const c = parseInt(r.codes, 16);
  if ((r.u == null || /^\s*$/.test(r.u)) && ONLY7[c] === undefined && ![0x01, 0x02, 0xDA, 0xDB].includes(c)) ONLY7[c] = r.codes + ' -> ' + r.bytes + ' (the Unicode composer answers nothing for this code)';
}
// codes the 7 composer types NOTHING for, although the Unicode composer writes them
const NEVER7 = { 0x20: 'ஂ (code 20; code 77 = ஂ 54 is typed)', 0x5F: 'ு (code 5F, the InScript ு key g: the 7 composer writes the bare letter)', 0xA1: 'ோ (precomposed code A1)', 0xA2: 'ொ (precomposed code A2)', 0xA3: 'ௌ (precomposed code A3)', 0xA6: 'ஔ (code A6)' };
const NOGLYPH = new Set(['3C', '80', '81', '8A', '8D', '8E', '8F', '90', '9D', '9E']); // not in the SHREE-TAM7-08xx faces (3C only in Shree-Tam7-001/002)
for (let b = 0; b < 0x20; b++) NOGLYPH.add(h(b));

// structural "not text" checks on the code sequence (a syllable: [pre] C [post] [pulli] [tails])
function notText(codes) {
  let state = null, signs = 0, pend = 0, pul = 0;
  for (let i = 0; i < codes.length; i++) {
    const c = codes[i];
    if (CONS.has(c) && !(c === 0x26 && state === 'C' && signs === 1 && codes[i - 2] === 0x76 && codes.indexOf(0x76) === i - 2)) { state = 'C'; signs = pend; pend = 0; pul = 0; continue; }
    if (c === 0x26 && state === 'C') { signs++; continue; } // ெ C ள = ௌ
    if (PREC.has(c)) { if (pend) return 'quirk:pre-base-sign-on-a-precomposed-syllable-code (not text: the Unicode composer writes the sign after the syllable)'; state = 'C'; signs = 1; pul = 0; continue; }
    if (PRE.has(c)) { if (pend) return 'quirk:two-pre-base-signs (not text)'; pend = 1; state = null; continue; }
    if (INDV.has(c) || c === 0xD1) { if (pend) return 'quirk:pre-base-sign-before-a-vowel (not text)'; state = 'V'; signs = 0; pul = 0; continue; }
    if (POSTS.has(c)) {
      if (state === 'V') return 'quirk:vowel-sign-after-an-independent-vowel (not text)';
      if (state !== 'C') return i === 0 || state === null ? 'quirk:sign-with-no-letter-before-it (not text)' : null; // round 2: ுக ா்க were filed as text in round 1
      if (pul) return 'quirk:sign-after-the-pulli (not text: the 7 composer replaces the pulli, க்ி = 51)';
      if (c === 0x74 && signs === 1 && (codes.slice(0, i).some(x => x === 0x76 || x === 0x62))) { signs = 2; continue; }
      if (signs) return 'quirk:two-vowel-signs (not text)';
      signs = 1; continue;
    }
    if (c === PULLI) { if (state === null) return 'quirk:sign-with-no-letter-before-it (not text)'; if (state === 'V') return 'quirk:pulli-after-an-independent-vowel (not text)'; if (state === 'C' && signs) return 'quirk:pulli-after-a-vowel-sign (not text)'; if (pul) return 'quirk:two-pullis (not text)'; pul = 1; continue; }
    if (TAILS.has(c) || INVISIBLE.has(c)) { if (TAILS.has(c) && i + 1 < codes.length && (POSTS.has(codes[i + 1]) || codes[i + 1] === PULLI)) return 'quirk:sign-after-ஂ/ஃ (not text)'; continue; }
  }
  if (pend && codes.some(c => CONS.has(c) || PREC.has(c))) return null;
  return null;
}
function orderClass(tag, codes) {
  for (let i = 0; i + 1 < codes.length; i++) if (NATIVE.has(codes[i]) && (codes[i + 1] === 0x5D || codes[i + 1] === 0x5E)) return 'alt:grantha-u-sign-on-a-native-consonant (5D/5E draw the separate Grantha hook FA/FC after the letter, க + FA; Tamil writes the native ligature: கு = 53 from the precomposed key, கூ = 54 from 40; fonts/u-forms.png)';
  if (codes.includes(0xD3)) { const i = codes.indexOf(0xD3); const nx = codes[i + 1]; if (![0x64, 0x6D, 0x26, 0x70].includes(nx)) return 'alt:old-ai-curl-on-a-letter-that-never-took-it (D3 = F9, the pre-1978 curl of ணை லை ளை னை only; on other letters it is no Tamil form; fonts/old-forms.png)'; }
  for (let i = 0; i + 1 < codes.length; i++) if (PRE.has(codes[i]) && OLD.has(codes[i + 1]) && codes[i + 1] !== 0xD3) return 'alt:old-ligature-with-a-pre-base-sign (ெ + னா = the pre-1978 னொ, F6 E0; the Unicode composer misreads it as னாெ)';
  if (codes.some(c => OLD.has(c))) return 'canonical:old-orthography (pre-1978 ligatures னா E0, றா D9, ணா 74 and the ை curl F9 on ணை லை ளை னை: accepted, ranked after the modern spelling)';
  for (let i = 0; i + 1 < codes.length; i++) if (GRANTHA.has(codes[i]) && codes[i + 1] === 0x40 && [0xD0, 0xD7].includes(codes[i])) return 'canonical:secondary (ஷ/க்ஷ + the ூ code 40 = the generic Grantha hook FC; the code 5E gives the ஷ-shaped hook FD: preferred)';
  if (codes[0] === 0x76 && CONS.has(codes[1]) && codes[2] === 0x26) return 'canonical:au (ெ + C + ள = C + ௌ: the 7 composer writes F6 C CD, the au length mark drawn as ள; the Unicode composer misreads the keystrokes as Cளெ)';
  if (codes.includes(PULLI) && codes.some((c, i) => i > codes.indexOf(PULLI) && CONS.has(c))) return 'canonical:cluster (C1 + pulli 66 + C2: C1் has its own glyph, then C2 as a syllable)';
  return 'canonical';
}
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
function rule7(codes, bytes) {
  if (codes.some(c => VEDIC.has(c))) return 'quirk:vedic-accent-code (॑ ॒ ᳚: no glyph in any Tamil face; the 7 composer drops it, writes a control byte, or after a letter writes its ி/ீ form for 01/02)';
  if (codes.includes(0x77)) return 'quirk:7-composer-writes-a-wrong-glyph-for-ஂ (code 77 -> 54, which is the கூ glyph in every Tamil face: no face has an anusvara; fonts/symbols.png)';
  if (codes.includes(0xED)) return 'quirk:7-composer-writes-a-wrong-glyph-for-॥ (code ED -> 88, the same byte as ௳ E2: no face has a double danda)';
  if (codes.some(c => ALTI.has(c))) return 'quirk:alternate-i-sign-code-81/82/84/85 (the 7 composer writes the sign on the dotted circle B7 91/92 after the letter)';
  const nt = notText(codes); if (nt) return nt;
  if (bytes.split(' ').some(b => NOGLYPH.has(b))) return 'quirk:7-composer-wrote-a-byte-with-no-glyph (' + bytes.split(' ').filter(b => NOGLYPH.has(b)).join(' ') + ': no Tamil face has a glyph there)';
  if (codes[0] === 0xD8 && codes.length > 1 && !TAILS.has(codes[1])) return 'quirk:ஸ்ரீ-code-D8-with-a-sign-or-pulli (not a syllable)';
  // round 2: a pre-base sign struck before ஸ்ரீ (ஸ்ரீ already ends in ீ: ஸ்ரீெ is two vowel signs, not text)
  if (codes.some((c, i) => c === 0xD8 && i > 0 && PRE.has(codes[i - 1]))) return 'quirk:ஸ்ரீ-code-D8-with-a-sign-or-pulli (not a syllable)';
  return null;
}
const loneSign = codes => codes.every(c => PRE.has(c) || POSTS.has(c) || c === PULLI || TAILS.has(c)) && codes.some(c => PRE.has(c) || POSTS.has(c) || c === PULLI);

const pairs = [];
let quirks = 0;
for (const r of vendor) {
  const row = { unicode: key(r.unicode), bytes: r.bytes, source: 'vendor ' + r.source + (r.codes ? ': codes ' + r.codes : ''), kind: 'vendor-key' };
  let q = null;
  if (r.codes) {
    const codes = parse(r.codes);
    const exp = expected(codes);
    if (/[A-Za-z]|\u0000/.test(r.uRaw)) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(r.uRaw);
    else if (/\.[\u0BBE-\u0BCD]/.test(r.uRaw) && r.uRaw.length > 2) q = 'quirk:pre-base-sign-struck-after-its-consonant (the Unicode composer puts it on a . carrier)';
    else if (loneSign(codes)) q = 'quirk:lone-vowel-sign-key (a sign key alone: the Unicode composer writes the sign, on a carrier for ெ ே ை; the 7 composer the bare sign glyph, or nothing for ி ீ ் 40 5F)';
    else if (codes.length === 3 && codes[0] === 0x76 && CONS.has(codes[1]) && codes[2] === 0x26 && norm(exp) !== norm(r.unicode)) { row.unicode = key(exp); row.uComposer = r.uRaw; q = null; }
    else if (norm(exp) === norm(r.unicode)) q = rule7(codes, r.bytes) || dropped(codes, r.bytes, r.unicode);
    else q = 'quirk:vendor-key-types-other-than-its-codes (phonetic mode by keyboard name, the INSCRIPT/NEWROMA/PHONET86 built-in ோ ொ ௌ, or ஒ+ள read as ஒள): U ' + JSON.stringify(r.uRaw) + ' for codes ' + JSON.stringify(exp);
    if (!q && codes.some(c => ONLY7[c] !== undefined)) q = 'quirk:7-only-code: ' + ONLY7[codes.find(c => ONLY7[c] !== undefined)];
    if (!q) { const oc = orderClass('', codes); if (oc.startsWith('alt:') || /old-orthography|secondary|au/.test(oc)) q = oc; }
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
  const row = { unicode: uRead, bytes, source: `probe ${r.file.replace('.TAM', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: orderClass(r.tag, codes) };
  if (uRead !== u) row.uComposer = u;
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  let q = null;
  const wj = r.tag.startsWith('W') ? wJoin(r) : null;
  if (r.tag.startsWith('W')) {
    if (!wj) q = 'quirk:W-probe-with-a-unit-that-has-no-answer';
    else if (wj.bytes !== bytes) q = 'quirk:W-probe-7-bytes-not-the-syllables-joined';
    else {
      row.unicode = key(wj.u);
      row.syllables = wj.syllables;
      if (uRead !== row.unicode) row.uComposer = u;
      row.kind = row.kind.startsWith('alt:') || /old-orthography|secondary/.test(row.kind) ? row.kind : (uRead === row.unicode ? 'canonical:join (syllables typed in one keystroke; the 7 composer wrote their own answers joined)' : 'canonical:join (syllables typed in one keystroke; the 7 composer wrote their own answers joined; filed under the syllables\' Unicode answers joined, because the Unicode composer moves a pre-base sign to the last letter of the keystroke)');
      q = rule7(codes, bytes);
      if (q && /^quirk:(pre-base|two-|pulli-after|sign-after|vowel-sign-after|ஸ்ரீ-code-D8)/.test(q)) q = null; // these checks read a multi-syllable keystroke as one syllable; the units were checked one by one
    }
  }
  else if (o7 !== undefined) q = 'quirk:7-only-code: ' + ONLY7[o7];
  else if (/[A-Za-z]|[\u0000-\u001F]/.test(uRead.replace(/[ -~]/g, m => /[A-Za-z]/.test(m) ? m : ''))) q = 'quirk:U-composer-carrier-or-placeholder-in-answer ' + JSON.stringify(u);
  else if (/\.[\u0BBE-\u0BCD]/.test(r.uRaw || '') && codes.length > 1 && codes[0] !== 0x2E) q = 'quirk:pre-base-sign-struck-after-its-consonant-or-twice (the Unicode composer puts it on a . carrier; the 7 composer writes the sign glyph where it was struck)';
  else if (loneSign(codes)) q = 'quirk:lone-vowel-sign (no letter: the Unicode composer writes the sign, on a carrier for ெ ே ை; the 7 composer the bare sign glyph F5 F6 F7 F8 F9 FA FC, or nothing for ி ீ ் 40 5F)';
  else if (/^canonical:au/.test(row.kind) && norm(uRead) !== norm(exp) && codes.length === 3) { row.unicode = key(exp); row.uComposer = u; }
  else if (/^alt:old-ligature-with-a-pre-base-sign/.test(row.kind) && codes.length === 2 && (codes[0] === 0x76 || codes[0] === 0x62)) { row.unicode = key(exp); row.uComposer = u; }
  else if (norm(uRead) !== norm(exp)) q = 'quirk:U-composer-wrote-' + JSON.stringify(uRead) + '-for-' + JSON.stringify(exp);
  if (!q && !r.tag.startsWith('W')) q = rule7(codes, bytes) || (row.kind.startsWith('alt:') ? null : dropped(codes, bytes, row.unicode));
  if (q) { row.kind = q; quirks++; }
  if (q && q.startsWith('quirk:7-only')) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: exp, bytes, why: q, source: row.source });
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) a main canonical spelling (not old orthography, not secondary); (2) vendor keys; (3) canonical;
// (4) fewer bytes; (5) more probes. Accepted = every canonical and vendor spelling.
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
    if (p.kind.startsWith('canonical') && !/old-orthography|secondary/.test(p.kind)) s.main++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) => (b.main > 0) - (a.main > 0) || b.vendor - a.vendor || (b.can > 0) - (a.can > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  truth[u] = { preferred: ranked[0].bytes, accepted: ranked.map(x => x.bytes), alternatives: [...new Set(list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes))].filter(b => !ranked.some(x => x.bytes === b)) };
}

// W strings: the composer joins syllables as they are, so every join of the syllables' ACCEPTED spellings is a spelling
// Shree types for the same text (e.g. the old னா ligature E0 inside a word); preferred stays the join of the preferred.
let wAcceptedAdded = 0;
for (const [u, t] of Object.entries(truth)) {
  const ps = groups.get(u);
  if (!ps.every(p => p.syllables)) continue;
  const syl = ps.find(p => p.syllables && !p.kind.startsWith('quirk:'));
  if (!syl || syl.syllables.some(s => !truth[s])) continue;
  let combos = [''];
  for (const s of syl.syllables) { const next = []; for (const c of combos) for (const a of truth[s].accepted) next.push(c ? c + ' ' + a : a); combos = next.slice(0, 64); }
  for (const c of combos) if (!t.accepted.includes(c)) { t.accepted.push(c); wAcceptedAdded++; }
}
const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1').replace(/^(quirk:U-composer-carrier-or-placeholder-in-answer).*/, '$1').replace(/^(quirk:7-composer-wrote-a-byte-with-no-glyph).*/, '$1').replace(/^(quirk:7-composer-dropped-code-).*/, '$1*'); kinds[k] = (kinds[k] || 0) + 1; }

// Composer gaps: Unicode strings a table needs that Shree's 7 composer never spells directly, derived from its own answers.
const gaps = [];
for (const [u, t] of Object.entries(truth)) {
  const m = /^(.+)ௌ$/.exec(u);
  if (m && [...m[1]].length === 1) { const e = m[1] + 'ெள'; if (!truth[e]) gaps.push({ unicode: e, derived: t.preferred, from: u, why: 'Cெ + ள and C + ௌ are the same bytes in this encoding (the au length mark is the ள glyph CD; Shree has no separate ௗ glyph): F6 C CD. A reverse table must pick one reading; the vendor keyboards type ௌ this way (slots 76 26 = ெள).' }); }
}
if (!truth['ஔ']) gaps.push({ unicode: 'ஔ', derived: (truth['ஒள'] || {}).preferred || '4A CD', from: 'ஒள', why: 'the vendor ஔ keys (slots 7A 26 on IE, INSCRIPT, KANIAN, MONO, PHONET86, PHONETIC, ENG, TAMIL99, ...) type ஒ + ள; the Unicode composer writes ஒள (U+0B92 U+0BB3), not ஔ (U+0B94 = ஒ + ௗ), and the ஔ code A6 has no 7 answer. Derived: ஒ 4A + the ள glyph CD.' });
for (const [u, s] of [['ௌ', 'F6 CD'], ['ொ', 'F6 F5'], ['ோ', 'F7 F5']]) if (!truth[u]) gaps.push({ unicode: u, derived: s, from: 'ெ/ே + ா/ள', why: 'a lone two-part sign has no composer spelling (A1-A3 have no 7 answer); derived from the lone pre-base sign glyph + the post part' });
// Round 2: symbols the composers do not settle, derived from the glyphs (dupglyph.js, fonts/symbols.png) and the 7-only evidence
const addGap = (u, derived, why) => { if (!truth[u]) gaps.push({ unicode: u, derived, from: 'glyph check / 7-only evidence', why }); };
addGap('-', '26', 'HYPHEN-MINUS U+002D has no Shree code: the - key (code 2D, on 23 keyboards) makes the Unicode composer write EN DASH U+2013 and the 7 composer the dash glyph 26 (truth: – = 26). Byte 2D itself is an EMPTY glyph (advance 5) in every SHREE-TAM7/TAM face, so passing - through as 2D prints nothing. 26 and AD draw the identical dash.');
addGap('—', '97', 'EM DASH: code 7B is answered only by the 7 composer (97); 97 draws the long dash in every face.');
addGap('÷', '24', 'code DD is answered only by the 7 composer (24); 24 draws ÷.');
addGap('×', '96', 'the chart key (Ctrl+Shift+1 *) holds code DC, and the 7 composer writes 23 for it - but 23 draws ய் (the same outline as B4). The × glyph is 96 (fonts/symbols.png). Shree itself prints ய் for ×.');
for (const [u, why] of [
  ['ஂ', 'U+0B82: the 7 composer writes 54 (= the கூ glyph) for code 77 and nothing for code 20; no Tamil face has an anusvara glyph'],
  ['₹', 'code DB: the Unicode composer answers a space, the 7 composer 1E (no glyph); no Tamil face has a rupee glyph'],
  ['॥', 'code ED: the 7 composer writes 88, the ௳ glyph; no face has a double danda'],
  ['௶ ௷', 'codes 5C/7C: the 7 composer writes 09/0A, which have no glyph in any face'],
  ['# $ < > @ _ [ ] { } \\ | ` ^ & “ ”', 'the 7 composer writes control bytes 06-1C for them (# 12, $ 13, < 14, > 15, @ 16, _ 18, [ 0B, ] 0C, { 1A, } 1B, “ 0D, ” 0E, \\ 17, | 1C, ` 19, ^ 0F, & 06) - no glyph in any face; their own ASCII bytes are TAMIL glyphs in these fonts (23 ய், 24 ÷, 26 dash, 3C empty/absent, 3E ।, 40 ே, 5B ங், 5C ச, 5D சி, 5E சீ, 5F சு, 60 சூ, 7B நி, 7C ந, 7D நீ, 7E நு). Nothing a table writes shows the symbol in a Shree Tamil font.'],
  ['௦-௯ (as Western digits)', 'not a gap but a policy: bytes 30-39 draw WESTERN digits; Shree\'s Unicode composer writes Tamil digits U+0BE6-0BEF for the same keys (truth). ASCII digits 0-9 have no separate Shree code and pass through to the same bytes 30-39.'],
]) gaps.push({ unicode: u, derived: null, from: 'no spelling', why });
// Cு for Grantha/native with 5F (InScript g): the 7 composer drops it; the native ligature comes from the precomposed keys
// every byte the 7 composer writes, against the font code sets (fonts/family-check.json)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['WTAM7001.TTF'].codeList.split(' ')), setS7 = new Set(fam.faces['S0800890.TTF'].codeList.split(' ')), set456 = new Set(fam.faces['S08000F0.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js + ttf.js): (3,1) cmap of every installed Tamil Shree face - 7: WTAM7001/2.TTF (Shree-Tam7-001/002), S0800890..S0803890 + S1316890 + S3883890 (SHREE-TAM7-0800..0803/1316/3883); 4/5/6: S08000F0..S08030F0 + S13160F0 + S38830F0 (SHREE-TAM-0800..0803/1316/3883). Per code: glyph id, advance, sha1 of the glyf bytes.',
  result: 'The 12 SHREE-TAM7-xxxx / SHREE-TAM-xxxx faces have the same 213 codes (20-7E except 3C, 82-89, 8B 8C, 91-9C, 9F, A0-FF) on the same code points; same-number pairs are identical in 210 codes and differ ONLY in 9B 9F BA (ரி ரீ ர், redrawn). Shree-Tam7-001/002 add 3C. One table serves both families code for code; the 7 vs 4/5/6 difference is the drawing of ரி ரீ ர் only (same Unicode, same bytes).',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b) && !setS7.has(b) && !set456.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
  bytesInTruthNotIn456: [...used.keys()].filter(b => setS7.has(b) && !set456.has(b)).sort(),
  glyphCheck: {
    sheets: 'fonts/all-0800-20-9F.png, fonts/all-0800-A0-FF.png (every code of SHREE-TAM7-0800), fonts/u-forms.png, fonts/old-forms.png, fonts/symbols.png, fonts/ri-rii-r-7-vs-456.png',
    rightAsWritten: 'every consonant block (base, ி, ீ, ு, ூ, ்), ா F5, ெ F6, ே F7, ை F8, the old ை curl F9, the Grantha hooks FA FB FC FD, ஸ்ரீ FF, க்ஷ F1-F4, the old னா E0 / றா D9 / ணா 74, ஃ 4C, । 3E, ‘ ’ 22 27, ÷ 24, — 97, the Tamil number/calendar signs 84 85 98 4D 4E 4F 99 9A FE (shapes consistent, not verified against a reference)',
    wrongGlyph: { 'ஂ (code 77)': '54 = the கூ glyph (no Tamil face has an anusvara): rows filed as quirk, not truth', '௦-௯ (codes 30-39)': 'bytes 30-39 draw WESTERN digits 0-9 (no Tamil digit glyphs in any face); the Unicode composer writes Tamil digits for these codes', '॥ (code ED)': '88, the same byte as ௳ (E2); no double danda in any face: rows filed as quirk, not truth', '× (code DC, only7 evidence)': '23 draws ய்; the × glyph is 96', '– (code 7D, only7 evidence)': '96 draws ×; the dash glyph is 26 (code 2D)', 'symbols under 20': '௶ 09, ௷ 0A, [ ] 0B 0C, “ ” 0D 0E, # $ < > @ _ { } 12-1B, ₹ 1E, | 1C: no glyph in any face' },
  },
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, moreThanOneAccepted: Object.values(truth).filter(t => t.accepted.length > 1).length, withAlternatives: Object.values(truth).filter(t => t.alternatives.length).length, quirks, vendorKeyboards: 22, vendorRows: vendor.length, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, probeSets: probeSets.map(([f, p]) => p + ' ' + f).join(', '), composerGaps: gaps.length, wStringsAcceptedSpellingsAddedFromSyllableJoins: wAcceptedAdded, byKind: kinds, fontCheck,
  wProbes: 'round 2 (gen-w.js, capw/ + capw-b/ byte-identical): 21,224 keystrokes of two or three syllables (every pair of 13 syllable shapes on 11 letters + 12 vowels; all 24 letters with a pre-base sign before/after a neighbour; 78 common words that fit in 4 codes and 353 syllable pairs/triples from 341 common words). The 7 composer wrote the syllables\' own answers joined in 21,224/21,224 (wjoin.json): Tamil syllables join by plain concatenation, no rule reaches across them. The Unicode composer did not in 4,424 (a pre-base sign stays pending and lands on the last letter of the keystroke) - those rows are filed under the syllables\' answers joined.',
  notProbed: 'A slot holds at most 4 codes, so these are not in the truth as one keystroke: a two-part sign (ொ ோ ௌ = 3 codes) on the second consonant of a cluster (C1 66 ெ C2 ா = 5 codes; C1 66 ெ C2 IS probed, and Tamil clusters are plain syllables: C1் + the C2 syllable, which every probed cluster confirms; the W round confirms syllables join with no cross-syllable effect); clusters of three consonants (C1 66 C2 66 is probed, C3 follows as its own syllable); words longer than 4 codes (their syllable pairs and triples are probed). Tamil has no reph, no nukta and no subjoined forms, so those probe classes do not exist for this script.' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Tamil bytes (fonts SHREE-TAM7-0800..0803/1316/3883 = S0800890..S0803890/S1316890/S3883890.TTF and Shree-Tam7-001/002 = WTAM7001/2.TTF; the 4/5/6 fonts SHREE-TAM-0800..0803/1316/3883 = S08000F0..S08030F0/S13160F0/S38830F0.TTF have the same code set, identical glyphs except 9B 9F BA = ரி ரீ ர் redrawn), answered by Shree\'s own composers S8TamU64.dll (TutorGetMappedCodeW, Unicode) and S8Tam764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code (bytes 80-9F are raw bytes: in a document they sit on their Windows-1252 code points; 80 81 8A 8D 8E 8F 90 9D 9E and every byte under 20 have no glyph). Every probe set and every vendor keyboard was captured twice in separate processes: byte-identical. '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 22 shipped .TAM keyboards loaded from its ORIGINAL path (the composers switch typing mode by name); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Tamil (visual order): the pre-base sign first (ெ 76 = F6, ே 62 = F7, ை 75 = F8), then the consonant (each consonant has a block: base, +1 ி, +2 ீ, +3 ு, +4 ூ, +5 ்: க 50 51 52 53 54 55; exceptions ந 7C 7B 7D 7E A1 A2, ம A9 AA AB 95 89 AE, ர B5 9B 9F B8 B9 BA), then the post-base sign (ா 74 = F5 after the letter; ொ = F6 C F5, ோ = F7 C F5, ௌ = F6 C CD); the native ு comes from the precomposed codes 41-59/7E/7F/D5 (கு = 53), the native ூ from code 40 (கூ = 54); on the Grantha letters ஜ ஷ ஸ ஹ ஶ க்ஷ the separate hooks FA/FC (FB/FD on ஷ க்ஷ) from codes 5D/5E; clusters = C1 + pulli 66 (the C1் glyph) + C2 as its own syllable; ஸ்ரீ = FF (code D8) or ஸ் + ரீ = EC 9F; க்ஷ = F1 (code D7) or க் + ஷ = 55 E5. '
  + 'canonical:au = ெ + C + ள filed under C + ௌ (the vendor ௌ keys type exactly this; the Unicode composer misreads it as Cளெ, kept in uComposer). canonical:old-orthography = the pre-1978 ligatures (னா E0, றா D9, ணா 74, the ை curl F9 on ணை லை ளை னை): accepted, ranked after the modern spelling. canonical:secondary = ஷ/க்ஷ + 40 (generic hook FC): accepted, ranked after 5E (FD). '
  + 'canonical:join = round 2 (W probes): two or three syllables typed in one keystroke; the 7 composer wrote the syllables\' own answers joined in 21,224 of 21,224 probes, so these rows are filed under the syllables\' own Unicode answers joined (the Unicode composer keeps a pre-base sign pending and puts it on the last letter of the keystroke - its answer is kept in uComposer). '
  + 'preferred: a main canonical spelling, then vendor key, then fewer bytes, then more probes; accepted keeps every canonical and vendor spelling. '
  + 'alt:* = the composer answer for a visually non-standard form, kept, not used as truth: the Grantha hook 5D/5E on a native consonant (க + FA), the ை curl D3 on a letter that never took it, an old ligature with a pre-base sign. quirk:* = the Unicode composer wrote something other than the codes mean (a pre-base sign struck after its letter goes on a . carrier; ெ C + ஂ/ஃ/joiner puts ெ after them), or the 7 composer dropped a code the Unicode composer wrote (5F ு, 20 ஂ, A1-A3, A6), a code only the 7 composer types (03 E0 86 80 83 57), not text (two signs, a sign after the pulli or after an independent vowel), Vedic accents, a byte with no glyph in any face (the symbol codes the 7 composer writes under 20: ௶ 09, ௷ 0A, [ ] 0B 0C, “ ” 0D 0E, # $ < > @ _ { } 12-1B, ₹ 1E, | 1C), or a lone vowel sign - kept for evidence, not used. '
  + 'Unicode keys are what the Unicode composer wrote, in NFC (ெ+ா = ொ, ே+ா = ோ, ெ+ௗ = ௌ), except canonical:au as said. Rules with examples: tam7-patterns.txt. Strings a table needs that the composer never spells directly: tam7-composer-gaps.json (derived, not truth).';

for (const r of probe) {
  if (r.bytes == null) continue;
  const codes = parse(r.codes);
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  if (r.u == null || o7 !== undefined && !evidence.some(e => e.codes === r.codes)) evidence.push({ codes: r.codes, tag: r.tag, uComposer: r.u, unicodeByCodes: expected(codes), bytes: r.bytes, why: o7 !== undefined ? 'quirk:7-only-code: ' + ONLY7[o7] : 'the Unicode composer gives no answer', source: `probe ${r.file.replace('.TAM', '')} key ${r.key}` });
}
fs.writeFileSync(__dirname + '/tam7-only7-evidence.json', JSON.stringify({ about: 'Probes the Unicode composer cannot vouch for although the 7 composer typed something: codes only S8Tam764 types (' + Object.values(ONLY7).join('; ') + ') and symbol codes the Unicode composer does not answer (× DC -> 23, ÷ DD -> 24, — 7B -> 97, – 7D -> 96, | D9 -> 1C, ` 60 -> 19, ¢ F3 -> 07, ^ F5 -> 0F, \\ FE -> 17). unicodeByCodes = what the codes mean by Shree\'s own code table (assets/config/shree-kbd/TAM.json) - an inference, not a composer answer. bytes = the 7 composer answer.', rows: evidence }, null, 1));
fs.writeFileSync(__dirname + '/tam7-composer-gaps.json', JSON.stringify({ about: 'Unicode strings with no direct Shree 7 composer spelling, with a spelling derived from the composer\'s own answers. NOT part of the truth.', gaps }, null, 1));
fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8TamU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Tam764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
