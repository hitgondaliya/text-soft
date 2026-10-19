// node plan.js <layoutFile> <wordsFile> [max] : key plans (keys=unicode;...) for words typed in Unicode order on a layout:
// each consonant and vowel by the key whose own text it is, each sign by a key typing it (or the vowel key the layout's
// own runs make type it after a consonant: k + e = కా teaches e = ా), the virama by its key.
const fs = require('fs');
const [lf, wf, max] = process.argv.slice(2);
const L = JSON.parse(fs.readFileSync(lf, 'utf8'));
const PK = [[48,"0",")"],[49,"1","!"],[50,"2","@"],[51,"3","#"],[52,"4","$"],[53,"5","%"],[54,"6","^"],[55,"7","&"],[56,"8","*"],[57,"9","("],[186,";",":"],[187,"=","+"],[188,",","<"],[189,"-","_"],[190,".",">"],[191,"/","?"],[192,"`","~"],[219,"[","{"],[220,String.fromCharCode(92),"|"],[221,"]","}"],[222,"'",'"']];
for (let c = 65; c <= 90; c++) PK.push([c, String.fromCharCode(c + 32), String.fromCharCode(c)]);
const alone = {};
for (const [vk, n, s] of PK) { const k = L.keys.find(k => k.vk === vk); if (!k) continue; if (k.base) alone[n] = k.base; if (k.shift) alone[s] = k.shift; }
const byText = {};
for (const [key, t] of Object.entries(alone)) if (!(t in byText) && !/[0-9A-Za-z]/.test(t)) byText[t] = key;
// signs a vowel key types after a consonant, from the layout's own two-key runs on ka
const signKey = {};
for (const s of L.sequences || []) if (s.keys.length === 2 && alone[s.keys[0]] === 'క' && s.to.startsWith('క') && s.to.length === 2) signKey[s.to[1]] ??= s.keys[1];
const words = fs.readFileSync(wf, 'utf8').split(/\r?\n/).map(w => w.trim()).filter(w => w);
const out = [];
for (const w of words) {
  let keys = '', ok = true;
  for (const ch of w) {
    if (ch === ' ') { keys += ' '; continue; }
    const k = byText[ch] ?? signKey[ch];
    if (k === undefined) { ok = false; break; }
    keys += k;
  }
  if (ok) out.push(keys + String.fromCharCode(9) + w);
  if (max && out.length >= +max) break;
}
console.log(out.join(String.fromCharCode(10)));
