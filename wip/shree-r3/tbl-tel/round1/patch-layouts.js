// Telugu layout fixes found by the per-key audit (round1/audit: the real TypingEngine against Shree's composers, key by
// key, cap-tel/tel-vendor-keys.json). Every slot is checked against Shree's own keyboard file (C:\Shree-Lipi nxt\<KB>.TEL,
// 406 bytes: byte[ascii] = the code a key types, FF = nothing; bytes 128-165 = Ctrl+Space in the order
// 1234567890-=qwertyuiopasdfghjklzxcvbnm) and against the value it replaces; anything unexpected throws.
// Usage: node patch-layouts.js <layoutsDir> [ShreeDir]   (apply to main's layouts: tel-itr / tel-newroma as main has them)
const fs = require("fs"), path = require("path");
const dir = process.argv[2], shree = process.argv[3] || "C:/Shree-Lipi nxt";
const BS = String.fromCharCode(92), NL = String.fromCharCode(10);
const CS_ORDER = "1234567890-=qwertyuiopasdfghjklzxcvbnm";
const tel = {};
const kbOf = { "tel-apple": "APPLE", "tel-crtronic": "CRTRONIC", "tel-doe": "DOE", "tel-eng": "ENG", "tel-godrej": "GODREJ", "tel-indica": "INDICA",
  "telugu-inscript": "INSCRIPT", "tel-itr": "ITR", "tel-micro": "MICRO", "tel-mono": "MONO", "tel-newroma": "NEWROMA", "tel-phonetic": "PHONETIC",
  "tel-prakash": "PRAKASH", "tel-roma": "ROMA", "tel-typewrit": "TYPEWRIT", "tel-vision": "VISION" };
function bytes(kb) { return tel[kb] ??= fs.readFileSync(path.join(shree, kb + ".TEL")); }
function code(kb, ch, ctrlSpace) {
  const b = bytes(kb);
  if (b.length !== 406) throw new Error(kb + ".TEL is " + b.length + " bytes");
  if (!ctrlSpace) return b[ch.charCodeAt(0)];
  const i = CS_ORDER.indexOf(ch);
  if (i < 0) throw new Error("no Ctrl+Space slot for " + ch);
  return b[128 + i];
}
function vkOf(c) {
  if (c >= "a" && c <= "z") return [c.toUpperCase().charCodeAt(0), false];
  if (c >= "A" && c <= "Z") return [c.charCodeAt(0), true];
  if (c >= "0" && c <= "9") return [c.charCodeAt(0), false];
  const sh = ")!@#$%^&*(".indexOf(c);
  if (sh >= 0) return [48 + sh, true];
  const m = { ";": [186, 0], ":": [186, 1], "=": [187, 0], "+": [187, 1], ",": [188, 0], "<": [188, 1], "-": [189, 0], "_": [189, 1],
    ".": [190, 0], ">": [190, 1], "/": [191, 0], "?": [191, 1], "`": [192, 0], "~": [192, 1], "[": [219, 0], "{": [219, 1],
    [BS]: [220, 0], "|": [220, 1], "]": [221, 0], "}": [221, 1], "'": [222, 0], "\"": [222, 1] };
  if (m[c]) return [m[c][0], m[c][1] === 1];
  throw new Error("no key for " + c);
}
const files = {}, raws = {};
function layout(id) {
  if (!files[id]) { const p = path.join(dir, id + ".json"); raws[id] = fs.readFileSync(p, "utf8"); files[id] = JSON.parse(raws[id]); }
  return files[id];
}
const log = [];
function setKey(id, ch, from, to, wantCode, why) {
  const [vk, shifted] = vkOf(ch);
  const j = layout(id), k = j.keys.find(k => k.vk === vk);
  if (!k) throw new Error(`${id}: no key vk ${vk} for '${ch}'`);
  const slot = shifted ? "shift" : "base";
  const have = k[slot];
  if (have === to) { log.push(`${id} '${ch}': already ${JSON.stringify(to)}`); return; }
  if (from !== undefined && have !== from) throw new Error(`${id} '${ch}' ${slot} is ${JSON.stringify(have)}, not ${JSON.stringify(from)}`);
  const c = code(kbOf[id], ch, false);
  if (wantCode !== undefined && c !== wantCode) throw new Error(`${id} '${ch}': ${kbOf[id]}.TEL has ${c.toString(16)}, not ${wantCode.toString(16)}`);
  k[slot] = to;
  log.push(`${id} '${ch}' (vk ${vk} ${slot}): ${JSON.stringify(have)} -> ${JSON.stringify(to)} (${kbOf[id]}.TEL ${c.toString(16).toUpperCase()}): ${why}`);
}
function setEscape(id, ch, from, to, wantCode, why) {
  const j = layout(id), e = (j.escape || []).find(e => e.keys === ch);
  if (!e) throw new Error(`${id}: no Ctrl+Space entry ${ch}`);
  if (e.to === to) { log.push(`${id} Ctrl+Space '${ch}': already ${JSON.stringify(to)}`); return; }
  if (e.to !== from) throw new Error(`${id} Ctrl+Space '${ch}' is ${JSON.stringify(e.to)}, not ${JSON.stringify(from)}`);
  const c = code(kbOf[id], ch, true);
  if (c !== wantCode) throw new Error(`${id} Ctrl+Space '${ch}': ${kbOf[id]}.TEL has ${c.toString(16)}, not ${wantCode.toString(16)}`);
  e.to = to;
  // the run machinery keeps Ctrl+Space keys in "sequences" too (Ø + key): keep both the same
  const s = (j.sequences || []).find(s => s.keys === "\u00D8" + ch);
  if (s && s.to === from) s.to = to;
  log.push(`${id} Ctrl+Space '${ch}': ${JSON.stringify(from)} -> ${JSON.stringify(to)} (${kbOf[id]}.TEL ${c.toString(16).toUpperCase()})${s ? " (and its run)" : ""}: ${why}`);
}

