// Builds the probe keyboards in C:\Users\harde\AppData\Local\Temp\r3g (short path, non-vendor names
// whose second-to-last character is a letter, so a load writes table 0 of SL8_DLL_DATA only).
// V?? = byte copies of the 19 vendor .GUJ files; C?? = code sweep: every code that is not a
// conjunct-slot reference (90-9F, B0-CF) on one printable key, slots and Ctrl+Space layer empty.
const fs = require('fs'), path = require('path');
const S = 'C:/Shree-Lipi nxt', K = 'C:/Users/harde/AppData/Local/Temp/r3g';
const vend = ['AKSHAR','ASTERIX','BPMK','CRTRONIC','DOE','ENG','INDICA','INSCRIPT','ITR','MTNK','NEWROMA','PHONET86','PHONETIC','RAMING','RAMING1','RAMING2','ROMA','SCRIPT','TYPEWRIT'];
const L = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const manifest = { vendor: {}, sweep: [] };
vend.forEach((v, i) => {
  const name = 'VA' + L[i] + '.GUJ';
  const b = fs.readFileSync(path.join(S, v + '.GUJ'));
  if (b.length !== 406) throw new Error(v + ' size ' + b.length);
  fs.writeFileSync(path.join(K, name), b);
  manifest.vendor[name] = v;
});
const base = fs.readFileSync(path.join(S, 'INSCRIPT.GUJ'));
const codes = [];
for (let c = 1; c <= 0xFE; c++) if (!(c >= 0x90 && c <= 0x9F) && !(c >= 0xB0 && c <= 0xCF)) codes.push(c);
const keys = []; for (let k = 0x21; k <= 0x7E; k++) keys.push(k);
for (let f = 0; f * keys.length < codes.length; f++) {
  const name = 'CA' + L[f] + '.GUJ';
  const b = Buffer.from(base);
  for (let k = 0x21; k <= 0x7E; k++) b[k] = 0xFF;
  for (let i = 128; i < 166; i++) b[i] = 0xFF;
  for (let n = 0; n < 48; n++) { const o = 166 + n * 5; b[o] = 4; for (let j = 1; j <= 4; j++) b[o + j] = 0xFF; }
  codes.slice(f * keys.length, (f + 1) * keys.length).forEach((c, i) => { b[keys[i]] = c; manifest.sweep.push({ file: name, key: keys[i], code: c }); });
  fs.writeFileSync(path.join(K, name), b);
}
fs.writeFileSync('probe-manifest.json', JSON.stringify(manifest, null, 1));
const W = p => p.split('/').join('\\');
const out = W(process.cwd()) + '\\out\\';
const lines = fs.readdirSync(K).filter(f => /\.GUJ$/.test(f)).sort().map(f => W(K + '/' + f) + '\t' + out + f.replace('.GUJ', '.tsv'));
fs.writeFileSync('caplist.txt', lines.join('\r\n'));
console.log(lines.length, 'keyboards;', codes.length, 'sweep codes');
