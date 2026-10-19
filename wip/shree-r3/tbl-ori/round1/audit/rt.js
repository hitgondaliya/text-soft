// node rt.js <layoutsDir> : do the Odia layouts round-trip through JSON.stringify(_, null, 2) + "\n" byte for byte?
const fs = require('fs');
const dir = process.argv[2];
for (const f of ['odia-inscript', 'ori-crtronic', 'ori-doe', 'ori-eng', 'ori-modular', 'ori-newroma', 'ori-phonetic', 'ori-roma', 'ori-typewrit']) {
  const s = fs.readFileSync(dir + '/' + f + '.json', 'utf8');
  const r = JSON.stringify(JSON.parse(s), null, 2) + '\n';
  console.log(f, s === r ? 'roundtrip-identical' : 'DIFFERENT', s.includes(String.fromCharCode(92) + 'u') ? 'has-escapes' : '', s.includes('\r') ? 'CRLF' : '');
}
