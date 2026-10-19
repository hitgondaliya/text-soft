// Do the U and 7 composers answer the Ctrl+Space (layer 1) and Ctrl+Shift+1 (layer 2) keys alike, and do they answer
// what the key's code means? The code meanings come from the single-code probes (capp, "code XX" rows).
// layer 0: key k holds code c = file[k]; layer 1: code c = file[128 + i] (i = index in CTRL_SPACE_ORDER);
// layer 2: hypothesis = the key character itself as a code.
const { fs, readTsv, cleanU, hex2, readKbd, CTRL_SPACE_ORDER } = require('./lib');
const res = require('./capp/P-results.json');
const one = new Map(res.filter(r => /^code /.test(r.tag)).map(r => [parseInt(r.codes, 16), r]));
const S = 'C:/Shree-Lipi nxt/';
const files = fs.readdirSync(S).filter(f => /\.PUN$/i.test(f)).sort();
const st = {};
const bump = (k, ok) => { st[k] = st[k] || { agree: 0, differ: 0, ex: [] }; ok ? st[k].agree++ : st[k].differ++; };
for (const f of files) {
  const kb = readKbd(S + f);
  const { rows } = readTsv('capvo/' + f.replace(/\.PUN$/i, '') + '.tsv');
  const l0 = new Map(rows.filter(r => r.layer === 0).map(r => [r.keyCode, r]));
  const b7 = r => (r && r.lb ? r.lb.map(hex2).join(' ') : null);
  for (const r of rows) {
    let c;
    if (r.layer === 0) c = kb.bytes[r.keyCode];
    else if (r.layer === 1) { const i = CTRL_SPACE_ORDER.indexOf(r.key); if (i < 0) continue; c = kb.bytes[128 + i]; }
    else c = r.keyCode;
    if (c === 0xFF || (c >= 0x90 && c <= 0x9F) || (c >= 0xB0 && c <= 0xCF)) continue;
    const p = one.get(c);
    if (!p) continue;
    const uOk = cleanU(r.u) === p.u, lOk = b7(r) === p.bytes;
    bump(`L${r.layer} U==code`, uOk); bump(`L${r.layer} 7==code`, lOk);
    if (!lOk) { const s = st[`L${r.layer} 7==code`]; const v = l0.get(c); if (s.ex.length < 8) s.ex.push(`${f} '${r.key}' code ${hex2(c)}: 7 ${b7(r)} code-probe ${p.bytes} L0-key('${String.fromCharCode(c)}') ${b7(v)}`); }
    if (!uOk) { const s = st[`L${r.layer} U==code`]; if (s.ex.length < 8) s.ex.push(`${f} '${r.key}' code ${hex2(c)}: U ${JSON.stringify(r.u)} code-probe ${JSON.stringify(p.u)}`); }
    if (r.layer === 1 && !lOk) { const v = l0.get(c); bump('L1 7==L0-key-chr(code)', b7(v) === b7(r)); }
  }
}
for (const [k, v] of Object.entries(st)) console.log(k, 'agree', v.agree, 'differ', v.differ, v.ex.length ? '\n   ' + v.ex.join('\n   ') : '');
