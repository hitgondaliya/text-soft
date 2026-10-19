// Replace nukta-letter literals in C# sources with \u escapes (the editor normalises them to decomposed form):
// letter + U+0A3C for the six letters with a precomposed form -> the precomposed letter's escape;
// any precomposed nukta letter -> its escape.
const fs = require("fs");
const dir = process.argv[2];
const BS = String.fromCharCode(92);
const esc = cp => BS + "u" + cp.toString(16).toUpperCase().padStart(4, "0");
const pre = { 0x0A16: 0x0A59, 0x0A17: 0x0A5A, 0x0A1C: 0x0A5B, 0x0A2B: 0x0A5E, 0x0A32: 0x0A33, 0x0A38: 0x0A36 };
for (const f of process.argv.slice(3)) {
  const p = dir + "/" + f;
  const s = fs.readFileSync(p, "utf8");
  const t = s.replace(/([ਖਗਜਫਲਸ])਼/g, (all, l) => esc(pre[l.codePointAt(0)]))
             .replace(/[ਖ਼-ਫ਼ਲ਼ਸ਼]/g, ch => esc(ch.codePointAt(0)));
  if (t !== s) { fs.writeFileSync(p, t); console.log("fixed " + f); }
}
