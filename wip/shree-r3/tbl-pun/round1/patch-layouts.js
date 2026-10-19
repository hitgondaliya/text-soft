// Layout-data fixes from the per-key audit (round1/audit/l1compare.js against cap-pun/pun-vendor-keys.json).
// Text edits on the JSON (keeps the house format: one key object per line is NOT assumed - the file is re-serialised
// with the same 2-space indent and \uXXXX escapes the layouts use). Usage: node patch-layouts.js <layoutsDir>
const fs = require('fs'), path = require('path');
const dir = process.argv[2];
const AVAGRAHA = '\u093D';
const log = [];

function load(id) { const p = path.join(dir, id + '.json'); return { p, raw: fs.readFileSync(p, 'utf8'), j: JSON.parse(fs.readFileSync(p, 'utf8')) }; }

// The layouts are 2-space JSON with raw UTF-8 and no null fields: JSON.stringify round-trips them byte for byte
// (checked on all 11 files before editing).
function save(f) {
  const strip = o => { for (const k of Object.keys(o)) if (o[k] === null) delete o[k]; return o; };
  f.j.keys.forEach(strip);
  const nl = String.fromCharCode(10);
  fs.writeFileSync(f.p, JSON.stringify(f.j, null, 2) + (f.raw.endsWith(nl) ? nl : ""));
}

function key(f, vk) {
  let k = f.j.keys.find(k => k.vk === vk);
  if (!k) { k = { vk, base: null, shift: null, deadKey: false }; f.j.keys.push(k); f.j.keys.sort((a, b) => a.vk - b.vk); }
  return k;
}
function setShift(id, vk, to, why) {
  const f = load(id); const k = key(f, vk);
  if (k.shift != null && k.shift !== '' && k.shift !== to && !why.startsWith('replace')) throw new Error(`${id} vk ${vk} shift already ${k.shift}`);
  log.push(`${id}: Shift+vk${vk} ${JSON.stringify(k.shift)} -> ${JSON.stringify(to)}  (${why})`);
  k.shift = to; save(f);
}
function setBase(id, vk, to, why) {
  const f = load(id); const k = key(f, vk);
  if (k.base != null && k.base !== '' && k.base !== to) throw new Error(`${id} vk ${vk} base already ${k.base}`);
  log.push(`${id}: vk${vk} ${JSON.stringify(k.base)} -> ${JSON.stringify(to)}  (${why})`);
  k.base = to; save(f);
}
function setBoth(id, vk, base, shift, why) {
  const f = load(id); const k = key(f, vk);
  if ((k.base ?? '') !== '' || (k.shift ?? '') !== '') throw new Error(`${id} vk ${vk} not empty: ${k.base} ${k.shift}`);
  log.push(`${id}: vk${vk} ${JSON.stringify([k.base, k.shift])} -> ${JSON.stringify([base, shift])}  (${why})`);
  k.base = base; k.shift = shift; save(f);
}
function addEscape(id, k, to, why) {
  const f = load(id);
  if (f.j.escape.some(e => e.keys === k)) throw new Error(`${id} escape ${k} exists`);
  if (f.j.sequences.some(s => s.keys === '\u00D8' + k)) throw new Error(`${id} sequence Ø${k} exists`);
  f.j.escape.push({ keys: k, to }); f.j.escape.sort((a, b) => a.keys < b.keys ? -1 : a.keys > b.keys ? 1 : 0);
  f.j.sequences.push({ keys: '\u00D8' + k, to });
  log.push(`${id}: Ctrl+Space ${k} (escape + sequence Ø${k}) -> ${JSON.stringify(to)}  (${why})`);
  save(f);
}

const W41 = 'Shree\'s code 41: the 7 composer writes 40, the avagraha-like glyph the fonts draw at 40 (the tables write U+093D ऽ as 40); TextSoft typed the Ctrl+Space fallback / the unshifted letter';
for (const id of ['pun-crtronic', 'pun-doe', 'pun-godrej', 'pun-itr', 'pun-raming', 'pun-roma']) addEscape(id, 'w', AVAGRAHA, W41);
addEscape('pun-typewrit', '6', AVAGRAHA, W41);
setShift('punjabi-inscript', 86, AVAGRAHA, W41);   // Shift+V
setShift('pun-itr', 66, AVAGRAHA, W41);            // Shift+B
setShift('pun-varitype', 188, AVAGRAHA, W41);      // Shift+, (<)
setShift('pun-newroma', 86, AVAGRAHA, W41);        // Shift+V
setShift('pun-newroma', 87, AVAGRAHA, W41);        // Shift+W
setShift('pun-raming', 190, '\u0A09\u0A02', 'replace: the chart cell "ੳਉ" (two characters for one key) is an artefact; the key holds codes 42 69 62 (ੳ, bindi, ੁ) and the 7 composer writes 44 74 - ੳ with the bindi and the u-sign, drawn ਉਂ');
// Keys the vendor keyboard leaves empty (Shree types nothing) that TextSoft left to the application: in a Shree font the
// raw ASCII draws a letter (\ ਪ, ` ਮ, | ੈਂ, ~ ੋਂ, } ੋ). "" types nothing, as Shree.
const EMPTY = 'Shree\'s keyboard types nothing on this key; TextSoft left it to the application, which typed the ASCII character - a Gurmukhi glyph in a Shree font';
setBoth('pun-doe', 220, '', '', EMPTY);             // \ |
setBoth('pun-doe', 192, '', '', EMPTY);             // ` ~
setBoth('punjabi-inscript', 220, '', '', EMPTY);    // \ |
setBase('punjabi-inscript', 192, ' ', 'Shree\'s InScript types a space on ` (code 20; both composers answer a space); TextSoft left ` to the application');
setBoth('pun-newroma', 192, '', '', EMPTY);         // ` ~
setBoth('pun-phonetic', 220, '', '', EMPTY);        // \ |
setBase('pun-phonetic', 192, '', EMPTY);            // ` (Shift ~ is ੍ਵ)
setBase('pun-raming', 220, '', EMPTY);              // \ (Shift | is ੍ਯ)
setShift('pun-roma', 221, '', EMPTY);               // } (] unchanged: the application types ], which the fonts draw as itself)
fs.writeFileSync(path.join(__dirname, 'layout-fixes.txt'), log.join('\n') + '\n');
console.log(log.join('\n'));
