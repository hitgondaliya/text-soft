// Explore the Punjabi code set: every single code 0x21..0xFE (not the slot references 90-9F / B0-CF), each code after
// ਕ (73), each code before ਕ, and a few known two-code joins.
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (!seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const ALL = [];
for (let c = 0x21; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) ALL.push(c);
for (const c of ALL) add([c], 'code ' + h(c));
for (const c of ALL) add([0x73, c], 'ਕ + ' + h(c));
for (const c of ALL) add([c, 0x73], h(c) + ' + ਕ');
fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S));
console.log(S.length);
