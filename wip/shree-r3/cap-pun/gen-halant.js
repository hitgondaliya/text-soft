// Second probe set (prefix H): every two-consonant cluster typed with the visible HALANT code 44 instead of the link 64,
// alone and with every sign, tail, subjoined form, a pending link / halant after it; nukta letters with the halant; and
// three-letter runs C1 44 C2 44 (pending). Same shape as gen.js section 3 so the truth has a halant spelling for every
// cluster string the link set has.
const fs = require('fs');
const CONS = { 'ਕ': 0x73, 'ਖ': 0x53, 'ਗ': 0x78, 'ਘ': 0x58, 'ਙ': 0x5A, 'ਚ': 0x76, 'ਛ': 0x56, 'ਜ': 0x63, 'ਝ': 0x43, 'ਞ': 0xD8, 'ਟ': 0x55, 'ਠ': 0x49, 'ਡ': 0x4F, 'ਢ': 0x50, 'ਣ': 0x4A, 'ਤ': 0x66, 'ਥ': 0x46, 'ਦ': 0x72, 'ਧ': 0x52, 'ਨ': 0x65, 'ਪ': 0x77, 'ਫ': 0x57, 'ਬ': 0x71, 'ਭ': 0x7A, 'ਮ': 0x61, 'ਯ': 0x75, 'ਰ': 0x6A, 'ਲ': 0x6D, 'ਵ': 0x79, 'ੜ': 0x59, 'ਸ': 0x26, 'ਹ': 0x6F };
const NUK = { 'ਸ਼': [0x26, 0xD7], 'ਖ਼': [0x53, 0xD7], 'ਗ਼': [0x78, 0xD7], 'ਜ਼': [0x63, 0xD7], 'ਫ਼': [0x57, 0xD7], 'ਲ਼': [0x6D, 0xD7] };
const LINK = 0x64, HALANT = 0x44;
const SUB = { '੍ਰ': 0xD1, '੍ਹ': 0xD2, '੍ਵ': 0xD0, '੍ਯ': 0x4C };
const TAILS = { 'ਂ': 0x69, 'ੰ': 0xD6, 'ਃ': 0xD4, 'ੱ': 0x40 };
const V = { 'ਾ': { post: [0x6B] }, 'ਿ': { pre: 0x67 }, 'ੀ': { post: [0x68] }, 'ੁ': { post: [0x62] }, 'ੂ': { post: [0x6E] }, 'ੇ': { post: [0x6C] }, 'ੈ': { post: [0x70] }, 'ੋ': { post: [0x74] }, 'ੌ': { post: [0x54] } };
const S = [];
const seen = new Set(JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')).map(s => s.codes.join(','))); // skip what the P set has
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const withSign = (cl, v, after = []) => { const s = V[v]; return [...(s.pre !== undefined ? [s.pre] : []), ...cl, ...(s.post || []), ...after]; };
const cl1 = Object.entries(CONS);
for (const [b1, c1] of cl1) {
  for (const [b2, c2] of cl1) {
    const C = [c1, HALANT, c2];
    add(C, `${b1} halant ${b2}`);
    for (const v of Object.keys(V)) add(withSign(C, v), `${b1} halant ${b2} ${v}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} halant ${b2} ${t}`);
    for (const [s, sc] of Object.entries(SUB)) add([...C, sc], `${b1} halant ${b2} ${s}`);
    add([...C, LINK], `${b1} halant ${b2} link`);
    add([...C, HALANT], `${b1} halant ${b2} halant`);
  }
  for (const [n, nc] of Object.entries(NUK)) {
    add([c1, HALANT, ...nc], `${b1} halant ${n}`);
    add([...nc, HALANT, c1], `${n} halant ${b1}`);
  }
}
for (const [n, nc] of Object.entries(NUK)) { add([...nc, HALANT], `${n} halant`); for (const [s, sc] of Object.entries(SUB)) add([...nc, sc, HALANT], `${n} ${s} halant`); }
for (const [b, c] of cl1) for (const [s, sc] of Object.entries(SUB)) add([c, sc, HALANT, 0x66], `${b} ${s} halant ਤ`);
fs.writeFileSync(__dirname + '/seq-halant.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
