// Which codes the 12 vendor .PUN keyboards use (layer 0, Ctrl+Space, conjunct slots), with PUN.json meanings.
const fs = require('fs');
const S = 'C:/Shree-Lipi nxt/';
const PUN = JSON.parse(fs.readFileSync('C:/Users/harde/Desktop/Windows Font Software/assets/config/shree-kbd/PUN.json', 'utf8').replace(/^\uFEFF/, ''));
const C = {}; for (const [k, v] of Object.entries(PUN.codes)) C[parseInt(k, 16)] = v;
const h = n => n.toString(16).toUpperCase().padStart(2, '0');
const files = fs.readdirSync(S).filter(f => /\.PUN$/i.test(f)).sort();
const use = new Map();
const slotsAll = [];
for (const f of files) {
  const b = fs.readFileSync(S + f);
  if (b.length !== 406) { console.log(f, 'len', b.length); continue; }
  const add = (c, where) => { if (c === 0xFF || c === 0) return; if (!use.has(c)) use.set(c, new Set()); use.get(c).add(f.replace('.PUN', '') + ':' + where); };
  for (let i = 0x21; i < 0x7F; i++) { const c = b[i]; if (c >= 0xB0 && c <= 0xCF || c >= 0x90 && c <= 0x9F) continue; add(c, 'L0'); }
  for (let i = 128; i < 166; i++) { const c = b[i]; if (c >= 0xB0 && c <= 0xCF || c >= 0x90 && c <= 0x9F) continue; add(c, 'L1'); }
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse(); if (codes.length) { slotsAll.push({ f, n, codes }); codes.forEach(c => add(c, 'slot')); } }
}
const out = [];
for (const c of [...use.keys()].sort((a, b) => a - b)) out.push(`${h(c)} ${JSON.stringify(C[c] ?? null)} used by ${use.get(c).size}: ${[...use.get(c)].slice(0, 6).join(' ')}`);
console.log(out.join('\n'));
const uniq = new Map();
for (const s of slotsAll) { const k = s.codes.map(h).join(' '); if (!uniq.has(k)) uniq.set(k, []); uniq.get(k).push(s.f.replace('.PUN', '')); }
console.log('slots', slotsAll.length, 'unique', uniq.size);
console.log([...uniq].map(([k, fs]) => k + ' = ' + k.split(' ').map(x => C[parseInt(x, 16)] ?? '?' + x).join('') + ' (' + fs.length + ')').join('\n'));
