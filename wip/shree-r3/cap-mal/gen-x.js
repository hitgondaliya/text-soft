// Probe set X (round 1 additions): samvruthokaram and other sign + virama shapes the P set did not ask:
// C + every post sign + link 64 / halant 44 / link + space, C + ു + link + C2, C്C2 + ു + link (5 codes: not possible),
// and the sign-first ra on ശ clusters. Sequences already probed are skipped.
const fs = require('fs');
const { C } = require('./expected');
const h = c => c.toString(16).toUpperCase().padStart(2, '0');
const old = new Set(['seq-all.json', 'seq-explore1.json', 'seq-w.json'].flatMap(f => JSON.parse(fs.readFileSync(__dirname + '/' + f, 'utf8'))).map(s => s.codes.join(',')));
const S = [], seen = new Set(); let skipped = 0;
const add = (codes, tag) => { const k = codes.join(','); if (codes.length > 4 || seen.has(k)) return; seen.add(k); if (old.has(k)) { skipped++; return; } S.push({ codes, tag }); };
const LET = Object.keys(C).map(Number);
const POST = { 'ാ': 0x6C, 'ി': 0x6A, 'ീ': 0x4A, 'ു': 0x6B, 'ൂ': 0x6F, 'ൃ': 0xD0, 'ൗ': 0x4F };
for (const c of LET) {
  for (const [s, sc] of Object.entries(POST)) { add([c, sc, 0x64], `X ${C[c]}${s} link`); add([c, sc, 0x44], `X ${C[c]}${s} halant`); add([c, sc, 0x64, 0x20], `X ${C[c]}${s} link space`); }
  add([c, 0x6B, 0x64, 0x66], 'X ' + C[c] + 'ു link ക'); add([c, 0x6B, 0x64, 0x72], 'X ' + C[c] + 'ു link ന'); add([0x73, c, 0x6B, 0x64], 'X െ' + C[c] + 'ു link');
  add([c, 0x41, 0x64], 'X ' + C[c] + 'ം link'); add([c, 0x55, 0x6B, 0x64], 'X ' + C[c] + '്യു link'); add([0x59, c, 0x6B, 0x64], 'X ra ' + C[c] + 'ു link');
}
fs.writeFileSync(__dirname + '/seq-x.json', JSON.stringify(S));
console.log('X sequences', S.length, 'skipped', skipped, 'files', Math.ceil(S.length / 47));
