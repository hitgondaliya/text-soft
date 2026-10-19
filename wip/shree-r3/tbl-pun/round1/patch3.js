// Fix the common-mode block (nukta precompose with ASCII escapes, tuple names) and the decomposed RealWords entries.
// Every escape is built from String.fromCharCode(92): the tool input pipeline turns escapes into characters and
// normalises them (NFC decomposes the six nukta letters), so they cannot be typed directly.
const fs = require('fs');
const d = process.argv[2];
const B = String.fromCharCode(92);
const u = cp => B + 'u' + cp.toString(16).toUpperCase().padStart(4, '0');
const pairs = [[0x0A16, 0x0A59], [0x0A17, 0x0A5A], [0x0A1C, 0x0A5B], [0x0A2B, 0x0A5E], [0x0A32, 0x0A33], [0x0A38, 0x0A36]];

let p = fs.readFileSync(d + '/Program.cs', 'utf8');
const start = p.indexOf('            string pre = w.Replace(');
const end = p.indexOf('            n++;', start);
if (start < 0 || end < 0) throw new Error('common block not found');
const repl = pairs.map(([a, b]) => `.Replace("${u(a)}${u(0x0A3C)}", "${u(b)}")`).join('');
const block =
`            string pre = w${repl};
            var parts = pre.Split(' ').Select(p => Build.ByTruth(p, byU)).ToList();
            (string Hex, string Split)? exp = parts.Any(p => p is null) ? null
                : (string.Join(" 20 ", parts.Select(p => p!.Value.Hex)), string.Join(" ", parts.Select(p => p!.Value.Split)));
            if (exp is null) { none++; Console.WriteLine($"  {w}\\tno split into truth strings\\tours {Hex(conv.ConvertText(w))}"); continue; }
`;
// the line after the old block start up to n++ includes the old 'if (exp is null)' line: replace all of it
p = p.slice(0, start) + block + p.slice(end);
fs.writeFileSync(d + '/Program.cs', p);

let b = fs.readFileSync(d + '/Build.cs', 'utf8');
const anchor = '        // nukta letters typed decomposed (NFC text: the six nukta letters are composition exclusions)\n';
const i = b.indexOf(anchor);
if (i < 0) throw new Error('RealWords anchor not found');
const j = b.indexOf('\n', i + anchor.length);
const line = `        "${u(0x0A16)}${u(0x0A3C)}${u(0x0A3E)}${u(0x0A32)}${u(0x0A38)}${u(0x0A3E)}", "${u(0x0A38)}${u(0x0A3C)}${u(0x0A39)}${u(0x0A3F)}${u(0x0A30)}", "${u(0x0A1C)}${u(0x0A3C)}${u(0x0A3F)}${u(0x0A32)}${u(0x0A4D)}${u(0x0A39)}${u(0x0A3E)}",`;
b = b.slice(0, i + anchor.length) + line + b.slice(j);
fs.writeFileSync(d + '/Build.cs', b);
console.log('patched');
