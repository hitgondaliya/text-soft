// Vendor keyboards: turn the per-key captures (capvo/, each of the 12 shipped .PUN keyboards loaded from its ORIGINAL
// path, because the composers switch typing mode by keyboard NAME - ENG/NEWROMA/PHONETIC/ROMA are phonetic) into
//  (1) vendor-rows.json: (unicode, bytes, source, codes) rows for the ground truth, and
//  (2) pun-vendor-keys.json: every key of every keyboard on every tutor layer, both composers, raw and cleaned, so a
//      later per-key audit needs no composer.
// Tutor layers: 0 = the printed keyboard (normal + shift: one ASCII char per key), 1 = Ctrl+Space (code = file[128+i]),
// 2 = the composer's raw code layer (key char = internal code; layers 3..12 answer the same). The Ctrl+Shift+1 symbol
// layer of the charts ($ -> ₹, * -> ×, / -> ÷) is NOT reachable through the tutor (layers-probe.js): it is SLNXT's own.
// For Punjabi both composers answer every layer by the code the key holds (layercheck.js: L1 124/124, L2 570/584 agree;
// the misses are the phonetic keyboards' own modes), so all three layers are aligned and used.
const { fs, readTsv, cleanU, hex2, readKbd, CTRL_SPACE_ORDER, uhex } = require('./lib');
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.PUN$/i.test(f)).sort();
const rows = [];
const keys = { about: 'Every key of the 12 vendor Punjabi keyboards (C:/Shree-Lipi nxt/*.PUN, loaded from their original paths) answered by S8PunU64.dll (TutorGetMappedCodeW, Unicode) and S8Pun764.dll (TutorGetMappedCode, Shree-Lipi 7 raw bytes; the 4/5/6 fonts draw the same bytes, ₹ = 3C exists in 7 only). Two capture runs (capvo/, capvo-b/) were byte-identical, and each run asked every key twice (#stable 1). layer 0 = printed keyboard (normal and shift are different ASCII keys), layer 1 = Ctrl+Space, layer 2 = the composer raw code layer (key char = internal code; NOT the Ctrl+Shift+1 symbol layer, which the tutor does not expose). Per key: codes = the internal codes the key holds (a conjunct slot expanded, typed order), uHex/u = the Unicode composer answer raw / without its carrier, b7 = the 7 composer bytes. u null = no answer or a placeholder; b7 "07 xx" = the 7 composer shows a lone sign on byte 07 (no font has 07).', keyboards: {} };
const layerName = ['', ' (Ctrl+Space)', ' (raw code layer)'];
let skipped = { nonScript: 0, noAnswer: 0 };
function keyCodes(kb, layer, key) {
  let code;
  if (layer === 0) code = kb.bytes[key.charCodeAt(0)];
  else if (layer === 1) { const i = CTRL_SPACE_ORDER.indexOf(key); if (i < 0) return null; code = kb.bytes[128 + i]; }
  else code = key.charCodeAt(0);
  if (code === 0xFF || code === undefined) return null;
  if (code >= 0xB0 && code <= 0xCF) return kb.slots[code - 0xB0];
  if (code >= 0x90 && code <= 0x9F) return kb.slots[code - 0x90 + 32];
  return [code];
}
for (const f of files) {
  const label = f.toUpperCase();
  const kb = readKbd(S + f);
  const capFile = 'capvo/' + f.replace(/\.PUN$/i, '') + '.tsv';
  const { load: stable, rows: caps } = readTsv(capFile);
  const capB = fs.readFileSync('capvo-b/' + f.replace(/\.PUN$/i, '') + '.tsv', 'utf8');
  const twoRuns = capB === fs.readFileSync(capFile, 'utf8');
  if (stable !== 1 || !twoRuns) { console.error('UNSTABLE capture, skipped:', f); continue; }
  const kk = keys.keyboards[label] = { file: S + f, capture: capFile, layers: { 0: {}, 1: {}, 2: {} } };
  for (const r of caps) {
    const codes = keyCodes(kb, r.layer, r.key);
    let u = cleanU(r.u);
    let lb = r.lb ? r.lb.slice() : null;
    if (lb) while (lb.length && lb[lb.length - 1] === 0) lb.pop();
    const b7 = lb && lb.length ? lb.map(hex2).join(' ') : null;
    kk.layers[r.layer][r.key] = { codes: codes ? codes.map(hex2).join(' ') : null, uHex: r.uHex || null, u, b7, uUnits: r.uUnits, lUnits: r.lUnits };
    if (u == null || !b7) { skipped.noAnswer++; continue; }
    u = u.replace(/\u0000+$/, '');
    if (!u.length) continue;
    if (!/[\u0A00-\u0A7F\u0964\u0965₹]/.test(u)) { skipped.nonScript++; continue; } // script text only (punctuation and symbols come from the single-code probes)
    rows.push({ unicode: u, bytes: b7, source: `${label} key ${r.key}${layerName[r.layer]}`, codes: codes ? codes.map(hex2).join(' ') : null, layer: r.layer });
  }
}
// The Ctrl+Shift+1 symbol layer: not reachable through the tutor (layers 2..12 all answer the raw code table, see
// layers/ITR-L12.tsv). Every Punjabi chart (all 13 layouts' "symbols") has the same three keys; their characters are
// internal codes EF DC DD, whose answers come from the single-code probes (capp/P-results.json).
const single = new Map(require('./capp/P-results.json').filter(r => !r.codes.includes(' ')).map(r => [r.codes, r]));
keys.ctrlShift1 = { source: 'the vendor charts (CHM "Special Keys", the same in all 13 Punjabi charts: assets/config/layouts/pun-*.json "symbols") + the composers\' answers for the internal code of each character (single-code probes)', keys: {} };
for (const [k, ch, code] of [['$', '₹', 'EF'], ['*', '×', 'DC'], ['/', '÷', 'DD']]) { const r = single.get(code); keys.ctrlShift1.keys[k] = { char: ch, code, u: r ? r.u : null, b7: r ? r.bytes : null }; }
fs.writeFileSync('vendor-rows.json', JSON.stringify(rows, null, 0));
fs.writeFileSync('pun-vendor-keys.json', JSON.stringify(keys, null, 1));
console.log('rows', rows.length, 'with codes', rows.filter(r => r.codes).length, 'keyboards', Object.keys(keys.keyboards).length, JSON.stringify(skipped));
