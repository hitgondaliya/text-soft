// node runs2.js <layoutId> <ORIFILE> : chart runs (keys on the normal layer only) against what Shree's own keyboard file types
// for the same keys one by one (each key's Unicode answer from the capture; the link key 64 as the virama). Prints the runs
// whose text differs from Shree's, with what TextSoft's keys type one by one too.
const fs = require('fs');
const [id, file] = process.argv.slice(2);
const L = JSON.parse(fs.readFileSync('C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/tbl-ori/repo/assets/config/layouts/' + id + '.json', 'utf8'));
const V = JSON.parse(fs.readFileSync('C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-ori/ori-vendor-keys.json', 'utf8'));
const S = V.keyboards[file].layers['0'];
const shreeKey = c => { const e = S[c]; if (!e) return null; if (e.codes === '64') return '\u0B4D'; if (e.codes === 'D5') return '\u0B4D\u200D'; if (e.codes === '44') return '\u0B4D\u200C'; return e.u; };
let n = 0, bad = 0;
const out = [];
for (const s of L.sequences) {
  if ([...s.keys].some(c => c === '\u00D8' || c === '\u00A4')) continue;
  const parts = [...s.keys].map(shreeKey);
  if (parts.some(p => p == null)) continue;
  n++;
  const shree = parts.join('').normalize('NFC'), to = s.to.normalize('NFC');
  if (shree !== to) { bad++; out.push(`${s.keys}\trun ${to}\tShree's keys one by one ${shree}`); }
}
for (const o of out) console.log(o);
console.log(`${id}: ${n} runs of plain keys, ${bad} differ from Shree's keys one by one`);
