// Third probe set (prefix S): ਠ/ਨ + subjoined + vowel sign + tail, where the canonical order made the 7 composer lose a
// glyph (ਠ੍ਹਾਂ = 53 BE A3 BE AA): every other order of the same codes, to see whether Shree can spell them at all.
const fs = require('fs');
const S = [];
const seen = new Set([...JSON.parse(fs.readFileSync(__dirname + '/seq-all.json', 'utf8')), ...JSON.parse(fs.readFileSync(__dirname + '/seq-halant.json', 'utf8'))].map(s => s.codes.join(',')));
const add = (codes, tag) => { const k = codes.join(','); if (codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase();
function perms(a) { if (a.length <= 1) return [a]; const out = []; a.forEach((x, i) => perms([...a.slice(0, i), ...a.slice(i + 1)]).forEach(p => out.push([x, ...p]))); return out; }
for (const C of [0x65, 0x49, 0x73])
  for (const sub of [0xD1, 0xD2, 0xD0, 0x4C])
    for (const v of [0x6B, 0x62, 0x6E, 0x68, 0x6C, 0x70, 0x74, 0x54])
      for (const t of [0x69, 0xD6, 0x40])
        for (const rest of perms([sub, v, t])) add([C, ...rest], `wide ${h(C)} | ${rest.map(h).join(' ')}`);
fs.writeFileSync(__dirname + '/seq-wide.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
