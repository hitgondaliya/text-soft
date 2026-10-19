// Build ori7-groundtruth.json from vendor-keyboard rows and probe rows (same shape as ban7/pun7-groundtruth.json).
const fs = require('fs');
const { CODES } = require('./lib');
const { expected, norm, key } = require('./expected');
const vendor = require('./vendor-rows.json');
const probeSets = [['capp/P-results.json', 'P'], ['caph/H-results.json', 'H'], ...(fs.existsSync(__dirname + '/capw/W-results.json') ? [['capw/W-results.json', 'W']] : [])];
const probe = probeSets.flatMap(([f]) => require('./' + f));
const OUT = __dirname + '/ori7-groundtruth.json';

const parse = s => s.split(' ').map(x => parseInt(x, 16));
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const INVISIBLE = new Set([0xF9, 0xFA]); // ZWJ, ZWNJ: no glyph is right
// codes the 7 composer types NOTHING for (alone and after a letter), although the Unicode composer writes them
const NEVER7 = { 0xE4: 'ୡ (code E4)', 0xE5: 'ୱ (code E5; ୱ is typed ଓ + link + ବ = 48 B4)', 0xE6: 'nukta ଼ (code E6)', 0xE7: 'ୈ (code E7; ୈ is typed େ + C + ୖ)', 0xE8: 'ୋ (code E8; ୋ is typed େ + C + ା)', 0xE9: 'ୌ (code E9; ୌ is typed େ + C + ୗ)', 0xEA: 'ଵ (code EA)', 0xF4: 'ଓଁ (code F4)', 0xF6: 'ୢ (code F6)', 0xF7: 'ୣ (code F7)', 0xF8: 'ୄ (code F8)', 0xDD: '॥ (code DD)', 0xDE: '× (code DE)', 0xDF: '÷ (code DF)', 0x7B: '— (code 7B)', 0x7C: '“ (code 7C)', 0x7D: '– (code 7D)', 0x7E: '” (code 7E)', 0x2B: '+ (code 2B)', 0x3B: '; (code 3B)', 0x3D: '= (code 3D)', 0x5B: '[ (code 5B)', 0x5D: '] (code 5D)', 0xEB: '॑ (code EB)', 0xEC: '॓ (code EC)', 0xED: '॒ (code ED)' };
// codes only the 7 composer types (the Unicode composer writes nothing, or something unrelated, for them)
const ONLY7 = { 0xE1: 'ba-phala ୍ବ (code E1 -> FD; the Unicode composer writes nothing; Unicode C୍ବ is typed C + link + ବ = C B4)', 0xE0: 'code E0 -> E3 (a dotted letter glyph; the Unicode composer writes nothing)', 0x51: 'code 51 -> 23 (a subscript hook, the half-ta of ତ୍କ ତ୍ନ...; the Unicode composer writes ତ୍ before କ only, else nothing or ି)' };

function orderClass(tag, codes) {
  if (/after-େ/.test(tag)) return 'alt:େ struck after the letter (the Unicode composer writes it on a carrier, କ.େ; the 7 composer writes 7B AFTER the letter)';
  if (/^reph-first/.test(tag)) return 'alt:reph struck before the letter (the 7 composer writes F6 BEFORE the letter: the reph floats over the previous glyph)';
  if (/ reph before /.test(tag)) return 'alt:reph struck before the post-base sign';
  if (/ before (raph|yaph|baph)/.test(tag)) return 'alt:vowel sign struck before the phala code';
  if (codes.length === 1 && codes[0] === 0x40) return 'alt:lone reph (code 40 alone = F6, the reph mark with no letter under it; Unicode ର୍ at a word end is ର + virama = C0 FF FA)';
  const c0 = codes[0] === 0x6C ? codes.slice(1) : codes;
  if (c0[0] === 0x6A && c0[1] === 0x64 && c0.length > 2 && c0[2] !== 0xF9 && c0[2] !== 0xFA) return 'alt:ର + link + C (ର with C subjoined, e.g. ର୍କ = C0 FF 50; Unicode ର୍C is the reph, typed C + reph = C F6)';
  const iLink = codes.indexOf(0x64);
  if (iLink >= 0 && codes[iLink + 1] === 0x75) return 'alt:C + link + ଯ (U+0B2F): visible halant FA + full ଯ BE, not the ya-phala (Shree\'s ya-phala is ୍ୟ = 0B4D 0B5F, code D2 -> BF)';
  if (iLink >= 0 && codes[iLink + 1] === undefined) return 'canonical:pending-link (word-final virama: the 7 composer writes the visible virama FA)';
  if (iLink >= 0 && (codes[iLink + 1] === 0xF9 || codes[iLink + 1] === 0xFA)) return 'canonical:link+ZWJ/ZWNJ (virama FA, no glyph for the joiner)';
  if (codes.includes(0x44)) return 'canonical:halant (code 44 = virama + ZWNJ: visible virama FA, no conjunct)';
  if (codes.includes(0xD5)) return 'canonical:half (code D5 = virama + ZWJ: visible virama FA, no conjunct)';
  if (/^vendor-slot/.test(tag)) return 'canonical:vendor-slot';
  const iR = codes.indexOf(0x40), TAIL = new Set([0x69, 0xD6, 0xD4]);
  const base = iLink >= 0 ? 'canonical:link (conjunct typed with the link code 64)' : 'canonical';
  if (iR > 0 && TAIL.has(codes[iR + 1])) return base + ' reph-then-tail';
  if (iR > 0 && TAIL.has(codes[iR - 1])) return base + ' tail-then-reph';
  return base;
}

