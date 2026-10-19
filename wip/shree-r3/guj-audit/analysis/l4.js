// L4: what the reverse tables read from Shree's own bytes and from TextSoft's own bytes.
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const o = JSON.parse(fs.readFileSync(path.join(A, 'results', 'l4-conv-out.json'), 'utf8'));
const U = s => [...(s || '')].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const clean = u => { if (u == null) return null; if (u.length > 1 && (u[0] === ' ' || u[0] === '.') && /[ઁ-ઃ઼ા-્ૢૣ]/.test(u[1])) return u.slice(1); if (u.length > 1 && u[0] === ' ') return u.slice(1); return u; };
const stats = {}, wrong = {};
const add = (src, ok, item) => { const s = stats[src] = stats[src] || { reads: 0, same: 0, wrong: 0 }; s.reads++; if (ok) s.same++; else { s.wrong++; (wrong[src] = wrong[src] || []).push(item); } };
const leak = /[﷐-﷟-]/;
for (const r of o) {
  const t = JSON.parse(r.Tag);
  const got = r.out;
  let want = t.src === 'ts-l3' ? t.u : clean(t.u);
  if (t.src === 'named') { add('named', true, null); (wrong.named = wrong.named || []).push({ why: t.why, hex: t.hex, reads: got, readsHex: U(got), unmapped: r.unmapped }); continue; }
  if (want == null) continue;
  if (/^[A-Za-z]{2}$/.test(want)) continue;
  const ok = got === want;
  // equivalences that are not reading errors: ZWJ/ZWNJ the composer adds to a lone half form or halant
  const loose = !ok && got.replace(/[‌‍]/g, '') === want.replace(/[‌‍]/g, '');
  add(t.src + (r.Dir === 'rev456' ? '-456' : '') + (t.src === 'truth' && !t.pref ? '-alt' : ''), ok || loose, { dir: r.Dir, hex: t.hex, want, wantHex: U(want), got, gotHex: U(got), leak: leak.test(got), unmapped: r.unmapped, onlyJoiner: loose, kb: t.kb, key: t.key });
}
// group the wrong readings by what went wrong
const groups = {};
for (const [src, list] of Object.entries(wrong)) {
  if (src === 'named') continue;
  for (const w of list) {
    let g;
    if (w.leak) g = 'private marker leaks (U+FDDx/U+E0xx) into the text';
    else if (w.unmapped && w.unmapped.length) g = 'byte(s) not read at all: ' + w.unmapped.map(u => U(u)).join(',');
    else if (/્‌/.test(w.want) && !/્‌/.test(w.got)) g = 'ZWNJ halant inside a word lost';
    else if (w.want.replace(/્/g, '') === w.got.replace(/્/g, '')) g = 'virama lost or added';
    else g = 'other';
    const key = src + ' | ' + g;
    (groups[key] = groups[key] || []).push(w);
  }
}
const out = { about: 'L4 read-back: bytes Shree writes (truth spellings, per-key answers, code sweep, code runs, joined truth words) and bytes TextSoft writes (L3 characters between two letters) through shree-guj7-to-unicode / shree-guj456-to-unicode.', stats, named: wrong.named, groups: Object.fromEntries(Object.entries(groups).map(([k, v]) => [k, { count: v.length, examples: v.slice(0, 25) }])) };
fs.writeFileSync(path.join(A, 'results', 'l4-readback.json'), JSON.stringify(out, null, 1));
console.log(JSON.stringify(stats, null, 0));
for (const [k, v] of Object.entries(groups)) { console.log('\n##', k, v.length); for (const w of v.slice(0, 8)) console.log('  ', w.hex, '| want', JSON.stringify(w.want), w.wantHex, '| got', JSON.stringify(w.got), w.gotHex, w.kb ? w.kb + ' ' + w.key : ''); }
console.log('\n## named');
for (const n of wrong.named) console.log('  ', n.hex.padEnd(16), n.why.padEnd(40), JSON.stringify(n.reads), n.readsHex, n.unmapped && n.unmapped.length ? 'UNMAPPED ' + JSON.stringify(n.unmapped) : '');
