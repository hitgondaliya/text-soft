// What an Odia code sequence (typed order) means in Unicode logical order, and how strings are compared/filed.
//  - the pre-base e-sign େ (code 6C) struck first moves behind the first cluster; େ + ା = ୋ, େ + ୗ = ୌ, େ + ୖ = ୈ
//  - the reph ର୍ (code 40) and the half-ta ତ୍ (code 51, the Unicode composer's reading) struck after a syllable move
//    in front of that syllable
//  - codes the Unicode composer has no text for (E0, E1, 51 alone, 23, 60, 7F, 80-AF, D9-DC, EE-F3, FB-FE) are dropped
const { CODES } = require('./lib');
const LETTER = /[ଅ-ହଡ଼ଢ଼ୟ-ୡୱଵ]/u;
const CONS = /[କ-ହଡ଼ଢ଼ୟୱଵ]/u;
const UNIT = { 0x40: 'REPH', 0x51: 'HALFTA' };
// the end of the cluster that starts at i: a letter, its nukta, then any virama + letter (+ nukta)
function clusterEnd(text, i = 0) {
  if (!LETTER.test(text[i] || '')) return -1;
  i++;
  for (;;) {
    if (text[i] === '଼') { i++; continue; }
    if (text[i] === '୍' && CONS.test(text[i + 1] || '')) { i += 2; continue; }
    break;
  }
  return i;
}
// start of the syllable that ends at text position p (exclusive): back over signs, then over the cluster
function syllableStart(text, p) {
  let i = p;
  while (i > 0 && /[ଁ-ଃ଼ା-ୌ୕-ୗୢୣ‌‍]/.test(text[i - 1])) i--;
  if (i > 0 && text[i - 1] === '୍') i--; // a pending virama
  if (i > 0 && LETTER.test(text[i - 1])) {
    i--;
    while (i >= 2 && text[i - 1] === '୍' && CONS.test(text[i - 2])) i -= 2;
    if (i >= 1 && text[i - 1] === '଼') { /* nukta handled by letter test */ }
  }
  return i;
}
function expected(codes) {
  let pre = null;
  let core = codes.slice();
  if (core.length > 1 && core[0] === 0x6C) { pre = 'େ'; core = core.slice(1); }
  let text = '';
  const moves = [];
  for (const c of core) {
    if (UNIT[c]) { const s = syllableStart(text, text.length); const ins = c === 0x40 ? 'ର୍' : 'ତ୍'; text = text.slice(0, s) + ins + text.slice(s); continue; }
    const t = CODES[c];
    if (t == null || /^[\u0018]/.test(t) || c === 0xE0 || c === 0xE1) continue;
    text += t;
  }
  if (pre) {
    // after a moved reph, the e-sign goes behind the cluster that follows it
    let start = 0;
    if (text.startsWith('ର୍') && core.includes(0x40)) start = 2;
    if (text.startsWith('ତ୍') && core.includes(0x51)) start = 2;
    const k = clusterEnd(text, start);
    text = k < 0 ? pre + text : text.slice(0, k) + pre + text.slice(k);
  }
  return key(text);
}
// The key a Unicode string is filed under: e-sign + length mark / aa joined, vowel-carrier + sign joined.
const JOIN = [['ୋ', 'ୋ'], ['ୌ', 'ୌ'], ['ୈ', 'ୈ'], ['ଅା', 'ଆ']];
const key = s => { for (const [a, b] of JOIN) s = s.split(a).join(b); return s; };
// compare in NFD (ଡ଼ ଢ଼ are composition exclusions: NFD/NFC = letter + nukta) after the joins
const norm = s => key(s).normalize('NFD');
module.exports = { expected, norm, key, clusterEnd };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, expected(a.split(',').map(x => parseInt(x, 16))));
