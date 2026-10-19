// Strings a table needs that Shree's composer cannot be asked in one key (more than 4 codes), with a spelling DERIVED by
// the composer's own syllable rule (model.js: 22,617/22,617 truth syllables agree with it) -> mal7-composer-gaps.json.
// NOT part of the truth. Also: the Unicode 5.0 chillus (C്‍), samvruthokaram without ZWNJ, and three-consonant clusters
// the rule cannot build (listed as unknown).
const fs = require('fs');
const g = require('./mal7-groundtruth.json');
const T = g.truth;
const { make } = require('./model');
const { deriveAll } = make(T);
const { syllables } = require('./malenc');
const PR = new Map(require('./capp/P-results.json').map(r => [r.codes, r]));
const CMAP = require('./expected').C;
const CODE = Object.fromEntries(Object.entries(CMAP).map(([k, v]) => [v, Number(k)]));
const hx = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = 'കഖഗഘങചഛജഝഞടഠഡഢണതഥദധനപഫബഭമയരറലളഴവശഷസഹ'.split('');
const V = '്';
const gaps = new Map();
const add = (u, why) => {
  u = u.normalize('NFC');
  if (T[u] || gaps.has(u)) return;
  const xs = deriveAll(u);
  if (!xs.length) return;
  const syl = syllables(u);
  gaps.set(u, { unicode: u, derived: xs[0], accepted: xs, codes: syl ? syl.map(s => s.codes.map(hx).join(' ')).join(' | ') : null, why });
};
// the real conjuncts: pairs the composer draws as a ligature or with a subjoined form (not C1 + chandrakkala + C2)
const real = [];
for (const a of CONS) for (const b of CONS) {
  const r = PR.get([CODE[a], 0x64, CODE[b]].map(hx).join(' '));
  if (!r || !r.bytes) continue;
  const pa = T[a].preferred, pb = T[b].preferred;
  if (r.bytes !== pa + ' 56 ' + pb && !/^[രയവ]$/.test(b)) real.push(a + V + b);
}
real.push('ക്ഷ');
// clusters in the common words (also those drawn with the chandrakkala)
const words = fs.readFileSync(__dirname + '/common-words-ml.txt', 'utf8').split(/\r?\n/).filter(Boolean);
const wordClusters = new Set();
for (const w of words) for (const s of syllables(w) || []) { const m = /^((?:[ക-ഹ]്)+[ക-ഹ])/.exec(s.u); if (m) wordClusters.add(m[1]); }
const PRE = ['െ', 'േ', 'ൈ', 'ൊ', 'ോ', 'ൌ'];
const POST = ['ാ', 'ി', 'ീ', 'ു', 'ൂ', 'ൃ', 'ൗ'];
const TAILS = ['', 'ം', 'ഃ'];
const clusters = [...new Set([...real, ...[...wordClusters].filter(c => c.split(V).length === 2)])];
for (const cl of clusters) {
  for (const s of [...PRE, ...POST, '']) for (const t of TAILS) {
    add(cl + s + t, 'two-consonant cluster + sign/tail: 5+ codes (' + (PRE.includes(s) ? 'pre-base sign + C1 64 C2 + ...' : 'C1 64 C2 + sign + tail') + ')');
    for (const y of ['്യ', '്വ']) add(cl + y + s + t, 'cluster + ' + y + ' + sign/tail: 5+ codes');
    add(cl + '്ര' + s + t, 'ra sign + cluster + sign/tail: 5+ codes (59 C1 64 C2 + ...)');
    add(cl + '്ര്യ' + s + t, 'ra sign + cluster + ്യ + sign/tail');
  }
  add(cl + 'ു്', 'samvruthokaram on a cluster: C1 64 C2 ു + the halant 44 = 5 codes');
  add(cl + 'ു്‌', 'samvruthokaram on a cluster (with ZWNJ): 5 codes');
}
// single letters: ra + ya + sign + tail, ya/va + sign + tail beyond 4 codes, pre + ra + ya ...
for (const c of CONS) for (const s of [...PRE, ...POST, '']) for (const t of TAILS) {
  add(c + '്ര്യ' + s + t, 'ra sign + letter + ്യ + sign/tail: 5+ codes');
  add(c + '്ര' + s + t, 'ra sign + letter + sign + tail: 5+ codes');
  add(c + '്യ' + s + t, 'letter + ്യ + sign + tail: 5+ codes');
  add(c + '്വ' + s + t, 'letter + ്വ + sign + tail: 5+ codes');
}
// samvruthokaram without ZWNJ: the composer types C + ു + 44 = C ു ് ZWNJ; C + ു + 64 loses the virama
for (const c of CONS) for (const s of POST) { const u = c + s + V; if (!T[u]) { const w = T[c + s + V + '‌']; if (w) gaps.set(u.normalize('NFC'), { unicode: u.normalize('NFC'), derived: w.preferred, accepted: [w.preferred], codes: hx(CODE[c]) + ' ' + ({ 'ാ': '6C', 'ി': '6A', 'ീ': '4A', 'ു': '6B', 'ൂ': '6F', 'ൃ': 'D0', 'ൗ': '4F' })[s] + ' 44', why: 'sign + virama with no ZWNJ (samvruthokaram കു്): the only key that writes it is the halant 44, whose Unicode answer carries a ZWNJ (കു്‌); the link 64 after a sign is dropped by both composers' }); } }
// Unicode 5.0 chillus: C + virama + ZWJ
const chillu = { 'ണ': 'ൺ', 'ന': 'ൻ', 'ര': 'ർ', 'ല': 'ൽ', 'ള': 'ൾ', 'ക': 'ൿ' };
const zwjRows = [];
for (const [c, ch] of Object.entries(chillu)) {
  const u = c + V + '‍';
  const atomic = T[ch] ? T[ch].preferred : null;
  const composer = g.pairs.find(p => p.unicode === u && /link \+ ZWJ/.test(p.kind));
  zwjRows.push({ unicode: u, atomicChillu: ch, atomicBytes: atomic, composerAnswerForTheZwjSpelling: composer ? composer.bytes : null, why: 'Unicode 5.0 chillu spelling: Unicode fonts draw the chillu; the 7 composer ignores the ZWJ and writes C + the chandrakkala 56 (ത്‍ / മ്‍: nothing). Policy choice for a table: the atomic chillu bytes (what the text looks like) or the composer bytes.' });
}
// three-consonant clusters in the common words (and frequent ones) the rule cannot build: no probe can ask them
const unknown = [];
for (const w of words) for (const s of syllables(w) || []) {
  const m = /^((?:[ക-ഹ]്)+[ക-ഹ])/.exec(s.u); if (!m) continue;
  const letters = m[1].split(V);
  if (letters.length < 3) continue;
  if (deriveAll(s.u).length || T[s.u.normalize('NFC')]) continue;
  unknown.push({ syllable: s.u, word: w, codes: s.codes.map(hx).join(' ') });
}
for (const u of ['സ്റ്റ', 'ന്റ്റ', 'ക്ക്ക', 'ന്ത്ത', 'ങ്ക്ക', 'ച്ച്ച', 'ട്ട്ട', 'ത്ത്ത', 'പ്പ്പ', 'ല്ല്ല']) if (!unknown.some(x => x.syllable === u)) unknown.push({ syllable: u, word: null, codes: (syllables(u) || [{ codes: [] }])[0].codes.map(hx).join(' ') });
const list = [...gaps.values()];
const why = {}; for (const x of list) why[x.why] = (why[x.why] || 0) + 1;
fs.writeFileSync(__dirname + '/mal7-composer-gaps.json', JSON.stringify({
  about: 'Unicode strings with NO direct Shree 7 composer answer (a key holds at most 4 codes; the Tutor answers one key at a time with no state; the slot header byte is ignored: gen-len.js), with a spelling DERIVED by the composer\'s own syllable rule (model.js) from truth entries of their pieces. The rule reproduces 22,617 of the 22,617 truth syllables that are built from pieces (mal7-patterns.txt §4). NOT part of the truth. clusters = the 357 pairs the composer draws as a ligature or with a subjoined form, ക്ഷ, and every two-consonant cluster of common-words-ml.txt. accepted lists both spellings of യ്യ/വ്വ (link and sign). chillusWithZwj and unknownThreeConsonantClusters are policy items with no composer answer.',
  rule: 'bytes = [െ 52 | േ 53 | ൈ 52 52 | ൊ 52 | ോ 53 | ൌ 52] [ra 4E] [cluster as typed alone] [്യ 7C | ്വ 7E]* [ാ 4C ി 5D ീ 7D ു 4F ൂ 50 ൃ 51 ൗ 54 | ൊ ോ 4C | ൌ 54] [ം 55 | ഃ 40]* [് 56]; ra + ശ = E8 and then no conjunct (ശ്ക്ര = E8 56 57)',
  counts: { derived: list.length, byWhy: why, chillusWithZwj: zwjRows.length, unknownThreeConsonantClusters: unknown.length },
  gaps: list, chillusWithZwj: zwjRows, unknownThreeConsonantClusters: unknown,
}, null, 1));
console.log(JSON.stringify({ derived: list.length, byWhy: why, zwj: zwjRows, unknown }, null, 1).slice(0, 4000));
