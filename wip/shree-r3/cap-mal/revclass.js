// Reverse failures of the shipped shree-mal7-to-unicode table (measured/reverse-failures.tsv), grouped by cause.
const fs = require('fs');
const dir = process.argv[2] || 'measured';
const L = fs.readFileSync(dir + '/reverse-failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => l.split('\t'));
const causes = [
  ['ra sign 4E read in visual order (്ര before the letter: 4E 57 -> ്രക, should be ക്ര)', (b, u, o) => /(^| )4E( |$)/.test(b) && /^്ര|[^\u0D15-\u0D39]്ര/.test(o)],
  ['subjoined forms 9B (ട) / 9C (ധ) / FF (doubling stroke) unmapped: read as › œ ÿ', (b, u, o) => /(^| )(9B|9C|FF)( |$)/.test(b)],
  ['ligature byte read as another conjunct (A8 ക്ക -> ന്പ ...)', (b, u, o) => /(^| )A8( |$)/.test(b)],
  ['C + 56 read as a chillu or the chandrakkala lost', (b, u, o) => / 56/.test(b) && /[ൺ-ൿ]/.test(o) && !/[ൺ-ൿ]/.test(u)],
  ['C + E6 (la sign) read as ല് / chillu', (b, u, o) => /(^| )E6( |$)/.test(b)],
  ['7C / 7E (ya / va sign) read wrongly', (b, u, o) => /(^| )(7C|7E)( |$)/.test(b)],
  ['A2 72 (ൻ + റ) read as ന്റ or other', (b, u, o) => /A2 72/.test(b)],
  ['3C (₹) read as <', (b, u, o) => /3C/.test(b)],
  ['26 (hyphen) read as &', (b, u, o) => /26/.test(b)],
  ['other', () => true],
];
const g = new Map();
for (const [b, u, o] of L) { let n = 'other'; for (const [k, f] of causes) if (f(b, u, o)) { n = k; break; } if (!g.has(n)) g.set(n, { count: 0, examples: [] }); const x = g.get(n); x.count++; if (x.examples.length < 8) x.examples.push(`${b}: Shree ${u} | ours ${o}`); }
const out = { failing: L.length, groups: Object.fromEntries([...g].sort((a, b) => b[1].count - a[1].count)) };
fs.writeFileSync(dir + '/reverse-failure-groups.json', JSON.stringify(out, null, 1));
for (const [k, v] of Object.entries(out.groups)) console.log(v.count, k, '\n   ', v.examples.slice(0, 4).join(' ;; '));
