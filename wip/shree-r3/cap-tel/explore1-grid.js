// One line per code: what the composers answer for the code alone and in each explore context. -> explore1-grid.txt
const { fs, CODES, hex2, uhex } = require('./lib');
const res = require('./capx/E-results.json');
const by = new Map(res.map(r => [r.codes, r]));
const ctx = [['', ''], ['65 ', ''], ['', ' 65'], ['79 ', ''], ['65 64 ', ''], ['', ' 64 65'], ['6B 64 ', '']];
const names = ['alone', 'క+c', 'c+క', 'ప+c', 'క్+c', 'c+్క', 'ర్+c'];
const out = [];
for (let c = 0x21; c <= 0xFE; c++) {
  if ((c >= 0x90 && c <= 0x9F) || (c >= 0xB0 && c <= 0xCF)) continue;
  const h = hex2(c);
  const cells = ctx.map(([a, b], i) => { const r = by.get(a + h + b); if (!r) return names[i] + ':-'; return names[i] + ':' + (r.u == null ? 'null' : JSON.stringify(r.u)) + '=' + (r.bytes || '-'); });
  out.push(h + ' ' + JSON.stringify(CODES[c] ?? null) + ' | ' + cells.join(' | '));
}
fs.writeFileSync(__dirname + '/explore1-grid.txt', out.join('\n'));
