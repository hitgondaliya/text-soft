const R = require('./capp/P-results.json');
const by = new Map(R.map(r => [r.codes, r]));
const CONS = [0x67, 0x6E, 0x72, 0x4E, 0x69, 0x64, 0x79, 0x6C, 0x6B, 0x65, 0x63, 0x61, 0x6D, 0x6F, 0x78, 0x26, 0x73, 0x70, 0x24, 0xD0, 0x51, 0xD2, 0xE3, 0xD7];
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const cols = [['', c => [c]], ['ா', c => [c, 0x74]], ['ி', c => [c, 0x68]], ['ீ', c => [c, 0x6A]], ['ு5D', c => [c, 0x5D]], ['ூ5E', c => [c, 0x5E]], ['5F', c => [c, 0x5F]], ['ூ40', c => [c, 0x40]], ['03', c => [c, 0x03]], ['்', c => [c, 0x66]], ['ெ', c => [0x76, c]], ['ே', c => [0x62, c]], ['ை75', c => [0x75, c]], ['ைD3', c => [0xD3, c]], ['ொ', c => [0x76, c, 0x74]], ['ோ', c => [0x62, c, 0x74]], ['ௌ', c => [0x76, c, 0x26]]];
const mode = process.argv[2] || '7';
console.log('C   ' + cols.map(c => c[0].padEnd(9)).join(''));
for (const c of CONS) {
  const cells = cols.map(([n, f]) => { const r = by.get(f(c).map(h).join(' ')); if (!r) return '-'.padEnd(9); return (mode === '7' ? (r.bytes || 'null') : JSON.stringify(r.u)).padEnd(9); });
  console.log(h(c) + ' ' + (by.get(h(c)).u || '').padEnd(3) + cells.join(''));
}
