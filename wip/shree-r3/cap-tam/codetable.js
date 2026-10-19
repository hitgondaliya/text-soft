// Every single internal code 01..FE (not the slot references): what each composer answers, alone and after க (67).
// node codetable.js -> codetable.tsv (code, importer text, U alone, 7 alone, U after க, 7 after க, vendor keys using it)
const { fs, CODES, hex2, uhex } = require('./lib');
const P = JSON.parse(fs.readFileSync(__dirname + '/capp/P-results.json', 'utf8'));
const E = JSON.parse(fs.readFileSync(__dirname + '/capx/E-results.json', 'utf8'));
const by = new Map();
for (const r of [...P, ...E]) if (!by.has(r.codes)) by.set(r.codes, r);
const scan = fs.readFileSync(__dirname + '/kbdscan.txt', 'utf8').split('\n');
const uses = {};
let inCodes = false;
for (const l of scan) { if (l === '== codes used') { inCodes = true; continue; } if (inCodes) { const m = l.match(/^([0-9A-F]{2}) .* x(\d+)/); if (m) uses[m[1]] = +m[2]; } }
const show = r => r ? (r.u == null ? '-' : JSON.stringify(r.u) + ' [' + uhex(r.u) + ']') : '(not probed)';
const b7 = r => r ? (r.bytes == null ? '-' : r.bytes) : '(not probed)';
const out = ['code\timporter\tU alone\t7 alone\tU after க 67\t7 after க 67\tvendor uses'];
for (let c = 1; c <= 0xFE; c++) {
  if ((c >= 0x90 && c <= 0x9F) || (c >= 0xB0 && c <= 0xCF)) continue;
  const a = by.get(hex2(c)), k = by.get('67 ' + hex2(c));
  out.push([hex2(c), JSON.stringify(CODES[c] ?? null), show(a), b7(a), show(k), b7(k), uses[hex2(c)] || 0].join('\t'));
}
fs.writeFileSync(__dirname + '/codetable.tsv', out.join('\n') + '\n');
console.log(out.join('\n'));
