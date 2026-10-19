// Minimal TrueType reader: (3,1)/(3,0) cmap format 4, glyph outlines (glyf/loca, composites resolved), advances.
// Bytes of a legacy font: cmap code points folded back to bytes the way Windows-1252 maps them.
const fs = require('fs');
const zlib = require('zlib');
const CP1252 = { 0x20AC: 0x80, 0x201A: 0x82, 0x0192: 0x83, 0x201E: 0x84, 0x2026: 0x85, 0x2020: 0x86, 0x2021: 0x87, 0x02C6: 0x88, 0x2030: 0x89, 0x0160: 0x8A, 0x2039: 0x8B, 0x0152: 0x8C, 0x017D: 0x8E, 0x2018: 0x91, 0x2019: 0x92, 0x201C: 0x93, 0x201D: 0x94, 0x2022: 0x95, 0x2013: 0x96, 0x2014: 0x97, 0x02DC: 0x98, 0x2122: 0x99, 0x0161: 0x9A, 0x203A: 0x9B, 0x0153: 0x9C, 0x017E: 0x9E, 0x0178: 0x9F };

function load(file) {
  const b = fs.readFileSync(file);
  const n = b.readUInt16BE(4);
  const T = {};
  for (let i = 0; i < n; i++) { const o = 12 + i * 16; T[b.toString('latin1', o, o + 4)] = { off: b.readUInt32BE(o + 8), len: b.readUInt32BE(o + 12) }; }
  const head = T.head.off, upem = b.readUInt16BE(head + 18), locFmt = b.readInt16BE(head + 50);
  const numGlyphs = b.readUInt16BE(T.maxp.off + 4);
  const nH = b.readUInt16BE(T.hhea.off + 34);
  const adv = g => b.readUInt16BE(T.hmtx.off + 4 * Math.min(g, nH - 1));
  const loca = g => locFmt ? b.readUInt32BE(T.loca.off + 4 * g) : 2 * b.readUInt16BE(T.loca.off + 2 * g);
  // cmap
  const cm = T.cmap.off; const nt = b.readUInt16BE(cm + 2);
  let sub = null, which = null;
  for (let i = 0; i < nt; i++) { const p = b.readUInt16BE(cm + 4 + i * 8), e = b.readUInt16BE(cm + 6 + i * 8), o = b.readUInt32BE(cm + 8 + i * 8); if (p === 3 && (e === 1 || e === 0) && b.readUInt16BE(cm + o) === 4 && (!sub || e === 1)) { sub = cm + o; which = `${p},${e}`; } }
  const map = new Map(); // code point -> glyph
  if (sub) {
    const seg = b.readUInt16BE(sub + 6) / 2;
    const ends = sub + 14, starts = ends + seg * 2 + 2, deltas = starts + seg * 2, ranges = deltas + seg * 2;
    for (let s = 0; s < seg; s++) {
      const end = b.readUInt16BE(ends + s * 2), start = b.readUInt16BE(starts + s * 2), delta = b.readInt16BE(deltas + s * 2), ro = b.readUInt16BE(ranges + s * 2);
      for (let c = start; c <= end && c !== 0xFFFF; c++) {
        let g;
        if (ro === 0) g = (c + delta) & 0xFFFF;
        else { const gi = ranges + s * 2 + ro + (c - start) * 2; g = b.readUInt16BE(gi); if (g) g = (g + delta) & 0xFFFF; }
        if (g) map.set(c, g);
      }
    }
  }
  const bytes = new Map(); // byte -> { cp, glyph }
  for (const [cp, g] of map) {
    let byte = null;
    if (cp < 0x100) byte = cp; else if (CP1252[cp] !== undefined) byte = CP1252[cp]; else if (cp >= 0xF000 && cp <= 0xF0FF) byte = cp - 0xF000;
    if (byte !== null && !bytes.has(byte)) bytes.set(byte, { cp, glyph: g });
  }
  // outline: list of contours, each a list of {x,y,on}; composites flattened
  function outline(g, depth = 0) {
    const o0 = loca(g), o1 = loca(g + 1);
    if (o1 <= o0) return [];
    const p = T.glyf.off + o0;
    const nc = b.readInt16BE(p);
    if (nc >= 0) {
      const endPts = []; for (let i = 0; i < nc; i++) endPts.push(b.readUInt16BE(p + 10 + i * 2));
      const np = nc ? endPts[nc - 1] + 1 : 0;
      let q = p + 10 + nc * 2; const il = b.readUInt16BE(q); q += 2 + il;
      const flags = [];
      while (flags.length < np) { const f = b[q++]; flags.push(f); if (f & 8) { let r = b[q++]; while (r--) flags.push(f); } }
      const xs = [], ys = []; let x = 0, y = 0;
      for (const f of flags) { if (f & 2) { const d = b[q++]; x += (f & 16) ? d : -d; } else if (!(f & 16)) { x += b.readInt16BE(q); q += 2; } xs.push(x); }
      for (const f of flags) { if (f & 4) { const d = b[q++]; y += (f & 32) ? d : -d; } else if (!(f & 32)) { y += b.readInt16BE(q); q += 2; } ys.push(y); }
      const cs = []; let s = 0;
      for (const e of endPts) { const c = []; for (let i = s; i <= e; i++) c.push({ x: xs[i], y: ys[i], on: !!(flags[i] & 1) }); cs.push(c); s = e + 1; }
      return cs;
    }
    if (depth > 8) return [];
    const cs = []; let q = p + 10, more = true;
    while (more) {
      const fl = b.readUInt16BE(q), gi = b.readUInt16BE(q + 2); q += 4;
      let dx, dy;
      if (fl & 1) { dx = b.readInt16BE(q); dy = b.readInt16BE(q + 2); q += 4; } else { dx = b.readInt8(q); dy = b.readInt8(q + 1); q += 2; }
      let a = 1, bb = 0, c = 0, d = 1;
      if (fl & 8) { a = d = b.readInt16BE(q) / 16384; q += 2; } else if (fl & 0x40) { a = b.readInt16BE(q) / 16384; d = b.readInt16BE(q + 2) / 16384; q += 4; } else if (fl & 0x80) { a = b.readInt16BE(q) / 16384; bb = b.readInt16BE(q + 2) / 16384; c = b.readInt16BE(q + 4) / 16384; d = b.readInt16BE(q + 6) / 16384; q += 8; }
      for (const ct of outline(gi, depth + 1)) cs.push(ct.map(pt => ({ x: a * pt.x + c * pt.y + dx, y: bb * pt.x + d * pt.y + dy, on: pt.on })));
      more = !!(fl & 0x20);
    }
    return cs;
  }
  const rawGlyph = g => { const o0 = loca(g), o1 = loca(g + 1); return b.slice(T.glyf.off + o0, T.glyf.off + o1); };
  const name = (() => { const nm = T.name.off; const cnt = b.readUInt16BE(nm + 2), so = nm + b.readUInt16BE(nm + 4); let fam = null; for (let i = 0; i < cnt; i++) { const r = nm + 6 + i * 12; const pid = b.readUInt16BE(r), nid = b.readUInt16BE(r + 6), len = b.readUInt16BE(r + 8), off = b.readUInt16BE(r + 10); if (nid === 4 && pid === 3) { let s = ''; for (let k = 0; k < len; k += 2) s += String.fromCharCode(b.readUInt16BE(so + off + k)); fam = s; } } return fam; })();
  return { file, name, upem, numGlyphs, which, map, bytes, adv, outline, rawGlyph };
}

