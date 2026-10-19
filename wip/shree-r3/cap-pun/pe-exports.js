// List the exported names of a PE DLL (read-only file parse).
const fs = require('fs');
const b = fs.readFileSync(process.argv[2]);
const pe = b.readUInt32LE(0x3C);
const magic = b.readUInt16LE(pe + 24);
const nsec = b.readUInt16LE(pe + 6), optSize = b.readUInt16LE(pe + 20);
const dd = pe + 24 + (magic === 0x20B ? 112 : 96);
const expRva = b.readUInt32LE(dd);
const secs = [];
for (let i = 0; i < nsec; i++) { const o = pe + 24 + optSize + i * 40; secs.push({ va: b.readUInt32LE(o + 12), vs: b.readUInt32LE(o + 8), raw: b.readUInt32LE(o + 20) }); }
const off = rva => { for (const s of secs) if (rva >= s.va && rva < s.va + Math.max(s.vs, 1)) return rva - s.va + s.raw; return -1; };
const e = off(expRva);
const n = b.readUInt32LE(e + 24), names = off(b.readUInt32LE(e + 32));
const out = [];
for (let i = 0; i < n; i++) { let p = off(b.readUInt32LE(names + i * 4)); let s = ''; while (b[p]) s += String.fromCharCode(b[p++]); out.push(s); }
console.log(out.join(' '));
