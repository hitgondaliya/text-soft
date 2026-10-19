// node patch-eng.js <translit/odia-shree-eng.json> : the Odia Eng (type-by-sound) scheme against Shree's own ENG.ORI
// (per-key capture r3\cap-ori\ori-vendor-keys.json). In Eng mode the engine sends the letter keys A-Z a-z to the
// scheme; a letter the scheme does not know is typed as itself - a Latin letter in Odia text, left out in a Shree font.
//  1. e after a letter types its sign େ (Shree's e is code 6C, the e-sign); the scheme had none, so କେ could not be typed.
//  2. The special "ke" -> କ (a chart row) is removed: it made "ke" type କ and "kee" କଏ.
//  3. Aa after a letter types ା (was nothing).
//  4. j is a consonant, so it joins: "kj" types କ୍ଜ (as a special it typed କଜ).
//  5. Letters Shree's ENG.ORI types that the scheme left Latin: v ବ, J ଯ, y ୟ, X ଢ଼ (consonants); E ଏ, I ଇ, O ଓ, U ଉ
//     (vowels, with their signs after a letter); B ୰, Y ୍ୟ, Q ୍‍ (the half key), F ଼, R ୃ (specials); C K P, which have no
//     code in ENG.ORI, type nothing.
//  6. V is ୱ, as ENG.ORI has it (codes 54 64 71): the scheme had ବ, which b already types (one character on two keys).
// Keeps the file's own format (CRLF, two-space indent, no final newline) and the order of the existing entries.
const fs = require('fs');
const file = process.argv[2];
const text = fs.readFileSync(file, 'utf8');
const t = JSON.parse(text);
const ZWJ = String.fromCharCode(0x200D);
const log = [];
function setIn(obj, name, key, value) {
  const before = obj[key];
  if (JSON.stringify(before) === JSON.stringify(value)) return;
  obj[key] = value;
  log.push(`${name} ${JSON.stringify(key)}: ${JSON.stringify(before)} -> ${JSON.stringify(value)}`);
}
function del(obj, name, key) { if (key in obj) { log.push(`${name} ${JSON.stringify(key)}: ${JSON.stringify(obj[key])} -> removed`); delete obj[key]; } }
setIn(t.vowels, 'vowels', 'e', ['ଏ', 'େ']);
del(t.specials, 'specials', 'ke');
setIn(t.vowels, 'vowels', 'Aa', ['ଆ', 'ା']);
if (t.specials.j !== undefined) { del(t.specials, 'specials', 'j'); }
setIn(t.consonants, 'consonants', 'j', 'ଜ');
setIn(t.consonants, 'consonants', 'v', 'ବ');
setIn(t.consonants, 'consonants', 'J', 'ଯ');
setIn(t.consonants, 'consonants', 'y', 'ୟ');
setIn(t.consonants, 'consonants', 'X', 'ଢ଼');
setIn(t.consonants, 'consonants', 'V', 'ୱ');
setIn(t.vowels, 'vowels', 'E', ['ଏ', 'େ']);
setIn(t.vowels, 'vowels', 'I', ['ଇ', 'ି']);
setIn(t.vowels, 'vowels', 'O', ['ଓ', 'ୋ']);
setIn(t.vowels, 'vowels', 'U', ['ଉ', 'ୁ']);
setIn(t.specials, 'specials', 'B', '୰');
setIn(t.specials, 'specials', 'Y', '୍ୟ');
setIn(t.specials, 'specials', 'Q', '୍' + ZWJ);
setIn(t.specials, 'specials', 'F', '଼');
setIn(t.specials, 'specials', 'R', 'ୃ');
for (const k of ['C', 'K', 'P']) setIn(t.specials, 'specials', k, '');
// 7. The specials ya -> ୟ and Xa -> ଢ଼ are dropped: the consonants y and X type the same and join (dya: ଦ୍ୟ, not ଦୟ).
del(t.specials, 'specials', 'ya');
del(t.specials, 'specials', 'Xa');
const out = JSON.stringify(t, null, 2).replace(/\n/g, '\r\n');
if (out !== text) fs.writeFileSync(file, out);
for (const l of log) console.log(l);
console.log(`${log.length} changes`);
