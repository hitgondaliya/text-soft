// Family check for Telugu: read the (3,1) cmap of every installed Shree-Lipi 7 face (WTEL7001/2 + the tiny
// WTEL701T/702T "A" faces, M0900890/N0902890 Apoorva regular/bold, M1642890/N1644890 Asmitha regular/bold) and every
// 4/5/6 face (M09000F0/N09020F0, M16420F0/N16440F0), compare code by code: code sets, the code points each byte sits
// on, glyph ids, advance widths and raw outline bytes (same-design pairs). Writes fonts/family-check.json and sheets
// of every code in every face (by glyph index).
const { load, sheet } = require('./ttf');
const fs = require('fs');
const crypto = require('crypto');
const F = 'C:/Windows/Fonts/';
const seven = ['WTEL7001.TTF', 'WTEL7002.TTF', 'WTEL701T.TTF', 'WTEL702T.TTF', 'M0900890.TTF', 'N0902890.TTF', 'M1642890.TTF', 'N1644890.TTF'];
const four = ['M09000F0.TTF', 'N09020F0.TTF', 'M16420F0.TTF', 'N16440F0.TTF'];
const fonts = [...seven, ...four].map(f => load(F + f));
const by = Object.fromEntries(fonts.map(f => [f.file.replace(F, ''), f]));
const h2 = n => n.toString(16).toUpperCase().padStart(2, '0');
const out = { method: 'ttf.js: (3,1) cmap format 4 of each TTF in C:/Windows/Fonts, code points folded back to bytes as Windows-1252 maps them (U+0000-00FF = the byte itself, the 27 cp1252 code points of 80-9F = their byte, U+F0xx = the byte xx); per code: glyph id, advance (hmtx), sha1 of the raw glyf bytes. Same design = same 4-digit number in the family name; M = regular, N = bold of the same family (registry: "SHREE-TEL7-Apoorva-0900 Bold" => N0902890.TTF).', faces: {}, pairs: {}, codeSets: {}, codePointsOf80to9F: {} };
for (const f of fonts) out.faces[f.file.replace(F, '')] = { name: f.name, cmap: f.which, upem: f.upem, glyphs: f.numGlyphs, codes: f.bytes.size, codeList: [...f.bytes.keys()].sort((a, b) => a - b).map(h2).join(' ') };
for (const f of [by['WTEL7001.TTF'], by['M0900890.TTF'], by['M09000F0.TTF']]) {
  const r = {};
  for (let b = 0x80; b <= 0x9F; b++) {
    const at = [];
    if (f.map.has(b)) at.push('U+' + b.toString(16).toUpperCase().padStart(4, '0'));
    for (const [cp, g] of f.map) if (cp >= 0x100 && f.bytes.get(b) && f.bytes.get(b).glyph === g && cp !== b) at.push('U+' + cp.toString(16).toUpperCase().padStart(4, '0'));
    r[h2(b)] = at.length ? at.join(' ') : 'no glyph';
  }
  out.codePointsOf80to9F[f.file.replace(F, '')] = r;
}
// every code point of each face that is NOT a byte position (U+0100+ outside cp1252/F0xx)
out.otherCodePoints = Object.fromEntries(fonts.map(f => [f.file.replace(F, ''), [...f.map.keys()].filter(cp => cp >= 0x100 && !Object.prototype.hasOwnProperty.call({}, cp) && !(cp >= 0xF000 && cp <= 0xF0FF) && ![0x20AC, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x017D, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x017E, 0x0178].includes(cp)).map(cp => 'U+' + cp.toString(16).toUpperCase()).join(' ')]));
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
  res.differCount = Object.keys(res.differ).length;
  if (res.differCount > 40) res.differ = { first40: Object.fromEntries(Object.entries(res.differ).slice(0, 40)) };
  return res;
}
const P = (x, y) => { out.pairs[`${x} vs ${y}`] = cmp(by[x], by[y]); };
P('M0900890.TTF', 'M09000F0.TTF'); P('N0902890.TTF', 'N09020F0.TTF'); P('M1642890.TTF', 'M16420F0.TTF'); P('N1644890.TTF', 'N16440F0.TTF');
P('WTEL7001.TTF', 'M0900890.TTF'); P('WTEL7002.TTF', 'M1642890.TTF'); P('WTEL7001.TTF', 'WTEL7002.TTF'); P('WTEL701T.TTF', 'WTEL702T.TTF');
P('M0900890.TTF', 'N0902890.TTF');
// every face against M09000F0: code set and glyph id per code (the layout); designs differ
const ref = by['M09000F0.TTF'];
for (const f of fonts) {
  const only = [...new Set([...f.bytes.keys(), ...ref.bytes.keys()])].filter(c => !f.bytes.has(c) || !ref.bytes.has(c)).sort((a, b) => a - b).map(c => h2(c) + (f.bytes.has(c) ? '+' : '-'));
  const gdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).glyph !== e.glyph).map(([c]) => h2(c));
  const cpdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).cp !== e.cp).map(([c, e]) => h2(c) + ' U+' + e.cp.toString(16) + '/U+' + ref.bytes.get(c).cp.toString(16));
  const advdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.adv(ref.bytes.get(c).glyph) !== f.adv(e.glyph)).length;
  out.codeSets[f.file.replace(F, '') + ' vs M09000F0'] = { codes: f.bytes.size, codesNotInBoth: only.length > 30 ? only.length + ' codes' : only, glyphIdDiffers: gdiff.length > 30 ? gdiff.length + ' codes' : gdiff, codePointDiffers: cpdiff, advanceDiffers: advdiff };
}
fs.mkdirSync(__dirname + '/fonts', { recursive: true });
fs.writeFileSync(__dirname + '/fonts/family-check.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out.pairs, null, 1));
console.log(JSON.stringify(out.codeSets, null, 1));
console.log(JSON.stringify(out.otherCodePoints, null, 0));
console.log(JSON.stringify(out.faces['WTEL701T.TTF']));
// sheets: every code of the union, one row per code, each face's glyph side by side (by glyph index)
const union = [...new Set(fonts.flatMap(f => [...f.bytes.keys()]))].sort((a, b) => a - b);
const rows = union.map(c => ({ label: h2(c), runs: fonts.map(f => ({ font: f, glyphs: f.bytes.has(c) ? [f.bytes.get(c).glyph] : [] })) }));
for (let i = 0; i < rows.length; i += 40) sheet(__dirname + `/fonts/codes-${h2(union[i])}.png`, rows.slice(i, i + 40), { px: 36, cellW: 900, cols: 1 });
console.log('sheets', Math.ceil(rows.length / 40), 'codes in union', union.length);
