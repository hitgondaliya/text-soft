// Generate the Punjabi (Gurmukhi) probe sequences (internal Shree codes, typed order, <= 4 codes each).
// Shree's typing order for Gurmukhi (the ITR / typewriter keyboards): the i-sign ਿ (code 67) is struck FIRST, before
// the consonant cluster; every other sign after the cluster; bindi/tippi/visarga/addak after the sign; the subjoined
// (pairin) letters ੍ਰ ੍ਹ ੍ਵ ੍ਯ have codes of their own (D1 D2 D0 4C); the link code 64 joins two letters with an
// invisible virama, 44 is the visible halant; nukta letters are letter + nukta (D7).
const fs = require('fs');
const CONS = { 'ਕ': 0x73, 'ਖ': 0x53, 'ਗ': 0x78, 'ਘ': 0x58, 'ਙ': 0x5A, 'ਚ': 0x76, 'ਛ': 0x56, 'ਜ': 0x63, 'ਝ': 0x43, 'ਞ': 0xD8, 'ਟ': 0x55, 'ਠ': 0x49, 'ਡ': 0x4F, 'ਢ': 0x50, 'ਣ': 0x4A, 'ਤ': 0x66, 'ਥ': 0x46, 'ਦ': 0x72, 'ਧ': 0x52, 'ਨ': 0x65, 'ਪ': 0x77, 'ਫ': 0x57, 'ਬ': 0x71, 'ਭ': 0x7A, 'ਮ': 0x61, 'ਯ': 0x75, 'ਰ': 0x6A, 'ਲ': 0x6D, 'ਵ': 0x79, 'ੜ': 0x59, 'ਸ': 0x26, 'ਹ': 0x6F };
const NUK = { 'ਸ਼': [0x26, 0xD7], 'ਖ਼': [0x53, 0xD7], 'ਗ਼': [0x78, 0xD7], 'ਜ਼': [0x63, 0xD7], 'ਫ਼': [0x57, 0xD7], 'ਲ਼': [0x6D, 0xD7] };
const LINK = 0x64, HALANT = 0x44, NUKTA = 0xD7, ZWJ = 0xE2, ZWNJ = 0xE3;
const SUB = { '੍ਰ': 0xD1, '੍ਹ': 0xD2, '੍ਵ': 0xD0, '੍ਯ': 0x4C };
const TAILS = { 'ਂ': 0x69, 'ੰ': 0xD6, 'ਃ': 0xD4, 'ੱ': 0x40 };
// vowel signs: pre = code struck before the cluster, post = codes struck after it
const V = { 'ਾ': { post: [0x6B] }, 'ਿ': { pre: 0x67 }, 'ੀ': { post: [0x68] }, 'ੁ': { post: [0x62] }, 'ੂ': { post: [0x6E] }, 'ੇ': { post: [0x6C] }, 'ੈ': { post: [0x70] }, 'ੋ': { post: [0x74] }, 'ੌ': { post: [0x54] } };
// the two codes only the legacy composer types (U answers nothing): glyphs 40 and A9 - probed as evidence
const LEGACY_ONLY = { 'L41': 0x41, 'LEE': 0xEE };
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const withSign = (cl, v, after = []) => { const s = V[v]; return [...(s.pre !== undefined ? [s.pre] : []), ...cl, ...(s.post || []), ...after]; };
const bases = { ...Object.fromEntries(Object.entries(CONS).map(([k, c]) => [k, [c]])), ...NUK };

