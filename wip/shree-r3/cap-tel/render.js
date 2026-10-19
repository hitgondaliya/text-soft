// node render.js <out.png> <font file> "<label>=<hex bytes>" ... : draw byte strings in a legacy font by glyph index
// (bytes with no glyph are drawn as nothing and listed). Default font C:/Windows/Fonts/WTEL7001.TTF.
const { load, sheet } = require('./ttf');
const [out, fontFile, ...items] = process.argv.slice(2);
const f = load(fontFile || 'C:/Windows/Fonts/WTEL7001.TTF');
const rows = items.map(it => {
  const [label, hex] = it.includes('=') ? it.split('=') : [it, it];
  const bytes = hex.trim().split(/\s+/).map(h => parseInt(h, 16));
  const missing = bytes.filter(b => !f.bytes.has(b));
  if (missing.length) console.log(label, 'no glyph for', missing.map(b => b.toString(16)).join(' '));
  return { label: label.replace(/[^0-9A-F +\-|]/gi, '').toUpperCase().slice(0, 40), runs: [{ font: f, glyphs: bytes.filter(b => f.bytes.has(b)).map(b => f.bytes.get(b).glyph) }] };
});
sheet(out, rows, { px: 64, cellW: 330, cols: 3 });
console.log('wrote', out, rows.length);
