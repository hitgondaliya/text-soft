// What a Telugu code sequence (typed order, Shree's internal codes) means in Unicode logical order, and how the
// Unicode composer's answers are normalised and filed.
//  - a consonant starts a syllable; the link 64 + a consonant joins it to the cluster (C1 + ్ + C2); a vowel sign or
//    a tail struck BEFORE the link (C1 + ి + 64 + C2) still belongs after the whole cluster (the composer moves it)
//  - the ra-vattu codes D7 / ED are struck BEFORE the consonant they belong to (D7 + C = C్ర, D7 + C + ి = C్రి,
//    D7 + C1 + link + C2 = C1్C2్ర); struck after a letter with no letter after it they stay pending (C + D7 = C + ్ర)
//  - 44 = virama + ZWNJ (halant), 64 at the end = a pending virama, F2/F3 = ZWJ/ZWNJ
//  - codes with no Unicode meaning in the U composer (E1, 86, DB, E7, E9-EB, EE) are dropped
const C = { 0x65: 'క', 0x45: 'ఖ', 0x77: 'గ', 0x57: 'ఘ', 0xD1: 'ఙ', 0x63: 'చ', 0x43: 'ఛ', 0x78: 'జ', 0x58: 'ఝ', 0xD2: 'ఞ', 0x72: 'ట', 0x52: 'ఠ', 0x74: 'డ', 0x54: 'ఢ', 0x46: 'ణ', 0x67: 'త', 0x47: 'థ', 0x68: 'ద', 0x48: 'ధ', 0x6A: 'న', 0x79: 'ప', 0x59: 'ఫ', 0x55: 'బ', 0x4A: 'భ', 0x75: 'మ', 0x66: 'య', 0x6B: 'ర', 0x4B: 'ఱ', 0x69: 'ల', 0x49: 'ళ', 0x6F: 'వ', 0x4F: 'శ', 0x71: 'ష', 0x6C: 'స', 0x4C: 'హ', 0x5E: 'క్ష', 0xD8: 'శ్రీ' };
const SIGN = { 0x61: 'ా', 0x73: 'ి', 0x76: 'ీ', 0x6E: 'ు', 0x26: 'ూ', 0x7A: 'ృ', 0x5F: 'ౄ', 0x62: 'ె', 0xD3: 'ే', 0x6D: 'ై', 0x70: 'ొ', 0x24: 'ో', 0x40: 'ౌ', 0xF0: 'ౢ', 0xF1: 'ౣ' };
const TAIL = { 0x3C: 'ం', 0x3E: 'ః', 0xE0: 'ఁ' };
const IND = { 0x51: 'అ', 0x41: 'ఆ', 0x53: 'ఇ', 0x56: 'ఈ', 0x4E: 'ఉ', 0xD5: 'ఊ', 0x5A: 'ఋ', 0xE2: 'ౠ', 0xD9: 'ఌ', 0xDA: 'ౡ', 0x42: 'ఎ', 0xD4: 'ఏ', 0x4D: 'ఐ', 0x50: 'ఒ', 0xD0: 'ఓ', 0xD6: 'ఔ' };
const OTHER = { 0x20: ' ', 0x21: '!', 0x22: '‘', 0x25: '%', 0x27: '’', 0x28: '(', 0x29: ')', 0x2A: '*', 0x2B: '+', 0x2C: ',', 0x2D: '-', 0x2E: '.', 0x2F: '/', 0x3A: ':', 0x3B: ';', 0x3D: '=', 0x3F: '?', 0x5B: '[', 0x5D: ']', 0x7B: '—', 0x7D: '–', 0xFB: '—', 0xFD: '–', 0xDC: '₹', 0xDD: '×', 0xDE: '÷', 0xE3: 'ఽ', 0xE4: '।', 0xF4: '॥', 0xF2: '‍', 0xF3: '‌' };
for (let d = 0; d < 10; d++) OTHER[0x30 + d] = String.fromCharCode(0x0C66 + d);
const LINK = 0x64, HAL = 0x44, RAV = new Set([0xD7, 0xED]);
const DROP = new Set([0xE1, 0x86, 0xDB, 0xE7, 0xE9, 0xEA, 0xEB, 0xEE, 0x23]);
const V = '్';

