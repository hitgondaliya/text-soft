// Forward failures of the shipped unicode-to-shree-mal7 table (measured/forward-failures.tsv from measure.exe), grouped
// by cause, with counts split into syllable strings (P/X probes, vendor keys) and joined strings (W probes only).
// node failclass.js [measuredDir=measured] [truth=mal7-groundtruth.json]  -> <dir>/forward-failure-groups.json
const fs = require('fs');
const dir = process.argv[2] || 'measured';
const gt = require('./' + (process.argv[3] || 'mal7-groundtruth.json'));
const T = gt.truth;
// which strings have syllable-level evidence (not only W joins)
const syl = new Set();
if (gt.pairs) for (const p of gt.pairs) if (!/^(alt|quirk)/.test(p.kind) && !/^probe W/.test(p.source)) syl.add(p.unicode);
const lines = fs.readFileSync(dir + '/forward-failures.tsv', 'utf8').split('\n').slice(1).filter(Boolean).map(l => l.split('\t'));
const LIG = { '84': '57 E6', '85': '59 E6', '86': '6B E6', '87': '6D E6', '98': '77 E6', 'FE': '79 E6', 'FA': '6C E6', '8A': '73 E6', '88': '5C FF', '89': '6D FF', '8B': '76 FF', 'FB': '57 9B', 'FC': '6D 9C', 'FD': '6D C9', 'A3': 'A2 72', 'E0': 'DF 56 57', 'E5': 'E4 56 57' };
const causes = [
  ['ZWNJ (U+200C) passed into the legacy text: Shree writes nothing for it (the halant 44 / C + link + ZWNJ = the chandrakkala 56 alone)', (u, o, s) => /U\+200C/.test(o)],
  ['ZWJ (U+200D) passed into the legacy text', (u, o, s) => /U\+200D/.test(o)],
  ['ൄ ൢ ൣ passed through: the 7 composer writes nothing for them', (u, o, s) => /U\+0D4[4]|U\+0D6[23]/.test(o)],
  ['other Unicode passed through (₹ U+20B9 = 3C, which no Malayalam 7 face draws)', (u, o, s) => /U\+/.test(o)],
  ['C് written as a chillu or a sign glyph (A2 ൻ A5 ൾ A6 ൺ E6 la-sign 4E ra-sign 7E va-sign) where Shree writes C + the chandrakkala 56: the table treats a plain virama as the Unicode 5.0 chillu', (u, o, s) => { const ob = o.split(' '), sb = s.split(' '); if (['A2', 'A5', 'A6'].some(b => ob.includes(b) && !sb.includes(b)) && /[ണനള]്/.test(u)) return true; return /[ലരവ]്($|[^യവര])/.test(u) && ['E6', '4E', '7E'].some(b => ob.includes(b) && ob.filter(x => x === b).length > sb.filter(x => x === b).length); }],
  ['ra sign ്ര not written as 4E BEFORE the letter/cluster (the table writes C 56 71, or puts 4E after; ശ്ര should be E8)', (u, o, s) => /്ര/.test(u) && (/(^| )(4E|E8)( |$)/.test(s)) && o !== s && !(/(^| )4E/.test(o) && o.split(' ').indexOf('4E') === s.split(' ').indexOf('4E'))],
  ['split vowel ൊ ോ ൌ: the table writes C 52 4C (sign after the letter), Shree 52 C 4C', (u, o, s) => /[ൊോൌ]/.test(u) && o.split(' ').sort().join(' ') === s.split(' ').sort().join(' ')],
  ['ya/va sign: the table writes C 56 70 / C 56 76 (chandrakkala + full letter) where Shree writes the sign 7C / 7E', (u, o, s) => /്[യവ]/.test(u) && /56 (70|76)/.test(o) && /(7C|7E)/.test(s)],
  ['ൻറ / ന്റ written as the ligature glyph A3, which no Shree key writes (Shree: ൻറ = A2 72, ന്റ = 6A 56 72)', (u, o, s) => /(^| )A3( |$)/.test(o)],
  ['precomposed ligature glyph where Shree writes C + a subjoined mark or C + the doubling stroke FF (similar picture, other bytes: 84/8A/88/89/8B/FB/FC/FD/98/FE/86/87/85/FA/E0/E5; fonts/ligature-vs-composer.png)', (u, o, s) => o.split(' ').some(b => LIG[b] && s.includes(LIG[b]))],
  ['conjunct missing: the table writes C1 56 C2 where Shree writes a ligature or a subjoined form', (u, o, s) => /്[ക-ഹ]/.test(u) && (o.match(/56/g) || []).length > (s.match(/56/g) || []).length],
  ['conjunct where Shree writes the chandrakkala: the table writes a ligature/subjoined form for a pair Shree draws C1 56 C2', (u, o, s) => /്[ക-ഹ]/.test(u) && (o.match(/56/g) || []).length < (s.match(/56/g) || []).length],
  ['hyphen: the table keeps 2D, Shree writes 26 for - (code 2D)', (u, o, s) => u.includes('-')],
  ['other', () => true],
];
const groups = new Map();
let total = 0, sylN = 0;
for (const [u, cps, cat, ours, pref, acc, ft] of lines) {
  total++; const isSyl = syl.has(u); if (isSyl) sylN++;
  let name = 'other';
  for (const [n, f] of causes) { try { if (f(u, ours, pref)) { name = n; break; } } catch (e) { } }
  if (!groups.has(name)) groups.set(name, { count: 0, syllableStrings: 0, joinedStrings: 0, examples: [] });
  const g = groups.get(name); g.count++; if (isSyl) g.syllableStrings++; else g.joinedStrings++;
  if (g.examples.length < 12 && (isSyl || g.examples.length < 6)) g.examples.push(`${u} [${cps}]: ours ${ours} | Shree ${pref}`);
}
const truthTotal = Object.keys(T).length, truthSyl = Object.keys(T).filter(u => syl.has(u)).length;
const out = { source: dir + '/forward-failures.tsv', truthStrings: truthTotal, syllableStrings: truthSyl, joinedOnlyStrings: truthTotal - truthSyl, failing: total, failingSyllableStrings: sylN, failingJoinedOnlyStrings: total - sylN, forwardExactSyllableStrings: `${truthSyl - sylN}/${truthSyl} = ${(100 * (truthSyl - sylN) / truthSyl).toFixed(2)}%`, forwardExactJoinedOnly: `${truthTotal - truthSyl - (total - sylN)}/${truthTotal - truthSyl} = ${(100 * (truthTotal - truthSyl - (total - sylN)) / (truthTotal - truthSyl)).toFixed(2)}%`, groups: Object.fromEntries([...groups].sort((a, b) => b[1].count - a[1].count)) };
fs.writeFileSync(dir + '/forward-failure-groups.json', JSON.stringify(out, null, 1));
console.log(JSON.stringify(out, null, 1));
