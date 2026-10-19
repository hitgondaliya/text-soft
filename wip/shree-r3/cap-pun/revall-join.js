// Join the 5 revall runs: for every non-preferred spelling Shree types for a string, does our reverse table read it back
// as the same text (measure.exe SameText: equal after NFD + vowel-carrier joins, or text Shree writes the same bytes for)?
// Kind of each spelling = the kinds of the pairs rows that gave it. Writes revall/summary.json and revall/failures.tsv.
const fs = require('fs');
const g = require('./pun7-groundtruth.json');
const idx = require('./revall/index.json');
const kinds = new Map();
for (const p of g.pairs) { const k = p.unicode + '\t' + p.bytes; if (!kinds.has(k)) kinds.set(k, new Set()); kinds.get(k).add(p.kind.replace(/ \(.*$/, '')); }
const fails = new Map();
for (let k = 0; k < 5; k++) {
  const lines = fs.readFileSync(`revall/m${k}/reverse-failures.tsv`, 'utf8').split('\n').slice(1).filter(Boolean);
  for (const l of lines) { const [bytes, u, ours] = l.split('\t'); fails.set(k + '\t' + u + '\t' + bytes, ours); }
}
const byKind = {}, out = ['bytes\tshree_unicode\tours\tkind\taccepted_or_alt'];
let nFail = 0;
for (const x of idx) {
  const ours = fails.get(x.k + '\t' + x.u + '\t' + x.bytes);
  const kk = [...(kinds.get(x.u + '\t' + x.bytes) || ['?'])].join('+');
  const key = (x.accepted ? 'accepted ' : 'alternative ') + kk;
  byKind[key] = byKind[key] || { spellings: 0, readBackSameText: 0, examples: [] };
  byKind[key].spellings++;
  if (ours === undefined) byKind[key].readBackSameText++;
  else { nFail++; if (byKind[key].examples.length < 8) byKind[key].examples.push(`${x.bytes}: Shree ${x.u} | ours ${ours}`); out.push(`${x.bytes}\t${x.u}\t${ours}\t${kk}\t${x.accepted ? 'accepted' : 'alt'}`); }
}
const sum = { otherSpellings: idx.length, readBackSameText: idx.length - nFail, failing: nFail, byKind };
fs.writeFileSync('revall/summary.json', JSON.stringify(sum, null, 1));
fs.writeFileSync('revall/failures.tsv', out.join('\n') + '\n');
console.log(JSON.stringify(sum, null, 1));
