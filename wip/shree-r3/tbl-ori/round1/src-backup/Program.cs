using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// OriTruth - build and check the Shree-Lipi Odia tables against what Shree's own composers write
// (tests/FontSoftware.Tests/Data/shree-ori7-truth.json: 41,470 Unicode strings and the bytes S8Ori764 types for each).
//
// The tables are rules plus pairs. The rules (Rules.cs) carry what the composer does everywhere: the reph after its
// syllable and before ଂ ଁ ଃ, the e-sign before its cluster and ୈ ୋ ୌ split around it. The learner (Learner.cs) starts
// from the characters, the subjoined forms and what follows a hanging one (read off the truth), and adds a pair only
// where composing from smaller pieces gives the wrong bytes, so a syllable outside the truth is still built the
// composer's way. One table pair serves both font families (Family.cs: the 7 faces and the 4/5/6 faces are the same
// design code for code; ₹, which only WORI7001/2 draw, is passed through).
//
// Text that is not Odia follows tools/Shared/ShreeNonIndic.cs (and Ascii.cs for ASCII): punctuation the fonts draw is
// written as its look-alike, any other character whose code the fonts map is reported as unmapped and left out, and a
// character no code of the fonts draws is passed through as itself, reported (P3b).
//
//   oritruth prep                          the capture: rows, codes written, accepted spellings
//   oritruth fixture                       write the test fixture from the capture
//   oritruth build [outDir]                learn and check the tables, write them (default assets/config/mappings,
//                                          and the shree-ori7 line of assets/config/encoding-limits.json)
//   oritruth verify                        build into a temp folder and compare byte for byte with the tables in the repo
//   oritruth measure <table> [raw] [show]  truth strings through a forward table (raw: against the composer's raw bytes)
//   oritruth words <table> [n] [seed]
//   oritruth common <table>                the capture's common words against the composer's syllables joined
//   oritruth learned [n]                   which strings needed a pair of their own, by shape
//   oritruth cv [folds] [seed] [show] [shapeRegex]   hold-out: learn without some strings, convert them
//   oritruth back-learned [n]              the reverse pairs, by shape
//   oritruth reverse <table> [n]           a reverse table over the truth, the accepted spellings and the whole capture
//   oritruth read <dir|-> <hex;hex;...>    legacy bytes through the reverse table (and forward again)
//   oritruth fontcheck [dir]               every code point the forward table in dir writes, against every installed face's cmap
//   oritruth cmaps [out]                   write the faces' cmaps (the test fixture Data/shree-ori-cmaps.json)
//   oritruth wordfinal [n]                 the rows that end in a virama, and what tools/Shared/ShreeWordFinal.cs would write
//   oritruth oldread <oldForwardTable> <dir|-> [n]   what an earlier forward table wrote, read back by the reverse table now
//   oritruth convert <in> <out>            words to bytes and back

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "prep";
string Arg(int i, string def) => args.Length > i ? args[i] : def;
var fam = Fam.Ori7;

static List<Truth> Load() => File.Exists(Fixture) ? LoadTruth(Fixture) : Capture.Read(GroundTruth);

