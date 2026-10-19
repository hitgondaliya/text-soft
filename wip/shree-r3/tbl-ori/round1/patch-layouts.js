// node patch-layouts.js <layoutsDir> [shreeDir] [--dry]
//
// The Odia layout fixes of the per-key audit (r3\tbl-ori\round1\audit), read off Shree's own .ORI keyboard files
// (406 bytes: 0-127 = the code each ASCII key types, 0xFF = nothing; 128-165 = the Ctrl+Space layer in the order
// 1234567890-=qwertyuiopasdfghjklzxcvbnm). Only the 8 vendor keyboards are touched (Modular has no .ORI file).
// Every change is logged; a slot already holding the wanted value is left alone, so the script can run again
// (after a merge) and changes nothing the second time.
//
//  A. A key Shree's file leaves empty (0xFF) that TextSoft let through raw, or typed the unshifted key's character
//     for (an unset Shift), or typed a Latin letter for (a chart artefact): set to "" - types nothing, as Shree.
//     A raw ASCII key reaches a Shree font as its own code, which draws an Odia glyph there (] ିଁ, ` ଚ, ~ ଣ, + ଣ୍ଟ,
//     ; ନ୍ତ, = ଣ୍ଠ, [ ନ, \ ଙ୍ଖ, | ଢ, { େ, } ିର୍, " ‘). Kept: a key the chart sets to something of its own (the chart
//     wins where it prints a key), and the keys the fonts draw as themselves (! % ( ) , - . / : ? * ').
//  B. The link key (code 64: alone nothing, between letters the conjunct): the virama ୍, where the layout had no
//     key (InScript d, Roma f, Newroma =) or a Latin letter (DOE d, Typewriter d: the chart's "d"). Without it only
//     the conjuncts the chart lists could be typed; ସ d ଝ typed ସdଝ.
//  C. The half key (code D5, virama + ZWJ): ୍‍, where the layout had nothing (Crtronic Z, DOE Z), or an unset
//     Shift that typed the unshifted letter (Newroma F typed ଫ, Phonetic Q typed ୰).
//  D. The hook key (code 51: S8Ori764 types the hook 23, S8OriU64 types nothing - no Unicode text) and code E0
//     (the dotted ଚ E3, no Unicode text either): "" - nothing, as Shree's Unicode composer, never the raw key
//     (DOE = typed 3D, ଣ୍ଠ) or a Latin letter (Typewriter Q typed "Q").
//  E. Ctrl+Space keys holding code E1 (the second ba-phala FD; the charts call it ୍ବ): ୍ବ - the layouts had none, so
//     Ctrl+Space then the key typed the key's own letter (Crtronic l typed ୀ).
//  H. Newroma's chart run "ko" types କେ। (a danda for ା): କୋ, as NEWROMA.ORI's o (ୋ, codes 6C 6B).
//  I. Chart runs whose text contradicts their own keys (InScript KdY = ଖ୍ଭ, where Y types ୠ; DOE Md: = ଶ୍ଚ, where : types ଛ ...):
//     removed - the keys then type, one by one, what Shree's own keyboard types for them (23 runs on 4 layouts).
//  F. InScript Ctrl+Space q / w: the chart repeats the normal layer there (ୌ ୈ, which q / w already type); Shree's
//     INSCRIPT.ORI has % and ‘ (codes 25 22), and the composer types those.
const fs = require('fs');
const path = require('path');
const [dir, shreeArg, dryArg] = process.argv.slice(2);
const capture = JSON.parse(fs.readFileSync(process.env.ORI_VENDOR_KEYS || 'C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-ori/ori-vendor-keys.json', 'utf8'));
const shree = shreeArg && shreeArg !== '--dry' ? shreeArg : 'C:/Shree-Lipi nxt';
const dry = process.argv.includes('--dry');
const VIRAMA = String.fromCharCode(0x0B4D), ZWJ = String.fromCharCode(0x200D), BA = String.fromCharCode(0x0B2C);
const MARK = String.fromCharCode(0xD8);   // EscapeLayer.Mark (Ctrl+Space), as the layouts write it
const files = { 'odia-inscript': 'INSCRIPT.ORI', 'ori-crtronic': 'CRTRONIC.ORI', 'ori-doe': 'DOE.ORI', 'ori-eng': 'ENG.ORI',
                'ori-newroma': 'NEWROMA.ORI', 'ori-phonetic': 'PHONETIC.ORI', 'ori-roma': 'ROMA.ORI', 'ori-typewrit': 'TYPEWRIT.ORI' };
