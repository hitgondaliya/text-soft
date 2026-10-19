// Patterns of Shree's Malayalam 7 composer, each checked against the ground truth -> mal7-patterns.txt
const fs = require('fs');
const g = require('./mal7-groundtruth.json');
const T = g.truth;
const { make } = require('./model');
const { derive, deriveAll, parse, split } = make(T);
const PR = new Map(require('./capp/P-results.json').map(r => [r.codes, r]));
const CODE = Object.fromEntries(Object.entries(require('./expected').C).map(([k, v]) => [v, Number(k)]));
const hx = c => c.toString(16).toUpperCase().padStart(2, '0');
const L = [];
const say = s => L.push(s);
const CONS = 'കഖഗഘങചഛജഝഞടഠഡഢണതഥദധനപഫബഭമയരറലളഴവശഷസഹ'.split('');
const pref = u => T[u] ? T[u].preferred : '-';
say('Shree-Lipi 7 Malayalam (S8Mal764.dll) - patterns, each checked against mal7-groundtruth.json (' + Object.keys(T).length + ' Unicode strings with truth).');
say('Typing order is VISUAL: pre-base signs and the ra sign are struck before the letter; bytes come out in the same visual order.');
say('');
say('1. Letters (one code each, the byte the font draws):');
say('   ' + CONS.map(c => c + '=' + pref(c)).join('  '));
say('   vowels: ' + ['അ', 'ആ', 'ഇ', 'ഈ', 'ഉ', 'ഊ', 'ഋ', 'എ', 'ഏ', 'ഐ', 'ഒ', 'ഓ', 'ഔ', 'ഌ', 'ൠ', 'ൡ'].map(c => c + '=' + pref(c)).join('  ') + '   (ഈ = ഇ + ൗ 43 54, ഊ 44 54, ഐ = െ + എ 52 49, ഓ = ഒ + ാ 4B 4C, ഔ 4B 54; ഌ ൠ ൡ: the 7 composer writes nothing)');
say('   chillus: ' + ['ൺ', 'ൻ', 'ർ', 'ൽ', 'ൾ', 'ൿ'].map(c => c + '=' + pref(c)).join('  ') + '   (ൿ: nothing, no glyph)');
say('   ക്ഷ = ' + pref('ക്ഷ') + ' (code D5), ശ്ര = ' + pref('ശ്ര') + ', ശ്രീ = ' + pref('ശ്രീ') + ', ം = 55, ഃ = 40, the chandrakkala ് = 56');
say('   digits ൦-൯ = 30-39; ‘ 22 ’ 27 × 23 ÷ 24 — 97 – 96 - 26 ₹ 3C (no glyph in any face)');
say('');
// 2. signs on every letter
const SIGNS = { 'ാ': '4C', 'ി': '5D', 'ീ': '7D', 'ു': '4F', 'ൂ': '50', 'ൃ': '51', 'ൗ': '54', 'ം': '55', 'ഃ': '40', '്': '56', '്യ': '7C', '്വ': '7E' };
const PRES = { 'െ': ['52', ''], 'േ': ['53', ''], 'ൈ': ['52 52', ''], 'ൊ': ['52', '4C'], 'ോ': ['53', '4C'], 'ൌ': ['52', '54'] };
let ok = 0, n = 0; const bad = [];
for (const c of CONS) {
  for (const [s, b] of Object.entries(SIGNS)) { const u = (c + s).normalize('NFC'); if (!T[u]) continue; n++; if (T[u].preferred === pref(c) + ' ' + b) ok++; else bad.push(u + '=' + T[u].preferred); }
  for (const [s, [p, q]] of Object.entries(PRES)) { const u = (c + s).normalize('NFC'); if (!T[u]) continue; n++; if (T[u].preferred === [p, pref(c), q].filter(Boolean).join(' ')) ok++; else bad.push(u + '=' + T[u].preferred); }
  const ur = c + '്ര'; if (T[ur]) { n++; if (T[ur].preferred === '4E ' + pref(c)) ok++; else bad.push(ur + '=' + T[ur].preferred); }
}
say(`2. Signs on a letter: C + ാ ി ീ ു ൂ ൃ ൗ ം ഃ ് ്യ ്വ = the letter byte + the sign byte (4C 5D 7D 4F 50 51 54 55 40 56 7C 7E); െ േ ൈ = 52 / 53 / 52 52 BEFORE the letter; ൊ = 52 C 4C, ോ = 53 C 4C, ൌ = 52 C 54; ്ര = 4E before the letter. ${ok}/${n} letter+sign strings follow it; exceptions: ${bad.join(' ')}`);
say('   NO letter has its own u/uu (ു ൂ) or ൃ form in the 7 encoding: ു = 4F, ൂ = 50, ൃ = 51 after every one of the 36 letters and after every cluster (the reformed script). ൄ ൢ ൣ: the 7 composer writes nothing.');
say('   The precomposed sign codes DC ോ DD ൊ DE ൈ DF ൌ (INSCRIPT/KERGOVT keys) make the 7 composer write NOTHING; the Unicode composer writes the sign.');
say('');
// 3. conjuncts
const lig = [], subj = new Map(), vis = [], other = [];
for (const a of CONS) for (const b of CONS) {
  const u = a + '്' + b; const r = PR.get([CODE[a], 0x64, CODE[b]].map(hx).join(' ')); if (!r || !r.bytes) { other.push(u + '=none'); continue; }
  const cand = r.bytes;
  const pa = pref(a), pb = pref(b);
  if (cand === pa + ' 56 ' + pb) vis.push(u);
  else if (cand.startsWith(pa + ' ') && cand.split(' ').length === pa.split(' ').length + 1) { const s = cand.slice(pa.length + 1); if (!subj.has(b + ' ' + s)) subj.set(b + ' ' + s, []); subj.get(b + ' ' + s).push(a); }
  else lig.push(u + '=' + cand);
}
say(`3. Two-consonant conjuncts typed with the link 64 (C1 64 C2), all ${CONS.length * CONS.length} pairs probed (the raw composer answer for the three codes):`);
say(`   ${lig.length} ligatures (മ്ബ and ന്പ both D2): ${lig.join('  ')}`);
say(`   C1 + a subjoined form of C2 (${[...subj.values()].reduce((a, b) => a + b.length, 0)} pairs):`);
for (const [k, v] of subj) say(`     ${k}: after ${v.join(' ')}`);
say(`   C1 + the chandrakkala 56 + C2: the other ${vis.length} pairs (e.g. ${vis.slice(0, 12).join(' ')}); ര്ക = 71 56 57 (no dot reph, no ർ - the chillu ർ is its own code 47 = A1)`);
say('   C + link + ര/യ/വ gives the chandrakkala form (ക്ര via link = 57 56 71); the signs are the codes 59 ്ര (4E BEFORE the letter), 55 ്യ (7C), 49 ്വ (7E): the truth uses the signs, the link forms are kept as alternatives. യ്യ: E1 (link) and 70 7C (sign) are both canonical; വ്വ: 76 FF (link) and 76 7E (sign).');
say('   Font glyphs no key ever writes: ' + g.counts.fontCheck.fontCodesNeverWrittenByTheComposer + ' (84 ക്ല 85 ഗ്ല 86 പ്ല 87 ബ്ല 98 ശ്ല FE സ്ല FA ഫ്ല 8A ല്ല 88 ച്ച 89 ബ്ബ 8B വ്വ A3 ന്റ F0 സ്റ്റ FB ക്ട FC ബ്ധ FD ബ്ദ E0 യ്ക്ക E5 ല്ക്ക ...: the composer writes the same pictures as C + E6 / C + FF / C + 9B ... ; fonts/never-produced.png, fonts/check1.png)');
say('');
// 4. cluster + sign regularity and every derived rule
let d = 0, dok = 0; const dbad = [];
for (const [u, t] of Object.entries(T)) {
  const p = parse(u); if (!p) continue;
  const { core, ra, yv } = split(p.letters);
  if (!p.sign && !p.tails && !p.vir && !ra && !yv.length) continue; // a bare cluster: derive() would read its own entry
  const xs = deriveAll(u); if (!xs.length) continue;
  d++; if (xs.some(x => t.accepted.includes(x))) dok++; else dbad.push(u + ': rule ' + xs.join(' or ') + ' / Shree ' + t.preferred);
}
say(`4. The whole syllable rule (model.js): [pre 52|53|52 52] [ra 4E] [cluster as typed alone] [7C|7E]* [post] [55|40]* [56]: ${dok}/${d} truth syllables built from their pieces agree with Shree (${d - dok} differ${dbad.length ? ': ' + dbad.slice(0, 15).join('; ') : ''}).`);
say('   Inside the rule: the ra sign 4E + ശ 77 is always the ligature E8 and then no conjunct with the next letter (ശ്ര = E8, ശ്ക്ര = E8 56 57, ശ്ച്ര = E8 56 5C although ശ്ച alone is E9 - the ra drawn on ശ). Left to the composer: C1്ശ under the ra sign = 4E C1 E8 (the ra twice and no chandrakkala: a quirk, kept out of the truth).');
say('   Note: the pre-base signs and the ra sign always go before the WHOLE cluster, also when the cluster is drawn as C1 + chandrakkala + C2 (ക്ചെ = 52 57 56 5C, ക്ച്ര = 4E 57 56 5C): that is the composer own output, even where the picture reads as if the sign belonged to C1.');
say('');
// 5. joins
const wj = require('./wjoin.json');
say(`5. Syllables join without effects (W set, ${wj.probes} probes of 2-3 syllables in one key): the 7 composer writes the syllables' own answers joined in ${wj.sevenComposerWritesTheUnitsJoined}/${wj.withAllUnitsAnswered}. The other ${wj.sevenDiffers}: 207 x "C + link, then the next letter's pre-base sign" (ക ് െ ക = 57 A8: the composer joins the cluster across the sign and LOSES the sign - not Shree's typing order for ക്കെ, which is െ ക ് ക = 52 A8) and 50 "ക്ഷ + link + C" which are one cluster, not two syllables (ക്ഷ്മ = 83 DC).`);
say(`   The Unicode composer writes the syllables joined in ${wj.unicodeComposerWritesTheUnitsJoined}/${wj.withAllUnitsAnswered}: after ്യ/്വ it drops every following sign, even in the next syllable (ക്യ + കാ = "ക്യക"), and under the ra sign it drops the ക of ക്ഷ ("ഷ്ര").`);
say('');
say('6. Viramas: the link 64 at the end = C + 56 (ക് = 57 56); the halant 44 (virama + ZWNJ) = 56 too; C + link + ZWNJ = 56; C + link + ZWJ (the Unicode 5.0 chillu ന്‍) = C + 56, NOT the chillu (ന്‍: 6A 56 against ൻ A2) - and ത്‍ / മ്‍ make the 7 composer write nothing at all. Samvruthokaram: C + ു + 44 = C 4F 56 (കു്‌); C + ു + 64 loses the virama in both composers. The code E2 (a virama on no vendor keyboard) writes no glyph and a backspace byte 08 before a ligature.');
say('7. Dot reph ൎ (U+0D4E): no code, no key; ര + link + C = 71 56 C (visible chandrakkala); the chillu ർ (47 = A1) + C is the modern reph. ന്റ: link = 6A 56 72, chillu ൻ + റ = A2 72; the font\'s ന്റ glyph A3 is written by no key.');
say('8. Quirks kept out of the truth: ' + Object.entries(g.counts.byKind).filter(([k]) => k.startsWith('quirk:')).map(([k, v]) => k.replace(/ \(.*/, '') + ' ' + v).join('; '));
fs.writeFileSync(__dirname + '/mal7-patterns.txt', L.join('\n') + '\n');
console.log(L.join('\n'));
