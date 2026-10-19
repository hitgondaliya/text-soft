// Round 2 Telugu layout fixes (after round1\patch-layouts.js), found by probing every key class of every Shree keyboard
// on EVERY consonant, not only on క (round2\measure: the real TypingEngine). Each slot is checked against Shree's own
// keyboard file (C:\Shree-Lipi nxt\<KB>.TEL: byte[ascii] = the code a key types; bytes 128-165 = Ctrl+Space in the
// order 1234567890-=qwertyuiopasdfghjklzxcvbnm) and against the value it replaces; anything unexpected throws.
// Idempotent. Usage: node patch-layouts-r2.js <layoutsDir> [ShreeDir]
const fs = require("fs"), path = require("path");
const dir = process.argv[2], shree = process.argv[3] || "C:/Shree-Lipi nxt";
const BS = String.fromCharCode(92), NL = String.fromCharCode(10), ESC = "\u00D8";
const CS_ORDER = "1234567890-=qwertyuiopasdfghjklzxcvbnm";
const kbOf = { "tel-apple": "APPLE", "tel-crtronic": "CRTRONIC", "tel-doe": "DOE", "tel-eng": "ENG", "tel-godrej": "GODREJ", "tel-indica": "INDICA",
  "telugu-inscript": "INSCRIPT", "tel-itr": "ITR", "tel-micro": "MICRO", "tel-mono": "MONO", "tel-newroma": "NEWROMA", "tel-phonetic": "PHONETIC",
  "tel-prakash": "PRAKASH", "tel-roma": "ROMA", "tel-typewrit": "TYPEWRIT", "tel-vision": "VISION" };
const tel = {};
function bytes(kb) { return tel[kb] ??= fs.readFileSync(path.join(shree, kb + ".TEL")); }
function code(kb, ch, ctrlSpace) {
  const b = bytes(kb);
  if (b.length !== 406) throw new Error(kb + ".TEL is " + b.length + " bytes");
  if (!ctrlSpace) return b[ch.charCodeAt(0)];
  const i = CS_ORDER.indexOf(ch);
  return i < 0 ? undefined : b[128 + i];
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
  return null;
}
const PRINTABLE = Array.from({ length: 0x7E - 0x21 + 1 }, (_, i) => String.fromCharCode(0x21 + i)).filter(c => vkOf(c));
const files = {}, raws = {};
function layout(id) {
  if (!files[id]) { const p = path.join(dir, id + ".json"); raws[id] = fs.readFileSync(p, "utf8"); files[id] = JSON.parse(raws[id]); }
  return files[id];
}
const log = [];
const touched = new Set();
function keyOf(j, ch) { const [vk] = vkOf(ch); return j.keys.find(k => k.vk === vk); }
function alone(j, ch) {           // what a key types alone in the layout: "x" on layer 0, "Øx" on Ctrl+Space
  if (ch.startsWith(ESC)) { const s = (j.sequences || []).find(s => s.keys === ch); return s ? s.to : undefined; }
  if (!vkOf(ch)) return undefined;
  const [, shifted] = vkOf(ch); const k = keyOf(j, ch);
  if (!k) return undefined;
  const v = shifted ? k.shift : k.base;          // the slot as the layout sets it (an unset Shift slot is not a key of its own)
  return v === null ? undefined : v;
}
function setKey(id, ch, from, to, why) {
  const j = layout(id); const [vk, shifted] = vkOf(ch); const slot = shifted ? "shift" : "base";
  let k = j.keys.find(k => k.vk === vk);
  if (!k) { k = { vk, deadKey: false }; const at = j.keys.findIndex(x => x.vk > vk); if (at < 0) j.keys.push(k); else j.keys.splice(at, 0, k); }
  const have = k[slot];
  if (have === to) { log.push(`${id} '${ch}': already ${JSON.stringify(to)}`); return; }
  if (from !== "*" && have !== from) throw new Error(`${id} '${ch}' ${slot} is ${JSON.stringify(have)}, not ${JSON.stringify(from)}`);
  k[slot] = to; touched.add(id);
  log.push(`${id} '${ch}' (vk ${vk} ${slot}): ${JSON.stringify(have)} -> ${JSON.stringify(to)}: ${why}`);
}
function addRun(id, keys, to, why) {
  const j = layout(id); j.sequences ??= [];
  const s = j.sequences.find(s => s.keys === keys);
  if (s) { if (s.to === to) { log.push(`${id} run ${keys}: already ${to}`); return false; } throw new Error(`${id} run ${keys} is ${s.to}, not ${to}`); }
  j.sequences.push({ keys, to }); touched.add(id);
  log.push(`${id} run ${keys} = ${to}: ${why}`);
  return true;
}
const kaKey = j => PRINTABLE.find(c => alone(j, c) === "\u0C15" && !(keyOf(j, c) || {}).deadKey);

