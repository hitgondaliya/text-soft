// Wire the SHREE-PUN-* (4/5/6) fonts to shree-pun456 and list its tables. Text edits only (the file keeps its format).
// Usage: node patch-languages.js <languages.json>
const fs = require("fs");
const p = process.argv[2];
let s = fs.readFileSync(p, "utf8");
let n = 0;
for (const id of ["0950", "0951", "0952", "0955", "0956"]) {
  const re = new RegExp('("id": "shree-pun-' + id + '",\\s*"name": "SHREE-PUN-' + id + ' \\(Shree-Lipi 4\\.0/5\\.0/6\\.0\\)",\\s*"family": "SHREE-PUN-' + id + '",\\s*"type": "legacy",\\s*"encoding": )"shree-pun7"');
  if (!re.test(s)) throw new Error("font shree-pun-" + id + " not found as expected");
  s = s.replace(re, '$1"shree-pun456"');
  n++;
}
const anchor = '    "mappings/unicode-to-shree-pun7.json",';
if (!s.includes(anchor)) throw new Error("mapping list anchor not found");
if (!s.includes('"mappings/unicode-to-shree-pun456.json"'))
  s = s.replace(anchor, anchor + '\n    "mappings/shree-pun456-to-unicode.json",\n    "mappings/unicode-to-shree-pun456.json",');
fs.writeFileSync(p, s);
console.log("fonts rewired: " + n);
