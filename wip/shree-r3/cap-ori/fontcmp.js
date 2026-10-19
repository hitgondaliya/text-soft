// Family check for Odia: read the (3,1) cmap of every installed Shree-Lipi 7 (WORI700x, S06xx892) and 4/5/6 (S06xx0F2)
// face, compare code by code: code sets, the code points each byte sits on, glyph ids, advance widths and raw outline
// bytes (same-design pairs). Writes fonts/family-check.json and sheets of every code in every face (by glyph index).
const { load, sheet } = require('./ttf');
const fs = require('fs');
const crypto = require('crypto');
const F = 'C:/Windows/Fonts/';
const seven = ['WORI7001.TTF', 'WORI7002.TTF', 'S0600892.TTF', 'S0601892.TTF', 'S0602892.TTF', 'S0603892.TTF', 'S0604892.TTF'];
const four = ['S06000F2.TTF', 'S06010F2.TTF', 'S06020F2.TTF', 'S06030F2.TTF', 'S06040F2.TTF'];
const fonts = [...seven, ...four].map(f => load(F + f));
const by = Object.fromEntries(fonts.map(f => [f.file.replace(F, ''), f]));
const h2 = n => n.toString(16).toUpperCase().padStart(2, '0');
const out = { method: 'ttf.js: (3,1) cmap format 4 of each TTF in C:/Windows/Fonts, code points folded back to bytes as Windows-1252 maps them (U+0000-00FF = the byte itself, the 27 cp1252 code points of 80-9F = their byte); per code: glyph id, advance (hmtx), sha1 of the raw glyf bytes. Same design = same 4-digit number in the family name.', faces: {}, pairs: {}, codeSets: {}, codePointsOf80to9F: {} };
for (const f of fonts) out.faces[f.file.replace(F, '')] = { name: f.name, cmap: f.which, upem: f.upem, glyphs: f.numGlyphs, codes: f.bytes.size, codeList: [...f.bytes.keys()].sort((a, b) => a - b).map(h2).join(' ') };
// where does each byte 80-9F (and A0-FF that has a cp1252 twin) sit: its own C1 code point, its cp1252 code point, or both
for (const f of [by['WORI7001.TTF'], by['S06030F2.TTF']]) {
  const r = {};
  for (let b = 0x80; b <= 0x9F; b++) {
    const cps = [...f.map.keys()].filter(cp => (cp === b) || (cp >= 0x100 && f.bytes.get(b) && f.bytes.get(b).cp === cp));
    const at = [];
    if (f.map.has(b)) at.push('U+' + b.toString(16).toUpperCase().padStart(4, '0'));
    for (const [cp, g] of f.map) if (cp >= 0x100 && f.bytes.get(b) && f.bytes.get(b).glyph === g && cp !== b) at.push('U+' + cp.toString(16).toUpperCase().padStart(4, '0'));
    r[h2(b)] = at.length ? at.join(' ') : 'no glyph';
  }
  out.codePointsOf80to9F[f.file.replace(F, '')] = r;
}
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
const P = (x, y) => { out.pairs[`${x} vs ${y}`] = cmp(by[x], by[y]); };
P('S0600892.TTF', 'S06000F2.TTF'); P('S0601892.TTF', 'S06010F2.TTF'); P('S0602892.TTF', 'S06020F2.TTF');
P('S0603892.TTF', 'S06030F2.TTF'); P('WORI7001.TTF', 'S06030F2.TTF'); P('WORI7001.TTF', 'S0603892.TTF');
P('S0604892.TTF', 'S06040F2.TTF'); P('WORI7002.TTF', 'S06040F2.TTF'); P('WORI7002.TTF', 'S0604892.TTF');
// every face against S06000F2: code set and glyph id per code (the layout); designs differ
const ref = by['S06000F2.TTF'];
for (const f of fonts) {
  const only = [...new Set([...f.bytes.keys(), ...ref.bytes.keys()])].filter(c => !f.bytes.has(c) || !ref.bytes.has(c)).map(c => h2(c) + (f.bytes.has(c) ? '+' : '-'));
  const gdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).glyph !== e.glyph).map(([c]) => h2(c));
  const cpdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).cp !== e.cp).map(([c, e]) => h2(c) + ' U+' + e.cp.toString(16) + '/U+' + ref.bytes.get(c).cp.toString(16));
  const advdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.adv(ref.bytes.get(c).glyph) !== f.adv(e.glyph)).length;
  out.codeSets[f.file.replace(F, '') + ' vs S06000F2'] = { codes: f.bytes.size, codesNotInBoth: only, glyphIdDiffers: gdiff, codePointDiffers: cpdiff, advanceDiffers: advdiff };
}
fs.mkdirSync(__dirname + '/fonts', { recursive: true });
fs.writeFileSync(__dirname + '/fonts/family-check.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out.pairs, null, 1));
console.log(JSON.stringify(out.codeSets, null, 1));
console.log(JSON.stringify(out.codePointsOf80to9F, null, 0));
// sheets: every code of the union, one row per code, each face's glyph side by side (by glyph index)
const union = [...new Set(fonts.flatMap(f => [...f.bytes.keys()]))].sort((a, b) => a - b);
const rows = union.map(c => ({ label: h2(c), runs: fonts.map(f => ({ font: f, glyphs: f.bytes.has(c) ? [f.bytes.get(c).glyph] : [] })) }));
for (let i = 0; i < rows.length; i += 40) sheet(__dirname + `/fonts/codes-${h2(union[i])}.png`, rows.slice(i, i + 40), { px: 36, cellW: 760, cols: 1 });
console.log('sheets', Math.ceil(rows.length / 40), 'codes in union', union.length);
