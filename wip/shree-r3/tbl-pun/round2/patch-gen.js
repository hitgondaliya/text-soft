// Round 2 generator edits (tools/PunTruth). Exact text edits, each must apply once.
// A backslash-u escape cannot be written in a tool input (it turns into the character), so BS builds them.
const fs = require("fs");
const dir = process.argv[2];
const BS = String.fromCharCode(92);
const U = (hex) => BS + "u" + hex;            // the six characters \uXXXX as they stand in the C# source
const CH = (hex) => String.fromCharCode(parseInt(hex, 16));
const ZWJ = CH("200D"), ZWNJ = CH("200C"), VIRAMA = CH("0A4D");

function edit(file, pairs) {
  const p = dir + "/" + file;
  let s = fs.readFileSync(p, "utf8");
  for (const [a, b] of pairs) {
    const n = s.split(a).length - 1;
    if (n !== 1) throw new Error(`${file}: ${n} matches for: ${a.slice(0, 90)}`);
    s = s.replace(a, b);
  }
  fs.writeFileSync(p, s);
  console.log("edited " + file);
}

edit("Reader.cs", [
  [`Learner.Extra.Where(e => !e.U.Contains('${U("200C")}') && e.U != "${U("0965")}")`,
   `Learner.Extra.Where(e => !e.U.Contains('${U("200C")}') && !e.U.Contains('${U("200D")}') && e.U != "${U("0965")}")`],
  ["    public static List<RewriteRule> ReversePre() => new()\n    {\n",
   "    public static List<RewriteRule> ReversePre() => new()\n    {\n        Rules.NoMarksIn,\n"],
]);

edit("Rules.cs", [
  ["    public static List<RewriteRule> ForwardPre() => new()\n    {\n",
   "    /// <summary>\n" +
   "    /// The tables' marks are noncharacters (U+FDD1, U+FDD8-U+FDDE): one already in the text would be taken for a mark (the\n" +
   "    /// spacer BE, the far addak ...). It becomes U+FFFD, which no pair takes: passed through and reported (as tools/BanTruth does).\n" +
   "    /// </summary>\n" +
   "    public static readonly RewriteRule NoMarksIn = R(\"[\" + (char)0xFDD0 + \"-\" + (char)0xFDDF + \"]\", \"\" + (char)0xFFFD,\n" +
   "        \"A noncharacter U+FDD0-U+FDDF in the text is not one of the tables' marks: U+FFFD, passed through and reported.\");\n\n" +
   "    public static List<RewriteRule> ForwardPre() => new()\n    {\n        NoMarksIn,\n"],
]);

edit("Learner.cs", [
  [`        ("${VIRAMA}${ZWNJ}", "B5"),\n`,
   `        ("${VIRAMA}${ZWNJ}", "B5"),\n` +
   "        // A virama before a ZWJ: Gurmukhi has no half forms, so a Unicode font draws the visible halant there, as before a\n" +
   "        // ZWNJ. The composer's link spelling (no glyph: C + virama + ZWJ = C alone) loses the virama (Capture.HalantBeforeZwj).\n" +
   `        ("${VIRAMA}${ZWJ}", "B5"),\n`],
  [`        atoms["${VIRAMA}${ZWJ}"] = Rules.Keep;\n`, ""],
  [`(${CH("0A15")}${VIRAMA}${ZWNJ} = 48 B5, ${CH("0A15")}${VIRAMA}${ZWJ} = 48).</summary>`,
   `(${CH("0A15")}${VIRAMA}${ZWNJ} = 48 B5, ${CH("0A15")}${VIRAMA}${ZWJ} = 48 B5).</summary>`],
  [`joiner (${CH("0A15")}${VIRAMA}${ZWJ} = 48: no glyph) and of a halant (${CH("0A15")}${VIRAMA}${ZWNJ} = 48 B5)`,
   `ZWJ and of a ZWNJ (${CH("0A15")}${VIRAMA}${ZWJ} = ${CH("0A15")}${VIRAMA}${ZWNJ} = 48 B5: the visible halant)`],
]);

