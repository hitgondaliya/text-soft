const o = require('../results/l1-compare.json');
const cause = {}; const add = (k, x) => (cause[k] = cause[k] || []).push(x);
for (const [v, r] of Object.entries(o.keyboards)) {
  for (const x of r.b7) {
    let k;
    if (x.why && x.why.startsWith('only the Shree-Lipi 7')) k = x.shree7 === '90' ? 'code EE (byte 90) missing: TextSoft types the plain key' : 'ru/ruu ligature key (7B 24 / 7E 24 / EA 24) typed as ra+sign';
    else if (x.shree7 === 'AE') k = 'reph key alone (AE vs 66 B9)';
    else if (/^9C 00$/.test(x.shree7)) k = 'nukta alone (composer adds 00)';
    else if (/B5/.test(x.shree7) && x.ts7 === x.shree7.replace('B5', 'B8')) k = 'candra sign: composer B5 (code D6) vs TextSoft B8 (both accepted by truth)';
    else if (/્$/.test(x.shreeU || '')) k = 'half-letter key alone: half form vs full letter + halant';
    else if (x.shree7 === '84 AE') k = 'anusvara+reph order (84 AE vs AF, both in truth)';
    else k = 'other';
    add(k, v + ' ' + x.key + ' ' + (x.shree7 || '') + ' vs ' + x.ts7);
  }
}
for (const [k, l] of Object.entries(cause)) console.log(k.padEnd(80), l.length, '|', l.slice(0, 4).join('; '));
const sn = {}; for (const [v, r] of Object.entries(o.keyboards)) for (const x of r.shreeNothing) { const k = x.layer ? 'Ctrl+Space + key the file leaves empty (FF): TextSoft types the plain key' : x.fileCode === 'FF' ? 'printed key the file leaves empty (FF)' : 'code ' + x.fileCode + ' (no composer answer)'; sn[k] = (sn[k] || 0) + 1; }
console.log(sn);
let syms = 0, symBad = []; for (const [id, l] of Object.entries(o.symbolLayer)) for (const s of l) { if (!'$*-/KL[]_klq|R'.includes(s.key.slice(-1))) continue; syms++; if (s.shree7 !== s.ts7 && !(s.shree7 == null && s.ts7 === '')) symBad.push(id + ' ' + s.key + ' ' + s.uni + ' ts ' + s.ts7 + ' shree ' + s.shree7); }
console.log('symbol layer chart keys', syms, 'differ', symBad.length); console.log([...new Set(symBad.map(s => s.split(' ').slice(1).join(' ')))].join('\n'));