// a probe whose code the 7 composer ignored: same bytes as the probe without that code, while the Unicode composer did write it
const probeBy = new Map(probe.filter(r => r.u != null && r.bytes != null).map(r => [r.codes, r]));
function dropped(codes, bytes, u) {
  for (const c of codes) if (NEVER7[c] !== undefined) return 'quirk:7-composer-drops-' + NEVER7[c];
  for (const c of codes) if (ONLY7[c] !== undefined) return 'quirk:7-only-code: ' + ONLY7[c];
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
  const e2 = exp.split('\u0B13\u0B4D\u0B30\u0B4D\u0B2C').join('\u0B30\u0B4D\u0B13\u0B4D\u0B2C'); // reph typed after \u0B13 link \u0B2C
  if (nu === norm(e2.split('\u0B13\u0B4D\u0B2C').join('\u0B71')) && e2.includes('\u0B13\u0B4D\u0B2C'))
    return 'canonical:ୱ = ଓ + link + ବ (vendor slot 54 64 71 on DOE/INSCRIPT/ENG/PHONETIC: the Unicode composer writes ୱ U+0B71, the 7 composer ଓ + the ba-phala stroke, 48 B4)';
  return null;
}
// characters the fonts cannot draw / composer answers that draw something else
function badBytes(u, bytes) {
  const b = bytes.split(' ');
  if (b.includes('07')) return 'quirk:7-composer-carrier-07 (a phonetic keyboard answers a lone sign on byte 07, which no SHREE-ORI font has)';
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
      if (!rule) q = 'quirk:vendor-key-types-other-than-its-codes (phonetic/inscript mode): U ' + JSON.stringify(r.unicode) + ' for codes ' + JSON.stringify(exp);
      else { const q2 = dropped(codes, r.bytes, r.unicode); if (q2) q = q2; }
    }
  }
  if (!q && (r.codes === '40' || r.bytes === 'F6')) q = 'alt:lone reph key (code 40 alone = F6, the reph mark with no letter under it; Unicode ର୍ at a word end is ର + virama = C0 FF FA)';
  if (!q && r.codes && /^6A 64 [0-9A-F]{2}/.test(r.codes) && !/^6A 64 (F9|FA)/.test(r.codes)) q = 'alt:ର + link + C key (ର with C subjoined, not the reph)';
  if (q) { row.kind = q; if (q.startsWith('quirk:')) quirks++; }
  pairs.push(row);
}
for (const r of probe) {
  if (r.u == null || r.bytes == null) continue;
  const codes = parse(r.codes);
  const u = r.u;
  const exp = expected(codes);
  const set = r.file[0];
  const row = { unicode: key(u), bytes: r.bytes, source: `probe ${r.file.replace('.ORI', '')} key ${r.key}: codes ${r.codes} = ${r.tag}`, kind: orderClass(r.tag, codes) };
  const o7 = codes.find(c => ONLY7[c] !== undefined);
  if (o7 !== undefined) { row.kind = 'quirk:7-only-code: ' + ONLY7[o7]; quirks++; pairs.push(row); continue; } // the Unicode answer lacks what the bytes draw
  const bad = badBytes(u, r.bytes);
  if (/[\u0000-\u001F]/.test(u) || /[.]/.test(u) && /[\u0B00-\u0B7F]/.test(u) && !/^\.+$/.test(u) && row.kind.startsWith('canonical')) { row.kind = 'quirk:U-composer-carrier-in-answer ' + JSON.stringify(u); quirks++; }
  else if (bad) { row.kind = bad; if (bad.startsWith('quirk:')) quirks++; }
  else if (norm(u) !== norm(exp)) {
    const rule = composerRule(codes, u, exp);
    if (rule && !row.kind.startsWith('alt:')) { row.kind = rule; const q = dropped(codes, r.bytes, u); if (q) { row.kind = q; quirks++; } }
    else if (!rule && !row.kind.startsWith('alt:')) { row.kind = 'quirk:U-composer-wrote-' + JSON.stringify(u) + '-for-' + JSON.stringify(exp); quirks++; }
  } else if (!row.kind.startsWith('alt:')) {
    const q = dropped(codes, r.bytes, u);
    if (q) { row.kind = q; quirks++; }
  }
  pairs.push(row);
}

