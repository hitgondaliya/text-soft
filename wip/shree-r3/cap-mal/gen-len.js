// Slot-length experiment: is the first byte of a conjunct slot (always 04 in vendor files) a code COUNT? If the composer
// honours it, a slot can hold more than 4 codes by running into the next slot's bytes, and 5-code syllables
// (ൊ on a cluster, ra sign + cluster + sign ...) can be asked directly.
// Slot layout (memory note): 5 bytes per slot at 166 + 5n: [header 04][last code]...[first code], padded FF.
// Writes ln/LAAA.MAL (+ ln/L-manifest.json) from k/BASEA.MAL; every test slot is followed by an unused slot.
const fs = require('fs');
const path = require('path');
const base = fs.readFileSync(path.join(__dirname, 'k', 'BASEA.MAL'));
const b = Buffer.from(base);
for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 0x04; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
const KEYS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTU';
for (const k of KEYS) b[k.charCodeAt(0)] = 0xFF;
const manifest = [];
// put raw bytes from offset o (header first), then map key -> slot
function slot(n, key, raw, tag) {
  const o = 166 + n * 5;
  raw.forEach((x, j) => { b[o + j] = x; });
  b[key.charCodeAt(0)] = n < 32 ? 0xB0 + n : 0x90 + n - 32;
  manifest.push({ file: 'LAAA.MAL', key, slot: n, raw: raw.map(x => x.toString(16).toUpperCase().padStart(2, '0')).join(' '), tag });
}
// typed ക ് ക ാ = 66 64 66 6C, stored last first: 6C 66 64 66
slot(0, 'a', [0x04, 0x6C, 0x66, 0x64, 0x66], 'reference: header 04, typed 66 64 66 6C (ക്കാ = A8 4C)');
slot(2, 'b', [0x02, 0x6C, 0x66, 0x64, 0x66], 'header 02 with 4 codes: does the header cut it to 2?');
slot(4, 'c', [0x05, 0x6C, 0x66, 0x64, 0x66, 0x73], 'header 05, 5th byte (= next slot header) 73: typed 73 66 64 66 6C = ക്കൊ (52 A8 4C)?');
slot(7, 'd', [0x06, 0x6C, 0x66, 0x64, 0x66, 0x73, 0x73], 'header 06 running 2 bytes into slot 8: typed 73 73 66 64 66 6C?');
slot(10, 'e', [0x08, 0x6C, 0x66, 0x64, 0x66, 0x73, 0x79, 0x64, 0x63], 'header 08: typed 63 64 79 73 66 64 66 6C?');
slot(13, 'f', [0x00, 0x6C, 0x66, 0x64, 0x66], 'header 00');
slot(15, 'g', [0xFF, 0x6C, 0x66, 0x64, 0x66], 'header FF');
slot(17, 'h', [0x03, 0x6C, 0x66, 0x64, 0x66], 'header 03 with 4 codes');
slot(19, 'i', [0x01, 0x6C, 0x66, 0x64, 0x66], 'header 01 with 4 codes');
slot(21, 'j', [0x05, 0x79, 0x64, 0x79, 0x64, 0x63], 'header 05: typed 63 64 79 64 79 = സ്റ്റ?');
slot(24, 'k', [0x05, 0x4A, 0xD7, 0x64, 0x63, 0x59], 'header 05: typed 59 63 64 D7 4A = സ്ത്രീ?');
slot(27, 'l', [0x04, 0x4A, 0xD7, 0x64, 0x63], 'reference: 63 64 D7 4A (സ്തീ)');
fs.mkdirSync(path.join(__dirname, 'ln'), { recursive: true });
fs.writeFileSync(path.join(__dirname, 'ln', 'LAAA.MAL'), b);
fs.writeFileSync(path.join(__dirname, 'ln', 'L-manifest.json'), JSON.stringify(manifest, null, 1));
console.log('wrote ln/LAAA.MAL', manifest.length, 'test keys');
