// Do two byte strings draw the same picture in a Malayalam 7 face? Rasterise both by glyph index (ttf.js: no character
// semantics, so the 0xAD trap cannot hide a glyph) at 40 px/em and compare the bitmaps pixel by pixel.
const { load, flatten, fill } = require('./ttf');
const fonts = {};
const font = f => fonts[f] || (fonts[f] = load(f));
const cache = new Map();
function raster(f, hex) {
  const k = f.file + '|' + hex; if (cache.has(k)) return cache.get(k);
  const px = 40, scale = px / f.upem;
  const bytes = hex.trim() ? hex.trim().split(/\s+/).map(h => parseInt(h, 16)) : [];
  let w = 8; for (const b of bytes) { const e = f.bytes.get(b); if (e) w += Math.max(0, f.adv(e.glyph) * scale); }
  const W = Math.ceil(w) + 40, H = px * 2;
  const img = new Uint8Array(W * H).fill(255);
  let x = 20, missing = 0;
  for (const b of bytes) { const e = f.bytes.get(b); if (!e) { missing++; continue; } fill(img, W, flatten(f.outline(e.glyph)), x, px * 1.4, scale); x += f.adv(e.glyph) * scale; }
  const r = { W, H, img, missing };
  cache.set(k, r); return r;
}
function same(a, b, file = 'C:/Windows/Fonts/S0501890.TTF') {
  if (a === b) return true;
  const f = font(file);
  const A = raster(f, a), B = raster(f, b);
  if (A.missing || B.missing) return false;
  // compare ink after trimming blank columns (advance rounding can shift by a pixel: allow 1px offset, <=1% of ink pixels)
  const ink = R => { const pts = []; for (let y = 0; y < R.H; y++) for (let x = 0; x < R.W; x++) if (R.img[y * R.W + x] === 0) pts.push([x, y]); return pts; };
  const pa = ink(A), pb = ink(B);
  if (!pa.length || !pb.length) return pa.length === pb.length;
  const minx = p => Math.min(...p.map(q => q[0]));
  const ox = minx(pa) - minx(pb);
  const setB = new Set(pb.map(([x, y]) => (x + ox) + ',' + y));
  let diff = 0; for (const [x, y] of pa) if (!setB.has(x + ',' + y) && !setB.has((x - 1) + ',' + y) && !setB.has((x + 1) + ',' + y)) diff++;
  const setA = new Set(pa.map(([x, y]) => x + ',' + y));
  for (const [x, y] of pb) { const X = x + ox; if (!setA.has(X + ',' + y) && !setA.has((X - 1) + ',' + y) && !setA.has((X + 1) + ',' + y)) diff++; }
  return diff <= Math.max(3, 0.01 * (pa.length + pb.length));
}
module.exports = { same, raster };
if (require.main === module) { const [a, b] = process.argv.slice(2); console.log(a, '|', b, '->', same(a, b)); }
