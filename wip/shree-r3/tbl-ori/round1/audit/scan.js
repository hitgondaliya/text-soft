// node scan.js : chart artefacts in the Odia layouts - a danda inside a run's text, signs in odd order, malformed text
const fs = require('fs');
const dir = 'C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/tbl-ori/repo/assets/config/layouts/';
const isCons = c => (c >= 0x0B15 && c <= 0x0B39) || [0x0B5C, 0x0B5D, 0x0B5F, 0x0B71].includes(c);
const isSign = c => (c >= 0x0B3E && c <= 0x0B4C) || [0x0B56, 0x0B57, 0x0B62, 0x0B63, 0x0B01, 0x0B02, 0x0B03, 0x0B3C, 0x0B4D].includes(c);
for (const f of ['odia-inscript', 'ori-crtronic', 'ori-doe', 'ori-eng', 'ori-modular', 'ori-newroma', 'ori-phonetic', 'ori-roma', 'ori-typewrit']) {
  const L = JSON.parse(fs.readFileSync(dir + f + '.json', 'utf8'));
  const items = [];
  for (const k of L.keys) { items.push(['key ' + k.vk + ' base', k.base]); items.push(['key ' + k.vk + ' shift', k.shift]); }
  for (const s of L.sequences) items.push(['run ' + s.keys, s.to]);
  for (const [where, v] of items) {
    if (!v || v.length < 2) continue;
    const cps = [...v].map(c => c.codePointAt(0));
    const bad = [];
    if (cps.includes(0x0964) && cps.some(isCons)) bad.push('danda with letters');
    for (let i = 0; i < cps.length; i++) {
      const p = i > 0 ? cps[i - 1] : 0;
      if (isSign(cps[i]) && cps[i] !== 0x0B4D && i > 0 && !(isCons(p) || isSign(p) || p === 0x0B3C || p === 0x0B05)) bad.push('sign after ' + p.toString(16));
      if (cps[i] === 0x0B4D && i > 0 && !isCons(p)) bad.push('virama after ' + p.toString(16));
    }
    if (bad.length) console.log(f, where, JSON.stringify(v), cps.map(c => c.toString(16)).join(' '), bad.join('; '));
  }
}
