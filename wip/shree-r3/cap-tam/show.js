// node show.js <tsv> [layer] : print key, codes in the file (layer 0 / ctrl-space), U answer (hex + text), 7 bytes
const { readTsv, readKbd, hex2, uhex, CTRL_SPACE_ORDER } = require('./lib');
const [tsv, kbd, lay] = process.argv.slice(2);
const kb = kbd && kbd !== '-' ? readKbd(kbd) : null;
for (const r of readTsv(tsv).rows) {
  if (lay != null && r.layer !== Number(lay)) continue;
  let code = '';
  if (kb) { if (r.layer === 0) code = hex2(kb.bytes[r.keyCode]); else if (r.layer === 1) { const i = CTRL_SPACE_ORDER.indexOf(r.key); code = i >= 0 ? hex2(kb.bytes[128 + i]) : ''; } }
  console.log(r.layer, r.key, code.padEnd(3), String(r.uUnits).padStart(2), (r.uHex || '').padEnd(20), JSON.stringify(r.u).padEnd(10), '|', r.lUnits, r.lb ? r.lb.map(hex2).join(' ') : '');
}
