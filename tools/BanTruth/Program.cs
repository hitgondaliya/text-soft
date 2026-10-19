using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// BanTruth - build and check the Shree-Lipi Bangla / Assamese tables against what Shree's
// own composers write (tests/FontSoftware.Tests/Data/shree-ban7-truth.json: the Unicode
// strings and the codes S8Ban764 types for each, moved to the codes the fonts draw).
//
// The tables are rules plus pairs. The rules (Rules.cs) carry what the composer does
// everywhere: the pre-base signs first, the reph after the letter and its signs, the signs
// below a letter before its spacer, the ya-phala. The learner (Learner.cs) then adds a pair
// only where composing from smaller pieces gives the wrong bytes - the ligatures, the ra-phala
// forms, the composer's own u/uu shapes - so a syllable outside the truth is still built the
// composer's way. One table pair per font family (Family.cs).
//
//   bantruth prep                          read the capture: truth rows and what is left out, and why
//   bantruth fixture                       write the test fixture from the capture
//   bantruth build [outDir]                learn and check every family's tables, write them
//   bantruth measure <table> [fam] [show]  truth strings through a table (default family shree-ban7)
//   bantruth words <table> [fam] [n] [seed]
//   bantruth learned [fam] [n]             which strings needed a pair of their own, by shape
//   bantruth cv [fam] [folds] [seed] [show] [shapeRegex]   hold-out: learn without some strings, convert them
//   bantruth back-learned [fam] [n]        the reverse pairs, by shape
//   bantruth back-same <fam> [n]           truth spellings the written reverse table reads as other text (same bytes)
//   bantruth convert <fam> <in> <out>      words to bytes and back
//   bantruth read <fam> <dir|-> <hex;hex;...>   legacy bytes through the family's reverse table (and forward again)
//   bantruth fwd <fam> <dir|-> <text;text;...>  Unicode through the family's forward table, what it reports, and back
//   bantruth syllables <dir|-> [n]          every truth cluster x vowel signs x ঁ ং ঃ x reph, written and read back
//   bantruth capture <fam> [n] [dir]        every composer spelling of the capture read back (no Latin/ASCII/C1, well formed)
//   bantruth rawtruth [n]                   the app's own converters against every raw composer row, byte for byte
//   bantruth fontcheck [dir]              every code point each forward table in dir writes, against every installed face's cmap
//   bantruth cmaps [out]                   write the faces' cmaps (the test fixture Data/shree-ban-cmaps.json)
//   bantruth decisions [n]                 the truth rows Decisions.cs changes, by kind

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "prep";
string Arg(int i, string def) => args.Length > i ? args[i] : def;

static List<Truth> Load() => File.Exists(Fixture) ? LoadTruth(Fixture) : Capture.Read(GroundTruth).Rows;

