// Third probe set (prefix W): three-consonant clusters the 4-code limit still allows.
//  - C1 + link + C2 + E1 (the 7-only ba-phala code): the chart clusters ତ୍ତ୍ବ ଦ୍ଧ୍ବ ନ୍ଧ୍ବ ନ୍ଦ୍ବ ଜ୍ଜ୍ବ ଦ୍ଦ୍ବ and every other
//    C1୍C2୍ବ - the Unicode composer writes nothing for E1, so these are 7-byte evidence only (what glyph Shree draws)
//  - C1 + link + C2 + link + C3 needs 5 codes: not possible; C1 + link + C2 + D1/D2 is in the P set
//  - C + E1 + reph / tails, and C1 + link + C2 + 51 (the hook code) for the ତ୍ forms
const fs = require('fs');
const CONS = { 'କ': 0x73, 'ଖ': 0x53, 'ଗ': 0x78, 'ଘ': 0x58, 'ଙ': 0x5A, 'ଚ': 0x76, 'ଛ': 0x56, 'ଜ': 0x63, 'ଝ': 0x43, 'ଞ': 0xD8, 'ଟ': 0x55, 'ଠ': 0x49, 'ଡ': 0x4F, 'ଢ': 0x50, 'ଣ': 0x4A, 'ତ': 0x66, 'ଥ': 0x46, 'ଦ': 0x72, 'ଧ': 0x52, 'ନ': 0x65, 'ପ': 0x77, 'ଫ': 0x57, 'ବ': 0x71, 'ଭ': 0x7A, 'ମ': 0x61, 'ଯ': 0x75, 'ର': 0x6A, 'ଲ': 0x4D, 'ଳ': 0x6D, 'ଶ': 0x4B, 'ଷ': 0x4C, 'ସ': 0x74, 'ହ': 0x6F, 'ୟ': 0xD7 };
const LINK = 0x64, BAPH = 0xE1;
const S = [];
const seen = new Set([...JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')), ...JSON.parse(fs.readFileSync(__dirname + '/seq-halant.json', 'utf8'))].map(s => s.codes.join(',')));
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const cl = Object.entries(CONS);
for (const [b1, c1] of cl) for (const [b2, c2] of cl) add([c1, LINK, c2, BAPH], `${b1} link ${b2} baph`);
for (const [b, c] of cl) { add([c, BAPH, 0x40], `${b} baph reph`); add([c, BAPH, 0x69], `${b} baph ଂ`); add([0x6C, c, BAPH, 0x6B], `${b} baph ୋ`); }
fs.writeFileSync(__dirname + '/seq-wide.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
