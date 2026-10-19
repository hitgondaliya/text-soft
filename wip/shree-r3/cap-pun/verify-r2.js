// Round-2 re-verification (no composer): run A vs run B byte-identical, #stable/#loaded flags, counts.
const fs = require('fs');
const path = require('path');
const D = __dirname;
const out = {};
for (const [a, b] of [['capvo', 'capvo-b'], ['capp', 'capp-b'], ['caph', 'caph-b'], ['capw', 'capw-b']]) {
  const fa = fs.readdirSync(path.join(D, a)).filter(f => f.endsWith('.tsv'));
  const fb = new Set(fs.readdirSync(path.join(D, b)).filter(f => f.endsWith('.tsv')));
  let same = 0, diff = [], missing = [], unstable = [], notLoaded = [];
  for (const f of fa) {
    const x = fs.readFileSync(path.join(D, a, f));
    if (!/#stable	1/.test(x.toString('latin1'))) unstable.push(a + '/' + f);
    if (!/#loaded	1/.test(x.toString('latin1'))) notLoaded.push(a + '/' + f);
    if (!fb.has(f)) { missing.push(f); continue; }
    const y = fs.readFileSync(path.join(D, b, f));
    if (!/#stable	1/.test(y.toString('latin1'))) unstable.push(b + '/' + f);
    if (Buffer.compare(x, y) === 0) same++; else diff.push(f);
  }
  out[a + ' vs ' + b] = { files: fa.length, identical: same, differ: diff, missingInB: missing, unstable, notLoaded };
}
for (const a of ['capx', 'capx2']) {
  const fa = fs.readdirSync(path.join(D, a)).filter(f => f.endsWith('.tsv'));
  out[a] = { files: fa.length, unstable: fa.filter(f => !/#stable	1/.test(fs.readFileSync(path.join(D, a, f), 'latin1'))) };
}
console.log(JSON.stringify(out, null, 1));
