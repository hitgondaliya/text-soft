// Generate the Tamil probe sequences (Shree internal codes, typed order, <= 4 codes each) -> seq-all.json
// Typing model under a neutral keyboard name (learned in explore1-show.txt): consonant, then post-base signs;
// the pre-base signs ெ 76 / ே 62 / ை 75 / ை(old) D3 are struck BEFORE the consonant (visual order), ா 74 after it makes
// ொ/ோ, ள 26 after ெ+C makes ௌ; the pulli 66 after a consonant; clusters are C1 + 66 + C2 (Tamil writes the pulli).
const fs = require('fs');
const S = [];
const seen = new Set();
const add = (codes, tag) => { if (codes.length > 4 || !codes.length) throw new Error('len ' + codes); const k = codes.join(','); if (!seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const CONS = [0x67, 0x6E, 0x72, 0x4E, 0x69, 0x64, 0x79, 0x6C, 0x6B, 0x65, 0x63, 0x61, 0x6D, 0x6F, 0x78, 0x26, 0x73, 0x70, 0x24, 0xD0, 0x51, 0xD2, 0xE3, 0xD7];
const IND = [0x46, 0x54, 0x48, 0x4A, 0x3E, 0xD6, 0x56, 0x42, 0x55, 0x7A, 0x5A, 0xA6, 0xD1];
const POST = [0x74, 0x68, 0x6A, 0x5D, 0x5E, 0x5F, 0x40, 0x03, 0x66];           // ா ி ீ ு ூ(5E) ு/ூ(5F) ூ(40) 03 ்
const POSTX = [0x81, 0x82, 0x84, 0x85, 0x80, 0x83, 0x86, 0xE0, 0xA1, 0xA2, 0xA3, 0x26, 0x77, 0x20, 0xD1, 0xA4, 0xA5, 0xEF, 0xDA, 0xDB, 0xDE, 0x06, 0x01, 0x02];
const PRE = [0x76, 0x62, 0x75, 0xD3];
const TAIL = [0x77, 0x20, 0xD1, 0xA4, 0xA5];
const PRECOMP = [0x41, 0x43, 0x44, 0x45, 0x47, 0x49, 0x4B, 0x4C, 0x4D, 0x4F, 0x50, 0x52, 0x53, 0x57, 0x58, 0x59, 0x7E, 0x7F, 0xD5, 0x3C, 0x71, 0xD4];
// A. every single code
for (let c = 0x01; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + h(c));
// B. consonant alone, with every post sign / other code, with sign + tail, with the pulli + tail
for (const c of CONS) {
  add([c], 'C ' + h(c));
  for (const p of [...POST, ...POSTX]) add([c, p], 'C ' + h(c) + ' + ' + h(p));
  for (const p of POST) for (const t of TAIL) add([c, p, t], 'C ' + h(c) + ' + ' + h(p) + ' tail ' + h(t));
  for (const t of TAIL) add([c, t, 0x74], 'C ' + h(c) + ' tail ' + h(t) + ' then ா');
  add([c, 0x74, 0x74], 'C ' + h(c) + ' ா ா'); add([c, 0x66, 0x66], 'C ' + h(c) + ' ் ்'); add([c, 0x68, 0x74], 'C ' + h(c) + ' ி ா'); add([c, 0x5D, 0x5D], 'C ' + h(c) + ' ு ு');
}
// C. pre-base signs: s + C, s + C + post, s + C + post + tail; logical order C + s (+ post)
for (const s of PRE) for (const c of CONS) {
  add([s, c], 'pre ' + h(s) + ' C ' + h(c));
  for (const p of [0x74, 0x26, 0x66, 0x68, 0x6A, 0x5D, 0x5E, 0x40, 0x5F, 0x03, ...TAIL]) add([s, c, p], 'pre ' + h(s) + ' C ' + h(c) + ' + ' + h(p));
  for (const t of TAIL) { add([s, c, 0x74, t], 'pre ' + h(s) + ' C ' + h(c) + ' ா tail ' + h(t)); add([s, c, 0x26, t], 'pre ' + h(s) + ' C ' + h(c) + ' ள tail ' + h(t)); }
  add([c, s], 'logical C ' + h(c) + ' + pre ' + h(s)); add([c, s, 0x74], 'logical C ' + h(c) + ' + pre ' + h(s) + ' ா'); add([c, s, 0x26], 'logical C ' + h(c) + ' + pre ' + h(s) + ' ள');
  add([s, s, c], 'pre twice ' + h(s) + ' C ' + h(c));
}
// D. two-consonant clusters C1 + pulli + C2: alone, + every post sign, + tail; pre-base sign before C2; joiners
for (const a of CONS) for (const b of CONS) {
  add([a, 0x66, b], 'cl ' + h(a) + ' ் ' + h(b));
  for (const p of [...POST, 0x77, 0xD1]) add([a, 0x66, b, p], 'cl ' + h(a) + ' ் ' + h(b) + ' + ' + h(p));
  for (const s of PRE) add([a, 0x66, s, b], 'cl ' + h(a) + ' ் pre ' + h(s) + ' ' + h(b));
  add([a, 0x66, 0xA4, b], 'cl ' + h(a) + ' ் ZWNJ ' + h(b)); add([a, 0x66, 0xA5, b], 'cl ' + h(a) + ' ் ZWJ ' + h(b));
  add([a, 0x66, b, 0x66], 'cl ' + h(a) + ' ் ' + h(b) + ' ்');
  add([a, b], 'adjacent ' + h(a) + ' ' + h(b));
}
// E. precomposed codes (C+u/uu, old னா றா ணா): alone, + tail/pulli/sign, after pre-base signs, after C + pulli
for (const p of PRECOMP) {
  add([p], 'precomp ' + h(p));
  for (const t of [...TAIL, 0x66, 0x74, 0x68, 0x5D, 0x40, 0x26]) add([p, t], 'precomp ' + h(p) + ' + ' + h(t));
  for (const s of PRE) { add([s, p], 'pre ' + h(s) + ' precomp ' + h(p)); add([s, p, 0x74], 'pre ' + h(s) + ' precomp ' + h(p) + ' ா'); add([s, p, 0x26], 'pre ' + h(s) + ' precomp ' + h(p) + ' ள'); }
  for (const c of CONS) add([c, 0x66, p], 'cl ' + h(c) + ' ் precomp ' + h(p));
}
// F. independent vowels and ஃ: alone, + tail, + pulli, + sign (not text), before a consonant; ஒ + ள = ஔ
for (const v of IND) {
  add([v], 'V ' + h(v));
  for (const t of [...TAIL, 0x66, 0x26, 0x74, 0x5E]) add([v, t], 'V ' + h(v) + ' + ' + h(t));
  for (const c of CONS) add([v, c], 'V ' + h(v) + ' C ' + h(c));
}
add([0x7A, 0x26, 0x77], 'ஒள tail'); add([0x7A, 0x26, 0xD1], 'ஒள ஃ');
// G. ஸ்ரீ / ஶ்ரீ / க்ஷ / ஷ்ர spelled from pieces and with signs
for (const c of [0x51, 0xE3, 0xD0, 0x67, 0x79, 0x6B]) { add([c, 0x66, 0x61, 0x6A], 'shri ' + h(c) + ' ் ர ீ'); add([c, 0x66, 0x61, 0x68], h(c) + ' ் ர ி'); add([c, 0x66, 0x61, 0x5D], h(c) + ' ் ர ு'); add([c, 0x66, 0x61, 0x74], h(c) + ' ் ர ா'); }
for (const x of [0xD8, 0xD7]) { add([x], 'lig ' + h(x)); for (const p of [...POST, ...TAIL, 0x26]) add([x, p], 'lig ' + h(x) + ' + ' + h(p)); for (const s of PRE) { add([s, x], 'pre ' + h(s) + ' lig ' + h(x)); add([s, x, 0x74], 'pre ' + h(s) + ' lig ' + h(x) + ' ா'); add([s, x, 0x26], 'pre ' + h(s) + ' lig ' + h(x) + ' ள'); } for (const c of CONS) { add([c, 0x66, x], 'cl ' + h(c) + ' ் lig ' + h(x)); add([x, 0x66, c], 'lig ' + h(x) + ' ் ' + h(c)); } }
for (const c of CONS) add([0x67, 0x66, 0xD0, c], 'க்ஷ pieces + ' + h(c));
// H. ஃ + consonant (ஃப = f, ஃஜ = z), symbols and digits after a letter
for (const c of CONS) add([0xD1, c], 'ஃ ' + h(c));
for (let d = 0x30; d <= 0x39; d++) { add([0x67, d], 'C + digit ' + h(d)); add([d, 0x67], 'digit ' + h(d) + ' + C'); }
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log(S.length);