// 1. Conjunct keys stored in visual order: the ra-vattu before its letter. Shree's slots are D7 67 (త్ర) and D7 4F (శ్ర).
for (const id of ["tel-indica", "telugu-inscript"]) {
  setKey(id, "^", "\u0C4D\u0C30\u0C24", "\u0C24\u0C4D\u0C30", undefined, "త్ర typed as ్రత (the chart's visual order; not text): Shree's slot D7 67 is త్ర");
  setKey(id, "*", "\u0C4D\u0C30\u0C36", "\u0C36\u0C4D\u0C30", undefined, "శ్ర typed as ్రశ: Shree's slot D7 4F is శ్ర");
}

// 2. The ై key (Shree code 6D) typing ఐ్ / ఐ‌ / ఐొ alone - the Unicode composer's placeholder answer copied from the
//    capture, not a character. Alone it types ఐ (as every vendor keyboard's vowel-sign key does alone); after a
//    consonant the keyboard's own chart run (kw = కై) then teaches TypingOrder that it types ై on every letter, where it
//    typed Cఐ్ on everything but క.
const AI = "\u0C10";
const aiKeys = [
  ["tel-doe", "w", "\u0C10\u0C4D"], ["tel-godrej", "q", "\u0C10\u0C4D"], ["tel-indica", "w", "\u0C10\u200C"], ["telugu-inscript", "w", "\u0C10\u0C4D"],
  ["tel-prakash", "w", "\u0C10\u0C4D"], ["tel-typewrit", "q", "\u0C10\u0C4D"], ["tel-crtronic", "X", "\u0C10\u0C4D"], ["tel-newroma", "[", "\u0C10\u0C4D"],
  ["tel-roma", "`", "\u0C10\u0C4D"], ["tel-eng", "`", "\u0C10\u0C4D"], ["tel-phonetic", "`", "\u0C10\u0C4D"], ["tel-itr", "O", "\u0C10\u0C4A"],
];
for (const [id, ch, from] of aiKeys) setKey(id, ch, from, AI, 0x6D, "the ై key alone types ఐ (it typed a placeholder, and Cఐ్ after any letter but క)");
setEscape("tel-newroma", "e", "\u0C10\u0C4D", AI, 0x6D, "the ై key on Ctrl+Space");

