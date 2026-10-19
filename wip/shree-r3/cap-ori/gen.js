// Generate the Odia probe sequences (internal Shree codes, typed order, <= 4 codes each).
// Shree's typing order for Odia (TYPEWRIT and the other non-phonetic keyboards): the pre-base e-sign େ (code 6C) is
// struck FIRST, before the consonant cluster; ୋ = େ + C + ା (6C .. 6B), ୌ = େ + C + ୗ (6C .. 79), ୈ = େ + C + ୖ
// (6C .. 70); every other sign after the cluster; ଂ ଁ ଃ after the sign; the reph (code 40) is struck AFTER the base
// (and its signs); ra-phala D1, ya-phala D2 (the Unicode composer writes ୍ୟ = 0B4D 0B5F), ba-phala E1 (7 only);
// the link code 64 = virama between letters, 44 = virama + ZWNJ (visible halant), D5 = virama + ZWJ (half form).
// E7/E8/E9 = ୈ ୋ ୌ as one code (the 7 composer writes nothing for them), E6 = nukta (7 writes nothing).
const fs = require('fs');
const CONS = { 'କ': 0x73, 'ଖ': 0x53, 'ଗ': 0x78, 'ଘ': 0x58, 'ଙ': 0x5A, 'ଚ': 0x76, 'ଛ': 0x56, 'ଜ': 0x63, 'ଝ': 0x43, 'ଞ': 0xD8, 'ଟ': 0x55, 'ଠ': 0x49, 'ଡ': 0x4F, 'ଢ': 0x50, 'ଣ': 0x4A, 'ତ': 0x66, 'ଥ': 0x46, 'ଦ': 0x72, 'ଧ': 0x52, 'ନ': 0x65, 'ପ': 0x77, 'ଫ': 0x57, 'ବ': 0x71, 'ଭ': 0x7A, 'ମ': 0x61, 'ଯ': 0x75, 'ର': 0x6A, 'ଲ': 0x4D, 'ଳ': 0x6D, 'ଶ': 0x4B, 'ଷ': 0x4C, 'ସ': 0x74, 'ହ': 0x6F };
// letters with a code of their own: nukta letters ଡ଼ ଢ଼, ୟ, ୱ (7 writes nothing), ଵ (7 writes nothing), and କ୍ଷ
const EXTRA = { 'ଡ଼': 0x45, 'ଢ଼': 0x41, 'ୟ': 0xD7, 'ୱ': 0xE5, 'ଵ': 0xEA, 'କ୍ଷ': 0x5C };
const LINK = 0x64, HALANT = 0x44, HALF = 0xD5, RAPH = 0xD1, YAPH = 0xD2, BAPH = 0xE1, REPH = 0x40, NUKTA = 0xE6, ZWJ = 0xF9, ZWNJ = 0xFA, HALFTA = 0x51, CODE_E0 = 0xE0;
const ANUS = 0x69, CANDRA = 0xD6, VISARGA = 0xD4;
const TAILS = { 'ଂ': ANUS, 'ଁ': CANDRA, 'ଃ': VISARGA };
// vowel signs: pre = code struck before the cluster, post = codes struck after it
const V = {
  'ା': { post: [0x6B] }, 'ି': { post: [0x67] }, 'ୀ': { post: [0x68] }, 'ୁ': { post: [0x62] }, 'ୂ': { post: [0x6E] },
  'ୃ': { post: [0xD0] }, 'େ': { pre: 0x6C }, 'ୈ': { pre: 0x6C, post: [0x70] }, 'ୋ': { pre: 0x6C, post: [0x6B] },
  'ୌ': { pre: 0x6C, post: [0x79] }, 'ୖ': { post: [0x70] }, 'ୗ': { post: [0x79] },
  'ୄ(F8)': { post: [0xF8] }, 'ୢ(F6)': { post: [0xF6] }, 'ୣ(F7)': { post: [0xF7] },
  'ୈ(E7)': { post: [0xE7] }, 'ୋ(E8)': { post: [0xE8] }, 'ୌ(E9)': { post: [0xE9] },
};
const MAIN = ['ା', 'ି', 'ୀ', 'ୁ', 'ୂ', 'ୃ', 'େ', 'ୈ', 'ୋ', 'ୌ'];
const ONEPOST = ['ା', 'ି', 'ୀ', 'ୁ', 'ୂ', 'ୃ', 'ୖ', 'ୗ']; // one code after the cluster
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const withSign = (cl, v, after = []) => { const s = V[v]; return [...(s.pre !== undefined ? [s.pre] : []), ...cl, ...(s.post || []), ...after]; };
const bases = { ...CONS, ...EXTRA };

