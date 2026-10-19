// Explore the Tamil code set: every single code 0x01..0xFE (not the slot references 90-9F / B0-CF, not 00/FF),
// each code after க (67), before க, after ட (69, டி/டீ are ligatures), after ம (65), க + pulli 66 + code, code + pulli + க,
// and the pre-base signs: ெ (76) / ே (62) / ை (75, D3) + க + code (to find what ends ொ ோ ௌ), க + ெ/ே + code (logical order).
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (!seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const ALL = [];
for (let c = 0x01; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) ALL.push(c);
for (const c of ALL) add([c], 'code ' + h(c));
for (const c of ALL) add([0x67, c], 'க + ' + h(c));
for (const c of ALL) add([c, 0x67], h(c) + ' + க');
for (const c of ALL) add([0x69, c], 'ட + ' + h(c));
for (const c of ALL) add([0x65, c], 'ம + ' + h(c));
for (const c of ALL) add([0x67, 0x66, c], 'க் + ' + h(c));
for (const c of ALL) add([c, 0x66, 0x67], h(c) + ' + ் க');
for (const s of [0x76, 0x62, 0x75, 0xD3]) for (const c of ALL) add([s, 0x67, c], h(s) + ' க + ' + h(c));
for (const s of [0x76, 0x62, 0x75, 0xD3]) for (const c of [0x74, 0x26, 0x40, 0x5E, 0x5F, 0x66, 0x77, 0x20]) add([0x67, s, c], 'க ' + h(s) + ' + ' + h(c));
fs.writeFileSync(__dirname + '/seq-explore1.json', JSON.stringify(S));
console.log(S.length);