// 3. ఎ typed with a ZWNJ after it (Shree code 42 = ఎ): the joiner is not text.
for (const [id, ch] of [["tel-doe", "S"], ["tel-godrej", "F"], ["tel-indica", "Z"], ["telugu-inscript", "Z"], ["tel-itr", ":"]])
  setKey(id, ch, "\u0C0E\u200C", "\u0C0E", 0x42, "ఎ, without the ZWNJ the chart's cell carried");

// 5. Keys where Shree's keyboard file holds a code the layout types otherwise, and the chart repeats itself or prints
//    the code's glyph as the full letter:
//    - TYPEWRIT @ is Shree's conjunct slot 64 D2 (link + ఞ): the subjoined ఞ, how జ్ఞ is typed there (j @); the chart
//      printed the full ఞ, so జ్ఞానం came out జఞానం. TYPEWRIT has no other ఞ key in Shree either.
//    - PRAKASH Shift+D is the link 64 (it typed the unshifted d, అ).
//    - ITR a e i q r w are FF in ITR.TEL: the chart's letters there repeat its Ctrl+Space row (Ø a = గ ...), which is where
//      Shree has them (codes 77 58 69 55 5E 57).
setKey("tel-typewrit", "@", "ఞ", "్ఞ", undefined, "the subjoined ఞ (Shree's slot 64 D2)");
setKey("tel-prakash", "D", undefined, "్", 0x64, "the link (virama), Shree's 64");
for (const ch of "aeiqrw") {
  const [vk] = vkOf(ch); const k = layout("tel-itr").keys.find(k => k.vk === vk);
  const esc = (layout("tel-itr").escape || []).find(e => e.keys === ch);
  if (!esc || k.base !== esc.to) { if (k.base === "") { log.push("tel-itr '" + ch + "': already \"\""); continue; } throw new Error("tel-itr " + ch + ": base " + k.base + " is not its Ctrl+Space " + (esc && esc.to)); }
  setKey("tel-itr", ch, esc.to, "", 0xFF, "Shree's ITR has nothing on " + ch + "; " + esc.to + " is Ctrl+Space " + ch + ", where the chart prints it too");
}

// 6. The halant key (Shree code 44 = virama + ZWNJ: the visible virama, no conjunct - the composer answers it "్‌") typing
//    the bare virama, the same as the link key (64): a consonant after it then made a conjunct (గ + halant + క typed గ్క,
//    56 E6 FC 50, where Shree shows గ్‌క, 56 8A FC 4D E6 FC). It types ్ + ZWNJ, as the charts' own runs do on ka (ITR fS = క్‌).
for (const [id, kb] of Object.entries(kbOf)) {
  const j = layout(id);
  for (let a = 0x21; a <= 0x7E; a++) {
    const ch = String.fromCharCode(a);
    if (code(kb, ch, false) !== 0x44) continue;
    const [vk, shifted] = vkOf(ch); const k = j.keys.find(k => k.vk === vk); const slot = shifted ? "shift" : "base";
    if (k && k[slot] === "్") setKey(id, ch, "్", "్‌", 0x44, "the halant key: the visible virama, no conjunct");
  }
  for (const ch of CS_ORDER) {
    if (code(kb, ch, true) !== 0x44) continue;
    const e = (j.escape || []).find(e => e.keys === ch);
    if (e && e.to === "్") setEscape(id, ch, "్", "్‌", 0x44, "the halant key on Ctrl+Space: the visible virama, no conjunct");
  }
}

