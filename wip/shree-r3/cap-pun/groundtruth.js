// Build pun7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as ban7-groundtruth.json).
const fs = require('fs');
const { CODES } = require('./lib');
const { expected, norm, key } = require('./expected');
const vendor = require('./vendor-rows.json');
const probe = [...require('./capp/P-results.json'), ...require('./caph/H-results.json'), ...require('./capw/S-results.json')];
const OUT = __dirname + '/pun7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const PAIRIN = new Set([0x6A, 0x6F, 0x79, 0x75]); // ਰ ਹ ਵ ਯ: Unicode C੍ਰ C੍ਹ C੍ਵ C੍ਯ draw the subjoined form (codes D1 D2 D0 4C)
const INVISIBLE = new Set([0xE2, 0xE3, 0x64]); // ZWJ, ZWNJ and the link (virama typed with code 64): no glyph is right
const ADDAK_GLYPHS = ['A7', 'A8'];
// codes whose glyph the 7 composer never writes although the Unicode composer writes the character
const NEVER7 = { 0x7C: '“ (code 7C)', 0x7E: '” (code 7E)', 0xD9: '॥ (code D9)', 0xE0: 'ੴ (code E0)', 0xE4: 'ਇ (code E4; ਇ is typed ਿ + ੲ = 71 42)' };

function orderClass(tag, codes) {
  if (/^wide /.test(tag)) return 'alt:another typing order of ਠ/ਨ/ਕ + subjoined + sign + tail (probe set S, evidence for the composer gap: ਨ ੍ਹ ਂ ਾ = B2 A3 BE AA 70, bindi on the letter)';
  if (/after-ਿ/.test(tag)) return 'alt:ਿ struck after the letter (the 7 composer writes the i-sign glyph 71 AFTER the letter, and loses a subjoined code or bindi/tippi struck after it)';
  if (/ before ੍/.test(tag)) return 'alt:vowel sign struck before the subjoined code';
  if (/ (ੱ|ਂ|ੰ) before /.test(tag)) return 'alt:mark struck before the vowel sign';
  const iLink = codes.indexOf(0x64), iHal = codes.indexOf(0x44);
  if (iLink >= 0 && PAIRIN.has(codes[iLink + 1])) return 'alt:C+link+ਰ/ਹ/ਵ/ਯ (two full letters, the virama has no glyph; Unicode C੍ਰ C੍ਹ C੍ਵ C੍ਯ is the subjoined form, codes D1 D2 D0 4C)';
  if (iHal >= 0 && PAIRIN.has(codes[iHal + 1])) return 'alt:C+halant+ਰ/ਹ/ਵ/ਯ (visible halant B5 + full letter, not the subjoined form)';
  if (iLink >= 0 && codes[iLink + 1] === undefined) return 'canonical:pending-link (virama typed with the link code 64, last: the 7 composer writes no glyph for it)';
  if (iLink >= 0 && (codes[iLink + 1] === 0xE2 || codes[iLink + 1] === 0xE3)) return 'canonical:link+ZWJ/ZWNJ (no glyph for the virama or the joiner)';
  if (iLink >= 0) return 'canonical:link (virama typed with the link code 64: no glyph for it, the letters stand side by side)';
  if (iHal >= 0) return 'canonical:halant (virama typed with the halant code 44: the visible halant B5)';
  if (/^vendor-slot/.test(tag)) return 'canonical:vendor-slot';
  if (codes.includes(0xD7)) return 'canonical:nukta-letter';
  return 'canonical';
}

