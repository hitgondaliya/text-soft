// What a Tamil code sequence (typed order, Shree's internal codes) means in Unicode logical order.
// Typing model of the composers under a neutral keyboard name (explore1-show.txt, capp/P-results.json):
//  - a consonant (or a precomposed syllable code) starts a syllable; Tamil clusters are plain syllables C1் C2
//  - the pre-base signs ெ 76 / ே 62 / ை 75 / ை(old curl) D3 are struck BEFORE their consonant (visual order) and
//    belong to the next consonant; ா 74 after ெ/ே + C makes ொ/ோ; ள 26 right after ெ + C makes ௌ (the au length mark
//    is drawn as ள, and the Tamil keyboards type ௌ as ெ C ள: vendor slots "76 26")
//  - post-base signs follow the consonant: ா 74, ி 68, ீ 6A, ு 5D (Grantha hook FA), ூ 5E (Grantha hook FC), ு 5F
//    (Unicode composer only), ூ 40 (the native ligature), ் 66
//  - 77 and 20 = ஂ, D1 = ஃ, A4/EF = ZWNJ, A5 = ZWJ; A1 ோ A2 ொ A3 ௌ A6 ஔ are Unicode-composer-only codes
//  - codes with no Unicode meaning for the Unicode composer (03 E0 86 80 83 57 and the symbol codes it does not answer)
//    are dropped; 57 = சூ for the 7 composer only
const C = { 0x67: 'க', 0x6E: 'ங', 0x72: 'ச', 0x4E: 'ஞ', 0x69: 'ட', 0x64: 'ண', 0x79: 'த', 0x6C: 'ந', 0x6B: 'ப', 0x65: 'ம', 0x63: 'ய', 0x61: 'ர', 0x6D: 'ல', 0x6F: 'வ', 0x78: 'ழ', 0x26: 'ள', 0x73: 'ற', 0x70: 'ன', 0x24: 'ஜ', 0xD0: 'ஷ', 0x51: 'ஸ', 0xD2: 'ஹ', 0xE3: 'ஶ', 0xD7: 'க்ஷ', 0xD8: 'ஸ்ரீ' };
// precomposed syllables: [consonant, sign]
const PRECOMP = { 0x41: ['ர', 'ு'], 0x43: ['ய', 'ு'], 0x44: ['ண', 'ு'], 0x45: ['ம', 'ு'], 0x47: ['க', 'ு'], 0x49: ['ட', 'ு'], 0x4B: ['ப', 'ு'], 0x4C: ['ந', 'ு'], 0x4D: ['ல', 'ு'], 0x4F: ['வ', 'ு'], 0x50: ['ன', 'ு'], 0x52: ['ச', 'ு'], 0x53: ['ற', 'ு'], 0x58: ['ழ', 'ு'], 0x59: ['த', 'ு'], 0x7E: ['ஞ', 'ு'], 0x7F: ['ங', 'ு'], 0xD5: ['ள', 'ு'], 0x3C: ['ன', 'ா'], 0x71: ['ற', 'ா'], 0xD4: ['ண', 'ா'] };
const OLD = new Set([0x3C, 0x71, 0xD4, 0xD3]); // old (pre-1978) orthography: னா றா ணா ligatures, the ை curl
const PRE = { 0x76: 'ெ', 0x62: 'ே', 0x75: 'ை', 0xD3: 'ை' };
const POST = { 0x74: 'ா', 0x68: 'ி', 0x6A: 'ீ', 0x5D: 'ு', 0x5E: 'ூ', 0x5F: 'ு', 0x40: 'ூ', 0x81: 'ி', 0x84: 'ி', 0x82: 'ீ', 0x85: 'ீ', 0xA1: 'ோ', 0xA2: 'ொ', 0xA3: 'ௌ', 0x66: '்' };
const TAIL = { 0x77: 'ஂ', 0x20: 'ஂ' };
const IND = { 0x46: 'அ', 0x54: 'ஆ', 0x48: 'இ', 0x4A: 'ஈ', 0x3E: 'உ', 0xD6: 'ஊ', 0x56: 'எ', 0x42: 'ஏ', 0x55: 'ஐ', 0x7A: 'ஒ', 0x5A: 'ஓ', 0xA6: 'ஔ', 0xD1: 'ஃ' };
const OTHER = { 0x21: '!', 0x22: '‘', 0x23: '#', 0x25: '%', 0x27: '’', 0x28: '(', 0x29: ')', 0x2A: '*', 0x2B: '+', 0x2C: ',', 0x2D: '–', 0x2E: '.', 0x2F: '/', 0x3A: ':', 0x3B: ';', 0x3D: '=', 0x3F: '?',
  0x5B: '௸', 0x5C: '௶', 0x7C: '௷', 0x09: '௶', 0x0A: '௷', 0x0B: '[', 0x0C: ']', 0x0D: '“', 0x0E: '”', 0x12: '#', 0x13: '$', 0x14: '<', 0x15: '>', 0x16: '@', 0x18: '_', 0x1A: '{', 0x1B: '}',
  0xE1: '।', 0xED: '॥', 0xE2: '௳', 0xE4: '௰', 0xE5: '௱', 0xE6: '௲', 0xE7: 'ௐ', 0xE8: '௵', 0xE9: '௴', 0xEA: '௺', 0xEB: '௹', 0xEC: '”', 0xEE: '“',
  0xF2: '@', 0xF4: '$', 0xF6: '&', 0xF7: '_', 0xF8: '[', 0xF9: ']', 0xFA: '{', 0xFB: '}', 0xFC: '<', 0xFD: '>', 0xA4: '‌', 0xEF: '‌', 0xA5: '‍',
  0x06: '॒', 0xDE: '॒', 0xDA: '॑', 0x01: '॑', 0x02: '᳚', 0xDB: '᳚' };
