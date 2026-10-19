// pun7-composer-gaps.json: Unicode strings typed in Shree's canonical order for which the 7 composer gives NO usable
// answer (it loses a glyph), with what it wrote and - where the composer's own rules give one unambiguous spelling -
// the spelling a table should write (derived, marked as such; render-checked samples in fonts/check3.png).
const fs = require('fs');
const g = require('./pun7-groundtruth.json');
const T = g.truth;
const by = new Map();
for (const p of g.pairs) { if (!by.has(p.unicode)) by.set(p.unicode, []); by.get(p.unicode).push(p); }
const LIG = { '72': '73', '79': '7A', 'B1': '7C', '7D': '7E', 'A1': 'A2' };
const gaps = {};
for (const [u, list] of by) {
  if (T[u]) continue;
  const q = list.filter(p => /^quirk:7-composer-(loses|dropped|writes-the-spacer)/.test(p.kind));
  if (!q.length || list.some(p => p.kind.startsWith('quirk:U-composer'))) continue;
  const entry = { composerWrote: [...new Set(q.map(p => p.bytes))], why: [...new Set(q.map(p => p.kind))], derived: null, derivedFrom: null };
  // derive: X + tail where X (u without its last mark) has truth
  const last = u.slice(-1), head = u.slice(0, -1);
  if (T[head] && 'ਂੰੱ'.includes(last)) {
    const hb = T[head].preferred.split(' ');
    const sign = head.slice(-1);
    if (last === 'ਂ' || (last === 'ੰ' && 'ਾੀੇੈੋੌ'.includes(sign))) {
      const i = hb.findIndex(b => LIG[b]);
      if ('ੀੇੈੋੌ'.includes(sign) && i >= 0) { const d = hb.slice(); d[i] = LIG[d[i]]; entry.derived = d.join(' '); entry.derivedFrom = `${head} = ${T[head].preferred}, with the sign+bindi ligature ${hb[i]}->${LIG[hb[i]]} (as ਕੀਂ ਕੇਂ ... and ਨੇਂ = B2 7A BE)`; }
      else if (sign === 'ਾ') { entry.derived = hb.concat('A3').join(' '); entry.derivedFrom = `${head} = ${T[head].preferred} + bindi A3 after ਾ (as ਕ੍ਹਾਂ = 48 AA 70 A3; a tippi after ਾ is written as bindi)`; }
    } else if (last === 'ੱ' && sign === 'ਾ') { entry.derived = hb.concat('A7').join(' '); entry.derivedFrom = `${head} = ${T[head].preferred} + addak A7 after ਾ (as ਕਾੱ = 48 70 A7)`; }
  }
  gaps[u] = entry;
}
const n = Object.keys(gaps).length, d = Object.values(gaps).filter(e => e.derived).length;
fs.writeFileSync(__dirname + '/pun7-composer-gaps.json', JSON.stringify({ about: 'Unicode strings typed in Shree\'s canonical order (ਠ/ਨ + subjoined + sign + tail, two subjoined codes, a sign lost to a bindi ligature) for which S8Pun764 loses a glyph, so pun7-groundtruth.json has no truth for them. composerWrote = the broken answers; derived = the spelling the composer\'s own rules give (head string\'s truth + the tail as the composer writes it elsewhere), null where no single spelling follows (ੁ/ੂ + tail on ਠ/ਨ: the composer writes 74/76 before or AE/AF after BE depending on the tail).', count: n, derived: d, gaps }, null, 1));
console.log('gaps', n, 'derived', d);
for (const [u, e] of Object.entries(gaps).slice(0, 12)) console.log(u, JSON.stringify(e.composerWrote), '->', e.derived);
for (const u of ['ਨ੍ਹਾਂ', 'ਠ੍ਹਾਂ', 'ਨ੍ਹੇਂ', 'ਨ੍ਰੁਂ']) console.log(u, JSON.stringify(gaps[u] || null));
