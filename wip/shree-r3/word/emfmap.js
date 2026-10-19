// Map EMF pictures to code points by their blue label; report the black run (font + codes) per code point.
const fs = require("fs");
function parse(file) {
  const b = fs.readFileSync(file);
  let o = 0, color = 0, cur = ""; const objs = {}; const outs = [];
  while (o + 8 <= b.length) {
    const t = b.readUInt32LE(o), n = b.readUInt32LE(o + 4); if (n < 8) break;
    if (t === 82) objs[b.readUInt32LE(o + 8)] = b.slice(o + 40, o + 104).toString("utf16le").replace(/\0.*$/, "");
    if (t === 37) { const h = b.readUInt32LE(o + 8); if (!(h & 0x80000000) && objs[h]) cur = objs[h]; }
    if (t === 24) color = b.readUInt32LE(o + 8) & 0xffffff;
    if (t === 84) { const x = b.readInt32LE(o + 36), nch = b.readUInt32LE(o + 44), off = b.readUInt32LE(o + 48); const ch = []; for (let i = 0; i < nch; i++) ch.push(b.readUInt16LE(o + off + 2 * i)); outs.push({ color, face: cur, x, ch }); }
    o += n;
  }
  const label = outs.filter(q => q.color === 0xff0000).flatMap(q => q.ch).map(c => String.fromCharCode(c)).join("").trim();
  const reds = outs.filter(q => q.color === 0xff).sort((a, b) => a.x - b.x);
  const xl = reds.length ? reds[0].x : -1e9, xr = reds.length ? reds[reds.length - 1].x : 1e9;
  const black = outs.filter(q => q.color === 0 && q.x > xl && q.x < xr).map(q => q.face + ":" + q.ch.map(c => c.toString(16).toUpperCase().padStart(4, "0")).join(" ")).join(" | ");
  return { label, black };
}
const [dirA, dirB] = process.argv.slice(2);
const mapOf = d => { const m = {}; for (const f of fs.readdirSync(d).filter(x => x.endsWith(".emf"))) { const r = parse(d + "/" + f); if (r.label.startsWith("U+")) m[r.label] = r.black || "(nothing)"; } return m; };
const A = mapOf(dirA), B = mapOf(dirB);
let same = 0; const diff = [];
for (const k of Object.keys(A)) { if (A[k] === B[k]) same++; else diff.push(`${k}: docx[${A[k]}] rtf[${B[k]}]`); }
console.log(`${dirB}: ${Object.keys(B).length} labelled pictures; same black run as the docx picture: ${same}/${Object.keys(A).length}`);
for (const d of diff) console.log("  " + d);
