using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// Guj7Truth - build and check the Shree-Lipi Gujarati tables against what Shree's
// own composers write (tests/FontSoftware.Tests/Data/shree-guj7-truth.json: 17,335
// Unicode strings and the bytes the Shree-Lipi 7 composer types for each).
//
// The tables are rules plus pairs. The rules (Rules.cs) carry what the composer does
// everywhere: reph after the letter, ya-sign on a bare ya-cluster, i-sign before the
// cluster and its glyph by base (71/72/74/5B), signs before a letter's spacer, the
// sign+reph and sign+anusvara glyphs. The learner (Learner.cs) then adds a pair only
// where composing from smaller pieces gives the wrong bytes - the half letters, the
// ligatures, the composer's own quirks - so a syllable outside the truth is still
// built the composer's way (10-fold hold-out: 99.5% right).
//
// A virama that ends a word is the visible halant, as every Unicode font draws it - not the
// half form the composer answers for a bare virama, which is only what it shows while it
// waits for the next letter (tools/Shared/ShreeWordFinal.cs says how each such row is spelled;
// the fixture's "exceptions" list them, and the composer's waiting answers are still learned
// as the half forms a following letter joins).
//
// Text that is not Gujarati follows tools/Shared/ShreeNonIndic.cs: punctuation the fonts
// draw is written as its look-alike (“ ” as ‘‘ ’’, … as ...), any other character whose
// code the fonts map is reported as unmapped and left out, never printed as that code's letter.
//
//   guj7truth build [outDir]            learn and check all four tables, write them
//                                        (default: assets/config/mappings)
//   guj7truth measure [table] [show]     truth strings through the real FontConverter
//   guj7truth words [table] [n] [seed]   joined truth syllables, exact bytes
//   guj7truth cv [folds] [seed] [show]   hold-out: learn without some strings, convert them
//   guj7truth learned | back-learned     which strings needed a pair of their own, and why
//   guj7truth rulecheck                  the post rules must leave every composer spelling alone
//   guj7truth convert <table> <in> <out> words to bytes and back (for drawing them in the font)
//   guj7truth fixture [groundtruth.json] write the test fixture from the full capture

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "measure";
string Arg(int i, string def) => args.Length > i ? args[i] : def;