// Rasterise glyphs (by glyph index) into a PNG grid: cells = [{ label, glyphs: [[font, glyph]...] }] drawn left to right.
function flatten(cs) {
  const polys = [];
  for (const c of cs) {
    if (!c.length) continue;
    // expand implied on-curve points
    const pts = [];
    for (let i = 0; i < c.length; i++) { const p = c[i], n = c[(i + 1) % c.length]; pts.push(p); if (!p.on && !n.on) pts.push({ x: (p.x + n.x) / 2, y: (p.y + n.y) / 2, on: true }); }
    let s = pts.findIndex(p => p.on); if (s < 0) continue;
    const seq = pts.slice(s).concat(pts.slice(0, s)); seq.push(seq[0]);
    const poly = [[seq[0].x, seq[0].y]];
    for (let i = 1; i < seq.length; i++) {
      const p = seq[i];
      if (p.on) poly.push([p.x, p.y]);
      else { const p0 = seq[i - 1].on ? seq[i - 1] : { x: poly[poly.length - 1][0], y: poly[poly.length - 1][1] }; const p2 = seq[i + 1]; const x0 = poly[poly.length - 1][0], y0 = poly[poly.length - 1][1]; for (let t = 1; t <= 8; t++) { const u = t / 8; poly.push([(1 - u) * (1 - u) * x0 + 2 * u * (1 - u) * p.x + u * u * p2.x, (1 - u) * (1 - u) * y0 + 2 * u * (1 - u) * p.y + u * u * p2.y]); } i++; }
    }
    polys.push(poly);
  }
  return polys;
}
function fill(img, W, polys, ox, oy, scale) {
  // nonzero winding scanline fill, y up in font units -> image y down
  const edges = [];
  for (const p of polys) for (let i = 0; i + 1 < p.length; i++) { const a = p[i], c = p[i + 1]; edges.push([ox + a[0] * scale, oy - a[1] * scale, ox + c[0] * scale, oy - c[1] * scale]); }
  if (!edges.length) return;
  let ymin = Infinity, ymax = -Infinity; for (const e of edges) { ymin = Math.min(ymin, e[1], e[3]); ymax = Math.max(ymax, e[1], e[3]); }
  const H = img.length / W;
  for (let y = Math.max(0, Math.floor(ymin)); y <= Math.min(H - 1, Math.ceil(ymax)); y++) {
    const yc = y + 0.5; const xs = [];
    for (const [x0, y0, x1, y1] of edges) { if ((y0 <= yc && y1 > yc) || (y1 <= yc && y0 > yc)) { const t = (yc - y0) / (y1 - y0); xs.push([x0 + t * (x1 - x0), y1 > y0 ? 1 : -1]); } }
    xs.sort((a, b) => a[0] - b[0]);
    let w = 0;
    for (let i = 0; i + 1 < xs.length; i++) { w += xs[i][1]; if (w !== 0) for (let x = Math.max(0, Math.round(xs[i][0])); x < Math.min(W, Math.round(xs[i + 1][0])); x++) img[y * W + x] = 0; }
  }
}
// tiny 5x7 font for hex labels
const DIG = { '0': '01110100011001110101110011000101110', '1': '00100011000010000100001000010001110', '2': '01110100010000100010001000100011111', '3': '11111000100010000010000011000101110', '4': '00010001100101010010111110001000010', '5': '11111100001111000001000011000101110', '6': '00110010001000011110100011000101110', '7': '11111000010001000100010000100001000', '8': '01110100011000101110100011000101110', '9': '01110100011000101111000010001001100', 'A': '01110100011000111111100011000110001', 'B': '11110100011000111110100011000111110', 'C': '01110100011000010000100001000101110', 'D': '11100100101000110001100011001011100', 'E': '11111100001000011110100001000011111', 'F': '11111100001000011110100001000010000', ' ': '00000000000000000000000000000000000', '+': '00000001000010011111001000010000000', '-': '00000000000000011111000000000000000', '|': '00100001000010000100001000010000100' };
function text(img, W, s, x0, y0) { for (let k = 0; k < s.length; k++) { const d = DIG[s[k]] || DIG[' ']; for (let r = 0; r < 7; r++) for (let c = 0; c < 5; c++) if (d[r * 5 + c] === '1') { const x = x0 + k * 6 + c, y = y0 + r; if (x < W && y * W + x < img.length) img[y * W + x] = 0; } } }
function png(file, img, W, H) {
  const raw = Buffer.alloc((W + 1) * H);
  for (let y = 0; y < H; y++) { raw[y * (W + 1)] = 0; for (let x = 0; x < W; x++) raw[y * (W + 1) + 1 + x] = img[y * W + x]; }
  const crcT = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; crcT[n] = c >>> 0; }
  const crc = buf => { let c = 0xFFFFFFFF; for (const x of buf) c = crcT[(c ^ x) & 0xFF] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; };
  const chunk = (t, d) => { const len = Buffer.alloc(4); len.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([len, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 0; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
}
// rows: [{ label, runs: [{ font, glyphs: [g...] }] }]; each run drawn at its own x, glyphs advancing by their widths
function sheet(file, rows, { px = 48, cellW = 260, cols = 4 } = {}) {
  const rowH = Math.round(px * 1.9), W = cellW * cols, H = rowH * Math.ceil(rows.length / cols);
  const img = new Uint8Array(W * H).fill(255);
  rows.forEach((r, i) => {
    const cx = (i % cols) * cellW, cy = Math.floor(i / cols) * rowH;
    text(img, W, r.label, cx + 2, cy + 2);
    let x = cx + 6;
    for (const run of r.runs) {
      const scale = px / run.font.upem;
      for (const g of run.glyphs) { fill(img, W, flatten(run.font.outline(g)), x, cy + 12 + px * 1.15, scale); x += Math.max(4, run.font.adv(g) * scale); }
      x += 14;
    }
    for (let y = cy; y < cy + rowH; y++) img[y * W + cx] = 160;
    for (let x2 = cx; x2 < cx + cellW; x2++) img[(cy + rowH - 1) * W + x2] = 160;
  });
  png(file, img, W, H);
}
module.exports = { load, sheet, png, flatten, fill };
