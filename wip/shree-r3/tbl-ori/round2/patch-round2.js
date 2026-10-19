// node patch-round2.js <configDir> [--dry]        (configDir = assets/config of the repo)
//
// Round 2 of the Odia keyboard fixes (r3\tbl-ori, skeptic's round-2 defects). Idempotent: a second run changes nothing.
// Reads Shree's own per-key answers from the capture (r3\cap-ori\ori-vendor-keys.json, env ORI_VENDOR_KEYS).
//
// ori-phonetic.json (PHONETIC.ORI):
//  P1. 'a' types ଅ, as S8OriU64 answers for the key (code 6B), not ା: a word starts with it ("amar" = ଅମର), and after a
//      consonant's virama form it is the inherent a (TypingOrder: ka = କ teaches "virama + ଅ" -> nothing).
//  P2. 'h' types ହ୍, the virama form S8OriU64 answers (code 6F), like every other consonant key; it was the bare ହ, so
//      "he" could not be ହେ.
//  P3. A run "<consonant keys>a" -> C (da = ଦ, sta = ସ୍ତ, ndha = ନ୍ଧ) is removed where its keys type C + virama without it
//      and the vowel join makes "a" the inherent a: the engine (TypingOrder) then types every vowel after the cluster
//      the way the runs on ka show it (de = ଦେ, dai = ଦୈ). Kept as a run, its text would end the cluster and the vowel
//      after it ("dai") would be a letter of its own (ଦଇ). Where its keys do not type C + virama by themselves (d h
//      types ଦ୍ହ), the run on those keys is added first (dh = ଧ୍), so "dhe" is ଧେ. The vowel runs on k (ka ke kaa kai ...)
//      stay: they teach the joins; k's clusters (kha kta ...) go the same way as the others (kh = ଖ୍).
//  P4. Ctrl+Space t types ତ୍, as PHONETIC.ORI's Ctrl+Space layer (code 66, ତ୍ 86 FF FA): it typed nothing.
//  P5. "aa" is a run, ଆ: after a consonant a a is then the aa sign (the join learns ୍ + ଆ = ା from kaa), and a then Shift+A
//      is ଅ after the inherent a (ନଅ, ଛଅ) - two keys that both type ଅ could not be told apart otherwise.
//  P6. The runs through Ctrl+Space that end in "a" (jØma = ଜ୍ଞ, Ømca = ଞ୍ଚ) stay (a typist may hold Ctrl through the run, and
//      only a run keeps those keys off the shortcuts); their virama form is added where the keys do not type it (jØm = ଜ୍ଞ୍),
//      and P + aa / ai / au, which go through the a, become runs of their own (jØmaa = ଜ୍ଞା).
// ori-eng.json (ENG.ORI):
//  E1. Ctrl+Space t types ତ୍ (as P4).
//  E2. '[', Shift+6 '^' and Shift+- '_' type nothing, as ENG.ORI (byte FF): they typed ଵ, ୬ (the 6 key's digit) and '-'.
// translit/odia-shree-eng.json (the Eng keyboard's sounds):
//  T1. ';' is not ଞ: ENG.ORI types ';' (code 3B, S8OriU64 ';'; the 7 composer nothing - 3B draws ନ୍ତ, so the table leaves
//      it out). ଞ is Ctrl+Space m a, as on the chart.
//  T2. '[' is not ଵ (ENG.ORI: nothing; ଵ is Shift+4).
//  T3. '@' is ୌ, read with the word (k@ = କୌ, as ENG.ORI's code E9, the au sign): it was a key typed alone after the word,
//      which wrote the e-sign after the letter in a Shree font (4C FF 7B F2 for 7B 4C FF F2).
//  T4. C K P are not specials that write "": ENG.ORI has nothing for them (FF), so the word goes on (k C e = କେ); as specials
//      they were read with the word and split it (କଏ). A letter no token spells is swallowed by the engine.
const fs = require('fs');
const path = require('path');
const [dir] = process.argv.slice(2);
const dry = process.argv.includes('--dry');
const capture = JSON.parse(fs.readFileSync(process.env.ORI_VENDOR_KEYS || 'C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/7673923b-96b4-4287-87dd-a8e7c6533eca/scratchpad/r3/cap-ori/ori-vendor-keys.json', 'utf8'));
const VIR = '\u0B4D', MARK = '\u00D8', SYM = '\u00A4';
const log = [];
let changes = 0;
const off = c => { const cp = c.codePointAt(0); return cp >= 0x0900 && cp <= 0x0DFF ? (cp - 0x0900) % 0x80 : -1; };
const isCons = c => { const o = off(c); return (o >= 0x15 && o <= 0x3A) || (o >= 0x58 && o <= 0x5F) || c === 'ୱ'; };   // ୱ (U+0B71) is a letter too
const isSign = c => { const o = off(c); return (o >= 1 && o <= 3) || (o >= 0x3C && o <= 0x4C) || (o >= 0x55 && o <= 0x57) || o === 0x62 || o === 0x63; };
const isVowel = c => { const o = off(c); return (o >= 5 && o <= 0x14) || o === 0x60 || o === 0x61; };
const punct = { 186: ';:', 187: '=+', 188: ',<', 189: '-_', 190: '.>', 191: '/?', 192: '`~', 219: '[{', 220: '\\|', 221: ']}', 222: '\'"' };
function charsOf(vk) {
  if (vk >= 65 && vk <= 90) return [String.fromCharCode(vk + 32), String.fromCharCode(vk)];
  if (vk >= 48 && vk <= 57) return [String.fromCharCode(vk), ')!@#$%^&*('[vk - 48]];
  return punct[vk] ? punct[vk].split('') : [null, null];
}
function slotOf(ch) {
  for (let vk = 0; vk < 256; vk++) { const [n, s] = charsOf(vk); if (n === ch) return { vk, shift: false }; if (s === ch) return { vk, shift: true }; }
  return null;
}
const shree = (file, layer, key) => (capture.keyboards[file].layers[layer] || {})[key];

