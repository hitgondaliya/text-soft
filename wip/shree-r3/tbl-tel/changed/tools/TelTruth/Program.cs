using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// TelTruth - build and check the Shree-Lipi Telugu tables against what Shree's own composers write
// (tests/FontSoftware.Tests/Data/shree-tel7-truth.json: the Unicode strings and the bytes S8Tel764 types for each).
//
// The tables are rules plus pairs. The rules (Rules.cs) carry what the composer does everywhere: a sign on a cluster on its
// first letter with the vattu after it, ృ after the vattu behind the spacer A0, ై as the e-shape + 4F (95 before a wide
// vattu), the ra-vattu hook before its cluster (5B with ై; the combined 88 89 after స ష), క్ష as one letter with its vattu
// behind a spacer, the visible halant for a virama with no consonant after it. The learner (Learner.cs) then adds a pair
// only where composing from smaller pieces gives the wrong bytes, so a syllable outside the truth is still built the
// composer's way. One table pair per font family (Family.cs: the 7 and 4/5/6 Apoorva/Asmitha faces are one design; the
// Shree-Tel7-001/002 faces draw a star at 92 where the others draw ₹).
//
// Text that is not Telugu follows tools/Shared/ShreeNonIndic.cs (and Ascii.cs for ASCII): punctuation the fonts draw is
// written as its look-alike, any other character whose code the fonts map is reported as unmapped and left out, a
// character no code draws passes through as itself, reported.
//
//   teltruth prep                          the capture: rows, codes written, the documented exceptions
//   teltruth fixture                       write the test fixture from the capture
//   teltruth build [outDir] [fam...]       learn and check every family's tables, write them (default assets/config/mappings,
//                                          and the families' lines of assets/config/encoding-limits.json)
//   teltruth verify                        build into a temp folder and compare byte for byte with the tables in the repo
//   teltruth measure <table> [fam] [show]  truth strings through a table (default family shree-tel7)
//   teltruth words <table> [fam] [n] [seed]
//   teltruth common <table> [fam]          the capture's common words against the composer's syllables joined
//   teltruth learned [fam] [n]             which strings needed a pair of their own, by shape
//   teltruth cv [fam] [folds] [seed] [show] [shapeRegex]   hold-out: learn without some strings, convert them
//   teltruth back-learned [fam] [n]        the reverse pairs, by shape
//   teltruth reverse <table> [fam] [n]     a reverse table over the truth, the accepted spellings and the whole capture
//   teltruth read <fam> <dir|-> <hex;hex;...>   legacy bytes through the family's reverse table (and forward again)
//   teltruth fontcheck [dir]               every code point each forward table in dir writes, against every installed face's cmap
//   teltruth cmaps [out]                   write the faces' cmaps (the test fixture Data/shree-tel-cmaps.json)
//   teltruth keys                          write the key classes of Shree's 14 fixed keyboards (the test fixture Data/shree-tel-keys.json)
//   teltruth oldread <oldForwardTable> <dir|-> [n]   what an earlier forward table wrote, read back by the reverse tables now
//   teltruth convert <fam> <in> <out>      words to bytes and back

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "prep";
string Arg(int i, string def) => args.Length > i ? args[i] : def;

static List<Truth> Load() => File.Exists(Fixture) ? LoadTruth(Fixture) : Capture.Read(GroundTruth).Rows;

