// node runlist.js <outdir> <kbd paths... | one directory> : ONE probe process, keyboards loaded in turn,
// U composer + legacy composer (FAM=7 default, SCRIPT=Tel default). Writes <outdir>/<base><SUFFIX>.tsv per keyboard.
// Resumable: keyboards whose .tsv already exists (and is stable + loaded) are skipped unless FORCE=1.
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const win = p => p.replace(/\//g, '\\');
const SD = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--USE~1\\767392~1\\SCRATC~1\\r3\\cap-tel\\'; // 8.3 short form of this directory
const exe = SD + 'probe2\\bin\\probe2.exe';
const out = win(/^[A-Za-z]:/.test(process.argv[2]) ? process.argv[2] : SD + process.argv[2]);
fs.mkdirSync(out, { recursive: true });
let files = process.argv.slice(3).map(p => win(/^[A-Za-z]:/.test(p) ? p : SD + p));
if (files.length === 1 && fs.statSync(files[0]).isDirectory()) {
  const d = files[0];
  files = fs.readdirSync(d).filter(f => /\.(TEL)$/i.test(f)).sort().map(f => path.win32.join(d, f));
}
const tsvOf = f => path.win32.join(out, path.win32.basename(f).replace(/\.[^.]+$/, '') + (process.env.SUFFIX || '') + '.tsv');
if (process.env.FORCE !== '1') files = files.filter(f => { const t = tsvOf(f); return !(fs.existsSync(t) && /^#stable\t1\n#loaded\t1\n/.test(fs.readFileSync(t, 'utf8'))); });
if (!files.length) { console.log('nothing to do'); process.exit(0); }
const list = files.map(f => f + '\t' + tsvOf(f)).join('\r\n');
const lf = SD + 'list-' + process.pid + '.txt';
fs.writeFileSync(lf, list);
execFileSync(exe, ['--list', lf, '--legacy', process.env.FAM || '7', '--script', process.env.SCRIPT || 'Tel', ...(process.env.LAYERS ? ['--layers', process.env.LAYERS] : [])], { stdio: 'inherit' });
fs.unlinkSync(lf);
console.log('captured', files.length);
