// Group the forward failures (measured/forward-failures.tsv) by cause; one string can have several causes.
// -> measured/forward-failure-classes.json
const fs = require('fs');
const g = require('./kan7-groundtruth.json');
const T = g.truth;
const rows = fs.readFileSync(__dirname + '/measured/forward-failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => { const p = l.split('\t'); return { u: p[0], cat: p[2], ours: p[3], shree: p[4] }; });
const V = '್';
const isCons = c => /[ಕ-ಹೞ]/.test(c);
const classes = [
  ['A. ZWNJ/ZWJ written into the legacy text (halant C್‌ : Shree writes the virama glyph 85 and nothing for the joiner)', r => /U\+200[CD]/.test(r.ours)],
  ['B. reph (arkavattu): EC written right after the letter + head, before the vowel sign (Shree: the syllable, then EC) (ರ್ಕಾ: ours 50 DC EC DD | Shree 50 DD EC)', r => /^ರ್/.test(r.u) && isCons(r.u[2])],
  ['C. vowel sign on a conjunct written AFTER the subjoined form (Shree puts the sign on C1 and the subjoined form last: ಕ್ಕಾ ours 50 DC 52 DD | Shree 50 DD 52; ಕ್ಕಿ ours 50 DC 52 DF | Shree 51 52)', r => /್[ಕ-ಹೞ][ಾ-ೌ]/.test(r.u) && r.cat.includes('vowel sign')],
  ['D. ೀ ೇ ೋ after a conjunct: no pair, passed through as Kannada', r => /U\+0CC[07B]/.test(r.ours)],
  ['E. letter glyph choice: ಙ 9A (Shree 5F), ಫ B6 (Shree B4), ಱ 93 (Shree 94), ಘಿ 99 (Shree 5C), ಘೆ 4E (Shree 5B E6)', r => /ಙ|ಫ|ಱ|ಘ[ಿೆೇೈೊೋ]/.test(r.u) && (/(^| )(9A|B6|93|99|4E)( |$)/.test(r.ours))],
  ['F. ಕ್ಷ / ಜ್ಞ + sign: head kept or a different base', r => /ಕ್ಷ|ಜ್ಞ/.test(r.u)],
  ['G. nukta order (82 placed after the sign, or the head DC kept)', r => r.u.includes('಼')],
  ['H. reph on an independent vowel or a halant/ZW form', r => /^ರ್[ಅ-ಔೠೡ]/.test(r.u)],
  ['I. subjoined form differs (ಕ್ಫ ours 50 DC 23 | Shree 50 DC 96; ಕ್ಮ ours 50 DC 24 BE | Shree 50 DC BE; lone ್C pieces)', r => /್[ಕ-ಹೞ]/.test(r.u) && !/[ಾ-ೌ]/.test(r.u)],
  ['J. punctuation / symbols (‘ ’ 91 92 | Shree 22 27; — – 97 96 | Shree AC B6; ಽ 97 | Shree 98; ÷; ₹)', r => r.cat.startsWith('punctuation') || r.cat.startsWith('other letter') || r.u === '಼'],
];
const out = {}, firstOnly = {};
let other = [];
for (const r of rows) {
  let hit = false;
  for (const [name, f] of classes) if (f(r)) { if (!out[name]) out[name] = { count: 0, examples: [] }; out[name].count++; if (out[name].examples.length < 6) out[name].examples.push(`${r.u}: ours ${r.ours} | Shree ${r.shree}`); if (!hit) firstOnly[name] = (firstOnly[name] || 0) + 1; hit = true; }
  if (!hit) other.push(`${r.u}: ours ${r.ours} | Shree ${r.shree}`);
}
const res = { failures: rows.length, byCause: out, firstCause: firstOnly, other: { count: other.length, examples: other.slice(0, 40) } };
fs.writeFileSync(__dirname + '/measured/forward-failure-classes.json', JSON.stringify(res, null, 1));
console.log(JSON.stringify({ failures: rows.length, byCause: Object.fromEntries(Object.entries(out).map(([k, v]) => [k.slice(0, 70), v.count])), firstCause: Object.fromEntries(Object.entries(firstOnly).map(([k, v]) => [k.slice(0, 3), v])), other: other.length }, null, 1));
console.log(other.slice(0, 30).join('\n'));
