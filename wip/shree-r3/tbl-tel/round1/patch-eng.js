// The Telugu Eng (type-by-sound) keyboard, assets/config/translit/telugu-shree-eng.json, against Shree's own ENG.TEL
// (cap-tel/tel-vendor-keys.json, layer 0, both composers). Keys Shree's Eng types a Telugu character for that TextSoft let
// through as raw Latin or punctuation (into a Shree font a raw key is a code that draws something else: '#' 23 is a u-sign,
// ']' 5D the visarga, '<' 3C a subjoined ఱ, '`' 60 చీ) get Shree's character; "e", which typed the bare sign ె even
// alone, is the vowel ఎ / ె (Shree's e = code 62). Every addition is checked not to replace an existing token.
// Usage: node patch-eng.js <telugu-shree-eng.json>
const fs = require("fs");
const p = process.argv[2];
const raw = fs.readFileSync(p, "utf8");
const crlf = raw.includes("\r\n");
const j = JSON.parse(raw);
const NL = String.fromCharCode(10);
const log = [];
function add(group, key, value, why) {
  const has = j.consonants[key] !== undefined || j.vowels[key] !== undefined || j.specials[key] !== undefined;
  if (has) {
    const cur = j[group][key];
    if (JSON.stringify(cur) === JSON.stringify(value)) { log.push(`${group} ${JSON.stringify(key)}: already ${JSON.stringify(value)}`); return; }
    throw new Error(`${JSON.stringify(key)} is already a token`);
  }
  j[group][key] = value;
  log.push(`${group} ${JSON.stringify(key)} = ${JSON.stringify(value)}: ${why}`);
}
// "e": the bare sign alone was a chart artefact (specials); Shree's e (code 62) is ఎ alone, ె after a letter.
if (j.specials["e"] === "ె") { delete j.specials["e"]; log.push(`specials "e" (ె, also alone) removed`); }
add("vowels", "e", ["ఎ", "ె"], "Shree's e (62): ఎ alone, ె after a letter");
// Uppercase vowels and the ై key, as Shree's Eng types them
add("vowels", "A", ["అ", ""], "Shree's A (51) = అ");
add("vowels", "I", ["ఇ", "ి"], "Shree's I (53) = ఇ");
add("vowels", "U", ["ఊ", "ూ"], "Shree's U (26) = ఊ / ూ");
add("vowels", "V", ["ఔ", "ౌ"], "Shree's V (40) = ఔ / ౌ");
add("vowels", "Y", ["ఎ", "ె"], "Shree's Y (42) = ఎ");
add("vowels", "`", ["ఐ", "ై"], "Shree's ` (6D) = ై after a letter, ఐ alone");
// Uppercase consonants Shree's Eng types (its phonetic mode answers each as C + the pending link)
for (const [k, c, code] of [["B", "భ", "4A"], ["C", "ఛ", "43"], ["F", "హ", "4C"], ["G", "ఘ", "57"], ["J", "ఝ", "58"],
                            ["K", "ఖ", "45"], ["P", "ఫ", "59"], ["Q", "న", "6A"], ["W", "ప", "79"], ["X", "ఙ", "D1"], ["q", "క", "65"], ["w", "వ", "6F"]])
  add("consonants", k, c, `Shree's ${k} (${code}) = ${c}`);
// Signs and punctuation
add("specials", "#", "ఽ", "Shree's # (E3) = ఽ");
add("specials", ">", "।", "Shree's > (E4) = ।");
add("specials", "]", "శ్రీ", "Shree's ] (D8) = శ్రీ");
add("specials", "[", "ఉ", "Shree's [ (4E) = ఉ");
add("specials", "<", ",", "Shree's < (2C) = ,");
add("specials", "Z", "్ర", "Shree's Z (D7) = the ra-vattu ్ర");
let out = JSON.stringify(j, null, 2) + (raw.endsWith(NL) ? NL : "");
if (crlf) out = out.split(NL).join("\r" + NL);
fs.writeFileSync(p, out);
console.log(log.join(NL));
