// What a Malayalam code sequence (typed order = VISUAL order, Shree's internal codes) means in Unicode logical order.
//  - the pre-base signs െ 73 / േ 53 (ൈ = 73 73) and the ra sign ്ര 59 are struck BEFORE their consonant cluster and
//    belong to it: 73 C = Cെ, 59 C = C്ര, 59 C1 64 C2 = C1്C2്ര; ാ 6C after 73/53 + C makes ൊ / ോ, ൗ 4F after 73 + C makes ൌ
//  - the link 64 (and E2) + a consonant joins it to the cluster (C1 + ് + C2); at the end = a pending virama ്
//  - 44 = virama + ZWNJ (halant), F1 = ZWJ, DB / F2 = ZWNJ; the ya/va signs 55 ്യ / 49 ്വ follow the cluster
//  - the precomposed signs DC ോ DD ൊ DE ൈ DF ൌ are what the Unicode composer writes for them (the 7 composer drops them)
const C = { 0x66: 'ക', 0x46: 'ഖ', 0x7A: 'ഗ', 0x5A: 'ഘ', 0x62: 'ങ', 0x76: 'ച', 0x56: 'ഛ', 0x77: 'ജ', 0x5E: 'ഝ', 0x42: 'ഞ', 0x6D: 'ട', 0x4D: 'ഠ', 0xD3: 'ഡ', 0xD4: 'ഢ', 0x6E: 'ണ', 0xD7: 'ത', 0xD8: 'ഥ', 0x70: 'ദ', 0x50: 'ധ', 0x72: 'ന', 0x65: 'പ', 0x45: 'ഫ', 0x3E: 'ബ', 0x40: 'ഭ', 0x61: 'മ', 0x75: 'യ', 0x67: 'ര', 0x79: 'റ', 0x68: 'ല', 0x78: 'ള', 0x71: 'ഴ', 0x69: 'വ', 0x43: 'ശ', 0x24: 'ഷ', 0x63: 'സ', 0x3C: 'ഹ', 0xD5: 'ക്ഷ' };
const POST = { 0x6C: 'ാ', 0x6A: 'ി', 0x4A: 'ീ', 0x6B: 'ു', 0x6F: 'ൂ', 0xD0: 'ൃ', 0xE9: 'ൄ', 0xEA: 'ൢ', 0xEB: 'ൣ', 0x4F: 'ൗ', 0xDC: 'ോ', 0xDD: 'ൊ', 0xDE: 'ൈ', 0xDF: 'ൌ' };
const TAIL = { 0x41: 'ം', 0xD6: 'ഃ' };
const CHILLU = { 0x47: 'ർ', 0x48: 'ൽ', 0x4E: 'ൺ', 0x52: 'ൻ', 0x58: 'ൾ', 0xEC: 'ൿ' };
const IND = { 0x4C: 'അ', 0xD2: 'ആ', 0x54: 'ഇ', 0x4B: 'ഉ', 0x57: 'ഋ', 0xE6: 'ൠ', 0xE4: 'ഌ', 0xE5: 'ൡ', 0x74: 'എ', 0xD1: 'ഏ', 0xF0: 'ഐ', 0x51: 'ഒ' };
const INDJOIN = { 'ഇൗ': 'ഈ', 'ഉൗ': 'ഊ', 'ഒാ': 'ഓ', 'ഒൗ': 'ഔ' };
const OTHER = { 0x20: ' ', 0x21: '!', 0x22: '‘', 0x23: '×', 0x25: '%', 0x27: '’', 0x28: '(', 0x29: ')', 0x2A: '*', 0x2B: '+', 0x2C: ',', 0x2D: '-', 0x2E: '.', 0x2F: '/', 0x3A: ':', 0x3B: ';', 0x3D: '=', 0x3F: '?', 0x5B: '[', 0x5D: ']', 0x7B: '—', 0x7D: '–', 0xE0: '×', 0xE1: '÷', 0xE3: 'ശ്രീ', 0xE7: '₹', 0xE8: 'ഽ', 0xEE: '•', 0xF4: '൳', 0xF5: '൴', 0xF6: '൵', 0xF7: '൹', 0xF8: '൰', 0xF9: '൱', 0xFA: '൲', 0xF1: '‍', 0xDB: '‌', 0xF2: '‌' };
for (let d = 0; d < 10; d++) OTHER[0x30 + d] = String.fromCharCode(0x0D66 + d);
const E = 0x73, EE = 0x53, RA = 0x59, YA = 0x55, VA = 0x49, LINK = 0x64, LINK2 = 0xE2, HAL = 0x44;
const V = '്', ZWNJ = '‌', ZWJ = '‍';
// codes with no Unicode meaning (the 7-only codes 86 -> 2D, ED -> 9E, and empty codes) are dropped
const DROP = new Set([0x86, 0xED, 0x26, 0x5C, 0x5F, 0x60, 0x7C, 0x7E, 0x7F]);

