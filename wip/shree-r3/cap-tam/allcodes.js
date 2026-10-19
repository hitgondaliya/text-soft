// node allcodes.js <font> <out.png> [first] [last]: every code of a face in one sheet (label = hex byte), by glyph index
const { load, sheet } = require('./ttf');
const [font, out, a, b] = process.argv.slice(2);
const f = load(font);
const lo = a ? parseInt(a, 16) : 0, hi = b ? parseInt(b, 16) : 255;
const rows = [...f.bytes.keys()].sort((x, y) => x - y).filter(c => c >= lo && c <= hi).map(c => ({ label: c.toString(16).toUpperCase().padStart(2, '0'), runs: [{ font: f, glyphs: [f.bytes.get(c).glyph] }] }));
sheet(out, rows, { px: 40, cellW: 110, cols: 10 });
console.log(rows.length);