switch (mode)
{
    case "prep":
    {
        var rows = Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]);
        Console.WriteLine($"truth rows {rows.Count}, with more than one accepted spelling {rows.Count(r => r.Acc.Length > 1)}");
        var codes = rows.SelectMany(r => r.Acc).SelectMany(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(c => c).ToList();
        Console.WriteLine($"codes written ({codes.Count}): {string.Join(" ", codes)}");
        Console.WriteLine($"rows whose raw spelling holds 9C (ନ): {rows.Count(r => r.Pref.Split(' ').Contains("9C"))}");
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
        // measure <table> [raw] [show]: a forward table against the truth as the table must write it, or (raw) against the
        // composer's raw bytes (the capture agent's measure: ନ 9C, ₹ 3C, ୰ 8F, ଽ 90).
        bool raw = Arg(2, "") == "raw";
        var truth = raw ? Capture.Read(GroundTruth) : fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine((raw ? "RAW composer bytes: " : "") + Checks.Truth(conv, truth, out var fails));
        int noZw = truth.Count(t => !t.U.Any(IsJoiner)), noZwOk = truth.Count(t => !t.U.Any(IsJoiner) && Hex(conv.ConvertText(t.U)) == t.Pref);
        Console.WriteLine($"  without ZWJ/ZWNJ: {noZwOk}/{noZw} = {100.0 * noZwOk / noZw:F2}%");
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
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        Console.WriteLine(Checks.Words(conv, truth, int.Parse(Arg(2, "20000")), int.Parse(Arg(3, "7")), out var fails));
        foreach (var f in fails.Take(40)) Console.WriteLine("  " + f);
        break;
    }
    case "common":
    {
        var truth = fam.Truth(Load());
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var conv = new FontConverter(LoadTable(Arg(1, $"unicode-to-{fam.Id}.json")));
        string file = Path.Combine(Path.GetDirectoryName(GroundTruth)!, "common-words.txt");
        int n = 0, ok = 0, none = 0;
        foreach (var w in File.ReadAllLines(file, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            // the truth holds the nukta letters precomposed, as the composer writes them; words are split at spaces
            string pre = w.Replace("ଡ଼", "ଡ଼").Replace("ଢ଼", "ଢ଼");
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
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "atoms":
    {
        var truth = fam.Truth(Load());
        foreach (var (u, a) in new Learner(fam).Atoms(truth).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {Rules.Show(u)}\t[{Cps(u)}]\t{Hex(a.Replace(Rules.Keep, ""))}");
        break;
    }
    case "cv":
    {
        var truth = fam.Truth(Load());
        int folds = int.Parse(Arg(1, "10")), seed = int.Parse(Arg(2, "1")), show = int.Parse(Arg(3, "20"));
        var only = args.Length > 4 ? new System.Text.RegularExpressions.Regex(args[4]) : null;
        Console.WriteLine(CrossCheck.Run(fam, truth, folds, seed, show, only));
        break;
    }
    case "build":
    {
        Build.All(Load(), Arg(1, MapDir), Array.Empty<string>());
        break;
    }
    case "verify":
    {
        // Regenerate into a temp folder and compare byte for byte with what the repo holds.
        string tmp = Path.Combine(Path.GetTempPath(), "oritruth-verify-" + Environment.ProcessId);
        Build.All(Load(), tmp, Array.Empty<string>());
        int diff = 0;
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
        var truth = fam.Truth(Load());
        var fwd = new FontConverter(Build.Forward(fam, truth, out _, out _));
        var reader = new Reader(fam);
        var log = new List<string>();
        reader.Learn(truth, fwd, out var stats, log);
        Console.WriteLine(stats);
        foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b);
        }
        break;
    }
    case "reverse":
    {
        // reverse <table> [n]: a reverse table (the shipped one, or ours) over the truth, the accepted spellings and the capture.
        var truth = fam.Truth(Load());
        var rev = new FontConverter(LoadTable(Arg(1, $"{fam.Id}-to-unicode.json")));
        string revPath = File.Exists(Arg(1, "")) ? Arg(1, "") : Path.Combine(MapDir, $"{fam.Id}-to-unicode.json");
        var fwd = new FontConverter(LoadTable(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(revPath))!, $"unicode-to-{fam.Id}.json")));
        int show = int.Parse(Arg(2, "10"));
        Console.WriteLine(Reader.Check(rev, fwd, truth, out var rf));
        foreach (var f in rf.Take(show)) Console.WriteLine("  " + f);
        Console.WriteLine(Reader.CheckAccepted(rev, fwd, truth, out var ra));
        foreach (var f in ra.Take(show)) Console.WriteLine("  " + f);
        foreach (int seed in new[] { 7, 11 }) Console.WriteLine(Reader.CheckWords(rev, fwd, truth, 20000, seed, out _));
        Console.WriteLine(Reader.CheckWellFormed(rev, truth, out var wfm));
        foreach (var f in wfm.Take(show)) Console.WriteLine("  " + f);
        if (File.Exists(GroundTruth))
        {
            Console.WriteLine(Reader.CheckCapture(rev, fwd, Capture.Spellings(GroundTruth).Select(x => (x.U, fam.Doc(x.Hex))).Distinct(), out var cf));
            foreach (var g in cf.GroupBy(f => f.Split('\t')[0]))
            {
                Console.WriteLine($"  {g.Key}: {g.Count()}");
                foreach (var f in g.Take(show)) Console.WriteLine("    " + f);
            }
        }
        break;
    }
    case "capture-kinds":
    {
        // capture-kinds [dir] [show]: every composer spelling of the capture read back, by the kind of capture row.
        string dir = Arg(1, "-") == "-" ? MapDir : args[1];
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
        int show = int.Parse(Arg(2, "3"));
        var all = Capture.Spellings(GroundTruth).Select(x => (x.U, Hex: fam.Doc(x.Hex), Kind: x.Kind.Split(new[] { " (", ": U " }, StringSplitOptions.None)[0]))
            .GroupBy(x => (x.U, x.Hex)).Select(g => g.First()).ToList();
        foreach (var g in all.GroupBy(x => x.Kind).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine(Reader.CheckCapture(rev, fwd, g.Select(x => (x.U, x.Hex)), out var cf).Replace("composer spellings of the whole capture", g.Key));
            foreach (var f in cf.Take(show)) Console.WriteLine("    " + f);
        }
        break;
    }
    case "read":
    {
        string dir = args[1] == "-" ? MapDir : args[1];
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
        foreach (var hex in args[2].Split(';'))
        {
            string u = rev.ConvertText(FromHex(hex.Trim()));
            Console.WriteLine($"{hex.Trim()}\t{u}\t[{Cps(u)}]\t-> {Hex(fwd.ConvertText(u))}\t{string.Join("; ", Malformed(u))}");
        }
        break;
    }
    case "fontcheck":
    {
        string dir = Arg(1, MapDir);
        string path = Path.Combine(dir, $"unicode-to-{fam.Id}.json");
        var table = LoadTable(path);
        var truth = fam.Truth(Load());
        var written = Fonts.Written(table, new FontConverter(table), truth.Select(t => t.U).Concat(Build.RealWords));
        foreach (var (label, pattern, _) in fam.FaceSets)
        {
            var faces = Fonts.Faces(pattern);
            var bad = new List<string>();
            foreach (var face in faces)
            {
                var cmap = Fonts.Cmap(face);
                var missing = written.Where(c => !cmap.ContainsKey(c) && !char.IsWhiteSpace((char)c)).ToList();
                if (missing.Count > 0) bad.Add($"{Path.GetFileName(face)}: no glyph for {string.Join(" ", missing.Select(c => $"U+{c:X4}"))}");
            }
            Console.WriteLine($"{label}: {written.Count} code points written, faces {faces.Count} ({string.Join(" ", faces.Select(Path.GetFileName))}), faces missing one: {bad.Count}");
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
        var rows = File.Exists(GroundTruth) ? Capture.Read(GroundTruth) : Load();
        var byU = rows.ToDictionary(t => t.U, StringComparer.Ordinal);
        int finals = 0, changed = 0;
        foreach (var t in rows.Where(t => t.U.Length > 1 && IsVirama(t.U[^1])))
        {
            finals++;
            if (Capture.WordFinalSpelling(t, byU) is { } a)
            {
                changed++;
                if (changed <= int.Parse(Arg(1, "20"))) Console.WriteLine($"  {t.U}\t{t.Pref} -> {a.Pref}\t{a.Why}");
            }
        }
        Console.WriteLine($"rows ending in a virama {finals}: the composer's spelling already the visible virama {finals - changed}, written otherwise {changed}; " +
                          $"with FA {rows.Count(t => t.U.Length > 1 && IsVirama(t.U[^1]) && t.Pref.Split(' ').Contains("FA"))}");
        break;
    }
    case "oldread":
    {
        // oldread <oldForwardTable> <dir|-> [n]: what an earlier forward table wrote (for the truth and 20000 words), read
        // back by the reverse table now (P5: a document written before reads as its text).
        var old = new FontConverter(LoadTable(args[1]));
        var truth = fam.Truth(Load());
        string dir = Arg(2, "-") == "-" ? MapDir : args[2];
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
        int n = 0, exact = 0, same = 0, latin = 0, malformed = 0, other = 0, skipped = 0;
        var bad = new List<string>();
        var items = truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U));
        foreach (var u in items)
        {
            var w = old.Convert(u);
            if (w.HasUnmapped) { skipped++; continue; }
            n++;
            string got = rev.ConvertText(w.Text);
            var mal = Malformed(got);
            if (got == u) exact++;
            else if (mal.Any(b => b.StartsWith("Latin") || b.StartsWith("C1") || b.StartsWith("control") || b.StartsWith("noncharacter")))
            { latin++; bad.Add($"LATIN\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]"); }
            else if (mal.Count > 0 && Malformed(u).Count == 0) { malformed++; bad.Add($"MALFORMED\t{u}\told {Hex(w.Text)}\tread {got}\t{string.Join("; ", mal)}"); }
            else if (Hex(fwd.ConvertText(got)) == Hex(fwd.ConvertText(u))) same++;
            else { other++; bad.Add($"OTHER\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]"); }
        }
        Console.WriteLine($"earlier table's spellings read back {n} (skipped {skipped} it reported unmapped): exact {exact}, same text {same}, Latin/C1 {latin}, malformed {malformed}, other text {other}");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]))
            foreach (var b in g.Take(int.Parse(Arg(3, "8")))) Console.WriteLine("    " + b);
        break;
    }
    case "convert":
    {
        var conv = new FontConverter(LoadTable($"unicode-to-{fam.Id}.json"));
        var read = new FontConverter(LoadTable($"{fam.Id}-to-unicode.json"));
        var lines = new List<string>();
        foreach (var w in File.ReadAllLines(args[1], Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var r = conv.Convert(w);
            string again = read.ConvertText(r.Text);
            lines.Add($"{w}\t{Hex(r.Text)}\t{(again == w ? "=" : again)}\t{string.Join("", r.UnmappedCharacters)}");
        }
        File.WriteAllLines(args[2], lines, new UTF8Encoding(false));
        foreach (var l in lines) Console.WriteLine(l);
        break;
    }
    default:
        Console.Error.WriteLine("unknown mode " + mode);
        return 1;
}
return 0;