// Per Unicode string: accepted answers and one preferred answer.
// Preference: (1) vendor keys; (2) canonical; (3) fewer bytes; (4) more probes.
const groups = new Map();
for (const p of pairs) { if (!groups.has(p.unicode)) groups.set(p.unicode, []); groups.get(p.unicode).push(p); }
const truth = {};
for (const [u, list] of groups) {
  const ok = list.filter(p => !p.kind.startsWith('alt:') && !p.kind.startsWith('quirk:'));
  if (!ok.length) continue;
  const score = new Map();
  for (const p of ok) {
    const s = score.get(p.bytes) || { bytes: p.bytes, vendor: 0, can: 0, n: 0, rt: 0, reph: 0 };
    s.n++;
    if (/reph-then-tail/.test(p.kind)) s.rt++;
    if (/^ର୍/.test(u) && /= .*reph/.test(p.source) && !/(raph|yaph|baph)/.test(p.source)) s.reph++;
    if (p.kind === 'vendor-key') s.vendor++;
    if (p.kind.startsWith('canonical')) s.can++;
    score.set(p.bytes, s);
  }
  const ranked = [...score.values()].sort((a, b) => b.vendor - a.vendor || (b.rt > 0) - (a.rt > 0) || (b.reph > 0) - (a.reph > 0) || (b.can > 0) - (a.can > 0) || a.bytes.length - b.bytes.length || b.n - a.n);
  truth[u] = { preferred: ranked[0].bytes, accepted: ranked.map(x => x.bytes), alternatives: [...new Set(list.filter(p => p.kind.startsWith('alt:')).map(p => p.bytes))].filter(b => !ranked.some(x => x.bytes === b)) };
}

const kinds = {};
for (const p of pairs) { const k = p.kind.replace(/^(quirk:U-composer-wrote).*/, '$1-*').replace(/^(quirk:vendor-key-types-other-than-its-codes[^:]*).*/, '$1').replace(/^(quirk:U-composer-carrier-in-answer).*/, '$1'); kinds[k] = (kinds[k] || 0) + 1; }

