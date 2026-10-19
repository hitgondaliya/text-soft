// L1: Shree's composers (per vendor key: cap-pun/pun-vendor-keys.json) against TextSoft's real TypingEngine
// (results/l1-textsoft-keys.json), key by key, layer by layer. Usage: node l1compare.js [tag]
const fs = require('fs'), path = require('path');
const here = __dirname;
const vendor = JSON.parse(fs.readFileSync(path.join(here, '..', '..', '..', 'cap-pun', 'pun-vendor-keys.json'), 'utf8'));
const ts = JSON.parse(fs.readFileSync(path.join(here, 'results', process.env.L1_IN || 'l1-textsoft-keys.json'), 'utf8'));
const tag = process.argv[2] || 'now';
const layoutOf = { CRTRONIC: 'pun-crtronic', DOE: 'pun-doe', ENG: 'pun-eng', GODREJ: 'pun-godrej', INSCRIPT: 'pa-inscript', ITR: 'pun-itr',
  NEWROMA: 'pun-newroma', PHONETIC: 'pun-phonetic', RAMING: 'pun-raming', ROMA: 'pun-roma', TYPEWRIT: 'pun-typewrit', VARITYPE: 'pun-varitype' };
const PHON = new Set(['ENG', 'NEWROMA', 'PHONETIC', 'ROMA']);
const U = s => [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const to456 = h => h == null ? null : h.split(' ').map(x => x === '3C' ? 'U+20B9' : x).join(' ');
const SH = String.raw`~!@#$%^&*()_+{}|:"<>?`, UN = String.raw`` + "`1234567890-=[]" + String.fromCharCode(92) + ";" + String.fromCharCode(39) + ",./";
const unshift = k => SH.includes(k) ? UN[SH.indexOf(k)] : k.toLowerCase();
const nfc = s => s == null ? s : s.normalize('NFC');

const T = {}; const inc = k => T[k] = (T[k] || 0) + 1;
const rows = [];
for (const [file, kb] of Object.entries(vendor.keyboards)) {
  const name = file.replace('.PUN', '');
  const id = layoutOf[name];
  const tk = ts[id];
  for (const L of ['0', '1']) {
    const layer = Number(L);
    for (const tkey of tk.keys.filter(k => k.layer === layer)) {
      const key = tkey.key;
      const v = (kb.layers[L] || {})[key];
      inc('positions');
      const shreeU = v && v.u != null && v.u !== '' ? v.u : null;
      const shreeB = v && v.b7 ? v.b7 : null;
      const tsU = tkey.uni;
      // what the app would show if TextSoft left the key alone: the key itself (layer 0) or a space and the key (layer 1)
      const tsNothing = !tkey.uniHandled || tsU === '';
      const r = { keyboard: name, layout: id, layer, key, codes: v ? v.codes : null, shreeU, shreeUHex: shreeU ? U(shreeU) : null, shree7: shreeB,
        textsoftU: tsU, textsoftUHex: U(tsU), textsoft7: tkey.p7Hex, textsoft456: tkey.p456Hex, handled: tkey.uniHandled,
        p7IsConverterOfUnicode: tkey.p7IsConverterOfUnicode, p456IsConverterOfUnicode: tkey.p456IsConverterOfUnicode };
      if (!tkey.p7IsConverterOfUnicode || !tkey.p456IsConverterOfUnicode) { r.cls = 'x-typing-differs-from-converter'; inc(r.cls); rows.push(r); continue; }
      if (shreeU == null && shreeB == null) {
        if (tsNothing) { inc('both-nothing'); continue; }
        r.cls = 'd-shree-nothing-textsoft-types';
        // the fallback conventions: Ctrl+Space key with nothing on the layer types the normal key; Shift with no shift char the normal char
        const normal = tk.keys.find(k => k.layer === 0 && k.key === key);
        if (layer === 1 && normal && normal.uni === tsU) r.sub = 'Ctrl+Space fallback (types the key\'s own character)';
        else if (layer === 0 && unshift(key) !== key && tk.keys.find(k => k.layer === 0 && k.key === unshift(key))?.uni === tsU) r.sub = 'Shift fallback (types the unshifted character)';
        else if (layer === 0) r.sub = 'the chart prints a character where the vendor file has none';
        else if (!tkey.uniHandled) r.sub = 'key left to the application';
        else r.sub = 'other';
        inc(r.cls + ': ' + r.sub);
        rows.push(r); continue;
      }
      if (tsNothing) { r.cls = 'a-textsoft-nothing'; inc(r.cls); rows.push(r); continue; }
      const sameU = shreeU != null && nfc(shreeU) === nfc(tsU);
      if (sameU) {
        const ok7 = (shreeB ?? "") === tkey.p7Hex, ok456 = (to456(shreeB) ?? "") === tkey.p456Hex;
        if (ok7 && ok456) { inc('same'); continue; }
        r.cls = 'b-same-unicode-other-bytes'; inc(r.cls); rows.push(r); continue;
      }
      // different Unicode
      if (shreeU == null && shreeB != null) {
        r.cls = 'c-shree-bytes-only (the Unicode composer answers nothing)';
        r.sub = shreeB === tkey.p7Hex ? 'same 7 bytes' : 'other 7 bytes';
        inc(r.cls + ': ' + r.sub); rows.push(r); continue;
      }
      r.cls = PHON.has(name) ? 'e-phonetic-mode-differs' : 'c-different-unicode';
      r.sub = shreeB === tkey.p7Hex ? 'same 7 bytes' : 'other 7 bytes';
      inc(r.cls + ': ' + r.sub); rows.push(r);
    }
  }
}
// Ctrl+Shift+1: the chart's symbol keys on every layout
for (const [id, tk] of Object.entries(ts)) {
  for (const [key, s] of Object.entries(vendor.ctrlShift1.keys)) {
    const t = tk.keys.find(k => k.layer === 2 && k.key === key);
    inc('ctrlShift1-positions');
    const ok = t.uni === s.u && t.p7Hex === s.b7 && t.p456Hex === to456(s.b7);
    if (ok) inc('ctrlShift1-same');
    else { inc('ctrlShift1-differs'); rows.push({ cls: 'ctrlShift1', layout: id, key, shreeU: s.u, shree7: s.b7, textsoftU: t.uni, textsoft7: t.p7Hex, textsoft456: t.p456Hex }); }
  }
}
fs.writeFileSync(path.join(here, 'results', `l1-compare-${tag}.json`), JSON.stringify({ totals: T, rows }, null, 1));
console.log(JSON.stringify(T, null, 1));
const by = {};
for (const r of rows) { const k = r.cls + (r.sub ? ': ' + r.sub : ''); (by[k] = by[k] || []).push(r); }
for (const [k, list] of Object.entries(by)) {
  console.log(`\n== ${k}: ${list.length}`);
  for (const r of list.slice(0, Number(process.argv[3] || 12)))
    console.log(`  ${r.keyboard || ''} ${r.layout} L${r.layer ?? 2} ${JSON.stringify(r.key)} codes ${r.codes ?? ''}: shree ${r.shreeU ?? '-'} [${r.shree7 ?? '-'}]  textsoft ${JSON.stringify(r.textsoftU)} [${r.textsoft7}] [456 ${r.textsoft456}]`);
}