// 4. Keys Shree's keyboard file leaves empty (FF) where the layout has no entry for that Shift state, so the engine
//    typed the unshifted key's character, or let the raw key through into the Shree font (DOE ` = 60 draws చీ there).
//    Each becomes "" (types nothing, swallowed), as in Shree. (A slot the chart fills is left as the chart has it.)
let empties = 0;
for (const [id, kb] of Object.entries(kbOf)) {
  const j = layout(id);
  for (let a = 0x21; a <= 0x7E; a++) {
    const ch = String.fromCharCode(a);
    let vk, shifted;
    try { [vk, shifted] = vkOf(ch); } catch { continue; }
    if (code(kb, ch, false) !== 0xFF) continue;
    let k = j.keys.find(k => k.vk === vk);
    if (!k) { k = { vk, deadKey: false }; const at = j.keys.findIndex(x => x.vk > vk); if (at < 0) j.keys.push(k); else j.keys.splice(at, 0, k); }
    const slot = shifted ? "shift" : "base";
    if (k[slot] !== undefined && k[slot] !== null) continue;          // the chart prints something there: keep it
    // a run that starts with this key (the chart's own sequence) keeps the key alive
    if ((j.sequences || []).some(s => s.keys.startsWith(ch))) continue;
    const typed = shifted ? (k.base ? JSON.stringify(k.base) : "the raw key") : "the raw key";
    k[slot] = "";
    empties++;
    log.push(`${id} '${ch}' (vk ${vk} ${slot}): unset (typed ${typed}) -> "" (${kb}.TEL ${a.toString(16).toUpperCase()} = FF)`);
  }
}

// 4b. (round 2) Keys whose code in Shree's keyboard file is one only the 7 composer types - E1, a third ra-vattu hook
//     written AFTER the syllable (క + E1 = 4D E6 FC 93; cap-tel\tel7-only7-evidence.json, 555 rows), and E7 E9 EA EB,
//     glyph pieces (3D 2B 3B 25) - which the Unicode composer answers with nothing, so there is no text to type. Where
//     the layout has no entry for that Shift state the engine typed the unshifted key's character instead (APPLE
//     Shift+Q typed అ, ROMA Shift+X ష, VISION > a full stop) or let the raw key through. Each becomes "" (types nothing),
//     as Shree's Unicode composer does. (A slot the chart fills is left as the chart has it.)
const SEVEN_ONLY = new Set([0xE1, 0xE7, 0xE9, 0xEA, 0xEB]);
let sevenOnly = 0;
for (const [id, kb] of Object.entries(kbOf)) {
  const j = layout(id);
  for (let a = 0x21; a <= 0x7E; a++) {
    const ch = String.fromCharCode(a);
    let vk, shifted;
    try { [vk, shifted] = vkOf(ch); } catch { continue; }
    const c = code(kb, ch, false);
    if (!SEVEN_ONLY.has(c)) continue;
    let k = j.keys.find(k => k.vk === vk);
    if (!k) { k = { vk, deadKey: false }; const at = j.keys.findIndex(x => x.vk > vk); if (at < 0) j.keys.push(k); else j.keys.splice(at, 0, k); }
    const slot = shifted ? "shift" : "base";
    if (k[slot] === "") { log.push(`${id} '${ch}': already "" (${kb}.TEL ${c.toString(16).toUpperCase()}, 7-only)`); continue; }
    if (k[slot] !== undefined && k[slot] !== null) { log.push(`${id} '${ch}' (${kb}.TEL ${c.toString(16).toUpperCase()}, 7-only): kept ${JSON.stringify(k[slot])} (the chart prints it)`); continue; }
    if ((j.sequences || []).some(s => s.keys.startsWith(ch))) continue;
    const typed = shifted ? (k.base ? JSON.stringify(k.base) : "the raw key") : "the raw key";
    k[slot] = "";
    sevenOnly++;
    log.push(`${id} '${ch}' (vk ${vk} ${slot}): unset (typed ${typed}) -> "" (${kb}.TEL ${c.toString(16).toUpperCase()}: a code only the 7 composer types, ${c === 0xE1 ? "the ra-vattu hook 93 after the syllable" : "a glyph piece"}; the Unicode composer types nothing)`);
  }
}

for (const [id, j] of Object.entries(files)) {
  const raw = raws[id];
  fs.writeFileSync(path.join(dir, id + ".json"), JSON.stringify(j, null, 2) + (raw.endsWith(NL) ? NL : ""));
}
console.log(log.join(NL));
console.log(`${log.length} edits (${empties} empty slots, ${sevenOnly} 7-only slots), ${Object.keys(files).length} files written`);
