// measured/common-words.tsv (the real FontConverter on 341 common Tamil words vs Shree's syllable spellings joined,
// which round 2 showed is exactly what the 7 composer writes) re-read by glyph -> measured/common-words-classes.json
const fs = require('fs');
const D = __dirname;
const EQ = { B4: '23', AD: '26', F7: '40', '8C': '5C', '94': '5F', '8B': '60' };
const canon = s => s.split(' ').filter(Boolean).map(t => EQ[t] || t).join(' ');
const sorted = s => s.split(' ').filter(Boolean).sort().join(' ');
const lines = fs.readFileSync(D + '/measured/common-words.tsv', 'utf8').split('\n').slice(1).filter(Boolean);
const out = { words: 0, exact: 0, glyphEqual: 0, visiblyWrong: 0, notCoverable: [], byReason: {}, wrong: [] };
for (const l of lines) {
  const [w, cps, ours, shree, syl, ok] = l.split('\t');
  if (/not coverable/.test(shree)) { out.notCoverable.push(w + ' [' + cps + ']'); continue; }
  out.words++;
  if (ok === '1') { out.exact++; continue; }
  if (canon(ours) === canon(shree)) { out.glyphEqual++; continue; }
  const modern = s => canon(s).split(' ').map(t => ({ E0: 'DA F5', D9: 'D3 F5', '74': '6E F5' })[t] || t).join(' ');
  if (modern(ours) === modern(shree)) { out.oldOrthographyAccepted = (out.oldOrthographyAccepted || 0) + 1; out.wrong.push(`${w}: ours ${ours} | Shree ${shree} | ACCEPTED: the pre-1978 ligature (E0 னா / D9 றா / 74 ணா) that Shree's own னா/றா/ணா keys type; Shree writes the modern form when ா is struck after the letter`); continue; }
  out.visiblyWrong++;
  const reasons = [];
  if (/U\+0BCC/.test(ours)) reasons.push('ௌ unmapped');
  if (/[ொோ]/.test(w)) reasons.push('ொ/ோ: pre-base part left after the letter');
  if (/்[க-ஹ][ெேை]/.test(w)) reasons.push('pre-base sign put in front of the whole pulli cluster (C்Cெ)');
  if (/ரி|ரீ/.test(w) && /B5 9[12]/.test(ours)) reasons.push('ரி/ரீ written B5 + 91/92 instead of the 9B/9F ligature');
  if (/[ஷ][ுூ]/.test(w)) reasons.push('ஷு/ஷூ hook FA/FC instead of FB/FD');
  if (!reasons.length) reasons.push(sorted(canon(ours)) === sorted(canon(shree)) ? 'other order' : 'other bytes');
  for (const r of reasons) out.byReason[r] = (out.byReason[r] || 0) + 1;
  out.wrong.push(`${w}: ours ${ours} | Shree ${shree} | ${reasons.join('; ')}`);
}
const oo = out.oldOrthographyAccepted || 0;
out.summary = `${out.words} coverable words: ${out.exact} exact bytes (${(100 * out.exact / out.words).toFixed(1)}%), ${out.exact + out.glyphEqual} draw exactly what Shree draws (${(100 * (out.exact + out.glyphEqual) / out.words).toFixed(1)}%), ${oo} more use the old னா/றா/ணா ligature Shree also types (accepted), ${out.visiblyWrong} visibly wrong (${(100 * out.visiblyWrong / out.words).toFixed(1)}%)`;
fs.writeFileSync(D + '/measured/common-words-classes.json', JSON.stringify(out, null, 1));
console.log(out.summary, JSON.stringify(out.byReason), 'notCoverable', out.notCoverable.join(' '));
console.log(out.wrong.join('\n'));
