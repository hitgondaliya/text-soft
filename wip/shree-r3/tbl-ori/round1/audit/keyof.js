// node keyof.js <layoutId> <chars...> : which keys type each string on a layout (keys, sequences)
const fs = require('fs');
const [id, ...wants] = process.argv.slice(2);
const L = JSON.parse(fs.readFileSync('C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/tbl-ori/repo/assets/config/layouts/' + id + '.json', 'utf8'));
const punct = { 186: ';:', 187: '=+', 188: ',<', 189: '-_', 190: '.>', 191: '/?', 192: '`~', 219: '[{', 220: '\|', 221: ']}', 222: '\'"' };
const ch = (vk, sh) => vk >= 65 && vk <= 90 ? (sh ? String.fromCharCode(vk) : String.fromCharCode(vk + 32)) : vk >= 48 && vk <= 57 ? (sh ? ')!@#$%^&*('[vk - 48] : String.fromCharCode(vk)) : punct[vk] ? punct[vk][sh ? 1 : 0] : '?';
for (const w of wants) {
  const n = w.normalize('NFC');
  const hits = [];
  for (const k of L.keys) { if (k.base && k.base.normalize('NFC') === n) hits.push(ch(k.vk, false)); if (k.shift && k.shift.normalize('NFC') === n) hits.push(ch(k.vk, true)); }
  for (const s of L.sequences) if (s.to.normalize('NFC') === n) hits.push('run ' + s.keys);
  console.log(w, [...w].map(c => c.codePointAt(0).toString(16)).join(' '), '->', hits.join(' | ') || 'NONE');
}
