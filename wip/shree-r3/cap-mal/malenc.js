// Malayalam Unicode -> syllables -> Shree's internal codes in typing (= visual) order, as the probes taught:
//   [െ 73 | േ 53 | ൈ 73 73] [ra sign 59] C1 (64 C2)* [്യ 55 | ്വ 49]* [post sign] [ം 41 | ഃ D6]* [final virama 64]
//   ൊ = 73 .. 6C, ോ = 53 .. 6C, ൌ = 73 .. 4F; ക്ഷ = D5; chillus are atomic codes; independent vowels as the keyboards type them.
// syllables(word) -> [{ u, codes }] or null when a character has no code.
const { C } = require('./expected');
const L2C = Object.fromEntries(Object.entries(C).filter(([k]) => Number(k) !== 0xD5).map(([k, v]) => [v, Number(k)]));
const POST = { 'ാ': [0x6C], 'ി': [0x6A], 'ീ': [0x4A], 'ു': [0x6B], 'ൂ': [0x6F], 'ൃ': [0xD0], 'ൄ': [0xE9], 'ൢ': [0xEA], 'ൣ': [0xEB], 'ൗ': [0x4F] };
const PRE = { 'െ': [[0x73], []], 'േ': [[0x53], []], 'ൈ': [[0x73, 0x73], []], 'ൊ': [[0x73], [0x6C]], 'ോ': [[0x53], [0x6C]], 'ൌ': [[0x73], [0x4F]] };
const TAIL = { 'ം': 0x41, 'ഃ': 0xD6 };
const CHILLU = { 'ർ': 0x47, 'ൽ': 0x48, 'ൺ': 0x4E, 'ൻ': 0x52, 'ൾ': 0x58, 'ൿ': 0xEC };
const OLDCHILLU = { 'ണ': 'ൺ', 'ന': 'ൻ', 'ര': 'ർ', 'ല': 'ൽ', 'ള': 'ൾ', 'ക': 'ൿ' };
const IND = { 'അ': [0x4C], 'ആ': [0xD2], 'ഇ': [0x54], 'ഈ': [0x54, 0x4F], 'ഉ': [0x4B], 'ഊ': [0x4B, 0x4F], 'ഋ': [0x57], 'എ': [0x74], 'ഏ': [0xD1], 'ഐ': [0x73, 0x74], 'ഒ': [0x51], 'ഓ': [0x51, 0x6C], 'ഔ': [0x51, 0x4F] };
const OTHER = { ' ': 0x20, '!': 0x21, '‘': 0x22, '%': 0x25, '’': 0x27, '(': 0x28, ')': 0x29, '*': 0x2A, '+': 0x2B, ',': 0x2C, '-': 0x2D, '.': 0x2E, '/': 0x2F, ':': 0x3A, ';': 0x3B, '=': 0x3D, '?': 0x3F, '—': 0x7B, '–': 0x7D, '×': 0xE0, '÷': 0xE1, '₹': 0xE7 };
for (let d = 0; d < 10; d++) OTHER[String.fromCharCode(0x0D66 + d)] = 0x30 + d;
const V = '്', ZWJ = '‍', ZWNJ = '‌';
const isCons = ch => L2C[ch] !== undefined;

function clusterCodes(letters) {
  // letters: consonants in logical order. Peel ്യ/്വ (not doubled) and then ്ര from the end.
  const ls = letters.slice();
  const yv = [];
  while (ls.length >= 2 && ((ls[ls.length - 1] === 'യ' && ls[ls.length - 2] !== 'യ') || (ls[ls.length - 1] === 'വ' && ls[ls.length - 2] !== 'വ'))) yv.unshift(ls.pop() === 'യ' ? 0x55 : 0x49);
  let ra = false;
  if (ls.length >= 2 && ls[ls.length - 1] === 'ര') { ls.pop(); ra = true; }
  const body = [];
  for (let i = 0; i < ls.length; i++) {
    if (ls[i] === 'ക' && ls[i + 1] === 'ഷ') { if (body.length) body.push(0x64); body.push(0xD5); i++; continue; }
    if (body.length) body.push(0x64);
    body.push(L2C[ls[i]]);
  }
  return { ra, body, yv };
}
function syllables(word) {
  const w = word.normalize('NFC');
  const out = [];
  let i = 0;
  while (i < w.length) {
    const ch = w[i];
    if (IND[ch]) { const codes = IND[ch].slice(); let u = ch; i++; while (TAIL[w[i]] !== undefined) { codes.push(TAIL[w[i]]); u += w[i]; i++; } out.push({ u, codes }); continue; }
    if (CHILLU[ch] !== undefined) { out.push({ u: ch, codes: [CHILLU[ch]] }); i++; continue; }
    if (OTHER[ch] !== undefined) { out.push({ u: ch, codes: [OTHER[ch]] }); i++; continue; }
    if (!isCons(ch)) return null;
    // cluster
    const letters = [ch]; let u = ch; i++;
    while (w[i] === V && isCons(w[i + 1])) { letters.push(w[i + 1]); u += w[i] + w[i + 1]; i += 2; }
    // old chillu C്‍
    if (w[i] === V && w[i + 1] === ZWJ && letters.length === 1 && OLDCHILLU[ch]) { out.push({ u: u + V + ZWJ, codes: [CHILLU[OLDCHILLU[ch]]] }); i += 2; continue; }
    const { ra, body, yv } = clusterCodes(letters);
    let pre = [], post = [], tails = [], vir = [];
    if (PRE[w[i]]) { [pre, post] = PRE[w[i]]; u += w[i]; i++; }
    else if (POST[w[i]]) { post = POST[w[i]]; u += w[i]; i++; }
    while (TAIL[w[i]] !== undefined) { tails.push(TAIL[w[i]]); u += w[i]; i++; }
    if (!pre.length && !post.length && w[i] === V) { u += V; i++; if (w[i] === ZWNJ) { u += ZWNJ; i++; vir = [0x44]; } else vir = [0x64]; }
    out.push({ u, codes: [...pre, ...(ra ? [0x59] : []), ...body, ...yv, ...post, ...tails, ...vir] });
  }
  return out;
}
module.exports = { syllables, clusterCodes, L2C, POST, PRE, TAIL, CHILLU, IND };
if (require.main === module) for (const a of process.argv.slice(2)) console.log(a, JSON.stringify(syllables(a)?.map(s => s.u + '=' + s.codes.map(c => c.toString(16).toUpperCase().padStart(2, '0')).join(' '))));
