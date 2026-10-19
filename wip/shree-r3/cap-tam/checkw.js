// Consistency of the round-2 truth: W quirks, strings whose accepted set grew, and whether every W string's preferred
// spelling equals its syllables' preferred spellings joined (syllables taken from the round-1 truth).
const g = require('./tam7-groundtruth.json');
const r1 = require('./r1/tam7-groundtruth.json').truth;
const T = g.truth;
const wq = g.pairs.filter(p => / W[A-Z]{3} key/.test(p.source) && p.kind.startsWith('quirk:'));
console.log('W quirk rows', wq.length, JSON.stringify(wq.slice(0, 6)));
const grew = Object.keys(T).filter(u => r1[u] && T[u].accepted.length !== r1[u].accepted.length);
console.log('round-1 strings whose accepted set changed', grew.length, grew.map(u => u + ' ' + JSON.stringify(r1[u].accepted) + ' -> ' + JSON.stringify(T[u].accepted)).join(' | '));
const prefChanged = Object.keys(r1).filter(u => T[u] && T[u].preferred !== r1[u].preferred);
console.log('round-1 strings whose preferred changed', prefChanged.length, prefChanged.slice(0, 10).join(' '));
const lost = Object.keys(r1).filter(u => !T[u]);
console.log('round-1 strings lost', lost.length);
// syllable segmentation of a W string against the round-1 truth (greedy longest piece that ends at a syllable boundary)
const keys = Object.keys(r1).sort((a, b) => b.length - a.length);
const joins = c => /[ஂா-்ௗ‌‍]/.test(c);
let n = 0, same = 0, accOk = 0, nocover = 0; const bad = [];
for (const [u, t] of Object.entries(T)) {
  if (r1[u]) continue;
  n++;
  // dynamic programming: fewest round-1 pieces
  const best = new Array(u.length + 1).fill(null); best[0] = [];
  for (let i = 0; i < u.length; i++) { if (!best[i]) continue; for (let L = 1; L <= Math.min(12, u.length - i); L++) { const s = u.substr(i, L); if (!r1[s]) continue; if (i + L < u.length && joins(u[i + L])) continue; const c = [...best[i], s]; if (!best[i + L] || best[i + L].length > c.length) best[i + L] = c; } }
  const parts = best[u.length];
  if (!parts) { nocover++; continue; }
  const want = parts.map(p => r1[p].preferred).join(' ');
  if (t.preferred === want) same++; else if (bad.length < 15) bad.push(`${u} W ${t.preferred} | syllables ${parts.join('+')} ${want}`);
  if (t.accepted.includes(want)) accOk++;
}
console.log(`W strings ${n}: preferred = syllables' preferred joined ${same}, accepted includes it ${accOk}, not coverable by round-1 syllables ${nocover}`);
console.log(bad.join('\n'));