const punct = { 186: ';:', 187: '=+', 188: ',<', 189: '-_', 190: '.>', 191: '/?', 192: '`~', 219: '[{', 220: '\\|', 221: ']}', 222: '\'"' };
function slotOf(ch) {
  if (ch >= 'a' && ch <= 'z') return { vk: ch.toUpperCase().charCodeAt(0), shift: false };
  if (ch >= 'A' && ch <= 'Z') return { vk: ch.charCodeAt(0), shift: true };
  if (ch >= '0' && ch <= '9') return { vk: ch.charCodeAt(0), shift: false };
  const d = ')!@#$%^&*('.indexOf(ch);
  if (d >= 0) return { vk: 48 + d, shift: true };
  for (const [vk, p] of Object.entries(punct)) { const i = p.indexOf(ch); if (i >= 0) return { vk: +vk, shift: i === 1 }; }
  return null;
}
const itself = new Set('!%(),-./:?*\'0123456789'.split(''));
const isLatin = s => typeof s === 'string' && /^[A-Za-z]+$/.test(s);
const escOrder = '1234567890-=qwertyuiopasdfghjklzxcvbnm';
const log = [];
let changes = 0;
for (const [id, file] of Object.entries(files)) {
  const p = path.join(dir, id + '.json');
  const text = fs.readFileSync(p, 'utf8');
  const L = JSON.parse(text);
  const bytes = fs.readFileSync(path.join(shree, file));
  if (bytes.length !== 406) throw new Error(file + ': not a 406-byte Shree keyboard file');
  const phonetic = !!L.phonetic;
  const key = vk => L.keys.find(k => k.vk === vk);
  function set(ch, value, why) {
    const s = slotOf(ch);
    let k = key(s.vk);
    const prop = s.shift ? 'shift' : 'base';
    const before = k ? k[prop] : undefined;
    if (before === value) return;
    if (!k) {
      k = { vk: s.vk, deadKey: false };
      L.keys.push(k);
      L.keys.sort((a, b) => a.vk - b.vk);
    }
    // keep the house property order: vk, base, shift, deadKey
    const rebuilt = { vk: k.vk };
    for (const pr of ['base', 'shift']) { const v = pr === prop ? value : k[pr]; if (v !== undefined) rebuilt[pr] = v; }
    for (const [pr, v] of Object.entries(k)) if (!(pr in rebuilt) && pr !== 'base' && pr !== 'shift') rebuilt[pr] = v;
    L.keys[L.keys.indexOf(k)] = rebuilt;
    log.push(`${id}\t'${ch}'\t${prop} vk ${s.vk}\t${JSON.stringify(before)} -> ${JSON.stringify(value)}\t${why}`);
    changes++;
  }
  // ENG is typed by sound (its scheme drives it: translit/odia-shree-eng.json, patched by patch-eng.js); its key map is only drawn.
  if (!phonetic)
    for (let c = 0x21; c <= 0x7E; c++) {
      const ch = String.fromCharCode(c), code = bytes[c], s = slotOf(ch);
      if (!s) continue;
      const k = key(s.vk), v = k ? (s.shift ? k.shift : k.base) : undefined;
      const fallback = s.shift && v === undefined && k && k.base !== undefined ? k.base : undefined;  // an unset Shift types the normal character
      const isLetter = /[A-Za-z]/.test(ch);
      const ans = (capture.keyboards[file].layers['0'] || {})[ch];
      const special = [0x64, 0xD5, 0x51, 0xE0].includes(code);
      const slot = (code >= 0xB0 && code <= 0xCF) || (code >= 0x90 && code <= 0x9F);   // a conjunct slot of the file
      const nothing = code === 0xFF || (!special && !slot && (!ans || (ans.u == null && !ans.b7)));   // (a key the capture has no answer for: neither composer types anything)   // neither composer types anything for this key
      if (nothing) {
        if (isLatin(v)) set(ch, '', `A: Shree's ${file} has ${code === 0xFF ? 'no code here (FF)' : 'code ' + code.toString(16).toUpperCase() + ', which neither composer types anything for'}; the chart's Latin "${v}" types nothing`);
        else if (v === undefined && fallback !== undefined && fallback !== '') set(ch, '', `A: Shree's ${file} has ${code === 0xFF ? 'no code here (FF)' : 'code ' + code.toString(16).toUpperCase() + ', which neither composer types anything for'}; the unset Shift typed the normal key's ${fallback}`);
        else if (v === undefined && fallback === undefined && !isLetter && !itself.has(ch)) set(ch, '', `A: Shree's ${file} has ${code === 0xFF ? 'no code here (FF)' : 'code ' + code.toString(16).toUpperCase() + ', which neither composer types anything for'}; the key reached the Shree font raw as ${c.toString(16).toUpperCase()}, an Odia glyph there`);
      } else if (ans && !ans.b7 && ans.u === ch && v === undefined && fallback === undefined && !itself.has(ch)) {
        set(ch, ch, `G: ${file} code ${code.toString(16).toUpperCase()}: the Unicode composer types ${ch}, the 7 composer nothing; the key reached the Shree font raw (${c.toString(16).toUpperCase()}, an Odia glyph there) - now the table decides: ${ch} in Unicode, left out and reported in a Shree font`);
      } else if (code === 0x64) {
        if (v === undefined || isLatin(v)) set(ch, VIRAMA, `B: the link key (code 64) - the virama, so any two letters join (was ${v === undefined ? 'no key' : 'the Latin "' + v + '"'})`);
      } else if (code === 0xD5) {
        if (v === undefined || isLatin(v)) set(ch, VIRAMA + ZWJ, `C: the half key (code D5, virama + ZWJ) (was ${v === undefined ? (fallback !== undefined ? 'an unset Shift typing ' + fallback : 'no key') : 'the Latin "' + v + '"'})`);
      } else if (code === 0x51 || code === 0xE0) {
        if (v === undefined || isLatin(v)) set(ch, '', `D: code ${code.toString(16).toUpperCase()} has no Unicode text (the Unicode composer types nothing) (was ${v === undefined ? (fallback !== undefined ? 'an unset Shift typing ' + fallback : 'the raw key') : 'the Latin "' + v + '"'})`);
      }
    }
  // E: Ctrl+Space keys holding E1
  for (let i = 0; i < escOrder.length; i++) {
    const ch = escOrder[i], code = bytes[128 + i];
    if (code !== 0xE1) continue;
    const keys = MARK + ch;
    if (L.sequences.some(q => q.keys === keys)) continue;
    L.sequences.push({ keys, to: VIRAMA + BA });
    if (!L.escape.some(e => e.keys === ch)) L.escape.push({ keys: ch, to: VIRAMA + BA });
    log.push(`${id}\tCtrl+Space '${ch}'\tsequence ${JSON.stringify(keys)}\t(none) -> "${VIRAMA + BA}"\tE: ${file} holds code E1 (the ba-phala the charts call ୍ବ)`);
    changes++;
  }
  // F: InScript Ctrl+Space q / w
  if (id === 'odia-inscript')
    for (const [ch, want] of [['q', '%'], ['w', '‘']]) {
      const i = escOrder.indexOf(ch);
      if (bytes[128 + i] !== (want === '%' ? 0x25 : 0x22)) throw new Error(`${file} Ctrl+Space ${ch}: expected code ${want}`);
      const base = key(ch.toUpperCase().charCodeAt(0)).base;
      const seq = L.sequences.find(q => q.keys === MARK + ch), esc = L.escape.find(e => e.keys === ch);
      if (seq && seq.to === want && esc && esc.to === want) continue;
      if (!seq || seq.to.normalize('NFC') !== base.normalize('NFC')) throw new Error(`odia-inscript Ctrl+Space ${ch}: expected the chart's repeat of the normal key (${base}), found ${seq && seq.to}`);
      log.push(`${id}\tCtrl+Space '${ch}'\tsequence and escape\t${JSON.stringify(seq.to)} -> ${JSON.stringify(want)}\tF: the chart repeats the normal key's ${base} here; ${file} has ${want}`);
      seq.to = want; if (esc) esc.to = want;
      changes++;
    }
  // H: Newroma's chart run "ko" types କେ। - a danda U+0964 where ୋ's ା belongs (the two strokes look alike); NEWROMA.ORI's
  // o holds codes 6C 6B (େ + ା, ୋ), and S8Ori764 writes 7B 4C FF E6 for k o. With the danda, TypingOrder could not learn
  // that o after a letter is its sign: l o k typed ଲଓକ.
  if (id === 'ori-newroma') {
    const ko = L.sequences.find(q => q.keys === 'ko');
    const want = 'କୋ'.normalize('NFC');
    const ans = capture.keyboards[file].layers['0']['o'];
    if (!ans || (ans.u || '').normalize('NFC') !== 'ୋ') throw new Error(`${file} o: expected ୋ, the capture has ${ans && ans.u}`);
    if (ko && ko.to !== want) {
      if (ko.to !== 'କେ।') throw new Error(`ori-newroma run ko: expected the chart's କେ।, found ${ko.to}`);
      log.push(`${id}\trun 'ko'\tsequence\t${JSON.stringify(ko.to)} -> ${JSON.stringify(want)}\tH: the chart's danda for ା; ${file} o is ୋ (codes 6C 6B)`);
      ko.to = want;
      changes++;
    }
  }
  // I: chart runs whose text contradicts their own keys - a letter of another key (the chart's InScript runs use y for ବ, Y for
  // ଭ, I for ତ; DOE's swap ଚ and ଛ ...). Each key types what Shree's file has it type (the key maps agree with the .ORI files),
  // so with the run gone the keys type, one by one, what Shree's own keyboard types for them. Checked: the run still holds
  // the chart's text, and Shree's keys one by one (the capture's answer for each key, the link code 64 as the virama) differ.
  const wrongRuns = {
    'odia-inscript': ['KdY', 'NdY', 'cdY', 'odY', 'UdI', 'Udi', 'kdI', 'odI', 'Vdl', 'ydO', 'ydo', 'ydp', 'ydy'],
    'ori-doe': ['Md:', 'Md;', 'NdY', 'odI', 'vdOdy'],
    'ori-typewrit': ['$dJ', 'ZdS', 'qdq', 'tds'],
    'ori-roma': ['tfk'],
  }[id] || [];
  const answers = capture.keyboards[file].layers['0'];
  const shreeKeys = keys => [...keys].map(c => { const e = answers[c]; if (!e) return null; return e.codes === '64' ? VIRAMA : e.u; });
  for (const keys of wrongRuns) {
    const i = L.sequences.findIndex(q => q.keys === keys);
    if (i < 0) continue;   // already gone
    const parts = shreeKeys(keys);
    if (parts.some(p => p == null)) throw new Error(`${id} run ${keys}: a key with no answer in ${file}`);
    const shreeText = parts.join('').normalize('NFC');
    const runText = L.sequences[i].to;
    if (runText.normalize('NFC') === shreeText) throw new Error(`${id} run ${keys}: agrees with ${file} (${shreeText})`);
    L.sequences.splice(i, 1);
    log.push(`${id}\trun '${keys}'\tremoved\t${JSON.stringify(runText)} -> (keys one by one) ${JSON.stringify(shreeText)}\tI: the chart's run contradicts its keys; ${file} types ${shreeText}`);
    changes++;
  }
  const out = JSON.stringify(L, null, 2) + '\n';
  if (out !== text && !dry) fs.writeFileSync(p, out);
}
for (const l of log) console.log(l);
console.log(`${changes} changes${dry ? ' (dry run)' : ''}`);
