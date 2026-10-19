// Group measured/reverse-failures.tsv (Shree's preferred bytes -> the shipped shree-tam7-to-unicode, real FontConverter)
// by cause -> measured/reverse-failure-groups.json
const fs = require('fs');
const D = __dirname;
const rows = fs.readFileSync(D + '/measured/reverse-failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => { const [bytes, u, ours, cps, cat] = l.split('\t'); return { bytes, u, ours, cps, cat }; });
const groups = {};
const why = r => {
  const b = r.bytes.split(' ');
  if (b.includes('9B') || b.includes('9F')) return 'ரி 9B / ரீ 9F not in the reverse table (read as › / Ÿ)';
  if (/ௌ/.test(r.u) && /ெள/.test(r.ours)) return 'F6 C CD read as Cெள, Shree means Cௌ (the encoding cannot tell them apart; vendor ௌ keys type ெ C ள)';
  if (/ஸ்ரீ|ஶ்ரீ/.test(r.u)) return 'ஸ்ரீ / ஶ்ரீ spelling';
  return 'other';
};
for (const r of rows) { const k = why(r); const g = groups[k] || (groups[k] = { n: 0, examples: [] }); g.n++; if (g.examples.length < 6) g.examples.push(`${r.bytes}: Shree ${r.u} | ours ${r.ours} [${r.cps}]`); }
fs.writeFileSync(D + '/measured/reverse-failure-groups.json', JSON.stringify({ total: rows.length, groups }, null, 1));
console.log(rows.length, JSON.stringify(groups, null, 1));
