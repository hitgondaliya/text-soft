// Vendor keyboards: turn the per-key captures (capvo/, each of the 8 shipped .ORI keyboards loaded from its ORIGINAL
// path, because the composers switch typing mode by keyboard NAME - ENG/NEWROMA/PHONETIC are phonetic, INSCRIPT types
// ୈ ୋ ୌ (E7 E8 E9) as େ + length mark) into
//  (1) vendor-rows.json: (unicode, bytes, source, codes) rows for the ground truth, and
//  (2) ori-vendor-keys.json: every key of every keyboard on every tutor layer, both composers, raw and cleaned, so a
//      later per-key audit needs no composer.
// Tutor layers: 0 = the printed keyboard (normal + shift: one ASCII char per key), 1 = Ctrl+Space (code = file[128+i]),
// 2 = the composer's raw code layer (key char = internal code; layers 3..12 answer exactly as layer 2: layers/TYPEWRIT.tsv).
// The Ctrl+Shift+1 symbol layer of the charts is NOT reachable through the tutor: it is SLNXT's own; it is filled from
// the charts (assets/config/layouts/ori-*.json "symbols", the same 20 keys in all 9 Odia charts) + the single-code probes.
// layercheck.js: L1 77/77 (U and 7) agree with the code the key holds; L0/L2 misses are the phonetic keyboards' own modes.
const { fs, readTsv, cleanU, hex2, readKbd, CTRL_SPACE_ORDER, uhex } = require('./lib');
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.ORI$/i.test(f)).sort();
const rows = [];
const keys = { about: 'Every key of the 8 vendor Odia keyboards (C:/Shree-Lipi nxt/*.ORI, loaded from their original paths) answered by S8OriU64.dll (TutorGetMappedCodeW, Unicode) and S8Ori764.dll (TutorGetMappedCode, Shree-Lipi 7 raw bytes; the 4/5/6 fonts draw the same bytes, ₹ = 3C exists in WORI7001/2 only). Two capture runs (capvo/, capvo-b/) were identical (after normalising the random unit count the 7 composer returns for "no answer"), and each run asked every key twice (#stable 1); every load verified in SLNXT\'s shared table (#loaded 1). layer 0 = printed keyboard (normal and shift are different ASCII keys), layer 1 = Ctrl+Space, layer 2 = the composer raw code layer (key char = internal code; NOT the Ctrl+Shift+1 symbol layer, which the tutor does not expose - see ctrlShift1). Per key: codes = the internal codes the key holds (a conjunct slot expanded, typed order), uHex/u = the Unicode composer answer raw / without its carrier, b7 = the 7 composer bytes. u null = no answer or a placeholder ("Lk" = the link code 64 alone); b7 "07" = the 7 composer shows a lone sign on byte 07 in phonetic mode (no font has 07).', keyboards: {} };
const layerName = ['', ' (Ctrl+Space)', ' (raw code layer)'];
const skipped = { nonScript: 0, noAnswer: 0 };
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
const norm = t => t.split('\n').map(l => { if (!l || l[0] === '#') return l; const p = l.split('\t'); if (!(Number(p[2]) >= 0 && Number(p[2]) <= 60)) { p[2] = 'none'; p[3] = ''; } if (!(Number(p[4]) >= 0 && Number(p[4]) <= 60)) { p[4] = 'none'; p[5] = ''; } return p.join('\t'); }).join('\n');
for (const f of files) {
  const label = f.toUpperCase();
  const kb = readKbd(S + f);
  const capFile = 'capvo/' + f.replace(/\.ORI$/i, '') + '.tsv';
  const { load: stable, loaded, rows: caps } = readTsv(capFile);
  const capB = fs.readFileSync('capvo-b/' + f.replace(/\.ORI$/i, '') + '.tsv', 'utf8');
  const twoRuns = norm(capB) === norm(fs.readFileSync(capFile, 'utf8'));
  if (stable !== 1 || loaded !== '1' || !twoRuns) { console.error('UNSTABLE capture, skipped:', f); continue; }
  const kk = keys.keyboards[label] = { file: S + f, capture: capFile, layers: { 0: {}, 1: {}, 2: {} } };
  for (const r of caps) {
    const codes = keyCodes(kb, r.layer, r.key);
    let u = cleanU(r.u);
    let lb = r.lb ? r.lb.slice() : null;
    if (lb) while (lb.length && lb[lb.length - 1] === 0) lb.pop();
    const b7 = lb && lb.length ? lb.map(hex2).join(' ') : null;
    kk.layers[r.layer][r.key] = { codes: codes ? codes.map(hex2).join(' ') : null, uHex: r.uHex || null, u, b7, uUnits: r.uUnits, lUnits: (r.lUnits >= 0 && r.lUnits <= 60) ? r.lUnits : 'none' };
    if (u == null || !b7) { skipped.noAnswer++; continue; }
    u = u.replace(/\u0000+$/, '');
    if (!u.length) continue;
    if (!/[\u0B00-\u0B7F\u0964\u0965₹]/.test(u)) { skipped.nonScript++; continue; } // script text only (punctuation and symbols come from the single-code probes)
    rows.push({ unicode: u, bytes: b7, source: `${label} key ${r.key}${layerName[r.layer]}`, codes: codes ? codes.map(hex2).join(' ') : null, layer: r.layer });
  }
}
// The Ctrl+Shift+1 symbol layer: not reachable through the tutor (layers 2..12 all answer the raw code table). Every Odia
// chart has the same 20 keys; their characters are internal codes whose answers come from the single-code probes.
const single = new Map(require('./capp/P-results.json').filter(r => !r.codes.includes(' ')).map(r => [r.codes, r]));
keys.ctrlShift1 = { source: 'the vendor charts (CHM "Special Keys", the same 20 keys in all 9 Odia charts: assets/config/layouts/ori-*.json and odia-inscript.json "symbols") + the composers\' answers for the internal code of each character (single-code probes, capp/P-results.json). b7 null = the 7 composer types nothing for that code.', keys: {} };
const CS1 = [['$', '₹', 'F5'], ['*', '×', 'DE'], ['-', '—', '7B'], ['.', '।', 'D3'], ['/', '÷', 'DF'], ['K', 'ୡ', 'E4'], ['L', 'ଌ', 'E3'], ['R', 'ୠ', 'E2'], ['[', '[', '5B'], [']', ']', '5D'], ['_', '–', '7D'], ['a', '଼', 'E6'], ['k', 'ୣ', 'F7'], ['l', 'ୢ', 'F6'], ['q', 'ଽ', '5F'], ['r', 'ୄ', 'F8'], ['u', 'ଓଁ', 'F4'], ['w', 'ୱ', 'E5'], ['y', '୍ୟ', 'D2'], ['|', '॥', 'DD']];
for (const [k, ch, code] of CS1) { const r = single.get(code); keys.ctrlShift1.keys[k] = { char: ch, code, u: r ? r.u : null, b7: r ? r.bytes : null }; }
fs.writeFileSync('vendor-rows.json', JSON.stringify(rows, null, 0));
fs.writeFileSync('ori-vendor-keys.json', JSON.stringify(keys, null, 1));
console.log('rows', rows.length, 'with codes', rows.filter(r => r.codes).length, 'keyboards', Object.keys(keys.keyboards).length, JSON.stringify(skipped));
console.log(JSON.stringify(keys.ctrlShift1.keys));
