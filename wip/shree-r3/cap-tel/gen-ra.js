// Set R: the ra-vattu struck FIRST, before the letter (the vendor charts' order: TYPEWRIT K r a = స్త్ర, ITR Ctrl+Space g f = క్ర,
// INDICA/APPLE slots D7 + C). The 7 composer then writes the hook BEFORE the letter (7B C), where the శ్రీ ligature 7D
// has it; struck after the letter it writes C 7B (hook on the right). Codes <= 4 per sequence; ై gets a space after it.
const fs = require('fs');
const { C, SIGN, TAIL } = require('./expected');
const CONS = Object.entries(C).filter(([, t]) => t !== 'శ్రీ').map(([c, t]) => [Number(c), t]);
const SIGNS = Object.entries(SIGN).filter(([c]) => Number(c) !== 0xF0 && Number(c) !== 0xF1).map(([c, t]) => [Number(c), t]);
const TAILS = Object.entries(TAIL).map(([c, t]) => [Number(c), t]);
const S = [], seen = new Set();
const add = (codes, tag) => { if (codes[codes.length - 1] === 0x6D && codes.length <= 3) { codes = [...codes, 0x20]; tag += ' +term'; } const k = codes.join(','); if (codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
for (const [rv, rn] of [[0xD7, 'rav'], [0xED, 'rav2(ED)'], [0xE1, 'rav3(E1)']]) {
  for (const [c, t] of CONS) {
    add([rv, c], `${rn} first ${t}`);
    if (rv === 0xE1) continue;
    for (const [s, st] of SIGNS) { add([rv, c, s], `${rn} first ${t} ${st}`); if (rv === 0xD7) for (const [tc, tt] of TAILS) if (s !== 0x6D) add([rv, c, s, tc], `${rn} first ${t} ${st} ${tt}`); }
    if (rv === 0xD7) { for (const [tc, tt] of TAILS) add([rv, c, tc], `${rn} first ${t} ${tt}`); add([rv, c, 0x6D, 0x3C], `${rn} first ${t} ై ం`); }
    if (rv === 0xD7) for (const [c2, t2] of CONS) add([rv, c, 0x64, c2], `${rn} first ${t} link ${t2}`);
    if (rv === 0xD7) add([rv, c, 0x44, 0x20], `${rn} first ${t} halant +term`);
  }
}
// the second letter of a word after a ra-first syllable, and ra-first after a syllable (does 7B stay with its letter?)
for (const [c, t] of CONS) { add([0x65, 0xD7, c], `క then rav first ${t}`); add([0x65, 0x73, 0xD7, c], `కి then rav first ${t}`); add([0xD7, c, 0x65], `rav first ${t} then క`); }
fs.writeFileSync(__dirname + '/seq-ra.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
