// node shmstate.js [label] : dump SL8_DLL_DATA (read-only, no DLL loaded) to shmdump/<label>.bin and say
// what it holds: header, which vendor/probe keyboard each table is, and how it differs from the dump
// taken before this capture (shmdump/before/SL8_DLL_DATA.bin, all zeros on 2026-10-18 02:26).
const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const SD = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--USE~1\\767392~1\\SCRATC~1\\r3\\cap-pun\\';
const label = process.argv[2] || 'now';
const file = SD + 'shmdump\\' + label + '.bin';
const r = spawnSync(SD + 'shmlab\\bin\\shmlab.exe', ['dump:' + file], { encoding: 'utf8' });
if (r.status !== 0) { console.log('shmlab failed', r.status, r.stderr); process.exit(1); }
const b = fs.readFileSync(file);
const before = fs.readFileSync(SD + 'shmdump\\before\\SL8_DLL_DATA.bin');
let diff = 0;
for (let i = 0; i < b.length; i++) if (b[i] !== (i < before.length ? before[i] : 0)) diff++;
console.log('header', [...b.slice(0, 11)].map(x => x.toString(16).padStart(2, '0')).join(' '), '| bytes differing from before-dump:', diff);
const kb = [];
for (const dir of ['C:\\Shree-Lipi nxt']) for (const f of fs.readdirSync(dir)) if (/\.(DEV|GUJ|PUN|BAN|ASS|ORI|TAM|TEL|KAN|MAL|SAN)$/i.test(f)) { const x = fs.readFileSync(path.join(dir, f)); if (x.length === 406) kb.push([f, x]); }
for (const sub of ['k', 'p', 'px', 'px2', 'ph', 'pw']) { const d = SD + sub; if (fs.existsSync(d)) for (const f of fs.readdirSync(d)) if (/\.PUN$/i.test(f)) kb.push([sub + '/' + f, fs.readFileSync(path.join(d, f))]); }
for (const [o, name] of [[0x0B, 'table0'], [0x1A7, 'table1'], [0x33D, 'table2']]) {
  const t = b.slice(o, o + 406);
  if (t.every(x => x === 0)) { console.log(name, '@0x' + o.toString(16), 'all zero'); continue; }
  const ranked = kb.map(([f, x]) => { let d = 0; for (let i = 0; i < 406; i++) if (x[i] !== t[i]) d++; return [f, d]; }).sort((a, c) => a[1] - c[1]);
  console.log(name, '@0x' + o.toString(16), 'best', ranked.slice(0, 3).map(([f, d]) => `${f}(${d})`).join(', '));
}
console.log('byte 4D3 =', b[0x4d3].toString(16));
