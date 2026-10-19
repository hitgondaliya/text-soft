// L4 input: legacy bytes to read back through shree-guj7-to-unicode and shree-guj456-to-unicode.
// Sources: Shree's own spellings (ground truth of the last round, per-key answers of this round,
// code sweep, code runs) and TextSoft's own forward output for the L3 characters and the L2 words.
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const GT = 'C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/624671f5-ee6d-402b-b810-05b52f236677/scratchpad/shree7/guj7-groundtruth.json';
const enc = new TextDecoder('windows-1252');
const fromHex = h => h.split(' ').filter(Boolean).map(x => { const b = parseInt(x, 16); const s = enc.decode(new Uint8Array([b])); return s; }).join('');
const NGA = [['50 B9 24 4C 24', '91'], ['50 B9 24 6E', 'B6'], ['50 B9 24 4D', '92'], ['50 B9 24 4E', '93'], ['50 B9 24 4F', '94']];
const to456 = h => { let s = ' ' + h + ' '; for (const [a, b] of NGA) s = s.split(' ' + a + ' ').join(' ' + b + ' '); return s.trim(); };
const items = [];
const gt = JSON.parse(fs.readFileSync(GT, 'utf8'));
for (const [u, t] of Object.entries(gt.truth)) {
  for (const h of t.accepted) {
    items.push({ dir: 'rev7', text: fromHex(h), tag: JSON.stringify({ src: 'truth', u, hex: h, pref: h === t.preferred }) });
    items.push({ dir: 'rev456', text: fromHex(to456(h)), tag: JSON.stringify({ src: 'truth', u, hex: to456(h), pref: h === t.preferred }) });
  }
}
// joined words of the composer: two and three syllables in a row (seeded sample)
const keys = Object.keys(gt.truth).filter(k => /^[\u0A95-\u0AB9]/.test(k));
let seed = 11; const rnd = n => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed % n; };
for (let i = 0; i < 6000; i++) {
  const n = 2 + rnd(2); let u = '', h = [];
  for (let j = 0; j < n; j++) { const k = keys[rnd(keys.length)]; u += k; h.push(gt.truth[k].preferred); }
  items.push({ dir: 'rev7', text: fromHex(h.join(' ')), tag: JSON.stringify({ src: 'joined', u, hex: h.join(' ') }) });
}
// per-key composer answers (this round)
const vk = JSON.parse(fs.readFileSync(path.join(A, 'results', 'composer-vendor-keys.json'), 'utf8')).keyboards;
const seen = new Set();
for (const [v, rows] of Object.entries(vk)) for (const r of rows) {
  if (!r.composer7 || seen.has(r.composer7)) continue; seen.add(r.composer7);
  items.push({ dir: 'rev7', text: fromHex(r.composer7), tag: JSON.stringify({ src: 'key', kb: v, key: r.key, layer: r.layer, u: r.composerClean, hex: r.composer7 }) });
}
// code sweep and code runs
const codes = JSON.parse(fs.readFileSync(path.join(A, 'results', 'composer-codes.json'), 'utf8')).codes;
for (const [c, s] of Object.entries(codes)) if (s.b7) items.push({ dir: 'rev7', text: fromHex(s.b7), tag: JSON.stringify({ src: 'code', code: c, u: s.u, hex: s.b7 }) });
const seq = JSON.parse(fs.readFileSync(path.join(A, 'results', 'composer-sequences.json'), 'utf8')).runs;
for (const s of seq) if (s.b7) items.push({ dir: 'rev7', text: fromHex(s.b7), tag: JSON.stringify({ src: 'run', codes: s.codes, u: s.u, hex: s.b7 }) });
// named open items
for (const [h, why] of [['4C B9 24 4C 24', 'in-word ZWNJ halant (composer: ક્‌ક)'], ['22 22', '“ as TextSoft writes it'], ['27 27', '” as TextSoft writes it'], ['2E 2E 2E', '… as TextSoft writes it'], ['B7', 'bullet'], ['97 26 26', '॥ as the composer writes two dandas'], ['97 26 97 26', '।। as TextSoft writes it'], ['3C', '₹ as the composer writes it'], ['EA 24', 'old ruu key'], ['7E 24', 'new ruu key'], ['7B 24', 'ru key'], ['90', 'code EE'], ['81', '–'], ['81 81', '—'], ['8D', '÷'], ['8F', '૰'], ['5F B9 5F', 'ન્‌ન (composer)'], ['4C B9 24 73', 'ક્‌ત (composer)'], ['9C 4C 24', 'ક઼ (composer)'], ['9C 61', 'ફ઼'], ['9A', 'ઝ઼'], ['66 B9 4C 24', 'ર્‌ક / ર્‍ક (composer)'], ['B9', 'halant alone'], ['AE', 'reph alone'], ['E9', 'ya-sign alone'], ['B0', 'ra-sign alone'], ['84', 'anusvara alone'], ['71', 'i-sign alone'], ['24', 'spacer alone'], ['3E', 'spacer > alone'], ['26', '& alone']])
  items.push({ dir: 'rev7', text: fromHex(h), tag: JSON.stringify({ src: 'named', why, hex: h }) });
// TextSoft's own forward spellings (L3 characters) read back
const l3 = JSON.parse(fs.readFileSync(path.join(A, 'results', 'l3-conv-out.json'), 'utf8'));
for (const r of l3) if (r.Tag === 'between') items.push({ dir: r.Dir === 'fwd7' ? 'rev7' : 'rev456', text: r.out, tag: JSON.stringify({ src: 'ts-l3', u: r.Text, hex: r.outHex }) });
fs.writeFileSync(path.join(A, 'results', 'l4-conv-in.json'), JSON.stringify(items));
console.log(items.length, 'reads');
