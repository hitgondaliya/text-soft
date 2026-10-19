// Round 2: can a conjunct slot hold a REFERENCE to another slot (codes B0-CE / 90-9F inside a slot)? If the
// composers expand it, one key types sequences longer than 4 codes (5-codes clusters, whole words).
// node gen-nest.js exp            -> pn/QAAA.PUN (+ pn/Q-manifest.json): the yes/no experiment
// node gen-nest.js <seqs.json> <outdir> <prefix>  -> packed probe keyboards for long sequences (after the experiment)
// Packing: a sequence of <= 4 codes takes one slot; a longer one is [c1 c2 c3 REF] -> REF = the rest, packed the same
// way (tail nesting). Slot 31 (code CF) is never used (it reaches nothing). Base = k/BASEA.PUN (a copy of ITR.PUN).
const fs = require('fs');
const path = require('path');
const base = fs.readFileSync(path.join(__dirname, 'k', 'BASEA.PUN'));
const KEYS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTU'; // 47 keys
const SLOTS = [...Array(48).keys()].filter(n => n !== 31);   // 47 slots
const L = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const slotCode = n => (n < 32 ? 0xB0 + n : 0x90 + n - 32);
const hex2 = n => n.toString(16).toUpperCase().padStart(2, '0');

function blank() {
  const b = Buffer.from(base);
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 0x04; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
  for (const k of KEYS) b[k.charCodeAt(0)] = 0xFF;
  return b;
}
function putSlot(b, n, codes) {
  if (codes.length < 1 || codes.length > 4) throw new Error('slot length ' + codes.length);
  const o = 166 + n * 5;
  codes.slice().reverse().forEach((c, j) => { b[o + 1 + j] = c; }); // stored last piece first
}
// slots a sequence needs with tail nesting
const need = len => (len <= 4 ? 1 : 1 + need(len - 3));

if (process.argv[2] === 'exp') {
  // slot index -> raw slot content (refs written as slotCode(n)); every slot also sits on key KEYS[i]
  const R = slotCode;
  const exp = [
    [[0x67, 0x73], 'ਕਿ'],
    [[R(0), 0xD6], 'ref first: [ਕਿ] ੰ = ਕਿੰ'],
    [[0x73, 0x64, 0x66], 'ਕ link ਤ'],
    [[0x67, R(2), 0xD6], 'ref middle: ਿ [ਕ link ਤ] ੰ'],
    [[0x73, 0x44, 0x66], 'ਕ halant ਤ'],
    [[0x67, R(4), 0xD6], 'ਿ [ਕ halant ਤ] ੰ'],
    [[0x26, 0x64, 0x66, 0xD1], 'ਸ link ਤ ੍ਰ'],
    [[R(6), 0x68], '[ਸ link ਤ ੍ਰ] ੀ'],
    [[0x65, 0xD2, 0x6B], 'ਨ ੍ਹ ਾ'],
    [[R(8), 0x69], '[ਨ ੍ਹ ਾ] ਂ (direct 4 codes known)'],
    [[R(0)], '[ਕਿ] alone'],
    [[R(0), R(0)], '[ਕਿ][ਕਿ]'],
    [[R(1), R(1), R(1), R(1)], '4 x [[ਕਿ] ੰ]'],
    [[R(11)], '2 levels: [[ਕਿ][ਕਿ]]'],
    [[0x73, R(0)], 'ref last: ਕ [ਕਿ]'],
    [[0x42, 0x62, 0x65, R(16)], 'ੳ ੁ ਨ [੍ਹ ਾ ਂ] = ਉਨ੍ਹਾਂ'],
    [[0xD2, 0x6B, 0x69], '੍ਹ ਾ ਂ (helper)'],
    [[0x67, 0x63, 0x65, R(16)], 'ਿ ਜ ਨ [੍ਹ ਾ ਂ] = ਜਿਨ੍ਹਾਂ'],
    [[0x77, 0xD6, 0x63, R(19)], 'ਪ ੰ ਜ [ਾ ਬ] = ਪੰਜਾਬ'],
    [[0x6B, 0x71], 'ਾ ਬ (helper)'],
    [[0x77, 0xD6, 0x63, 0x6B], 'ਪ ੰ ਜ ਾ direct'],
    [[0x67, 0x73, 0xD6], 'ਿ ਕ ੰ direct (= ref first?)'],
    [[0x67, 0x73, 0x64, 0x66], 'ਿ ਕ link ਤ direct 4'],
  ];
  const b = blank();
  const manifest = [];
  exp.forEach(([codes, tag], i) => {
    const n = SLOTS[i];
    putSlot(b, n, codes);
    b[KEYS.charCodeAt(i)] = slotCode(n);
    manifest.push({ file: 'QAAA.PUN', key: KEYS[i], slot: n, raw: codes.map(hex2).join(' '), tag });
  });
  fs.mkdirSync(path.join(__dirname, 'pn'), { recursive: true });
  fs.writeFileSync(path.join(__dirname, 'pn', 'QAAA.PUN'), b);
  fs.writeFileSync(path.join(__dirname, 'pn', 'Q-manifest.json'), JSON.stringify(manifest, null, 1));
  console.log('pn/QAAA.PUN with', exp.length, 'slots');
  process.exit(0);
}

const [seqFile, outDir, prefix] = process.argv.slice(2);
const seqs = JSON.parse(fs.readFileSync(seqFile, 'utf8'));
fs.mkdirSync(outDir, { recursive: true });
const manifest = [];
let fileNo = 0, i = 0;
while (i < seqs.length) {
  const name = `${prefix}${L[Math.floor(fileNo / 676) % 26]}${L[Math.floor(fileNo / 26) % 26]}${L[fileNo % 26]}.PUN`;
  fileNo++;
  const b = blank();
  let free = 0, key = 0; // next free slot index into SLOTS, next key
  while (i < seqs.length && key < KEYS.length) {
    const s = seqs[i];
    const k = need(s.codes.length);
    if (free + k > SLOTS.length) break;
    // tail nesting: slot j holds [c1 c2 c3 REF(j+1)] until the rest fits in 4
    let rest = s.codes.slice();
    const first = SLOTS[free];
    for (let j = 0; j < k; j++) {
      const n = SLOTS[free + j];
      if (rest.length <= 4) { putSlot(b, n, rest); rest = []; }
      else { putSlot(b, n, [...rest.slice(0, 3), slotCode(SLOTS[free + j + 1])]); rest = rest.slice(3); }
    }
    free += k;
    b[KEYS.charCodeAt(key)] = slotCode(first);
    manifest.push({ file: name, key: KEYS[key], slot: first, slots: k, codes: s.codes, tag: s.tag });
    key++; i++;
  }
  fs.writeFileSync(path.join(outDir, name), b);
}
fs.writeFileSync(path.join(outDir, prefix + '-manifest.json'), JSON.stringify(manifest));
console.log('files', fileNo, 'sequences', seqs.length);
