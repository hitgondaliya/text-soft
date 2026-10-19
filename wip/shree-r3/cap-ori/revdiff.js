// Group the reverse failures by the part that differs (common prefix/suffix removed): "Shree X | ours Y".
const fs = require('fs');
const file = process.argv[2] || __dirname + '/measured/reverse-failures.tsv';
const max = Number(process.argv[3] || 40);
const lines = fs.readFileSync(file, 'utf8').trim().split('\n').slice(1).map(l => l.split('\t'));
const cp = s => [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const groups = new Map();
for (const [bytes, shree, ours, , cat] of lines) {
  const a = [...shree.normalize('NFD')], b = [...ours.normalize('NFD')];
  let i = 0; while (i < a.length && i < b.length && a[i] === b[i]) i++;
  let j = 0; while (j < a.length - i && j < b.length - i && a[a.length - 1 - j] === b[b.length - 1 - j]) j++;
  const key = `Shree [${a.slice(i, a.length - j).join('')}] (${cp(a.slice(i, a.length - j).join(''))}) | ours [${b.slice(i, b.length - j).join('')}] (${cp(b.slice(i, b.length - j).join(''))})`;
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push(`${bytes}: ${shree} -> ${ours}`);
}
const sorted = [...groups].sort((x, y) => y[1].length - x[1].length);
console.log('reverse failures', lines.length, 'diff groups', groups.size);
for (const [k, v] of sorted.slice(0, max)) console.log(String(v.length).padStart(5), k, '  e.g.', v.slice(0, 3).join('; '));
fs.writeFileSync(__dirname + '/measured/reverse-failure-groups.json', JSON.stringify(Object.fromEntries(sorted.map(([k, v]) => [k, { count: v.length, examples: v.slice(0, 10) }])), null, 1));
