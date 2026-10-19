// Write ori7-patterns.txt: the rules of Shree's Odia 7 spelling, each checked against the truth (counts + examples).
const fs = require('fs');
const g = require('./ori7-groundtruth.json');
const T = g.truth;
const probe = [...require('./capp/P-results.json'), ...require('./caph/H-results.json')];
const out = [];
const P = s => out.push(s);
const pref = u => T[u] ? T[u].preferred : '(none)';
const V = '୍';
const CONS = [...'କଖଗଘଙଚଛଜଝଞଟଠଡଢଣତଥଦଧନପଫବଭମଯରଲଳଶଷସହୟ'];
const SIGNS = ['ା', 'ି', 'ୀ', 'ୁ', 'ୂ', 'ୃ', 'େ', 'ୈ', 'ୋ', 'ୌ', 'ୖ', 'ୗ'];
function rule(name, strings, fn) {
  let n = 0, ok = 0; const miss = [];
  for (const u of strings) { const want = fn(u); if (want == null || !T[u]) continue; n++; if (T[u].preferred === want || T[u].accepted.includes(want)) ok++; else if (miss.length < 8) miss.push(`${u} rule ${want} / truth ${T[u].preferred}`); }
  P(`  check "${name}": ${ok}/${n} truth strings follow it${miss.length ? '; exceptions: ' + miss.join('; ') : ''}`);
  return { ok, n };
}
const letter = {}; for (const c of CONS) letter[c] = pref(c);
const head = b => b.replace(/ FF$/, ''); // the letter without its trailing spacer

