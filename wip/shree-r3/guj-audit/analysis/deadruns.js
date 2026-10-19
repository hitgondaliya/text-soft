// Runs a typist can never start: the run's first key types nothing on its own, and the engine
// (TypingEngine.OnKey -> ResolveSequence -> "output empty and replace 0" -> ResetSequence) drops
// a run whose first key put nothing on screen. Also: runs containing a literal "or" (chart "A or B").
const fs = require('fs'), path = require('path');
const D = path.join(__dirname, '..', 'repo', 'assets', 'config', 'layouts');
const phys = [];
for (let c = 97; c <= 122; c++) phys.push([c - 32, String.fromCharCode(c), String.fromCharCode(c - 32)]);
const sh = ')!@#$%^&*(';
for (let d = 0; d < 10; d++) phys.push([48 + d, String(d), sh[d]]);
[[186, ';', ':'], [187, '=', '+'], [188, ',', '<'], [189, '-', '_'], [190, '.', '>'], [191, '/', '?'], [192, '`', '~'], [219, '[', '{'], [220, '\\', '|'], [221, ']', '}'], [222, "'", '"']].forEach(x => phys.push(x));
const res = {};
let total = 0, totalOr = 0;
for (const f of fs.readdirSync(D).filter(f => /^guj|^gujarati/.test(f))) {
  const j = JSON.parse(fs.readFileSync(path.join(D, f), 'utf8').replace(/^﻿/, ''));
  const out = {};
  for (const [vk, n, s] of phys) {
    const k = j.keys.find(k => k.vk === vk); if (!k) continue;
    if (k.base) out[n] = k.base;
    const ss = k.shift == null ? k.base : k.shift; if (ss) out[s] = ss;
  }
  const dead = j.sequences.filter(s => { const c = s.keys[0]; return c !== 'Ø' && c !== '¤' && !out[c]; });
  const orRuns = j.sequences.filter(s => /or/.test(s.keys) && s.keys.length > 3);
  total += dead.length; totalOr += orRuns.length;
  res[f] = { runs: j.sequences.length, deadStart: dead.map(s => s.keys + '=' + s.to), orRuns: orRuns.map(s => s.keys + '=' + s.to) };
  if (dead.length) console.log(f.padEnd(24), 'runs', j.sequences.length, 'first key types nothing alone:', dead.length, '|', dead.slice(0, 16).map(s => s.keys + '=' + s.to).join(' '));
}
console.log('total dead-start runs', total, '; runs with a literal "or"', totalOr);
fs.writeFileSync(path.join(__dirname, '..', 'results', 'l1-dead-runs.json'), JSON.stringify(res, null, 1));