// 1. The link key (Shree code 64) that typed nothing alone on DOE, GODREJ, INDICA, INSCRIPT and VISION: d was unset, so
//    only the chart's own runs on it made a conjunct (kdk, kdj ...) and every other one came out as two letters
//    (InScript k d l typed కత, not క్త). It types the virama, as the link key of the other ten keyboards does.
for (const id of ["tel-doe", "tel-godrej", "tel-indica", "telugu-inscript", "tel-vision"]) {
  const c = code(kbOf[id], "d", false);
  if (c !== 0x64) throw new Error(`${id}: ${kbOf[id]}.TEL d is ${c.toString(16)}, not 64`);
  setKey(id, "d", undefined, "\u0C4D", "the link key (Shree 64) types the virama; it typed nothing alone");
}

// 2. Modular (a chart only; Shree ships no MODULAR.TEL): its runs "\e" = క్ర and "eØx" = కఁ show \ as the ra-vattu
//    (struck before its letter, as on the typewriter keyboards) and Ctrl+Space x as ఁ - neither typed anything alone.
setKey("tel-modular", BS, undefined, "\u0C4D\u0C30", "the ra-vattu, struck before its letter (the chart's run \\e = క్ర)");
addRun("tel-modular", ESC + "x", "\u0C01", "Ctrl+Space x is ఁ (the chart's run eØx = కఁ)");
{ const j = layout("tel-modular"); j.escape ??= []; if (!j.escape.some(e => e.keys === "x")) { j.escape.push({ keys: "x", to: "\u0C01" }); j.escape.sort((a, b) => a.keys < b.keys ? -1 : 1); } }

// 3. Phonetic: Shree's composers type PHONETIC.TEL by sound, as ENG.TEL (they switch typing mode by the keyboard's
//    name; the two files differ only on Shift+F Q W, which hold nothing, and Shift+X, the link). The layout was a fixed
//    keyboard whose consonant keys type C్, so every sign the chart teaches on క failed on every other consonant
//    (gi = గ్ఇ). It is typed by sound now, with the Eng keyboard's scheme; Shift+F Q W X type nothing, as there.
{
  const id = "tel-phonetic", j = layout(id);
  if (j.phonetic !== true) {
    const o = {}; for (const [k, v] of Object.entries(j)) { o[k] = v; if (k === "name") o.phonetic = true; }
    files[id] = o; touched.add(id); log.push(`${id}: "phonetic": true (Shree types PHONETIC.TEL by sound, as ENG.TEL)`);
  } else log.push(`${id}: already phonetic`);
  for (const ch of "FQWX") {
    const c = code("PHONETIC", ch, false);
    if (!(c === 0xFF || (ch === "X" && c === 0x64))) throw new Error(`PHONETIC.TEL ${ch} is ${c.toString(16)}`);
    setKey(id, ch, "*", "", `PHONETIC.TEL ${c.toString(16).toUpperCase()}: ${c === 0xFF ? "nothing" : "the link, which typing by sound has no use for"} (ENG.TEL has a letter there)`);
  }
  // The chart's run k=r = క్ర is Newroma's: PHONETIC.TEL has '=' itself on = (3D), and by sound క్ర is kr.
  if (code("PHONETIC", "=", false) !== 0x3D) throw new Error("PHONETIC.TEL = is not 3D");
  const at = (layout(id).sequences || []).findIndex(s => s.keys === "k=r");
  if (at >= 0) { layout(id).sequences.splice(at, 1); touched.add(id); log.push(`${id}: run k=r = క్ర removed (PHONETIC.TEL = is '=' itself; by sound క్ర is kr)`); }
}

