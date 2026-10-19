// Wire Shree-Tel7-001/002 (WTEL7001/7002.TTF: 92 draws a star there, the rupee sign in SHREE-TEL7/SHREE-TEL) to the
// shree-tel7w tables and list them. Text edits only (the file keeps its format and line endings).
// Usage: node patch-languages.js <languages.json>
const fs = require("fs");
const p = process.argv[2];
let s = fs.readFileSync(p, "utf8");
const nl = s.includes("\r\n") ? "\r\n" : "\n";
let n = 0;
for (const id of ["001", "002"]) {
  const lines = [
    '          "id": "shree-tel7-' + id + '",',
    '          "name": "Shree-Tel7-' + id + ' (Shree-Lipi 7.0)",',
    '          "family": "Shree-Tel7-' + id + '",',
    '          "type": "legacy",',
    '          "encoding": "shree-tel7"',
  ];
  const block = lines.join(nl);
  const at = s.indexOf(block);
  if (at < 0 || s.indexOf(block, at + 1) >= 0) throw new Error("font shree-tel7-" + id + " not found exactly once as expected");
  s = s.replace(block, block.slice(0, block.length - 1) + 'w"');
  n++;
}
const anchor = '    "mappings/unicode-to-shree-tel7.json",';
if (!s.includes(anchor)) throw new Error("mapping list anchor not found");
if (!s.includes('"mappings/unicode-to-shree-tel7w.json"'))
  s = s.replace(anchor, anchor + nl + '    "mappings/shree-tel7w-to-unicode.json",' + nl + '    "mappings/unicode-to-shree-tel7w.json",');
fs.writeFileSync(p, s);
console.log("fonts rewired: " + n);
