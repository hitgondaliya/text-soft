// node splice.js <file> <patchfile> <fromLine> <toLine> : replace lines fromLine..toLine (1-based, inclusive)
const fs = require('fs');
const [f, p, from, to] = process.argv.slice(2);
if (!(+from >= 1 && +to >= +from)) throw new Error('bad line range ' + from + '..' + to);
const lines = fs.readFileSync(f, 'utf8').split('\n');
const rep = fs.readFileSync(p, 'utf8').replace(/\n$/, '').split('\n');
lines.splice(+from - 1, +to - +from + 1, ...rep);
fs.writeFileSync(f, lines.join('\n'), 'utf8');
