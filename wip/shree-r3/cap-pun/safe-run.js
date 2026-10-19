// Round 2 driver: ONE composer process, with SLNXT's SL8_DLL_DATA saved first and written back after (even on error).
// node safe-run.js <label> <capdir> <probedir>
//  1. shmlab dump (read-only) -> shmdump/<label>-pre.bin
//  2. node runlist.js <capdir> <probedir>   (probe2: U + 7 composer, every key, layers 0..2, two passes, #loaded check)
//  3. probe2 --put shmdump/<label>-pre.bin  (no composer loaded) and a fresh dump compared byte for byte
const { spawnSync } = require('child_process');
const fs = require('fs');
const SD = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--USE~1\\767392~1\\SCRATC~1\\r3\\cap-pun\\';
const [label, capdir, probedir] = process.argv.slice(2);
const pre = SD + 'shmdump\\' + label + '-pre.bin';
const post = SD + 'shmdump\\' + label + '-post-restore.bin';
const log = m => { console.log(m); fs.appendFileSync(__dirname + '/safe-run.log', new Date().toISOString() + ' ' + label + ' ' + m + '\n'); };
let r = spawnSync(SD + 'shmlab\\bin\\shmlab.exe', ['dump:' + pre], { encoding: 'utf8' });
if (r.status !== 0) { log('pre-dump failed ' + r.status + ' ' + r.stderr); process.exit(1); }
const before = fs.readFileSync(pre);
log('pre-dump header ' + [...before.slice(0, 11)].map(x => x.toString(16).padStart(2, '0')).join(' ') + ' nonzero bytes ' + before.filter(x => x).length);
let ok = true;
try {
  r = spawnSync('node', [__dirname + '/runlist.js', capdir, probedir], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], timeout: 3600000 });
  log('runlist exit ' + r.status + ' ' + (r.stdout || '').trim().split('\n').slice(-2).join(' | ') + ' ' + (r.stderr || '').trim().slice(0, 400));
  if (r.status !== 0) ok = false;
} finally {
  r = spawnSync(SD + 'probe2\\bin\\probe2.exe', ['--put', pre], { encoding: 'utf8' });
  log('restore: ' + (r.stdout || '').trim() + ' ' + (r.stderr || '').trim());
  r = spawnSync(SD + 'shmlab\\bin\\shmlab.exe', ['dump:' + post], { encoding: 'utf8' });
  const after = fs.readFileSync(post);
  let d = 0; for (let i = 0; i < 4096; i++) if ((before[i] || 0) !== (after[i] || 0)) d++;
  log('after restore: ' + d + ' bytes differ from the pre-dump');
  if (d) ok = false;
}
process.exit(ok ? 0 : 2);