// Syllables: { cl: [letters], signs: '', tails: '', ra: bool, link: bool (pending), hal: bool }
function expected(codes) {
  const out = [];
  let syl = null, preRa = false, loneLink = false;
  const flush = () => { if (syl) out.push(render(syl)); syl = null; };
  for (const c of codes) {
    if (preRa && C[c] === undefined && !RAV.has(c)) { out.push(V + 'ర'); preRa = false; } // pending ra-vattu: stays where it was struck
    if (C[c] !== undefined) {
      if (loneLink) { loneLink = false; out.push(C[c]); continue; }
      if (syl && syl.link) { syl.cl.push(C[c]); syl.link = false; continue; }
      flush();
      syl = { cl: [C[c]], signs: '', tails: '', ra: preRa, link: false, hal: false, ind: false };
      preRa = false;
      continue;
    }
    if (IND[c] !== undefined) { flush(); syl = { cl: [IND[c]], signs: '', tails: '', ra: false, link: false, hal: false, ind: true }; continue; }
    if (SIGN[c] !== undefined) { if (!syl) { out.push(SIGN[c]); continue; } syl.signs += SIGN[c]; continue; }
    if (TAIL[c] !== undefined) { if (!syl) { out.push(TAIL[c]); continue; } syl.tails += TAIL[c]; continue; }
    if (c === LINK) { if (!syl) { out.push(V); syl = null; loneLink = true; continue; } syl.link = true; continue; }
    if (c === HAL) { if (!syl) { out.push(V + '‌'); continue; } syl.hal = true; flush(); continue; }
    if (RAV.has(c)) { flush(); preRa = true; continue; } // the ra-vattu belongs to the NEXT consonant (pre-typed)
    if (DROP.has(c)) continue;
    if (OTHER[c] !== undefined) {
      if ((c === 0xF2 || c === 0xF3) && syl) { syl.joiner = (syl.joiner || '') + OTHER[c]; continue; }
      flush(); out.push(OTHER[c]); continue;
    }
  }
  flush();
  if (preRa) out.push(V + 'ర');
  return key(out.join(''));
}
function render(s) {
  let t = s.cl.join(V);
  if (s.ra) t += V + 'ర';
  if (s.link || s.hal) t += V;
  if (s.joiner) t += s.joiner;
  if (s.hal) t += '‌';
  return t + s.signs + s.tails;
}

// The Unicode composer writes the ra-vattu as "*్ర" where it was struck and leaves it for the NEXT consonant (D7 + క
// = "*్రక"; క + D7 + క = "క*్రక" = క + క్ర, and the 7 composer writes the hook 7B before that second క). Read it as ్ర
// behind the cluster that follows; with no consonant after it, it stays where it was struck (pending).
const LETTER = /[అ-హ]/;
const CONSRE = /[క-హ]/;
const MARK = /[ఀ-ఄా-ౌౕౖౢౣ]/;
function clusterEndFrom(t, i) { // t[i] is a letter: end of its cluster
  i++;
  while (t[i] === V && CONSRE.test(t[i + 1] || '')) i += 2;
  return i;
}
function fixRaVattu(u) {
  if (u == null || !u.includes('*' + V + 'ర')) return u;
  let guard = 0;
  while (u.includes('*' + V + 'ర') && guard++ < 6) {
    const p = u.indexOf('*' + V + 'ర');
    const before = u.slice(0, p), after = u.slice(p + 3);
    if (CONSRE.test(after[0] || '')) { // the composers' model: "*్ర" waits for the NEXT consonant
      const e = clusterEndFrom(after, 0);
      u = before + after.slice(0, e) + V + 'ర' + after.slice(e);
    } else u = before + V + 'ర' + after; // nothing follows: a pending ra-vattu, left where it was struck
  }
  return u;
}
// The key a Unicode string is filed under: ె + ౖ joined to ై (NFC does the same).
const key = s => s == null ? s : s.split('ై').join('ై');
const norm = s => key(s).normalize('NFC');
module.exports = { expected, norm, key, fixRaVattu, C, SIGN, TAIL, IND, OTHER };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, JSON.stringify(expected(a.split(',').map(x => parseInt(x, 16)))));