function setKey(L, id, ch, value, why) {
  const s = slotOf(ch);
  let k = L.keys.find(k => k.vk === s.vk);
  if (!k) { k = { vk: s.vk, deadKey: false }; L.keys.push(k); L.keys.sort((a, b) => a.vk - b.vk); }
  const prop = s.shift ? 'shift' : 'base';
  if (k[prop] === value) return;
  log.push(`${id}\t'${ch}'\t${prop} vk ${s.vk}\t${JSON.stringify(k[prop])} -> ${JSON.stringify(value)}\t${why}`);
  k[prop] = value; changes++;
}
function addEscape(L, id, key, to, why) {
  const keys = MARK + key;
  const seq = L.sequences.find(s => s.keys === keys);
  if (seq && seq.to === to) return;
  if (seq) { log.push(`${id}\tCtrl+Space '${key}'\tsequence\t${JSON.stringify(seq.to)} -> ${JSON.stringify(to)}\t${why}`); seq.to = to; }
  else { log.push(`${id}\tCtrl+Space '${key}'\tsequence\t(none) -> ${JSON.stringify(to)}\t${why}`); L.sequences.push({ keys, to }); }
  const esc = L.escape.find(e => e.keys === key);
  if (esc) esc.to = to; else L.escape.push({ keys: key, to });
  changes++;
}

