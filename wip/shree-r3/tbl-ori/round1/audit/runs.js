// node runs.js <layoutId> : chart runs whose text is not what their keys type one after another (link keys as the virama)
const fs = require('fs');
const id = process.argv[2];
const L = JSON.parse(fs.readFileSync('C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/tbl-ori/repo/assets/config/layouts/' + id + '.json', 'utf8'));
const punct = { 186: ';:', 187: '=+', 188: ',<', 189: '-_', 190: '.>', 191: '/?', 192: '`~', 219: '[{', 220: '\|', 221: ']}', 222: '\'"' };
const alone = {};
for (const k of L.keys) {
  const n = k.vk >= 65 && k.vk <= 90 ? [String.fromCharCode(k.vk + 32), String.fromCharCode(k.vk)] : k.vk >= 48 && k.vk <= 57 ? [String.fromCharCode(k.vk), ')!@#$%^&*('[k.vk - 48]] : punct[k.vk] ? [punct[k.vk][0], punct[k.vk][1]] : null;
  if (!n) continue;
  if (k.base !== undefined) alone[n[0]] = k.base;
  alone[n[1]] = k.shift !== undefined ? k.shift : k.base;
}
let n = 0;
for (const s of L.sequences) {
  if ([...s.keys].some(c => c === '\u00D8' || c === '\u00A4')) continue;
  if (![...s.keys].every(c => alone[c] !== undefined)) continue;
  const concat = [...s.keys].map(c => alone[c]).join('').normalize('NFC');
  const to = s.to.normalize('NFC');
  if (concat !== to) { n++; if (process.argv[3] !== '-q') console.log(`${s.keys}\trun ${to} [${[...to].map(c => c.codePointAt(0).toString(16)).join(' ')}]\tkeys one by one ${concat}`); }
}
console.log(id, 'runs whose text differs from their keys one by one:', n, 'of', L.sequences.length);
