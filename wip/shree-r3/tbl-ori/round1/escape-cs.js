// node escape-cs.js <file.cs>... : in C# string and char literals (not verbatim @"" strings, not comments), write the
// characters a reader cannot see or tell apart as \uXXXX escapes: C1 controls, ZWNJ/ZWJ, the line separators, the BOM, the
// noncharacters the tables use as marks, the nukta and the precomposed nukta letters (ଡ଼ ଢ଼ look the same either way), and
// the e/ai/o/au parts of the normalisation rules (any literal that holds U+0B47 next to U+0B56, U+0B3E or U+0B57).
const fs = require('fs');
const B = String.fromCharCode(92);
const hidden = c => (c >= 0x80 && c <= 0x9F) || c === 0x200C || c === 0x200D || c === 0x2028 || c === 0x2029 || c === 0xFEFF ||
  (c >= 0xFDD0 && c <= 0xFDEF) || c === 0x0B3C || c === 0x0B5C || c === 0x0B5D;
const esc = c => B + 'u' + c.toString(16).toUpperCase().padStart(4, '0');
for (const file of process.argv.slice(2)) {
  const s = fs.readFileSync(file, 'utf8');
  let out = '', i = 0, changed = 0;
  while (i < s.length) {
    const ch = s[i], nx = s[i + 1];
    if (ch === '/' && nx === '/') { const j = s.indexOf('\n', i); const end = j < 0 ? s.length : j; out += s.slice(i, end); i = end; continue; }
    if (ch === '/' && nx === '*') { const j = s.indexOf('*/', i + 2); const end = j < 0 ? s.length : j + 2; out += s.slice(i, end); i = end; continue; }
    if (ch === '@' && nx === '"') { let j = i + 2; while (j < s.length) { if (s[j] === '"' && s[j + 1] === '"') { j += 2; continue; } if (s[j] === '"') break; j++; } out += s.slice(i, j + 1); i = j + 1; continue; }
    if (ch === '$' && nx === '@') { let j = i + 3; while (j < s.length) { if (s[j] === '"' && s[j + 1] === '"') { j += 2; continue; } if (s[j] === '"') break; j++; } out += s.slice(i, j + 1); i = j + 1; continue; }
    if (ch === '"' || ch === "'") {
      const q = ch; let j = i + 1; let lit = '';
      while (j < s.length && s[j] !== q) { if (s[j] === B) { lit += s[j] + s[j + 1]; j += 2; continue; } if (s[j] === '\n') break; lit += s[j]; j++; }
      const cps = [...lit];
      const eForm = /େ[ୖାୗ]/.test(lit) && lit.length <= 3;   // the normalisation pair "େ + length mark": escape it whole
      let body = '';
      for (const c of cps) { const cp = c.codePointAt(0); if (hidden(cp) || (eForm && cp >= 0x0B00 && cp <= 0x0B7F)) { body += esc(cp); changed++; } else body += c; }
      out += q + body + (s[j] === q ? q : ''); i = j + (s[j] === q ? 1 : 0); continue;
    }
    out += ch; i++;
  }
  if (out !== s) fs.writeFileSync(file, out);
  console.log(file, changed, 'characters escaped');
}
