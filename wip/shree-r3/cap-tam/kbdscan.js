// Dump the vendor Tamil keyboards (C:/Shree-Lipi nxt/*.TAM): layer 0 codes, Ctrl+Space codes, conjunct slots,
// and the union of every code used (with how many keyboards use it and where). -> kbdscan.txt
const { fs, readKbd, hex2, CODES, CTRL_SPACE_ORDER } = require('./lib');
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.TAM$/i.test(f)).sort();
const use = new Map();
const note = (c, where) => { if (!use.has(c)) use.set(c, []); use.get(c).push(where); };
const out = [];
for (const f of files) {
  const kb = readKbd(S + f);
  out.push('== ' + f);
  let l0 = [];
  for (let k = 0x21; k <= 0x7E; k++) { const c = kb.bytes[k]; if (c !== 0xFF) { l0.push(String.fromCharCode(k) + '=' + hex2(c)); note(c, f + ':' + String.fromCharCode(k)); } }
  out.push(' L0 ' + l0.join(' '));
  let l1 = [];
  for (let i = 0; i < 38; i++) { const c = kb.bytes[128 + i]; if (c !== 0xFF) { l1.push(CTRL_SPACE_ORDER[i] + '=' + hex2(c)); note(c, f + ':^' + CTRL_SPACE_ORDER[i]); } }
  out.push(' L1 ' + l1.join(' '));
  const sl = [];
  kb.slots.forEach((s, n) => { if (s.length) { sl.push(n + ':' + s.map(hex2).join(',') + '=' + s.map(c => CODES[c] ?? '?').join('')); s.forEach(c => note(c, f + ':slot' + n)); } });
  out.push(' SLOTS ' + sl.join(' '));
}
out.push('== codes used');
for (const c of [...use.keys()].sort((a, b) => a - b)) {
  if ((c >= 0xB0 && c <= 0xCF) || (c >= 0x90 && c <= 0x9F)) continue;
  const w = use.get(c);
  out.push(hex2(c) + ' ' + JSON.stringify(CODES[c] ?? null) + ' x' + w.length + ' ' + w.slice(0, 4).join(' '));
}
fs.writeFileSync(__dirname + '/kbdscan.txt', out.join('\n'));
console.log(out.length);
