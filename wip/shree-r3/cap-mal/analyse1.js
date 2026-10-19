// Compare what the codes mean (expected.js) with the Unicode composer answer for every probe; group mismatches by tag shape.
// node analyse1.js [results.json ...]
const { expected, norm } = require('./expected');
const files = process.argv.slice(2).length ? process.argv.slice(2) : ['./capp/P-results.json'];
const r = files.flatMap(f => require(f.startsWith('.') || /^[A-Za-z]:/.test(f) ? f : './' + f));
const groups = new Map();
let same = 0, nou = 0;
for (const x of r) {
  if (x.u == null) { nou++; continue; }
  const e = expected(x.codes.split(' ').map(h => parseInt(h, 16)));
  if (norm(e) === norm(x.u)) { same++; continue; }
  const shape = x.tag.replace(/[ഀ-ൿ‌‍]+(\([0-9A-F]{2}\))?/g, 'X').replace(/vendor-slot \S+/, 'vendor-slot');
  if (!groups.has(shape)) groups.set(shape, []);
  groups.get(shape).push(x.codes + ' | U ' + JSON.stringify(x.u) + ' exp ' + JSON.stringify(e) + ' | ' + x.bytes + ' | ' + x.tag);
}
console.log('same', same, 'noU', nou, 'diff', r.length - same - nou);
for (const [k, v] of [...groups].sort((a, b) => b[1].length - a[1].length)) { console.log('##', k, v.length); for (const l of v.slice(0, 4)) console.log('   ', l); }