switch (mode)
{
    case "measure":
    {
        // Against the composer's bytes: the table as the composer spells (P3b - the no-glyph
        // characters, which the real table passes through, written as nothing), and then the proof
        // that the real table is exactly that plus the characters themselves.
        var truth = LoadTruth();
        var table = LoadTable(Arg(1, "unicode-to-shree-guj7.json"));
        var conv = new FontConverter(NonIndic.AsComposer(table, Policy.NoGlyph));
        var r = Checks.Truth(conv, truth, out var fails);
        Console.WriteLine(r);
        foreach (var f in fails.Take(int.Parse(Arg(2, "40")))) Console.WriteLine("  " + f);
        Console.WriteLine(NonIndic.CheckPassThrough(new FontConverter(table), conv, truth.Select(t => t.U).ToList(), Policy.NoGlyph, out var pf));
        foreach (var f in pf.Take(10)) Console.WriteLine("  " + f);
        break;
    }
    case "words":
    {
        var truth = LoadTruth();
        var table = LoadTable(Arg(1, "unicode-to-shree-guj7.json"));
        var conv = new FontConverter(NonIndic.AsComposer(table, Policy.NoGlyph));
        int n = int.Parse(Arg(2, "20000")), seed = int.Parse(Arg(3, "7"));
        var r = Checks.Words(conv, truth, n, seed, out var fails);
        Console.WriteLine(r);
        foreach (var f in fails.Take(int.Parse(Arg(4, "40")))) Console.WriteLine("  " + f);
        Console.WriteLine(NonIndic.CheckPassThrough(new FontConverter(table), conv, Checks.MakeWords(truth, n, seed).Select(w => w.U).ToList(), Policy.NoGlyph, out var pf));
        foreach (var f in pf.Take(10)) Console.WriteLine("  " + f);
        break;
    }
    case "learn":
    {
        // learn [out.json] [log.tsv]
        var truth = LoadTruth();
        var log = new List<string>();
        var pairs = Learner.Learn(truth, truth, out var stats, log);
        Console.WriteLine(stats);
        string path = Arg(1, Path.Combine(MapDir, "unicode-to-shree-guj7.json"));
        Save(Learner.Build(pairs), path);
        Console.WriteLine("wrote " + path);
        if (args.Length > 2) File.WriteAllLines(args[2], log);
        var conv = new FontConverter(LoadTable(path));
        Console.WriteLine(Checks.Truth(conv, truth, out _));
        Console.WriteLine(Checks.Words(conv, truth, 20000, 7, out _));
        break;
    }
    case "rulecheck":
    {
        // The post rules must leave every composer spelling as it is.
        var truth = LoadTruth();
        var post = new FontConverter(Table("p", "p", "a", "b", FontSoftware.Core.Models.FontType.Legacy,
            FontSoftware.Core.Models.FontType.Legacy, Array.Empty<FontSoftware.Core.Models.RewriteRule>(),
            Array.Empty<(string, string)>(), Learner.PostRules));
        var bad = new List<string>();
        foreach (var t in truth)
        {
            string got = Hex(post.ConvertText(FromHex(t.Pref)));
            if (got != t.Pref) bad.Add($"{Shape(t.U)}\t{t.U}\t{t.Pref}\t-> {got}");
        }
        Console.WriteLine($"post rules change {bad.Count} composer spellings");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        break;
    }
    case "learned":
    {
        // Which truth strings needed a pair of their own, by shape.
        var truth = LoadTruth();
        var log = new List<string>();
        var pairs = Learner.Learn(truth, truth, out var stats, log);
        Console.WriteLine(stats);
        foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "convert":
    {
        // convert <table> <words.txt> <out.tsv>: one word per line -> word, hex, round trip.
        var conv = new FontConverter(LoadTable(args[1]));
        string back = args[1].Contains("guj456") ? "shree-guj456-to-unicode.json" : "shree-guj7-to-unicode.json";
        var read = new FontConverter(LoadTable(back));
        var lines = new List<string>();
        foreach (var w in File.ReadAllLines(args[2], Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var r = conv.Convert(w);
            string again = read.ConvertText(r.Text);
            lines.Add($"{w}\t{Hex(r.Text)}\t{(again == w ? "=" : again)}\t{string.Join("", r.UnmappedCharacters)}");
        }
        File.WriteAllLines(args[3], lines, new UTF8Encoding(false));
        foreach (var l in lines) Console.WriteLine(l);
        break;
    }
    case "fixture":
    {
        // fixture [groundtruth.json]: write the compact test fixture from the full capture.
        var truth = LoadTruth(Arg(1, GroundTruth), raw: true);
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners are invisible: write them as escapes.
        string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc })
            .Replace("\u200C", "\\u200C").Replace("\u200D", "\\u200D");
        var sb = new StringBuilder();
        sb.Append("{\n  \"about\": ").Append(J(
            "What Shree-Lipi's own composers write for Unicode Gujarati in the Shree-Lipi 7 Gujarati fonts (SHREE-GUJ7-*). " +
            "Each row: the Unicode string, the preferred bytes (hex, one byte per glyph code: what the 7 composer types for it), " +
            "and, when there are any, the other spellings the composer also accepts for the same text. Captured from " +
            "S8GujU64.dll and S8Guj764.dll (Shree-Lipi nxt) by typing the same keys into both; tools/Guj7Truth builds and checks " +
            "the shree-guj7 and shree-guj456 tables against it. Rows that end in a bare virama hold the composer's answer while it " +
            "waits for the next letter (a half form); at the end of a word the tables write the visible halant instead. The ru and ruu " +
            "rows are written as the ligatures Shree's own ru/ruu keys type, and ૰ (whose code Word draws as nothing, and which no other code of the fonts draws) is passed through as itself and reported (U+0AF0). " +
            "\"exceptions\" says, row by row, what they write (\"the table writes <hex>: why\" - a U+XXXX token is that character, passed through -, or \"the table leaves it out and reports it: why\")."));
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var exceptions = truth.Select(t => (t, a: Adjust(t, byU))).Where(x => x.a is not null).OrderBy(x => x.t.U, StringComparer.Ordinal).ToList();
        sb.Append(",\n  \"exceptions\": {\n");
        for (int i = 0; i < exceptions.Count; i++)
        {
            var (t, a) = exceptions[i];
            sb.Append("    ").Append(J(t.U)).Append(": ").Append(J(a!.Value.Pref.Length == 0 ? $"the table leaves it out and reports it: {a.Value.Why}" : $"the table writes {a.Value.Pref}: {a.Value.Why}"))
              .Append(i < exceptions.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  }");
        sb.Append(",\n  \"rows\": [\n");
        var rows = truth.OrderBy(t => t.U, StringComparer.Ordinal).ToList();
        for (int i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var other = t.Acc.Where(a => a != t.Pref).ToList();
            sb.Append("    [").Append(J(t.U)).Append(", ").Append(J(t.Pref));
            if (other.Count > 0) sb.Append(", [").Append(string.Join(", ", other.Select(J))).Append(']');
            sb.Append(']').Append(i < rows.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Fixture)!);
        File.WriteAllText(Fixture, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {rows.Count} rows to {Fixture} ({new FileInfo(Fixture).Length / 1024} KB)");
        break;
    }
    case "back-learned":
    {
        var truth = LoadTruth();
        var conv7 = new FontConverter(Learner.Build(Learner.Learn(truth, truth, out _)));
        var log = new List<string>();
        Reader.Learn(truth, conv7, out var stats, log);
        Console.WriteLine(stats);
        foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "build":
    {
        // build [outDir]: all four Shree-Guj tables, learned and checked.
        string outDir = Arg(1, MapDir);
        var truth = LoadTruth();
        Build.All(truth, outDir);
        break;
    }
    case "cv":
    {
        // cv [folds] [seed] [show] [shapeRegex]: learn on all folds but one, convert the one left out.
        var truth = LoadTruth();
        int folds = int.Parse(Arg(1, "10")), seed = int.Parse(Arg(2, "1")), show = int.Parse(Arg(3, "30"));
        var only = args.Length > 4 ? new System.Text.RegularExpressions.Regex(args[4]) : null;
        var rng = new Random(seed);
        // The units text is built from are always known: single characters, half letters
        // (C+virama, with or without a joiner), the ra- and ya-clusters (C+virama+ra/ya).
        // Everything longer is held out in turn.
        static bool Unit(string u) => u.Length <= 2 || Shape(u) is "CHR" or "CHN" or "CHJ" or "RHN" or "RHJ" ||
            (u.Length == 3 && u.EndsWith("્ય"));
        var fold = truth.ToDictionary(t => t.U, t => Unit(t.U) ? -1 : rng.Next(folds), StringComparer.Ordinal);
        int ok = 0, n = 0;
        var bad = new List<string>();
        for (int f = 0; f < folds; f++)
        {
            var pairs = Learner.Learn(truth, truth.Where(t => fold[t.U] != f), out _);
            var conv = new FontConverter(Learner.Build(pairs));
            foreach (var t in truth.Where(t => fold[t.U] == f && t.U.Length > 1))
            {
                if (only is not null && !only.IsMatch(t.U)) continue;
                n++;
                string got = Hex(conv.ConvertText(t.U));
                if (got == t.Pref || t.Acc.Contains(got)) ok++;
                else bad.Add($"{Shape(t.U)}\t{t.U}\twant {t.Pref}\tgot {got}");
            }
        }
        Console.WriteLine($"held out {n}: {ok} right ({100.0 * ok / n:F2}%), {bad.Count} wrong");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(show)) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        break;
    }
    default:
        Console.Error.WriteLine("unknown mode " + mode);
        return 1;
}
return 0;

// C = consonant, H = virama, m = sign, V = vowel, J/N = joiners, o = other.
static string Shape(string u) => new(u.Select(c =>
    c == 'ર' ? 'R' : IsCons(c) ? 'C' : IsVirama(c) ? 'H' : c == '‍' ? 'J' : c == '‌' ? 'N' : IsMark(c) ? 'm' :
    (c >= 'અ' && c <= 'ઔ') || c == 'ૠ' || c == 'ૡ' ? 'V' : 'o').ToArray());