for (let d = 0; d < 10; d++) OTHER[0x30 + d] = String.fromCharCode(0x0BE6 + d);
// the Unicode composer writes nothing for these (7-only or no-answer codes): read as nothing
const DROP = new Set([0x03, 0xE0, 0x86, 0x80, 0x83, 0x57, 0x60, 0xD9, 0xDC, 0xDD, 0x7B, 0x7D, 0xF3, 0xF5, 0xFE, 0x04, 0x05, 0x07, 0x08, 0x0F, 0x10, 0x11, 0x17, 0x19, 0x1C, 0x1D, 0x1E, 0x1F]);
const JOIN = { 'ொ': 'ொ', 'ோ': 'ோ' };

function expected(codes) {
  const out = [];
  let syl = null, pend = '';
  const flush = () => { if (syl) out.push(syl.base + syl.signs + syl.tails); syl = null; };
  const start = (base, sign) => { flush(); syl = { base, signs: pend + (sign || ''), tails: '', cons: true }; pend = ''; };
  for (const c of codes) {
    if (c === 0x26 && syl && syl.cons && syl.signs === 'ெ' && !syl.tails) { syl.signs = 'ௌ'; continue; }
    if (C[c] !== undefined) { start(C[c]); continue; }
    if (PRECOMP[c] !== undefined) { const [b, s] = PRECOMP[c]; const p = pend; start(b, ''); syl.signs = JOIN[p + s] || (p + s); continue; }
    if (PRE[c] !== undefined) { flush(); pend += PRE[c]; continue; }
    if (POST[c] !== undefined) {
      if (!syl) { flush(); out.push(pend + POST[c]); pend = ''; continue; }
      const last = syl.signs.slice(-1);
      if (c === 0x74 && JOIN[last + 'ா'] && !syl.tails) syl.signs = syl.signs.slice(0, -1) + JOIN[last + 'ா'];
      else syl.signs += POST[c];
      continue;
    }
    if (TAIL[c] !== undefined) { if (!syl) { out.push(pend + TAIL[c]); pend = ''; continue; } syl.tails += TAIL[c]; continue; }
    if (IND[c] !== undefined) { flush(); if (pend) { out.push(pend); pend = ''; } syl = { base: IND[c], signs: '', tails: '', cons: false }; continue; }
    if (DROP.has(c)) continue;
    if (OTHER[c] !== undefined) { flush(); if (pend) { out.push(pend); pend = ''; } out.push(OTHER[c]); continue; }
  }
  flush();
  if (pend) out.push(pend);
  return key(out.join(''));
}
// The key a Unicode string is filed under: canonical composition (ெ+ா = ொ, ே+ா = ோ, ெ+ௗ = ௌ)
const key = s => s == null ? s : s.normalize('NFC');
const norm = s => key(s);
module.exports = { expected, norm, key, C, PRECOMP, PRE, POST, TAIL, IND, OTHER, DROP, OLD };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, JSON.stringify(expected(a.split(',').map(x => parseInt(x, 16)))));
