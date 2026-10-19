// node compare.js <l1.json> <out.json> : TextSoft's per-key output (oriaudit l1) against Shree's own keyboards (cap-ori ori-vendor-keys.json)
const fs = require('fs');
const [l1File, outFile] = process.argv.slice(2);
const T = JSON.parse(fs.readFileSync(l1File, 'utf8'));
const V = JSON.parse(fs.readFileSync('C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-ori/ori-vendor-keys.json', 'utf8'));
const map = { 'or-inscript': 'INSCRIPT.ORI', 'ori-crtronic': 'CRTRONIC.ORI', 'ori-doe': 'DOE.ORI', 'ori-eng': 'ENG.ORI', 'ori-newroma': 'NEWROMA.ORI', 'ori-phonetic': 'PHONETIC.ORI', 'ori-roma': 'ROMA.ORI', 'ori-typewrit': 'TYPEWRIT.ORI' };
const docMap = { '9C': '5B' }, pass = { '3C': 'U+20B9', '8F': 'U+0B70', '90': 'U+0B3D' };
const want7 = b => (b || '').split(' ').filter(Boolean).map(t => pass[docMap[t] || t] || docMap[t] || t).join(' ');
const uhex = s => [...(s || '')].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const ascii = '!'.charCodeAt(0);
const out = { byLayout: {}, totals: {} };
const tot = {};
function add(cls, layout, item) { tot[cls] = (tot[cls] || 0) + 1; ((out.byLayout[layout] ||= {})[cls] ||= []).push(item); }
for (const [lid, vname] of Object.entries(map)) {
  const tk = T[lid].keys; const vk = V.keyboards[vname].layers;
  for (const row of tk) {
    let v;
    if (row.layer === 0) v = vk['0'][row.key];
    else if (row.layer === 1) v = vk['1'][row.key];
    else { const s = V.ctrlShift1.keys[row.key]; v = s ? { codes: s.code, u: s.u, b7: s.b7 || '', src: 'chart' } : undefined; }
    const uni = row.uni, o7 = row.o7Hex, o7s = row.o7Space;
    const passedRaw = !row.o7Handled && row.o7 !== '' && row.o7 === row.key; // the key reached the Shree font as its own ASCII code
    const item = { layer: row.layer, key: row.key, codes: v ? v.codes : null, shreeU: v ? v.u : null, shreeUHex: v ? uhex(v.u) : null, shree7: v ? want7(v.b7) : null, shree7raw: v ? v.b7 : null, tsU: uni, tsUHex: row.uniHex, ts7: o7, ts7AfterSpace: o7s, tsHandled: row.o7Handled };
    const shreeTypes = v && v.b7 && v.b7 !== '07';
    if (!v || !shreeTypes) {
      if (!v || !v.b7) {
        if (row.layer === 0 && passedRaw && !/[!%(),\-.\/:?0-9*]/.test(row.key)) add('shree-nothing:ts-raw-ascii-into-font', lid, item);
        else if (o7 !== '' && !passedRaw) add('shree-nothing:ts-types', lid, item);
        else add('both-nothing-or-itself', lid, item);
        continue;
      }
      add('quirk:07-carrier', lid, item); continue;
    }
    const w7 = want7(v.b7);
    if (o7 === w7 && (uni === v.u || v.u == null)) { add('same', lid, item); continue; }
    if (o7 === '' || (passedRaw && w7 !== o7)) { add('a:ts-nothing-or-raw', lid, item); continue; }
    if (o7s === w7) { add('same-after-space(pending run)', lid, item); continue; }
    if (uni === v.u) { add('b:same-unicode-other-bytes', lid, item); continue; }
    add('c:other-unicode', lid, item);
  }
}
out.totals = tot;
fs.writeFileSync(outFile, JSON.stringify(out, null, 1));
console.log(JSON.stringify(tot));
for (const [l, c] of Object.entries(out.byLayout)) console.log(l, Object.entries(c).map(([k, v]) => k + ':' + v.length).join('  '));
