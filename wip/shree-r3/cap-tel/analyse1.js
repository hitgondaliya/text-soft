// Compare every probe's Unicode-composer answer with what its codes mean (expected.js); group by tag pattern.
const { expected, norm, key, fixRaVattu, C, SIGN, TAIL } = require('./expected');
const r = [...require('./capp/P-results.json'), ...require('./capr/R-results.json')];
const names = Object.values(C).sort((a, b) => b.length - a.length);
const pat = t => t.replace(/vendor-slot .*/, 'vendor-slot').split(' ').map(w => names.includes(w) ? 'C' : Object.values(SIGN).includes(w) ? 'S' : Object.values(TAIL).includes(w) ? 'T' : w).join(' ');
const g = new Map();
for (const x of r) {
  let u = x.u, b = x.bytes;
  if (/\+term$/.test(x.tag)) { if (u) u = u.replace(/ $/, ''); if (b) b = b.replace(/ ?20$/, ''); }
  const uf = key(fixRaVattu(u));
  const codes = x.codes.split(" ").map(h => parseInt(h, 16)); if (/\+term$/.test(x.tag)) codes.pop();
  const exp = expected(codes); //(' ').map(h => parseInt(h, 16)));
  const ok = uf != null && norm(uf) === norm(exp);
  const p = pat(x.tag);
  if (!g.has(p)) g.set(p, { n: 0, ok: 0, nob: 0, ex: [] });
  const s = g.get(p); s.n++; if (ok) s.ok++; if (!b) s.nob++;
  if (!ok && s.ex.length < 4) s.ex.push(`${x.tag} [${x.codes}] U=${JSON.stringify(x.uRaw)} fixed=${JSON.stringify(uf)} exp=${JSON.stringify(exp)} 7=${b}`);
}
for (const [p, s] of [...g].sort((a, b) => (b[1].n - b[1].ok) - (a[1].n - a[1].ok))) {
  if (s.ok === s.n && !process.argv.includes('--all')) continue;
  console.log(`${p}: ${s.ok}/${s.n} U==expected, no7 ${s.nob}`);
  for (const e of s.ex) console.log('    ' + e);
}
