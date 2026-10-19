// Generate the Kannada probe sequences (internal Shree codes, typed order, <= 4 codes each) -> seq-all.json
// What explore1 showed (explore1-show.txt / explore1-compact.txt):
//  - a consonant code types the letter WITH its talakattu head DC (ಕ = 50 DC); a vowel sign replaces or follows it
//    (ಕಾ 50 DD, ಕಿ 51 = one glyph, ಕು 50 DC E1, ಪು B1 DC E2 = a letter-specific u-sign, ಕೆ 50 E6, ಕೈ 50 E6 E7, ಕೌ 50 E8)
//  - the length mark ೕ is its own code 76, struck AFTER ಿ ೆ ೊ (ಕ + ಿ + 76 = ಕೀ 51 E0; ಕೇ 50 E6 E0; ಕೋ 50 E6 E3 E0);
//    the precomposed codes F0-F3 (ೈ ೀ ೇ ೋ, TYPEWRIT only) are dropped by the 7 composer
//  - the reph (arkavattu) is the code 4B struck AFTER its syllable (ಕ + 4B = ರ್ಕ = 50 DC EC; ಕಾ + 4B = 50 DD EC);
//    struck first, the Unicode composer writes a full ರ (4B ಕ = "ರಕ", 7: EC 50 DC)
//  - the link 64 + a consonant = the subjoined form after the head (ಕ್ಕ = 50 DC 52); a pending link writes no virama
//    glyph (ಕ್ = 50 DC); the visible virama is the halant code 44 (ಕ್‌ = 50 85); ZWJ 5E / ZWNJ 5C are ignored by 7
//  - nukta D3 (ಕ಼ 50 DC 82); ರ + link + C = ರ with the subjoined C (C3 DC 52), the Unicode composer writes ರ್ಕ for it
const fs = require('fs');
const CONS = { 'ಕ': 0x65, 'ಖ': 0x45, 'ಗ': 0x77, 'ಘ': 0x57, 'ಙ': 0xD1, 'ಚ': 0x63, 'ಛ': 0x43, 'ಜ': 0x78, 'ಝ': 0x58, 'ಞ': 0xD2, 'ಟ': 0x72, 'ಠ': 0x52, 'ಡ': 0x74, 'ಢ': 0x54, 'ಣ': 0x46, 'ತ': 0x67, 'ಥ': 0x47, 'ದ': 0x68, 'ಧ': 0x48, 'ನ': 0x6A, 'ಪ': 0x79, 'ಫ': 0x59, 'ಬ': 0x55, 'ಭ': 0x4A, 'ಮ': 0x75, 'ಯ': 0x66, 'ರ': 0x6B, 'ಱ': 0xE3, 'ಲ': 0x69, 'ಳ': 0x49, 'ವ': 0x6F, 'ಶ': 0x4F, 'ಷ': 0x71, 'ಸ': 0x6C, 'ಹ': 0x4C, 'ೞ': 0xE5 };
const EXTRA = { 'ಕ್ಷ': 0xE1, 'ಜ್ಞ': 0xE0, 'ಶ್ರೀ': 0xD8 };
// vowel signs: one code, or sign + the length mark 76
const SIGNS = { 'ಾ': [0x61], 'ಿ': [0x73], 'ೀ': [0x73, 0x76], 'ು': [0x6E], 'ೂ': [0x26], 'ೃ': [0x7A], 'ೄ': [0xE2], 'ೆ': [0x62], 'ೇ': [0x62, 0x76], 'ೈ': [0x6D], 'ೊ': [0x70], 'ೋ': [0x70, 0x76], 'ೌ': [0x40], 'ೢ': [0xD9], 'ೣ': [0xDA] };
const SIGN1 = Object.entries(SIGNS).filter(([, v]) => v.length === 1);
const PRECOMP = { 'ೈ(F0)': 0xF0, 'ೀ(F1)': 0xF1, 'ೇ(F2)': 0xF2, 'ೋ(F3)': 0xF3 };
const TAILS = { 'ಂ': 0x3C, 'ಃ': 0x3E };
const IND = { 'ಅ': 0x51, 'ಆ': 0x41, 'ಇ': 0x53, 'ಈ': 0x56, 'ಉ': 0x4E, 'ಊ': 0xD5, 'ಋ': 0x5A, 'ೠ': 0x5F, 'ಌ': 0xE9, 'ೡ': 0xED, 'ಎ': 0x42, 'ಏ': 0x24, 'ಐ': 0x4D, 'ಒ': 0x50, 'ಓ': 0xD0, 'ಔ': 0xD6 };
const LINK = 0x64, HAL = 0x44, REPH = 0x4B, NUK = 0xD3, LEN = 0x76, SP = 0x20, ZWJ = 0x5E, ZWNJ = 0x5C;
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const bases = { ...CONS, ...EXTRA };
// 1. every base: alone, pending link (+ space), halant (+ space / + ಕ), joiners, nukta, the reph in every order,
//    every sign (+ tail, + reph before/after the tail, reph before the sign, reph first), tails, two tails, 76 alone
for (const [b, c] of Object.entries(bases)) {
  add([c], b);
  add([c, LINK], `${b} link`); add([c, LINK, SP], `${b} link space`); add([c, LINK, LINK], `${b} link link`);
  add([c, HAL], `${b} halant`); add([c, HAL, SP], `${b} halant space`); add([c, HAL, 0x65], `${b} halant ಕ`);
  add([c, LINK, ZWJ], `${b} link zwj`); add([c, LINK, ZWNJ], `${b} link zwnj`); add([c, LINK, ZWJ, 0x65], `${b} link zwj ಕ`); add([c, LINK, ZWNJ, 0x65], `${b} link zwnj ಕ`);
  add([c, REPH], `${b} reph`); add([REPH, c], `reph-first ${b}`); add([c, LINK, REPH], `${b} link reph`); add([c, HAL, REPH], `${b} halant reph`);
  add([c, LEN], `${b} 76`); add([c, LEN, REPH], `${b} 76 reph`);
  add([c, NUK], `${b} nukta`); add([c, NUK, REPH], `${b} nukta reph`); add([c, NUK, LINK], `${b} nukta link`); add([c, NUK, HAL], `${b} nukta halant`);
  for (const [s, sc] of SIGN1) add([c, NUK, ...sc], `${b} nukta ${s}`);
  for (const [s, sc] of Object.entries(SIGNS)) {
    add([c, ...sc], `${b} ${s}`);
    add([c, ...sc, REPH], `${b} ${s} reph`);
    add([c, REPH, ...sc], `${b} reph before ${s}`);
    add([REPH, c, ...sc], `reph-first ${b} ${s}`);
    if (sc.length === 2) add([c, sc[0], REPH, sc[1]], `${b} ${s} reph inside`);
    for (const [t, tc] of Object.entries(TAILS)) { add([c, ...sc, tc], `${b} ${s} ${t}`); add([c, ...sc, REPH, tc], `${b} ${s} reph ${t}`); add([c, ...sc, tc, REPH], `${b} ${s} ${t} reph`); }
    add([c, ...sc, LINK], `${b} ${s} link`); add([c, ...sc, HAL], `${b} ${s} halant`);
  }
  for (const [s, sc] of Object.entries(PRECOMP)) { add([c, sc], `${b} ${s}`); add([c, sc, REPH], `${b} ${s} reph`); }
  for (const [t, tc] of Object.entries(TAILS)) { add([c, tc], `${b} ${t}`); add([c, REPH, tc], `${b} reph ${t}`); add([c, tc, REPH], `${b} ${t} reph`); }
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c, c1, c2], `${b} ${t1} ${t2}`);
}
// 2. two-consonant clusters through the link 64: alone, with every one-code sign, every tail, the reph after,
//    a pending link after (C1್C2್), the halant after; the halant between (visible virama); ಿ ೆ ೊ struck before the
//    link for ten C1 (the only 4-code way to put a two-code sign on a cluster is impossible: C1 64 C2 73 76 = 5 codes)
const CL = Object.entries({ ...CONS, 'ಕ್ಷ': 0xE1 });
const SIGNFIRST = new Set(['ಕ', 'ಪ', 'ಶ', 'ತ', 'ರ', 'ನ', 'ಸ', 'ಗ', 'ಮ', 'ದ']);
for (const [b1, c1] of CL) for (const [b2, c2] of CL) {
  const C = [c1, LINK, c2];
  add(C, `${b1} link ${b2}`);
  for (const [s, sc] of SIGN1) add([...C, ...sc], `${b1} link ${b2} ${s}`);
  for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} link ${b2} ${t}`);
  add([...C, REPH], `${b1} link ${b2} reph`); add([...C, LINK], `${b1} link ${b2} link`); add([...C, HAL], `${b1} link ${b2} halant`); add([...C, LEN], `${b1} link ${b2} 76`);
  add([c1, HAL, c2], `${b1} halant ${b2}`);
  if (SIGNFIRST.has(b1)) for (const [s, sc] of SIGN1) add([c1, ...sc, LINK, c2], `${b1} ${s} before link ${b2}`);
}
// 3. subjoined forms alone (a link with nothing before it) and two subjoined forms: the pieces of C1್C2್C3 (5 codes)
for (const [b2, c2] of CL) { add([LINK, c2], `link ${b2}`); for (const [s, sc] of SIGN1) add([LINK, c2, ...sc], `link ${b2} ${s}`); }
const FREQ3 = ['ತ', 'ರ', 'ಯ', 'ವ', 'ಟ', 'ಕ', 'ಷ', 'ಪ', 'ಮ', 'ನ', 'ಣ', 'ದ', 'ಧ', 'ಥ', 'ಸ', 'ಚ', 'ಞ', 'ಲ'];
for (const b2 of FREQ3) for (const b3 of FREQ3) add([LINK, CONS[b2], LINK, CONS[b3]], `link ${b2} link ${b3}`);
// 4. independent vowels alone, with tails, two tails, the link, the reph, the length mark
for (const [v, c] of Object.entries(IND)) {
  add([c], v);
  for (const [t, tc] of Object.entries(TAILS)) add([c, tc], `${v} ${t}`);
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c, c1, c2], `${v} ${t1} ${t2}`);
  add([c, LINK], `${v} link`); add([c, REPH], `${v} reph`); add([c, LEN], `${v} 76`); add([c, NUK], `${v} nukta`);
}
// 5. every single code (digits, punctuation, symbols); 90-9F / B0-CF are slot references; signs alone and on carriers
for (let c = 0x20; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + c.toString(16).toUpperCase());
for (const [s, sc] of Object.entries(SIGNS)) { add([...sc], `sign ${s}`); add([0x23, ...sc], `23 ${s}`); add([SP, ...sc], `space ${s}`); for (const [t, tc] of Object.entries(TAILS)) add([...sc, tc], `sign ${s} ${t}`); }
add([LEN], '76 alone'); add([REPH], 'reph alone'); add([REPH, REPH], 'reph reph'); add([0x7E, 0x7E], '। ।'); add([0x2E, 0x2E, 0x2E], '...'); add([0x22, 0x22], '‘‘'); add([0x27, 0x27], '’’');
add([0xD0, 0x3C], 'ಓಂ'); add([0x62, 0x26], 'sign ೆ ೂ'); add([0x65, 0x62, 0x26], 'ಕ ೆ ೂ (ೊ from pieces)'); add([0x65, 0x62, 0x6D], 'ಕ ೆ ೈ');
// 6. the vendor keyboards' own conjunct-slot sequences, probed under the neutral name, alone and after ಕ / ಪ
const SH = 'C:/Shree-Lipi nxt/';
for (const f of fs.readdirSync(SH).filter(f => /\.KAN$/i.test(f)).sort()) {
  const b = fs.readFileSync(SH + f);
  for (let n = 0; n < 48; n++) {
    const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse();
    if (!codes.length) continue;
    add(codes, `vendor-slot ${f}#${n}`);
    if (codes.length <= 3) { add([0x65, ...codes], `ಕ + vendor-slot ${f}#${n}`); add([0x79, ...codes], `ಪ + vendor-slot ${f}#${n}`); }
  }
}
// 7. syllable joins: every letter after ಕಿ / ಕಾ / ಕೆ / ಕು / ಕಂ / ಕ್ಕ / ರ್ಕ / ರ್ಕಾ / ಕ್‌ / ಕೀ (word-like)
for (const [b2, c2] of Object.entries(CONS)) {
  add([0x65, 0x73, c2], `ಕಿ ${b2}`); add([0x65, 0x61, c2], `ಕಾ ${b2}`); add([0x65, 0x62, c2], `ಕೆ ${b2}`);
  add([0x65, 0x6E, c2], `ಕು ${b2}`); add([0x65, 0x3C, c2], `ಕಂ ${b2}`); add([0x65, LINK, 0x65, c2], `ಕ್ಕ ${b2}`);
  add([0x65, REPH, c2], `ರ್ಕ ${b2}`); add([0x65, 0x61, REPH, c2], `ರ್ಕಾ ${b2}`); add([0x65, HAL, c2], `ಕ್‌ ${b2}`);
  add([0x65, 0x73, LEN, c2], `ಕೀ ${b2}`); add([0x65, 0x6D, c2], `ಕೈ ${b2}`); add([0x65, 0x70, c2], `ಕೊ ${b2}`);
  add([c2, 0x61, 0x65], `${b2}ಾ ಕ`); add([c2, REPH, 0x65], `ರ್${b2} ಕ`);
}
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
