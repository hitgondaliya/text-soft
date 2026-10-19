// The Telugu Shree lines of assets/config/encoding-limits.json ("shree-tel7", and the new "shree-tel7w" after it), copied
// as text from the tbl-tel copy, so no other family's lines are touched or re-serialised. Idempotent.
// Usage: node patch-limits.js <source encoding-limits.json (the copy)> <target encoding-limits.json (main)>
const fs = require("fs");
const [src, dst] = process.argv.slice(2);
const S = fs.readFileSync(src, "utf8"), raw = fs.readFileSync(dst, "utf8");
const crlf = raw.includes("\r\n");
let T = raw.replace(/\r\n/g, "\n");
const NL = "\n";
function block(text, key) {
  const head = "  " + JSON.stringify(key) + ": [";
  const at = text.indexOf(head);
  if (at < 0) return null;
  const end = text.indexOf(NL + "  ]", at);
  if (end < 0) throw new Error("no end for " + key);
  return { at, end: end + NL.length + 3, text: text.slice(at, end + NL.length + 3) };
}
const s = S.replace(/\r\n/g, "\n");
for (const key of ["shree-tel7", "shree-tel7w"]) {
  const b = block(s, key);
  if (!b) throw new Error("source has no " + key);
  const t = block(T, key);
  if (t) T = T.slice(0, t.at) + b.text + T.slice(t.end);
  else {
    const prev = block(T, "shree-tel7");
    if (!prev) throw new Error("target has no shree-tel7 to put " + key + " after");
    T = T.slice(0, prev.end) + "," + NL + b.text + T.slice(prev.end);
  }
  console.log("wrote " + key);
}
const j = JSON.parse(T), js = JSON.parse(s);
for (const key of ["shree-tel7", "shree-tel7w"]) if (JSON.stringify(j[key]) !== JSON.stringify(js[key])) throw new Error("check failed for " + key);
fs.writeFileSync(dst, crlf ? T.replace(/\n/g, "\r\n") : T);
