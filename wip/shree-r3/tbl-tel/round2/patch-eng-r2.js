// Round 2: the Telugu Eng scheme (assets/config/translit/telugu-shree-eng.json, after round1\patch-eng.js). Its specials
// "aM" = ం, "aH" = ః and "a$" = ఁ are matched before the vowel "a" (longest token first), so a word that starts with
// అం / అః / అఁ lost its అ: aMdaru typed ందరు, aMTE ంటే. Without them "a" is అ (or the inherent a after a consonant) and
// M H $ are ం ః ఁ: aM = అం, kaM = కం, aH = అః, kaH = కః, as Shree's ENG chart has them. Text edits, idempotent.
// Usage: node patch-eng-r2.js <telugu-shree-eng.json>
const fs = require("fs");
const p = process.argv[2];
let raw = fs.readFileSync(p, "utf8");
const j = JSON.parse(raw);
for (const [tok, to] of [["M", "ం"], ["H", "ః"], ["$", "ఁ"]])
  if (j.specials[tok] !== to) throw new Error(`special ${tok} is ${JSON.stringify(j.specials[tok])}, not ${to}: removing "a${tok}" would lose it`);
let n = 0;
for (const tok of ["aM", "aH", "a$"]) {
  if (!(tok in j.specials)) { console.log(`special ${tok}: already gone`); continue; }
  // remove the line   "aM": "ం",   as text (the file keeps its format and line endings)
  const esc = tok.split("$").join("[$]");
  const re = new RegExp(`\r?\n[ \t]*"${esc}"[ \t]*:[ \t]*"[^"]*",?`);
  const before = raw;
  raw = raw.replace(re, "");
  if (raw === before) throw new Error("could not find the line of special " + tok);
  console.log(`special ${tok} = ${j.specials[tok]} removed`);
  n++;
}
JSON.parse(raw);
fs.writeFileSync(p, raw);
console.log(`${n} specials removed`);
