using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// PunTruth - build and check the Shree-Lipi Punjabi (Gurmukhi) tables against what Shree's own composers write
// (tests/FontSoftware.Tests/Data/shree-pun7-truth.json: 27,752 Unicode strings and the bytes S8Pun764 types for each).
//
// The tables are rules plus pairs. The rules (Rules.cs) carry what the composer does everywhere: the i-sign before
// its cluster, the spacer BE after ਠ ਨ with the top marks before it and ੁ ੂ after it as AE AF, the far addak A8
// after a mark that reaches the top, the bindi A4 after ਉ ਊ, a tippi after a vowel sign written as the bindi. The
// learner (Learner.cs) then adds a pair only where composing from smaller pieces gives the wrong bytes, so a syllable
// outside the truth is still built the composer's way. One table pair per font family (Family.cs: the 7 faces and the
// 4/5/6 faces are the same design but for ₹ 3C).
//
// Text that is not Gurmukhi follows tools/Shared/ShreeNonIndic.cs (and Ascii.cs for ASCII): punctuation the fonts draw
// is written as its look-alike, any other character whose code the fonts map is reported as unmapped and left out.
//
//   puntruth prep                          the capture: rows, codes written, accepted spellings
//   puntruth fixture                       write the test fixture from the capture
//   puntruth build [outDir] [fam...]       learn and check every family's tables, write them (default assets/config/mappings,
//                                          and the families' lines of assets/config/encoding-limits.json)
//   puntruth verify                        build into a temp folder and compare byte for byte with the tables in the repo
//   puntruth measure <table> [fam] [show]  truth strings through a table (default family shree-pun7)
//   puntruth words <table> [fam] [n] [seed]
//   puntruth common <table> [fam]          the capture's 238 common words against the composer's syllables joined
//   puntruth learned [fam] [n]             which strings needed a pair of their own, by shape
//   puntruth cv [fam] [folds] [seed] [show] [shapeRegex]   hold-out: learn without some strings, convert them
//   puntruth back-learned [fam] [n]        the reverse pairs, by shape
//   puntruth reverse <table> [fam] [n]     a reverse table over the truth, the accepted spellings and the whole capture
//   puntruth read <fam> <dir|-> <hex;hex;...>   legacy bytes through the family's reverse table (and forward again)
//   puntruth fontcheck [dir]               every code point each forward table in dir writes, against every installed face's cmap
//   puntruth cmaps [out]                   write the faces' cmaps (the test fixture Data/shree-pun-cmaps.json)
//   puntruth wordfinal [n]                 the rows that end in a virama, and what tools/Shared/ShreeWordFinal.cs would write
//   puntruth oldread <oldForwardTable> <dir|-> [n]   what an earlier forward table wrote, read back by the reverse tables now
//   puntruth convert <fam> <in> <out>      words to bytes and back

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "prep";
string Arg(int i, string def) => args.Length > i ? args[i] : def;

static List<Truth> Load() => File.Exists(Fixture) ? LoadTruth(Fixture) : Capture.Read(GroundTruth);

