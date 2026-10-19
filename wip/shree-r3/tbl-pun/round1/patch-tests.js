// Add the Punjabi tables to ShreeNonIndicTests and ShreeFontCmapTests (exact text edits; each must apply once).
const fs = require('fs');
const d = process.argv[2];
function edit(file, pairs) {
  const p = d + '/' + file;
  let s = fs.readFileSync(p, 'utf8');
  for (const [a, b] of pairs) {
    const n = s.split(a).length - 1;
    if (n !== 1) throw new Error(`${file}: ${n} matches for: ${a.slice(0, 80)}`);
    s = s.replace(a, b);
  }
  fs.writeFileSync(p, s);
  console.log('edited ' + file);
}

edit('ShreeNonIndicTests.cs', [
  ['        { "mangal", "shree-dev7" }, { "mangal", "shree-dev456" }, { "mangal", "shree-san7" },\n    };',
   '        { "mangal", "shree-dev7" }, { "mangal", "shree-dev456" }, { "mangal", "shree-san7" },\n        { "raavi", "shree-pun7" }, { "raavi", "shree-pun456" },\n    };'],
  ['    public static HashSet<char> FontCodes(string encoding)\n    {\n',
   '    public static HashSet<char> FontCodes(string encoding)\n    {\n        // The Gurmukhi faces map ASCII and A0-BF only (and U+00B7 U+2219): their cmaps, read from every installed face.\n        if (encoding.StartsWith("shree-pun")) return ShreePunjabiTruthTests.CmapCodes(encoding);\n'],
  ['        string letter = encoding.StartsWith("shree-guj") ? "ક" : "क", alone = to.ConvertText(letter);',
   '        string letter = encoding.StartsWith("shree-guj") ? "ક" : encoding.StartsWith("shree-pun") ? "ਕ" : "क", alone = to.ConvertText(letter);'],
  ['        Assert.True(left > 100, $"{encoding}: only {left} characters left out");',
   '        // (the Gurmukhi faces map only A0-BF beyond ASCII: fewer codes to leave out)\n        Assert.True(left > (encoding.StartsWith("shree-pun") ? 20 : 100), $"{encoding}: only {left} characters left out");'],
  ['    [InlineData("mangal", "shree-san7", "राम…", "5A 70 58 22 2E 2E 2E")]\n',
   '    [InlineData("mangal", "shree-san7", "राम…", "5A 70 58 22 2E 2E 2E")]\n    [InlineData("raavi", "shree-pun7", "“ਪੰਜਾਬ”", "22 22 5C A5 4F 70 5E 27 27")]   // the fonts\' ‘ ’ are 22 27\n    [InlineData("raavi", "shree-pun456", "ਪੰਜਾਬ…", "5C A5 4F 70 5E 2E 2E 2E")]\n    [InlineData("raavi", "shree-pun7", "• ਕ", "B7 20 48")]\n'],
  ['            Assert.Equal(c == \'€\' ? "€" : "", r.Text);',
   '            // (a character not in the fonts at all - € everywhere, ½ ™ ƒ in the Gurmukhi faces - passes through)\n            Assert.Equal(FontCodes(encoding).Contains(c) ? "" : c.ToString(), r.Text);'],
]);

edit('ShreeFontCmapTests.cs', [
  ['        { "mangal", "shree-san7", "^SHREE-SAN7-", "shree-san7-truth.json" },\n    };',
   '        { "mangal", "shree-san7", "^SHREE-SAN7-", "shree-san7-truth.json" },\n        { "raavi", "shree-pun7", "^SHREE-PUN7-", "shree-pun7-truth.json" },\n        { "raavi", "shree-pun456", "^SHREE-PUN-", "shree-pun7-truth.json" },\n    };'],
  ['            foreach (char c in conv.ConvertText(u)) Note(c, u);',
   '            // what passes through unmapped (₹ in a 4/5/6 Gurmukhi face) is reported to the user, not written by the table\n            var r = conv.Convert(u);\n            foreach (char c in r.Text) if (!r.UnmappedCharacters.Contains(c.ToString())) Note(c, u);'],
]);
