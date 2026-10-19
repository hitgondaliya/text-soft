// Join probe captures to their manifest; verify each load took.
// node probeparse.js <pdir> <capdir> <prefix> [--show]
// A file counts only when: both passes inside the probe agreed (#stable 1), table 0 of SL8_DLL_DATA equalled the
// file after the load (#loaded 1), and the untouched keys of the ITR base still type what ITR types.
const { fs, path, readTsv, cleanU, hex2, CODES, uhex } = require('./lib');
const [pdir, capdir, prefix] = process.argv.slice(2);
const show = process.argv.includes('--show');
const manifest = JSON.parse(fs.readFileSync(path.join(pdir, prefix + '-manifest.json'), 'utf8'));
// untouched keys of the ITR.PUN base and what they must type (U answer, 7 answer) - from capvo/ITR.tsv
const CHECK = { V: ['ਫ', 'B3'], X: ['ਠ', '53 BE'], Z: ['ਢ', '55'], ':': ['ਥ', '58'], ';': ['ਤ', '57'], '0': ['੦', '30'], '5': ['੫', '35'] };
const byFile = new Map();
for (const m of manifest) { if (!byFile.has(m.file)) byFile.set(m.file, []); byFile.get(m.file).push(m); }
const results = [];
let badFiles = 0, missing = 0;
for (const [file, items] of byFile) {
  const tsv = path.join(capdir, file.replace(/\.PUN$/, '.tsv'));
  if (!fs.existsSync(tsv)) { missing++; continue; }
  const { load: stable, loaded, rows } = readTsv(tsv);
  const l0 = new Map(rows.filter(r => r.layer === 0).map(r => [r.key, r]));
  let ok = stable === 1 && loaded === '1';
  if (!ok) console.error(file, 'stable', stable, 'loaded', loaded);
  for (const [k, [wantU, want7]] of Object.entries(CHECK)) {
    const r = l0.get(k);
    const got7 = r && r.lb ? r.lb.map(hex2).join(' ') : null;
    if (!r || cleanU(r.u) !== wantU || got7 !== want7) { ok = false; console.error(file, 'check key', k, 'got', r && r.u, got7, 'want', wantU, want7); }
  }
  if (!ok) { badFiles++; continue; }
  for (const m of items) {
    const r = l0.get(m.key);
    const naive = m.codes.map(c => CODES[c] ?? '?').join('');
    let u = r ? r.u : null;
    let lb = r && r.lb ? r.lb.slice() : null;
    if (lb) while (lb.length && lb[lb.length - 1] === 0) lb.pop();
    const res = { file, key: m.key, codes: m.codes.map(hex2).join(' '), tag: m.tag, naive, uRaw: u, uUnits: r ? r.uUnits : 0, u: cleanU(u), bytes: lb && lb.length ? lb.map(hex2).join(' ') : null, lUnits: r ? r.lUnits : 0 };
    results.push(res);
    if (show) console.log(res.codes.padEnd(12), (m.tag || '').padEnd(24), 'naive', JSON.stringify(naive).padEnd(12), 'U', JSON.stringify(u).padEnd(14), uhex(u) || '-', '| 7', res.bytes, res.lUnits);
  }
}
fs.writeFileSync(path.join(capdir, prefix + '-results.json'), JSON.stringify(results));
console.error(`files ${byFile.size} bad ${badFiles} missing ${missing} results ${results.length}`);