switch (mode)
{
    case "prep":
    {
        var (rows, left) = Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]);
        Console.WriteLine($"truth rows {rows.Count}, left out {left.Count}");
        foreach (var g in left.GroupBy(l => l.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var l in g.Take(int.Parse(Arg(2, "12")))) Console.WriteLine("      " + l.Substring(l.IndexOf('\t') + 1));
        }
        foreach (var g in Capture.Log.GroupBy(l => l.Split('\t')[0]))
            Console.WriteLine($"  decision {g.Key}: {g.Count()}");
        var codes = rows.SelectMany(r => r.Acc).SelectMany(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().OrderBy(c => c).ToList();
        Console.WriteLine($"codes written ({codes.Count}): {string.Join(" ", codes)}");
        break;
    }
    case "fixture":
    {
        var (rows, left) = Capture.Read(Arg(1, "-") == "-" ? GroundTruth : args[1]);
        Fixtures.Write(rows, left);
        break;
    }
    case "measure":
    {
        var fam = Fam.Get(Arg(2, "shree-ban7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, "unicode-to-shree-ban7.json")));
        var r = Checks.Truth(conv, truth, out var fails);
        Console.WriteLine(r);
        foreach (var g in fails.GroupBy(f => Shape(f.Split('\t')[0])).OrderByDescending(g => g.Count()).Take(40))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var f in g.Take(int.Parse(Arg(3, "4")))) Console.WriteLine("      " + f);
        }
        Console.WriteLine(Checks.Words(conv, truth, 20000, 7, out _));
        break;
    }
    case "words":
    {
        var fam = Fam.Get(Arg(2, "shree-ban7"));
        var truth = fam.Truth(Load());
        var conv = new FontConverter(LoadTable(Arg(1, "unicode-to-shree-ban7.json")));
        Console.WriteLine(Checks.Words(conv, truth, int.Parse(Arg(3, "20000")), int.Parse(Arg(4, "7")), out var fails));
        foreach (var f in fails.Take(40)) Console.WriteLine("  " + f);
        break;
    }
    case "learned":
    {
        var fam = Fam.Get(Arg(1, "shree-ban7"));
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
        var fam = Fam.Get(Arg(1, "shree-ban7"));
        var truth = fam.Truth(Load());
        int folds = int.Parse(Arg(2, "10")), seed = int.Parse(Arg(3, "1")), show = int.Parse(Arg(4, "20"));
        var only = args.Length > 5 ? new System.Text.RegularExpressions.Regex(args[5]) : null;
        Console.WriteLine(CrossCheck.Run(fam, truth, folds, seed, show, only));
        break;
    }
    case "build":
    {
        string outDir = Arg(1, MapDir);
        Build.All(Load(), outDir, args.Skip(2).ToArray());
        break;
    }
    case "back-learned":
    {
        var fam = Fam.Get(Arg(1, "shree-ban7"));
        var truth = fam.Truth(Load());
        var learner = new Learner(fam);
        var conv = new FontConverter(learner.Build(learner.Learn(truth, out _)));
        var reader = new Reader(fam, truth);
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
    case "back-same":
    {
        // back-same <fam> [n]: truth spellings the written reverse table reads as other text with the same bytes, by shape.
        var fam = Fam.Get(Arg(1, "shree-ban7"));
        var truth = fam.Truth(Load());
        var fwd = new FontConverter(LoadTable($"unicode-to-{fam.Id}.json"));
        var rev = new FontConverter(LoadTable($"{fam.Id}-to-unicode.json"));
        var diff = new List<string>();
        foreach (var t in truth.Where(t => t.Pref.Length > 0))
        {
            string got = rev.ConvertText(FromHex(t.Pref));
            if (got != t.U) diff.Add($"{Shape(t.U)}\t{t.U}\t{t.Pref}\tread {got}\t{(Reader.Same(got, t.U, t.Pref, fwd) ? "same" : "WRONG")}");
        }
        Console.WriteLine($"{diff.Count} read as other text");
        foreach (var g in diff.GroupBy(d => d.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var d in g.Take(int.Parse(Arg(2, "6")))) Console.WriteLine("      " + d.Substring(d.IndexOf('\t') + 1));
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
            Console.WriteLine($"{hex.Trim()}	{u}	[{Cps(u)}]	-> {Hex(fwd.ConvertText(u))}	{string.Join("; ", Malformed(u))}");
        }
        break;
    }
    case "fwd":
    {
        // fwd <fam> <dir|-> <text;text;...>: Unicode through the family's forward table (and back), with what it reports.
        var fam = Fam.Get(args[1]);
        string dir = args[2] == "-" ? MapDir : args[2];
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        foreach (var u in args[3].Split(';'))
        {
            var r = fwd.Convert(u);
            string back = rev.ConvertText(r.Text);
            Console.WriteLine($"{u} [{Cps(u)}]\t{Hex(r.Text)}\tunmapped [{string.Join(" ", r.UnmappedCharacters.Select(Cps))}]\tback {back} [{Cps(back)}]");
        }
        break;
    }
    case "syllables":
    {
        // syllables <dir|-> [n]: every cluster of the truth x vowel signs x ঁ ং ঃ x reph, written and read back by each
        // family's tables in dir (Reader.CheckSyllables: the text, well formed, no ASCII the text did not have).
        string dir = Arg(1, "-") == "-" ? MapDir : args[1];
        foreach (var fam in Fam.All)
        {
            var fwd = new FontConverter(LoadTable(Path.Combine(dir, $"unicode-to-{fam.Id}.json")));
            var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
            var fails = new List<string>();
            Console.WriteLine($"{fam.Id}: " + Reader.CheckSyllables(rev, fwd, fam.Truth(Load()), fam.Ra, out fails));
            int ascii = fails.Count(f => f.Contains("ASCII"));
            Console.WriteLine($"  of the wrong, with ASCII in the read back: {ascii}");
            foreach (var f in fails.Take(int.Parse(Arg(2, "3")))) Console.WriteLine("    " + f);
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
            Console.WriteLine($"{fam.Id}: {written.Count} code points written, faces {Fonts.Faces(fam).Count}, faces missing one: {bad.Count}");
            foreach (var b in bad) Console.WriteLine("    " + b);
            Console.WriteLine($"  written: {string.Join(" ", written.Select(c => c.ToString("X4")))}");
        }
        break;
    }
    case "cmaps":
    {
        Fonts.WriteFixture(Arg(1, Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-ban-cmaps.json")));
        break;
    }
    case "decisions":
    {
        Capture.Read(GroundTruth);
        foreach (var g in Capture.Log.GroupBy(l => l.Split('\t')[0]))
        {
            Console.WriteLine($"{g.Key}: {g.Count()}");
            foreach (var l in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("    " + l.Substring(l.IndexOf('\t') + 1));
        }
        break;
    }
    case "capture":
    {
        // capture <fam> [n] [dir]: every composer spelling of the capture read back by the family's reverse table.
        var fam = Fam.Get(args[1]);
        string dir = Arg(3, "-") == "-" ? MapDir : args[3];
        var rev = new FontConverter(LoadTable(Path.Combine(dir, $"{fam.Id}-to-unicode.json")));
        var sp = Capture.Spellings(GroundTruth).Select(x => (x.U, x.Hex)).ToList();
        Console.WriteLine(Reader.CheckCapture(rev, fam, sp, out var fails));
        foreach (var f in fails.Take(int.Parse(Arg(2, "40")))) Console.WriteLine("  " + f);
        break;
    }
    case "wordfinal":
    {
        // wordfinal [n]: the rows that end in a hasanta, and what tools/Shared/ShreeWordFinal.cs would write instead.
        var rows = Load();
        var byU = rows.ToDictionary(t => t.U, StringComparer.Ordinal);
        int finals = 0, changed = 0, failed = 0;
        foreach (var t in rows.Where(t => t.U.Length > 1 && IsVirama(t.U[^1])))
        {
            finals++;
            try
            {
                if (WordFinal.Spell(t, byU, '্', '়', "F4", new[] { "A1", "FE" }) is { } a)
                {
                    changed++;
                    if (changed <= int.Parse(Arg(1, "20"))) Console.WriteLine($"  {t.U}\t{t.Pref} -> {a.Pref}\t{a.Why}");
                }
            }
            catch (InvalidOperationException e) { failed++; if (failed <= 10) Console.WriteLine($"  {t.U}\t{t.Pref}\tcannot: {e.Message}"); }
        }
        Console.WriteLine($"rows ending in a hasanta {finals}: the composer's spelling already the visible halant {finals - changed - failed}, written otherwise {changed}, cannot say {failed}");
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
            var unread = new Dictionary<char, int>();
            var bad = new List<string>();
            var items = truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U));
            foreach (var u in items)
            {
                var w = old.Convert(u);
                if (w.HasUnmapped) continue;
                n++;
                string got = rev.ConvertText(w.Text);
                string want = fam.IsAssamese ? u.Replace('র', 'ৰ') : u.Replace('ৰ', 'র');
                var mal = Malformed(got);
                if (got == want) exact++;
                else if (mal.Any(b => b.StartsWith("Latin") || b.StartsWith("C1") || b.StartsWith("noncharacter")))
                {
                    latin++;
                    bad.Add($"LATIN\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]");
                    foreach (char ch in got.Where(ch => ch >= '\u0080' && ch <= 'ɏ' && ch != '×' && ch != '÷')) unread[ch] = unread.GetValueOrDefault(ch) + 1;
                }
                else if (mal.Count > 0 && Malformed(u).Count == 0) { malformed++; bad.Add($"MALFORMED\t{u}\told {Hex(w.Text)}\tread {got}\t{string.Join("; ", mal)}"); }
                else if (Hex(fwd.ConvertText(got)) == Hex(fwd.ConvertText(want))) same++;
                else { other++; bad.Add($"OTHER\t{u}\told {Hex(w.Text)}\tread {got} [{Cps(got)}]"); }
            }
            Console.WriteLine($"{fam.Id}: earlier table's spellings read back {n}: exact {exact}, same text {same}, Latin/C1 {latin}, malformed {malformed}, other text {other}");
            Console.WriteLine($"  codes read as Latin: {string.Join(" ", unread.OrderByDescending(k => k.Value).Select(k => $"{Hex(k.Key.ToString())}x{k.Value}"))}");
            foreach (var g in bad.GroupBy(b => b.Split('\t')[0]))
                foreach (var b in g.Take(int.Parse(Arg(3, "8")))) Console.WriteLine("    " + b);
        }
        break;
    }
    case "enc":
    {
        // What Encoding.GetEncoding(1252) (FileConversion.WriteTxt / ReadTxt) does with the codes around C1.
        _ = Cp1252;   // registers the code pages provider, as the app does
        var e = System.Text.Encoding.GetEncoding(1252);
        foreach (char c in new[] { '\u0081', '\u008D', '\u0090', '\u009E', 'ž', 'ৱ', '•', '“' })
        {
            var b = e.GetBytes(c.ToString());
            Console.WriteLine($"U+{(int)c:X4} -> {BitConverter.ToString(b)} -> U+{(int)e.GetString(b)[0]:X4}");
        }
        foreach (byte b in new byte[] { 0x81, 0x8D, 0x8F, 0x90, 0x9D, 0x9E })
            Console.WriteLine($"byte {b:X2} -> U+{(int)e.GetString(new[] { b })[0]:X4}");
        break;
    }
    case "convert":
    {
        // convert <fam> <words.txt> <out.tsv>: one word per line -> word, hex, round trip.
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
    case "rareads":
    {
        // rareads [n]: every composer spelling of the capture with a ra and its own ra-phala (B9 F8, B9 E7 A1 F8,
        // B9 EA A1 F8 - ক্ৰ্র = 41 F4 A1 B9 F8), read back by each family's reverse table (assets/config).
        var re = new System.Text.RegularExpressions.Regex("(^| )B9 (E7 A1 |EA A1 )?F8( |$)");
        var sp = Capture.Spellings(GroundTruth).Where(x => re.IsMatch(x.Hex)).Select(x => (x.U, x.Hex)).Distinct().ToList();
        foreach (var fam in Fam.All)
        {
            var rev = new FontConverter(LoadTable($"{fam.Id}-to-unicode.json"));
            int bad = 0, n = 0;
            foreach (var (u, hex) in sp)
            {
                string bytes = fam.Doc(hex);
                if (bytes.Length == 0) continue;
                n++;
                string got = rev.ConvertText(FromHex(bytes));
                var m = Malformed(got);
                if (m.Count > 0 || got.Any(c => c is >= '\u0080' and <= 'ɏ' && c != '×' && c != '÷'))
                {
                    bad++;
                    if (bad <= int.Parse(Arg(1, "5"))) Console.WriteLine($"    {u}\t{bytes}\tread {got} [{Cps(got)}]\t{string.Join("; ", m)}");
                }
                else if (n <= 3) Console.WriteLine($"    {u}\t{bytes}\tread {got}");
            }
            Console.WriteLine($"{fam.Id}: composer ra + ra-phala spellings read back {n}: malformed or Latin {bad}");
        }
        break;
    }
    case "rawtruth":
    {
        // rawtruth [n]: the app's own converters (assets/config through ConfigLoader, as TypingEngine and the
        // Converter get them) against every row of the raw composer capture, byte for byte, per family:
        // the composer's preferred bytes, another spelling it accepts, a Decisions.cs row (named), a row the
        // composer itself got wrong (Capture.cs: a letter lost - not compared), or wrong.
        var bundle = new FontSoftware.Core.Config.ConfigLoader(Path.Combine(Root, "assets", "config")).Load();
        var pipeline = new ConversionPipeline(bundle);
        var doc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(GroundTruth, Encoding.UTF8))!["truth"]!.AsObject();
        var (rows, left) = Capture.Read(GroundTruth);
        var fixedPref = rows.ToDictionary(r => r.U, r => r.Pref, StringComparer.Ordinal);
        var decided = Capture.Log.Select(l => l.Split('\t')).GroupBy(p => p[1], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last()[0], StringComparer.Ordinal);
        var bug = left.Select(l => l.Split('\t')).ToDictionary(p => p[1], p => p[0], StringComparer.Ordinal);
        int show = int.Parse(Arg(1, "6"));
        foreach (var fam in Fam.All)
        {
            var fwd = bundle.CreateConverterToLegacy(fam.Id)!;
            var cats = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var samples = new Dictionary<string, List<string>>();
            void Count(string cat, string line)
            {
                cats[cat] = cats.GetValueOrDefault(cat) + 1;
                if (!samples.TryGetValue(cat, out var l)) samples[cat] = l = new();
                if (l.Count < show) l.Add(line);
            }
            int typedSame = 0, n = 0;
            foreach (var (u, node) in doc)
            {
                n++;
                string rawPref = Norm((string)node!["preferred"]!);
                var accRaw = node["accepted"]!.AsArray().Select(x => Norm((string)x!)).Prepend(rawPref).ToList();
                var r = fwd.Convert(u);
                string got = Hex(r.Text);
                // the pipeline (Converter, files) must write the very same
                if (Hex(pipeline.Convert(u, LayoutKey.Unicode, LayoutKey.Legacy(fam.Id)).Text) == got) typedSame++;
                string? Comp(string hex) => Capture.Fix(u, hex, out _) is { } f ? fam.Apply(f) : null;
                string? want = Comp(rawPref);
                if (bug.TryGetValue(u, out var why)) { Count($"composer's own bug row ({why}), not compared", $"{u}\tcomposer {rawPref}\twe {got}"); continue; }
                bool unm = r.HasUnmapped && !(r.UnmappedCharacters.All(x => x == "ৱ") && !fam.IsAssamese);
                if (want is not null && got == want && !unm)
                {
                    if (got.Contains("U+09F1")) Count("= composer preferred, but its ৱ 9E (U+009E, drawn by nothing in Word) passed through as U+09F1, reported", $"{u}\tcomposer {Comp(rawPref)?.Replace("U+09F1", "9E")}\twe {got}");
                    else Count("= composer preferred", "");
                    continue;
                }
                if (!unm && accRaw.Skip(1).Select(Comp).Contains(got)) { Count("= another spelling the composer accepts", $"{u}\tpref {want}\twe {got}"); continue; }
                if (decided.TryGetValue(u, out var kind) && fixedPref.TryGetValue(u, out var fp) && got == fam.Apply(fp) && !unm)
                {
                    string k = kind.StartsWith("rra no twin (reph") ? "rra no twin (a reph: the composer's own র্ spelling, or its pieces)" : kind;
                    Count($"decision: {k}", $"{u}\tcomposer {want}\twe {got}");
                    continue;
                }
                if (want is not null && want.Length == 0 && r.HasUnmapped && (r.Text == u || r.Text.Length == 0))
                {
                    Count(r.Text.Length == 0 ? "no glyph in this family (its code draws something else): left out, reported"
                                             : "no glyph in this family: passed through, reported", $"{u}\twe {got}");
                    continue;
                }
                Count("WRONG", $"{u} [{Cps(u)}]\tcomposer {want} (raw {rawPref})\twe {got}\tunmapped {string.Join("", r.UnmappedCharacters)}");
            }
            Console.WriteLine($"{fam.Id}: raw composer rows {n}; pipeline writes the same as the typing converter for {typedSame}");
            foreach (var (cat, c) in cats)
            {
                Console.WriteLine($"  {cat}: {c}");
                if (cat != "= composer preferred") foreach (var s in samples[cat]) Console.WriteLine("      " + s);
            }
        }
        break;
    }
    default:
        Console.Error.WriteLine("unknown mode " + mode);
        return 1;
}
return 0;

