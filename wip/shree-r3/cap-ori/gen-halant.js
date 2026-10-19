// Second probe set (prefix H): every two-consonant cluster typed with the visible halant code 44 (Unicode C୍‌C, with
// ZWNJ) and with the half code D5 (Unicode C୍‍C, with ZWJ), alone; the 44 form also with ା ି ୁ େ and ଂ; and the
// reph / ra-phala / ya-phala after a 44 cluster. Same consonant set as gen.js section 3.
const fs = require('fs');
const CONS = { 'କ': 0x73, 'ଖ': 0x53, 'ଗ': 0x78, 'ଘ': 0x58, 'ଙ': 0x5A, 'ଚ': 0x76, 'ଛ': 0x56, 'ଜ': 0x63, 'ଝ': 0x43, 'ଞ': 0xD8, 'ଟ': 0x55, 'ଠ': 0x49, 'ଡ': 0x4F, 'ଢ': 0x50, 'ଣ': 0x4A, 'ତ': 0x66, 'ଥ': 0x46, 'ଦ': 0x72, 'ଧ': 0x52, 'ନ': 0x65, 'ପ': 0x77, 'ଫ': 0x57, 'ବ': 0x71, 'ଭ': 0x7A, 'ମ': 0x61, 'ଯ': 0x75, 'ର': 0x6A, 'ଲ': 0x4D, 'ଳ': 0x6D, 'ଶ': 0x4B, 'ଷ': 0x4C, 'ସ': 0x74, 'ହ': 0x6F, 'ୟ': 0xD7, 'ଡ଼': 0x45, 'ଢ଼': 0x41 };
const HALANT = 0x44, HALF = 0xD5;
const S = [];
const seen = new Set(JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')).map(s => s.codes.join(','))); // skip what the P set has
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const cl = Object.entries(CONS);
for (const [b1, c1] of cl) for (const [b2, c2] of cl) {
  add([c1, HALANT, c2], `${b1} halant ${b2}`);
  add([c1, HALF, c2], `${b1} half ${b2}`);
  for (const [v, vc] of [['ା', 0x6B], ['ି', 0x67], ['ୁ', 0x62], ['ଂ', 0x69]]) add([c1, HALANT, c2, vc], `${b1} halant ${b2} ${v}`);
  add([0x6C, c1, HALANT, c2], `${b1} halant ${b2} େ`);
  add([c1, HALANT, c2, 0x40], `${b1} halant ${b2} reph`);
  add([c1, HALANT, c2, 0xD1], `${b1} halant ${b2} raph`);
  add([c1, HALANT, c2, 0xD2], `${b1} halant ${b2} yaph`);
}
fs.writeFileSync(__dirname + '/seq-halant.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
