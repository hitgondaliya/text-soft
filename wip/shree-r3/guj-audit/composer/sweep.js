// Code sweep: every Shree internal code -> what the Unicode composer (S8GujU64) and the
// Shree-Lipi 7 composer (S8Guj764) type for it. Sources: out/CA?.tsv (one code per key, layer 0)
// and layer 2 of every capture (the composer's built-in identity table: key char = code).
// Writes ../results/composer-codes.json
const fs = require('fs'), path = require('path');
const OUT = path.join(__dirname, 'out');
const man = JSON.parse(fs.readFileSync(path.join(__dirname, 'probe-manifest.json'), 'utf8'));
function readTsv(f) {
  const rows = [];
  for (const line of fs.readFileSync(f, 'utf8').split('\n')) {
    if (!line || line[0] === '#') continue;
    const [layer, key, uu, uh, lu, lh] = line.split('\t');
    const uUnits = Number(uu), lUnits = Number(lu);
    const u = (uUnits > 0 && uUnits < 64 && uh) ? uh.split(' ').map(h => String.fromCharCode(parseInt(h, 16))).join('') : null;
    const l = (lUnits > 0 && lUnits < 64 && lh) ? lh.trim() : null;
    rows.push({ layer: Number(layer), key: parseInt(key, 16), uUnits, u, uh: uh || '', lUnits, l });
  }
  return rows;
}
const codes = {};
for (const s of man.sweep) {
  const rows = readTsv(path.join(OUT, s.file.replace('.GUJ', '.tsv')));
  const r = rows.find(x => x.layer === 0 && x.key === s.key);
  codes[s.code.toString(16).toUpperCase().padStart(2, '0')] = r ? { u: r.u, uHex: r.uh, uUnits: r.uUnits, b7: r.l, b7Units: r.lUnits, src: s.file + ' key ' + String.fromCharCode(s.key) } : { u: null, b7: null, src: s.file + ' key ' + String.fromCharCode(s.key) + ' (no answer)' };
}
// layer 2 identity cross-check
const l2 = readTsv(path.join(OUT, 'VAH.tsv')).filter(r => r.layer === 2);
let agree = 0, disagree = [];
for (const r of l2) {
  const c = r.key.toString(16).toUpperCase().padStart(2, '0');
  const s = codes[c];
  if (s && s.uHex === r.uh && s.b7 === r.l) agree++; else disagree.push({ code: c, sweep: s && [s.uHex, s.b7], layer2: [r.uh, r.l] });
}
fs.writeFileSync(path.join(__dirname, '..', 'results', 'composer-codes.json'), JSON.stringify({
  about: 'Shree internal Gujarati code -> S8GujU64 Unicode answer (u, uHex) and S8Guj764 Shree-Lipi 7 byte answer (b7, hex). Captured 2026-10-18 by comp.exe from probe keyboards C:/Users/harde/AppData/Local/Temp/r3g/CA?.GUJ (one code per key). Codes 90-9F and B0-CF are conjunct-slot references, not codes. Composer answers about a key in isolation: a sign comes back on a carrier, two Latin letters (Lk, Hf) are placeholders.',
  layer2IdentityCheck: { agree, disagree },
  codes
}, null, 1));
console.log('codes', Object.keys(codes).length, 'layer2 identity agree', agree, 'disagree', disagree.length);
for (const [c, s] of Object.entries(codes)) if (s.u || s.b7) console.log(c, JSON.stringify(s.u), s.uHex, '|', s.b7);
