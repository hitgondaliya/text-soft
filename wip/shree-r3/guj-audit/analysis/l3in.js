// L3 input: every printable ASCII character and the typographic symbols, alone and between two
// letters, through both forward tables; plus the composer's byte for each (code sweep).
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const items = [];
const chars = [];
for (let c = 0x20; c <= 0x7E; c++) chars.push(String.fromCharCode(c));
for (const s of ['–', '—', '…', '“', '”', '‘', '’', '•', '₹', '°', '×', '÷', '।', '॥', 'ૐ', 'ઽ', '૰', '૱', ' ', '­', '·', '©', '®', '™', '€', '½', '¼', '¾', '¹', '²', '³', 'µ', '¶', '§', '¢', '£', '¥', '«', '»', '‚', '„', '†', '‡', '‰', '‹', '›', '−', '‐', '‑', '―', '′', '″', '→', '।।', '​', '﻿', '´']) chars.push(s);
for (const c of chars) {
  for (const dir of ['fwd7', 'fwd456']) {
    items.push({ dir, text: c, tag: 'alone' });
    items.push({ dir, text: 'ક' + c + 'ક', tag: 'between' });
  }
}
fs.writeFileSync(path.join(A, 'results', 'l3-conv-in.json'), JSON.stringify(items));
console.log(items.length);