// ---- the engine's runs and TypingOrder's after-virama joins, as src/FontSoftware.Core/Keyboards/TypingOrder.cs learns them ----
function aloneOf(L) {
  const m = new Map();
  for (const k of L.keys) {
    const [n, s] = charsOf(k.vk);
    if (n && k.base) m.set(n, k.base);
    const sh = k.shift !== undefined && k.shift !== null ? k.shift : k.base;
    if (s && sh) m.set(s, sh);
  }
  return m;
}
function runsOf(L) {
  const runs = new Map(), prefixes = new Set();
  for (const s of L.sequences) {
    if (!s.keys || !s.to) continue;
    runs.set(s.keys, s.to);
    for (let i = 1; i < s.keys.length; i++) prefixes.add(s.keys.slice(0, i));
  }
  return { runs, prefixes };
}
function joinsOf(L, alone, runs) {
  const joins = new Map(), clash = new Set();
  let vowelTaught = false, signTaught = false;
  for (const s of L.sequences) {
    if (!s.keys || !s.to || s.keys.length < 2 || s.keys.length > 3 || s.keys.includes(MARK) || s.keys.includes(SYM)) continue;
    const first = alone.get(s.keys[0]);
    if (!first || first.length !== 2 || !isCons(first[0]) || off(first[1]) !== 0x4D || s.to[0] !== first[0]) continue;
    const sign = s.to.slice(1);
    if (![...sign].every(isSign)) continue;
    const tail = s.keys.slice(1);
    let typed = runs.get(tail);
    if (typed === undefined) { typed = ''; for (const k of tail) { const t = alone.get(k); if (t === undefined) { typed = null; break; } typed += t; } }
    if (!typed || ![...typed].every(c => isSign(c) || isVowel(c))) continue;
    const from = first[1] + typed;
    if (joins.has(from) && joins.get(from) !== sign) clash.add(from);
    joins.set(from, sign);
    if ([...typed].some(isVowel) && sign.length > 0) vowelTaught = true;
    if (typed.length === 1 && sign === typed && off(typed) >= 0x3E && off(typed) <= 0x4C) signTaught = true;
  }
  if (!vowelTaught) return { list: [], signTaught: false };
  const list = [...joins].filter(([f]) => !clash.has(f)).sort((a, b) => b[0].length - a[0].length || (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0));
  return { list, signTaught };
}
function compose(raw, J) {
  let t = raw;
  for (const [from, to] of J.list) t = t.split(from).join(to);
  if (J.signTaught) {
    let o = '';
    for (let i = 0; i < t.length; i++) {
      const n = i + 1 < t.length ? off(t[i + 1]) : -1;
      if (off(t[i]) === 0x4D && ((n >= 0x3E && n <= 0x4C) || [0x55, 0x56, 0x57, 0x62, 0x63].includes(n))) continue;
      o += t[i];
    }
    t = o;
  }
  return t;
}
// The engine's runs (TypingEngine.ResolveSequence, keys typed without Ctrl held): Ø (Ctrl+Space) is appended to the run
// and puts nothing on screen; a key that cannot grow the run starts a new one - at its last escape if that is a run, else
// nothing when it follows the escape directly (the layer leaves it empty), else the key alone.
function simulate(L, keys) {
  const alone = aloneOf(L), { runs, prefixes } = runsOf(L), J = joinsOf(L, alone, runs);
  let seqKeys = '', seqOut = '', raw = '';
  for (const ch of keys) {
    if (ch === MARK || ch === SYM) { seqKeys += ch; continue; }
    const single = alone.get(ch) || '';
    const grown = seqKeys + ch;
    if (prefixes.has(grown) || runs.has(grown)) {
      const text = runs.has(grown) ? runs.get(grown) : seqOut + single;
      raw = raw.slice(0, raw.length - seqOut.length) + text; seqKeys = grown; seqOut = text;
      continue;
    }
    const mark = Math.max(grown.lastIndexOf(MARK), grown.lastIndexOf(SYM));
    if (mark > 0) {
      const tail = grown.slice(mark);
      if (runs.has(tail) || prefixes.has(tail)) { seqKeys = tail; seqOut = runs.has(tail) ? runs.get(tail) : single; raw += seqOut; continue; }
    }
    if (grown.length >= 2 && grown[grown.length - 2] === MARK) { seqKeys = ''; seqOut = ''; continue; }
    seqKeys = ch; seqOut = runs.has(ch) ? runs.get(ch) : single; raw += seqOut;
  }
  return compose(raw, J);
}

