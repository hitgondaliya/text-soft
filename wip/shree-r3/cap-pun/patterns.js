// Write pun7-patterns.txt: the rules of Shree's Gurmukhi 7 spelling, each checked against the truth (counts + examples).
const fs = require('fs');
const g = require('./pun7-groundtruth.json');
const T = g.truth;
const probe = [...require('./capp/P-results.json'), ...require('./caph/H-results.json')];
const out = [];
const P = s => out.push(s);
const cps = s => [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const pref = u => T[u] ? T[u].preferred : '(none)';
const CONS = 'ਕਖਗਘਙਚਛਜਝਞਟਠਡਢਣਤਥਦਧਨਪਫਬਭਮਯਰਲਵੜਸਹ';
const SIGNS = ['ਾ', 'ਿ', 'ੀ', 'ੁ', 'ੂ', 'ੇ', 'ੈ', 'ੋ', 'ੌ'];
// check a rule over many strings: fn(u) -> expected bytes or null (not applicable); report agree/total and misses
function rule(name, strings, fn) {
  let n = 0, ok = 0; const miss = [];
  for (const u of strings) { const want = fn(u); if (want == null || !T[u]) continue; n++; if (T[u].preferred === want) ok++; else if (miss.length < 6) miss.push(`${u} want-by-rule ${want} truth ${T[u].preferred}`); }
  P(`  rule check: ${ok}/${n} truth strings follow it${miss.length ? '; exceptions: ' + miss.join('; ') : ''}`);
}
const letter = {}; for (const c of CONS) letter[c] = pref(c);
const sign = {}; for (const s of SIGNS) sign[s] = pref('ਕ' + s);

P('Shree-Lipi 7 Punjabi (Gurmukhi): how S8Pun764.dll spells Unicode, read from ' + Object.keys(T).length + ' truth strings (pun7-groundtruth.json). Bytes are hex, one per glyph; fonts WPUN7001/2 (7) and S09xx0F2 (4/5/6) draw them identically except ₹ 3C (7 only).');
P('');
P('1. Internal codes (what each code of a .PUN keyboard means; single-code probes, both composers):');
const single = probe.filter(r => !r.codes.includes(' ')).sort((a, b) => parseInt(a.codes, 16) - parseInt(b.codes, 16));
const line = [];
for (const r of single) line.push(`${r.codes}=${r.u == null ? '-' : JSON.stringify(r.u).slice(1, -1)}→${r.bytes || (r.lUnits === -256 ? 'nothing' : '-')}`);
for (let i = 0; i < line.length; i += 8) P('   ' + line.slice(i, i + 8).join('  '));
P('   (- = no answer. 41 and EE answer only in the 7 composer: 41→40 an avagraha-like ऽ glyph, EE→A9 a short low dash. 64 = link (virama with no glyph), 44 = halant (B5), D7 = nukta, E2/E3 = ZWJ/ZWNJ, D1 D2 D0 4C = subjoined ੍ਰ ੍ਹ ੍ਵ ੍ਯ (6F AA AB 62), 59 = ੜ (6D), 26 = ਸ (67). Codes the 7 composer types NOTHING for: 64 (link), D7 (nukta, except on the 6 nukta letters), E2/E3 (ZWJ/ZWNJ), E4 (ਇ), E0 (ੴ), 7C “, 7E ”, D9 ॥.)');
P('');
P('2. Consonants: one byte each: ' + CONS.split('').map(c => `${c}${letter[c]}`).join(' '));
P('   ਠ (53) and ਨ (B2) are followed by the spacer BE (advance 120): ਠ = 53 BE, ਨ = B2 BE. Marks that sit on top of the letter (ੇ ੈ ੋ ੌ ਂ ੰ ੱ and ੇਂ ੈਂ ੋਂ ੌਂ) go BEFORE BE; ਾ ੀ ੀਂ, ੁ ੂ, the halant B5 and a subjoined form go AFTER it, ੁ ੂ as the variants AE AF there.');
rule('top marks before BE on ਠ/ਨ', ['ਠੇ', 'ਠੈ', 'ਠੋ', 'ਠੌ', 'ਠਂ', 'ਠੰ', 'ਠੱ', 'ਨੇ', 'ਨੈ', 'ਨੋ', 'ਨੌ', 'ਨਂ', 'ਨੰ', 'ਨੱ'], u => { const m = { 'ੇ': '79', 'ੈ': 'B1', 'ੋ': '7D', 'ੌ': 'A1', 'ਂ': 'A3', 'ੰ': 'A5', 'ੱ': 'A7' }[u[1]]; return (u[0] === 'ਠ' ? '53' : 'B2') + ' ' + m + ' BE'; });
rule('ੁ ੂ after BE as AE AF on ਠ/ਨ', ['ਠੁ', 'ਠੂ', 'ਨੁ', 'ਨੂ'], u => (u[0] === 'ਠ' ? '53' : 'B2') + ' BE ' + (u[1] === 'ੁ' ? 'AE' : 'AF'));
rule('subjoined after BE on ਠ/ਨ', ['ਠ੍ਰ', 'ਠ੍ਹ', 'ਠ੍ਵ', 'ਠ੍ਯ', 'ਨ੍ਰ', 'ਨ੍ਹ', 'ਨ੍ਵ', 'ਨ੍ਯ'], u => (u[0] === 'ਠ' ? '53' : 'B2') + ' BE ' + { 'ਰ': '6F', 'ਹ': 'AA', 'ਵ': 'AB', 'ਯ': '62' }[u[2]]);
P('   e.g. ਨੂੰ [0A28 0A42 0A70] = ' + pref('ਨੂੰ') + ' (tippi before BE, ੂ after it); typed tippi-first (vendor key, Unicode ਨੰੂ 0A28 0A70 0A42) = ' + pref('ਨੰੂ'));
P('');
P('3. Vowel signs (after ਕ): ' + SIGNS.map(s => `${s}${sign[s].replace(/^48 | 48$/, '').replace('71 48', '71')}`).join(' ') + '. The i-sign 71 is written BEFORE its consonant cluster (Unicode order: after it).');
rule('ਿ first', CONS.split('').map(c => c + 'ਿ'), u => '71 ' + letter[u[0]]);
rule('C + sign = letter + sign byte', CONS.split('').flatMap(c => SIGNS.filter(s => s !== 'ਿ').map(s => c + s)), u => letter[u[0]] + ' ' + sign[u[1]].split(' ').slice(1).join(' '));
P('');
P('4. Bindi/tippi: ਂ A3, ੰ A5. After ੀ ੇ ੈ ੋ ੌ the bindi joins the sign: ੀਂ 73 ੇਂ 7A ੈਂ 7C ੋਂ 7E ੌਂ A2 (ਾਂ stays 70 A3). After ਉ ਊ the bindi is A4 (to the right of the curl); ੳ + bindi = 44. Both composers write a tippi typed after ਾ ੀ ੇ ੈ ੋ ੌ ਉ ਊ ੳ as bindi ਂ (U+0A02).');
rule('sign + bindi ligatures', CONS.split('').flatMap(c => ['ੀ', 'ੇ', 'ੈ', 'ੋ', 'ੌ'].map(s => c + s + 'ਂ')), u => (letter[u[0]].split(' ')[0]) + ' ' + { 'ੀ': '73', 'ੇ': '7A', 'ੈ': '7C', 'ੋ': '7E', 'ੌ': 'A2' }[u[1]] + (letter[u[0]].includes('BE') ? ' BE' : ''));
P('   ਉਂ = ' + pref('ਉਂ') + ', ਊਂ = ' + pref('ਊਂ') + ', ੳਂ = ' + pref('ੳਂ') + ', ਕੀਂ = ' + pref('ਕੀਂ') + ', ਈਂ = ' + pref('ਈਂ'));
P('');
P('5. Addak ੱ: A7 right after a letter or a low/side sign (ਕੱ ' + pref('ਕੱ') + ', ਕੁੱ ' + pref('ਕੁੱ') + ', ਕਿੱ ' + pref('ਕਿੱ') + ', ਕਾੱ ' + pref('ਕਾੱ') + '); A8 (further right) after a mark that reaches the top: ੀ ੇ ੈ ੋ ੌ ਂ ੰ and after ਉ ਊ ੳ (ਉੱ ' + pref('ਉੱ') + ', ਕੀੱ ' + pref('ਕੀੱ') + ', ਕਂੱ ' + pref('ਕਂੱ') + '). Typed ੱ then ਂ, the Unicode composer writes ਁ U+0A01 (ਕਁ = ' + pref('ਕਁ') + ').');
rule('A8 after top marks', CONS.split('').filter(c => c !== 'ਠ' && c !== 'ਨ').flatMap(c => ['ੀ', 'ੇ', 'ੈ', 'ੋ', 'ੌ'].map(s => c + s + 'ੱ')), u => letter[u[0]] + ' ' + sign[u[1]].split(' ').slice(1).join(' ') + ' A8');
P('');
P('6. Subjoined (pairin) forms: ੍ਰ 6F, ੍ਹ AA, ੍ਵ AB, ੍ਯ 62 after the letter; ਕ੍ਰ is the ligature 6E. Unicode C+੍+ਰ/ਹ/ਵ/ਯ is always the subjoined form (typing link 64 + ਰ gives two full letters 48 63, kept as alt).');
rule('C੍ਰ = C 6F (ਕ: 6E)', CONS.split('').filter(c => c !== 'ਠ' && c !== 'ਨ').map(c => c + '੍ਰ'), u => u[0] === 'ਕ' ? '6E' : letter[u[0]] + ' 6F');
P('   ਸ੍ਤ੍ਰ = ' + pref('ਸ੍ਤ੍ਰ') + ', ਕ੍ਰਿ = ' + pref('ਕ੍ਰਿ') + ', ਪ੍ਰੇ = ' + pref('ਪ੍ਰੇ') + ', ਦ੍ਵਾ = ' + pref('ਦ੍ਵਾ') + ', ਕ੍ਯਾ = ' + pref('ਕ੍ਯਾ'));
P('');
P('7. Virama before a letter that has no subjoined form: typed with the link 64 the 7 composer writes NO glyph (ਕ੍ਤ = 48 57, the bytes of ਕਤ); typed with the halant 44 it writes B5 (ਕ੍ਤ = 48 B5 57). preferred = B5 (see about); both accepted. On ਠ/ਨ the halant goes after BE (ਨ੍ਦ = ' + pref('ਨ੍ਦ') + '). Word-final ਕ੍ = ' + pref('ਕ੍') + ' (link: 48). ZWJ/ZWNJ have no glyph (ਕ੍‍ = ' + pref('ਕ੍‍') + ', ਕ੍‌ = ' + pref('ਕ੍‌') + ').');
rule('C੍C = C B5 C (halant)', CONS.split('').filter(c => !'ਠਨ'.includes(c)).flatMap(a => 'ਕਤਦਮਪ'.split('').map(b => a + '੍' + b)), u => letter[u[0]] + ' B5 ' + letter[u[2]]);
P('   i-sign over a cluster: ਕ੍ਤਿ = ' + pref('ਕ੍ਤਿ') + ' (71 before the whole cluster).');
P('');
P('8. Nukta letters: one byte each: ਸ਼ ' + pref('ਸ਼') + ' ਖ਼ ' + pref('ਖ਼') + ' ਗ਼ ' + pref('ਗ਼') + ' ਜ਼ ' + pref('ਜ਼') + ' ਫ਼ ' + pref('ਫ਼') + ' ਲ਼ ' + pref('ਲ਼') + ' (the Unicode composer writes the precomposed code points; NFC text holds letter + U+0A3C). Other letters have no nukta glyph (ਕ਼ → 48, kept as quirk). ਡ + nukta is written ੜ (U+0A5C) by the Unicode composer (quirk).');
P('');
P('9. Independent vowels: ' + ['ਅ', 'ਆ', 'ਇ', 'ਈ', 'ਉ', 'ਊ', 'ਏ', 'ਐ', 'ਓ', 'ਔ', 'ੲ', 'ੳ'].map(v => `${v} ${pref(v)}`).join(', ') + '. ਇ is ਿ + ੲ (71 42; the code E4 types nothing in 7).');
P('');
P('10. ੴ = ' + pref('ੴ') + ' (੧ + the onkar arc B0). Code 51 alone writes B0, which every font draws WITHOUT the ੧ (kept as alt).');
P('');
P('11. Digits ' + '੦੧੨੩੪੫੬੭੮੯'.split('').map(d => d + pref(d)).join(' ') + '; punctuation: । ' + pref('।') + ', ‘ ' + pref('‘') + ', ’ ' + pref('’') + ', — ' + pref('—') + ', – ' + pref('–') + ', × ' + pref('×') + ', ÷ ' + pref('÷') + ', ₹ ' + pref('₹') + ' (no glyph in 4/5/6); ॥ “ ” have NO 7 spelling (the composer types nothing).');
P('');
// 12. syllables in words
const words = probe.filter(r => /^(ਕਿ |ਕਿੱ |ਪੱ |ਕੀ )/.test(r.tag) && r.u && r.bytes);
let wOk = 0; const wMiss = [];
const conc = { 'ਕਿ': '71 48', 'ਕਿੱ': '71 48 A7', 'ਕੀ': '48 72' };
for (const r of words) {
  const [head, c2, tail] = r.tag.split(' ');
  let want;
  if (head === 'ਪੱ') want = ['5C A7', pref(c2 + 'ਾ')].join(' ');
  else want = [conc[head], pref(c2)].join(' ');
  if (want === r.bytes) wOk++; else if (wMiss.length < 6) wMiss.push(`${r.tag}: ${r.bytes} vs concatenation ${want}`);
}
P(`12. Syllables join by concatenation: ${wOk}/${words.length} word probes (ਕਿ+C, ਕਿੱ+C, ਪੱ+C+ਾ, ਕੀ+C) are exactly the syllables' own bytes${wMiss.length ? '; not: ' + wMiss.join('; ') : ''}.`);
P('');
P('13. Composer quirks (kept in pairs with kind quirk:*, not used as truth):');
for (const [k, v] of Object.entries(g.counts.byKind)) if (k.startsWith('quirk:') || k.startsWith('alt:')) P(`   ${String(v).padStart(5)}  ${k}`);
fs.writeFileSync(__dirname + '/pun7-patterns.txt', out.join('\n') + '\n');
console.log(out.join('\n'));
