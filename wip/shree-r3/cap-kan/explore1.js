// Explore the Kannada code set: every single code 0x21..0xFE (not the slot references 90-9F / B0-CF), each code after
// ಕ (65), each code before ಕ, each code after ಪ (79, a letter with a different shape), ಕ + link + code,
// code + link + ಕ, and ರ + link + code (to see whether ರ్ + C types the vattu or something else).
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (!seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const ALL = [];
for (let c = 0x21; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) ALL.push(c);
for (const c of ALL) add([c], 'code ' + h(c));
for (const c of ALL) add([0x65, c], 'ಕ + ' + h(c));
for (const c of ALL) add([c, 0x65], h(c) + ' + ಕ');
for (const c of ALL) add([0x79, c], 'ಪ + ' + h(c));
for (const c of ALL) add([0x65, 0x64, c], 'ಕ link + ' + h(c));
for (const c of ALL) add([c, 0x64, 0x65], h(c) + ' + link ಕ');
for (const c of ALL) add([0x6B, 0x64, c], 'ರ link + ' + h(c));
fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S));
console.log(S.length);
// Kannada additions: the reph code 4B and the halant 44 in more positions; signs on ಪ / ಮ / ಗ (letters with other heads)
{
  const S2 = JSON.parse(fs.readFileSync(__dirname + '/seq-explore1.json', 'utf8'));
  const seen2 = new Set(S2.map(s => s.codes.join(',')));
  const add2 = (codes, tag) => { const k = codes.join(','); if (!seen2.has(k)) { seen2.add(k); S2.push({ codes, tag }); } };
  const SG = [0x61, 0x73, 0x76, 0x6E, 0x26, 0x7A, 0xE2, 0x62, 0x6D, 0x70, 0x40, 0xF0, 0xF1, 0xF2, 0xF3, 0xD9, 0xDA];
  for (const b of [0x65, 0x79, 0x75, 0x77, 0x6B, 0x4C, 0x6C, 0x63]) for (const s of SG) { add2([b, s], h(b) + ' + sign ' + h(s)); add2([b, s, 0x4B], h(b) + ' sign ' + h(s) + ' reph'); add2([0x4B, b, s], 'reph ' + h(b) + ' sign ' + h(s)); add2([b, s, 0x76], h(b) + ' sign ' + h(s) + ' 76'); }
  for (const b of [0x65, 0x79, 0x75]) { add2([b, 0x4B, 0x3C], h(b) + ' reph ಂ'); add2([b, 0x3C, 0x4B], h(b) + ' ಂ reph'); add2([b, 0x64, 0x65, 0x4B], h(b) + ' link ಕ reph'); add2([0x4B, b, 0x64, 0x65], 'reph ' + h(b) + ' link ಕ'); add2([b, 0x4B, 0x64, 0x65], h(b) + ' reph link ಕ'); add2([0x6B, 0x64, b], 'ರ link ' + h(b)); add2([0x6B, 0x44, b], 'ರ halant ' + h(b)); add2([b, 0x44, 0x65], h(b) + ' halant ಕ'); add2([b, 0x44, 0x20], h(b) + ' halant space'); add2([b, 0x64, 0x20], h(b) + ' link space'); add2([b, 0x64, 0x5E], h(b) + ' link zwj'); add2([b, 0x64, 0x5C], h(b) + ' link zwnj'); add2([b, 0x64, 0x5E, 0x65], h(b) + ' link zwj ಕ'); add2([b, 0x64, 0x5C, 0x65], h(b) + ' link zwnj ಕ'); add2([b, 0xD3], h(b) + ' nukta'); add2([b, 0xD3, 0x61], h(b) + ' nukta ಾ'); }
  fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S2));
  console.log('with additions', S2.length);
}
