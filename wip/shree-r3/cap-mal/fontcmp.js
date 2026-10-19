// Family check for Malayalam: read the (3,1) cmap of every installed Shree-Lipi 7 Malayalam face (WMAL7001/2 =
// Shree-Mal7-001/002, S0501890/S0502890/S0507890/S0508890/S0520890 = SHREE-MAL7-0501/0502/0507/0508/0520) and look for any
// 4/5/6 face (SHREE-MAL-xxxx = S05xx0F?.TTF): none is installed (registry + C:/Windows/Fonts + C:/Shree-Lipi nxt checked).
// Compare code by code: code sets, the code points each byte sits on, glyph ids, advance widths and raw outline bytes.
// Writes fonts/family-check.json and sheets of every code in every face (by glyph index). (adapted from ../cap-kan/fontcmp.js)
const { load, sheet } = require('./ttf');
const fs = require('fs');
const crypto = require('crypto');
const F = 'C:/Windows/Fonts/';
const seven = ['WMAL7001.TTF', 'WMAL7002.TTF', 'S0501890.TTF', 'S0502890.TTF', 'S0507890.TTF', 'S0508890.TTF', 'S0520890.TTF'];
const four = fs.readdirSync(F).filter(f => /^S05[0-4]\d0F\d\.TTF$/i.test(f)); // SHREE-MAL-05xx would be S05xx0Fn.TTF
const fonts = [...seven, ...four].map(f => load(F + f));
const by = Object.fromEntries(fonts.map(f => [f.file.replace(F, ''), f]));
const h2 = n => n.toString(16).toUpperCase().padStart(2, '0');
const out = { method: 'ttf.js: (3,1) cmap format 4 of each TTF in C:/Windows/Fonts, code points folded back to bytes as Windows-1252 maps them (U+0000-00FF = the byte itself, the 27 cp1252 code points of 80-9F = their byte, U+F0xx = the byte xx); per code: glyph id, advance (hmtx), sha1 of the raw glyf bytes. Faces: registry HKLM Fonts (Shree-Mal7-001 => WMAL7001.TTF, Shree-Mal7-002 => WMAL7002.TTF, SHREE-MAL7-0501 => S0501890.TTF, 0502 => S0502890, 0507 => S0507890, 0508 => S0508890, 0520 => S0520890). 4/5/6 faces found: ' + (four.length ? four.join(' ') : 'NONE (no SHREE-MAL-xxxx face in the registry, C:/Windows/Fonts or C:/Shree-Lipi nxt)'), faces: {}, pairs: {}, codeSets: {}, codePointsOf80to9F: {} };
for (const f of fonts) out.faces[f.file.replace(F, '')] = { name: f.name, cmap: f.which, upem: f.upem, glyphs: f.numGlyphs, codes: f.bytes.size, codeList: [...f.bytes.keys()].sort((a, b) => a - b).map(h2).join(' ') };
for (const f of [by['WMAL7001.TTF'], by['S0501890.TTF']]) {
  const r = {};
  for (let b = 0x80; b <= 0x9F; b++) {
    const at = [];
    if (f.map.has(b)) at.push('U+' + b.toString(16).toUpperCase().padStart(4, '0'));
    for (const [cp, g] of f.map) if (cp >= 0x100 && f.bytes.get(b) && f.bytes.get(b).glyph === g && cp !== b) at.push('U+' + cp.toString(16).toUpperCase().padStart(4, '0'));
    r[h2(b)] = at.length ? at.join(' ') : 'no glyph';
  }
  out.codePointsOf80to9F[f.file.replace(F, '')] = r;
}
const CP = [0x20AC, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x017D, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x017E, 0x0178];
out.otherCodePoints = Object.fromEntries(fonts.map(f => [f.file.replace(F, ''), [...f.map.keys()].filter(cp => cp >= 0x100 && !(cp >= 0xF000 && cp <= 0xF0FF) && !CP.includes(cp)).map(cp => 'U+' + cp.toString(16).toUpperCase()).join(' ')]));
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
P('WMAL7001.TTF', 'WMAL7002.TTF'); P('S0501890.TTF', 'S0502890.TTF'); P('S0507890.TTF', 'S0508890.TTF'); P('WMAL7001.TTF', 'S0501890.TTF'); P('S0501890.TTF', 'S0520890.TTF');
const ref = by['S0501890.TTF'];
for (const f of fonts) {
  const only = [...new Set([...f.bytes.keys(), ...ref.bytes.keys()])].filter(c => !f.bytes.has(c) || !ref.bytes.has(c)).sort((a, b) => a - b).map(c => h2(c) + (f.bytes.has(c) ? '+' : '-'));
  const gdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).glyph !== e.glyph).map(([c]) => h2(c));
  const cpdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.bytes.get(c).cp !== e.cp).map(([c, e]) => h2(c) + ' U+' + e.cp.toString(16) + '/U+' + ref.bytes.get(c).cp.toString(16));
  const advdiff = [...f.bytes].filter(([c, e]) => ref.bytes.has(c) && ref.adv(ref.bytes.get(c).glyph) !== f.adv(e.glyph)).length;
  out.codeSets[f.file.replace(F, '') + ' vs S0501890'] = { codes: f.bytes.size, codesNotInBoth: only.length > 40 ? only.length + ' codes' : only, glyphIdDiffers: gdiff.length > 40 ? gdiff.length + ' codes' : gdiff, codePointDiffers: cpdiff, advanceDiffers: advdiff };
}
out.advances = {};
for (const name of ['WMAL7001.TTF', 'S0501890.TTF']) { const f = by[name]; const r = {}; for (const c of [0x52, 0x53, 0x4E, 0x7C, 0x7E, 0x4C, 0x54, 0x55, 0x56, 0x4F, 0x50, 0x51, 0x5D, 0x7D, 0xE6, 0x9B, 0x9C, 0xC5, 0xC9, 0xD4, 0xD7, 0xDC, 0xF5, 0x08, 0x2D, 0x9E, 0xA3]) { const e = f.bytes.get(c); r[h2(c)] = e ? f.adv(e.glyph) : null; } out.advances[name] = r; }
fs.mkdirSync(__dirname + '/fonts', { recursive: true });
fs.writeFileSync(__dirname + '/fonts/family-check.json', JSON.stringify(out, null, 1));
console.log(out.method.slice(-160));
console.log(JSON.stringify(out.pairs, null, 1));
console.log(JSON.stringify(out.codeSets, null, 1));
console.log(JSON.stringify(out.otherCodePoints, null, 0));
console.log(JSON.stringify(Object.fromEntries(Object.entries(out.faces).map(([k, v]) => [k, v.name + ' ' + v.codes + ' codes ' + v.glyphs + ' glyphs']))));
const union = [...new Set(fonts.flatMap(f => [...f.bytes.keys()]))].sort((a, b) => a - b);
const rows = union.map(c => ({ label: h2(c), runs: fonts.map(f => ({ font: f, glyphs: f.bytes.has(c) ? [f.bytes.get(c).glyph] : [] })) }));
for (let i = 0; i < rows.length; i += 40) sheet(__dirname + `/fonts/codes-${h2(union[i])}.png`, rows.slice(i, i + 40), { px: 32, cellW: 700, cols: 1 });
console.log('sheets', Math.ceil(rows.length / 40), 'codes in union', union.length, union.map(h2).join(' '));
