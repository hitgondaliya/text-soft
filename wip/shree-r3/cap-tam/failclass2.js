// Re-classify measured/forward-failures.tsv (the real FontConverter vs Shree's truth) by what a reader SEES.
// Every Tamil face (SHREE-TAM7-*/SHREE-TAM-*) draws these byte pairs with identical outlines + advances
// (fonts/dup-glyphs.json): 23=B4 (ய்), 26=AD (dash), 40=F7 (ே), 5C=8C (ச), 5F=94 (சு), 60=8B (சூ).
// Classes:
//   glyph-equal        ours draws exactly what Shree's accepted spelling draws (bytes differ only inside a pair above)
//   joiner-only        ours = accepted spelling + U+200C/U+200D passed through (invisible, but a foreign char in the doc)
//   joiner+glyph-equal both of the above
//   order              same glyphs, other order (visibly wrong: a sign on the wrong side / the wrong letter)
//   unmapped           a Tamil character passed through (ௌ)
//   other              different glyphs
// Groups: the Unicode string with every consonant -> C (ஸ்ரீ க்ஷ kept), plus the class and the byte difference.
// node failclass2.js -> measured/forward-failure-groups.json
const fs = require('fs');
const D = __dirname;
const EQ = { B4: '23', AD: '26', F7: '40', '8C': '5C', '94': '5F', '8B': '60' };
const canon = s => s.split(' ').filter(Boolean).map(t => EQ[t] || t).join(' ');
const noJ = s => s.split(' ').filter(t => t && t !== 'U+200C' && t !== 'U+200D').join(' ');
const rows = fs.readFileSync(D + '/measured/forward-failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => { const [u, cps, cat, ours, pref, acc, ft] = l.split('\t'); return { u, cps, cat, ours, pref, acc: acc.split(','), ft }; });
const shape = u => u.replace(/ஸ்ரீ/g, 'SRI').replace(/க்ஷ/g, 'KSS').replace(/[க-ஹ]/g, 'C');
const sorted = s => s.split(' ').filter(Boolean).sort().join(' ');
const out = { about: 'forward failures of the shipped unicode-to-shree-tam7 (real FontConverter, snapshot build) re-read by glyph: see failclass2.js header', total: rows.length, byClass: {}, groups: {} };
const classOf = r => {
  const acc = r.acc.map(canon);
  const o = r.ours;
  if (/U\+0B/.test(o)) return 'unmapped';
  const j = /U\+200[CD]/.test(o);
  const oc = canon(noJ(o));
  if (r.acc.includes(noJ(o))) return 'joiner-only';
  if (acc.includes(oc)) return j ? 'joiner+glyph-equal' : 'glyph-equal';
  if (acc.some(a => sorted(a) === sorted(oc))) return j ? 'joiner+order' : 'order';
  return j ? 'joiner+other' : 'other';
};
for (const r of rows) {
  const c = classOf(r);
  out.byClass[c] = (out.byClass[c] || 0) + 1;
  let diff = '';
  const oc = canon(noJ(r.ours)).split(' '), pc = canon(r.pref).split(' ');
  if (c.endsWith('other')) diff = ' | ours has ' + oc.filter(x => !pc.includes(x)).join(' ') + ' / Shree has ' + pc.filter(x => !oc.includes(x)).join(' ');
  if (c === 'glyph-equal' || c === 'joiner+glyph-equal') diff = ' | ' + [...new Set(noJ(r.ours).split(' ').filter((t, i) => t !== r.pref.split(' ')[i]))].join(' ') + ' for ' + [...new Set(r.pref.split(' ').filter((t, i) => t !== noJ(r.ours).split(' ')[i]))].join(' ');
  const k = shape(r.u.replace(/[‌‍]/g, 'J')) + ' | ' + c + diff;
  const g = out.groups[k] || (out.groups[k] = { n: 0, examples: [] });
  g.n++;
  if (g.examples.length < 4) g.examples.push(`${r.u} [${r.cps}]: ours ${r.ours} | Shree ${r.pref}`);
}
out.groups = Object.fromEntries(Object.entries(out.groups).sort((a, b) => b[1].n - a[1].n));
// how many truth strings are visually right (exact, accepted, or glyph-equal), with and without the joiner rows
const truth = require('./tam7-groundtruth.json').truth;
const N = Object.keys(truth).length;
const vis = (out.byClass['glyph-equal'] || 0);
const visJ = vis + (out.byClass['joiner-only'] || 0) + (out.byClass['joiner+glyph-equal'] || 0);
const m = JSON.parse(fs.readFileSync(D + '/measured/measure.json', 'utf8'));
const acc = parseInt(m.forward.accepted);
out.visual = {
  truthStrings: N,
  acceptedBytes: acc,
  glyphEqualOnly: vis,
  drawsWhatShreeDraws: `${acc + vis}/${N} = ${(100 * (acc + vis) / N).toFixed(2)}% (accepted bytes, or other bytes that draw the identical glyphs)`,
  drawsWhatShreeDrawsIgnoringJoiners: `${acc + visJ}/${N} = ${(100 * (acc + visJ) / N).toFixed(2)}% (also counting rows whose only extra is a passed-through ZWJ/ZWNJ)`,
  visiblyWrong: N - acc - visJ,
};
// split: round-1 strings (one syllable or one cluster: vendor keys + P/E probes) vs round-2 W strings (2-3 syllables),
// and with / without the strings that contain ZWNJ/ZWJ (probe artefacts: Shree writes nothing for them)
const r1 = fs.existsSync(D + '/r1/tam7-groundtruth.json') ? require('./r1/tam7-groundtruth.json').truth : null;
if (r1) {
  const failBy = new Map(rows.map(r => [r.u, classOf(r)]));
  const sets = { 'round 1 (syllables, clusters, vendor keys)': u => !!r1[u], 'round 2 W (2-3 syllables in one keystroke)': u => !r1[u], 'all without ZWNJ/ZWJ strings': u => !/[‌‍]/.test(u), 'all': () => true };
  out.bySet = {};
  for (const [name, f] of Object.entries(sets)) {
    let n = 0, ok = 0, ge = 0;
    for (const u of Object.keys(truth)) { if (!f(u)) continue; n++; const c = failBy.get(u); if (!c) ok++; else if (c === 'glyph-equal') ge++; }
    out.bySet[name] = { strings: n, acceptedBytes: `${ok} = ${(100 * ok / n).toFixed(2)}%`, drawsWhatShreeDraws: `${ok + ge} = ${(100 * (ok + ge) / n).toFixed(2)}%` };
  }
  console.log(JSON.stringify(out.bySet, null, 1));
}
fs.writeFileSync(D + '/measured/forward-failure-groups.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out.byClass), JSON.stringify(out.visual, null, 1));
for (const [k, g] of Object.entries(out.groups).slice(0, 70)) console.log(g.n, k, '::', g.examples[0]);
