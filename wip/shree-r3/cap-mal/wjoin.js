// W set: does the 7 composer write the syllables' own answers joined? Does the Unicode composer?
// Every W probe has units (its syllables' code lists); each unit's own answer comes from a probe of exactly that unit
// (capp/P-results.json, capx/E-results.json, capw/W-results.json). -> wjoin.json + console summary
const fs = require('fs');
const { norm, expected } = require('./expected');
const all = [...require('./capp/P-results.json'), ...require('./capx/E-results.json'), ...require('./capw/W-results.json')];
const by = new Map(all.map(r => [r.codes, r]));
const W = require('./capw/W-results.json');
let n = 0, same7 = 0, sameU = 0, noUnit = 0, uMatchesCodes = 0;
const diff7 = [], diffU = new Map();
for (const r of W) {
  if (!r.units) continue;
  const parts = r.units.map(u => by.get(u));
  if (parts.some(p => !p)) { noUnit++; continue; }
  n++;
  const j7 = parts.map(p => p.bytes || '').filter(Boolean).join(' ');
  if ((r.bytes || '') === j7) same7++; else diff7.push(`${r.codes} | ${r.tag} | joined ${j7} | got ${r.bytes}`);
  const jU = parts.map(p => p.u || '').join('');
  if (r.u != null && norm(r.u) === norm(jU)) sameU++;
  else { const shape = r.tag.replace(/[ഀ-ൿ]+/g, 'X').replace(/^(W\d \S+( \S+)?).*/, '$1'); if (!diffU.has(shape)) diffU.set(shape, []); diffU.get(shape).push(`${r.codes} | ${r.tag} | joined ${JSON.stringify(jU)} | got ${JSON.stringify(r.u)} | by codes ${JSON.stringify(expected(r.codes.split(' ').map(x => parseInt(x, 16))))} | 7 ${r.bytes}`); }
  if (r.u != null && norm(r.u) === norm(expected(r.codes.split(' ').map(x => parseInt(x, 16))))) uMatchesCodes++;
}
const out = { probes: W.length, withAllUnitsAnswered: n, unitMissing: noUnit, sevenComposerWritesTheUnitsJoined: same7, sevenDiffers: diff7.length, sevenDiffExamples: diff7.slice(0, 60), unicodeComposerWritesTheUnitsJoined: sameU, unicodeAnswerEqualsWhatTheCodesMean: uMatchesCodes, unicodeDiffByShape: Object.fromEntries([...diffU].map(([k, v]) => [k, { count: v.length, examples: v.slice(0, 6) }])) };
fs.writeFileSync(__dirname + '/wjoin.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify({ ...out, sevenDiffExamples: out.sevenDiffExamples.slice(0, 25), unicodeDiffByShape: Object.fromEntries(Object.entries(out.unicodeDiffByShape).map(([k, v]) => [k, v.count + ' ' + v.examples[0]])) }, null, 1));
