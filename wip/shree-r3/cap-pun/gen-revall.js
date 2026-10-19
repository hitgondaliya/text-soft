// Round 2: the reverse table against EVERY spelling Shree types for a string (accepted + alternatives, any typing order),
// not only the preferred one, without rebuilding measure.exe: file k makes the k-th other spelling the "preferred" one
// (strings without a k-th spelling keep their preferred; those rows are dropped when joining). accepted = all spellings,
// so measure's SameText (which also accepts any text Shree writes the same bytes for) is unchanged.
const fs = require('fs');
const g = require('./pun7-groundtruth.json');
const all = {}, others = {};
for (const [u, v] of Object.entries(g.truth)) {
  all[u] = [...new Set([...v.accepted, ...v.alternatives])];
  others[u] = all[u].filter(s => s !== v.preferred);
}
const K = Math.max(...Object.values(others).map(o => o.length));
fs.mkdirSync('revall', { recursive: true });
const index = [];
for (let k = 0; k < K; k++) {
  const t = {};
  for (const [u, v] of Object.entries(g.truth)) {
    const s = others[u][k];
    t[u] = { preferred: s ?? v.preferred, accepted: all[u], alternatives: [] };
    if (s) index.push({ k, u, bytes: s, accepted: v.accepted.includes(s) });
  }
  fs.writeFileSync(`revall/gt-${k}.json`, JSON.stringify({ truth: t }));
}
fs.writeFileSync('revall/index.json', JSON.stringify(index));
console.log('files', K, 'other spellings', index.length, 'of them accepted', index.filter(x => x.accepted).length);