// 4. Shift+7 holds FE in NEWROMA, PHONETIC and ENG.TEL, a code the composers type nothing for; the layouts left it unset,
//    so it typed the 7 key's ౭.
for (const id of ["tel-newroma", "tel-phonetic", "tel-eng"]) {
  const c = code(kbOf[id], "&", false);
  if (c !== 0xFE) throw new Error(`${id}: & is ${c.toString(16)}`);
  const k = keyOf(layout(id), "&");
  if (k && k.shift !== undefined && k.shift !== null && k.shift !== "") { log.push(`${id} '&': kept ${JSON.stringify(k.shift)}`); continue; }
  setKey(id, "&", k ? k.shift : undefined, "", "Shree's FE there types nothing; it typed ౭");
}

// 5. InScript Shift+1 2 4 and Shift+V hold code 20 in INSCRIPT.TEL: Shree types a space there; the layout left them
//    unset, so they typed ౧ ౨ ౪ and న.
for (const ch of "!@$V") {
  const c = code("INSCRIPT", ch, false);
  if (c !== 0x20) throw new Error(`INSCRIPT ${ch} is ${c.toString(16)}`);
  const k = keyOf(layout("telugu-inscript"), ch);
  setKey("telugu-inscript", ch, k ? k.shift : undefined, " ", "INSCRIPT.TEL 20: Shree types a space");
}

// 6. APPLE's run "Link" = క్ర is the help page's word "Link", not a keystroke: it made L i (ణ + ఉ) a run in progress,
//    so ి after ణ never became its sign (L i typed ణఉ).
{ const j = layout("tel-apple"); const at = (j.sequences || []).findIndex(s => s.keys === "Link");
  if (at >= 0) { j.sequences.splice(at, 1); touched.add("tel-apple"); log.push(`tel-apple: run "Link" = ${"\u0C15\u0C4D\u0C30"} removed (the help page's word, not keys)`); } }

