// Generate the Malayalam probe sequences (Shree internal codes, typed order, <= 4 codes each) -> seq-all.json
// Typing model under a neutral keyboard name (learned in explore1-show.txt): Shree's Malayalam is typed in VISUAL order:
//  - the pre-base signs െ 73 / േ 53 and the pre-base ra sign ്ര 59 are struck BEFORE the consonant (cluster); ൈ = 73 73;
//    the split vowels are the pre-base sign + C + ാ 6C (ൊ ോ) or + ൗ 4F (ൌ); the precomposed codes DC ോ DD ൊ DE ൈ DF ൌ
//    make the Unicode composer write the sign but the 7 composer writes NOTHING for them
//  - post-base signs after the consonant: ാ 6C ി 6A ീ 4A ു 6B ൂ 6F ൃ D0 ൄ E9 ൢ EA ൣ EB ൗ 4F; the ya/va signs ്യ 55 / ്വ 49
//  - the link 64 between consonants: the 7 composer writes a ligature (ക്ക A8, ക്ത A9), C1 + a subjoined form of C2
//    (ക്ട 57 9B, ക്ല 57 E6) or C1 + the visible chandrakkala 56 + C2; 64 at the end = C + 56. E2 = a virama the 7 composer
//    writes no glyph for; 44 = virama + ZWNJ (56)
//  - chillus are atomic codes: ർ 47 ൽ 48 ൺ 4E ൻ 52 ൾ 58 ൿ EC; tails ം 41 ഃ D6; ZWJ F1, ZWNJ DB / F2
const fs = require('fs');
const CONS = { 'ക': 0x66, 'ഖ': 0x46, 'ഗ': 0x7A, 'ഘ': 0x5A, 'ങ': 0x62, 'ച': 0x76, 'ഛ': 0x56, 'ജ': 0x77, 'ഝ': 0x5E, 'ഞ': 0x42, 'ട': 0x6D, 'ഠ': 0x4D, 'ഡ': 0xD3, 'ഢ': 0xD4, 'ണ': 0x6E, 'ത': 0xD7, 'ഥ': 0xD8, 'ദ': 0x70, 'ധ': 0x50, 'ന': 0x72, 'പ': 0x65, 'ഫ': 0x45, 'ബ': 0x3E, 'ഭ': 0x40, 'മ': 0x61, 'യ': 0x75, 'ര': 0x67, 'റ': 0x79, 'ല': 0x68, 'ള': 0x78, 'ഴ': 0x71, 'വ': 0x69, 'ശ': 0x43, 'ഷ': 0x24, 'സ': 0x63, 'ഹ': 0x3C };
const EXTRA = { 'ക്ഷ': 0xD5, 'ശ്രീ': 0xE3 };
const POST = { 'ാ': 0x6C, 'ി': 0x6A, 'ീ': 0x4A, 'ു': 0x6B, 'ൂ': 0x6F, 'ൃ': 0xD0, 'ൄ': 0xE9, 'ൢ': 0xEA, 'ൣ': 0xEB, 'ൗ': 0x4F };
const MAINPOST = { 'ാ': 0x6C, 'ി': 0x6A, 'ീ': 0x4A, 'ു': 0x6B, 'ൂ': 0x6F, 'ൃ': 0xD0 };
const PRECOMP = { 'ോ(DC)': 0xDC, 'ൊ(DD)': 0xDD, 'ൈ(DE)': 0xDE, 'ൌ(DF)': 0xDF };
const TAILS = { 'ം': 0x41, 'ഃ': 0xD6 };
const CHILLU = { 'ർ': 0x47, 'ൽ': 0x48, 'ൺ': 0x4E, 'ൻ': 0x52, 'ൾ': 0x58, 'ൿ': 0xEC };
const IND = { 'അ': [0x4C], 'ആ': [0xD2], 'ഇ': [0x54], 'ഈ': [0x54, 0x4F], 'ഉ': [0x4B], 'ഊ': [0x4B, 0x4F], 'ഋ': [0x57], 'ൠ': [0xE6], 'ഌ': [0xE4], 'ൡ': [0xE5], 'എ': [0x74], 'ഏ': [0xD1], 'ഐ': [0x73, 0x74], 'ഐ(F0)': [0xF0], 'ഒ': [0x51], 'ഓ': [0x51, 0x6C], 'ഔ': [0x51, 0x4F] };
const E = 0x73, EE = 0x53, RA = 0x59, YA = 0x55, VA = 0x49, AA = 0x6C, AU = 0x4F, LINK = 0x64, LINK2 = 0xE2, HAL = 0x44, ZWJ = 0xF1, ZWNJ = 0xDB, ZWNJ2 = 0xF2, SP = 0x20;
const S = [];
const seen = new Set();
const add = (codes, tag) => { const k = codes.join(','); if (codes.length && codes.length <= 4 && !seen.has(k)) { seen.add(k); S.push({ codes, tag }); } };
const bases = { ...CONS, ...EXTRA };
// pre-base sign shapes: [codes before C, codes after C, name]
const PRESHAPES = [[[E], [], 'െ'], [[EE], [], 'േ'], [[E, E], [], 'ൈ'], [[E], [AA], 'ൊ'], [[EE], [AA], 'ോ'], [[E], [AU], 'ൌ'], [[EE], [AU], 'േ+ൗ']];
// 1. every base letter
for (const [b, c] of Object.entries(bases)) {
  add([c], b);
  for (const [s, sc] of Object.entries(POST)) { add([c, sc], `${b} ${s}`); for (const [t, tc] of Object.entries(TAILS)) add([c, sc, tc], `${b} ${s} ${t}`); add([c, sc, 0x41, 0xD6], `${b} ${s} ം ഃ`); }
  for (const [t, tc] of Object.entries(TAILS)) add([c, tc], `${b} ${t}`);
  add([c, 0x41, 0xD6], `${b} ം ഃ`); add([c, 0xD6, 0x41], `${b} ഃ ം`); add([c, 0x41, 0x41], `${b} ം ം`);
  // viramas and joiners
  add([c, LINK], `${b} link`); add([c, LINK2], `${b} E2`); add([c, HAL], `${b} halant`); add([c, LINK, SP], `${b} link space`); add([c, LINK2, SP], `${b} E2 space`);
  add([c, LINK, ZWJ], `${b} link zwj`); add([c, LINK, ZWNJ], `${b} link zwnj`); add([c, LINK, ZWNJ2], `${b} link zwnj2`); add([c, LINK2, ZWJ], `${b} E2 zwj`); add([c, LINK2, ZWNJ], `${b} E2 zwnj`);
  add([c, LINK, ZWJ, 0x66], `${b} link zwj ക`); add([c, LINK, ZWNJ, 0x66], `${b} link zwnj ക`); add([c, LINK, LINK], `${b} link link`); add([c, LINK, 0x41], `${b} link ം`);
  add([c, HAL, 0x66], `${b} halant ക`); add([c, LINK2, 0x66], `${b} E2 ക`); add([c, LINK2, 0x67], `${b} E2 ര`);
  // ya / va signs
  for (const [y, yc] of [['്യ', YA], ['്വ', VA]]) {
    add([c, yc], `${b} ${y}`);
    for (const [s, sc] of Object.entries(POST)) add([c, yc, sc], `${b} ${y} ${s}`);
    for (const [t, tc] of Object.entries(TAILS)) add([c, yc, tc], `${b} ${y} ${t}`);
    for (const [s, sc] of Object.entries(MAINPOST)) add([c, yc, sc, 0x41], `${b} ${y} ${s} ം`);
    for (const [pre, post, nm] of PRESHAPES) add([...pre, c, yc, ...post], `${b} ${y} ${nm}`);
    add([c, yc, LINK], `${b} ${y} link`);
  }
  add([c, YA, VA], `${b} ്യ ്വ`); add([c, VA, YA], `${b} ്വ ്യ`);
  // ra sign (struck before the letter)
  add([RA, c], `ra-sign ${b}`);
  for (const [s, sc] of Object.entries(POST)) add([RA, c, sc], `ra-sign ${b} ${s}`);
  for (const [t, tc] of Object.entries(TAILS)) add([RA, c, tc], `ra-sign ${b} ${t}`);
  for (const [s, sc] of Object.entries(MAINPOST)) add([RA, c, sc, 0x41], `ra-sign ${b} ${s} ം`);
  add([RA, c, YA], `ra-sign ${b} ്യ`); add([RA, c, VA], `ra-sign ${b} ്വ`); add([RA, c, YA, AA], `ra-sign ${b} ്യ ാ`);
  for (const [pre, post, nm] of PRESHAPES) { add([...pre, RA, c, ...post], `ra-sign ${b} ${nm} (sign struck first)`); add([RA, ...pre, c, ...post], `ra-sign ${b} ${nm} (ra struck first)`); }
  add([c, RA], `${b} ra-sign after (logical order)`);
  // pre-base signs and split vowels
  for (const [pre, post, nm] of PRESHAPES) {
    add([...pre, c, ...post], `${b} ${nm}`);
    for (const [t, tc] of Object.entries(TAILS)) add([...pre, c, ...post, tc], `${b} ${nm} ${t}`);
    add([...pre, c, ...post, LINK], `${b} ${nm} link`);
  }
  add([c, E], `${b} െ after (logical order)`); add([c, EE], `${b} േ after (logical order)`); add([c, E, AA], `${b} െ ാ after (logical order)`);
  for (const [s, sc] of Object.entries(PRECOMP)) { add([c, sc], `${b} ${s}`); add([c, sc, 0x41], `${b} ${s} ം`); }
}
// 2. two-consonant clusters through the link 64 (every pair): alone, every post sign, tails, pre-base signs, the ra sign,
//    ya/va signs, a pending link, the E2 virama and the halant 44 between
const CL = Object.entries(CONS);
for (const [b1, c1] of CL) for (const [b2, c2] of CL) {
  const C = [c1, LINK, c2];
  add(C, `${b1} link ${b2}`);
  for (const [s, sc] of Object.entries(POST)) add([...C, sc], `${b1} link ${b2} ${s}`);
  for (const [t, tc] of Object.entries(TAILS)) add([...C, tc], `${b1} link ${b2} ${t}`);
  add([E, ...C], `${b1} link ${b2} െ`); add([EE, ...C], `${b1} link ${b2} േ`); add([RA, ...C], `ra-sign ${b1} link ${b2}`);
  add([...C, YA], `${b1} link ${b2} ്യ`); add([...C, VA], `${b1} link ${b2} ്വ`); add([...C, LINK], `${b1} link ${b2} link`);
  add([c1, LINK2, c2], `${b1} E2 ${b2}`); add([c1, HAL, c2], `${b1} halant ${b2}`);
}
// 2b. ക്ഷ D5 after a letter through the link, and chillu + letter (ൻറ etc.)
for (const [b1, c1] of CL) { add([c1, LINK, 0xD5], `${b1} link ക്ഷ`); add([0xD5, LINK, c1], `ക്ഷ link ${b1}`); }
for (const [ch, cc] of Object.entries(CHILLU)) {
  add([cc], `chillu ${ch}`); add([cc, 0x41], `chillu ${ch} ം`);
  for (const [b2, c2] of CL) { add([cc, c2], `chillu ${ch} + ${b2}`); add([cc, c2, AA], `chillu ${ch} + ${b2} ാ`); }
  for (const [b1, c1] of CL) add([c1, cc], `${b1} + chillu ${ch}`);
}
// 3. three-consonant pieces: a leading link (nothing before it) + C2 + link + C3, and C1 link C2 link (pending)
const FREQ3 = ['ക', 'ത', 'ന', 'സ', 'ട', 'പ', 'മ', 'ദ', 'ധ', 'ഷ', 'ണ', 'ച', 'ജ', 'ഞ', 'ങ', 'ല', 'ര', 'യ', 'വ', 'ബ', 'ഗ', 'സ', 'റ', 'ഥ'];
for (const [b2, c2] of CL) { add([LINK, c2], `link ${b2}`); for (const [s, sc] of Object.entries(MAINPOST)) add([LINK, c2, sc], `link ${b2} ${s}`); }
for (const b2 of FREQ3) for (const b3 of FREQ3) add([LINK, CONS[b2], LINK, CONS[b3]], `link ${b2} link ${b3}`);
// 4. independent vowels alone, with tails, the link, signs after them
for (const [v, c] of Object.entries(IND)) {
  add(c, v);
  for (const [t, tc] of Object.entries(TAILS)) add([...c, tc], `${v} ${t}`);
  add([...c, 0x41, 0xD6], `${v} ം ഃ`); add([...c, LINK], `${v} link`); add([...c, AU], `${v} ൗ`); add([...c, AA], `${v} ാ`);
}
// 5. every single code (digits, punctuation, symbols); signs alone and on carriers
for (let c = 0x20; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) add([c], 'code ' + c.toString(16).toUpperCase());
for (const [s, sc] of Object.entries({ ...POST, 'െ': E, 'േ': EE, '്ര': RA, '്യ': YA, '്വ': VA, ...TAILS })) { add([sc], `sign ${s}`); add([SP, sc], `space ${s}`); add([0x2E, sc], `. ${s}`); }
add([0x2E, 0x2E, 0x2E], '...'); add([0x22, 0x27], '‘’'); add([0x30, 0x31, 0x32, 0x33], '൦൧൨൩');
// 6. the vendor keyboards' own conjunct-slot sequences, probed under the neutral name, alone and after ക / പ
const SH = 'C:/Shree-Lipi nxt/';
for (const f of fs.readdirSync(SH).filter(f => /\.MAL$/i.test(f)).sort()) {
  const b = fs.readFileSync(SH + f);
  for (let n = 0; n < 48; n++) {
    const o = 166 + n * 5; const codes = []; for (let j = 1; j <= 4; j++) if (b[o + j] !== 0xFF) codes.push(b[o + j]); codes.reverse();
    if (!codes.length || codes.some(x => x < 0x20)) continue;
    add(codes, `vendor-slot ${f}#${n}`);
    if (codes.length <= 3) { add([0x66, ...codes], `ക + vendor-slot ${f}#${n}`); add([0x65, ...codes], `പ + vendor-slot ${f}#${n}`); }
  }
}
// 7. syllable joins: every letter after കി / കാ / ക / കു / കം / ക്ക / ക് / കൻ, and െ / ്ര struck before the second letter
for (const [b2, c2] of CL) {
  add([0x66, 0x6A, c2], `കി ${b2}`); add([0x66, AA, c2], `കാ ${b2}`); add([0x66, c2], `ക ${b2}`); add([0x66, 0x6B, c2], `കു ${b2}`);
  add([0x66, 0x41, c2], `കം ${b2}`); add([0x66, LINK, 0x66, c2], `ക്ക ${b2}`); add([0x66, HAL, c2], `ക്‌ ${b2}`); add([0x66, 0x52, c2], `കൻ ${b2}`);
  add([0x66, E, c2], `ക + െ${b2}`); add([0x66, RA, c2], `ക + ്ര${b2}`); add([E, 0x66, c2], `കെ ${b2}`); add([c2, E, 0x66], `${b2} + കെ`);
  add([c2, AA, 0x66], `${b2}ാ ക`); add([c2, 0x47, 0x66], `${b2}ർ ക`);
}
fs.writeFileSync(__dirname + '/seq-all.json', JSON.stringify(S));
console.log('sequences', S.length, 'files', Math.ceil(S.length / 47));
