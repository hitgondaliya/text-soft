// Write kan7-patterns.txt: the rules of Shree's Kannada 7 spelling, each checked against the truth (counts + examples).
// (adapted from ../cap-tel/patterns.js)
const fs = require('fs');
const g = require('./kan7-groundtruth.json');
const T = g.truth;
const probe = require('./capp/P-results.json');
const pBy = new Map(probe.map(r => [r.codes, r]));
const out = [];
const P = s => out.push(s);
const pref = u => T[u] ? T[u].preferred : null;
const V = '್';
const CONS = [...'ಕಖಗಘಙಚಛಜಝಞಟಠಡಢಣತಥದಧನಪಫಬಭಮಯರಱಲಳವಶಷಸಹೞ'];
const SIGNS = ['ಾ', 'ಿ', 'ೀ', 'ು', 'ೂ', 'ೃ', 'ೄ', 'ೆ', 'ೇ', 'ೈ', 'ೊ', 'ೋ', 'ೌ'];
const totals = { ok: 0, n: 0 };
function rule(name, strings, fn) {
  let n = 0, ok = 0; const miss = [];
  for (const u of strings) { const want = fn(u); if (want == null || !T[u]) continue; n++; if (T[u].preferred === want || T[u].accepted.includes(want)) ok++; else if (miss.length < 8) miss.push(`${u} rule ${want} / truth ${T[u].preferred}`); }
  P(`  check "${name}": ${ok}/${n} truth strings follow it${miss.length ? '; exceptions (first 8): ' + miss.join('; ') : ''}`);
  totals.ok += ok; totals.n += n;
  return { ok, n };
}
P('Shree-Lipi 7 Kannada: how S8Kan764.dll spells Unicode, read from ' + Object.keys(T).length + ' truth strings (kan7-groundtruth.json). Bytes are hex, one per glyph; the 7 faces (SHREE-KAN7-0850..0855/0899, Shree-Kan7-001/002) and the 4/5/6 faces (SHREE-KAN-0850..0855/0899) have the same 215 codes, and the same design number draws identical glyphs in both families. A "check" line counts the truth strings that follow the rule (preferred or accepted spelling).');
P('');
P('1. Internal codes (what each code of a .KAN keyboard means; single-code probes under a neutral keyboard name, both composers; "-" = no answer):');
const single = probe.filter(r => !r.codes.includes(' ')).sort((a, b) => parseInt(a.codes, 16) - parseInt(b.codes, 16));
const line = [];
for (const r of single) if (r.u != null || r.bytes) line.push(`${r.codes}=${r.u == null ? '-' : JSON.stringify(r.u).slice(1, -1)}→${r.bytes || 'nothing'}`);
for (let i = 0; i < line.length; i += 8) P('   ' + line.slice(i, i + 8).join('  '));
P('   64 = link (virama; before a consonant the conjunct: C1 + the subjoined form of C2; at a word end or before a space the 7 composer writes NO virama), 44 = virama + ZWNJ (the visible virama glyph 85 in place of the head DC), 4B = the reph (arkavattu EC) struck AFTER its syllable, 76 = the length mark (ೀ = ಿ + 76, ೇ = ೆ + 76, ೋ = ೊ + 76), D3 = nukta (82 after a letter), 5E/5C = ZWJ/ZWNJ (no glyph; ignored by the 7 composer after the link). F0-F3 (precomposed ೈ ೀ ೇ ೋ, TYPEWRIT only): the 7 composer writes nothing. A lone vowel-sign code makes the 7 composer write the INDEPENDENT vowel glyph (61 ಾ → 42 = ಆ).');
P('   The 7 composer types NOTHING for: D9 ೢ, DA ೣ, E9 ಌ, DE ॥, F0-F3. Codes only the 7 composer types (the Unicode composer writes nothing): D7 → 23, 86 → 2D, E6 → 89, E7 → 87, E8 → 88, EA → 8B (9B after a letter), EB → 7D, EE → 9A (fonts/only7.png: glyph pieces, 9A = a second ಙ glyph).');
P('   GLYPH CHECK (fonts/punct-check.png, fonts/all-codes-3faces.png, every installed face): the composer bytes for some symbols do not draw the symbol: ಽ (D4) → 98 = the nukta-on-carrier glyph (the avagraha glyph is 97); — (7B) → AC = an empty 100-unit glyph; – (7D) → B6 = a ಫ variant; ÷ (DD) → 8A = a zero-width curve; ₹ (DB) → 3C = no glyph in any Kannada face; codes 28/29: the Unicode composer writes [ ] but 28/29 draw ( ) (no face has [ ]). × (DC) → 8D, । (7E) → 3E, ‘ ’ → 22 27 are right.');
P('');
P('2. Consonants: the body glyph + the talakattu head DC (zero-width), or one glyph for letters drawn without a separate head:');
P('   ' + CONS.map(c => `${c} ${pref(c)}`).join(', '));
P(`   ಕ್ಷ = ${pref('ಕ್ಷ')} (code E1; C + link + ಷ = ${T['ಕ್ಷ'] ? T['ಕ್ಷ'].accepted.slice(1).join(', ') : '-'}, accepted); ಜ್ಞ = ${pref('ಜ್ಞ')} (code E0; also ${T['ಜ್ಞ'] ? T['ಜ್ಞ'].accepted.slice(1).join(', ') : '-'}); ಶ್ರೀ = ${pref('ಶ್ರೀ')} (code D8).`);
P('');
P('3. Vowel signs (gunitakshara): ಾ ೆ ೌ REPLACE the head DC (ಕಾ 50 DD, ಕೆ 50 E6, ಕೌ 50 E8); ಿ is one glyph per letter (ಕಿ 51); ು ೂ ೃ ೄ follow the letter + head (ಕು 50 DC E1; ಪ ಫ ವ take the second u-shape E2 / E4); ೈ = ೆ + E7; ೊ = ೆ + E3/E4; the length mark E0 makes ೀ ೇ ೋ. Full table:');
for (const c of CONS) P(`   ${c}: ` + SIGNS.map(s => `${s} ${pref(c + s) || '-'}`).join(' | '));
const plain = c => pref(c);
rule('C + ು = C + E1 (or E2)', CONS.map(c => c + 'ು'), u => { const b = plain(u[0]); if (!b) return null; const a = b + ' E1', z = b + ' E2'; return T[u] && T[u].accepted.includes(z) ? z : a; });
rule('C + ೂ = C + E3 (or E4)', CONS.map(c => c + 'ೂ'), u => { const b = plain(u[0]); if (!b) return null; const a = b + ' E3', z = b + ' E4'; return T[u] && T[u].accepted.includes(z) ? z : a; });
rule('C + ೃ = C + EA', CONS.map(c => c + 'ೃ'), u => plain(u[0]) ? plain(u[0]) + ' EA' : null);
rule('C + ೄ = C + EB', CONS.map(c => c + 'ೄ'), u => plain(u[0]) ? plain(u[0]) + ' EB' : null);
rule('C + ೀ = (C + ಿ) + E0', CONS.map(c => c + 'ೀ'), u => pref(u[0] + 'ಿ') ? pref(u[0] + 'ಿ') + ' E0' : null);
rule('C + ೇ = (C + ೆ) + E0', CONS.map(c => c + 'ೇ'), u => pref(u[0] + 'ೆ') ? pref(u[0] + 'ೆ') + ' E0' : null);
rule('C + ೋ = (C + ೊ) + E0', CONS.map(c => c + 'ೋ'), u => pref(u[0] + 'ೊ') ? pref(u[0] + 'ೊ') + ' E0' : null);
rule('C + ೈ = (C + ೆ) + E7', CONS.map(c => c + 'ೈ'), u => pref(u[0] + 'ೆ') ? pref(u[0] + 'ೆ') + ' E7' : null);
P('');
P('4. ಂ ಃ after the letter/sign: + ED / 40.');
rule('C(+sign) + ಂ = (C + sign) + ED', CONS.flatMap(c => ['', ...SIGNS].map(s => c + s + 'ಂ')), u => pref(u.slice(0, -1)) ? pref(u.slice(0, -1)) + ' ED' : null);
rule('C(+sign) + ಃ = (C + sign) + 40', CONS.flatMap(c => ['', ...SIGNS].map(s => c + s + 'ಃ')), u => pref(u.slice(0, -1)) ? pref(u.slice(0, -1)) + ' 40' : null);
P('');
P('5. Reph (arkavattu) ರ್ + C...: typed AFTER the syllable (code 4B); the 7 composer writes the syllable, then EC, then ಂ ಃ. ರ್ಕ = 50 DC EC, ರ್ಕಾ = 50 DD EC, ರ್ಕಿಂ = 51 EC ED. The other orders are alternatives (alt): reph before the sign (50 DC EC E1 for ರ್ಕು: the arkavattu between the letter and its sign; for ಾ ಿ the sign shape is lost), reph before the letter (EC 50 DC), tail before the reph (50 DD ED EC). ರ + link + C (C3 DC 52) is the non-arkavattu form (Unicode ರ್‍ಕ), kept as alt.');
rule('ರ್ + syllable = syllable + EC (before ಂ ಃ)', Object.keys(T).filter(u => /^ರ್[ಕ-ಹೞ](್[ಕ-ಹೞ])?[಼]?[ಾ-ೌ]?[ಂಃ]*$/.test(u)), u => { const rest = u.slice(2); const m = /^(.*?)([ಂಃ]*)$/.exec(rest); const b = pref(m[1]); if (!b) return null; return b + ' EC' + (m[2] ? ' ' + [...m[2]].map(t => t === 'ಂ' ? 'ED' : '40').join(' ') : ''); });
P('');
P('6. Conjuncts C1 + ್ + C2 (link code 64): C1 as written alone (with its head DC), then the subjoined form (ottakshara) of C2. Subjoined glyphs (read from ಕ್C2 = 50 DC + sub):');
const sub = {};
for (const c of CONS) { const b = pref('ಕ' + V + c); if (b && b.startsWith('50 DC ')) sub[c] = b.slice(6); }
P('   ' + Object.entries(sub).map(([c, b]) => `್${c} ${b}`).join(', '));
P('   ್ೞ: no subjoined form (C + link + ೞ = C alone in 7). C + link + ಕ್ಷ (E1): both composers drop the ಕ್ಷ, so C್ಕ್ಷ is not in the truth.');
const pairsCC = CONS.filter(c => c !== 'ರ').flatMap(a => CONS.map(b => a + V + b)); // ರ್C is the reph (section 5)
rule('C1್C2 = C1 + sub(C2)', pairsCC, u => pref(u[0]) && sub[u[2]] ? pref(u[0]) + ' ' + sub[u[2]] : null);
P('   A sign on a cluster shapes C1 as it would alone and the subjoined form follows; ೃ ೄ ೈ fuse with or follow the subjoined form (ಕ್ಕೃ = 50 DC A2, ಕ್ತೃ = 50 DC F5, ಕ್ಖೃ = 50 DC 56 24 EA; ಕ್ತೈ = 50 E6 F4, ಕ್ಕೈ = 50 E6 52 24 E7):');
for (const s of ['ಾ', 'ಿ', 'ು', 'ೂ', 'ೆ', 'ೊ', 'ೌ']) rule(`C1್C2 + ${s} = (C1 + ${s}) + sub(C2)`, pairsCC.map(u => u + s), u => pref(u[0] + s) && sub[u[2]] ? pref(u[0] + s) + ' ' + sub[u[2]] : null);
rule('C1್C2 + ೃ = C1 + sub(C2) + 24 EA (unless fused)', pairsCC.map(u => u + 'ೃ'), u => pref(u[0]) && sub[u[2]] ? pref(u[0]) + ' ' + sub[u[2]] + ' 24 EA' : null);
rule('C1್C2 + ೈ = (C1 + ೆ) + sub(C2) + 24 E7 (unless fused)', pairsCC.map(u => u + 'ೈ'), u => pref(u[0] + 'ೆ') && sub[u[2]] ? pref(u[0] + 'ೆ') + ' ' + sub[u[2]] + ' 24 E7' : null);
rule('C1್C2 + ಂ = C1್C2 + ED', pairsCC.map(u => u + 'ಂ'), u => pref(u.slice(0, 3)) ? pref(u.slice(0, 3)) + ' ED' : null);
rule('ರ್ + C1್C2 = C1್C2 + EC', pairsCC.map(u => 'ರ್' + u), u => pref(u.slice(2)) ? pref(u.slice(2)) + ' EC' : null);
{
  const fused = [];
  for (const u of pairsCC) { const t = T[u + 'ೃ']; if (t && pref(u[0]) && sub[u[2]] && t.preferred !== pref(u[0]) + ' ' + sub[u[2]] + ' 24 EA') fused.push(`${u}ೃ ${t.preferred}`); }
  P('   ೃ fused or placed otherwise (' + fused.length + '): ' + fused.slice(0, 40).join(', ') + (fused.length > 40 ? ' ...' : ''));
}
P('   ೀ ೇ ೋ on a cluster need 5 codes (C1 64 C2 + sign + 76): not probed; derived as (C1್C2 + ಿ/ೆ/ೊ) + E0 in kan7-composer-gaps.json, the rule all ' + g.counts.lengthMarkRule + ' single-letter cases follow.');
P('');
P('7. Halant (code 44 = virama + ZWNJ): the head DC (or the letter\'s own top) becomes the virama glyph 85: ಕ್‌ = 50 85; the bare Unicode virama at a word end (C್) has NO direct composer spelling (the pending link writes no virama): kan7-composer-gaps.json derives it from the halant spelling.');
P('   (letters without a separate head take a letter-specific top before 85: ಖ್‌ 54 85, ಜ್‌ 68 85, ಮ್‌ CA DC E5 85; full list below)');
P('   ' + CONS.map(c => c + '್‌ ' + (pref(c + V + '‌') || '-')).join(', '));
rule('C್‌ = C (without the head DC) + 85', CONS.map(c => c + V + '‌'), u => pref(u[0]) ? pref(u[0]).replace(/ DC$/, '') + ' 85' : null);
P('');
P('8. Three-consonant clusters C1್C2್C3 (5 codes, not probed whole): the pieces "link C2 link C3" were probed (4 codes) and give the subjoined pair, e.g. ್ತ್ರ = F7 (one glyph), ್ತ್ಯ = F6. C1 + those bytes is the derived spelling (not truth):');
{
  const ex = [];
  for (const r of probe) if (/^link \S+ link \S+$/.test(r.tag) && r.bytes) ex.push(`${r.u} ${r.bytes}`);
  P('   ' + ex.slice(0, 80).join(', ') + (ex.length > 80 ? ` ... (${ex.length})` : ''));
}
P('');
P('9. Syllables join by plain concatenation (two-syllable probes, P set: ಕಿ/ಕಾ/ಕೆ/ಕು/ಕಂ/ಕ್ಕ/ರ್ಕ/ರ್ಕಾ/ಕ್‌/ಕೀ/ಕೈ/ಕೊ + C, Cಾ + ಕ, ರ್C + ಕ):');
{
  let n = 0, ok = 0; const miss = [];
  const firsts = { 'ಕಿ': '65 73', 'ಕಾ': '65 61', 'ಕೆ': '65 62', 'ಕು': '65 6E', 'ಕಂ': '65 3C', 'ಕ್ಕ': '65 64 65', 'ರ್ಕ': '65 4B', 'ರ್ಕಾ': '65 61 4B', 'ಕ್‌': '65 44', 'ಕೀ': '65 73 76', 'ಕೈ': '65 6D', 'ಕೊ': '65 70' };
  const C = require('./expected').C;
  for (const [code, letter] of Object.entries(C)) {
    if (letter.length > 1) continue;
    const ch = Number(code).toString(16).toUpperCase().padStart(2, '0');
    for (const [syl, codes] of Object.entries(firsts)) {
      const r = pBy.get(codes + ' ' + ch); if (!r || !r.bytes || !pref(syl) || !pref(letter)) continue;
      n++; const want = pref(syl) + ' ' + pref(letter);
      if (r.bytes === want) ok++; else if (miss.length < 10) miss.push(`${syl}+${letter}: ${r.bytes} vs ${want}`);
    }
  }
  P(`  check "syllable + letter = concatenation": ${ok}/${n}${miss.length ? '; exceptions: ' + miss.join('; ') : ''}`);
}
P('');
P('10. Nukta (D3): C + 82 (ಕ಼ = 50 DC 82); before a sign the 7 composer keeps the head and adds the sign (ಕ಼ಾ = 50 DC 82 DD, ಕ಼ೆ = 50 E6 82); before ಿ (all letters), ೊ (all), and ಾ/ೌ on letters without a separate head (ಖ ಘ ಜ ಮ ...) it garbles (the letter twice, or the nukta lost): quirk, not truth.');
P('');
P(`Rule checks overall: ${totals.ok}/${totals.n}.`);
fs.writeFileSync(__dirname + '/kan7-patterns.txt', out.join('\n'));
console.log(out.filter(l => /check "/.test(l)).join('\n'));
