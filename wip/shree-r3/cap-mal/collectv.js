// Vendor keyboards: turn the per-key captures (capvo/, each of the 15 shipped .MAL keyboards loaded from its ORIGINAL
// path, because the composers switch typing mode by keyboard NAME) into
//  (1) vendor-rows.json: (unicode, bytes, source, codes) rows for the ground truth, and
//  (2) mal-vendor-keys.json: every key of every keyboard on every tutor layer, both composers, raw and cleaned, so a
//      later per-key audit needs no composer.
// Tutor layers: 0 = the printed keyboard (normal + shift: one ASCII char per key), 1 = Ctrl+Space (code = file[128+i]),
// 2 = the composer's raw code layer (key char = internal code; layers 3..12 answer exactly as layer 2: layers/DOE.tsv).
// The Ctrl+Shift+1 symbol layer of the charts is NOT reachable through the tutor; it is filled from the charts
// (assets/config/layouts/mal-*.json "symbols") + the single-code probes. (adapted from ../cap-kan/collectv.js)
const { fs, readTsv, cleanU, hex2, readKbd, CTRL_SPACE_ORDER } = require('./lib');
const { key } = require('./expected');
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.MAL$/i.test(f)).sort();
const rows = [];
const keys = { about: "Every key of the 15 vendor Malayalam keyboards (C:/Shree-Lipi nxt/*.MAL, loaded from their original paths) answered by S8MalU64.dll (TutorGetMappedCodeW, Unicode) and S8Mal764.dll (TutorGetMappedCode, Shree-Lipi 7 raw bytes; all seven installed SHREE-MAL7 / Shree-Mal7 faces have the same 189 codes on the same glyph ids: fonts/family-check.json; no 4/5/6 Malayalam face is installed). Two capture runs (capvo/, capvo-b/) were byte-identical; each run asked every key twice (#stable 1 on 13 keyboards; ENG and NEWROMA give #stable 0 - the second pass inside one process differs from the first, phonetic-mode state - but run A and run B agree byte for byte); every load verified in SLNXT's shared table (#loaded 1). layer 0 = printed keyboard (normal and shift are different ASCII keys), layer 1 = Ctrl+Space, layer 2 = the composer raw code layer (key char = internal code; NOT the Ctrl+Shift+1 symbol layer, which the tutor does not expose - see ctrlShift1). Per key: codes = the internal codes the key holds (a conjunct slot expanded, typed order), uHex/u = the Unicode composer answer raw / without its carrier, uRead = u in NFC, b7 = the 7 composer bytes. u null = no answer or a placeholder. Carriers to know: the Unicode composer shows a lone pre-base sign on a carrier ('.െ', '*്ര'); the phonetic keyboards (ENG, PHONETIC) answer a consonant key as C + virama, and their lone vowel-sign keys come back on the carrier byte 07 (no glyph in any face); the precomposed sign codes DC ോ DD ൊ DE ൈ DF ൌ (INSCRIPT/KERGOVT keys a ` w q, and others) make the 7 composer write NOTHING.", keyboards: {} };
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
  const label = f.toUpperCase().replace(/\.MAL$/, '');
  const kb = readKbd(S + f);
  const base = f.replace(/\.MAL$/i, '');
  const capFile = 'capvo/' + base + '.tsv';
  const { load: stable, loaded, rows: caps } = readTsv(capFile);
  const capB = fs.readFileSync('capvo-b/' + base + '.tsv', 'utf8');
  const twoRuns = norm(capB) === norm(fs.readFileSync(capFile, 'utf8'));
  if (loaded !== '1' || !twoRuns) { console.error('UNSTABLE capture, skipped:', f); continue; }
  const kk = keys.keyboards[label] = { file: S + f, capture: capFile, stableInsideRun: stable === 1, layers: { 0: {}, 1: {}, 2: {} } };
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
    if (!/[\u0D00-\u0D7F₹]/.test(u)) { skipped.nonScript++; continue; } // script text only (punctuation and symbols come from the single-code probes)
    rows.push({ unicode: uRead, uRaw: u, bytes: b7, source: `${label} key ${r.key}${layerName[r.layer]}`, codes: codes ? codes.map(hex2).join(' ') : null, layer: r.layer, phonetic: stable !== 1 || /^(ENG|PHONETIC|ISMENG|NEWROMA|ROMA)$/.test(label) });
  }
}
// The Ctrl+Shift+1 symbol layer: not reachable through the tutor. Its characters are internal codes whose answers come
// from the single-code probes.
const single = new Map(require('./capp/P-results.json').filter(r => !r.codes.includes(' ')).map(r => [r.codes, r]));
keys.ctrlShift1 = { source: "the vendor charts (CHM 'Special Keys'; assets/config/layouts/mal-*.json 'symbols': 16 of the 17 Malayalam charts list only $ = ₹, NEWROMA lists 14 keys) + the composers' answers for the internal code of each character (single-code probes, capp/P-results.json). b7 null = the 7 composer types nothing for that code; ₹ = 3C has NO glyph in any Malayalam 7 face.", keys: {} };
const CS1 = [['$', '₹', 'E7'], ['*', '×', 'E0'], ['-', '—', '7B'], ['/', '÷', 'E1'], ['K', 'ൡ', 'E5'], ['L', 'ഌ', 'E4'], ['R', 'ൠ', 'E6'], ['_', '–', '7D'], ['k', 'ൣ', 'EB'], ['l', 'ൢ', 'EA'], ['q', 'ഽ', 'E8'], ['[', '[', '5B'], [']', ']', '5D'], ['|', '॥', null]];
for (const [k, ch, code] of CS1) { const r = code ? single.get(code) : null; keys.ctrlShift1.keys[k] = { char: ch, code, u: r ? r.u : null, b7: r ? r.bytes : null, charts: k === '$' ? 'all 17 charts' : 'NEWROMA chart only' }; }
keys.answeredKeysPerLayer = perKb;
fs.writeFileSync('vendor-rows.json', JSON.stringify(rows, null, 0));
fs.writeFileSync('mal-vendor-keys.json', JSON.stringify(keys, null, 1));
console.log('rows', rows.length, 'with codes', rows.filter(r => r.codes).length, 'keyboards', Object.keys(keys.keyboards).length, JSON.stringify(skipped));
console.log(JSON.stringify(perKb));
console.log(JSON.stringify(keys.ctrlShift1.keys));
