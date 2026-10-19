// Group the forward failures by the part that differs (common prefix and suffix removed): "ours X | Shree Y".
// node faildiff.js [measured/forward-failures.tsv] [max]
const fs = require('fs');
const file = process.argv[2] || __dirname + '/measured/forward-failures.tsv';
const max = Number(process.argv[3] || 60);
const lines = fs.readFileSync(file, 'utf8').trim().split('\n').slice(1).map(l => l.split('\t'));
const groups = new Map();
for (const [u, cps, cat, ours, pref] of lines) {
  const a = ours.split(' '), b = pref.split(' ');
  let i = 0; while (i < a.length && i < b.length && a[i] === b[i]) i++;
  let j = 0; while (j < a.length - i && j < b.length - i && a[a.length - 1 - j] === b[b.length - 1 - j]) j++;
  const key = `ours [${a.slice(i, a.length - j).join(' ')}] | Shree [${b.slice(i, b.length - j).join(' ')}]`;
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push(`${u} (${ours} / ${pref})`);
}
const sorted = [...groups].sort((x, y) => y[1].length - x[1].length);
console.log('failures', lines.length, 'diff groups', groups.size);
for (const [k, v] of sorted.slice(0, max)) console.log(String(v.length).padStart(5), k, '  e.g.', v.slice(0, 3).join('; '));
fs.writeFileSync(__dirname + '/measured/forward-failure-groups.json', JSON.stringify(Object.fromEntries(sorted.map(([k, v]) => [k, { count: v.length, examples: v.slice(0, 10) }])), null, 1));
