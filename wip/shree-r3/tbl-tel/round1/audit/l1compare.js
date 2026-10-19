// L1: Shree's composers (per vendor key: cap-tel/tel-vendor-keys.json) against TextSoft's real TypingEngine
// (results/l1-textsoft-keys.json), key by key, layer by layer. Usage: node l1compare.js [tag] [show]
// The composer's 7 bytes are compared after the documented glyph exceptions (tools/TelTruth Capture.Exceptions):
// its 40 / B6 (ః ఁ, drawn as talakattu ticks) are 5D / 97, its 5D in a subjoined ఙ is 94, its CA in a subjoined ఱ is 3C.
const fs = require('fs'), path = require('path');
const here = __dirname;
const vendor = JSON.parse(fs.readFileSync(path.join(here, '..', '..', '..', 'cap-tel', 'tel-vendor-keys.json'), 'utf8'));
const ts = JSON.parse(fs.readFileSync(path.join(here, 'results', process.env.L1_IN || 'l1-textsoft-keys.json'), 'utf8'));
const tag = process.argv[2] || 'now';
const layoutOf = { APPLE: 'tel-apple', CRTRONIC: 'tel-crtronic', DOE: 'tel-doe', ENG: 'tel-eng', GODREJ: 'tel-godrej', INDICA: 'tel-indica',
  INSCRIPT: 'te-inscript', ITR: 'tel-itr', MICRO: 'tel-micro', MONO: 'tel-mono', NEWROMA: 'tel-newroma', PHONETIC: 'tel-phonetic',
  PRAKASH: 'tel-prakash', ROMA: 'tel-roma', TYPEWRIT: 'tel-typewrit', VISION: 'tel-vision' };
const PHON = new Set(['ENG', 'PHONETIC']);
const U = s => [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');
const nfc = s => s == null ? s : s.normalize('NFC');
function fixB7(b7, u) {
  if (b7 == null) return null;
  return b7.split(' ').map(x => x === '40' ? '5D' : x === 'B6' ? '97' : x === '5D' && u && u.includes('్ఙ') ? '94'
    : x === 'CA' && u && u.includes('్ఱ') ? '3C' : x).join(' ');
}

const T = {}; const inc = k => T[k] = (T[k] || 0) + 1;
const rows = [];
for (const [name, kb] of Object.entries(vendor.keyboards)) {
  const id = layoutOf[name];
  const tk = ts[id];
  if (!tk) { console.log('no layout for', name); continue; }
  for (const L of ['0', '1']) {
    const layer = Number(L);
    for (const tkey of tk.keys.filter(k => k.layer === layer)) {
      const key = tkey.key;
      const v = (kb.layers[L] || {})[key];
      inc('positions');
      const shreeU = v && v.uRead != null && v.uRead !== '' ? v.uRead : null;
      const shreeB = v && v.b7 ? fixB7(v.b7, shreeU) : null;
      const tsU = tkey.uni;
      const tsNothing = tsU === '';
      const r = { keyboard: name, layout: id, layer, key, codes: v ? v.codes : null, shreeU, shreeUHex: shreeU ? U(shreeU) : null, shree7: shreeB, shree7raw: v ? v.b7 : null,
        textsoftU: tsU, textsoftUHex: U(tsU), textsoft7: tkey.t7Hex, textsoft7w: tkey.t7wHex, handled: tkey.uniHandled,
        t7IsConverterOfUnicode: tkey.t7IsConverterOfUnicode, t7wIsConverterOfUnicode: tkey.t7wIsConverterOfUnicode };
      if (!tkey.t7IsConverterOfUnicode || !tkey.t7wIsConverterOfUnicode) { r.cls = 'x-typing-differs-from-converter'; inc(r.cls); rows.push(r); continue; }
      if (shreeU == null && shreeB == null) {
        if (tsNothing) { inc('both-nothing'); continue; }
        r.cls = 'd-shree-nothing-textsoft-types';
        const normal = tk.keys.find(k => k.layer === 0 && k.key === key);
        if (layer === 1 && normal && normal.uni === tsU) r.sub = 'Ctrl+Space fallback (types the key\'s own character)';
        else if (!tkey.uniHandled) r.sub = 'key left to the application';
        else r.sub = 'other';
        inc(r.cls + ': ' + r.sub);
        rows.push(r); continue;
      }
      if (tsNothing) { r.cls = 'a-textsoft-nothing'; inc(r.cls); rows.push(r); continue; }
      const sameU = shreeU != null && nfc(shreeU) === nfc(tsU);
      if (sameU) {
        const ok7 = (shreeB ?? '') === tkey.t7Hex && (shreeB ?? '') === tkey.t7wHex;
        if (ok7) { inc('same'); if (v.b7 !== shreeB) inc('same (after the documented glyph exceptions)'); continue; }
        r.cls = 'b-same-unicode-other-bytes'; inc(r.cls); rows.push(r); continue;
      }
      if (shreeU == null && shreeB != null) {
        r.cls = 'c-shree-bytes-only (the Unicode composer answers nothing)';
        r.sub = shreeB === tkey.t7Hex ? 'same 7 bytes' : 'other 7 bytes';
        inc(r.cls + ': ' + r.sub); rows.push(r); continue;
      }
      r.cls = PHON.has(name) ? 'e-phonetic-mode-differs' : 'c-different-unicode';
      r.sub = shreeB === tkey.t7Hex ? 'same 7 bytes' : 'other 7 bytes';
      inc(r.cls + ': ' + r.sub); rows.push(r);
    }
  }
}
// Ctrl+Shift+1: the chart's symbol keys on every layout
const cs1 = vendor.ctrlShift1.keys || vendor.ctrlShift1;
for (const [id, tk] of Object.entries(ts)) {
  for (const [key, s] of Object.entries(cs1)) {
    const t = tk.keys.find(k => k.layer === 2 && k.key === key);
    if (!t) continue;
    inc('ctrlShift1-positions');
    const want7 = fixB7(s.b7 || null, s.u);
    const ok = nfc(t.uni) === nfc(s.u) && (want7 ?? '') === t.t7Hex;
    if (ok) inc('ctrlShift1-same');
    else { inc('ctrlShift1-differs'); rows.push({ cls: 'ctrlShift1', layout: id, key, shreeU: s.u, shree7: want7, textsoftU: t.uni, textsoft7: t.t7Hex, textsoft7w: t.t7wHex }); }
  }
}
fs.writeFileSync(path.join(here, 'results', `l1-compare-${tag}.json`), JSON.stringify({ totals: T, rows }, null, 1));
console.log(JSON.stringify(T, null, 1));
const by = {};
for (const r of rows) { const k = r.cls + (r.sub ? ': ' + r.sub : ''); (by[k] = by[k] || []).push(r); }
for (const [k, list] of Object.entries(by)) {
  console.log(`\n== ${k}: ${list.length}`);
  for (const r of list.slice(0, Number(process.argv[3] || 12)))
    console.log(`  ${r.keyboard || ''} ${r.layout} L${r.layer ?? 2} ${JSON.stringify(r.key)} codes ${r.codes ?? ''}: shree ${r.shreeU ?? '-'} [${r.shree7 ?? '-'}]  textsoft ${JSON.stringify(r.textsoftU)} [${r.textsoft7}] [7w ${r.textsoft7w}]`);
}
