// Which tutor layers (a6 = 0..N) answer at all, for ITR.PUN: does any layer type the chart's Ctrl+Shift+1 symbols
// ($ -> ₹, * -> ×, / -> ÷)? Writes layers/ITR-L<N>.tsv.
const { execFileSync } = require('child_process');
const fs = require('fs');
const SD = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--USE~1\\767392~1\\SCRATC~1\\r3\\cap-pun\\';
const N = process.argv[2] || '12';
fs.mkdirSync(SD + 'layers', { recursive: true });
const lf = SD + 'list-layers.txt';
fs.writeFileSync(lf, 'C:\\Shree-Lipi nxt\\ITR.PUN\t' + SD + 'layers\\ITR-L' + N + '.tsv');
execFileSync(SD + 'probe2\\bin\\probe2.exe', ['--list', lf, '--legacy', '7', '--script', 'Pun', '--layers', N], { stdio: 'inherit' });
fs.unlinkSync(lf);
const rows = fs.readFileSync(SD + 'layers\\ITR-L' + N + '.tsv', 'utf8').split('\n').filter(l => l && l[0] !== '#').map(l => l.split('\t'));
const per = {};
for (const r of rows) per[r[0]] = (per[r[0]] || 0) + 1;
console.log('answers per layer', JSON.stringify(per));
for (const r of rows) if (['24', '2A', '2F', '71'].includes(r[1])) console.log('layer', r[0], 'key', String.fromCharCode(parseInt(r[1], 16)), 'U', r[3], '7', r[5]);