// 7. Vowel-sign keys (Shree codes below) that the chart never shows on క: alone they type the vowel, and after a letter
//    Shree's composer types the sign - but TextSoft learns that only from a chart run on క, so after every letter they
//    typed the vowel (GODREJ 1 B typed ధఔ, NEWROMA < ] ఠఒ, ROMA B @ భౠ). Each gets the chart's run on క it lacks.
const SIGN = { 0x24: "ో", 0x26: "ూ", 0x40: "ౌ", 0x5F: "ౄ", 0x61: "ా", 0x62: "ె", 0x6D: "ై", 0x6E: "ు", 0x70: "ొ", 0x73: "ి", 0x76: "ీ", 0x7A: "ృ", 0xD3: "ే" };
const VOWEL = { "ా": "ఆ", "ి": "ఇ", "ీ": "ఈ", "ు": "ఉ", "ూ": "ఊ", "ృ": "ఋ", "ౄ": "ౠ", "ె": "ఎ", "ే": "ఏ", "ై": "ఐ", "ొ": "ఒ", "ో": "ఓ", "ౌ": "ఔ" };
let signRuns = 0;
for (const [id, kb] of Object.entries(kbOf)) {
  const j = layout(id);
  if (j.phonetic) continue;
  const ka = kaKey(j);
  if (!ka) throw new Error(id + ": no key types క");
  const strokes = PRINTABLE.map(c => [c, code(kb, c, false)]).concat(CS_ORDER.split("").map(c => [ESC + c, code(kb, c, true)]));
  for (const [stroke, c] of strokes) {
    const sign = SIGN[c];
    if (!sign) continue;
    const by = alone(j, stroke);
    if (by !== VOWEL[sign]) { log.push(`${id} ${stroke} (${kb}.TEL ${c.toString(16).toUpperCase()} = ${sign}): types ${JSON.stringify(by)} alone, not ${VOWEL[sign]} - no run added`); continue; }
    // the chart already teaches it on some consonant key
    const taught = (j.sequences || []).some(s => s.keys.endsWith(stroke) && s.keys.length === stroke.length + 1
      && /^[\u0C15-\u0C39]$/.test(alone(j, s.keys[0]) || "") && s.to === alone(j, s.keys[0]) + sign);
    if (taught) continue;
    if (addRun(id, ka + stroke, "\u0C15" + sign, `${kb}.TEL ${c.toString(16).toUpperCase()} is the ${sign} key: ${VOWEL[sign]} alone, ${sign} after a letter (the chart shows it on no letter)`)) signRuns++;
  }
}

// 8. The ra-vattu key (Shree code D7) on the keyboards whose charts teach క్ర another way (link + ర): Shree's composers
//    put it on the NEXT consonant whatever the keyboard (D7 + క = క్ర; క + D7 + క = కక్ర), as the typewriter keyboards'
//    charts show it struck first. TextSoft put it on the letter before, so D7 then ఠ typed ్రఠ, and ఠ D7 ఠ typed ఠ్రఠ.
//    Each gets the run on క that teaches it is struck first.
let raRuns = 0;
for (const [id, kb] of Object.entries(kbOf)) {
  const j = layout(id);
  if (j.phonetic) continue;
  const ka = kaKey(j);
  const strokes = PRINTABLE.map(c => [c, code(kb, c, false)]).concat(CS_ORDER.split("").map(c => [ESC + c, code(kb, c, true)]));
  for (const [stroke, c] of strokes) {
    if (c !== 0xD7 || alone(j, stroke) !== "\u0C4D\u0C30") continue;
    const taught = (j.sequences || []).some(s => s.keys.startsWith(stroke) && s.keys.length > stroke.length && s.to.endsWith("\u0C4D\u0C30")
      && /^[\u0C15-\u0C39]/.test(s.to));
    if (taught) continue;
    if (addRun(id, stroke + ka, "\u0C15\u0C4D\u0C30", `${kb}.TEL ${c.toString(16).toUpperCase()} is the ra-vattu: Shree puts it on the next consonant`)) raRuns++;
  }
}

// 9. InScript has no halant key: its chart types the visible halant as the link twice (kdd = క్‌), on క only. Every
//    consonant key gets that run.
let halantRuns = 0;
{
  const id = "telugu-inscript", j = layout(id);
  for (const c of PRINTABLE) {
    const t = alone(j, c);
    if (!/^[\u0C15-\u0C39]$/.test(t || "") || t === "\u0C15") continue;
    if (addRun(id, c + "dd", t + "\u0C4D\u200C", "the link twice is the visible halant (the chart's kdd = క్‌)")) halantRuns++;
  }
}

