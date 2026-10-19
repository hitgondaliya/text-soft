// What a Kannada code sequence (typed order, Shree's internal codes) means in Unicode logical order.
//  - a consonant starts a syllable; the link 64 + a consonant joins it to the cluster (C1 + ್ + C2); a vowel sign or
//    a tail struck BEFORE the link (C1 + ಿ + 64 + C2) still belongs after the whole cluster (the composer moves it)
//  - the reph 4B is struck AFTER its syllable and means ರ್ in front of it (ಕ + ಾ + 4B = ರ್ಕಾ); a pending link before it
//    is dropped by both composers (ಕ + 64 + 4B = ರ್ಕ)
//  - the length mark 76 joins ಿ ೆ ೊ into ೀ ೇ ೋ; anywhere else it is U+0CD5 ೕ (the Unicode composer usually drops it)
//  - 44 = virama + ZWNJ (halant), 64 at the end = a pending virama, 5E/5C = ZWJ/ZWNJ, D3 = nukta on the last letter
//  - codes with no Unicode meaning (the 7-only codes 86 D7 E6 E7 E8 EA EB EE, and 23) are dropped
const C = { 0x65: 'ಕ', 0x45: 'ಖ', 0x77: 'ಗ', 0x57: 'ಘ', 0xD1: 'ಙ', 0x63: 'ಚ', 0x43: 'ಛ', 0x78: 'ಜ', 0x58: 'ಝ', 0xD2: 'ಞ', 0x72: 'ಟ', 0x52: 'ಠ', 0x74: 'ಡ', 0x54: 'ಢ', 0x46: 'ಣ', 0x67: 'ತ', 0x47: 'ಥ', 0x68: 'ದ', 0x48: 'ಧ', 0x6A: 'ನ', 0x79: 'ಪ', 0x59: 'ಫ', 0x55: 'ಬ', 0x4A: 'ಭ', 0x75: 'ಮ', 0x66: 'ಯ', 0x6B: 'ರ', 0xE3: 'ಱ', 0x69: 'ಲ', 0x49: 'ಳ', 0x6F: 'ವ', 0x4F: 'ಶ', 0x71: 'ಷ', 0x6C: 'ಸ', 0x4C: 'ಹ', 0xE5: 'ೞ', 0xE1: 'ಕ್ಷ', 0xE0: 'ಜ್ಞ', 0xD8: 'ಶ್ರೀ' };
const SIGN = { 0x61: 'ಾ', 0x73: 'ಿ', 0x6E: 'ು', 0x26: 'ೂ', 0x7A: 'ೃ', 0xE2: 'ೄ', 0x62: 'ೆ', 0x6D: 'ೈ', 0x70: 'ೊ', 0x40: 'ೌ', 0xD9: 'ೢ', 0xDA: 'ೣ', 0xF0: 'ೈ', 0xF1: 'ೀ', 0xF2: 'ೇ', 0xF3: 'ೋ' };
const TAIL = { 0x3C: 'ಂ', 0x3E: 'ಃ' };
const IND = { 0x51: 'ಅ', 0x41: 'ಆ', 0x53: 'ಇ', 0x56: 'ಈ', 0x4E: 'ಉ', 0xD5: 'ಊ', 0x5A: 'ಋ', 0x5F: 'ೠ', 0xE9: 'ಌ', 0xED: 'ೡ', 0x42: 'ಎ', 0x24: 'ಏ', 0x4D: 'ಐ', 0x50: 'ಒ', 0xD0: 'ಓ', 0xD6: 'ಔ' };
const OTHER = { 0x20: ' ', 0x21: '!', 0x22: '‘', 0x25: '%', 0x27: '’', 0x28: '[', 0x29: ']', 0x2A: '*', 0x2B: '+', 0x2C: ',', 0x2D: '-', 0x2E: '.', 0x2F: '/', 0x3A: ':', 0x3B: ';', 0x3D: '=', 0x3F: '?', 0x7B: '—', 0x7D: '–', 0x7E: '।', 0xDE: '॥', 0xDB: '₹', 0xDC: '×', 0xDD: '÷', 0xD4: 'ಽ', 0x5E: '‍', 0x5C: '‌' };
for (let d = 0; d < 10; d++) OTHER[0x30 + d] = String.fromCharCode(0x0CE6 + d);
const LINK = 0x64, HAL = 0x44, REPH = 0x4B, NUK = 0xD3, LEN = 0x76;
const DROP = new Set([0x86, 0xD7, 0xE6, 0xE7, 0xE8, 0xEA, 0xEB, 0xEE, 0x23]);
const V = '್', ZWNJ = '‌', ZWJ = '‍';
const LENJOIN = { 'ಿ': 'ೀ', 'ೆ': 'ೇ', 'ೊ': 'ೋ' };

