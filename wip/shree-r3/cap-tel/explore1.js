// Explore the Telugu code set: every single code 0x21..0xFE (not the slot references 90-9F / B0-CF), each code after
// క (65), each code before క, each code after ప (79, a letter with a different shape), క + link + code,
// code + link + క, and ర + link + code (to see whether ర్ + C types the vattu or something else).
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (!seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const ALL = [];
for (let c = 0x21; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) ALL.push(c);
for (const c of ALL) add([c], 'code ' + h(c));
for (const c of ALL) add([0x65, c], 'క + ' + h(c));
for (const c of ALL) add([c, 0x65], h(c) + ' + క');
for (const c of ALL) add([0x79, c], 'ప + ' + h(c));
for (const c of ALL) add([0x65, 0x64, c], 'క link + ' + h(c));
for (const c of ALL) add([c, 0x64, 0x65], h(c) + ' + link క');
for (const c of ALL) add([0x6B, 0x64, c], 'ర link + ' + h(c));
fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S));
console.log(S.length);