// 10. Chart runs that are not keystrokes, or that Shree's own keyboard file contradicts (round2\measure runaudit: each run
//     against what its keys type without it). A run is only removed where its keys alone, as Shree's file has them, type
//     something else; the keys then type what Shree types.
function dropRun(id, keys, why) {
  const j = layout(id); const at = (j.sequences || []).findIndex(s => s.keys === keys);
  if (at < 0) { log.push(`${id} run ${keys}: already gone`); return; }
  const to = j.sequences[at].to; j.sequences.splice(at, 1); touched.add(id);
  log.push(`${id} run ${keys} = ${to} removed: ${why}`);
}
function expectCode(kb, ch, want) { const c = code(kb, ch, false); if (c !== want) throw new Error(`${kb}.TEL ${ch} is ${c.toString(16)}, not ${want.toString(16)}`); }
// "A or B" chart cells kept as one run of keys: each key alone already types the text
for (const [id, keys] of [["tel-apple", "~OrE"], ["tel-applen", "~OrE"], ["tel-roma", "U/{"], ["tel-roma", "e/Y"], ["tel-roma", "u/["]])
  dropRun(id, keys, "the chart's 'A or B' cell, not a run of keys (each key alone types it)");
expectCode("ITR", "J", 0xD7);   // ITR's J is Shree's conjunct slot D7 6C 64 67 = స్త్ర
setKey("tel-itr", "J", "స్త్రి", "స్త్ర", "ITR.TEL J is the slot D7 6C 64 67 = స్త్ర (the chart repeated its స్త్రి); its one-key run J already typed స్త్ర");
dropRun("tel-itr", "J", "the key itself types స్త్ర now");
expectCode("ITR", "Z", 0x6C);
dropRun("tel-itr", "ZtZ", "ITR.TEL Z is 6C = స and t the link 64: Z t Z is స్స, not న్న");
expectCode("PRAKASH", "/", 0x2F);
dropRun("tel-prakash", "/D/", "PRAKASH.TEL / is '/' itself (2F): / D / is not య్య");
if (code("ROMA", "j", true) !== 0xD1) throw new Error("ROMA Ctrl+Space j is not D1");
dropRun("tel-roma", "Øjfm", "ROMA.TEL Ctrl+Space j is D1 = ఙ: Ctrl+Space j f m is ఙ్మ (క్ష్మ is Ctrl+Space x f m, which the keys make)");
expectCode("TYPEWRIT", "r", 0x6A);
for (const k of ["Kra", "KraZ", "Kras"]) dropRun("tel-typewrit", k, "TYPEWRIT.TEL r is 6A = న: K r a is న్త్ర (the chart's స్త్ర runs name the wrong key)");
expectCode("VISION", "m", 0x6C); expectCode("VISION", "l", 0x67);
dropRun("tel-vision", "mdl", "VISION.TEL m d l is స + link + త = స్త: the chart cell lost its d j, and every స్త typed స్త్ర (పుస్తకం came out పుస్త్రకం)");
expectCode("MICRO", "a", 0x61); expectCode("MICRO", "q", 0x51);
dropRun("tel-micro", "a" + BS, "MICRO.TEL a is 61 (ఆ alone): a then the visarga is ఆః; అః is q then the visarga (q is 51 = అ)");
// Applen and Modular are charts only (no .TEL): their runs XhX / XdX = X్X (Applen jhk = క్ర) show h / d as the link,
// which typed nothing alone - only the chart's own runs made a conjunct there.
setKey("tel-applen", "h", undefined, "్", "the link (the chart's runs X h X = X్X, j h k = క్ర); it typed nothing alone");
setKey("tel-modular", "d", undefined, "్", "the link (the chart's runs X d X = X్X, l d g d k = స్త్ర); it typed nothing alone");

for (const id of touched) {
  const j = files[id], raw = raws[id];
  if (j.sequences) j.sequences.sort((a, b) => a.keys.length - b.keys.length || (a.keys < b.keys ? -1 : a.keys > b.keys ? 1 : 0));
  fs.writeFileSync(path.join(dir, id + ".json"), JSON.stringify(j, null, 2) + (raw.endsWith(NL) ? NL : ""));
}
console.log(log.join(NL));
console.log(`${log.length} log lines; runs added: ${signRuns} vowel-sign, ${raRuns} ra-vattu, ${halantRuns} halant; ${touched.size} files written`);