function expected(codes) {
  const out = [];
  let syl = null;
  const fresh = () => ({ pre: '', ra: false, cl: [], yv: '', signs: '', tails: '', link: false, hal: false, joiner: '' });
  const flush = () => { if (syl) out.push(render(syl)); syl = null; };
  for (let i = 0; i < codes.length; i++) {
    const c = codes[i];
    if (c === E || c === EE || c === RA) {
      if (syl && syl.cl.length && syl.link) { /* a pre sign inside a pending cluster: keep it for the next letter */ }
      if (!syl || syl.cl.length || syl.ind) { flush(); syl = fresh(); }
      if (c === RA) syl.ra = true; else syl.pre = (c === E && syl.pre === 'െ') ? 'ൈ' : syl.pre + (c === E ? 'െ' : 'േ');
      continue;
    }
    if (C[c] !== undefined) {
      if (syl && syl.cl.length && syl.link && !syl.hal) { syl.cl.push(syl.joiner + C[c]); syl.link = false; syl.joiner = ''; continue; }
      if (syl && !syl.cl.length && !syl.ind && !syl.signs && !syl.tails) { syl.cl.push(C[c]); continue; }
      flush(); syl = fresh(); syl.cl.push(C[c]); continue;
    }
    if (IND[c] !== undefined) {
      if (syl && syl.pre === 'െ' && !syl.cl.length && c === 0x74) { syl.cl.push('ഐ'); syl.pre = ''; syl.ind = true; continue; }
      flush(); syl = fresh(); syl.cl.push(IND[c]); syl.ind = true; continue;
    }
    if (POST[c] !== undefined) {
      if (!syl) syl = fresh();
      if (syl.ind && syl.cl.length === 1 && INDJOIN[syl.cl[0] + POST[c]] && !syl.signs) { syl.cl[0] = INDJOIN[syl.cl[0] + POST[c]]; continue; }
      if (c === 0x6C && (syl.pre === 'െ' || syl.pre === 'േ') && !syl.signs) { syl.signs = syl.pre === 'െ' ? 'ൊ' : 'ോ'; syl.pre = ''; continue; }
      if (c === 0x4F && syl.pre === 'െ' && !syl.signs) { syl.signs = 'ൌ'; syl.pre = ''; continue; }
      syl.signs += POST[c]; continue;
    }
    if (c === YA || c === VA) { if (!syl) syl = fresh(); syl.yv += V + (c === YA ? 'യ' : 'വ'); continue; }
    if (TAIL[c] !== undefined) { if (!syl) { out.push(TAIL[c]); continue; } syl.tails += TAIL[c]; continue; }
    if (c === LINK || c === LINK2) { if (!syl) { out.push(V); continue; } if (syl.link) continue; syl.link = true; continue; }
    if (c === HAL) { if (!syl) { out.push(V + ZWNJ); continue; } syl.hal = true; syl.link = false; continue; }
    if (CHILLU[c] !== undefined) { flush(); out.push(CHILLU[c]); continue; }
    if (DROP.has(c)) continue;
    if (OTHER[c] !== undefined) {
      if ((c === 0xF1 || c === 0xDB || c === 0xF2) && syl && syl.link) { syl.joiner += OTHER[c]; continue; }
      flush(); out.push(OTHER[c]); continue;
    }
  }
  flush();
  return key(out.join(''));
}
function render(s) {
  let t = s.cl.join(V) + (s.ra ? V + 'ര' : '') + s.yv;
  const tailVirama = (s.link || s.hal) ? V + (s.joiner || '') + (s.hal ? ZWNJ : '') : '';
  // a pre-base sign that never met its letter (or an independent vowel) is written as it is
  return t + s.pre + s.signs + (s.signs || s.pre ? s.tails + tailVirama : tailVirama + s.tails);
}
// The key a Unicode string is filed under: NFC (െ+ാ = ൊ, േ+ാ = ോ, െ+ൗ = ൌ)
const key = s => s == null ? s : s.normalize('NFC');
const norm = s => key(s);
module.exports = { expected, norm, key, C, POST, TAIL, IND, OTHER, DROP, CHILLU };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, JSON.stringify(expected(a.split(',').map(x => parseInt(x, 16)))));
