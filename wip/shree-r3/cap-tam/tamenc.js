// Tamil Unicode -> Shree internal codes in the order a typist strikes them under a neutral keyboard name
// (visual order: ெ ே ை before the consonant; ொ = ெ C ா, ோ = ே C ா, ௌ = ெ C ள; ு on a native consonant = its
// precomposed code, ூ on a native consonant = 40, on a Grantha letter ு 5D / ூ 5E). Syllables are returned separately.
const CONS = { 'க': 0x67, 'ங': 0x6E, 'ச': 0x72, 'ஞ': 0x4E, 'ட': 0x69, 'ண': 0x64, 'த': 0x79, 'ந': 0x6C, 'ப': 0x6B, 'ம': 0x65, 'ய': 0x63, 'ர': 0x61, 'ல': 0x6D, 'வ': 0x6F, 'ழ': 0x78, 'ள': 0x26, 'ற': 0x73, 'ன': 0x70, 'ஜ': 0x24, 'ஷ': 0xD0, 'ஸ': 0x51, 'ஹ': 0xD2, 'ஶ': 0xE3 };
const NATIVE = new Set([...'கஙசஞடணதநபமயரலவழளறன']);
const U_PRE = { 'ர': 0x41, 'ய': 0x43, 'ண': 0x44, 'ம': 0x45, 'க': 0x47, 'ட': 0x49, 'ப': 0x4B, 'ந': 0x4C, 'ல': 0x4D, 'வ': 0x4F, 'ன': 0x50, 'ச': 0x52, 'ற': 0x53, 'ழ': 0x58, 'த': 0x59, 'ஞ': 0x7E, 'ங': 0x7F, 'ள': 0xD5 };
const VOW = { 'அ': [0x46], 'ஆ': [0x54], 'இ': [0x48], 'ஈ': [0x4A], 'உ': [0x3E], 'ஊ': [0xD6], 'எ': [0x56], 'ஏ': [0x42], 'ஐ': [0x55], 'ஒ': [0x7A], 'ஓ': [0x5A], 'ஔ': [0x7A, 0x26], 'ஃ': [0xD1] };
function syllables(word) {
  const out = [];
  let i = 0;
  const w = [...word.normalize('NFC')];
  while (i < w.length) {
    let base = null, key = null, grantha = false;
    if (w.slice(i, i + 4).join('') === 'ஸ்ரீ') { out.push({ u: 'ஸ்ரீ', codes: [0xD8] }); i += 4; continue; }
    if (w.slice(i, i + 3).join('') === 'க்ஷ') { base = 0xD7; key = 'க்ஷ'; grantha = true; i += 3; }
    else if (CONS[w[i]] != null) { base = CONS[w[i]]; key = w[i]; grantha = !NATIVE.has(w[i]); i += 1; }
    else if (VOW[w[i]]) { out.push({ u: w[i], codes: VOW[w[i]] }); i++; continue; }
    else return null; // not encodable here
    const s = w[i];
    let codes;
    switch (s) {
      case '்': codes = [base, 0x66]; break;
      case 'ா': codes = [base, 0x74]; break;
      case 'ி': codes = [base, 0x68]; break;
      case 'ீ': codes = [base, 0x6A]; break;
      case 'ு': codes = U_PRE[key] ? [U_PRE[key]] : [base, 0x5D]; break;
      case 'ூ': codes = grantha ? [base, 0x5E] : [base, 0x40]; break;
      case 'ெ': codes = [0x76, base]; break;
      case 'ே': codes = [0x62, base]; break;
      case 'ை': codes = [0x75, base]; break;
      case 'ொ': codes = [0x76, base, 0x74]; break;
      case 'ோ': codes = [0x62, base, 0x74]; break;
      case 'ௌ': codes = [0x76, base, 0x26]; break;
      default: codes = [base];
    }
    const u = key + (codes.length > 1 || codes[0] !== base ? s : '');
    out.push({ u, codes });
    if (codes.length > 1 || codes[0] !== base) i++;
  }
  return out;
}
module.exports = { syllables, CONS, NATIVE, U_PRE, VOW };