// 1. every base: alone, pending link, halant, half, link+ZWJ/ZWNJ, nukta, the phala forms, every sign, sign + tail,
//    sign + reph, reph before the post sign (other order), tails, two tails, the pre-base e struck after the letter (alt)
for (const [b, c] of Object.entries(bases)) {
  add([c], b);
  add([c, LINK], `${b} link`);
  add([c, HALANT], `${b} halant`);
  add([c, HALF], `${b} half`);
  add([c, LINK, ZWJ], `${b} link zwj`);
  add([c, LINK, ZWNJ], `${b} link zwnj`);
  add([c, NUKTA], `${b} nukta`);
  add([c, RAPH], `${b} raph`); add([c, YAPH], `${b} yaph`); add([c, BAPH], `${b} baph`);
  add([c, HALFTA], `${b} code51`); add([c, CODE_E0], `${b} codeE0`);
  add([c, RAPH, YAPH], `${b} raph yaph`); add([c, YAPH, RAPH], `${b} yaph raph`);
  add([c, REPH], `${b} reph`);
  add([REPH, c], `reph-first ${b}`);
  add([0x6A, LINK, c], `ra link ${b}`);
  add([0x6A, HALANT, c], `ra halant ${b}`);
  add([0x6A, HALF, c], `ra half ${b}`);
  for (const [t, tc] of Object.entries(TAILS)) { add([c, tc], `${b} ${t}`); add([c, tc, REPH], `${b} ${t} reph`); add([c, REPH, tc], `${b} reph ${t}`); }
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c, c1, c2], `${b} ${t1} ${t2}`);
  for (const v of Object.keys(V)) {
    add(withSign([c], v), `${b} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add(withSign([c], v, [tc]), `${b} ${v} ${t}`);
    add(withSign([c], v, [REPH]), `${b} ${v} reph`);
    const s = V[v];
    if (s.post) add([...(s.pre !== undefined ? [s.pre] : []), c, REPH, ...s.post], `${b} reph before ${v}`);
    if (s.pre !== undefined && !s.post) add([REPH, s.pre, c], `reph-first ${v} ${b}`);
  }
  for (const v of MAIN) { add(withSign([c], v, [ANUS, REPH]), `${b} ${v} ଂ reph`); add(withSign([c], v, [REPH, ANUS]), `${b} ${v} reph ଂ`); add(withSign([c], v, [CANDRA, REPH]), `${b} ${v} ଁ reph`); add(withSign([c], v, [REPH, CANDRA]), `${b} ${v} reph ଁ`); }
  add([c, 0x6C], `${b} after-େ`);
  add([c, 0x6C, 0x6B], `${b} after-େ ା`);
  for (const v of ONEPOST) add([c, NUKTA, ...V[v].post], `${b} nukta ${v}`);
  add([0x6C, c, NUKTA], `${b} nukta େ`);
  add([c, NUKTA, LINK], `${b} nukta link`);
}
// 2. phala forms (ra / ya / ba) with every sign, tail and reph
for (const [b, c] of Object.entries(bases)) {
  for (const [ph, pc] of [['raph', RAPH], ['yaph', YAPH], ['baph', BAPH]]) {
    const C = [c, pc];
    for (const v of Object.keys(V)) add(withSign(C, v), `${b} ${ph} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b} ${ph} ${t}`);
    for (const v of ONEPOST) for (const [t, tc] of Object.entries(TAILS)) add(withSign(C, v, [tc]), `${b} ${ph} ${v} ${t}`);
    add(withSign(C, 'େ', [ANUS]), `${b} ${ph} େ ଂ`);
    add([...C, LINK], `${b} ${ph} link`);
    add([...C, HALANT], `${b} ${ph} halant`);
    add([...C, REPH], `${b} ${ph} reph`);
    for (const v of ONEPOST) add(withSign(C, v, [REPH]), `${b} ${ph} ${v} reph`);
    add(withSign(C, 'େ', [REPH]), `${b} ${ph} େ reph`);
    // the sign struck before the phala code (other order)
    for (const v of ONEPOST) add([c, ...V[v].post, pc], `${b} ${v} before ${ph}`);
  }
}
// 3. two-consonant clusters through the link code 64, alone and with every one-code sign, େ, tails, phala, reph,
//    pending link, halant; and with the extra letters
const cl = Object.entries({ ...CONS, 'ୟ': 0xD7, 'ଡ଼': 0x45, 'ଢ଼': 0x41, 'ୱ': 0xE5, 'ଵ': 0xEA });
for (const [b1, c1] of cl) {
  for (const [b2, c2] of cl) {
    const C = [c1, LINK, c2];
    add(C, `${b1} link ${b2}`);
    for (const v of [...ONEPOST, 'େ']) add(withSign(C, v), `${b1} link ${b2} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} link ${b2} ${t}`);
    add([...C, RAPH], `${b1} link ${b2} raph`);
    add([...C, YAPH], `${b1} link ${b2} yaph`);
    add([...C, LINK], `${b1} link ${b2} link`);
    add([...C, HALANT], `${b1} link ${b2} halant`);
    add([...C, REPH], `${b1} link ${b2} reph`);
  }
  const k = 0x5C;
  add([c1, LINK, k], `${b1} link କ୍ଷ`); add([k, LINK, c1], `କ୍ଷ link ${b1}`);
  for (const v of [...ONEPOST, 'େ']) { add(withSign([c1, LINK, k], v), `${b1} link କ୍ଷ ${v}`); add(withSign([k, LINK, c1], v), `କ୍ଷ link ${b1} ${v}`); }
  add([c1, LINK, k, REPH], `${b1} link କ୍ଷ reph`); add([k, LINK, c1, REPH], `କ୍ଷ link ${b1} reph`);
  add([c1, LINK, k, ANUS], `${b1} link କ୍ଷ ଂ`); add([c1, LINK, k, YAPH], `${b1} link କ୍ଷ yaph`);
}
// 4. independent vowels with tails, reph, nukta
const IND = { 'ଅ': [0x24], 'ଆ': [0x24, 0x6B], 'ଇ': [0x47], 'ଈ': [0x48], 'ଉ': [0x42], 'ଊ': [0x4E], 'ଋ': [0x5E], 'ୠ': [0xE2], 'ଌ': [0xE3], 'ୡ': [0xE4], 'ଏ': [0x3C], 'ଐ': [0x3E], 'ଓ': [0x54], 'ଔ': [0x59], 'ଓଁ(F4)': [0xF4] };
for (const [b, c] of Object.entries(IND)) {
  add(c, b);
  for (const [t, tc] of Object.entries(TAILS)) { add([...c, tc], `${b} ${t}`); add([...c, tc, REPH], `${b} ${t} reph`); add([...c, REPH, tc], `${b} reph ${t}`); }
  for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([...c, c1, c2], `${b} ${t1} ${t2}`);
  add([...c, REPH], `${b} reph`);
  add([...c, NUKTA], `${b} nukta`);
  add([...c, LINK], `${b} link`);
}
for (const v of Object.keys(V)) add(withSign([0x24], v), `ଅ ${v}`);
add([0x54, LINK, 0x71], 'ଓ link ବ (vendor ୱ)'); add([0x54, LINK, 0x77], 'ଓ link ପ'); add([0x54, YAPH], 'ଓ yaph'); add([0x54, LINK, YAPH], 'ଓ link yaph');
for (const v of ONEPOST) add([0x54, LINK, 0x71, ...V[v].post], `ଓ link ବ ${v}`);
add([0x6C, 0x54, LINK, 0x71], 'େ ଓ link ବ');
for (const [t, tc] of Object.entries(TAILS)) add([0x54, LINK, 0x71, tc], `ଓ link ବ ${t}`);
add([0x54, LINK, 0x71, REPH], 'ଓ link ବ reph');
// 5. every single code (digits, punctuation, symbols); 90-9F / B0-CF are slot references
for (let c = 0x21; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + c.toString(16).toUpperCase());
for (const v of Object.keys(V)) add(withSign([], v), `sign ${v}`);
for (const v of Object.keys(V)) for (const [t, tc] of Object.entries(TAILS)) add(withSign([], v, [tc]), `sign ${v} ${t}`);
for (const [t1, c1] of Object.entries(TAILS)) for (const [t2, c2] of Object.entries(TAILS)) if (c1 !== c2) add([c1, c2], `sign ${t1} ${t2}`);
add([REPH, ANUS], 'sign reph ଂ'); add([RAPH, 0x6B], 'sign raph ା'); add([YAPH, 0x6B], 'sign yaph ା');
add([0xD3, 0xD3], '। ।'); add([0x2E, 0x2E, 0x2E], '...'); add([0x22, 0x22], '‘‘'); add([0x27, 0x27], '’’');
add([0x66, LINK, ZWJ], 'ତ link zwj'); add([0x66, HALANT, ZWNJ], 'ତ halant zwnj');
// 6. the vendor keyboards' own conjunct-slot sequences, probed under the neutral name
const SH = 'C:/Shree-Lipi nxt/';
for (const f of fs.readdirSync(SH).filter(f => /\.ORI$/i.test(f)).sort()) {
  const b = fs.readFileSync(SH + f);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse(); if (codes.length) add(codes, `vendor-slot ${f}#${n}`); }
}
// 7. syllable joins: every letter after କି / କା / କେ-cluster / reph-syllable (word-like), and before ା
for (const [b2, c2] of Object.entries(CONS)) {
  add([0x73, 0x67, c2], `କି ${b2}`); add([0x73, 0x6B, c2], `କା ${b2}`); add([0x6C, 0x73, c2], `କେ ${b2}`);
  add([0x73, REPH, c2], `କ reph ${b2}`); add([0x73, 0x62, c2], `କୁ ${b2}`); add([0x73, ANUS, c2], `କଂ ${b2}`);
  add([0x73, 0x6C, c2], `କ after-େ ${b2}`);
  add([0x73, LINK, c2, 0x6B], `କ link ${b2} ା`);
}
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
