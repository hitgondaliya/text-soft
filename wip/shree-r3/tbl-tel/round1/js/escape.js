// node escape.js <file.cs>... : write invisible / confusable characters as C# \uXXXX escapes
// (ZWNJ, ZWJ, the Telugu length mark U+0C56, the noncharacters U+FDD0-FDEF, U+FFFD, NBSP, soft hyphen, C1 controls).
const fs = require('fs');
const BS = String.fromCharCode(92);
for (const f of process.argv.slice(2)) {
  const s = fs.readFileSync(f, 'utf8');
  let n = 0;
  const out = [...s].map(c => {
    const x = c.codePointAt(0);
    if (x === 0x200C || x === 0x200D || x === 0x0C56 || (x >= 0xFDD0 && x <= 0xFDEF) || x === 0xFFFD || x === 0xA0 || x === 0xAD || (x >= 0x80 && x <= 0x9F)) {
      n++;
      return BS + 'u' + x.toString(16).toUpperCase().padStart(4, '0');
    }
    return c;
  }).join('');
  if (n) fs.writeFileSync(f, out, 'utf8');
  console.log(f.split(/[\\/]/).pop(), n);
}