// ---------------- ori-phonetic.json ----------------
{
  const id = 'ori-phonetic', file = 'PHONETIC.ORI';
  const p = path.join(dir, 'layouts', id + '.json');
  const text = fs.readFileSync(p, 'utf8');
  const L = JSON.parse(text);
  const a = shree(file, '0', 'a'), h = shree(file, '0', 'h'), t1 = shree(file, '1', 't');
  if (a.u !== '\u0B05' || h.u !== '\u0B39' + VIR || t1.u !== '\u0B24' + VIR) throw new Error('capture: unexpected PHONETIC.ORI answers for a / h / Ctrl+Space t');
  setKey(L, id, 'a', a.u, `P1: ${file} a = ${a.u} (code ${a.codes}); after a consonant the inherent a`);
  setKey(L, id, 'h', h.u, `P2: ${file} h = ${h.u} (code ${h.codes}), the virama form like every consonant key`);
  addEscape(L, id, 't', t1.u, `P4: ${file} Ctrl+Space t = ${t1.u} (code ${t1.codes}, ${t1.b7})`);

  // P5: "aa" is ଆ - a run of its own, so that after a consonant a a is the aa sign (the join learns ୍ + ଆ = ା from kaa) and
  //     a then Shift+A is the letter ଅ after the inherent a (ନଅ, ଛଅ): as two keys that both type ଅ they could not be told apart.
  {
    const AA = 'ଆ';
    const had = L.sequences.find(s => s.keys === 'aa');
    if (!had) { L.sequences.push({ keys: 'aa', to: AA }); log.push(`${id}\trun 'aa'\tadded\t(none) -> "${AA}"\tP5`); changes++; }
    else if (had.to !== AA) throw new Error(`${id}: a run 'aa' types ${had.to}`);
  }

  // P3: the "<consonant keys>a" runs the vowel join makes redundant
  const templateKey = (() => {   // the consonant key the vowel runs are shown on (k)
    const alone = aloneOf(L), count = new Map();
    for (const s of L.sequences) {
      const f = alone.get(s.keys[0]);
      if (s.keys.length >= 2 && f && f.length === 2 && off(f[1]) === 0x4D && !s.keys.includes(MARK) && /^[aeiou~]/.test(s.keys.slice(1)))
        count.set(s.keys[0], (count.get(s.keys[0]) || 0) + 1);
    }
    return [...count].sort((x, y) => y[1] - x[1])[0][0];
  })();
  const isCluster = s => s.length > 0 && [...s].every(c => isCons(c) || off(c) === 0x3C || off(c) === 0x4D) && (isCons(s[s.length - 1]) || off(s[s.length - 1]) === 0x3C);
  const candidates = L.sequences.filter(s => s.keys.length >= 2 && s.keys.endsWith('a') && !s.keys.includes(MARK) && !s.keys.includes(SYM)
      && s.keys !== templateKey + 'a' && isCluster(s.to) && /^[A-Za-z]+$/.test(s.keys))
    .sort((x, y) => x.keys.length - y.keys.length || (x.keys < y.keys ? -1 : 1));
  let removed = 0, added = 0, kept = [];
  for (const run of candidates) {
    if (!L.sequences.includes(run)) continue;
    const P = run.keys.slice(0, -1);
    const idx = L.sequences.indexOf(run);
    L.sequences.splice(idx, 1);
    if (simulate(L, run.keys) === run.to) { removed++; log.push(`${id}\trun '${run.keys}'\tremoved\t${JSON.stringify(run.to)}: its keys type it by themselves (the vowel join)\tP3`); changes++; continue; }
    if (P.length >= 2 && !L.sequences.some(s => s.keys === P)) {
      const half = { keys: P, to: run.to + VIR };
      L.sequences.splice(idx, 0, half);
      if (simulate(L, run.keys) === run.to) {
        removed++; added++;
        log.push(`${id}\trun '${P}'\tadded\t(none) -> ${JSON.stringify(half.to)}: '${run.keys}' = ${run.to}, so '${P}' is its virama form (its keys alone type ${simulate({ ...L, sequences: L.sequences.filter(s => s !== half) }, P)})\tP3`);
        log.push(`${id}\trun '${run.keys}'\tremoved\t${JSON.stringify(run.to)}: '${P}' + the inherent a\tP3`);
        changes += 2; continue;
      }
      L.sequences.splice(L.sequences.indexOf(half), 1);
    }
    L.sequences.splice(idx, 0, run);
    kept.push(`${run.keys}=${run.to} (types ${simulate({ ...L, sequences: L.sequences.filter(s => s !== run) }, run.keys)} without it)`);
  }
  // P6: the runs through Ctrl+Space ending in "a" (jØma = ଜ୍ଞ, Ømca = ଞ୍ଚ) stay - a typist may hold Ctrl through the whole
  //     run, and only a run of its own keeps those keys off the application's shortcuts. Their virama form is added where its
  //     keys do not type it (jØm = ଜ୍ଞ୍: "m" shows the normal layer's ମ୍ until the run ends), so a vowel key joins it as on
  //     any consonant (jØmaa would be ଜ୍ଞ + ଅ + ...); and P + aa / ai / au, which go through the a, are runs of their own.
  const viaEscape = L.sequences.filter(s => s.keys.length >= 2 && s.keys.endsWith('a') && s.keys.includes(MARK) && !s.keys.includes(SYM)
      && s.keys[s.keys.length - 2] !== MARK && isCluster(s.to))
    .sort((x, y) => x.keys.length - y.keys.length || (x.keys < y.keys ? -1 : 1));
  let escAdded = 0;
  for (const run of viaEscape) {
    const P = run.keys.slice(0, -1);
    if (!L.sequences.some(s => s.keys === P) && simulate(L, P) !== run.to + VIR) {
      L.sequences.push({ keys: P, to: run.to + VIR }); escAdded++; changes++;
      log.push(`${id}\trun '${P}'\tadded\t(none) -> ${JSON.stringify(run.to + VIR)}: the virama form of '${run.keys}' = ${run.to}\tP6`);
    }
    for (const [suffix, sign] of [['aa', 'ା'], ['ai', 'ୈ'], ['au', 'ୌ']]) {
      const keys = P + suffix;
      if (L.sequences.some(s => s.keys === keys)) continue;
      const before = simulate(L, keys);
      L.sequences.push({ keys, to: run.to + sign }); escAdded++; changes++;
      log.push(`${id}\trun '${keys}'\tadded\t(none) -> ${JSON.stringify(run.to + sign)} (its keys typed ${JSON.stringify(before)})\tP6`);
    }
  }
  log.push(`${id}\tP6 summary: ${escAdded} runs added for ${viaEscape.length} runs through Ctrl+Space`);

  // every run that is still there types what it did; the ones removed type their text from their keys
  for (const s of JSON.parse(text).sequences) {
    if (!s.keys || !s.to || s.keys.includes(SYM)) continue;
    const got = simulate(L, s.keys);
    if (got !== s.to && !(s.keys === 'a' || s.keys.startsWith('a'))) log.push(`${id}\tCHECK '${s.keys}'\tchart ${JSON.stringify(s.to)}\tnow types ${JSON.stringify(got)}`);
  }
  log.push(`${id}\tP3 summary: ${removed} runs removed, ${added} virama-form runs added; kept ${kept.length}: ${kept.join(', ')}`);
  const sample = ['de', 'di', 'do', 'du', 'dee', 'doo', 'dai', 'dau', 'da', 'daa', 'he', 'dhe', 'sthe', 'ntre', 'kSe', 'Odisha', 'amar', 'd~', 'd@', 'dR',
    'aa', 'naA', 'chaA', 'kaa', 'kai', 'khe', 'O' + MARK + 'dishaa', 'j' + MARK + 'maana', 'j' + MARK + 'me', MARK + 'maa', MARK + 'nka', MARK + 'nke'];
  log.push(`${id}\tsample: ${sample.map(k => k + '=' + simulate(L, k)).join('  ')}`);
  const out = JSON.stringify(L, null, 2) + '\n';
  if (out !== text && !dry) fs.writeFileSync(p, out);
}

