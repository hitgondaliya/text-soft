// Classify every forward failure of the shipped table (measured/forward-failures.tsv) by cause; a string can have
// several causes. Writes measured/forward-failure-classes.json (count + 5 examples per cause) and prints a summary.
const fs = require('fs');
const lines = fs.readFileSync(__dirname + '/measured/forward-failures.tsv', 'utf8').trim().split('\n').slice(1).map(l => l.split('\t'));
const g = require('./ori7-groundtruth.json');
const total = Object.keys(g.truth).length;
const causes = new Map();
const add = (k, ex) => { if (!causes.has(k)) causes.set(k, []); causes.get(k).push(ex); };
let none = 0;
const ZW = /‌|‍/;
for (const [u, cps, cat, ours, pref, acc] of lines) {
  const ex = `${u} [${cps}]: ours ${ours} | Shree ${pref}`;
  const o = ours.split(' '), p = pref.split(' ');
  let hit = false;
  const H = k => { add(k, ex); hit = true; };
  if (/U\+200C|U\+200D/.test(ours)) H('A. ZWNJ/ZWJ written into the legacy text (U+200C/U+200D passed through; Shree writes no glyph for them)');
  if (/^ର୍[କ-ହଡ଼ଢ଼ୟ]/.test(u) || /‌ର୍[କ-ହ]/.test(u)) H('B. reph ର୍C written as ର + subjoined C (C0 + subjoined) or F6 before the letter; Shree writes the letter, its signs, then F6');
  if (/U\+0B48|U\+0B4C/.test(ours)) H('C. ୈ / ୌ passed through as Odia text (no pair for U+0B48 / U+0B4C)');
  if (/ୋ/.test(u) && !/U\+0B4B/.test(ours) && ours.indexOf('7B') > pref.indexOf('7B')) H('D. ୋ: the e-glyph 7B written AFTER the letter (the preRule moves only େ/ୈ); Shree writes 7B first');
  if (/େ/.test(u) && /୍ୟ|୍ର/.test(u) && ours.indexOf('7B') > pref.indexOf('7B') && pref.startsWith('7B')) H('E. େ after a ra/ya-phala cluster written after the cluster (the preRule cluster pattern stops at ୟ / does not take ୍ର)');
  const nFF = a => a.filter(x => x === 'FF').length;
  if (nFF(p) > nFF(o) && /୍[କ-ହୟ]/.test(u) && !/^ର୍/.test(u)) H('F. the spacer FF of the first letter dropped before a subjoined form (ours 4C 52 / Shree 4C FF 52 for କ୍ଖ)');
  if (/[ଡ଼ଢ଼]େ|[ଡ଼ଢ଼]ୋ|[ଡ଼ଢ଼]ୈ|[ଡ଼ଢ଼]ୌ/.test(u) && ours.indexOf('7B') > pref.indexOf('7B')) H('P. େ on ଡ଼ ଢ଼ (U+0B5C/0B5D) written after the letter (the preRule letter class [କ-ହ] leaves them out)');
  if (p.includes('26') && !o.includes('26')) H('G. the lowering spacer 26 missing before ୃ / ୍ର / ୍ under a subjoined form');
  if (p.some((b, i) => (b === 'EC' || b === 'EE') && !o.includes(b))) H('H. ୁ / ୂ after a subjoined form written high (EB/ED) where Shree uses the low forms EC/EE');
  if (o.includes('5B') && p.includes('9C')) H('I. ନ written 5B (Shree: 9C FF)');
  if (/୍ୟ/.test(u) && /FA DF/.test(ours) && !/FA DF/.test(pref)) H('J. ya-phala ୍ୟ written visible halant + ୟ (FA DF) where Shree writes BF');
  if (/୍ର/.test(u) && /FA C0 FF/.test(ours) && !/FA C0 FF/.test(pref)) H('K. ra-phala ୍ର written visible halant + ର (FA C0 FF) where Shree writes F7');
  if (o.includes('FD') && p.includes('B4')) H('L. ba-phala ୍ବ written FD where Shree writes B4');
  if (/[ଖଥଧ]ି/.test(u) && p.includes('23') && !o.includes('23')) H('M. ି on ଖ ଥ ଧ written E7/5D where Shree writes the hook 23');
  if (/କ୍ଷ/.test(u) && !ours.includes('E4') && pref.includes('E4')) H('N. କ୍ଷ inside a cluster split (50 FA CC) where Shree writes E4');
  if (/U\+20B9/.test(ours)) H('O. ₹ passed through (Shree 3C)');
  if (!hit) { none++; add('Z. other', ex); }
}
const out = {};
for (const [k, v] of [...causes].sort()) out[k] = { count: v.length, examples: v.slice(0, 6) };
fs.writeFileSync(__dirname + '/measured/forward-failure-classes.json', JSON.stringify({ failures: lines.length, truthStrings: total, classes: out }, null, 1));
console.log('failures', lines.length, 'of', total);
for (const [k, v] of Object.entries(out)) console.log(String(v.count).padStart(6), k, '| e.g.', v.examples.slice(0, 2).join(' ;; '));
