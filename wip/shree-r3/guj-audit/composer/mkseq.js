// Sequence probes: runs of up to 4 Shree codes, each in a conjunct slot reached from one key
// (same scheme as tsk7g/build.js). Writes C:\Users\harde\AppData\Local\Temp\r3g\SA?.GUJ and seq-manifest.json.
const fs = require('fs'), path = require('path');
const K = 'C:/Users/harde/AppData/Local/Temp/r3g';
const base = fs.readFileSync('C:/Shree-Lipi nxt/INSCRIPT.GUJ');
const KEYS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTU';
const SLOTS = [...Array(48).keys()].filter(n => n !== 31);
// codes: 73 ક, 66 ત, 65 ન, 6A ર, 44 halant(ZWNJ), 64 link, D7 half, F8 ₹, 31 ૧, 32 ૨, D3 ।, F9 ॥, D2 ઽ, D4 ઃ,
// E1 nukta, 7B —, 7D –, DC ×, DD ÷, E2 ૰, 51 ૐ, 22 ‘, 27 ’, F4 “, F5 ”, 2E ., 2C ,, 3F ?, 21 !, 28 (, 29 ), 3A :
const seqs = [
  [0x73, 0x44, 0x73], [0x73, 0x44, 0x66], [0x65, 0x44, 0x65], [0x66, 0x44], [0x73, 0x44],
  [0xF8, 0x31, 0x32], [0x31, 0xF8], [0x73, 0xD3], [0x73, 0xF9], [0x73, 0xD2], [0x73, 0xD4],
  [0x73, 0x7B, 0x73], [0x73, 0x7D, 0x73], [0x31, 0xDC, 0x32], [0x31, 0xDD, 0x32], [0x73, 0xE2],
  [0x51, 0x73], [0x22, 0x73, 0x27], [0xF4, 0x73, 0xF5], [0x73, 0x2E], [0x73, 0x2C], [0x73, 0x3F],
  [0x73, 0x21], [0x28, 0x73, 0x29], [0x73, 0x3A], [0x73, 0x64, 0x73], [0x73, 0xD7, 0x73], [0x6A, 0xD7],
  [0x6A, 0xD7, 0x73], [0x73, 0xE1], [0x57, 0xE1], [0x43, 0xE1], [0x6A, 0x64, 0xF7, 0x73], [0x73, 0x64, 0xF7, 0x73],
  [0x73, 0x64, 0xF6, 0x73], [0xD3, 0xD3], [0x5B, 0x73], [0x5D], [0x5B], [0xF9], [0xF4], [0xEB], [0xE4], [0xE5],
  [0x6A, 0x62], [0x6A, 0x6E], [0x6A, 0xE9],
];
const manifest = [];
let fileNo = 0;
for (let i = 0; i < seqs.length; i += 47) {
  const name = 'SA' + 'ABCDEFGH'[fileNo++] + '.GUJ';
  const b = Buffer.from(base);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 4; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
  seqs.slice(i, i + 47).forEach((s, k) => {
    const n = SLOTS[k], o = 166 + n * 5;
    s.slice().reverse().forEach((c, j) => { b[o + 1 + j] = c; });
    b[KEYS.charCodeAt(k)] = n < 32 ? 0xB0 + n : 0x90 + n - 32;
    manifest.push({ file: name, key: KEYS[k], codes: s.map(c => c.toString(16).toUpperCase().padStart(2, '0')) });
  });
  fs.writeFileSync(path.join(K, name), b);
}
fs.writeFileSync(path.join(__dirname, 'seq-manifest.json'), JSON.stringify(manifest, null, 1));
const W = p => p.split('/').join('\\');
const out = W(__dirname) + '\\out\\';
fs.writeFileSync(path.join(__dirname, 'seqlist.txt'), [...new Set(manifest.map(m => m.file))].map(f => W(K + '/' + f) + '\t' + out + f.replace('.GUJ', '.tsv')).join('\r\n'));
console.log('files', fileNo, 'sequences', seqs.length);