edit("Capture.cs", [
  ["            rows.Add(new Truth(u, pref, acc.Distinct().ToArray()));\n        }\n        return rows.OrderBy(r => r.U, StringComparer.Ordinal).ToList();\n    }\n",
   "            rows.Add(new Truth(u, pref, acc.Distinct().ToArray()));\n        }\n        return HalantBeforeZwj(rows).OrderBy(r => r.U, StringComparer.Ordinal).ToList();\n    }\n\n" +
   "    /// <summary>\n" +
   "    /// A virama before a ZWJ. The capture typed C + virama + ZWJ only with the link code (kind canonical:link+ZWJ/ZWNJ,\n" +
   "    /// 38 rows): the 7 composer writes no glyph for the virama there, so its one spelling is the bare letter (" + CH("0A15") + VIRAMA + ZWJ + " = 48),\n" +
   "    /// which reads back as " + CH("0A15") + " and drops the virama a Unicode font shows - Gurmukhi has no half forms, so a virama before a\n" +
   "    /// ZWJ is drawn as the visible halant, as before a ZWNJ. Its ZWNJ twin was typed both ways and prefers the halant\n" +
   "    /// spelling (" + CH("0A15") + VIRAMA + ZWNJ + " = 48 B5, 48 accepted). So a row ending in virama + ZWJ takes its twin's preferred spelling; the composer's\n" +
   "    /// own stays accepted (and is what it shows while the link waits for its next letter). The same choice as\n" +
   "    /// tools/Shared/ShreeWordFinal.cs makes for a virama that ends a word (P1). 38 rows change.\n" +
   "    /// </summary>\n" +
   "    public static List<Truth> HalantBeforeZwj(List<Truth> rows)\n" +
   "    {\n" +
   "        var byU = rows.ToDictionary(r => r.U, StringComparer.Ordinal);\n" +
   "        return rows.Select(r =>\n" +
   `            r.U.Length >= 3 && r.U[^1] == '${U("200D")}' && r.U[^2] == '${U("0A4D")}'\n` +
   `            && byU.TryGetValue(r.U[..^1] + '${U("200C")}', out var twin) && twin.Pref != r.Pref\n` +
   "                ? r with { Pref = twin.Pref, Acc = r.Acc.Prepend(twin.Pref).Distinct().ToArray(), Composer = r.Pref }\n" +
   "                : r).ToList();\n" +
   "    }\n"],
]);

edit("Build.cs", [
  ["        Ascii.Complete(fwdTable, cmap, fam.Itself, learner.Stager());\n        NonIndic.Complete(fwdTable, codes, \"22\", \"27\");\n",
   "        Ascii.Complete(fwdTable, fam.Itself, learner.Stager());\n        NonIndic.Complete(fwdTable, codes, \"22\", \"27\");\n" +
   "        // P4 (shared policy): the C0 controls but TAB LF VT FF CR, DEL and the C1 codes are left out and reported.\n" +
   "        foreach (var l in NonIndic.LeaveOutControls(fwdTable)) Console.WriteLine(\"  \" + l);\n" +
   "        // P3b: what no code of the fonts draws is passed through as itself and reported - a pair marked unmapped that writes\n" +
   "        // the character, as the Gujarati / Devanagari tables hold them (NonIndic.Policy): the Gurmukhi signs the faces have no\n" +
   "        // glyph for, and ₹ in the 4/5/6 faces (no glyph at 3C).\n" +
   "        foreach (var g in PassedThrough(fam).Where(g => !fwdTable.Pairs.Any(p => p.From == g)))\n" +
   "            fwdTable.Pairs.Add(new FontSoftware.Core.Models.MappingPair { From = g, To = g, Unmapped = true });\n" +
   "        fwdTable.Pairs = fwdTable.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();\n"],
  ["        Console.WriteLine(\"  \" + Ascii.Check(fwd, cmap, fam.Itself, \"ਕ\", out var na));\n",
   "        Console.WriteLine(\"  \" + Ascii.Check(fwd, fam.Itself, \"ਕ\", out var na));\n"],
  ["        Console.WriteLine(\"  between ਨ: \" + Ascii.Check(fwd, cmap, fam.Itself, \"ਨ\", out var na2));\n",
   "        Console.WriteLine(\"  between ਨ: \" + Ascii.Check(fwd, fam.Itself, \"ਨ\", out var na2));\n"],
  ["    /// <summary>Code points Word or RichEdit draw nothing (or another font) for in these faces (r3\\word: SHREE-PUN, SHREE-PUN7).</summary>\n",
   "    /// <summary>The characters no code of the family's fonts draws, passed through as themselves and reported (P3b).</summary>\n" +
   "    public static string[] PassedThrough(Fam fam) => Learner.NoGlyph.Concat(fam.IsSeven ? Array.Empty<string>() : new[] { \"₹\" }).ToArray();\n\n" +
   "    /// <summary>Code points Word or RichEdit draw nothing (or another font) for in these faces (r3\\word: SHREE-PUN, SHREE-PUN7).</summary>\n"],
]);