// ---------------- ori-eng.json ----------------
{
  const id = 'ori-eng', file = 'ENG.ORI';
  const p = path.join(dir, 'layouts', id + '.json');
  const text = fs.readFileSync(p, 'utf8');
  const L = JSON.parse(text);
  const t1 = shree(file, '1', 't');
  if (t1.u !== '\u0B24' + VIR) throw new Error('capture: unexpected ENG.ORI Ctrl+Space t');
  addEscape(L, id, 't', t1.u, `E1: ${file} Ctrl+Space t = ${t1.u} (code ${t1.codes}, ${t1.b7})`);
  for (const ch of ['[', '^', '_']) {
    if (shree(file, '0', ch) !== undefined) throw new Error(`capture: ${file} types something for ${ch}`);
    setKey(L, id, ch, '', `E2: ${file} has nothing for '${ch}' (byte FF)`);
  }
  const out = JSON.stringify(L, null, 2) + '\n';
  if (out !== text && !dry) fs.writeFileSync(p, out);
}

// ---------------- translit/odia-shree-eng.json ----------------
{
  const p = path.join(dir, 'translit', 'odia-shree-eng.json');
  const text = fs.readFileSync(p, 'utf8');
  const crlf = text.includes('\r\n');
  const t = JSON.parse(text.replace(/^\uFEFF/, ''));
  const sp = t.specials;
  const semi = shree('ENG.ORI', '0', ';'), at = shree('ENG.ORI', '0', '@');
  if (semi.u !== ';') throw new Error('capture: ENG.ORI ; is not ;');
  if (at.codes !== 'E9') throw new Error('capture: ENG.ORI @ is not code E9');
  if (';' in sp) { log.push(`odia-shree-eng\tspecial ';'\t${JSON.stringify(sp[';'])} -> removed\tT1: ENG.ORI types ';' (code 3B)`); delete sp[';']; changes++; }
  if ('[' in sp) { log.push(`odia-shree-eng\tspecial '['\t${JSON.stringify(sp['['])} -> removed\tT2: ENG.ORI has nothing for '['`); delete sp['[']; changes++; }
  if (sp['@'] !== '\u0B4C') { log.push(`odia-shree-eng\tspecial '@'\t${JSON.stringify(sp['@'])} -> "\u0B4C"\tT3: ENG.ORI @ is the au sign (code E9), read with the word`); sp['@'] = '\u0B4C'; changes++; }
  // T4. C K P type nothing on ENG.ORI (byte FF: nothing reaches the composer, the word goes on - k C e is \u0B15\u0B47). As specials
  //     that write "", they were read with the word and split it (k C e was \u0B15\u0B0F); a letter no token spells is swallowed by
  //     the engine and the word goes on.
  for (const k of ['C', 'K', 'P']) {
    if (shree('ENG.ORI', '0', k) !== undefined) throw new Error(`capture: ENG.ORI types something for ${k}`);
    if (k in sp && sp[k] === '') { log.push(`odia-shree-eng\tspecial '${k}'\t"" -> removed\tT4: ENG.ORI has nothing for '${k}' (FF); a letter no token spells types nothing and the word goes on`); delete sp[k]; changes++; }
  }
  let out = JSON.stringify(t, null, 2);
  if (crlf) out = out.replace(/\n/g, '\r\n');
  if (text.endsWith('\n') && !out.endsWith('\n')) out += crlf ? '\r\n' : '\n';
  if (out !== text && !dry) fs.writeFileSync(p, out);
}

for (const l of log) console.log(l);
console.log(`changes: ${changes}${dry ? ' (dry run, nothing written)' : ''}`);
