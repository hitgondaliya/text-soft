// expected (code meaning) vs the Unicode composer answer, grouped by the shape of the difference
const { expected, key } = require('./expected');
const R = [...require('./capp/P-results.json')];
let same = 0, diff = 0, noU = 0;
const groups = {};
for (const r of R) {
  if (r.u == null) { noU++; continue; }
  const e = expected(r.codes.split(' ').map(x => parseInt(x, 16)));
  if (key(r.u) === e) { same++; continue; }
  diff++;
  const shape = r.tag.replace(/[0-9A-F]{2}/g, 'X');
  (groups[shape] = groups[shape] || []).push(r.codes + ' U ' + JSON.stringify(r.u) + ' exp ' + JSON.stringify(e) + ' 7 ' + r.bytes);
}
console.log('same', same, 'diff', diff, 'noU', noU);
for (const [k, v] of Object.entries(groups).sort((a, b) => b[1].length - a[1].length)) console.log(v.length, k, '|', v.slice(0, 3).join(' || '));
