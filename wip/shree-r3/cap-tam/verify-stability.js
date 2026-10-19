// Run A vs run B (separate composer processes): byte-identical after one normalisation - a unit count outside 0..60
// is the composer returning garbage for "no answer" (the 7 composer's return value for the lone link code 64 is a
// different large number every process: 4902400 / 5458176), so such counts are compared as "none".
// node verify-stability.js capp capp-b capvo capvo-b ...   -> verify-stability.json
const fs = require('fs');
const path = require('path');
const D = __dirname;
const norm = t => t.split('\n').map(l => { if (!l || l[0] === '#') return l; const p = l.split('\t'); const u = Number(p[2]), x = Number(p[4]); if (!(u >= 0 && u <= 60)) { p[2] = 'none'; p[3] = ''; } if (!(x >= 0 && x <= 60)) { p[4] = 'none'; p[5] = ''; } return p.join('\t'); }).join('\n');
const out = {};
const args = process.argv.slice(2);
for (let i = 0; i < args.length; i += 2) {
  const [a, b] = [args[i], args[i + 1]];
  const fa = fs.readdirSync(path.join(D, a)).filter(f => f.endsWith('.tsv'));
  const fb = new Set(fs.readdirSync(path.join(D, b)).filter(f => f.endsWith('.tsv')));
  let raw = 0, same = 0; const diff = [], missing = [], unstable = [], notLoaded = [], garbage = new Set();
  for (const f of fa) {
    const x = fs.readFileSync(path.join(D, a, f), 'utf8');
    if (!/^#stable\t1/m.test(x)) unstable.push(a + '/' + f);
    if (!/^#loaded\t1/m.test(x)) notLoaded.push(a + '/' + f);
    if (!fb.has(f)) { missing.push(f); continue; }
    const y = fs.readFileSync(path.join(D, b, f), 'utf8');
    if (!/^#stable\t1/m.test(y)) unstable.push(b + '/' + f);
    if (x === y) raw++;
    if (norm(x) === norm(y)) same++; else diff.push(f);
    for (const l of x.split('\n')) { const p = l.split('\t'); if (p.length > 5 && !(Number(p[4]) >= 0 && Number(p[4]) <= 60)) garbage.add('7:' + p[1]); if (p.length > 5 && !(Number(p[2]) >= 0 && Number(p[2]) <= 60)) garbage.add('U:' + p[1]); }
  }
  out[a + ' vs ' + b] = { files: fa.length, rawIdentical: raw, identicalAfterNormalisingGarbageCounts: same, differ: diff, missingInB: missing, unstable, notLoaded, keysWithGarbageCount: [...garbage].sort().join(' ') };
}
const file = path.join(D, 'verify-stability.json');
const prev = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
fs.writeFileSync(file, JSON.stringify({ ...prev, ...out }, null, 1));
console.log(JSON.stringify(out, null, 1));
