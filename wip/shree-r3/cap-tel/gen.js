// Generate the Telugu probe sequences (internal Shree codes, typed order, <= 4 codes each) -> seq-all.json
// What explore1/explore2 showed (explore1-grid.txt, explore2-show.txt):
//  - consonant codes type the letter WITH its talakattu (క = 4D E6 FC); a vowel-sign code after it replaces the
//    talakattu (కా 4D 3E, కి 4D EC FC, పి ED B3 ...); vowel-sign codes 61 73 76 6E 26 7A 5F 62 D3 6D 70 24 40 are the
//    signs, the independent vowels have their own codes (51 41 53 56 4E D5 5A E2 D9 DA 42 D4 4D 50 D0 D6)
//  - the link code 64 joins: C1 64 C2 = C1 + C2's vattu (క్క 4D E6 FC 50); a pending link (C 64, or C 64 + space)
//    writes NO virama glyph in 7; the visible virama is the halant code 44 (క్‌ = 4D 8A FC), Unicode virama + ZWNJ
//  - the ra-vattu ్ర is its own code D7 (7B; ED = 5B, E1 = 93 are other ra-vattu glyphs, E1 has no Unicode), struck
//    AFTER the consonant and its vowel sign (C + sign + D7); C + link + ర writes no ra-vattu at all in 7 (dropped)
//  - 6D (ై) and 44 (halant) stay pending at the end of a key: the answer is garbage until another code follows, so a
//    sequence ending in them gets a space (code 20) appended ("+term"); the space is stripped when the answer is read
const fs = require('fs');
const CONS = { 'క': 0x65, 'ఖ': 0x45, 'గ': 0x77, 'ఘ': 0x57, 'ఙ': 0xD1, 'చ': 0x63, 'ఛ': 0x43, 'జ': 0x78, 'ఝ': 0x58, 'ఞ': 0xD2, 'ట': 0x72, 'ఠ': 0x52, 'డ': 0x74, 'ఢ': 0x54, 'ణ': 0x46, 'త': 0x67, 'థ': 0x47, 'ద': 0x68, 'ధ': 0x48, 'న': 0x6A, 'ప': 0x79, 'ఫ': 0x59, 'బ': 0x55, 'భ': 0x4A, 'మ': 0x75, 'య': 0x66, 'ర': 0x6B, 'ఱ': 0x4B, 'ల': 0x69, 'ళ': 0x49, 'వ': 0x6F, 'శ': 0x4F, 'ష': 0x71, 'స': 0x6C, 'హ': 0x4C };
const EXTRA = { 'క్ష': 0x5E, 'శ్రీ': 0xD8 };
const SIGNS = { 'ా': 0x61, 'ి': 0x73, 'ీ': 0x76, 'ు': 0x6E, 'ూ': 0x26, 'ృ': 0x7A, 'ౄ': 0x5F, 'ె': 0x62, 'ే': 0xD3, 'ై': 0x6D, 'ొ': 0x70, 'ో': 0x24, 'ౌ': 0x40 };
const TAILS = { 'ం': 0x3C, 'ః': 0x3E, 'ఁ': 0xE0 };
const IND = { 'అ': 0x51, 'ఆ': 0x41, 'ఇ': 0x53, 'ఈ': 0x56, 'ఉ': 0x4E, 'ఊ': 0xD5, 'ఋ': 0x5A, 'ౠ': 0xE2, 'ఌ': 0xD9, 'ౡ': 0xDA, 'ఎ': 0x42, 'ఏ': 0xD4, 'ఐ': 0x4D, 'ఒ': 0x50, 'ఓ': 0xD0, 'ఔ': 0xD6 };
const LINK = 0x64, HAL = 0x44, RAV = 0xD7, RAV2 = 0xED, RAV3 = 0xE1, SP = 0x20, ZWJ = 0xF2, ZWNJ = 0xF3, AI = 0x6D;
const PENDING = new Set([AI, HAL]);
const S = [];
const seen = new Set();
const add = (codes, tag) => {
  if (codes.length && PENDING.has(codes[codes.length - 1]) && codes.length <= 3) { codes = [...codes, SP]; tag += ' +term'; }
  const k = codes.join(',');
  if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); }
};
const bases = { ...CONS, ...EXTRA };
// 1. every base: alone, pending link, link + space, halant (+ space / + క), joiners, every sign, sign + tail,
//    the ra-vattu after the sign (D7, ED, E1) and the other orders, tails, two tails
for (const [b, c] of Object.entries(bases)) {
  add([c], b);
  add([c, LINK], `${b} link`); add([c, LINK, SP], `${b} link space`);
  add([c, HAL], `${b} halant`); add([c, HAL, 0x65], `${b} halant క`);
  add([c, LINK, ZWJ], `${b} link zwj`); add([c, LINK, ZWNJ], `${b} link zwnj`);
  add([c, RAV], `${b} rav`); add([c, RAV2], `${b} rav2(ED)`); add([c, RAV3], `${b} rav3(E1)`);
  for (const [s, sc] of Object.entries(SIGNS)) {
    add([c, sc], `${b} ${s}`);
    for (const [t, tc] of Object.entries(TAILS)) { add([c, sc, tc], `${b} ${s} ${t}`); add([c, sc, RAV, tc], `${b} ${s} rav ${t}`); add([c, sc, tc, RAV], `${b} ${s} ${t} rav`); }
    add([c, sc, RAV], `${b} ${s} rav`); add([c, sc, RAV2], `${b} ${s} rav2(ED)`); add([c, sc, RAV3], `${b} ${s} rav3(E1)`);
    add([c, RAV, sc], `${b} rav before ${s}`);
  }
  for (const [t, tc] of Object.entries(TAILS)) { add([c, tc], `${b} ${t}`); add([c, RAV, tc], `${b} rav ${t}`); add([c, tc, RAV], `${b} ${t} rav`); }
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c, c1, c2], `${b} ${t1} ${t2}`);
}
// 2. two-consonant clusters through the link code 64 (C1 + C2's vattu), alone, with every sign but ై (ై stays
//    pending as the 4th code), every tail, the ra-vattu after (C1్C2్ర), the halant between (C1్‌C2, visible virama);
//    ై and the other signs struck BEFORE the link (C1 + sign + link + C2: the only 4-code way to see C1్C2ై)
const C1S = Object.entries({ ...CONS, 'క్ష': 0x5E });
const C2S = Object.entries({ ...CONS, 'క్ష': 0x5E });
const SIGNFIRST = new Set(['క', 'ప', 'శ', 'త', 'ర', 'న', 'స', 'గ', 'మ', 'ద']);
for (const [b1, c1] of C1S) for (const [b2, c2] of C2S) {
  const C = [c1, LINK, c2];
  add(C, `${b1} link ${b2}`);
  for (const [s, sc] of Object.entries(SIGNS)) if (sc !== AI) add([...C, sc], `${b1} link ${b2} ${s}`);
  for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} link ${b2} ${t}`);
  add([...C, RAV], `${b1} link ${b2} rav`);
  add([c1, HAL, c2], `${b1} halant ${b2}`);
  add([c1, AI, LINK, c2], `${b1} ై before link ${b2}`);
  if (SIGNFIRST.has(b1)) for (const [s, sc] of Object.entries(SIGNS)) add([c1, sc, LINK, c2], `${b1} ${s} before link ${b2}`);
}
// 3. independent vowels alone, with tails, two tails, the link
for (const [v, c] of Object.entries(IND)) {
  add([c], v);
  for (const [t, tc] of Object.entries(TAILS)) add([c, tc], `${v} ${t}`);
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c, c1, c2], `${v} ${t1} ${t2}`);
  add([c, LINK], `${v} link`);
}
// 4. every single code (digits, punctuation, symbols); 90-9F / B0-CF are slot references; signs alone and on carriers
for (let c = 0x20; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + c.toString(16).toUpperCase());
for (const [s, sc] of Object.entries(SIGNS)) { add([sc], `sign ${s}`); add([0x23, sc], `23 ${s}`); for (const [t, tc] of Object.entries(TAILS)) add([sc, tc], `sign ${s} ${t}`); }
add([RAV], 'rav alone'); add([0xE4, 0xE4], '। ।'); add([0x2E, 0x2E, 0x2E], '...'); add([0x22, 0x22], '‘‘'); add([0x27, 0x27], '’’');
// 5. the vendor keyboards' own conjunct-slot sequences, probed under the neutral name
const SH = 'C:/Shree-Lipi nxt/';
for (const f of fs.readdirSync(SH).filter(f => /\.TEL$/i.test(f)).sort()) {
  const b = fs.readFileSync(SH + f);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse(); if (codes.length) add(codes, `vendor-slot ${f}#${n}`); }
}
// 6. syllable joins: every letter after కి / కా / కె / కు / కం / క్క (word-like) and before ా
for (const [b2, c2] of Object.entries(CONS)) {
  add([0x65, 0x73, c2], `కి ${b2}`); add([0x65, 0x61, c2], `కా ${b2}`); add([0x65, 0x62, c2], `కె ${b2}`);
  add([0x65, 0x6E, c2], `కు ${b2}`); add([0x65, 0x3C, c2], `కం ${b2}`); add([0x65, LINK, 0x65, c2], `క్క ${b2}`);
  add([0x65, AI, c2], `కై ${b2}`); add([0x65, RAV, c2], `క rav ${b2}`); add([0x65, 0x61, RAV, c2], `కా rav ${b2}`);
  add([0x65, HAL, c2], `క halant ${b2}`);
}
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
