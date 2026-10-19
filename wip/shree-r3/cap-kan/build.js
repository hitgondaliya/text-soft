// Build probe keyboard files from a list of code sequences.
// node build.js <seqs.json> <outdir> <prefix>
// seqs.json: [{ "codes": [0x73, 0x64, 0x77], "tag": "..." }, ...]  (codes in typed order, <= 4)
// Each file: a copy of DOE.KAN (k/BASEA.TEL, a neutral name: the composers switch typing mode by keyboard NAME)
// whose 48 conjunct slots hold the sequences (slot 31 is skipped: it reaches nothing), each slot on one key a-z A-U.
// File names are <prefix> + three letters (PAAA, PAAB, ...): no digit as the second-to-last character, so a load
// writes only table 0 of SLNXT's SL8_DLL_DATA.
const fs = require('fs');
const path = require('path');
const base = fs.readFileSync(path.join(__dirname, 'k', 'BASEA.KAN'));
const KEYS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTU'; // 47 keys
const SLOTS = [...Array(48).keys()].filter(n => n !== 31);   // 47 slots
const L = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const [seqFile, outDir, prefix] = process.argv.slice(2);
const seqs = JSON.parse(fs.readFileSync(seqFile, 'utf8'));
fs.mkdirSync(outDir, { recursive: true });
const manifest = [];
let fileNo = 0;
for (let i = 0; i < seqs.length; i += 47) {
  const n0 = fileNo++;
  const name = `${prefix}${L[Math.floor(n0 / 676) % 26]}${L[Math.floor(n0 / 26) % 26]}${L[n0 % 26]}.KAN`;
  const b = Buffer.from(base);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 0x04; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
  const chunk = seqs.slice(i, i + 47);
  chunk.forEach((s, k) => {
    if (s.codes.length > 4 || !s.codes.length) throw new Error('bad length ' + JSON.stringify(s));
    const n = SLOTS[k];
    const o = 166 + n * 5;
    const stored = s.codes.slice().reverse(); // last piece first
    stored.forEach((c, j) => { b[o + 1 + j] = c; });
    const code = n < 32 ? 0xB0 + n : 0x90 + n - 32;
    const key = KEYS[k];
    b[key.charCodeAt(0)] = code;
    manifest.push({ file: name, key, slot: n, codes: s.codes, tag: s.tag });
  });
  for (let k = chunk.length; k < 47; k++) b[KEYS.charCodeAt(k)] = 0xFF;
  fs.writeFileSync(path.join(outDir, name), b);
}
fs.writeFileSync(path.join(outDir, prefix + '-manifest.json'), JSON.stringify(manifest));
console.log('files', fileNo, 'sequences', seqs.length);
