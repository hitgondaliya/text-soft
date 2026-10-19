// Do syllables join? For every W probe (two or three syllable units typed in one keystroke), compare what each
// composer wrote with its answers for the units typed alone (P/E/W single-unit probes).
// node wjoin.js -> wjoin.json
const fs = require('fs');
const D = __dirname;
const W = JSON.parse(fs.readFileSync(D + '/capw/W-results.json', 'utf8'));
const man = JSON.parse(fs.readFileSync(D + '/w/W-manifest.json', 'utf8'));
const seq = JSON.parse(fs.readFileSync(D + '/seq-w.json', 'utf8'));
const unitsBy = new Map(seq.map(s => [s.codes.map(c => c.toString(16).toUpperCase().padStart(2, '0')).join(' '), s.units]));
const all = [...JSON.parse(fs.readFileSync(D + '/capp/P-results.json', 'utf8')), ...JSON.parse(fs.readFileSync(D + '/capx/E-results.json', 'utf8')), ...W];
const one = new Map();
for (const r of all) if (!one.has(r.codes)) one.set(r.codes, r);
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const out = { about: 'W probes: two or three syllables typed as one key; "joins" = the composer wrote exactly the concatenation of its answers for each syllable alone.', byGroup: {}, mismatches7: [], mismatchesU: [] };
for (const r of W) {
  const units = unitsBy.get(r.codes);
  const g = r.tag.split(' ').slice(0, 2).join(' ');
  const G = out.byGroup[g] || (out.byGroup[g] = { probes: 0, joins7: 0, joinsU: 0, no7: 0, unitMissing: 0 });
  G.probes++;
  const parts = units.map(u => one.get(u.map(h).join(' ')));
  if (parts.some(p => !p)) { G.unitMissing++; continue; }
  const want7 = parts.map(p => p.bytes || '').filter(Boolean).join(' ');
  const wantU = parts.map(p => p.u || '').join('');
  if (r.bytes == null) G.no7++;
  if ((r.bytes || '') === want7) G.joins7++; else if (out.mismatches7.length < 400) out.mismatches7.push({ codes: r.codes, tag: r.tag, got7: r.bytes, joined7: want7, parts7: parts.map(p => p.bytes), u: r.u, partsU: parts.map(p => p.u) });
  if ((r.u || '') === wantU) G.joinsU++; else if (out.mismatchesU.length < 400) out.mismatchesU.push({ codes: r.codes, tag: r.tag, u: r.u, joinedU: wantU, partsU: parts.map(p => p.u), got7: r.bytes });
}
// the mismatches grouped by the shape of the units
const shape = t => t.replace(/[0-9A-F]{2}/g, 'X');
out.mismatch7Shapes = {}; for (const m of out.mismatches7) out.mismatch7Shapes[shape(m.tag)] = (out.mismatch7Shapes[shape(m.tag)] || 0) + 1;
out.mismatchUShapes = {}; for (const m of out.mismatchesU) out.mismatchUShapes[shape(m.tag)] = (out.mismatchUShapes[shape(m.tag)] || 0) + 1;
fs.writeFileSync(D + '/wjoin.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out.byGroup, null, 1));
console.log('7 mismatches', out.mismatches7.length, JSON.stringify(out.mismatch7Shapes, null, 1));
console.log(out.mismatches7.slice(0, 25).map(m => `${m.codes} ${m.tag} | 7 ${m.got7} joined ${m.joined7} | U ${JSON.stringify(m.u)} parts ${JSON.stringify(m.partsU)}`).join('\n'));
console.log('U mismatches', out.mismatchesU.length, JSON.stringify(out.mismatchUShapes, null, 1));
console.log(out.mismatchesU.slice(0, 25).map(m => `${m.codes} ${m.tag} | U ${JSON.stringify(m.u)} joined ${JSON.stringify(m.joinedU)} | 7 ${m.got7}`).join('\n'));
