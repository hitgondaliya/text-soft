// Round 2 probe set W: do Tamil syllables simply join? (<= 4 codes per probe, a slot holds 4)
// W1 every pair of syllable units (13 shapes x 11 sample letters + 12 independent vowels/aytham) that fits in 4 codes
// W2 all 24 letters: C1 + pre-base sign + C2 (is ெ/ே/ை struck after a letter given to the NEXT letter?), pre-base + C1 + C2,
//    C1 + ா/ி/ீ/ூ + C2, ொ/ோ + C2
// W3 common words (common-words-ta.txt) in typing order when they fit in 4 codes, and every adjacent syllable pair of every
//    word that fits. Sequences already in seq-all.json / seq-explore1.json are not probed again.
// -> seq-w.json (codes typed order, tag, units = the syllables' code lists)
const fs = require('fs');
const { syllables, NATIVE, U_PRE } = require('./tamenc');
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const old = new Set([...JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')), ...JSON.parse(fs.readFileSync(__dirname + '/seq-explore1.json', 'utf8'))].map(s => s.codes.join(',')));
const S = [], seen = new Set();
let skippedOld = 0;
const add = (units, tag) => { const codes = units.flat(); if (codes.length > 4) return; const k = codes.join(','); if (seen.has(k)) return; seen.add(k); if (old.has(k)) { skippedOld++; return; } S.push({ codes, tag, units }); };
const ALL = { 0x67: 'க', 0x6E: 'ங', 0x72: 'ச', 0x4E: 'ஞ', 0x69: 'ட', 0x64: 'ண', 0x79: 'த', 0x6C: 'ந', 0x6B: 'ப', 0x65: 'ம', 0x63: 'ய', 0x61: 'ர', 0x6D: 'ல', 0x6F: 'வ', 0x78: 'ழ', 0x26: 'ள', 0x73: 'ற', 0x70: 'ன', 0x24: 'ஜ', 0xD0: 'ஷ', 0x51: 'ஸ', 0xD2: 'ஹ', 0xE3: 'ஶ', 0xD7: 'க்ஷ' };
const LET = Object.keys(ALL).map(Number);
const SAMPLE = [0x67, 0x69, 0x65, 0x61, 0x26, 0x70, 0x63, 0x51, 0xD0, 0xD7, 0x24];
const uSign = c => (U_PRE[ALL[c]] ? [U_PRE[ALL[c]]] : [c, 0x5D]);
const uuSign = c => (NATIVE.has(ALL[c]) ? [c, 0x40] : [c, 0x5E]);
const unitsOf = c => [[c], [c, 0x74], [c, 0x68], [c, 0x6A], uSign(c), uuSign(c), [c, 0x66], [0x76, c], [0x62, c], [0x75, c], [0x76, c, 0x74], [0x62, c, 0x74], [0x76, c, 0x26]];
const VOWELS = [[0x46], [0x54], [0x48], [0x4A], [0x3E], [0xD6], [0x56], [0x42], [0x55], [0x7A], [0x5A], [0xD1]];
const UNITS = [...SAMPLE.flatMap(unitsOf), ...VOWELS];
// W1
for (const a of UNITS) for (const b of UNITS) add([a, b], 'W1 pair ' + a.map(h).join(' ') + ' | ' + b.map(h).join(' '));
// W2
for (const a of LET) for (const b of LET) {
  for (const s of [0x76, 0x62, 0x75]) { add([[a], [s, b]], 'W2 C + pre ' + h(s) + ' C'); add([[s, a], [b]], 'W2 pre ' + h(s) + ' C + C'); }
  for (const p of [0x74, 0x68, 0x6A]) add([[a, p], [b]], 'W2 C ' + h(p) + ' + C');
  add([uSign(a), [b]], 'W2 Cு + C'); add([uuSign(a), [b]], 'W2 Cூ + C');
  for (const s of [0x76, 0x62]) add([[s, a, 0x74], [b]], 'W2 two-part ' + h(s) + ' + C');
}
// W3
const words = fs.readFileSync(__dirname + '/common-words-ta.txt', 'utf8').split(/\r?\n/).map(x => x.trim()).filter(Boolean);
const wordInfo = [];
for (const w of words) {
  const syl = syllables(w);
  if (!syl) { wordInfo.push({ w, encodable: false }); continue; }
  const codes = syl.flatMap(s => s.codes);
  wordInfo.push({ w, syllables: syl.map(s => s.u), codes: codes.map(h).join(' '), whole: codes.length <= 4 });
  if (codes.length <= 4) add(syl.map(s => s.codes), 'W3 word ' + w);
  for (let i = 0; i + 1 < syl.length; i++) add([syl[i].codes, syl[i + 1].codes], 'W3 pair in ' + w + ': ' + syl[i].u + '+' + syl[i + 1].u);
  for (let i = 0; i + 2 < syl.length; i++) add([syl[i].codes, syl[i + 1].codes, syl[i + 2].codes], 'W3 triple in ' + w + ': ' + syl[i].u + '+' + syl[i + 1].u + '+' + syl[i + 2].u);
}
fs.writeFileSync(__dirname + '/seq-w.json', JSON.stringify(S));
fs.writeFileSync(__dirname + '/words-encoded.json', JSON.stringify(wordInfo, null, 1));
console.log('W sequences', S.length, 'already probed (skipped)', skippedOld, 'words', words.length, 'whole words in 4 codes', wordInfo.filter(x => x.whole).length, 'not encodable', wordInfo.filter(x => x.encodable === false).map(x => x.w).join(' '));