// a probe whose code the 7 composer ignored: same bytes as the probe without that code, while the Unicode composer did write it
const probeBy = new Map(probe.filter(r => r.u != null && r.bytes != null).map(r => [r.codes, r]));
function dropped(codes, bytes, u) {
  for (const c of codes) if (NEVER7[c] !== undefined) return 'quirk:7-composer-drops-' + NEVER7[c];
  const b = bytes.split(' ');
  // every vowel sign typed must leave its glyph (or a ligature holding it), and BE follows each ਠ/ਨ once
  for (const [vc, gl, name] of [[0x6B, ['70'], 'ਾ'], [0x68, ['72', '73'], 'ੀ'], [0x62, ['74', '75', 'AE'], 'ੁ'], [0x6E, ['76', '77', 'AF'], 'ੂ'], [0x6C, ['79', '7A'], 'ੇ'], [0x70, ['B1', '7C'], 'ੈ'], [0x74, ['7D', '7E'], 'ੋ'], [0x54, ['A1', 'A2'], 'ੌ'], [0x67, ['71'], 'ਿ']])
    if (codes.includes(vc) && !gl.some(g => b.includes(g)) && !/^[ਅ-ਔ]/.test(u)) return `quirk:7-composer-loses-the-vowel-sign-${name} (ਠ/ਨ + subjoined + ਾ/ੁ/ੂ + ਂ/ੰ/ੱ: ਠ੍ਹਾਂ = 53 BE A3 BE AA, no 70 and BE twice)`;
  const nBE = b.filter(x => x === 'BE').length, nWide = b.filter(x => x === '53' || x === 'B2').length;
  if (nBE > nWide) return 'quirk:7-composer-writes-the-spacer-BE-twice';
  for (const [sc, gl, name] of [[0xD1, ['6F', '6E'], '੍ਰ'], [0xD2, ['AA'], '੍ਹ'], [0xD0, ['AB'], '੍ਵ'], [0x4C, ['62'], '੍ਯ']])
    if (codes.includes(sc) && !gl.some(g => b.includes(g))) return `quirk:7-composer-loses-the-subjoined-${name} (two subjoined codes in a row: ਕ੍ਹ੍ਰ = 48 6E = ਕ + ਕ੍ਰ; or ਠ/ਨ + subjoined + a top sign + bindi: ਠ੍ਰੇਂ = 53 79 BE 7A, the sign written twice)`;
  if (codes.includes(0x40) && !ADDAK_GLYPHS.some(g => b.includes(g))) return 'quirk:7-composer-loses-the-addak ੱ (ੱ then ਂ after a sign that has a bindi ligature: the sign is written twice, no addak)';
  if (codes.includes(0xD7) && u.normalize('NFD').includes('\u0A3C') && !/[\u0A33\u0A36\u0A59-\u0A5B\u0A5E]/.test(u) && !/[ਲਸਖਗਜਫ]\u0A3C/.test(u.normalize('NFC')))
    return 'quirk:7-font-has-no-nukta-for-this-letter (only ਸ਼ ਖ਼ ਗ਼ ਜ਼ ਫ਼ ਲ਼ have a glyph: 66 69 6A 6B 6C B4; the composer writes the plain letter)';
  for (let j = 0; j < codes.length; j++) {
    if (INVISIBLE.has(codes[j])) continue;
    const red = codes.filter((_, k) => k !== j).map(h).join(' ');
    const o = probeBy.get(red);
    if (o && o.bytes === bytes && norm(o.u) !== norm(u)) return 'quirk:7-composer-dropped-code-' + h(codes[j]) + ' (' + (CODES[codes[j]] ?? '?') + ')';
  }
  return null;
}
// what the Unicode composer writes that the codes alone do not say, but is the composers' own rule (accepted)
function composerRule(codes, u, exp) {
  const nu = norm(u);
  if (nu === norm(exp.replace(/\u0A70/g, '\u0A02')) && /[\u0A3E\u0A40\u0A47\u0A48\u0A4B\u0A4C\u0A09\u0A0A\u0A73]\u0A70/.test(exp))
    return 'canonical:tippi-typed-written-as-bindi (after ਾ ੀ ੇ ੈ ੋ ੌ ਉ ਊ ੳ both composers write bindi ਂ for a tippi ੰ)';
  if (nu === norm(exp.replace(/\u0A71\u0A02/g, '\u0A01')) || nu === norm(exp.replace(/\u0A70/g, '\u0A02').replace(/\u0A71\u0A02/g, '\u0A01')))
    return 'canonical:addak+bindi-written-as-adak-bindi (the Unicode composer writes ੱ + ਂ as U+0A01 ਁ)';
  if (codes.length === 2 && codes[0] === 0x31 && codes[1] === 0x51 && u === 'ੴ')
    return 'canonical:ੴ = ੧ + onkar arc (vendor slot 31 51; the Unicode composer writes U+0A74, the 7 composer 31 B0)';
  return null;
}
// characters the fonts cannot draw / composer answers that draw something else
function badBytes(u, bytes, codes) {
  const b = bytes.split(' ');
  if (b.includes('07')) return 'quirk:7-composer-carrier-07 (a phonetic keyboard answers a lone sign on byte 07, which no SHREE-PUN font has)';
  if (u === 'ੴ' && bytes === 'B0') return 'alt:ੴ typed as code 51 alone = B0, the onkar arc WITHOUT the ੧ (every SHREE-PUN7 / SHREE-PUN font draws B0 so; the complete ੴ is 31 B0)';
  return null;
}