function expected(codes) {
  const out = [];
  let syl = null;
  const flush = () => { if (syl) out.push(render(syl)); syl = null; };
  for (const c of codes) {
    if (C[c] !== undefined) {
      if (syl && syl.link && !syl.ind && !syl.hal) { syl.cl.push((syl.cl.length ? '' : V) + (syl.joiner || '') + C[c]); syl.link = false; syl.joiner = ''; continue; }
      flush();
      syl = { cl: [C[c]], signs: '', tails: '', reph: false, link: false, hal: false, post: '' };
      continue;
    }
    if (IND[c] !== undefined) { flush(); syl = { cl: [IND[c]], signs: '', tails: '', reph: false, link: false, hal: false, post: '', ind: true }; continue; }
    if (SIGN[c] !== undefined) { if (!syl) syl = { cl: [], signs: '', tails: '', reph: false, link: false, hal: false }; if (c === 0x6D && syl.signs.endsWith('ೆ')) syl.signs = syl.signs.slice(0, -1); syl.signs += SIGN[c]; continue; }
    if (c === LEN) {
      if (!syl) { out.push('ೕ'); continue; }
      const last = syl.signs.slice(-1);
      if (LENJOIN[last] && !syl.tails) syl.signs = syl.signs.slice(0, -1) + LENJOIN[last]; else syl.signs += 'ೕ';
      continue;
    }
    if (TAIL[c] !== undefined) { if (!syl) { out.push(TAIL[c]); continue; } syl.tails += TAIL[c]; continue; }
    if (c === NUK) { if (!syl) { out.push('಼'); continue; } if (syl.cl.length && !syl.signs) syl.cl[syl.cl.length - 1] += '಼'; else syl.signs += '಼'; continue; }
    if (c === LINK) { if (!syl) { out.push(V); continue; } syl.link = true; continue; }
    if (c === HAL) { if (!syl) { out.push(V + ZWNJ); continue; } syl.hal = true; syl.link = false; continue; }
    if (c === REPH) { if (!syl || !syl.cl.length) { flush(); out.push('ರ' + V); continue; } syl.reph = true; syl.link = false; continue; }
    if (DROP.has(c)) continue;
    if (OTHER[c] !== undefined) {
      if ((c === 0x5E || c === 0x5C) && syl) { if (syl.link) { syl.joiner = (syl.joiner || '') + OTHER[c]; continue; } }
      flush(); out.push(OTHER[c]); continue;
    }
  }
  flush();
  return key(out.join(''));
}
function render(s) {
  let t = (s.reph ? 'ರ' + V : '') + s.cl.join(V);
  if (s.signs) t += s.signs + s.tails + ((s.link || s.hal) ? V : '') + (s.joiner || '') + (s.hal ? ZWNJ : '');
  else t += ((s.link || s.hal) ? V : '') + (s.joiner || '') + (s.hal ? ZWNJ : '') + s.tails;
  return t;
}
// The key a Unicode string is filed under: canonical composition (ಿ+ೕ = ೀ, ೆ+ೕ = ೇ, ೆ+ೂ = ೊ, ೆ+ೖ = ೈ, ೊ+ೕ = ೋ)
const key = s => s == null ? s : s.normalize('NFC');
const norm = s => key(s);
module.exports = { expected, norm, key, C, SIGN, TAIL, IND, OTHER, DROP };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, JSON.stringify(expected(a.split(',').map(x => parseInt(x, 16)))));