switch (mode)
{
    case "prep":
    {
        var cap = Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]);
        var rows = cap.Rows;
        Console.WriteLine($"truth rows {rows.Count}, with more than one accepted spelling {rows.Count(r => r.Acc.Length > 1)}");
        foreach (var (k, v) in cap.Counts) Console.WriteLine($"  {k}: {v}");
        foreach (var (u, c, w) in cap.Removed.Take(40)) Console.WriteLine($"  removed {u} [{Cps(u)}] {c}: {w}");
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
        var fam = Fam.Get(Arg(2, "shree-tel7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine(Checks.Truth(conv, truth, out var fails));
        Console.WriteLine(Checks.ComposerBytes(conv, truth));
        foreach (var g in fails.GroupBy(f => Shape(f.Split('\t')[0])).OrderByDescending(g => g.Count()).Take(40))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var f in g.Take(int.Parse(Arg(3, "4")))) Console.WriteLine("      " + f);
        }
        foreach (int seed in new[] { 7, 11 }) Console.WriteLine(Checks.Words(conv, truth, 20000, seed, out _));
        foreach (int seed in new[] { 7 }) Console.WriteLine(Checks.WordFinal(conv, truth, 20000, seed, out _));
        break;
    }
    case "words":
    {
        var fam = Fam.Get(Arg(2, "shree-tel7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine(Checks.Words(conv, truth, int.Parse(Arg(3, "20000")), int.Parse(Arg(4, "7")), out var fails));
        foreach (var f in fails.Take(40)) Console.WriteLine("  " + f);
        break;
    }
    case "common":
    {
        var fam = Fam.Get(Arg(2, "shree-tel7"));
        var truth = fam.Truth(Load());
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        string file = Path.Combine(Path.GetDirectoryName(GroundTruth)!, "common-words.txt");
        int n = 0, ok = 0, none = 0;
        var noSplit = new List<string>();
        foreach (var w in File.ReadAllLines(file, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var parts = w.Split(' ').Select(p => Build.ByTruth(p, byU)).ToList();
            (string Hex, string Split)? exp = parts.Any(p => p is null) ? null
                : (string.Join(" 20 ", parts.Select(p => p!.Value.Hex)), string.Join(" ", parts.Select(p => p!.Value.Split)));
            if (exp is null) { none++; noSplit.Add($"  {w}\tno split into truth strings\tours {Hex(conv.ConvertText(w))}"); continue; }
            n++;
            string got = Hex(conv.ConvertText(w));
            if (got == exp.Value.Hex) ok++;
            else Console.WriteLine($"  {w}\t{exp.Value.Split}\twant {exp.Value.Hex}\tgot {got}");
        }
        foreach (var l in noSplit) Console.WriteLine(l);
        Console.WriteLine($"common words: {ok}/{n} exact (Shree's syllable spellings joined), {none} with no split");
        break;
    }
    case "learned":
    {
        var fam = Fam.Get(Arg(1, "shree-tel7"));
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
        var fam = Fam.Get(Arg(1, "shree-tel7"));
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
        string tmp = Path.Combine(Path.GetTempPath(), "teltruth-verify-" + Environment.ProcessId);
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
        var fam = Fam.Get(Arg(1, "shree-tel7"));
        var truth = fam.Truth(Load());
        var learner = new Learner(fam);
        var conv = new FontConverter(learner.Build(learner.Learn(truth, out _)));
        var reader = new Reader(fam);
        var log = new List<string>();
        reader.Learn(truth, conv, Fonts.Codes(fam), out var stats, log);
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
        var fam = Fam.Get(Arg(2, "shree-tel7"));
        var truth = fam.Truth(Load());
        var rev = new FontConverter(LoadTable(Arg(1, $"{fam.Id}-to-unicode.json")));
        string revPath = File.Exists(Arg(1, "")) ? Arg(1, "") : Path.Combine(MapDir, $"{fam.Id}-to-unicode.json");
        var fwd = new FontConverter(LoadTable(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(revPath))!, $"unicode-to-{fam.Id}.json")));
        int show = int.Parse(Arg(3, "10"));
        Console.WriteLine(Reader.Check(rev, fwd, truth, out var rf));
        foreach (var f in rf.Take(show)) Console.WriteLine("  " + f);
        Console.WriteLine(Reader.CheckAccepted(rev, fwd, truth, out var ra));
        foreach (var f in ra.Take(show)) Console.WriteLine("  " + f);
        foreach (int seed in new[] { 7, 11 }) Console.WriteLine(Reader.CheckWords(rev, fwd, truth, 20000, seed, out _));
        Console.WriteLine(Reader.CheckWellFormed(rev, truth, out var wfm));
        foreach (var f in wfm.Take(show)) Console.WriteLine("  " + f);
        Console.WriteLine(Reader.CheckAvagraha(rev, fwd, truth, out var avf));
        foreach (var g in avf.GroupBy(f => f.Split('\t')[0])) foreach (var f in g.Take(show)) Console.WriteLine("  " + f);
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
    case "keys":
    {
        KeyFixture.Write();
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
