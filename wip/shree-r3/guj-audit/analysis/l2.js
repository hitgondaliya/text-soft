// L2 summary: backspace behaviour, typing order, unplanned words, lines.
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const o = JSON.parse(fs.readFileSync(path.join(A, 'results', 'l2-words.json'), 'utf8'));
const lines = JSON.parse(fs.readFileSync(path.join(A, 'results', 'l2-lines.json'), 'utf8'));
const bs = {}; const per = {};
let positions = { uni: 0, g7: 0, g456: 0 };
for (const w of o.words) {
  if (!w.plan) continue;
  for (const p of ['uni', 'g7', 'g456']) {
    positions[p] += w.plan.length;
    const pr = w[p]; if (!pr || !pr.backspace) continue;
    for (const b of pr.backspace) {
      const k = p + ' | ' + b.kind;
      (bs[k] = bs[k] || []).push({ layout: w.layout, word: w.word, plan: w.plan, ...b });
      const pk = w.layout + ' ' + p;
      per[pk] = per[pk] || {}; per[pk][b.kind] = (per[pk][b.kind] || 0) + 1;
    }
  }
}
console.log('backspace positions tested per profile', JSON.stringify(positions));
for (const [k, v] of Object.entries(bs)) {
  console.log('\n##', k, v.length);
  for (const b of v.slice(0, 10)) console.log('   ', b.layout, b.word, 'keys', JSON.stringify(b.keysThenBackspace), 'after BS', b.shownAfterBackspace, '| before key', b.shownBeforeThatKey, '| retyped', b.retypedRest, '| final', b.finalWithoutBackspace, '|', b.events);
}
// is "took back more" always a key that finished a chart run?
const rephFirst = o.words.filter(w => w.rephFirst);
console.log('\nreph struck first on keyboards that strike it after:', rephFirst.length, 'words; same word:', rephFirst.filter(w => w.rephFirst.sameWord).length);
for (const w of rephFirst.slice(0, 12)) console.log('   ', w.layout, w.word, JSON.stringify(w.rephFirst.keys), '->', w.rephFirst.unicode, w.rephFirst.unicodeHex, '| g7', w.rephFirst.g7, 'vs', w.rephFirst.g7OfWord);
console.log('\nunplanned:');
for (const s of o.summary) if (s.unplanned) console.log('   ', s.layout, s.unplanned, (s.unplannedWords || []).slice(0, 12).join(' '));
console.log('\nlines:');
for (const l of lines) {
  if (!l.g7) { console.log('   ', l.layout, JSON.stringify(l.line), 'NOT TYPED:', l.how.slice(-1)[0]); continue; }
  if (!l.unicodeOk || !l.g7.ok || !l.g456.ok) console.log('   ', l.layout, JSON.stringify(l.line), 'uni', JSON.stringify(l.unicode), l.unicodeOk, '| g7 typed', l.g7.typed, '| conv(line)', l.g7.converterOfLine, '| conv(typed uni)', l.g7.converterOfTypedUnicode);
}
fs.writeFileSync(path.join(A, 'results', 'l2-backspace.json'), JSON.stringify({ positions, perLayout: per, kinds: Object.fromEntries(Object.entries(bs).map(([k, v]) => [k, { count: v.length, examples: v.slice(0, 60) }])) }, null, 1));
