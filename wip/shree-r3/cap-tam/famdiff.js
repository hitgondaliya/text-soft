// fonts/ri-rii-r-7-vs-456.png: codes 9B 9F BA (and B5 B8 for reference) in every same-number 7 / 4-5-6 pair
const { load, sheet } = require('./ttf');
const NUM = ['0800', '0801', '0802', '0803', '1316', '3883'];
const rows = [];
for (const n of NUM) for (const fam of ['890', '0F0']) {
  const f = load(`C:/Windows/Fonts/S${n}${fam}.TTF`);
  rows.push({ label: (fam === '890' ? '7 ' : '456 ') + n, runs: [{ font: f, glyphs: [0xB5, 0x9B, 0x9F, 0xBA, 0xB8].map(c => f.bytes.get(c).glyph) }] });
}
sheet(__dirname + '/fonts/ri-rii-r-7-vs-456.png', rows, { px: 48, cellW: 330, cols: 2 });
