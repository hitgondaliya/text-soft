// Vendor keyboards: turn the per-key captures (capvo/, each of the 22 shipped .TAM keyboards loaded from its ORIGINAL
// path, because the composers switch typing mode by keyboard NAME) into
//  (1) vendor-rows.json: (unicode, bytes, source, codes) rows for the ground truth, and
//  (2) tam-vendor-keys.json: every key of every keyboard on every tutor layer, both composers, raw and cleaned, so a
//      later per-key audit needs no composer.
// Tutor layers: 0 = the printed keyboard (normal + shift: one ASCII char per key), 1 = Ctrl+Space (code = file[128+i]),
// 2 = the composer's raw code layer (key char = internal code; layers 3..12 answer exactly as layer 2: layers/DOE.tsv).
// The Ctrl+Shift+1 symbol layer of the charts is NOT reachable through the tutor; it is filled from the charts
// (assets/config/layouts/tam-*.json + tamil-inscript.json "symbols": 22 keys in 19 charts, 21 in the other 4) + the single-code probes.
// (adapted from ../cap-kan/collectv.js)
const { fs, readTsv, cleanU, hex2, readKbd, CTRL_SPACE_ORDER } = require('./lib');
const { key } = require('./expected');
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.TAM$/i.test(f)).sort();
const rows = [];
const keys = { about: 'Every key of the 22 vendor Tamil keyboards (C:/Shree-Lipi nxt/*.TAM, loaded from their original paths) answered by S8TamU64.dll (TutorGetMappedCodeW, Unicode) and S8Tam764.dll (TutorGetMappedCode, Shree-Lipi 7 raw bytes; the 4/5/6 fonts SHREE-TAM-* draw the same bytes except 9B 9F BA, redrawn ரி ரீ ர்: fonts/family-check.json). Two capture runs (capvo/, capvo-b/) were byte-identical, and each run asked every key twice (#stable 1); every load verified in SLNXT\'s shared table (#loaded 1). layer 0 = printed keyboard (normal and shift are different ASCII keys), layer 1 = Ctrl+Space, layer 2 = the composer raw code layer (key char = internal code; layers 3..12 answer exactly as layer 2: layers/DOE.tsv; NOT the Ctrl+Shift+1 symbol layer, which the tutor does not expose - see ctrlShift1). Per key: codes = the internal codes the key holds (a conjunct slot expanded, typed order; on INSCRIPT/NEWROMA/PHONET86 the composer ignores the file slots B0-B2 and types its own ோ ொ ௌ), uHex/u = the Unicode composer answer raw / without its carrier, uRead = u in NFC, b7 = the 7 composer bytes. u null = no answer or a placeholder. Carriers to know: a lone pre-base sign (ெ ே ை) comes back on a . carrier from the Unicode composer and as the bare sign glyph F6/F7/F8/F9 from the 7 composer; a lone post-base sign ி ீ ் 5F 40 has no 7 answer (garbage unit count).', keyboards: {} };
const layerName = ['', ' (Ctrl+Space)', ' (raw code layer)'];
const skipped = { nonScript: 0, noAnswer: 0 };
function keyCodes(kb, layer, k) {
  let code;
  if (layer === 0) code = kb.bytes[k.charCodeAt(0)];
  else if (layer === 1) { const i = CTRL_SPACE_ORDER.indexOf(k); if (i < 0) return null; code = kb.bytes[128 + i]; }
  else code = k.charCodeAt(0);
  if (code === 0xFF || code === undefined) return null;
  if (code >= 0xB0 && code <= 0xCF) return kb.slots[code - 0xB0];
  if (code >= 0x90 && code <= 0x9F) return kb.slots[code - 0x90 + 32];
  return [code];
}
const norm = t => t.split('\n').map(l => { if (!l || l[0] === '#') return l; const p = l.split('\t'); if (!(Number(p[2]) >= 0 && Number(p[2]) <= 60)) { p[2] = 'none'; p[3] = ''; } if (!(Number(p[4]) >= 0 && Number(p[4]) <= 60)) { p[4] = 'none'; p[5] = ''; } return p.join('\t'); }).join('\n');
const perKb = {};
for (const f of files) {
  const label = f.toUpperCase().replace(/\.TAM$/, '');
  const kb = readKbd(S + f);
  const base = f.replace(/\.TAM$/i, '');
  const capFile = 'capvo/' + base + '.tsv';
  const { load: stable, loaded, rows: caps } = readTsv(capFile);
  const capB = fs.readFileSync('capvo-b/' + base + '.tsv', 'utf8');
  const twoRuns = norm(capB) === norm(fs.readFileSync(capFile, 'utf8'));
  if (stable !== 1 || loaded !== '1' || !twoRuns) { console.error('UNSTABLE capture, skipped:', f); continue; }
  const kk = keys.keyboards[label] = { file: S + f, capture: capFile, layers: { 0: {}, 1: {}, 2: {} } };
  const cnt = perKb[label] = [0, 0, 0];
  for (const r of caps) {
    const codes = keyCodes(kb, r.layer, r.key);
    let u = cleanU(r.u);
    let lb = r.lb ? r.lb.slice() : null;
    if (lb) while (lb.length && lb[lb.length - 1] === 0) lb.pop();
    const b7 = lb && lb.length ? lb.map(hex2).join(' ') : null;
    if (u != null) u = u.replace(/\u0000+$/, '');
    const uRead = u == null ? null : key(u);
    kk.layers[r.layer][r.key] = { codes: codes ? codes.map(hex2).join(' ') : null, uHex: r.uHex || null, u, uRead, b7, uUnits: r.uUnits, lUnits: (r.lUnits >= 0 && r.lUnits <= 60) ? r.lUnits : 'none' };
    if (u != null || b7) cnt[r.layer]++;
    if (u == null || !b7) { skipped.noAnswer++; continue; }
    if (!u.length) continue;
    if (!/[\u0B80-\u0BFF\u0964\u0965₹]/.test(u)) { skipped.nonScript++; continue; } // script text only (punctuation and symbols come from the single-code probes)
    rows.push({ unicode: uRead, uRaw: u, bytes: b7, source: `${label} key ${r.key}${layerName[r.layer]}`, codes: codes ? codes.map(hex2).join(' ') : null, layer: r.layer });
  }
}
// The Ctrl+Shift+1 symbol layer: not reachable through the tutor. The Tamil charts have 22 keys; their
// characters are internal codes whose answers come from the single-code probes.
const single = new Map(require('./capp/P-results.json').filter(r => !r.codes.includes(' ')).map(r => [r.codes, r]));
keys.ctrlShift1 = { source: 'the vendor charts (CHM Special Keys: assets/config/layouts/tam-*.json and tamil-inscript.json symbols; 22 keys in 19 charts, the same minus * in ENG/IE/INDOWORD/one more) + the composers\' answers for the internal code of each character (single-code probes, capp/P-results.json). b7 null = the 7 composer types nothing for that code; u null = the Unicode composer has no answer. Bytes under 20 have no glyph in any Tamil face.', keys: {} };
const CS1 = [['#', '௺', 'EA'], ['$', '₹', 'DB'], ['*', '×', 'DC'], ['.', '|', 'D9'], ['/', '÷', 'DD'], ['1', '௰', 'E4'], ['2', '௱', 'E5'], ['3', '௲', 'E6'], ['4', '௷', '7C'], ['5', '௶', '5C'], ['6', '௸', '5B'], ['N', 'ணா', 'D4'], ['R', 'றா', '71'], ['[', '[', 'F8'], [']', ']', 'F9'], ['d', '௳', 'E2'], ['m', '௴', 'E9'], ['n', 'னா', '3C'], ['r', '௹', 'EB'], ['u', 'ௐ', 'E7'], ['y', '௵', 'E8'], ['|', '॥', 'ED']];
for (const [k, ch, code] of CS1) { const r = single.get(code); keys.ctrlShift1.keys[k] = { char: ch, code, u: r ? r.u : null, b7: r ? r.bytes : null }; }
keys.answeredKeysPerLayer = perKb;
fs.writeFileSync('vendor-rows.json', JSON.stringify(rows, null, 0));
fs.writeFileSync('tam-vendor-keys.json', JSON.stringify(keys, null, 1));
console.log('rows', rows.length, 'with codes', rows.filter(r => r.codes).length, 'keyboards', Object.keys(keys.keyboards).length, JSON.stringify(skipped));
console.log(JSON.stringify(perKb));
console.log(JSON.stringify(keys.ctrlShift1.keys));