const pairs = [];
let quirks = 0;
for (const r of vendor) {
  const row = { unicode: key(r.unicode), bytes: r.bytes, source: 'vendor ' + r.source + (r.codes ? ': codes ' + r.codes : ''), kind: 'vendor-key' };
  let q = badBytes(r.unicode, r.bytes);
  if (!q && r.codes) {
    const codes = parse(r.codes);
    const exp = expected(codes);
    if (norm(exp) === norm(r.unicode)) q = dropped(codes, r.bytes, r.unicode);
    else {
      const rule = composerRule(codes, r.unicode, exp);
      if (rule && rule.startsWith('canonical:ੴ')) row.kind = 'vendor-key';
      else if (!rule) q = 'quirk:vendor-key-types-other-than-its-codes (phonetic/inscript mode): U ' + JSON.stringify(r.unicode) + ' for codes ' + JSON.stringify(exp);
    }
  }
  if (q) { row.kind = q; if (q.startsWith('quirk:')) quirks++; }
  pairs.push(row);
}
for (const r of probe) {
  if (r.u == null || r.bytes == null) continue;
  const codes = parse(r.codes);
  const u = r.u;
  const exp = expected(codes);
  const row = { unicode: key(u), bytes: r.bytes, source: `probe ${r.file.replace('.PUN', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: orderClass(r.tag, codes) };
  const bad = badBytes(u, r.bytes, codes);
  if (/[\u0000-\u001F]/.test(u)) { row.kind = 'quirk:control-character-echo'; quirks++; }
  else if (bad) { row.kind = bad; if (bad.startsWith('quirk:')) quirks++; }
  else if (norm(u) !== norm(exp)) {
    const rule = composerRule(codes, u, exp);
    if (rule && !row.kind.startsWith('alt:')) { row.kind = rule; const q = dropped(codes, r.bytes, u); if (q) { row.kind = q; quirks++; } }
    else if (!rule) { row.kind = 'quirk:U-composer-wrote-' + JSON.stringify(u) + '-for-' + JSON.stringify(exp); quirks++; }
  } else if (!row.kind.startsWith('alt:')) {
    const q = dropped(codes, r.bytes, u);
    if (q) { row.kind = q; quirks++; }
  }
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) the visible halant (B5) spelling for a virama that is not a subjoined form - Unicode fonts draw ੍ there
// and B5 reads back as ੍, while the link spelling (no glyph) reads back as two plain letters; (2) vendor keys; (3) canonical;
// (4) fewer bytes; (5) more probes.
const groups = new Map();
for (const p of pairs) { if (!groups.has(p.unicode)) groups.set(p.unicode, []); groups.get(p.unicode).push(p); }
const truth = {};
for (const [u, list] of groups) {
  const ok = list.filter(p => !p.kind.startsWith('alt:') && !p.kind.startsWith('quirk:'));
  if (!ok.length) continue;
  const score = new Map();
  for (const p of ok) {
    const s = score.get(p.bytes) || { bytes: p.bytes, vendor: 0, hal: 0, can: 0, n: 0 };
    s.n++;
    if (p.kind === 'vendor-key') s.vendor++;
    if (p.kind.startsWith('canonical:halant')) s.hal++;
    if (p.kind.startsWith('canonical')) s.can++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) =>
    (b.hal > 0) - (a.hal > 0) || b.vendor - a.vendor || (b.can > 0) - (a.can > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  truth[u] = { preferred: ranked[0].bytes, accepted: ranked.map(x => x.bytes), alternatives: [...new Set(list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes))].filter(b => !ranked.some(x => x.bytes === b)) };
}

const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1'); kinds[k] = (kinds[k] || 0) + 1; }

// every byte the 7 composer writes, against the font code sets (fonts/family-check.json)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['WPUN7001.TTF'].codeList.split(' ')), set456 = new Set(fam.faces['S09510F2.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js): (3,1) cmap of WPUN7001.TTF (SHREE-PUN7-0951), WPUN7002.TTF (SHREE-PUN7-0952), S09500F2/S09510F2/S09520F2/S09550F2/S09560F2.TTF (SHREE-PUN-0950/0951/0952/0955/0956); per code glyph id, advance, sha1 of the glyf bytes; same-design pairs 0951 and 0952 compared outline by outline; every code of every face drawn by glyph index in fonts/codes-*.png.',
  result: 'Same design (0951: WPUN7001 vs S09510F2; 0952: WPUN7002 vs S09520F2): 118 of 119 codes identical in outline bytes and advance; the only difference is 3C (₹), present in the two 7 faces only. All five 4/5/6 faces map the same 118 codes to the same glyph ids, and both 7 faces the same 119 codes. ONE table serves both families; for a 4/5/6 face ₹ (3C) has no glyph.',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b)).sort(),
  bytesInTruthNotIn456: [...used.keys()].filter(b => set7.has(b) && !set456.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, quirks, vendorKeyboards: 12, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, byKind: kinds, fontCheck,
  notProbed: 'Sequences longer than 4 codes cannot be typed with one key (a conjunct slot holds at most 4 codes), so: a two-consonant cluster with the i-sign AND a tail (ਿ C੍C ੰ), C੍C + subjoined + a vowel sign, a nukta letter (2 codes) in a cluster with a vowel sign, and three-consonant clusters other than C੍C + ੍ਰ/੍ਹ/੍ਵ/੍ਯ are not in the truth. Udaat ੑ (U+0A51), yakash ੵ (U+0A75), ੶ and the Gurmukhi abbreviation sign have no code: no key of any Shree keyboard and no code 21-FE gives them.' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Punjabi (Gurmukhi) bytes (fonts SHREE-PUN7-0951/0952 = WPUN7001/2.TTF; the 4/5/6 fonts SHREE-PUN-0950/0951/0952/0955/0956 = S09xx0F2.TTF draw the same bytes, except ₹ 3C which they lack), answered by Shree\'s own composers S8PunU64.dll (TutorGetMappedCodeW, Unicode) and S8Pun764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code. Every probe set and every vendor keyboard was captured twice in separate processes: byte-identical. '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 12 shipped .PUN keyboards loaded from its ORIGINAL path (the composers switch typing mode by name: ENG/NEWROMA/PHONETIC/ROMA are phonetic, a bindi key types tippi there); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Gurmukhi: the i-sign ਿ (code 67) struck FIRST, every other sign after the letter, bindi/tippi/visarga/addak after the sign, subjoined ੍ਰ ੍ਹ ੍ਵ ੍ਯ with their own codes D1 D2 D0 4C, nukta letters = letter + nukta D7 (the 7 composer writes one glyph: ਸ਼ 66 ਖ਼ 69 ਗ਼ 6A ਜ਼ 6B ਫ਼ 6C ਲ਼ B4); canonical:halant = a virama typed with the halant code 44 (visible B5); canonical:link / pending-link = a virama typed with the link code 64, for which the 7 composer writes NO glyph (ਕ੍ਤ = 48 57, the same bytes as ਕਤ); canonical:tippi-typed-written-as-bindi and canonical:addak+bindi-written-as-adak-bindi = the composers\' own spelling rules (accepted); canonical:ੴ = the vendor slot 31 51 (੧ + onkar arc B0). '
  + 'preferred: for a virama that is not a subjoined form, the halant (B5) spelling - a choice, since the composer writes both: Unicode fonts draw ੍ visibly there and B5 reads back as ੍, the link spelling reads back as two plain letters; accepted keeps both. For C੍ਰ C੍ਹ C੍ਵ C੍ਯ only the subjoined spelling is accepted (6E for ਕ੍ਰ, else C 6F / C AA / C AB / C 62). '
  + 'alt:* = the composer answer for another typing order or a visibly different form (ਿ struck after the letter, a sign struck before the subjoined code or a mark before the sign, C + link/halant + ਰ ਹ ਵ ਯ = two full letters instead of the subjoined form, ੴ as code 51 alone = B0 without the ੧) - kept, not used as truth. quirk:* = the Unicode composer wrote something other than the codes mean (ਡ + nukta -> ੜ, ੳ ੰ written ੳਂ with the 7 composer losing it, ਓ + ੁ -> ਉ), or the 7 composer dropped a code the Unicode composer wrote (“ ” ॥ ੴ(E0) ਇ(E4) alone: nothing; the nukta on letters with no nukta glyph; the addak when ਂ follows it after ੀ ੇ ੈ ੌ; a subjoined code / bindi / tippi struck after a late i-sign), or answered on carrier byte 07 - kept for evidence, not used. '
  + 'The two codes only the legacy composer types (U answers nothing): 41 -> 40 (an avagraha-like ऽ glyph) and EE -> A9 (a short low dash); they are not in the truth. Unicode keys are what the Unicode composer wrote (nukta letters come back precomposed U+0A33 U+0A36 U+0A59-U+0A5B U+0A5E, which NFC decomposes - a table needs both forms), except that ੲ/ੳ/ਅ + sign are filed under the independent vowel the composer writes. Rules with examples: pun7-patterns.txt.';

fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8PunU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Pun764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