switch (mode)
{
    case "prep":
    {
        var rows = Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]);
        Console.WriteLine($"truth rows {rows.Count}, with more than one accepted spelling {rows.Count(r => r.Acc.Length > 1)}");
        var codes = rows.SelectMany(r => r.Acc).SelectMany(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(c => c).ToList();
        Console.WriteLine($"codes written ({codes.Count}): {string.Join(" ", codes)}");
        foreach (var g in rows.GroupBy(r => Shape(r.U)).OrderByDescending(g => g.Count()).Take(30))
            Console.WriteLine($"  {g.Key}: {g.Count()}");
        break;
    }
    case "fixture":
    {
        Fixtures.Write(Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]));
        break;
    }
    case "measure":
    {
        var fam = Fam.Get(Arg(2, "shree-pun7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine(Checks.Truth(conv, truth, out var fails));
        foreach (var g in fails.GroupBy(f => Shape(f.Split('\t')[0])).OrderByDescending(g => g.Count()).Take(40))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var f in g.Take(int.Parse(Arg(3, "4")))) Console.WriteLine("      " + f);
        }
        foreach (int seed in new[] { 7, 11 }) Console.WriteLine(Checks.Words(conv, truth, 20000, seed, out _));
        break;
    }
    case "words":
    {
        var fam = Fam.Get(Arg(2, "shree-pun7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine(Checks.Words(conv, truth, int.Parse(Arg(3, "20000")), int.Parse(Arg(4, "7")), out var fails));
        foreach (var f in fails.Take(40)) Console.WriteLine("  " + f);
        break;
    }
    case "common":
    {
        var fam = Fam.Get(Arg(2, "shree-pun7"));
        var truth = fam.Truth(Load());
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        string file = Path.Combine(Path.GetDirectoryName(GroundTruth)!, "common-words.txt");
        int n = 0, ok = 0, none = 0;
        foreach (var w in File.ReadAllLines(file, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            // the truth holds the nukta letters precomposed, as the composer writes them; words are split at spaces
            string pre = w.Replace("\u0A16\u0A3C", "\u0A59").Replace("\u0A17\u0A3C", "\u0A5A").Replace("\u0A1C\u0A3C", "\u0A5B").Replace("\u0A2B\u0A3C", "\u0A5E").Replace("\u0A32\u0A3C", "\u0A33").Replace("\u0A38\u0A3C", "\u0A36");
            var parts = pre.Split(' ').Select(p => Build.ByTruth(p, byU)).ToList();
            (string Hex, string Split)? exp = parts.Any(p => p is null) ? null
                : (string.Join(" 20 ", parts.Select(p => p!.Value.Hex)), string.Join(" ", parts.Select(p => p!.Value.Split)));
            if (exp is null) { none++; Console.WriteLine($"  {w}\tno split into truth strings\tours {Hex(conv.ConvertText(w))}"); continue; }
            n++;
            string got = Hex(conv.ConvertText(w));
            if (got == exp.Value.Hex) ok++;
            else Console.WriteLine($"  {w}\t{exp.Value.Split}\twant {exp.Value.Hex}\tgot {got}");
        }
        Console.WriteLine($"common words: {ok}/{n} exact (Shree's syllable spellings joined), {none} with no split");
        break;
    }
    case "learned":
    {
        var fam = Fam.Get(Arg(1, "shree-pun7"));
        var truth = fam.Truth(Load());
        var learner = new Learner(fam);
        var log = new List<string>();
        var pairs = learner.Learn(truth, out var stats, log);
        Console.WriteLine(stats);
        var conv = new FontConverter(learner.Build(pairs));
        Console.WriteLine(Checks.Truth(conv, truth, out var tf));
        foreach (var f in tf.Take(20)) Console.WriteLine("  " + f);
        Console.WriteLine(Checks.Words(conv, truth, 20000, 7, out var wf));
        foreach (var f in wf.Take(20)) Console.WriteLine("  " + f);
        foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(2, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "cv":
    {
        var fam = Fam.Get(Arg(1, "shree-pun7"));
        var truth = fam.Truth(Load());
        int folds = int.Parse(Arg(2, "10")), seed = int.Parse(Arg(3, "1")), show = int.Parse(Arg(4, "20"));
        var only = args.Length > 5 ? new System.Text.RegularExpressions.Regex(args[5]) : null;
        Console.WriteLine(CrossCheck.Run(fam, truth, folds, seed, show, only));
        break;
    }
    case "build":
    {
        Build.All(Load(), Arg(1, MapDir), args.Skip(2).ToArray());
        break;
    }
    case "verify":
    {
        // Regenerate into a temp folder and compare byte for byte with what the repo holds.
        string tmp = Path.Combine(Path.GetTempPath(), "puntruth-verify-" + Environment.ProcessId);
        Build.All(Load(), tmp, Array.Empty<string>());
        int diff = 0;
        foreach (var fam in Fam.All)
            foreach (var name in new[] { $"unicode-to-{fam.Id}.json", $"{fam.Id}-to-unicode.json" })
            {
                bool same = File.ReadAllBytes(Path.Combine(tmp, name)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(MapDir, name)));
                Console.WriteLine($"verify {name}: {(same ? "byte-identical" : "DIFFERS")}");
                if (!same) diff++;
            }
        bool limitsSame = File.ReadAllBytes(Path.Combine(tmp, "encoding-limits.json")).AsSpan()
            .SequenceEqual(File.ReadAllBytes(Path.Combine(Root, "assets", "config", "encoding-limits.json")));
        Console.WriteLine($"verify encoding-limits.json: {(limitsSame ? "byte-identical" : "DIFFERS")}");
        if (!limitsSame) diff++;
        Directory.Delete(tmp, true);
        return diff == 0 ? 0 : 2;
    }
    case "back-learned":
    {
        var fam = Fam.Get(Arg(1, "shree-pun7"));
        var truth = fam.Truth(Load());
        var learner = new Learner(fam);
        var conv = new FontConverter(learner.Build(learner.Learn(truth, out _)));
        var reader = new Reader(fam);
        var log = new List<string>();
        reader.Learn(truth, conv, out var stats, log);
        Console.WriteLine(stats);
        foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(2, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "reverse":
    {
        // reverse <table> [fam] [n]: a reverse table (the shipped one, or ours) over the truth, the accepted spellings and the capture.
        var fam = Fam.Get(Arg(2, "shree-pun7"));
        var truth = fam.Truth(Load());
        var rev = new FontConverter(LoadTable(Arg(1, $"{fam.Id}-to-unicode.json")));
        string revPath = File.Exists(Arg(1, "")) ? Arg(1, "") : Path.Combine(MapDir, $"{fam.Id}-to-unicode.json");
        // the forward table beside the reverse one (the same build)
        var fwd = new FontConverter(LoadTable(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(revPath))!, $"unicode-to-{fam.Id}.json")));
        int show = int.Parse(Arg(3, "10"));
        Console.WriteLine(Reader.Check(rev, fwd, truth, out var rf));
        foreach (var f in rf.Take(show)) Console.WriteLine("  " + f);
        Console.WriteLine(Reader.CheckAccepted(rev, fwd, truth, out var ra));
        foreach (var f in ra.Take(show)) Console.WriteLine("  " + f);
        foreach (int seed in new[] { 7, 11 }) Console.WriteLine(Reader.CheckWords(rev, fwd, truth, 20000, seed, out _));
        Console.WriteLine(Reader.CheckWellFormed(rev, truth, out var wfm));
        foreach (var f in wfm.Take(show)) Console.WriteLine("  " + f);
        if (File.Exists(GroundTruth))
        {
            Console.WriteLine(Reader.CheckCapture(rev, fwd, Capture.Spellings(GroundTruth).Select(x => (x.U, x.Hex)), out var cf));
            foreach (var g in cf.GroupBy(f => f.Split('\t')[0]))
            {
                Console.WriteLine($"  {g.Key}: {g.Count()}");
                foreach (var f in g.Take(show)) Console.WriteLine("    " + f);
            }
        }
        break;
    }
    case "read":
    {
        var fam = Fam.Get(args[1]);
        string dir = args[2] == "-" ? MapDir : args[2];
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
        foreach (var hex in args[3].Split(';'))
        {
            string u = rev.ConvertText(FromHex(hex.Trim()));
            Console.WriteLine($"{hex.Trim()}\t{u}\t[{Cps(u)}]\t-> {Hex(fwd.ConvertText(u))}\t{string.Join("; ", Malformed(u))}");
        }
        break;
    }
    case "fontcheck":
    {
        string dir = Arg(1, MapDir);
        foreach (var fam in Fam.All)
        {
            string path = Path.Combine(dir, $"unicode-to-{fam.Id}.json");
            if (!File.Exists(path)) { Console.WriteLine($"{fam.Id}: no table in {dir}"); continue; }
            var table = LoadTable(path);
            var truth = fam.Truth(Load());
            var written = Fonts.Written(table, new FontConverter(table), truth.Select(t => t.U).Concat(Build.RealWords));
            var bad = Fonts.Check(fam, written);
            Console.WriteLine($"{fam.Id}: {written.Count} code points written, faces {Fonts.Faces(fam).Count} ({string.Join(" ", Fonts.Faces(fam).Select(Path.GetFileName))}), faces missing one: {bad.Count}");
            foreach (var b in bad) Console.WriteLine("    " + b);
        }
        break;
    }
    case "cmaps":
    {
        Fonts.WriteFixture(Arg(1, Fonts.CmapFixture));
        break;
    }
    case "wordfinal":
    {
        var rows = Load();
        var byU = rows.ToDictionary(t => t.U, StringComparer.Ordinal);
        int finals = 0, changed = 0, failed = 0;
        foreach (var t in rows.Where(t => t.U.Length > 1 && IsVirama(t.U[^1])))
        {
            finals++;
            try
            {
                if (WordFinal.Spell(t, byU, '੍', '਼', "B5", new[] { "BE" }) is { } a)
                {
                    changed++;
                    if (changed <= int.Parse(Arg(1, "20"))) Console.WriteLine($"  {t.U}\t{t.Pref} -> {a.Pref}\t{a.Why}");
                }
            }
            catch (InvalidOperationException e) { failed++; if (failed <= 10) Console.WriteLine($"  {t.U}\t{t.Pref}\tcannot: {e.Message}"); }
        }
        Console.WriteLine($"rows ending in a virama {finals}: the composer's spelling already the visible halant {finals - changed - failed}, written otherwise {changed}, cannot say {failed}");
        break;
    }
    case "oldread":
    {
        // oldread <oldForwardTable> <dir|-> [n]: what an earlier forward table wrote (for the truth and 20000 words)
        // read back by each family's reverse table now (P5: a document written before reads as its text).
        var old = new FontConverter(LoadTable(args[1]));
        var truthAll = Load();
        foreach (var fam in Fam.All)
        {
            var truth = fam.Truth(truthAll);
            string dir = Arg(2, "-") == "-" ? MapDir : args[2];
            var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
            var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
            int n = 0, exact = 0, same = 0, latin = 0, malformed = 0, other = 0;
            var bad = new List<string>();
            var items = truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U));
            foreach (var u in items)
            {
                var w = old.Convert(u);
                if (w.HasUnmapped) continue;
                n++;
                string got = rev.ConvertText(w.Text);
                var mal = Malformed(got);
                if (got == u) exact++;
                else if (mal.Any(b => b.StartsWith("Latin") || b.StartsWith("C1") || b.StartsWith("control") || b.StartsWith("noncharacter")))
                { latin++; bad.Add($"LATIN\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]"); }
                else if (mal.Count > 0 && Malformed(u).Count == 0) { malformed++; bad.Add($"MALFORMED\t{u}\told {Hex(w.Text)}\tread {got}\t{string.Join("; ", mal)}"); }
                else if (Hex(fwd.ConvertText(got)) == Hex(fwd.ConvertText(u)) || Hex(fwd.ConvertText(got)) == Hex(w.Text)) same++;
                else { other++; bad.Add($"OTHER\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]"); }
            }
            Console.WriteLine($"{fam.Id}: earlier table's spellings read back {n}: exact {exact}, same text {same}, Latin/C1 {latin}, malformed {malformed}, other text {other}");
            foreach (var g in bad.GroupBy(b => b.Split('\t')[0]))
                foreach (var b in g.Take(int.Parse(Arg(3, "8")))) Console.WriteLine("    " + b);
        }
        break;
    }
    case "convert":
    {
        var fam = Fam.Get(args[1]);
        var conv = new FontConverter(LoadTable($"unicode-to-{fam.Id}.json"));
        var read = new FontConverter(LoadTable($"{fam.Id}-to-unicode.json"));
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
    default:
        Console.Error.WriteLine("unknown mode " + mode);
        return 1;
}
return 0;
