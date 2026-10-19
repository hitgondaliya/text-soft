// L2: are the bytes TextSoft typed for each word a concatenation of the composer's own spellings
// (ground truth of the last round: preferred + accepted) of that word's pieces?
const fs = require('fs'), path = require('path');
const GT = 'C:/Users/harde/AppData/Local/Temp/claude/C--Users-harde-Desktop-Windows-Font-Software/624671f5-ee6d-402b-b810-05b52f236677/scratchpad/shree7/guj7-groundtruth.json';
const gt = JSON.parse(fs.readFileSync(GT, 'utf8')).truth;
const o = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'results', 'l2-words.json'), 'utf8'));
const NGA = [['50 B9 24 4C 24', '91'], ['50 B9 24 6E', 'B6'], ['50 B9 24 4D', '92'], ['50 B9 24 4E', '93'], ['50 B9 24 4F', '94']];
const to456 = h => { let s = ' ' + h + ' '; for (const [a, b] of NGA) s = s.split(' ' + a + ' ').join(' ' + b + ' '); return s.trim(); };
const keys = Object.keys(gt).sort((a, b) => b.length - a.length);
const byFirst = {}; for (const k of keys) (byFirst[k[0]] = byFirst[k[0]] || []).push(k);
function match(word, bytes, fam) {
  const B = bytes.split(' ').filter(Boolean);
  const memo = new Map();
  function go(i, j) {
    if (i === word.length) return j === B.length;
    const key = i + ',' + j; if (memo.has(key)) return memo.get(key);
    let ok = false;
    for (const k of byFirst[word[i]] || []) {
      if (!word.startsWith(k, i)) continue;
      for (let h of gt[k].accepted) {
        if (fam === '456') h = to456(h);
        const hb = h.split(' ');
        if (hb.every((x, n) => B[j + n] === x) && go(i + k.length, j + hb.length)) { ok = true; break; }
      }
      if (ok) break;
    }
    memo.set(key, ok); return ok;
  }
  return go(0, 0);
}
const seen = new Map(); let n = 0, ok7 = 0, ok456 = 0, nopiece = 0; const bad = [];
for (const w of o.words) {
  if (!w.plan || seen.has(w.word)) continue;
  seen.set(w.word, true); n++;
  const m7 = match(w.word, w.g7.final, '7'), m4 = match(w.word, w.g456.final, '456');
  if (m7) ok7++; if (m4) ok456++;
  if (!m7 || !m4) bad.push({ word: w.word, g7: w.g7.final, g456: w.g456.final, m7, m4 });
}
console.log('distinct typed words', n, 'g7 = composer pieces', ok7, 'g456 = composer pieces', ok456);
for (const b of bad) console.log('  ', b.word, 'g7', b.g7, b.m7, '| g456', b.g456, b.m4);
fs.writeFileSync(path.join(__dirname, '..', 'results', 'l2-composer-pieces.json'), JSON.stringify({ words: n, g7: ok7, g456: ok456, notMatched: bad }, null, 1));
