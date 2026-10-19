const fs=require("fs"); const p=process.argv[2]; let s=fs.readFileSync(p,"utf8"); const before=s;
function rep(a,b){ if(!s.includes(a)) throw new Error("missing: "+a); s=s.replace(a,b); }
rep('["PUNX"] = "shree-punx",  ["PUN7"] = "shree-pun7",   ["PUN"] = "shree-pun7",',
    '// Punjabi 7.0 (SHREE-PUN7-*) and 4.0/5.0/6.0 (SHREE-PUN-*): the same design but for ₹ (3C), which only the 7 faces have.\n        ["PUNX"] = "shree-punx",  ["PUN7"] = "shree-pun7",   ["PUN"] = "shree-pun456",');
rep('["shree-punx"] = new[] { "Punjabi" }, ["shree-pun7"] = new[] { "Punjabi" },',
    '["shree-punx"] = new[] { "Punjabi" }, ["shree-pun7"] = new[] { "Punjabi" }, ["shree-pun456"] = new[] { "Punjabi" },');
rep('        fonts.Add(new JsonObject\n        {\n            ["id"] = id,\n            ["name"] = family + " (" + (guj456 ? "Shree-Lipi 4.0/5.0/6.0" : label) + ")",',
    '        // Likewise Punjabi: SHREE-PUN-* (no 7) has no ₹ at 3C, so it has its own tables (shree-pun456).\n        bool pun456 = enc == "shree-pun7" && !System.Text.RegularExpressions.Regex.IsMatch(family, @"^SHREE[-_ ]?PUN7", System.Text.RegularExpressions.RegexOptions.IgnoreCase);\n        fonts.Add(new JsonObject\n        {\n            ["id"] = id,\n            ["name"] = family + " (" + (guj456 || pun456 ? "Shree-Lipi 4.0/5.0/6.0" : label) + ")",');
rep('["encoding"] = guj456 ? "shree-guj456" : enc,', '["encoding"] = guj456 ? "shree-guj456" : pun456 ? "shree-pun456" : enc,');
fs.writeFileSync(p,s);
