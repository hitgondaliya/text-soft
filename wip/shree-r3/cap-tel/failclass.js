// Classify the forward failures (' + (process.argv[2] || 'measured') + '/forward-failures.tsv) by what differs, and count render-identical ones
// (bytes 93 = 7B and A0 = 90 draw the same outline with the same advance in every Telugu face: fonts/same-glyph-codes.json).
const fs = require('fs');
const rows = fs.readFileSync('' + (process.argv[2] || 'measured') + '/forward-failures.tsv', 'utf8').trim().split('\n').slice(1).map(l => l.split('\t'));
const eq = s => s.split(' ').map(b => b === '93' ? '7B' : b === 'A0' ? '90' : b).join(' ');
let renderSame = 0;
const sub = {}, ex = {};
const bump = (k, e) => { sub[k] = (sub[k] || 0) + 1; if (!ex[k]) ex[k] = []; if (ex[k].length < 4) ex[k].push(e); };
for (const [u, cps, cat, ours, pref, acc] of rows) {
  if (acc.split(',').some(a => eq(a) === eq(ours))) { renderSame++; continue; }
  const o = ours.split(' '), w = pref.split(' ');
  const e = `${u} ours ${ours} | Shree ${pref}`;
  const reasons = [];
  if (o.some(x => x.startsWith('U+'))) reasons.push('passes through ' + [...new Set(o.filter(x => x.startsWith('U+')))].join(','));
  if (/ై/.test(u) && o[0] === '4F') reasons.push('ై length mark 4F written BEFORE the letter (Shree: after the letter, after its spacer)');
  if (/ః/.test(u) && o.includes('5D') && w.includes('40')) reasons.push('ః = 5D (Shree writes 40)');
  if (/ఁ/.test(u) && o.includes('97') && w.includes('B6')) reasons.push('ఁ = 97 (Shree writes B6)');
  if (/్ర/.test(u) && o.includes('7B') && w.includes('7B') && o.indexOf('7B') > w.indexOf('7B')) reasons.push('ra-vattu hook after the letter (Shree: before)');
  if (/్ర/.test(u) && w[0] === '5B' && o[0] !== '5B') reasons.push('ra-vattu before ై: Shree uses 5B');
  if (/్[తట]్ర/.test(u) && (w.includes('88') || w.includes('89')) && !o.includes('88') && !o.includes('89')) reasons.push('్త్ర/్ట్ర ligature 88/89 missing');
  if (/[ృ]/.test(u) && w.includes('A0') && !o.includes('A0') && !o.includes('90')) reasons.push('ృ on a cluster: Shree A0 96 after the vattu');
  if (/క్ష/.test(u)) reasons.push('క్ష');
  const tal = ['E6', 'E8', 'B6', '40'];
  if (o.length === w.length) { const d = o.map((x, i) => x !== w[i] ? x + '→' + w[i] : null).filter(Boolean); if (d.length && d.every(x => tal.includes(x.split('→')[0]) && tal.includes(x.split('→')[1]))) reasons.push('talakattu variant ' + d.join(' ')); else if (!reasons.length && d.length <= 2) reasons.push('substitution ' + d.join(' ')); }
  if (!reasons.length) reasons.push('other');
  bump(reasons.join(' + '), e);
}
const out = { failing: rows.length, renderIdenticalToAnAcceptedSpelling: renderSame, classes: Object.entries(sub).sort((a, b) => b[1] - a[1]).map(([k, n]) => ({ n, why: k, examples: ex[k] })) };
fs.writeFileSync((process.argv[2] || 'measured') + '/forward-failure-classes.json', JSON.stringify(out, null, 1));
console.log('failing', rows.length, 'render-identical (93=7B, A0=90)', renderSame);
for (const c of out.classes.slice(0, 45)) console.log(String(c.n).padStart(6), c.why, '|', c.examples[0]);
