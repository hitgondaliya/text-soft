// Round 2: the Punjabi Eng (type-by-sound) scheme, assets/config/translit/punjabi-shree-eng.json.
// Usage: node patch-eng.js <punjabi-shree-eng.json>   (keeps the file's CRLF line endings; throws if a slot is taken)
//  - Shree's own ENG.PUN keys (cap-pun\pun-vendor-keys.json) that typed nothing in TextSoft (or raw Latin into the font):
//    consonants R = U+0A5C (codes 59 -> 6D), z = U+0A1D (43 -> 50), H = U+0A39 (D2 -> AA, the subjoined ha after a letter),
//    V and W = U+0A35 (D0 -> AB, the subjoined va after a letter); specials C = U+0A71 addak (40 -> A7), Q = U+0A70 tippi (D6 -> A5).
//  - vowels "aa" = [U+0A06, U+0A3E] (as gujarati-shree-eng.json has "aa"): no key could type the aa-sign after a letter.
//  - the special "a" = U+0A3E is removed: it shadowed (or, by List.Sort's unstable order, was shadowed by) the vowel "a".
const fs = require("fs");
const p = process.argv[2];
const raw = fs.readFileSync(p, "utf8");
const j = JSON.parse(raw);
const ch = (h) => String.fromCharCode(parseInt(h, 16));
function add(o, k, v) { if (o[k] !== undefined) throw new Error("taken: " + k); o[k] = v; }
add(j.consonants, "R", ch("0A5C"));
add(j.consonants, "z", ch("0A1D"));
add(j.consonants, "H", ch("0A39"));
add(j.consonants, "V", ch("0A35"));
add(j.consonants, "W", ch("0A35"));
add(j.specials, "C", ch("0A71"));
add(j.specials, "Q", ch("0A70"));
if (j.vowels.aa) throw new Error("taken: aa");
const v = {};
for (const [k, val] of Object.entries(j.vowels)) { v[k] = val; if (k === "a") v.aa = [ch("0A06"), ch("0A3E")]; }
j.vowels = v;
if (j.specials.a !== ch("0A3E")) throw new Error("special a is not the aa-sign");
delete j.specials.a;
const crlf = raw.includes("\r\n");
let out = JSON.stringify(j, null, 2);
if (crlf) out = out.split("\n").join("\r\n");
out += raw.endsWith("\r\n") ? "\r\n" : raw.endsWith("\n") ? "\n" : "";
fs.writeFileSync(p, out);
console.log("punjabi-shree-eng: 7 keys, aa added, special a removed");