// 1. every base: alone, pending link, halant, link+ZWJ/ZWNJ, nukta, every subjoined form, every sign, sign + tail,
//    tails alone, two tails, the i-sign struck after the letter (the other order, kept as alt), the legacy-only codes
for (const [b, cl] of Object.entries(bases)) {
  add(cl, b);
  add([...cl, LINK], `${b} link`);
  add([...cl, HALANT], `${b} halant`);
  add([...cl, LINK, ZWJ], `${b} link zwj`);
  add([...cl, LINK, ZWNJ], `${b} link zwnj`);
  add([...cl, HALANT, ZWNJ], `${b} halant zwnj`);
  if (cl.length === 1) add([...cl, NUKTA], `${b} nukta`);
  for (const [s, sc] of Object.entries(SUB)) add([...cl, sc], `${b} ${s}`);
  for (const [t, tc] of Object.entries(TAILS)) add([...cl, tc], `${b} ${t}`);
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([...cl, c1, c2], `${b} ${t1} ${t2}`);
  for (const v of Object.keys(V)) {
    add(withSign(cl, v), `${b} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add(withSign(cl, v, [tc]), `${b} ${v} ${t}`);
    for (const [l, lc] of Object.entries(LEGACY_ONLY)) add(withSign(cl, v, [lc]), `${b} ${v} ${l}`);
    // addak struck before the post sign (other order)
    if (V[v].post) add([...cl, 0x40, ...V[v].post], `${b} ੱ before ${v}`);
    // tail struck before the post sign (other order)
    if (V[v].post) add([...cl, 0x69, ...V[v].post], `${b} ਂ before ${v}`);
    if (V[v].post) add([...cl, 0xD6, ...V[v].post], `${b} ੰ before ${v}`);
  }
  add([...cl, 0x67], `${b} after-ਿ`);
  add([...cl, 0x67, 0x69], `${b} after-ਿ ਂ`);
  add([...cl, 0x67, 0xD6], `${b} after-ਿ ੰ`);
  for (const [l, lc] of Object.entries(LEGACY_ONLY)) add([...cl, lc], `${b} ${l}`);
}
// 2. the subjoined (pairin) forms with every sign and tail, and the other typing orders
for (const [b, cl] of Object.entries(bases)) {
  for (const [s, sc] of Object.entries(SUB)) {
    const C = [...cl, sc];
    for (const v of Object.keys(V)) add(withSign(C, v), `${b} ${s} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b} ${s} ${t}`);
    for (const v of Object.keys(V)) for (const [t, tc] of Object.entries(TAILS)) add(withSign(C, v, [tc]), `${b} ${s} ${v} ${t}`);
    add([...C, LINK], `${b} ${s} link`);
    add([...C, HALANT], `${b} ${s} halant`);
    // sign struck between the letter and the subjoined code (other order)
    for (const v of Object.keys(V)) if (V[v].post) add([...cl, ...V[v].post, sc], `${b} ${v} before ${s}`);
    add([...cl, 0x67, sc], `${b} after-ਿ before ${s}`);
    add([...C, 0x67], `${b} ${s} after-ਿ`);
    for (const [s2, sc2] of Object.entries(SUB)) if (sc2 !== sc) add([...C, sc2], `${b} ${s} ${s2}`);
  }
}
// 3. two-consonant clusters through the Link code, alone and with every sign, tail, subjoined form, pending link, halant;
//    through the visible halant; and with the nukta letters
const cl1 = Object.entries(CONS);
for (const [b1, c1] of cl1) {
  for (const [b2, c2] of cl1) {
    const C = [c1, LINK, c2];
    add(C, `${b1} link ${b2}`);
    for (const v of Object.keys(V)) add(withSign(C, v), `${b1} link ${b2} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} link ${b2} ${t}`);
    for (const [s, sc] of Object.entries(SUB)) add([...C, sc], `${b1} link ${b2} ${s}`);
    add([...C, LINK], `${b1} link ${b2} link`);
    add([...C, HALANT], `${b1} link ${b2} halant`);
    add([c1, HALANT, c2], `${b1} halant ${b2}`);
    add([c1, 0x40, c2], `${b1} ੱ ${b2}`);
  }
  for (const [n, nc] of Object.entries(NUK)) {
    add([c1, LINK, ...nc], `${b1} link ${n}`);
    add([...nc, LINK, c1], `${n} link ${b1}`);
  }
}
// 4. independent vowels and the vowel carriers with signs and tails
const IND = { 'ਅ': [0x24], 'ਆ': [0x24, 0x6B], 'ਇ': [0x67, 0x47], 'ਈ': [0x47, 0x68], 'ਉ': [0x42, 0x62], 'ਊ': [0x42, 0x6E], 'ਏ': [0x47, 0x6C], 'ਐ': [0x24, 0x70], 'ਓ': [0x4E], 'ਔ': [0x24, 0x54], 'ੲ': [0x47], 'ੳ': [0x42], 'ਇ(E4)': [0xE4] };
for (const [b, c] of Object.entries(IND)) {
  add(c, b);
  for (const [t, tc] of Object.entries(TAILS)) add([...c, tc], `${b} ${t}`);
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([...c, c1, c2], `${b} ${t1} ${t2}`);
  add([...c, NUKTA], `${b} nukta`);
  for (const [l, lc] of Object.entries(LEGACY_ONLY)) add([...c, lc], `${b} ${l}`);
}
for (const carrier of [[0x24], [0x47], [0x42], [0x4E]]) for (const v of Object.keys(V)) add(withSign(carrier, v), `carrier ${carrier[0].toString(16)} ${v}`);
add([0x42, 0x74], 'ੳ ੋ'); add([0x24, 0x6C], 'ਅ ੇ'); add([0x24, 0x68], 'ਅ ੀ'); add([0x24, 0x62], 'ਅ ੁ'); add([0x24, 0x67], 'ਅ after-ਿ');
// 5. every single code (digits, punctuation, symbols, ੴ, ₹, the signs alone); 90-9F / B0-CF are slot references
for (let c = 0x21; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + c.toString(16).toUpperCase());
for (const v of Object.keys(V)) add(withSign([], v), `sign ${v}`);
for (const v of Object.keys(V)) for (const [t, tc] of Object.entries(TAILS)) add(withSign([], v, [tc]), `sign ${v} ${t}`);
for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c1, c2], `sign ${t1} ${t2}`);
for (const [s, sc] of Object.entries(SUB)) for (const v of Object.keys(V)) add(withSign([sc], v), `sign ${s} ${v}`);
add([0xD3, 0xD3], '। ।'); add([0x31, 0x51], '੧ ੴ'); add([0x31, 0xE0], '੧ ੴ(E0)'); add([0x31, 0x42, 0x4D], '੧ ੳ 4D');
add([0x2E, 0x2E, 0x2E], '...'); add([0x22, 0x22], '‘‘'); add([0x27, 0x27], '’’');
// 6. the vendor keyboards' own conjunct-slot sequences (52 unique), probed under the neutral name
const path = require('path');
const SH = 'C:/Shree-Lipi nxt/';
for (const f of fs.readdirSync(SH).filter(f => /\.PUN$/i.test(f)).sort()) {
  const b = fs.readFileSync(SH + f);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse(); if (codes.length) add(codes, `vendor-slot ${f}#${n}`); }
}
// 7. syllable joins: the i-sign and addak across a following letter (word-like), every letter after ਕਿ / ਕੱ / ਪੱ
for (const [b2, c2] of cl1) { add([0x67, 0x73, c2], `ਕਿ ${b2}`); add([0x67, 0x73, 0x40, c2], `ਕਿੱ ${b2}`); add([0x77, 0x40, c2, 0x6B], `ਪੱ ${b2} ਾ`); add([0x73, 0x68, c2], `ਕੀ ${b2}`); }
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
