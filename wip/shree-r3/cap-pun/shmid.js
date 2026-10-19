// node shmid.js <SL8_DLL_DATA.bin> : which shipped keyboard file each 406-byte table in the shared memory is
const fs = require('fs');
const path = require('path');
const S = 'C:/Shree-Lipi nxt';
const b = fs.readFileSync(process.argv[2]);
const files = fs.readdirSync(S).filter(f => /\.(DEV|GUJ|PUN|BAN|ASS|ORI|TAM|TEL|KAN|MAL|SAN)$/i.test(f));
const kb = files.map(f => [f, fs.readFileSync(path.join(S, f))]).filter(([, x]) => x.length === 406);
// tables start where byte i == i for i = 1..31 (control chars echo themselves)
const starts = [];
for (let o = 0; o + 406 <= b.length; o++) {
  let ok = true;
  for (let i = 1; i < 32; i++) if (b[o + i] !== i) { ok = false; break; }
  if (ok) starts.push(o);
}
console.log('header', [...b.slice(0, starts[0] || 0)].map(x => x.toString(16).padStart(2, '0')).join(' '));
for (const o of starts) {
  const t = b.slice(o, o + 406);
  const ranked = kb.map(([f, x]) => { let d = 0; for (let i = 0; i < 406; i++) if (x[i] !== t[i]) d++; return [f, d]; }).sort((a, c) => a[1] - c[1]);
  console.log('table @0x' + o.toString(16), 'best', ranked.slice(0, 4).map(([f, d]) => `${f}(${d} diff)`).join(', '));
}
