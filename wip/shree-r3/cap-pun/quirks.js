// Where the Unicode composer wrote something other than the codes mean: grouped by shape. node quirks.js [results] [max]
const { expected, norm } = require('./expected');
const res = require(process.argv[2] || './capp/P-results.json');
const max = Number(process.argv[3] || 200);
const groups = new Map();
let n = 0, none = 0, nob = 0;
for (const r of res) {
  if (r.u == null) { none++; continue; }
  if (r.bytes == null) nob++;
  const codes = r.codes.split(' ').map(x => parseInt(x, 16));
  const exp = expected(codes);
  if (norm(r.u) === norm(exp)) continue;
  n++;
  const shape = r.tag.replace(/[ਕ-ਹੜ]਼?/g, 'C').replace(/vendor-slot .*/, 'vendor-slot');
  if (!groups.has(shape)) groups.set(shape, []);
  groups.get(shape).push(`${r.tag} [${r.codes}]: U ${JSON.stringify(r.u)} exp ${JSON.stringify(exp)} 7 ${r.bytes}`);
}
console.log('mismatch', n, 'no U answer', none, 'U but no 7 answer', nob);
for (const [s, l] of [...groups].sort((a, b) => b[1].length - a[1].length).slice(0, max)) console.log(String(l.length).padStart(5), s, '|', l.slice(0, 2).join(' || '));
