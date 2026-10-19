// Probe set W: do Malayalam syllables simply join? (<= 4 codes per probe, a slot holds 4)
// W1 every pair of syllable units (shapes x sample letters + vowels + chillus) that fits in 4 codes
// W2 all 36 letters: C1 + pre-base sign + C2 (is െ/േ/്ര struck after a letter given to the NEXT letter?), C1 + chillu,
//    chillu + C2, C1്C2 + C3 (a cluster then a new letter), C + ം + C2
// W3 common words (common-words-ml.txt) in typing order when they fit in 4 codes, and every adjacent syllable pair / triple
//    of every word that fits. Sequences already in seq-all.json / seq-explore1.json are not probed again.
// -> seq-w.json (codes typed order, tag, units = the syllables' code lists)
const fs = require('fs');
const { syllables } = require('./malenc');
const { C } = require('./expected');
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const old = new Set([...JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')), ...JSON.parse(fs.readFileSync(__dirname + '/seq-explore1.json', 'utf8'))].map(s => s.codes.join(',')));
const S = [], seen = new Set();
let skippedOld = 0;
const add = (units, tag) => { const codes = units.flat(); if (codes.length > 4 || !codes.length) return; const k = codes.join(','); if (seen.has(k)) return; seen.add(k); if (old.has(k)) { skippedOld++; return; } S.push({ codes, tag, units }); };
const LET = Object.keys(C).map(Number).filter(c => c !== 0xD5);
const SAMPLE = [0x66, 0xD7, 0x72, 0x67, 0x61, 0x75, 0x68, 0x69, 0x63, 0x79, 0x6E, 0xD5];
const unitsOf = c => [[c], [c, 0x6C], [c, 0x6A], [c, 0x4A], [c, 0x6B], [c, 0x6F], [c, 0x4F], [0x73, c], [0x53, c], [0x73, 0x73, c], [0x73, c, 0x6C], [0x53, c, 0x6C], [0x59, c], [c, 0x55], [c, 0x49], [c, 0x64], [c, 0x41]];
const VOWELS = [[0x4C], [0xD2], [0x54], [0x54, 0x4F], [0x4B], [0x74], [0xD1], [0x51], [0x51, 0x6C], [0x73, 0x74]];
const CHILLUS = [[0x47], [0x48], [0x4E], [0x52], [0x58]];
const CLUST = [[0x66, 0x64, 0x66], [0xD7, 0x64, 0xD7], [0x72, 0x64, 0x72], [0x6D, 0x64, 0x6D], [0x76, 0x64, 0x76], [0x65, 0x64, 0x65], [0x68, 0x64, 0x68], [0x61, 0x64, 0x65], [0x72, 0x64, 0xD7], [0x42, 0x64, 0x76], [0x62, 0x64, 0x66], [0x63, 0x64, 0xD7], [0x79, 0x64, 0x79], [0x72, 0x64, 0x79]];
const UNITS = [...SAMPLE.flatMap(unitsOf), ...VOWELS, ...CHILLUS, ...CLUST];
// W1
for (const a of UNITS) for (const b of UNITS) add([a, b], 'W1 pair ' + a.map(h).join(' ') + ' | ' + b.map(h).join(' '));
// W2
for (const a of LET) for (const b of LET) {
  for (const s of [0x73, 0x53, 0x59]) add([[a], [s, b]], 'W2 C + pre ' + h(s) + ' C');
  add([[a, 0x6C], [b]], 'W2 Cാ + C'); add([[a, 0x41], [b]], 'W2 Cം + C'); add([[a], [b, 0x6C]], 'W2 C + Cാ');
}
for (const a of LET) for (const ch of CHILLUS) { add([[a], ch], 'W2 C + chillu'); add([ch, [a]], 'W2 chillu + C'); add([ch, [a, 0x6C]], 'W2 chillu + Cാ'); add([ch, [0x73, a]], 'W2 chillu + െC'); }
for (const cl of CLUST) for (const b of LET) add([cl, [b]], 'W2 cluster + C');
// W3
const words = fs.readFileSync(__dirname + '/common-words-ml.txt', 'utf8').split(/\r?\n/).map(x => x.trim()).filter(Boolean);
const wordInfo = [];
for (const w of words) {
  const syl = syllables(w);
  if (!syl) { wordInfo.push({ w, encodable: false }); continue; }
  const codes = syl.flatMap(s => s.codes);
  wordInfo.push({ w, syllables: syl.map(s => s.u), codes: codes.map(h).join(' '), whole: codes.length <= 4 });
  if (codes.length <= 4) add(syl.map(s => s.codes), 'W3 word ' + w);
  for (let i = 0; i + 1 < syl.length; i++) add([syl[i].codes, syl[i + 1].codes], 'W3 pair in ' + w + ': ' + syl[i].u + '+' + syl[i + 1].u);
  for (let i = 0; i + 2 < syl.length; i++) add([syl[i].codes, syl[i + 1].codes, syl[i + 2].codes], 'W3 triple in ' + w + ': ' + syl[i].u + '+' + syl[i + 1].u + '+' + syl[i + 2].u);
  for (const s of syl) add([s.codes], 'W3 syllable of ' + w + ': ' + s.u);
}
fs.writeFileSync(__dirname + '/seq-w.json', JSON.stringify(S));
fs.writeFileSync(__dirname + '/words-encoded.json', JSON.stringify(wordInfo, null, 1));
console.log('W sequences', S.length, 'files', Math.ceil(S.length / 47), 'already probed (skipped)', skippedOld, 'words', words.length, 'whole words in 4 codes', wordInfo.filter(x => x.whole).length);
