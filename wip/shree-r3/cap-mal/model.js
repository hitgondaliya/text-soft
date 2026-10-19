// The composer's own rules, learned from the truth, as a function: Unicode syllable -> Shree-Lipi 7 bytes.
//   bytes = [pre: െ 52 | േ 53 | ൈ 52 52 | ൊ 52 | ോ 53 | ൌ 52] [ra sign 4E] [cluster] [്യ 7C | ്വ 7E]* [post: ാ 4C ... | ൊ ോ 4C | ൌ 54] [ം 55 | ഃ 40]* [virama 56]
//   cluster = the truth of the bare cluster (one letter, or C1്C2 typed with the link); ശ under the ra sign = E8 (no 4E).
// derive(u) uses ONLY truth entries of the pieces, never the string itself, so comparing derive(u) with truth[u] measures the
// rule. Used by patterns.js (rule checks) and gaps.js (spellings for strings the composer cannot be asked in one key).
const { syllables } = require('./malenc');
const PREB = { 'െ': ['52', ''], 'േ': ['53', ''], 'ൈ': ['52 52', ''], 'ൊ': ['52', '4C'], 'ോ': ['53', '4C'], 'ൌ': ['52', '54'] };
const POSTB = { 'ാ': '4C', 'ി': '5D', 'ീ': '7D', 'ു': '4F', 'ൂ': '50', 'ൃ': '51', 'ൗ': '54' };
const TAILB = { 'ം': '55', 'ഃ': '40' };
const V = '്';
function parse(u) {
  // one syllable: letters (C1 ് C2 ...), sign, tails, final virama (with ZWNJ?)
  const m = /^((?:[ക-ഹ]്)*[ക-ഹ])([ാ-ൌൗ]?)([ംഃ]*)(്‌?)?$/.exec(u);
  if (!m) return null;
  const letters = m[1].split(V);
  return { letters, sign: m[2], tails: m[3], vir: m[4] || '' };
}
function make(truth) {
  const pref = u => truth[u] ? truth[u].preferred : null;
  function clusterBytes(letters) {
    const u = letters.join(V);
    if (letters.length <= 2) return pref(u);
    return null; // three letters: not derivable from pairs (no 5-code probe exists)
  }
  // split the letters into cluster + ra + ya/va exactly as Shree types them (malenc.clusterCodes)
  function split(letters) {
    const ls = letters.slice(); const yv = [];
    while (ls.length >= 2 && ((ls[ls.length - 1] === 'യ' && ls[ls.length - 2] !== 'യ') || (ls[ls.length - 1] === 'വ' && ls[ls.length - 2] !== 'വ'))) yv.unshift(ls.pop() === 'യ' ? '7C' : '7E');
    let ra = false;
    if (ls.length >= 2 && ls[ls.length - 1] === 'ര') { ls.pop(); ra = true; }
    return { core: ls, ra, yv };
  }
  function deriveAll(u) {
    const out = [];
    const a = derive(u); if (a != null) out.push(a);
    const p = parse(u);
    if (p && p.letters.length >= 2 && (p.letters[p.letters.length - 1] === 'യ' || p.letters[p.letters.length - 1] === 'വ') && p.letters[p.letters.length - 2] === p.letters[p.letters.length - 1]) { const b = derive(u, true); if (b != null && !out.includes(b)) out.push(b); }
    return out;
  }
  function derive(u, signForDouble) {
    const p = parse(u);
    if (!p) return null;
    const sp = split(p.letters);
    if (signForDouble) { sp.yv.unshift(sp.core.pop() === 'യ' ? '7C' : '7E'); }
    const { core, ra, yv } = sp;
    let cb = clusterBytes(core);
    if (cb == null) return null;
    let raB = ra ? '4E' : '';
    if (ra && core[0] === 'ശ') { cb = core.length === 1 ? 'E8' : (core.length === 2 ? 'E8 56 ' + pref(core[1]) : null); raB = ''; if (cb == null || cb.includes('null')) return null; } // the ra sign 4E + ശ 77 is always the ligature E8, and then no conjunct with the next letter: ശ്ക്ര = E8 56 57, ശ്ച്ര = E8 56 5C
    if (ra && core.length === 2 && core[1] === 'ശ') return null; // the composer's own answer is a quirk (4E C1 E8)
    let pre = '', post = '';
    if (PREB[p.sign]) [pre, post] = PREB[p.sign];
    else if (POSTB[p.sign] !== undefined) post = POSTB[p.sign];
    else if (p.sign) return null; // ൄ ൢ ൣ: the 7 composer writes nothing for them
    const tails = [...p.tails].map(t => TAILB[t]);
    const vir = p.vir ? '56' : '';
    if (p.vir && p.sign && !p.vir.includes('‌')) { /* samvruthokaram without ZWNJ: same glyphs as with it */ }
    return [pre, raB, cb, ...yv, post, ...tails, vir].filter(Boolean).join(' ');
  }
  return { derive, deriveAll, parse, split };
}
module.exports = { make, parse };
