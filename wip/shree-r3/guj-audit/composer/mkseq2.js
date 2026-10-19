// Second sequence probe (SAB.GUJ): the ru / ruu ligature codes with the marks that follow them,
// and code EE between letters. Same slot scheme as mkseq.js.
const fs = require('fs'), path = require('path');
const K = 'C:/Users/harde/AppData/Local/Temp/r3g';
const base = fs.readFileSync('C:/Shree-Lipi nxt/INSCRIPT.GUJ');
const KEYS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTU';
const SLOTS = [...Array(48).keys()].filter(n => n !== 31);
// E9 ru, EA ruu (new), 7E ruu (old), 69 anusvara, 40 reph, 6A ra, 62 u, 6E uu, 73 ka, EE ?, D4 visarga, 3E candra, 67 i-sign
const seqs = [
  [0xE9], [0xEA], [0x7E], [0xE9, 0x69], [0xEA, 0x69], [0x7E, 0x69], [0x6A, 0x62, 0x69], [0x6A, 0x6E, 0x69],
  [0x73, 0xE9], [0x73, 0x7E], [0xE9, 0x73], [0x7E, 0x73], [0xE9, 0x40], [0x7E, 0x40], [0x73, 0xEE, 0x73], [0xEE],
  [0x31, 0xEE, 0x32], [0xE9, 0xD4], [0x7E, 0xD4], [0x67, 0xE9], [0x6A, 0x62, 0x40], [0x40, 0x6A, 0x62],
];
const manifest = [];
const name = 'SAB.GUJ';
const b = Buffer.from(base);
for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 4; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
seqs.forEach((s, k) => {
  const n = SLOTS[k], o = 166 + n * 5;
  s.slice().reverse().forEach((c, j) => { b[o + 1 + j] = c; });
  b[KEYS.charCodeAt(k)] = n < 32 ? 0xB0 + n : 0x90 + n - 32;
  manifest.push({ file: name, key: KEYS[k], codes: s.map(c => c.toString(16).toUpperCase().padStart(2, '0')) });
});
fs.writeFileSync(path.join(K, name), b);
fs.writeFileSync(path.join(__dirname, 'seq2-manifest.json'), JSON.stringify(manifest, null, 1));
const W = p => p.split('/').join('\\');
fs.writeFileSync(path.join(__dirname, 'seqlist2.txt'), W(K + '/' + name) + '\t' + W(__dirname) + '\\out\\SAB.tsv');
console.log('sequences', seqs.length);
