// Classify revall/failures.tsv: which failures are inherent to a spelling with no virama glyph (link code 64: the bytes
// are the bytes of the same letters without the virama), and which are table bugs.
const fs = require('fs');
const L = fs.readFileSync('revall/failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => l.split('\t'));
const J = [['ਅਾ', 'ਆ'], ['ਅੈ', 'ਐ'], ['ਅੌ', 'ਔ'], ['ੲਿ', 'ਇ'], ['ੲੀ', 'ਈ'], ['ੲੇ', 'ਏ'], ['ੳੁ', 'ਉ'], ['ੳੂ', 'ਊ']];
const norm = s => { for (const [a, b] of J) s = s.split(a).join(b); return s.normalize('NFD'); };
const strip = s => norm(s).replace(/੍|[‌‍]/g, '');
// the i-sign: with no virama glyph, 71 C1 C2 is also the spelling of C1 + ਿ + C2 (ਖਿਕ), so moving ਿ is not checkable
const noI = s => strip(s).replace(/ਿ/g, '');
const cls = {};
const add = (c, r) => { cls[c] = cls[c] || { n: 0, ex: [] }; cls[c].n++; if (cls[c].ex.length < 6) cls[c].ex.push(`${r[0]}: Shree ${r[1]} | ours ${r[2]} (${r[3]})`); };
for (const r of L) {
  const [b, u, o] = r;
  const hasB5 = b.split(' ').includes('B5');
  if (/[¾®¯]/.test(o)) add('table bug: BE/AE/AF read as ¾ ® ¯', r);
  else if (!hasB5 && strip(u) === strip(o)) add('inherent: link spelling, no virama glyph (same letters read back)', r);
  else if (!hasB5 && noI(u) === noI(o)) add('inherent: link spelling + i-sign (71 C1 C2 is also C1-i C2)', r);
  else if (hasB5 && noI(u) === noI(o) && (o.match(/੍/g) || []).length >= b.split(' ').filter(x => x === 'B5').length && !/^71 6D/.test(b)) add('inherent: link + halant in one spelling (the link virama has no glyph; every B5 virama read back)', r);
  else if (/^71 6D/.test(b)) add('table bug: 71 before ੜ not reordered', r);
  else add('other', r);
}
fs.writeFileSync('revall/classified.json', JSON.stringify(cls, null, 1));
console.log(JSON.stringify(cls, null, 1));