// every byte the 7 composer writes, against the font code sets (fonts/family-check.json)
const fam = require('./fonts/family-check.json');
const set7 = new Set(fam.faces['WORI7001.TTF'].codeList.split(' ')), set456 = new Set(fam.faces['S06000F2.TTF'].codeList.split(' '));
const used = new Map();
for (const [u, t] of Object.entries(truth)) for (const b of t.accepted.join(' ').split(' ')) { if (!used.has(b)) used.set(b, []); if (used.get(b).length < 3) used.get(b).push(u); }
const fontCheck = {
  method: 'fonts/family-check.json (fontcmp.js): (3,1) cmap of the 7 faces WORI7001.TTF (name SHREE-ORI7-0603), WORI7002.TTF (SHREE-ORI7-0604), S0600892..S0604892.TTF (SHREE-ORI7-0600..0604) and the 4/5/6 faces S06000F2..S06040F2.TTF (SHREE-ORI-0600..0604); per code glyph id, advance, sha1 of the glyf bytes; same-design pairs compared outline by outline; every code drawn by glyph index (fonts/codes-*.png, fonts/wori7001-all.png, fonts/diff-wori-vs-456.png).',
  result: 'S06xx892 (SHREE-ORI7-06xx) and S06xx0F2 (SHREE-ORI-06xx) are identical code by code: 216 codes, same glyph ids, same outline bytes and advances (0600, 0601, 0602, 0603, 0604: 216/216 each). WORI7001/WORI7002 carry the same family names as S0603892/S0604892 (SHREE-ORI7-0603/0604) and add ₹ at 3C (217 codes, glyph ids shifted by one); against S06030F2 7 codes have touched-up outlines (62 67 97 A1 A3 BE C7) and against S06040F2 5 (82 88 89 C6 F4) - the same characters, same advances (rendered side by side). Bytes 80 81 8D 8E 9D 9E have no glyph in any face; 8F and 90 sit on U+008F/U+0090 (the C1 code points), every other byte 80-9F on its Windows-1252 code point. ONE table serves both families; only ₹ (3C) is missing from every 4/5/6 face and from S06xx892.',
  bytesInTruthNotInAnyFont: [...used.keys()].filter(b => !set7.has(b)).sort(),
  bytesInTruthNotIn456: [...used.keys()].filter(b => set7.has(b) && !set456.has(b)).sort().map(b => b + ' (' + used.get(b).join(' ') + ')'),
};

const counts = { pairs: pairs.length, unicodeStrings: groups.size, withTruth: Object.keys(truth).length, quirks, vendorKeyboards: 8, probeKeyboards: new Set(probe.map(r => r.file)).size, probeSequences: probe.length, probeSets: probeSets.map(([f, p]) => p + ' ' + f).join(', '), byKind: kinds, fontCheck,
  notProbed: 'A conjunct slot holds at most 4 codes, so these are not in the truth: C୍C + ୈ/ୋ/ୌ (େ + C + link + C + length mark = 5 codes), C୍C + reph + a sign, C୍C + sign + tail, three-consonant clusters other than C୍C + ୍ର / ୍ୟ (5 codes: C link C link C), C୍C୍ର / C୍C୍ୟ + a sign. ୱ after a virama (C୍ୱ, U+0B71) and ଵ (U+0B35), ୡ, ଓଁ, ୄ ୢ ୣ, ॥, × ÷, — – “ ”, + ; = [ ] and the Vedic marks have no 7 spelling at all: the 7 composer types nothing for their codes. The Unicode ya-phala ୍ଯ (U+0B2F) has no canonical spelling: the composer writes C + link + ଯ as visible halant + full ଯ (FA BE, kept as alt); Shree\'s own ya-phala is ୍ୟ (U+0B5F) = BF.' };

