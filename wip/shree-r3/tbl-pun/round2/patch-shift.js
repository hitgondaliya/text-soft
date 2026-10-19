// Round 2 layout fix: the keys Shree's own keyboard file leaves empty (byte FF in C:\Shree-Lipi nxt\<KB>.PUN, so
// neither composer types anything) where the TextSoft layout has no entry for that Shift state, so the engine typed
// the unshifted key's character (Shift fallback) or let the raw key through. Each becomes "" (types nothing, swallowed).
// Every slot is checked against the .PUN byte before it is written; a slot that is already set is an error.
// Usage: node patch-shift.js <layoutsDir> <ShreeDir>
const fs = require("fs"), path = require("path");
const dir = process.argv[2], shree = process.argv[3] || "C:/Shree-Lipi nxt";

// keyboard file -> TextSoft layout, and the key characters (as the .PUN file indexes them) found by pcheck keys
// (round2/out/keys.txt, "Shree types nothing (no key), TextSoft types L0", both classes) whose .PUN byte is FF. Left alone:
// the slots that hold a code neither composer answers (4D CRTRONIC Q, RAMING ]; 45 GODREJ J; 4D GODREJ G; E9 EA E1 on ENG/PHONETIC
// @ $ _): unidentified codes, not FF.
const fixes = {
  "CRTRONIC.PUN": ["pun-crtronic", "\"OVX_|"],
  "DOE.PUN": ["pun-doe", "#$&<@BJNVX^_"],
  "GODREJ.PUN": ["pun-godrej", "(DILOPTUY_|"],
  "INSCRIPT.PUN": ["punjabi-inscript", "<@_"],
  "ITR.PUN": ["pun-itr", "H^"],
  "RAMING.PUN": ["pun-raming", "09F"],
  "TYPEWRIT.PUN": ["pun-typewrit", "#"],
  // the keyboards Shree types by sound (its composer switches mode by their names); an FF slot types nothing there too
  "ENG.PUN": ["pun-eng", "^{}"],
  "NEWROMA.PUN": ["pun-newroma", "$+<QRZ^{}"],
  "PHONETIC.PUN": ["pun-phonetic", "BPXZ^_{}"],
  "ROMA.PUN": ["pun-roma", "$@H]^_"],
};

// The character a US key types -> [vk, shifted]
function vkOf(c) {
  if (c >= "a" && c <= "z") return [c.toUpperCase().charCodeAt(0), false];
  if (c >= "A" && c <= "Z") return [c.charCodeAt(0), true];
  if (c >= "0" && c <= "9") return [c.charCodeAt(0), false];
  const sh = ")!@#$%^&*(".indexOf(c);
  if (sh >= 0) return [48 + sh, true];
  const m = { ";": [186, 0], ":": [186, 1], "=": [187, 0], "+": [187, 1], ",": [188, 0], "<": [188, 1], "-": [189, 0], "_": [189, 1],
    ".": [190, 0], ">": [190, 1], "/": [191, 0], "?": [191, 1], "`": [192, 0], "~": [192, 1], "[": [219, 0], "{": [219, 1],
    [String.fromCharCode(92)]: [220, 0], "|": [220, 1], "]": [221, 0], "}": [221, 1], "'": [222, 0], "\"": [222, 1] };
  if (m[c]) return [m[c][0], m[c][1] === 1];
  throw new Error("no key for " + c);
}

const log = [];
let n = 0;
for (const [kbFile, [id, chars]] of Object.entries(fixes)) {
  const bytes = fs.readFileSync(path.join(shree, kbFile));
  const p = path.join(dir, id + ".json");
  const raw = fs.readFileSync(p, "utf8");
  const j = JSON.parse(raw);
  for (const c of chars) {
    if (bytes[c.charCodeAt(0)] !== 0xFF) throw new Error(`${kbFile} '${c}' is ${bytes[c.charCodeAt(0)].toString(16)}, not FF`);
    const [vk, shifted] = vkOf(c);
    let k = j.keys.find(k => k.vk === vk);
    if (!k) throw new Error(`${id}: no key vk ${vk} for '${c}'`);
    const slot = shifted ? "shift" : "base";
    if (k[slot] !== undefined && k[slot] !== null) throw new Error(`${id} '${c}' (vk ${vk} ${slot}) is already ${JSON.stringify(k[slot])}`);
    const fallback = shifted ? (k.base ?? "(raw key)") : "(raw key)";
    k[slot] = "";
    log.push(`${id}: '${c}' vk ${vk} ${slot}: unset (typed ${JSON.stringify(fallback)}) -> "" (${kbFile} byte ${c.charCodeAt(0).toString(16).toUpperCase()} = FF)`);
    n++;
  }
  const nl = String.fromCharCode(10);
  fs.writeFileSync(p, JSON.stringify(j, null, 2) + (raw.endsWith(nl) ? nl : ""));
}
console.log(log.join("\n"));
console.log(`${n} keys set to "" (types nothing)`);
