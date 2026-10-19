// Within one face: which codes draw the SAME glyph (same glyph id, or identical outline bytes + advance)?
// Matters for the measure: a table byte that differs from Shree's byte may still draw the same thing.
// node dupglyph.js -> fonts/dup-glyphs.json
const { load } = require('./ttf');
const fs = require('fs');
const crypto = require('crypto');
const F = 'C:/Windows/Fonts/';
const NUM = ['0800', '0801', '0802', '0803', '1316', '3883'];
const files = ['WTAM7001.TTF', 'WTAM7002.TTF', ...NUM.map(n => `S${n}890.TTF`), ...NUM.map(n => `S${n}0F0.TTF`)];
const h2 = n => n.toString(16).toUpperCase().padStart(2, '0');
const out = { method: 'per face: codes grouped by identical (sha1 of raw glyf bytes + advance); groups of 2+ listed. An empty outline (space-like) is listed as its own group.', faces: {} };
for (const file of files) {
  const f = load(F + file);
  const groups = new Map();
  for (const [c, e] of f.bytes) {
    const raw = f.rawGlyph(e.glyph);
    const key = crypto.createHash('sha1').update(raw).digest('hex').slice(0, 12) + '/' + f.adv(e.glyph) + (raw.length ? '' : '/empty');
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(c);
  }
  const dups = [...groups.entries()].filter(([, v]) => v.length > 1).map(([k, v]) => ({ codes: v.sort((a, b) => a - b).map(h2).join(' '), key: k }));
  out.faces[file] = { name: f.name, duplicateGroups: dups };
}
fs.writeFileSync(__dirname + '/fonts/dup-glyphs.json', JSON.stringify(out, null, 1));
for (const [k, v] of Object.entries(out.faces)) console.log(k, v.name, v.duplicateGroups.map(g => '[' + g.codes + ']').join(' '));
