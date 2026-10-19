// Family check for Punjabi: read the (3,1) cmap of every installed Shree-Lipi 7 (WPUN700x) and 4/5/6 (S09xx0F2) face,
// compare code by code: code sets, glyph ids, advance widths and raw outline bytes (same-design pairs), and outline
// geometry normalised (cross-design). Writes fonts/family-check.json and sheets of every code in every face.
const { load, sheet } = require('./ttf');
const fs = require('fs');
const crypto = require('crypto');
const F = 'C:/Windows/Fonts/';
const seven = ['WPUN7001.TTF', 'WPUN7002.TTF'];
const four = ['S09500F2.TTF', 'S09510F2.TTF', 'S09520F2.TTF', 'S09550F2.TTF', 'S09560F2.TTF'];
const fonts = [...seven, ...four].map(f => load(F + f));
const h2 = n => n.toString(16).toUpperCase().padStart(2, '0');
const out = { method: 'ttf.js: (3,1) cmap format 4 of each TTF in C:/Windows/Fonts, code points folded back to bytes as Windows-1252 maps them; per code: glyph id, advance (hmtx), sha1 of the raw glyf bytes, and a geometry signature (outline points scaled to the advance box). Same design = same 4-digit number (0951: WPUN7001 vs S09510F2, 0952: WPUN7002 vs S09520F2).', faces: {}, pairs: {}, codeSets: {} };
for (const f of fonts) out.faces[f.file.replace(F, '')] = { name: f.name, cmap: f.which, upem: f.upem, glyphs: f.numGlyphs, codes: f.bytes.size, codeList: [...f.bytes.keys()].sort((a, b) => a - b).map(h2).join(' ') };
const sha = (f, b) => { const e = f.bytes.get(b); return e ? crypto.createHash('sha1').update(f.rawGlyph(e.glyph)).digest('hex').slice(0, 12) : null; };
function cmp(a, b) {
  const all = new Set([...a.bytes.keys(), ...b.bytes.keys()]);
  const res = { identicalOutlineAndAdvance: 0, differ: {} };
  for (const c of [...all].sort((x, y) => x - y)) {
    const ea = a.bytes.get(c), eb = b.bytes.get(c);
    if (!ea || !eb) { res.differ[h2(c)] = `only in ${ea ? a.file.replace(F, '') : b.file.replace(F, '')}`; continue; }
    const sa = sha(a, c), sb = sha(b, c), wa = a.adv(ea.glyph), wb = b.adv(eb.glyph);
    if (sa === sb && wa === wb) res.identicalOutlineAndAdvance++;
    else res.differ[h2(c)] = `outline ${sa === sb ? 'same' : 'differs'}, advance ${wa}/${wb}, glyph ${ea.glyph}/${eb.glyph}`;
  }
  return res;
}
out.pairs['0951 WPUN7001 vs S09510F2'] = cmp(fonts[0], fonts[3]);
out.pairs['0952 WPUN7002 vs S09520F2'] = cmp(fonts[1], fonts[4]);
out.pairs['7: WPUN7001 vs WPUN7002 (two designs)'] = { sameCodeSet: fonts[0].bytes.size === fonts[1].bytes.size && [...fonts[0].bytes.keys()].every(c => fonts[1].bytes.has(c)), sameGlyphIds: [...fonts[0].bytes].every(([c, e]) => fonts[1].bytes.get(c)?.glyph === e.glyph) };
// all 4/5/6 faces against S09510F2: code set and glyph id per code (the layout), designs differ
for (const f of fonts.slice(2)) {
  const ref = fonts[3];
  const only = [...new Set([...f.bytes.keys(), ...ref.bytes.keys()])].filter(c => !f.bytes.has(c) || !ref.bytes.has(c)).map(c => h2(c) + (f.bytes.has(c) ? '+' : '-'));
  const gdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).glyph !== e.glyph).map(([c]) => h2(c));
  out.codeSets[f.file.replace(F, '') + ' vs S09510F2'] = { codes: f.bytes.size, codesNotInBoth: only, glyphIdDiffers: gdiff };
}
for (const f of fonts.slice(0, 2)) {
  const ref = fonts[0];
  const only = [...new Set([...f.bytes.keys(), ...ref.bytes.keys()])].filter(c => !f.bytes.has(c) || !ref.bytes.has(c)).map(c => h2(c));
  out.codeSets[f.file.replace(F, '') + ' vs WPUN7001'] = { codes: f.bytes.size, codesNotInBoth: only };
}
// 7 vs 4/5/6 code sets across all faces
const s7 = new Set(fonts[0].bytes.keys()), s4 = new Set(fonts[3].bytes.keys());
out.codeSets['7 (WPUN7001) vs 4/5/6 (S09510F2)'] = { only7: [...s7].filter(c => !s4.has(c)).map(h2), only456: [...s4].filter(c => !s7.has(c)).map(h2) };
fs.mkdirSync(__dirname + '/fonts', { recursive: true });
fs.writeFileSync(__dirname + '/fonts/family-check.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out.faces, null, 0).slice(0, 3000));
console.log(JSON.stringify(out.pairs, null, 1));
console.log(JSON.stringify(out.codeSets, null, 1));
// sheets: every code of the union, one row per code, each face's glyph side by side (by glyph index)
const union = [...new Set(fonts.flatMap(f => [...f.bytes.keys()]))].sort((a, b) => a - b);
const rows = union.map(c => ({ label: h2(c), runs: fonts.map(f => ({ font: f, glyphs: f.bytes.has(c) ? [f.bytes.get(c).glyph] : [] })) }));
for (let i = 0; i < rows.length; i += 40) sheet(__dirname + `/fonts/codes-${h2(union[i])}.png`, rows.slice(i, i + 40), { px: 40, cellW: 520, cols: 2 });
console.log('sheets', Math.ceil(rows.length / 40), 'codes in union', union.length);
