// Per-layout Backspace statistics (Unicode and Shree-Lipi 7 profiles), excluding the harness artefact
// where the backspaced key was the second half of an escape run (Ctrl+Space x is one keystroke for the engine).
const o = require('../results/l2-words.json');
const per = {};
for (const w of o.words) {
  if (!w.plan) continue;
  const p = per[w.layout] = per[w.layout] || { words: 0, positions: 0, uniMore: 0, g7Wrong: 0, retypeOther: 0, examples: [] };
  p.words++; p.positions += w.plan.length;
  for (const b of (w.uni.backspace || [])) {
    const k = b.keysThenBackspace, prev = k.length > 1 ? k[k.length - 2] : '';
    if (prev === 'Ø' || prev === '¤') continue;
    if (b.kind === 'retype differs') { p.retypeOther++; continue; }
    p.uniMore++;
    if (p.examples.length < 3) p.examples.push(w.word + ' keys ' + JSON.stringify(k) + ' -> ' + b.shownAfterBackspace + ' (before that key: ' + b.shownBeforeThatKey + ')');
  }
  for (const b of (w.g7.backspace || [])) {
    const k = b.keysThenBackspace, prev = k.length > 1 ? k[k.length - 2] : '';
    if (prev === 'Ø' || prev === '¤' || b.kind === 'retype differs') continue;
    p.g7Wrong++;
  }
}
let T = { positions: 0, uni: 0, g7: 0 };
for (const [l, p] of Object.entries(per)) { T.positions += p.positions; T.uni += p.uniMore; T.g7 += p.g7Wrong; console.log(l.padEnd(13), 'words', p.words, 'BS positions', p.positions, 'wrong (uni)', p.uniMore, '(' + (100 * p.uniMore / p.positions).toFixed(1) + '%)', 'wrong (g7)', p.g7Wrong, 'retype-other', p.retypeOther, '|', p.examples[0] || ''); }
console.log(JSON.stringify(T));
require('fs').writeFileSync('../results/l2-backspace-per-layout.json', JSON.stringify({ totals: T, perLayout: per }, null, 1));
