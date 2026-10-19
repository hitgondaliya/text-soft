// Explore the Malayalam code set: every single code 0x01..0xFE (not the slot references 90-9F / B0-CF), each code after
// ക (66), each code before ക, each code after പ (65), ക + link + code, code + link + ക, ര + link + code, and the
// pre-base sign codes (73 െ, 53 േ, 59 ്ര, DE ൈ) before / after letters and clusters.
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const ALL = [];
for (let c = 0x01; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF) && c !== 0x0A && c !== 0x0D) ALL.push(c);
const KA = 0x66, PA = 0x65, RA = 0x67, LINK = 0x64;
for (const c of ALL) add([c], 'code ' + h(c));
for (const c of ALL) add([KA, c], 'ക + ' + h(c));
for (const c of ALL) add([c, KA], h(c) + ' + ക');
for (const c of ALL) add([PA, c], 'പ + ' + h(c));
for (const c of ALL) add([KA, LINK, c], 'ക link + ' + h(c));
for (const c of ALL) add([c, LINK, KA], h(c) + ' + link ക');
for (const c of ALL) add([RA, LINK, c], 'ര link + ' + h(c));
// pre-base signs: before a letter, before a cluster, after a letter (logical order), with ാ/ൗ after (split o/au)
const PRE = [0x73, 0x53, 0x59, 0xDE];
const SOME = [0x66, 0x65, 0x67, 0x6D, 0x72, 0x61, 0x75, 0x68, 0x69, 0x63, 0xD7, 0x77];
for (const s of PRE) for (const c of SOME) {
  add([s, c], 'pre ' + h(s) + ' ' + h(c)); add([c, s], 'logical ' + h(c) + ' pre ' + h(s));
  add([s, c, 0x6C], 'pre ' + h(s) + ' ' + h(c) + ' ാ'); add([s, c, 0x4F], 'pre ' + h(s) + ' ' + h(c) + ' ൗ');
  add([s, c, LINK, KA], 'pre ' + h(s) + ' ' + h(c) + ' link ക'); add([c, LINK, KA, s], h(c) + ' link ക logical pre ' + h(s));
  add([s, c, 0x41], 'pre ' + h(s) + ' ' + h(c) + ' ം'); add([s, s, c], 'pre ' + h(s) + ' twice ' + h(c));
}
for (const c of SOME) for (const x of [0x49, 0x55, 0x59, 0x44, 0xE2, 0x47, 0x48, 0x4E, 0x52, 0x58, 0xEC, 0x6B, 0x6F, 0xD0]) { add([c, x], h(c) + ' + ' + h(x)); add([c, x, 0x6C], h(c) + ' + ' + h(x) + ' ാ'); add([0x73, c, x], 'െ ' + h(c) + ' + ' + h(x)); }
fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S));
console.log(S.length, 'files', Math.ceil(S.length / 47));
