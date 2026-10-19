// L1: Shree's composers (per vendor key, saved in composer/out/VA?.tsv) against TextSoft's real
// TypingEngine (results/l1-textsoft-keys.json), key by key, layer by layer.
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const R = p => JSON.parse(fs.readFileSync(path.join(A, p), 'utf8'));
const man = R('composer/probe-manifest.json');
const ts = R('results/l1-textsoft-keys.json');
const codes = R('results/composer-codes.json').codes;
const layoutOf = { AKSHAR: 'guj-akshar', ASTERIX: 'guj-asterix', BPMK: 'guj-bpmk', CRTRONIC: 'guj-crtronic', DOE: 'guj-doe', ENG: 'guj-eng', INDICA: 'guj-indica', INSCRIPT: 'gu-inscript', ITR: 'guj-itr', MTNK: 'guj-mtnk', NEWROMA: 'guj-newroma', PHONET86: 'guj-phonet86', PHONETIC: 'guj-phonetic', RAMING: 'guj-raming', RAMING1: 'guj-raming1', RAMING2: 'guj-raming2', ROMA: 'guj-roma', SCRIPT: 'guj-script', TYPEWRIT: 'guj-typewrit' };
const CS = '1234567890-=qwertyuiopasdfghjklzxcvbnm';
const hex2 = n => n.toString(16).toUpperCase().padStart(2, '0');
function readTsv(f) {
  const rows = [];
  for (const line of fs.readFileSync(f, 'utf8').split('\n')) {
    if (!line || line[0] === '#') continue;
    const [layer, key, uu, uh, lu, lh] = line.split('\t');
    const uUnits = Number(uu), lUnits = Number(lu);
    const u = (uUnits > 0 && uUnits < 64 && uh) ? uh.split(' ').map(h => String.fromCharCode(parseInt(h, 16))).join('') : null;
    rows.push({ layer: Number(layer), key: String.fromCharCode(parseInt(key, 16)), u, uh: uh || '', b7: (lUnits > 0 && lUnits < 64 && lh) ? lh.trim() : null });
  }
  return rows;
}
const SIGN = /[\u0A81-\u0A83\u0ABC\u0ABE-\u0ACD\u0AE2\u0AE3]/;
function cleanU(u) {
  if (u == null) return { text: null, why: 'no answer' };
  if (/^[A-Za-z]{2}$/.test(u)) return { text: null, why: 'placeholder ' + u };
  let t = u;
  if (t.length > 1 && (t[0] === ' ' || t[0] === '.') && SIGN.test(t[1])) return { text: t.slice(1), why: 'carrier stripped' };
  if (t.length > 1 && t[0] === ' ') return { text: t.slice(1), why: 'space stripped' };
  return { text: t, why: '' };
}
const NGA = [['50 B9 24 4C 24', '91'], ['50 B9 24 6E', 'B6'], ['50 B9 24 4D', '92'], ['50 B9 24 4E', '93'], ['50 B9 24 4F', '94']];
const to456 = h => { let s = ' ' + h + ' '; for (const [a, b] of NGA) s = s.split(' ' + a + ' ').join(' ' + b + ' '); return s.trim(); };
const U = s => [...s].map(c => c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' ');

