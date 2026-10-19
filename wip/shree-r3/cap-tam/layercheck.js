// (1) layers/DOE.tsv: do tutor layers 3..12 answer exactly as layer 2 (the raw code layer)?
// (2) tam-vendor-keys.json: does every layer-0 / Ctrl+Space key that holds ONE code answer exactly as that code does on the
//     raw code layer of the same keyboard (both composers)? Misses = the keyboard-name modes (phonetic, built-in ோ ொ ௌ).
const fs = require('fs');
const lines = fs.readFileSync(__dirname + '/layers/DOE.tsv', 'utf8').split('\n').filter(l => l && l[0] !== '#');
const by = {};
for (const l of lines) { const p = l.split('\t'); (by[p[0]] = by[p[0]] || {})[p[1]] = p.slice(2).join('\t'); }
const l2 = JSON.stringify(by['2']);
const same = Object.keys(by).filter(k => +k >= 3).map(k => k + ':' + (JSON.stringify(by[k]) === l2 ? 'same' : 'DIFF'));
console.log('DOE layers 3..12 vs layer 2:', same.join(' '));
const j = require('./tam-vendor-keys.json');
const out = { layersVsRaw: same.join(' '), perLayer: {} };
for (const L of ['0', '1']) {
  let n = 0, okU = 0, ok7 = 0; const miss = [];
  for (const [kb, v] of Object.entries(j.keyboards)) {
    const raw = v.layers['2'] || {};
    for (const [k, r] of Object.entries(v.layers[L] || {})) {
      if (!r.codes || r.codes.includes(' ')) continue;
      const ch = String.fromCharCode(parseInt(r.codes, 16));
      const rr = raw[ch];
      if (!rr) continue;
      n++;
      const u = r.uHex === rr.uHex, b = r.b7 === rr.b7;
      if (u) okU++; if (b) ok7++;
      if ((!u || !b) && miss.length < 40) miss.push(`${kb} L${L} ${k} code ${r.codes}: U ${r.uHex}/${rr.uHex} 7 ${r.b7}/${rr.b7}`);
    }
  }
  out.perLayer[L] = { singleCodeKeys: n, unicodeSame: okU, sevenSame: ok7, misses: miss };
  console.log(`layer ${L}: ${n} single-code keys; Unicode answer = raw layer ${okU}, 7 answer = raw layer ${ok7}`);
  console.log('  ' + miss.slice(0, 12).join('\n  '));
}
fs.writeFileSync(__dirname + '/layercheck.json', JSON.stringify(out, null, 1));