const ABOUT = 'Unicode text -> Shree-Lipi 7 Odia bytes (fonts SHREE-ORI7-0600..0604 = S06xx892.TTF and WORI7001/2.TTF; the 4/5/6 fonts SHREE-ORI-0600..0604 = S06xx0F2.TTF draw the same bytes, except ₹ 3C which only WORI7001/2 have), answered by Shree\'s own composers S8OriU64.dll (TutorGetMappedCodeW, Unicode) and S8Ori764.dll (TutorGetMappedCode, raw bytes) for the same keystroke. bytes = the 7 composer\'s raw output, one hex byte per glyph code (bytes 80-9F are raw bytes: in a document they sit on their Windows-1252 code points, except 8F and 90 which the fonts map at U+008F/U+0090). Every probe set and every vendor keyboard was captured twice in separate processes: identical (the only difference is the random unit count the 7 composer returns for "no answer"). '
  + 'kind: vendor-key = a key (layer 0, Ctrl+Space, or the composer raw code layer) of one of the 8 shipped .ORI keyboards loaded from its ORIGINAL path (the composers switch typing mode by name: ENG/PHONETIC answer a consonant with its virama form, NEWROMA/ROMA are phonetic, INSCRIPT types E7/E8/E9 as େ + length mark); source names the keyboard, key and codes. '
  + 'canonical = probe in Shree\'s typing order for Odia: the e-sign େ (code 6C) struck FIRST, ୋ = େ + C + ା, ୌ = େ + C + ୗ, ୈ = େ + C + ୖ, every other sign after the cluster, ଂ ଁ ଃ after the sign, the reph (code 40) struck AFTER the letter (and its signs), ra-phala = code D1 (F7), ya-phala = code D2 (BF, the Unicode composer writes ୍ୟ = 0B4D 0B5F), conjuncts through the link code 64 (the 7 composer writes the conjunct glyph or the subjoined form); canonical:pending-link = a word-final virama (visible FA); canonical:halant / canonical:half = codes 44 / D5 (Unicode virama + ZWNJ / ZWJ: visible FA, no conjunct); canonical:ୱ = ଓ + link + ବ (48 B4). '
  + 'preferred: vendor key, then the reph struck before ଂ ଁ ଃ (sign, reph, tail: the reph F6 sits on the letter; struck after the tail it sits over the ଂ - both accepted), then for ର୍ର the reph reading (C0 FF F6) over the ra-phala one (C0 FF F7), then canonical, then fewer bytes (ଧ୍ୟ: link + ୟ = the ligature 9A, the ya-phala key D2 = 99 BF; କ୍ଷ: the code 5C = E4, C + link + ଷ = 4C FF FA CC; both accepted), then more probes; accepted keeps every canonical spelling. '
  + 'alt:* = the composer answer for another typing order or a visibly different form (reph struck first or before the post sign, େ struck after the letter, a sign before the phala code, ର + link + C = ର with C subjoined instead of the reph, C + link + ଯ = visible halant + ଯ instead of the ya-phala) - kept, not used as truth. quirk:* = the Unicode composer wrote something other than the codes mean (it keeps only the first of two ଂ ଁ ଃ, drops a reph or ୃ or link after the ra-phala, drops the reph after ୃ, puts a nukta after େ), or the 7 composer dropped a code the Unicode composer wrote (the nukta, ୱ ଵ ୡ ଓଁ ୄ ୢ ୣ, E7/E8/E9, ॥ × ÷ — – “ ” etc.: nothing; a reph struck after ି + ଂ), or a code only the 7 composer types (E1 ba-phala FD, E0 -> E3, 51 -> 23), or a carrier byte 07 - kept for evidence, not used. '
  + 'Unicode keys are what the Unicode composer wrote (ଡ଼ ଢ଼ come back precomposed U+0B5C U+0B5D, which NFC decomposes - a table needs both forms; ଅ + ା is filed under ଆ). Rules with examples: ori7-patterns.txt.';

// 7-only evidence: probes holding a code only the 7 composer types (E1 ba-phala, 51 hook, E0), with the text the code
// stands for on the charts (E1 = ୍ବ); the Unicode composer's own answer drops it, so these rows are not truth.
const only7 = probe.filter(r => r.bytes && /(^| )(E1|51|E0)( |$)/.test(r.codes)).map(r => {
  const codes = parse(r.codes);
  let intended = null;
  if (codes.includes(0xE1) && !codes.includes(0x51) && !codes.includes(0xE0)) intended = key(expected(codes.map(c => c === 0xE1 ? 0xD1 : c)).split('୍ର').join('୍ବ')); // E1 read as ୍ବ (in the ra-phala's place)
  return { codes: r.codes, tag: r.tag, uComposer: r.u, unicodeIfE1IsBaPhala: intended, bytes: r.bytes, source: `probe ${r.file.replace('.ORI', '')} key ${r.key}` };
});
fs.writeFileSync(__dirname + '/ori7-only7-evidence.json', JSON.stringify({ about: 'Probes holding a code that only S8Ori764 types: E1 (the second ba-phala form FD; DOE Ctrl+Space i, CRTRONIC Ctrl+Space l), 51 (the hook 23; DOE =, CRTRONIC Ctrl+Space a), E0 (E3). S8OriU64 writes nothing for these codes (51 after କ reads ତ୍କ, after ଖ ଥ ଧ reads ି), so they are not in the truth; unicodeIfE1IsBaPhala is the text the charts give E1 (୍ବ), put where the ra-phala would go - an inference, not a composer answer. bytes = the 7 composer answer.', rows: only7 }, null, 1));
fs.writeFileSync(OUT, JSON.stringify({ about: ABOUT, composers: { unicode: 'C:/Shree-Lipi nxt/S8OriU64.dll TutorGetMappedCodeW', legacy: 'C:/Shree-Lipi nxt/S8Ori764.dll TutorGetMappedCode' }, counts, truth, pairs }, null, 1));
const { fontCheck: _f, ...brief } = counts;
console.log(JSON.stringify(brief, null, 1));
console.log(JSON.stringify(fontCheck, null, 1));