P('Shree-Lipi 7 Odia: how S8Ori764.dll spells Unicode, read from ' + Object.keys(T).length + ' truth strings (ori7-groundtruth.json). Bytes are hex, one per glyph; fonts SHREE-ORI7-06xx (S06xx892.TTF, WORI7001/2.TTF) and SHREE-ORI-06xx (S06xx0F2.TTF) draw them identically except ₹ 3C (WORI7001/2 only). A "check" line counts the truth strings that follow the rule (preferred or accepted spelling).');
P('');
P('1. Internal codes (what each code of a .ORI keyboard means; single-code probes, both composers; - = no answer):');
const single = probe.filter(r => !r.codes.includes(' ')).sort((a, b) => parseInt(a.codes, 16) - parseInt(b.codes, 16));
const line = [];
for (const r of single) if (r.u != null || r.bytes) line.push(`${r.codes}=${r.u == null ? '-' : JSON.stringify(r.u).slice(1, -1)}→${r.bytes || 'nothing'}`);
for (let i = 0; i < line.length; i += 8) P('   ' + line.slice(i, i + 8).join('  '));
P('   64 = link (virama: alone it types nothing; after a letter FA, before a letter the conjunct), 44 = virama+ZWNJ (FA), D5 = virama+ZWJ (FA), 40 = reph (F6), D1 = ra-phala (F7), D2 = ya-phala (BF; the Unicode composer writes ୍ୟ = 0B4D 0B5F), E6 = nukta (the 7 composer types NOTHING), F9/FA = ZWJ/ZWNJ (no glyph).');
P('   The 7 composer types NOTHING for: E4 ୡ, E5 ୱ, E6 nukta, E7/E8/E9 (ୈ ୋ ୌ as one code), EA ଵ, F4 ଓଁ, F6 ୢ, F7 ୣ, F8 ୄ, EB/EC/ED (Vedic), DD ॥, DE ×, DF ÷, 7B —, 7C “, 7D –, 7E ”, 2B +, 3B ;, 3D =, 5B [, 5D ].');
P('   Codes only the 7 composer types (the Unicode composer writes nothing for them): E1 -> FD (a second ba-phala form), E0 -> E3 (a dotted letter), 51 -> 23 (the hook that is also ଖି ଥି ଧି\'s i-sign); the Unicode composer reads 51 after କ as ତ୍କ and after ଖ as ଖି - unreliable, kept as quirks.');
P('');
P('2. Consonants, one glyph each, 17 followed by the blank spacer FF (part of the letter, like Gujarati $): ' + CONS.map(c => `${c} ${letter[c]}`).join(', ') + '. Nukta letters: ଡ଼ ' + pref('ଡ଼') + ', ଢ଼ ' + pref('ଢ଼') + ' (the Unicode composer writes U+0B5C/U+0B5D; NFC text holds ଡ/ଢ + U+0B3C). ୱ = ' + pref('ୱ') + ' (ଓ 48 + the ba-phala stroke B4; typed ଓ + link + ବ, vendor slot 54 64 71). କ୍ଷ = ' + pref('କ୍ଷ') + ' (code 5C; C + link + ଷ gives the visible-halant spelling 4C FF FA CC, accepted).');
P('');
P('3. Vowel signs after a plain consonant: the sign glyph follows the letter (and its spacer): ା E6, ି E7, ୀ EA, ୁ EB, ୂ ED, ୃ F5, ୖ F0, ୗ F2; େ is 7B written BEFORE the cluster; ୈ = 7B ... F0, ୋ = 7B ... E6, ୌ = 7B ... F2 (e-glyph first, the length mark after).');
const SG = { 'ା': 'E6', 'ି': 'E7', 'ୀ': 'EA', 'ୁ': 'EB', 'ୂ': 'ED', 'ୃ': 'F5', 'ୖ': 'F0', 'ୗ': 'F2' };
rule('C + post sign = letter + sign glyph', CONS.flatMap(c => Object.keys(SG).map(s => c + s)), u => letter[u[0]] + ' ' + SG[u[1]]);
rule('C + େ/ୈ/ୋ/ୌ = 7B letter (+ F0/E6/F2)', CONS.flatMap(c => ['େ', 'ୈ', 'ୋ', 'ୌ'].map(s => c + s)), u => '7B ' + letter[u[0]] + ({ 'ୈ': ' F0', 'ୋ': ' E6', 'ୌ': ' F2' }[u[1]] || ''));
P('   exceptions: ି on ଖ ଥ ଧ is the hook 23 (ଖି ' + pref('ଖି') + ', ଥି ' + pref('ଥି') + ', ଧି ' + pref('ଧି') + '). ି + ଁ = the ligature 5D (କିଁ ' + pref('କିଁ') + '), but ଖିଁ = ' + pref('ଖିଁ') + '.');
P('');
P('4. ଂ F3, ଁ F4, ଃ 85 after the sign. Typed twice (ଂ then ଁ ...) the Unicode composer keeps only the first (quirk).');
rule('C + sign + ଂ = (C + sign) + F3', CONS.flatMap(c => ['ା', 'ୀ', 'ୁ', 'ୂ', 'େ', 'ୋ'].map(s => c + s + 'ଂ')), u => pref(u.slice(0, -1)) + ' F3');
P('');
P('5. Conjuncts C1 + ୍ + C2 (typed C1, link 64, C2): the first letter (with its spacer) + the SUBJOINED form of C2. Subjoined glyphs (the spelling of ପ୍C2 without ପ, except where ପ ligates):');
const subOf = {};
for (const c2 of CONS) {
  const tally = new Map();
  for (const c1 of CONS) { const b = T[c1 + V + c2] && T[c1 + V + c2].preferred; if (!b) continue; const L = letter[c1]; if (b.startsWith(L + ' ')) { const s = b.slice(L.length + 1); tally.set(s, (tally.get(s) || 0) + 1); } }
  const best = [...tally].sort((a, b) => b[1] - a[1])[0];
  subOf[c2] = best ? best[0] : null;
}
P('   ' + CONS.map(c => `୍${c} ${subOf[c]}`).join(', '));
P('   (FA + full letter = no subjoined form: Shree writes a visible virama, e.g. ଙ ଞ ଶ ଷ ହ ତ; ୍ଯ is FA BE - the ya-phala is ୍ୟ = BF; ୍ର = F7.)');
const r5 = rule('C1୍C2 = letter(C1) + subjoined(C2)', CONS.flatMap(a => CONS.map(b => a + V + b)), u => letter[u[0]] + ' ' + subOf[u[2]]);
const ligs = [];
for (const a of CONS) for (const b of CONS) { const u = a + V + b; if (!T[u] || b === 'ର' && a === 'ର') continue; if (a === 'ର') continue; const want = letter[a] + ' ' + subOf[b]; if (T[u].preferred !== want) ligs.push(`${u} ${T[u].preferred}`); }
P('   Ligatures (C1୍C2 with its own glyph, ' + ligs.length + '): ' + ligs.join(', '));
P('   ର୍C is never a conjunct: Unicode ର୍C is the reph (rule 8).');
P('');
P('6. A sign after a subjoined form / ligature: ୁ is EC and ୂ is EE (the low forms), ୃ is 26 F5, the ra-phala 26 F7, the virama 26 FA (26 = a zero-width lowering spacer) - but only after a subjoined glyph that hangs below; ା ି ୀ ୖ ୗ ଂ ଁ ଃ do not change.');
const sub2 = CONS.flatMap(a => CONS.map(b => a + V + b)).filter(u => T[u]);
rule('C୍C + ୁ = (C୍C) + EB|EC', sub2.map(u => u + 'ୁ'), u => { const b = pref(u.slice(0, -1)); const t = T[u]; if (!t) return null; return t.preferred === b + ' EC' ? b + ' EC' : b + ' EB'; });
const lowU = sub2.filter(u => T[u + 'ୁ'] && T[u + 'ୁ'].preferred.endsWith(' EC')).length, highU = sub2.filter(u => T[u + 'ୁ'] && T[u + 'ୁ'].preferred.endsWith(' EB')).length;
P(`   ୁ after a C୍C: EC in ${lowU}, EB in ${highU} (EB where the second letter is a full letter after FA, or a ligature that sits on the line).`);
const lowR = sub2.filter(u => T[u + 'ୃ'] && / 26 F5$/.test(T[u + 'ୃ'].preferred)).length, plainR = sub2.filter(u => T[u + 'ୃ'] && / [^6] F5$|^[^ ]+ F5$/.test(T[u + 'ୃ'].preferred) && !/ 26 F5$/.test(T[u + 'ୃ'].preferred)).length;
P(`   ୃ after a C୍C: 26 F5 in ${lowR}, plain F5 in ${plainR}. e.g. କ୍ଟୃ ${pref('କ୍ଟୃ')}, କ୍କୃ ${pref('କ୍କୃ')}, କ୍ଟୁ ${pref('କ୍ଟୁ')}, କ୍ଟ୍ର ${pref('କ୍ଟ୍ର')}, କ୍ଟ୍ ${pref('କ୍ଟ୍')}.`);
P('');
P('7. Phala forms: ra-phala ୍ର F7 (ଶ୍ରୁ ' + pref('ଶ୍ରୁ') + ' and ଶ୍ରୂ ' + pref('ଶ୍ରୂ') + ' = the ra-phala+u ligatures F8/F9), ya-phala ୍ୟ BF (U+0B5F; ଧ୍ୟ = ' + pref('ଧ୍ୟ') + ', accepted ' + JSON.stringify(T['ଧ୍ୟ'] && T['ଧ୍ୟ'].accepted) + '), ba-phala ୍ବ B4 (the stroke; code E1 writes the other form FD, Unicode composer: nothing). C୍ୱ (U+0B71 after a virama) has NO 7 spelling: the composer drops ୱ (C + link + ୱ = C FA).');
rule('C୍ର = letter + F7', CONS.filter(c => c !== 'ର').map(c => c + V + 'ର'), u => letter[u[0]] + ' F7');
rule('C୍ୟ (0B5F) = letter + BF', CONS.map(c => c + V + 'ୟ'), u => letter[u[0]] + ' BF');
rule('C୍ବ = letter + B4', CONS.map(c => c + V + 'ବ'), u => letter[u[0]] + ' B4');
P('');
P('8. Reph ର୍: written F6 AFTER its syllable (the base, its subjoined forms and its signs), before ଂ ଁ ଃ (sign, reph, tail - preferred; tail then reph F3 F6 is accepted); ି + reph = the ligature 7D, ୖ + reph = F1. Typed reph-first (F6 before the letter) the mark floats over the previous glyph (alt). ର + link + C is ର with C subjoined (C0 FF + subjoined), not a reph (alt).');
rule('ର୍C = letter(C) + F6', CONS.map(c => 'ର' + V + c), u => letter[u[2]] + ' F6');
rule('ର୍C + sign = (C + sign) + F6 (ି: 7D, ୖ: F1)', CONS.flatMap(c => ['ା', 'ୀ', 'ୁ', 'ୂ', 'େ', 'ୋ', 'ୌ', 'ୗ'].map(s => 'ର' + V + c + s)), u => pref(u.slice(2)) + ' F6');
rule('ର୍C + ି = letter + 7D', CONS.map(c => 'ର' + V + c + 'ି'), u => letter[u[2]] + ' 7D');
rule('ର୍C୍C = (C୍C) + F6', CONS.flatMap(a => ['କ', 'ତ', 'ପ', 'ଟ', 'ମ', 'ଯ'].map(b => 'ର' + V + a + V + b)), u => pref(u.slice(2)) + ' F6');
P('   ର୍ at a word end = ' + pref('ର୍') + ' (ର + visible virama); the lone reph key (code 40) writes F6 alone (alt).');
P('');
P('9. Virama: word-final C୍ = letter + FA (କ୍ ' + pref('କ୍') + '); C୍‌ (ZWNJ, code 44) and C୍‍ (ZWJ, code D5) are the same visible FA, then the next letter in full (କ୍‌ତ ' + pref('କ୍‌ତ') + ').');
rule('C୍ = letter + FA', CONS.filter(c => c !== 'ର').map(c => c + V), u => letter[u[0]] + ' FA');
rule('C1୍‌C2 = letter(C1) FA letter(C2)', CONS.flatMap(a => ['କ', 'ତ', 'ପ', 'ମ', 'ନ'].map(b => a + V + '‌' + b)), u => letter[u[0]] + ' FA ' + letter[u[3]]);
P('');
P('10. Independent vowels: ' + ['ଅ', 'ଆ', 'ଇ', 'ଈ', 'ଉ', 'ଊ', 'ଋ', 'ୠ', 'ଌ', 'ଏ', 'ଐ', 'ଓ', 'ଔ'].map(v => `${v} ${pref(v)}`).join(', ') + '. ୡ has no 7 spelling. ଆ is typed ଅ + ା (41 E6).');
P('');
P('11. Digits ' + '୦୧୨୩୪୫୬୭୮୯'.split('').map(d => d + pref(d)).join(' ') + '; । ' + pref('।') + ', ୰ ' + pref('୰') + ' (byte 8F: the fonts map it at U+008F), ଽ ' + pref('ଽ') + ' (byte 90: U+0090), ₹ ' + pref('₹') + ' (WORI7001/2 only), ‘ ' + pref('‘') + ', ’ ' + pref('’') + '. No 7 spelling (the composer types nothing): ॥ × ÷ — – “ ” + ; = [ ] ଓଁ ୄ ୢ ୣ and the Vedic marks.');
P('');
// 12. syllables in words
const words = probe.filter(r => /^(କି|କା|କେ|କ reph|କୁ|କଂ) \S+$/.test(r.tag) && r.u && r.bytes);
let wOk = 0; const wMiss = [];
const headOf = { 'କି': 'କି', 'କା': 'କା', 'କେ': 'କେ', 'କ reph': 'ର୍କ', 'କୁ': 'କୁ', 'କଂ': 'କଂ' };
for (const r of words) { const m = r.tag.match(/^(.+) (\S+)$/); const want = [pref(headOf[m[1]]), pref(m[2])].join(' '); if (want === r.bytes) wOk++; else if (wMiss.length < 6) wMiss.push(`${r.tag}: ${r.bytes} vs ${want}`); }
P(`12. Syllables join by concatenation: ${wOk}/${words.length} two-syllable probes (କି/କା/କେ/ର୍କ/କୁ/କଂ + every consonant) are exactly the two syllables' own preferred bytes${wMiss.length ? '; not: ' + wMiss.join('; ') : ''}.`);
P('');
P('13. Not in the truth (4 codes per key): C୍C + ୈ/ୋ/ୌ, C୍C୍ର / C୍C୍ୟ + a sign (ମନ୍ତ୍ରୀ, ସ୍ତ୍ରୀ, ସନ୍ଧ୍ୟା), three-consonant clusters other than C୍C୍ର/C୍C୍ୟ, reph + C୍C + sign; C୍ୱ (no spelling at all). Rules 3, 6, 7 and 8 predict them (sign glyphs append to the cluster; the reph goes last).');
P('');
P('14. Composer quirks and other typing orders (kept in pairs with kind quirk:* / alt:*, not used as truth):');
for (const [k, v] of Object.entries(g.counts.byKind)) if (k.startsWith('quirk:') || k.startsWith('alt:')) P(`   ${String(v).padStart(5)}  ${k}`);
fs.writeFileSync(__dirname + '/ori7-patterns.txt', out.join('\n') + '\n');
console.log(out.join('\n'));
