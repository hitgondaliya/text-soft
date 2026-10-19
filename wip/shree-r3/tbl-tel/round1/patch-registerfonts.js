// tools/RegisterFonts/Program.cs (main's version, which has the familyToEnc map): the Shree-Tel7-001/002 faces
// (WTEL7001/7002.TTF, a star at 92 where SHREE-TEL7/SHREE-TEL draw the rupee sign) get the shree-tel7w tables, and
// shree-tel7w is a Telugu encoding. Text edits, idempotent. Usage: node patch-registerfonts.js <Program.cs>
const fs = require("fs");
const p = process.argv[2];
let s = fs.readFileSync(p, "utf8");
const nl = s.includes("\r\n") ? "\r\n" : "\n";
function edit(from, to) {
  if (s.includes(to)) return;
  const at = s.indexOf(from);
  if (at < 0 || s.indexOf(from, at + 1) >= 0) throw new Error("anchor not found exactly once: " + from.trim());
  s = s.replace(from, to);
}
edit('        ["Shree-Ass7-001"] = "shree-ass7w", ["Shree-Ass7-002"] = "shree-ass7w",' + nl,
     '        ["Shree-Ass7-001"] = "shree-ass7w", ["Shree-Ass7-002"] = "shree-ass7w",' + nl +
     '        // Shree-Tel7-001/002 (WTEL7001/7002.TTF) draw a star at 92, where SHREE-TEL7/SHREE-TEL draw the rupee sign.' + nl +
     '        ["Shree-Tel7-001"] = "shree-tel7w", ["Shree-Tel7-002"] = "shree-tel7w",' + nl);
edit('        ["shree-tel7"] = new[] { "Telugu" }, ["shree-telx"] = new[] { "Telugu" },' + nl,
     '        ["shree-tel7"] = new[] { "Telugu" }, ["shree-tel7w"] = new[] { "Telugu" }, ["shree-telx"] = new[] { "Telugu" },' + nl);
fs.writeFileSync(p, s);
console.log("edited " + p);
