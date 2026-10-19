// A truth-shaped file of the strings the composer does not settle (tam7-composer-gaps.json, derived spellings) plus the
// ASCII symbols whose own bytes are Tamil glyphs (preferred = what the 7 composer writes for them: a control byte with no
// glyph), so measure.exe shows what the shipped table writes for them. NOT truth. -> gaps-as-truth.json
const fs = require('fs');
const g = require('./tam7-composer-gaps.json').gaps;
const truth = {};
for (const x of g) if (x.derived) truth[x.unicode] = { preferred: x.derived, accepted: [x.derived], alternatives: [] };
const ctl = { '#': '12', '$': '13', '<': '14', '>': '15', '@': '16', '_': '18', '[': '0B', ']': '0C', '{': '1A', '}': '1B', '\\': '17', '|': '1C', '`': '19', '^': '0F', '&': '06', '“': '0D', '”': '0E', '~': '7E?' };
for (const [u, b] of Object.entries(ctl)) if (!b.includes('?')) truth[u] = { preferred: b, accepted: [b], alternatives: [] };
fs.writeFileSync(__dirname + '/gaps-as-truth.json', JSON.stringify({ about: 'NOT truth: derived spellings (tam7-composer-gaps.json) and the composer control bytes for ASCII symbols, to see what the shipped table writes', truth }, null, 1));
console.log(Object.keys(truth).length);