const out = { about: 'L1 per key: composer (S8GujU64 / S8Guj764, captured 2026-10-18) vs TextSoft TypingEngine on the same layout, alone after a fresh start, profiles Unicode / SHREE-GUJ7 (shree-guj7 table) / SHREE-GUJ (shree-guj456 table).', keyboards: {}, totals: {} };
const T = { keys: 0, compared: 0, same: 0, a_textsoftNothing: 0, c_differentUnicode: 0, b_bytes7: 0, b_bytes456: 0, d_quirk: 0, shreeNothingTextsoftTypes: 0 };
const vendorKeys = {};
for (const [file, vendor] of Object.entries(man.vendor)) {
  const rows = readTsv(path.join(A, 'composer', 'out', file.replace('.GUJ', '.tsv')));
  const kb = fs.readFileSync('C:/Shree-Lipi nxt/' + vendor + '.GUJ');
  const lay = ts[layoutOf[vendor]];
  const findTs = (layer, key) => lay.keys.find(k => k.layer === layer && k.key === key);
  const res = { layout: layoutOf[vendor], phonetic: lay.phonetic, a: [], b7: [], b456: [], c: [], d: [], shreeNothing: [], same: 0, compared: 0 };
  vendorKeys[vendor] = [];
  for (const layer of [0, 1]) {
    for (let kc = 0x21; kc <= 0x7E; kc++) {
      const key = String.fromCharCode(kc);
      let code;
      if (layer === 0) code = kb[kc];
      else { const i = CS.indexOf(key); if (i < 0) continue; code = kb[128 + i]; }
      const r = rows.find(x => x.layer === layer && x.key === key) || { u: null, uh: '', b7: null };
      const cu = cleanU(r.u);
      const t = findTs(layer, key);
      T.keys++;
      vendorKeys[vendor].push({ layer, key, fileCode: hex2(code), composerU: r.u, composerUHex: r.uh, composerClean: cu.text, composer7: r.b7, codeTable: codes[hex2(code)] ? { u: codes[hex2(code)].u, b7: codes[hex2(code)].b7 } : null });
      const shreeTypes = cu.text != null || r.b7 != null;
      const tsUni = t.uni, tsTyped = t.uniHandled ? tsUni : (tsUni.length ? tsUni : '');
      const passedThrough = !t.uniHandled;
      const item = { layer, key: (layer ? 'Ctrl+Space ' : '') + key, fileCode: hex2(code), shreeU: cu.text, shreeUHex: cu.text ? U(cu.text) : '', shreeRawU: r.uh, shree7: r.b7, tsUni, tsUniHex: tsUni ? U(tsUni) : '', tsHandled: t.uniHandled, ts7: t.g7Hex, ts456: t.g456Hex };
      if (!shreeTypes) {
        // Shree types nothing (code FF / 00) on this key: does TextSoft type a character?
        if (code !== 0xFF && code !== 0x00 && r.u == null && r.b7 == null) { item.note = 'code ' + hex2(code) + ' with no answer from either composer'; }
        if (t.uniHandled && tsUni.length > 0) { res.shreeNothing.push(item); T.shreeNothingTextsoftTypes++; }
        continue;
      }
      res.compared++; T.compared++;
      if (cu.why.startsWith('placeholder')) { item.why = cu.why; }
      if (tsUni === '' || (passedThrough && layer === 1)) { res.a.push(item); T.a_textsoftNothing++; continue; }
      if (cu.text == null) {
        // the U composer has no answer but the 7 composer types bytes (legacy-only codes E9 EA 7E 3C EE...)
        item.why = 'only the Shree-Lipi 7 composer types this code';
        const want7 = r.b7;
        if (t.g7Hex !== want7) { res.b7.push(item); T.b_bytes7++; }
        else { res.same++; T.same++; }
        continue;
      }
      if (cu.text !== tsUni) {
        // quirk classes: phonetic vowel key answering its matra; half form answered where the chart prints the letter
        const q = (SIGN.test(cu.text) && !SIGN.test(tsUni)) ? 'composer answered the sign where TextSoft types the letter/vowel (key in isolation)'
          : (cu.text.endsWith('\u0ACD\u200D') && tsUni === cu.text.slice(0, -2)) ? 'composer answered the half form'
          : null;
        if (q) { item.why = q; res.d.push(item); T.d_quirk++; } else { res.c.push(item); T.c_differentUnicode++; }
        continue;
      }
      let bad = false;
      if (r.b7 != null && t.g7Hex !== r.b7) { res.b7.push(item); T.b_bytes7++; bad = true; }
      if (r.b7 != null && t.g456Hex !== to456(r.b7) && t.g456Hex !== r.b7) { res.b456.push(item); T.b_bytes456++; bad = true; }
      if (!bad) { res.same++; T.same++; }
    }
  }
  out.keyboards[vendor] = res;
}
// Ctrl+Shift+1 symbol layer: TextSoft vs the chart's symbols and the 7 composer's bytes for those characters
const symCode = {};
for (const [c, s] of Object.entries(codes)) if (s.u) { const cu = cleanU(s.u).text; if (cu && !(cu in symCode)) symCode[cu] = { code: c, b7: s.b7 }; }
const sym = {};
for (const [id, lay] of Object.entries(ts)) {
  sym[id] = lay.keys.filter(k => k.layer === 2 && k.uniHandled && k.uni !== '').map(k => ({ key: 'Ctrl+Shift+1 ' + k.key, uni: k.uni, uniHex: k.uniHex, ts7: k.g7Hex, ts456: k.g456Hex, shreeCode: symCode[k.uni] ? symCode[k.uni].code : null, shree7: symCode[k.uni] ? symCode[k.uni].b7 : null }));
}
out.symbolLayer = sym;
out.totals = T;
fs.writeFileSync(path.join(A, 'results', 'l1-compare.json'), JSON.stringify(out, null, 1));
fs.writeFileSync(path.join(A, 'results', 'composer-vendor-keys.json'), JSON.stringify({ about: 'Every key of the 19 vendor .GUJ keyboards, layer 0 (printed, normal+shift by ASCII char) and layer 1 (Ctrl+Space), as answered by S8GujU64 (composerU, raw with carriers) and S8Guj764 (composer7, raw Shree-Lipi 7 bytes), plus the code the 406-byte file holds for the key. Captured 2026-10-18 from byte copies C:/Users/harde/AppData/Local/Temp/r3g/VA?.GUJ, twice, stable.', keyboards: vendorKeys }, null, 1));
console.log(JSON.stringify(T));
for (const [v, r] of Object.entries(out.keyboards)) console.log(v.padEnd(9), 'compared', r.compared, 'same', r.same, 'a', r.a.length, 'c', r.c.length, 'b7', r.b7.length, 'b456', r.b456.length, 'd', r.d.length, 'shreeNothing', r.shreeNothing.length);
