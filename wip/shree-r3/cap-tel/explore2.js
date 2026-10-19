// Second exploration (prefix F): the open questions after explore1 -
//  A word-final virama / halant 44 / ZWJ ZWNJ: does a following space or letter flush a pending link?
//  B the ra-vattu: C + link + ర (the 7 composer showed no vattu), the codes D7 / ED / E1, with what follows
//  C the ai sign 6D (garbage alone) with what follows; ె + candidates for the length mark ౖ
//  D the vattu of ఝ / ఙ / ఞ / క్ష, pending conjuncts flushed by a following code
//  E carriers 23 / 20 before signs; vowel code after vowel code
const fs = require('fs');
const S = [];
const seen = new Set(JSON.parse(fs.readFileSync(__dirname + '/seq-explore1.json', 'utf8')).map(s => s.codes.join(',')));
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const K = 0x65, P = 0x79, LINK = 0x64, HAL = 0x44, SP = 0x20, RA = 0x6B;
const SIGNS = { 'ా': 0x61, 'ి': 0x73, 'ీ': 0x76, 'ు': 0x6E, 'ూ': 0x26, 'ృ': 0x7A, 'ౄ': 0x5F, 'ె': 0x62, 'ే': 0xD3, 'ై': 0x6D, 'ొ': 0x70, 'ో': 0x24, 'ౌ': 0x40 };
const TAILS = { 'ం': 0x3C, 'ః': 0x3E, 'ఁ': 0xE0 };
// A
for (const c of [K, P, RA, 0x58]) {
  add([c, LINK, SP], 'C link space'); add([c, HAL], 'C halant'); add([c, HAL, SP], 'C halant space'); add([c, HAL, K], 'C halant క');
  add([c, LINK, 0xF3], 'C link zwnj'); add([c, LINK, 0xF2], 'C link zwj'); add([c, LINK, 0xF3, K], 'C link zwnj క'); add([c, LINK, 0xF2, K], 'C link zwj క');
  add([c, LINK, K, SP], 'C link క space'); add([c, LINK, 0x2C], 'C link ,'); add([c, SP], 'C space'); add([c, LINK, LINK, K], 'C link link క');
  add([c, HAL, 0x2C], 'C halant ,'); add([c, 0xF3], 'C zwnj'); add([c, 0xF2], 'C zwj');
}
add([SP, K], 'space క'); add([SP], 'space');
// B
for (const c of [K, P, 0x4F, 0x67, 0x68, 0x6A]) {
  for (const r of [0xD7, 0xED, 0xE1]) {
    add([c, r], 'C ' + r.toString(16)); add([c, r, SP], 'C ' + r.toString(16) + ' space'); add([r, c], r.toString(16) + ' C');
    add([c, r, 0x61], 'C ' + r.toString(16) + ' ా'); add([c, r, 0x73], 'C ' + r.toString(16) + ' ి'); add([c, r, K], 'C ' + r.toString(16) + ' క');
    add([c, 0x61, r], 'C ా ' + r.toString(16)); add([c, 0x73, r], 'C ి ' + r.toString(16)); add([c, 0x6E, r], 'C ు ' + r.toString(16));
  }
  add([c, LINK, RA, SP], 'C link ర space'); add([c, LINK, RA, K], 'C link ర క');
  for (const [v, vc] of Object.entries(SIGNS)) add([c, LINK, RA, vc], 'C link ర ' + v);
  for (const [t, tc] of Object.entries(TAILS)) add([c, LINK, RA, tc], 'C link ర ' + t);
  add([c, LINK, RA, LINK], 'C link ర link'); add([c, LINK, RA, 0x66], 'C link ర య');
}
// C
for (const c of [K, P, 0x4F, 0x6B, 0x55]) {
  add([c, 0x6D, SP], 'C ై space'); add([c, 0x6D, K], 'C ై క'); add([c, 0x6D, 0x3C], 'C ై ం'); add([c, 0x6D, 0x3C, SP], 'C ై ం space');
  for (let x = 0xE5; x <= 0xEF; x++) add([c, 0x62, x], 'C ె ' + x.toString(16));
  add([c, 0x62, 0x6D], 'C ె ై'); add([c, 0x6D, 0x6D], 'C ై ై');
}
add([0x6D, SP], 'ై space'); add([0x6D, K, SP], 'ై క space'); add([0x51, 0x6D], 'అ ై'); add([0x23, 0x6D], '23 ై'); add([SP, 0x6D], 'space ై');
// D
for (const c2 of [0x58, 0xD1, 0xD2, 0x5E, 0x6B, 0x4B]) for (const c1 of [K, 0x6A, 0x67, 0x4F]) {
  add([c1, LINK, c2, SP], 'C link ' + c2.toString(16) + ' space'); add([c1, LINK, c2, 0x61], 'C link ' + c2.toString(16) + ' ా'); add([c1, LINK, c2, K], 'C link ' + c2.toString(16) + ' క');
}
// E
for (const [v, vc] of Object.entries(SIGNS)) { add([0x23, vc], '23 ' + v); add([SP, vc], 'space ' + v); add([0x51, vc], 'అ ' + v); add([vc, vc], v + ' ' + v); add([K, vc, SP], 'క ' + v + ' space'); }
for (const [t, tc] of Object.entries(TAILS)) for (const [v, vc] of Object.entries(SIGNS)) add([K, vc, tc], 'క ' + v + ' ' + t);
fs.writeFileSync(__dirname + '/seq-explore2.json', JSON.stringify(S));
console.log(S.length);
