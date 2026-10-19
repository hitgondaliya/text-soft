// What a Pun code sequence (typed order) means in Unicode logical order, and how strings are compared/filed.
const { CODES } = require('./lib');
const PRE = { 0x67: 'ਿ' };
// independent vowels a typist builds from a vowel carrier + sign (the U composer writes the precomposed letter;
// it does NOT join ੳ + ੋ into ਓ)
const JOIN = [['ਅਾ', 'ਆ'], ['ਅੈ', 'ਐ'], ['ਅੌ', 'ਔ'], ['ੲਿ', 'ਇ'], ['ੲੀ', 'ਈ'], ['ੲੇ', 'ਏ'], ['ੳੁ', 'ਉ'], ['ੳੂ', 'ਊ']];
const LETTER = /[ਅ-ਹਖ਼-ਫ਼ੲੳ]/u;

// the end of the first cluster of text: a letter, its nukta, then any virama + letter (+ nukta)
function clusterEnd(text) {
  let i = 0;
  if (!LETTER.test(text[0] || '')) return -1;
  i = 1;
  for (;;) {
    if (text[i] === '਼') { i++; continue; }
    if (text[i] === '੍' && LETTER.test(text[i + 1] || '')) { i += 2; continue; }
    break;
  }
  return i;
}

function expected(codes) {
  let pre = null;
  let core = codes.slice();
  if (core.length > 1 && PRE[core[0]] !== undefined) { pre = PRE[core[0]]; core = core.slice(1); }
  let text = core.map(c => CODES[c] ?? '?').join('');
  if (pre) {
    const k = clusterEnd(text);
    text = k < 0 ? pre + text : text.slice(0, k) + pre + text.slice(k);
  }
  return key(text);
}
// The key a Unicode string is filed under: vowel-carrier + sign joined into the independent vowel, nothing else changed.
const key = s => { for (const [a, b] of JOIN) s = s.split(a).join(b); return s; };
// compare in NFD (ਸ਼ ਖ਼ ਗ਼ ਜ਼ ਫ਼ ਲ਼ are composition exclusions: NFD/NFC = letter + nukta) after the joins
const norm = s => key(s).normalize('NFD');
module.exports = { expected, norm, key, clusterEnd };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, expected(a.split(',').map(x => parseInt(x, 16))));
