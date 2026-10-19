// L3 table: per character, what the guj7 / guj456 forward tables write, what the Converter
// screen reports, what Shree's composer writes for the same character (code sweep), and what
// the fonts draw at the written bytes (read off results/png/chart-*.png, transcribed below).
const fs = require('fs'), path = require('path');
const A = path.join(__dirname, '..');
const o = JSON.parse(fs.readFileSync(path.join(A, 'results', 'l3-conv-out.json'), 'utf8'));
const codes = JSON.parse(fs.readFileSync(path.join(A, 'results', 'composer-codes.json'), 'utf8')).codes;
// What SHREE-GUJ7-0750 / SHREE-GUJ-0750 / Shree-Guj7-001 draw at the ASCII and symbol bytes (same in all
// three except 2A, which is a star in the 0750 designs and an asterisk in Shree-Guj7-001).
const DRAW = {
  '20': 'space', '21': '!', '22': '‘ (left single quote)', '23': '×', '24': 'blank spacer', '25': '%', '26': '| (danda bar)',
  '27': '’ (right single quote)', '28': '(', '29': ')', '2A': '★ star (0750) / * (Guj7-001)', '2B': '+', '2C': ',', '2D': '-', '2E': '.', '2F': '/',
  '30': '૦', '31': '૧', '32': '૨', '33': '૩', '34': '૪', '35': '૫', '36': '૬', '37': '૭', '38': '૮', '39': '૯',
  '3A': ': (visarga)', '3B': ';', '3C': 'NO GLYPH (not in cmap)', '3D': '=', '3E': 'blank spacer', '3F': '?', '40': 'ઽ', '5B': 'long i-sign',
  '5C': 'થ', '5D': 'ii-sign+anusvara', '5E': 'ધ', '5F': 'ન', '60': 'પ', '7B': 'રુ ligature', '7C': 'ૂ sign', '7D': 'ii-sign+reph', '7E': 'રૂ (new) ligature',
  '81': '— dash', '8D': '÷', '8F': '° (abbreviation circle ૰)', '90': 'short dash', '97': 'blank (danda spacer)', 'B7': '● bullet', 'AD': 'blank',
};
for (let c = 0x41; c <= 0x5A; c++) DRAW[c.toString(16).toUpperCase()] = DRAW[c.toString(16).toUpperCase()] || 'a Gujarati letter';
for (let c = 0x61; c <= 0x7A; c++) DRAW[c.toString(16).toUpperCase()] = DRAW[c.toString(16).toUpperCase()] || 'a Gujarati letter/sign';
const byU = {};
for (const [c, s] of Object.entries(codes)) { if (!s.u) continue; let u = s.u; if (u.length > 1 && (u[0] === ' ' || u[0] === '.')) u = u.slice(1); if (!(u in byU)) byU[u] = { code: c, b7: s.b7 }; }
const rows = {};
for (const r of o) {
  const key = r.Tag === 'between' ? r.Text.slice(1, -1) : r.Text;
  (rows[key] = rows[key] || {})[r.Dir + '_' + r.Tag] = r;
}
const table = [];
for (const [c, x] of Object.entries(rows)) {
  const a7 = x.fwd7_alone, a4 = x.fwd456_alone, b7 = x.fwd7_between;
  const comp = byU[c];
  const drawn = (a7.outHex || '').split(' ').filter(Boolean).map(h => h.startsWith('U+') ? h + ' (not a font code: drawn by a fallback font, or a box)' : (DRAW[h] || '?')).join(' + ');
  table.push({ char: c, cp: [...c].map(ch => 'U+' + ch.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')).join(' '), guj7: a7.outHex, guj456: a4.outHex, betweenLetters7: b7.outHex, drawnAs: drawn, unmapped: a7.unmapped, reportedOnConverterScreen: a7.reported, shreeCode: comp ? comp.code : null, shree7: comp ? comp.b7 : null });
}
fs.writeFileSync(path.join(A, 'results', 'l3-symbols.json'), JSON.stringify(table, null, 1));
for (const t of table) console.log(JSON.stringify(t.char).padEnd(9), t.cp.padEnd(14), 'g7', (t.guj7 || '(none)').padEnd(12), 'g456', (t.guj456 || '(none)').padEnd(12), 'draws:', t.drawnAs.padEnd(34), 'rep', JSON.stringify(t.reportedOnConverterScreen).padEnd(8), 'shree', t.shreeCode ? t.shreeCode + '->' + t.shree7 : '-');
