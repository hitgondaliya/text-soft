// Which codes does the 7 composer silently drop? A code is dropped in a probe when the same probe without that code
// was answered with exactly the same bytes while the Unicode composer's answers differ.
const { norm } = require('./expected');
const res = require('./capp/P-results.json');
const by = new Map(res.filter(r => r.bytes).map(r => [r.codes, r]));
const stats = new Map();
for (const r of res) {
  if (!r.bytes || r.u == null) continue;
  const c = r.codes.split(' ');
  for (let j = 0; j < c.length; j++) {
    const red = c.filter((_, k) => k !== j).join(' ');
    const o = by.get(red);
    if (!o || o.u == null) continue;
    const s = stats.get(c[j]) || { dropped: 0, kept: 0, ex: [] };
    if (o.bytes === r.bytes && norm(o.u) !== norm(r.u)) { s.dropped++; if (s.ex.length < 6) s.ex.push(`${r.tag} [${r.codes}] = ${r.bytes}`); }
    else s.kept++;
    stats.set(c[j], s);
  }
}
for (const [code, s] of [...stats].sort((a, b) => b[1].dropped - a[1].dropped)) if (s.dropped) console.log(code, 'dropped', s.dropped, 'kept', s.kept, '|', s.ex.join(' || '));
