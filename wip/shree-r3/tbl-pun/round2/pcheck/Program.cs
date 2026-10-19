using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static U;

// pcheck - the independent Punjabi skeptic's own measure of the real FontConverter / TypingEngine (check1\repo copy)
// against the raw composer capture (cap-pun\pun7-groundtruth.json, pun-vendor-keys.json) and the Word map.
static class Program
{
    static readonly string Check1 = FindCheck1();
    static string FindCheck1()
    {
        // (round 2 copy: run against tbl-pun\repo, writing to round2\out)
        if (Environment.GetEnvironmentVariable("PCHECK_HOME") is { Length: > 0 } home) return home;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "repo", "FontSoftware.sln")) && Directory.Exists(Path.Combine(d.FullName, "pcheck"))) return d.FullName;
        throw new InvalidOperationException("check1 not found");
    }
    static readonly string Repo = Environment.GetEnvironmentVariable("PCHECK_REPO") is { Length: > 0 } r ? r : Path.Combine(Check1, "repo");
    static readonly string R3 = Path.GetFullPath(Path.Combine(Check1, "..", ".."));
    static readonly string Cap = Path.Combine(R3, "cap-pun");
    static readonly string WordFam = Path.Combine(R3, "word", "fam");
    static readonly string Out = Path.Combine(Check1, "out");
    static AssetBundle B;
    static readonly Dictionary<string, FontConverter> Fwd = new(), Rev = new();
    static ConversionPipeline Pipe;
    static readonly string[] Fams = { "shree-pun7", "shree-pun456" };

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Directory.CreateDirectory(Out);
        B = new ConfigLoader(Path.Combine(Repo, "assets", "config")).Load();
        Pipe = new ConversionPipeline(B);
        foreach (var f in Fams)
        {
            Fwd[f] = B.CreateConverterToLegacy(f) ?? throw new Exception("no forward " + f);
            var t = B.MappingTables.FirstOrDefault(m => m.SourceFontId == f && m.SourceType == FontType.Legacy && m.TargetType == FontType.Unicode)
                    ?? throw new Exception("no reverse " + f);
            Rev[f] = new FontConverter(t);
        }
        Console.WriteLine($"repo {Repo}; tables: " + string.Join(", ", Fams.Select(f => $"{f} fwd {Fwd[f].Table.Id} ({Fwd[f].Table.Pairs.Count} pairs) rev {Rev[f].Table.Id} ({Rev[f].Table.Pairs.Count})")));
        string mode = args.Length > 0 ? args[0] : "all";
        if (mode == "table")
        {
            var tbl = System.Text.Json.JsonSerializer.Deserialize<MappingTable>(File.ReadAllText(args[1]), ConfigLoader.JsonOptions);
            var tc = new FontConverter(tbl);
            foreach (var s in args.Skip(2)) { var rr = tc.Convert(s); Console.WriteLine($"{Path.GetFileName(args[1])}	{s}	[{Cps(s)}]	{Hex(rr.Text)}	rep {string.Join(",", rr.UnmappedCharacters)}"); }
            return 0;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "truth": Truth(); break;
            case "words": Words(int.Parse(args[1]), int.Parse(args[2])); break;
            case "reverse": Reverse(); break;
            case "sweep": Sweep(); break;
            case "keys": KeysAudit(); break;
            case "house": House(); break;
            case "docx": DocxMode(); break;
            case "typerand": TypeRand(int.Parse(args[1]), int.Parse(args[2])); break;
            case "leak":
            {
                var pa = B.Languages.First(l => l.Id == "pa");
                var tr = B.GetTransliterator(pa);
                const string itselfSet = "!%()*+,-./:;=?[]0123456789";
                int total = 0;
                foreach (var lref in pa.Layouts)
                {
                    var layout = B.GetLayout(lref);
                    foreach (int layer in new[] { 0, 1 })
                    {
                        var leaks = new List<string>();
                        for (char k = '!'; k <= '~'; k++)
                        {
                            using var t = new Keys(layout, tr, Fwd["shree-pun7"], "SHREE-PUN7-0951");
                            bool h = t.Press(k, layer);
                            string b = t.App.Buf.ToString();
                            var raw = b.Where(ch => ch >= 0x21 && ch <= 0x7E && !itselfSet.Contains(ch)).ToList();
                            // a raw key the engine did not take, drawn as another glyph in the Shree font
                            if (!h && !itselfSet.Contains(k)) leaks.Add($"{k}={Hex(k.ToString())}");
                        }
                        total += leaks.Count;
                        if (leaks.Count > 0) Console.WriteLine($"LEAK {lref.Id}{(layout.Phonetic ? " (phonetic)" : "")} L{layer}: {leaks.Count} keys not taken by the engine reach the Shree font raw: {string.Join(" ", leaks)}");
                    }
                }
                Console.WriteLine($"LEAK total {total}");
                break;
            }
            case "trace":
            {
                var pa = B.Languages.First(l => l.Id == "pa");
                var layout = B.GetLayout(pa.Layouts.First(l => l.Id == args[1]));
                var tr = B.GetTransliterator(pa);
                foreach (var seq in args.Skip(2))
                {
                    using var tu = new Keys(layout, tr, null, "Raavi");
                    using var t7 = new Keys(layout, tr, Fwd["shree-pun7"], "SHREE-PUN7-0951");
                    var line = new List<string>();
                    foreach (char k in seq)
                    {
                        bool hu = tu.Press(k), h7 = t7.Press(k);
                        string u = tu.App.Buf.ToString();
                        line.Add($"[{k}{(h7 ? "" : "*")}] uni {u} p7 {Hex(t7.App.Buf.ToString())} conv {Hex(Fwd["shree-pun7"].ConvertText(u))}");
                    }
                    Console.WriteLine($"{args[1]} '{seq}':\n    " + string.Join("\n    ", line));
                }
                break;
            }
            case "all": Truth(); Words(9001, 424242); Reverse(); Sweep(); KeysAudit(); House(); break;
            case "conv":
                foreach (var s in args.Skip(2)) { var rr = Fwd[args[1]].Convert(s); Console.WriteLine($"{s}\t[{Cps(s)}]\t{Hex(rr.Text)}\trep {string.Join(",", rr.UnmappedCharacters)}\tback {Rev[args[1]].ConvertText(rr.Text)}"); }
                break;
            case "read":
                foreach (var h in args.Skip(2)) { var u = Rev[args[1]].ConvertText(FromHex(h)); Console.WriteLine($"{h}\t{u}\t[{Cps(u)}]\t-> {Hex(Fwd[args[1]].ConvertText(u))}"); }
                break;
            default: Console.Error.WriteLine("modes: truth | words a b | reverse | sweep | keys | house | all | conv fam s.. | read fam hex.."); return 1;
        }
        Console.WriteLine($"[{mode} done in {sw.Elapsed.TotalSeconds:F1} s]");
        return 0;
    }

    // ------------------------------------------------------------------ truth
    record Row(string U, string Pref, string[] Acc, string[] Alt);

    static List<Row> GroundTruth()
    {
        var g = Json(Path.Combine(Cap, "pun7-groundtruth.json"));
        var rows = new List<Row>();
        foreach (var kv in g["truth"]!.AsObject())
        {
            var t = kv.Value!;
            rows.Add(new Row(kv.Key, Norm((string)t["preferred"]!),
                t["accepted"]!.AsArray().Select(x => Norm((string)x!)).ToArray(),
                t["alternatives"]!.AsArray().Select(x => Norm((string)x!)).ToArray()));
        }
        return rows;
    }

    static string Expect456(string hex) => string.Join(" ", hex.Split(' ').Select(h => h == "3C" ? "U+20B9" : h));

    static readonly HashSet<string> WrittenSeen7 = new(), WrittenSeen456 = new();
    static void Seen(string fam, string text, bool reportedAllowed, IEnumerable<string> reported)
    {
        var set = fam == "shree-pun7" ? WrittenSeen7 : WrittenSeen456;
        var rep = reported.ToHashSet();
        foreach (char c in text) if (!rep.Contains(c.ToString())) set.Add(((int)c).ToString("X4"));
    }

    static void Truth()
    {
        var rows = GroundTruth();
        Console.WriteLine($"TRUTH raw capture rows {rows.Count}");
        // the fixture against the raw capture
        var fx = Json(Path.Combine(Repo, "tests", "FontSoftware.Tests", "Data", "shree-pun7-truth.json"));
        var fxRows = fx["rows"]!.AsArray().Select(a => a!.AsArray()).ToDictionary(a => (string)a[0]!, a => a, StringComparer.Ordinal);
        int fxSame = 0, fxPrefDiff = 0, fxAccDiff = 0, fxMissing = 0;
        var fxNotes = new List<string>();
        foreach (var r in rows)
        {
            if (!fxRows.TryGetValue(r.U, out var a)) { fxMissing++; if (fxNotes.Count < 10) fxNotes.Add("missing " + r.U); continue; }
            string p = Norm((string)a[1]!);
            var acc = (a.Count > 2 ? a[2]!.AsArray().Select(x => Norm((string)x!)) : Enumerable.Empty<string>()).Append(p).ToHashSet();
            if (p != r.Pref) { fxPrefDiff++; if (fxNotes.Count < 20) fxNotes.Add($"pref differs {r.U}: capture {r.Pref} fixture {p}"); }
            else if (!acc.SetEquals(r.Acc.Append(r.Pref))) { fxAccDiff++; if (fxNotes.Count < 20) fxNotes.Add($"accepted differs {r.U}"); }
            else fxSame++;
        }
        Console.WriteLine($"  fixture vs capture: rows fixture {fxRows.Count}; same {fxSame}, preferred differs {fxPrefDiff}, accepted differs {fxAccDiff}, missing {fxMissing}");
        foreach (var n in fxNotes) Console.WriteLine("    " + n);

        foreach (var fam in Fams)
        {
            var conv = Fwd[fam];
            int pref = 0, acc = 0, exc = 0, repBad = 0, nfdDiff = 0, nfcDiff = 0, decompDiff = 0;
            var lines = new List<string>();
            var excByShape = new Dictionary<string, int>();
            foreach (var r in rows)
            {
                var res = conv.Convert(r.U);
                string got = Hex(res.Text);
                bool rupee = r.U.Contains('₹');
                string want = fam == "shree-pun456" && rupee ? Expect456(r.Pref) : r.Pref;
                var wantRep = fam == "shree-pun456" && rupee ? new[] { "₹" } : Array.Empty<string>();
                bool repOk = res.UnmappedCharacters.ToHashSet().SetEquals(wantRep);
                Seen(fam, res.Text, true, res.UnmappedCharacters);
                if (got == want && repOk) pref++;
                else if (got == want) { repBad++; lines.Add($"REPORT\t{r.U}\t[{Cps(r.U)}]\tgot {got}\treported {string.Join(",", res.UnmappedCharacters)}"); }
                else if (r.Acc.Contains(got) && repOk) { acc++; lines.Add($"ACCEPTED\t{r.U}\t[{Cps(r.U)}]\tpref {r.Pref}\tgot {got}"); }
                else
                {
                    exc++;
                    lines.Add($"EXCEPTION\t{r.U}\t[{Cps(r.U)}]\tpref {r.Pref}\tacc {string.Join("|", r.Acc)}\tgot {got}\trep {string.Join(",", res.UnmappedCharacters)}");
                }
                // the same text in other normal forms must give the same bytes
                string nfd = r.U.Normalize(NormalizationForm.FormD), nfc = r.U.Normalize(NormalizationForm.FormC);
                if (nfd != r.U && Hex(conv.ConvertText(nfd)) != got) { nfdDiff++; lines.Add($"NFD\t{r.U}\t[{Cps(nfd)}]\tgot {Hex(conv.ConvertText(nfd))} vs {got}"); }
                if (nfc != r.U && Hex(conv.ConvertText(nfc)) != got) { nfcDiff++; lines.Add($"NFC\t{r.U}\t[{Cps(nfc)}]\tgot {Hex(conv.ConvertText(nfc))} vs {got}"); }
                // nukta letters typed as letter + nukta (what InScript-like input and most text have)
                string dec = Decomp(r.U);
                if (dec != r.U && Hex(conv.ConvertText(dec)) != got) { decompDiff++; lines.Add($"DECOMP\t{r.U}\t[{Cps(dec)}]\tgot {Hex(conv.ConvertText(dec))} vs {got}"); }
            }
            Console.WriteLine($"  {fam}: {rows.Count} rows: preferred {pref}, accepted-not-preferred {acc}, exceptions {exc}, right bytes wrong report {repBad}; NFD differs {nfdDiff}, NFC differs {nfcDiff}, letter+nukta differs {decompDiff}");
            File.WriteAllLines(Path.Combine(Out, $"truth-{fam}.tsv"), lines, new UTF8Encoding(false));
            foreach (var l in lines.Where(l => !l.StartsWith("ACCEPTED")).Take(12)) Console.WriteLine("    " + l);
            var accShapes = lines.Where(l => l.StartsWith("ACCEPTED")).Take(6);
            foreach (var l in accShapes) Console.WriteLine("    " + l);
        }
    }

    static string Decomp(string s) => s.Replace("\u0A59", "\u0A16\u0A3C").Replace("\u0A5A", "\u0A17\u0A3C").Replace("\u0A5B", "\u0A1C\u0A3C")
        .Replace("\u0A5E", "\u0A2B\u0A3C").Replace("\u0A33", "\u0A32\u0A3C").Replace("\u0A36", "\u0A38\u0A3C");

    // ------------------------------------------------------------------ words
    static void Words(int seedA, int seedB)
    {
        var rows = GroundTruth();
        bool Plain(string u) => u.All(c => IsGurmukhi(c) || c == '\u200C' || c == '\u200D');
        var units = rows.Where(r => r.Pref.Length > 0 && Plain(r.U) && !IsMarkLike(r.U[0]) && r.U[^1] != '\u0A4D' && r.U[^1] != '\u200C' && r.U[^1] != '\u200D').ToList();
        var finals = rows.Where(r => r.Pref.Length > 0 && Plain(r.U) && r.U.Length > 1 && !IsMarkLike(r.U[0]) && r.U[^1] == '\u0A4D').ToList();
        Console.WriteLine($"WORDS units {units.Count}, word-final-virama units {finals.Count}");
        // the word-final virama rows themselves: must end in the visible halant B5
        foreach (var fam in Fams)
        {
            int fin = 0, finOk = 0, finPref = 0; var bad = new List<string>();
            foreach (var r in finals)
            {
                fin++;
                string got = Hex(Fwd[fam].ConvertText(r.U));
                if (got.EndsWith("B5")) finOk++; else if (bad.Count < 8) bad.Add($"{r.U} got {got} pref {r.Pref}");
                if (got == r.Pref) finPref++;
            }
            Console.WriteLine($"  {fam}: word-final virama rows {fin}: end in B5 {finOk}, equal composer preferred {finPref}");
            foreach (var b in bad) Console.WriteLine("    " + b);
        }
        foreach (int seed in new[] { seedA, seedB })
            foreach (var fam in Fams)
            {
                var rnd = new Random(seed);
                int words = 0, wordOk = 0, lines = 0, lineOk = 0, finalsUsed = 0;
                int revExact = 0, revSame = 0, revOther = 0, revBad = 0;
                var fails = new List<string>();
                while (words < 20000)
                {
                    var lu = new List<string>(); var lh = new List<string>();
                    for (int w = 0; w < 5 && words < 20000; w++)
                    {
                        int k = 1 + rnd.Next(4);
                        var parts = new List<Row>();
                        for (int i = 0; i < k; i++) parts.Add(units[rnd.Next(units.Count)]);
                        if (rnd.NextDouble() < 0.15) { parts[^1] = finals[rnd.Next(finals.Count)]; finalsUsed++; }
                        string u = string.Concat(parts.Select(p => p.U));
                        string want = string.Join(" ", parts.Select(p => p.Pref));
                        string got = Hex(Fwd[fam].ConvertText(u));
                        words++;
                        if (got == want) wordOk++; else if (fails.Count < 12) fails.Add($"word {u} [{Cps(u)}] want {want} got {got} parts {string.Join("+", parts.Select(p => p.U))}");
                        lu.Add(u); lh.Add(want);
                    }
                    string line = string.Join(" ", lu), lineWant = string.Join(" 20 ", lh);
                    var res = Fwd[fam].Convert(line);
                    lines++;
                    string lineGot = Hex(res.Text);
                    Seen(fam, res.Text, false, res.UnmappedCharacters);
                    if (lineGot == lineWant && res.UnmappedCharacters.Count == 0) lineOk++;
                    else if (fails.Count < 24) fails.Add($"line want {lineWant} got {lineGot} rep {string.Join(",", res.UnmappedCharacters)}");
                    // read the line back (P5)
                    string back = Rev[fam].ConvertText(res.Text);
                    if (BadChars(back).Count > 0) { revBad++; if (fails.Count < 30) fails.Add($"REVBAD {line} -> {back} {string.Join(";", BadChars(back))}"); }
                    else if (back.Normalize(NormalizationForm.FormC) == line.Normalize(NormalizationForm.FormC) || Decomp(back) == Decomp(line)) revExact++;
                    else if (Hex(Fwd[fam].ConvertText(back)) == lineGot) revSame++;
                    else { revOther++; if (fails.Count < 30) fails.Add($"REVOTHER {line} -> {back}"); }
                }
                Console.WriteLine($"  seed {seed} {fam}: words {wordOk}/{words} exact (final-virama words {finalsUsed}); lines of 5 {lineOk}/{lines}; read back: exact {revExact}, same bytes {revSame}, other {revOther}, bad chars {revBad}");
                foreach (var f in fails.Take(10)) Console.WriteLine("    " + f);
            }
    }

    // ------------------------------------------------------------------ reverse
    static void Reverse()
    {
        var g = Json(Path.Combine(Cap, "pun7-groundtruth.json"));
        var items = new List<(string U, string Hex, string Kind)>();
        foreach (var p in g["pairs"]!.AsArray()) items.Add(((string)p!["unicode"]!, Norm((string)p["bytes"]!), (string)p["kind"]!));
        foreach (var r in GroundTruth())
        {
            items.Add((r.U, r.Pref, "truth:preferred"));
            foreach (var a in r.Acc) items.Add((r.U, a, "truth:accepted"));
            foreach (var a in r.Alt) items.Add((r.U, a, "truth:alternative"));
        }
        var distinct = items.Where(i => i.Hex.Length > 0).GroupBy(i => (i.U, i.Hex)).Select(gr => gr.First()).ToList();
        var distinctBytes = distinct.Select(i => i.Hex).Distinct().ToList();
        Console.WriteLine($"REVERSE items {items.Count}, distinct (text,bytes) {distinct.Count}, distinct byte strings {distinctBytes.Count}");
        foreach (var fam in Fams)
        {
            var byKind = new SortedDictionary<string, int[]>(); // exact, same, other, bad
            var lines = new List<string>();
            int pipeDiffers = 0;
            foreach (var (u, hex, kind) in distinct)
            {
                string k = kind.Split(' ')[0].Split('(')[0];
                if (k.StartsWith("quirk:")) k = "quirk:" + (kind.Contains("carrier-07") ? "carrier-07" : "other");
                if (k.StartsWith("alt:")) k = kind.Length > 40 ? kind[..40] : kind;
                if (!byKind.TryGetValue(k, out var cnt)) byKind[k] = cnt = new int[4];
                string legacy = FromHex(hex);
                if (fam == "shree-pun456" && hex.Split(' ').Contains("3C")) { /* 456 has no ₹: still read it */ }
                string back = Rev[fam].ConvertText(legacy);
                string viaPipe = Pipe.Convert(legacy, LayoutKey.Legacy(fam), LayoutKey.Unicode).Text;
                if (viaPipe != back && viaPipe.Normalize(NormalizationForm.FormC) != back.Normalize(NormalizationForm.FormC)) pipeDiffers++;
                var bad = BadChars(back).Concat(BadChars(viaPipe)).Distinct().ToList();
                if (bad.Count > 0) { cnt[3]++; lines.Add($"BAD\t{kind}\t{u}\t{hex}\t-> {back} [{Cps(back)}]\t{string.Join(";", bad)}"); continue; }
                bool exact = back == u || back.Normalize(NormalizationForm.FormC) == u.Normalize(NormalizationForm.FormC) || Decomp(back) == Decomp(u);
                if (exact) { cnt[0]++; continue; }
                string again = Hex(Fwd[fam].ConvertText(back));
                string hexCmp = fam == "shree-pun456" ? Expect456(hex) : hex;
                if (again == hexCmp) { cnt[1]++; continue; }
                cnt[2]++; lines.Add($"OTHER\t{kind}\t{u}\t{hex}\t-> {back} [{Cps(back)}]\t-> {again}");
            }
            Console.WriteLine($"  {fam}: kind: exact / same bytes / other text / BAD chars   (pipeline read differs from table read: {pipeDiffers})");
            int[] tot = new int[4];
            foreach (var kv in byKind) { Console.WriteLine($"    {kv.Key,-60} {kv.Value[0],6} {kv.Value[1],6} {kv.Value[2],6} {kv.Value[3],6}"); for (int i = 0; i < 4; i++) tot[i] += kv.Value[i]; }
            Console.WriteLine($"    {"TOTAL",-60} {tot[0],6} {tot[1],6} {tot[2],6} {tot[3],6}");
            File.WriteAllLines(Path.Combine(Out, $"reverse-{fam}.tsv"), lines, new UTF8Encoding(false));
            foreach (var l in lines.Where(l => l.StartsWith("BAD")).Take(8)) Console.WriteLine("    " + l);
            foreach (var l in lines.Where(l => l.StartsWith("OTHER") && !l.Contains("\talt:") && !l.Contains("\tquirk:")).Take(12)) Console.WriteLine("    " + l);
        }
        // every byte string a face could hold: each single code 20-FF alone and after ਕ (48)
        foreach (var fam in Fams)
        {
            var bad = new List<string>();
            for (int b = 0x20; b <= 0xFF; b++)
            {
                if (b == 0x7F) continue;
                foreach (var hex in new[] { b.ToString("X2"), "48 " + b.ToString("X2"), b.ToString("X2") + " 48" })
                {
                    string back = Rev[fam].ConvertText(FromHex(hex));
                    var bc = BadChars(back);
                    if (bc.Count > 0) bad.Add($"{hex} -> {back} [{Cps(back)}] {string.Join(";", bc)}");
                }
            }
            Console.WriteLine($"  {fam}: single codes 20-FF (alone, after 48, before 48): bad reads {bad.Count}");
            foreach (var x in bad.Take(40)) Console.WriteLine("    " + x);
        }
    }

    // ------------------------------------------------------------------ sweep + cmap + Word
    static readonly Dictionary<string, string[]> Faces = new()
    {
        ["shree-pun7"] = new[] { "WPUN7001.TTF", "WPUN7002.TTF" },
        ["shree-pun456"] = new[] { "S09500F2.TTF", "S09510F2.TTF", "S09520F2.TTF", "S09550F2.TTF", "S09560F2.TTF" },
    };
    static readonly Dictionary<string, string> WordFamOf = new() { ["shree-pun7"] = "SHREE-PUN7", ["shree-pun456"] = "SHREE-PUN" };

    static (HashSet<int> Union, HashSet<int> Inter, List<string> Notes) FaceCmaps(string fam)
    {
        HashSet<int> uni = null, inter = null; var notes = new List<string>();
        foreach (var f in Faces[fam])
        {
            var set = Cmap.Read(Path.Combine(@"C:\Windows\Fonts", f), out var which);
            notes.Add($"{f} {which} {set.Count} code points");
            uni = uni is null ? new HashSet<int>(set) : uni.Union(set).ToHashSet();
            inter = inter is null ? new HashSet<int>(set) : inter.Intersect(set).ToHashSet();
        }
        return (uni, inter, notes);
    }

    static Dictionary<int, JsonNode> WordMap(string fam)
    {
        var j = Json(Path.Combine(WordFam, WordFamOf[fam] + ".json"));
        return j["codepoints"]!.AsArray().ToDictionary(c => Convert.ToInt32(((string)c!["cp"]!)[2..], 16), c => c!);
    }

    static bool WordOk(JsonNode e) =>
        (bool?)e["word"]?["ok"] == true && (bool?)e["richedit"]?["typed"]?["ok"] == true && (bool?)e["richedit"]?["rtf"]?["ok"] == true;

    static void Sweep()
    {
        foreach (var fam in Fams)
        {
            var (cmap, inter, notes) = FaceCmaps(fam);
            Console.WriteLine($"SWEEP {fam}: faces {string.Join("; ", notes)}; union {cmap.Count}, all faces same: {cmap.SetEquals(inter)}");
            var wm = WordMap(fam);
            var conv = Fwd[fam];
            string K = conv.ConvertText("ਕ");
            var cls = new SortedDictionary<string, List<string>>();
            void Add(string c, string line) { if (!cls.TryGetValue(c, out var l)) cls[c] = l = new(); l.Add(line); }
            for (int cp = 0; cp <= 0xFFFF; cp++)
            {
                if (cp >= 0xD800 && cp <= 0xDFFF) continue;
                if (cp >= 0x0A00 && cp <= 0x0A7F) continue;
                string s = ((char)cp).ToString();
                var r = conv.Convert(s);
                bool rep = r.UnmappedCharacters.Contains(s);
                string o = r.Text;
                Seen(fam, o, true, r.UnmappedCharacters);
                string id = $"U+{cp:X4}";
                string mid = conv.ConvertText("ਕ" + s + "ਕ");
                if (mid != K + o + K && cp != '"' && cp != '\'') Add("CONTEXT", $"{id}\talone {Hex(o)}\tbetween ਕ..ਕ {Hex(mid)}");
                bool asciiLetter = (cp >= 'A' && cp <= 'Z') || (cp >= 'a' && cp <= 'z');
                if (o == s)
                {
                    if (cmap.Contains(cp))
                    {
                        string draws = wm.TryGetValue(cp, out var e) ? (string)e["draws"]! : "?";
                        bool itself = draws.StartsWith("itself") || draws.StartsWith("script digit") || draws.StartsWith("nothing");
                        if (asciiLetter) Add(rep ? "P3? ASCII letter passed through as its code (reported), font draws" : "P3 WRONG ASCII letter passed unreported", $"{id} {s} draws {draws}");
                        else if (itself && !rep) Add("own code, drawn as itself (P2)", $"{id} {s} {draws}");
                        else if (itself) Add("own code drawn as itself BUT reported", $"{id} {s} {draws}");
                        else Add("P3 WRONG: own code drawn as something else", $"{id} {s} draws {draws} reported {rep}");
                    }
                    else if (rep) Add("passed through, reported (not in cmap)", id);
                    else Add("passed through UNREPORTED (not in cmap)", $"{id} {(char.IsWhiteSpace(s[0]) ? "whitespace" : "")} {System.Globalization.CharUnicodeInfo.GetUnicodeCategory(s[0])}");
                }
                else if (o.Length == 0) Add(rep ? "left out, reported" : "LEFT OUT SILENTLY", $"{id} {System.Globalization.CharUnicodeInfo.GetUnicodeCategory(s[0])}");
                else
                {
                    bool allIn = o.All(c => cmap.Contains(c));
                    string draws = string.Join(" + ", o.Select(c => wm.TryGetValue(c, out var e) ? $"{Hex(c.ToString())}:{(string)e["draws"]}" : $"{Hex(c.ToString())}:?"));
                    Add(allIn ? (rep ? "look-alike written AND reported" : "look-alike written") : "written as code(s) NOT in cmap", $"{id} {s}\t-> {Hex(o)}\t{draws}");
                }
            }
            var lines = new List<string>();
            foreach (var kv in cls)
            {
                Console.WriteLine($"  {kv.Key}: {kv.Value.Count}");
                bool show = !kv.Key.StartsWith("passed through, reported") && !kv.Key.StartsWith("left out, reported");
                foreach (var l in kv.Value.Take(show ? 80 : 6)) Console.WriteLine("      " + l);
                lines.Add("== " + kv.Key + " " + kv.Value.Count); lines.AddRange(kv.Value);
            }
            File.WriteAllLines(Path.Combine(Out, $"sweep-{fam}.txt"), lines, new UTF8Encoding(false));
        }
        Written();
    }

    /// <summary>Every code point the forward tables wrote in this run (truth, words, sweep, every pair alone): in every face, drawn by Word / RichEdit.</summary>
    static void Written()
    {
        foreach (var fam in Fams)
        {
            foreach (var p in Fwd[fam].Table.Pairs) { var r = Fwd[fam].Convert(p.From); Seen(fam, r.Text, true, r.UnmappedCharacters); }
            var seen = fam == "shree-pun7" ? WrittenSeen7 : WrittenSeen456;
            var wm = WordMap(fam);
            var faceSets = Faces[fam].Select(f => (f, Cmap.Read(Path.Combine(@"C:\Windows\Fonts", f), out _))).ToList();
            var bad = new List<string>();
            foreach (var h in seen.OrderBy(x => x))
            {
                int cp = Convert.ToInt32(h, 16);
                if (cp is 0x20 or 0x09 or 0x0A or 0x0D) continue;
                var missing = faceSets.Where(fs => !fs.Item2.Contains(cp)).Select(fs => fs.f).ToList();
                string word = wm.TryGetValue(cp, out var e) ? (WordOk(e) ? "ok" : $"NOT OK word={(string)e["word"]?["verdict"]} re={(string)e["richedit"]?["typed"]?["verdict"]}") : "not in Word map";
                if (missing.Count > 0 || word != "ok") bad.Add($"U+{h} {(char)cp}: missing in {string.Join(",", missing)}; Word/RichEdit {word}; draws {(e is null ? "?" : (string)e["draws"])}");
            }
            Console.WriteLine($"WRITTEN {fam}: {seen.Count} distinct code points written (not counting reported pass-throughs); problems {bad.Count}");
            foreach (var b in bad) Console.WriteLine("    " + b);
            Console.WriteLine("    all: " + string.Join(" ", seen.OrderBy(x => x)));
        }
    }

    // ------------------------------------------------------------------ keys
    static readonly Dictionary<string, string> LayoutOf = new()
    {
        ["CRTRONIC.PUN"] = "pun-crtronic", ["DOE.PUN"] = "pun-doe", ["ENG.PUN"] = "pun-eng", ["GODREJ.PUN"] = "pun-godrej",
        ["INSCRIPT.PUN"] = "pa-inscript", ["ITR.PUN"] = "pun-itr", ["NEWROMA.PUN"] = "pun-newroma", ["PHONETIC.PUN"] = "pun-phonetic",
        ["RAMING.PUN"] = "pun-raming", ["ROMA.PUN"] = "pun-roma", ["TYPEWRIT.PUN"] = "pun-typewrit", ["VARITYPE.PUN"] = "pun-varitype",
    };
    static readonly HashSet<string> PhoneticMode = new() { "ENG.PUN", "NEWROMA.PUN", "PHONETIC.PUN", "ROMA.PUN" };

    static void KeysAudit()
    {
        var v = Json(Path.Combine(Cap, "pun-vendor-keys.json"));
        var pa = B.Languages.First(l => l.Id == "pa");
        var tr = B.GetTransliterator(pa);
        var cls = new SortedDictionary<string, List<string>>();
        void Add(string c, string line) { if (!cls.TryGetValue(c, out var l)) cls[c] = l = new(); l.Add(line); }
        int positions = 0, shreeTypes = 0, eq = 0, eqNonPh = 0, nonPh = 0, p6bad = 0, p456diff = 0;
        foreach (var kv in v["keyboards"]!.AsObject())
        {
            string kb = kv.Key;
            var lref = pa.Layouts.First(l => l.Id == LayoutOf[kb]);
            var layout = B.GetLayout(lref);
            bool ph = PhoneticMode.Contains(kb);
            foreach (int layer in new[] { 0, 1 })
            {
                var keys = kv.Value!["layers"]![layer.ToString()]!.AsObject();
                for (char k = '!'; k <= '~'; k++)
                {
                    positions++;
                    var e = keys.ContainsKey(k.ToString()) ? keys[k.ToString()] : null;
                    string b7 = e is null ? "" : (string)e["b7"];
                    string uAns = e is null ? "" : (string)e["u"];
                    string got7, gotU, got456;
                    using (var t = new Keys(layout, tr, Fwd["shree-pun7"], "SHREE-PUN7-0951")) { t.Press(k, layer); got7 = t.App.Buf.ToString(); }
                    using (var t = new Keys(layout, tr, null, "Raavi")) { t.Press(k, layer); gotU = t.App.Buf.ToString(); }
                    using (var t = new Keys(layout, tr, Fwd["shree-pun456"], "SHREE-PUN-0950")) { t.Press(k, layer); got456 = t.App.Buf.ToString(); }
                    if (Fwd["shree-pun7"].ConvertText(gotU) != got7) { p6bad++; Add("P6: typed bytes != converter(typed Unicode)", $"{kb} L{layer} '{k}' typed {Hex(got7)} conv(uni {gotU}) {Hex(Fwd["shree-pun7"].ConvertText(gotU))}"); }
                    if (Hex(got456) != Hex(got7)) { p456diff++; Add("456 profile differs from 7 profile", $"{kb} L{layer} '{k}' 7 {Hex(got7)} 456 {Hex(got456)}"); }
                    string g7 = Hex(got7);
                    {
                        const string itselfSet = "!%()*+,-./:;=?[]0123456789 ";
                        var raw = gotU.Where(ch => ch >= 0x21 && ch <= 0x7E && !itselfSet.Contains(ch) && got7.Contains(ch)).Distinct().ToList();
                        if (raw.Count > 0) Add("RAW ASCII from an unhandled key reaches the legacy font (drawn as another glyph) L" + layer, $"{kb} L{layer} '{k}' TS {Hex(got7)} uni [{gotU}] raw {string.Concat(raw)}");
                    }
                    string tag = $"{kb}{(ph ? "(ph)" : "")} L{layer} '{k}'";
                    if (e is null)
                    {
                        if (got7.Length == 0) { eq++; if (!ph) { eqNonPh++; } }
                        else Add($"Shree types nothing (no key), TextSoft types{(ph ? " [phonetic-mode kb]" : "")} L{layer}", $"{tag} TS {g7} ({gotU})");
                        if (!ph) nonPh++;
                        continue;
                    }
                    if (b7 is null) { Add("Shree no answer (b7 null)", $"{tag} codes {(string)e["codes"]} TS {g7} ({gotU})"); continue; }
                    shreeTypes++; if (!ph) nonPh++;
                    if (b7.StartsWith("07")) { Add("Shree carrier 07", $"{tag} b7 {b7} u {uAns} TS {g7}"); continue; }
                    if (g7 == Norm(b7)) { eq++; if (!ph) eqNonPh++; continue; }
                    string cat;
                    if (got7.Length == 0) cat = "TextSoft types nothing, Shree types";
                    else
                    {
                        string a = Rev["shree-pun7"].ConvertText(got7), b = Rev["shree-pun7"].ConvertText(FromHex(b7));
                        cat = a.Normalize(NormalizationForm.FormC) == b.Normalize(NormalizationForm.FormC) ? "different bytes, same text read back" : "different bytes and text";
                    }
                    Add(cat + (ph ? " [phonetic-mode kb]" : ""), $"{tag} codes {(string)e["codes"]} Shree {b7} (u {uAns}) TS {g7} (uni {gotU})");
                }
            }
            // Ctrl+Shift+1 symbols
            foreach (var s in v["ctrlShift1"]!["keys"]!.AsObject())
            {
                char k = s.Key[0]; string b7 = (string)s.Value!["b7"]!;
                using var t = new Keys(layout, tr, Fwd["shree-pun7"], "SHREE-PUN7-0951"); t.Press(k, 2);
                string g = Hex(t.App.Buf.ToString());
                if (g == b7) Add("Ctrl+Shift+1 equal", $"{kb} {k} {g}"); else Add("Ctrl+Shift+1 DIFFERS", $"{kb} {k} Shree {b7} TS {g}");
            }
        }
        Console.WriteLine($"KEYS positions {positions} (12 kb x 2 layers x 94), Shree answers {shreeTypes}; equal (incl. both nothing) {eq}; non-phonetic-mode keyboards: equal {eqNonPh} of {nonPh}; P6 mismatches {p6bad}; 456-vs-7 profile diffs {p456diff}");
        var lines = new List<string>();
        foreach (var kv in cls)
        {
            Console.WriteLine($"  {kv.Key}: {kv.Value.Count}");
            foreach (var l in kv.Value.Take(kv.Key.Contains("equal") ? 0 : 40)) Console.WriteLine("      " + l);
            lines.Add("== " + kv.Key + " " + kv.Value.Count); lines.AddRange(kv.Value);
        }
        File.WriteAllLines(Path.Combine(Out, "keys.txt"), lines, new UTF8Encoding(false));
    }

    // ------------------------------------------------------------------ random key runs: typed legacy bytes = converter(typed Unicode)
    static void TypeRand(int n, int seed)
    {
        var pa = B.Languages.First(l => l.Id == "pa");
        var tr = B.GetTransliterator(pa);
        const string keys = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;',./[]=-`\\";
        foreach (var lref in pa.Layouts)
        {
            var layout = B.GetLayout(lref);
            var rnd = new Random(seed);
            int same7 = 0, same456 = 0, words = 0, onSpace = 0; var bad = new List<string>();
            for (int i = 0; i < n; i++)
            {
                int len = 2 + rnd.Next(6);
                var seq = new string(Enumerable.Range(0, len).Select(_ => keys[rnd.Next(keys.Length)]).ToArray());
                string Type(FontConverter c, string font, bool space)
                {
                    using var t = new Keys(layout, tr, c, font);
                    foreach (char k in seq) t.Press(k);
                    if (space) t.Press(' ');
                    return t.App.Buf.ToString();
                }
                string u = Type(null, "Raavi", true), b7 = Type(Fwd["shree-pun7"], "SHREE-PUN7-0951", true), b456 = Type(Fwd["shree-pun456"], "SHREE-PUN-0950", true);
                words++;
                string want7 = Hex(Fwd["shree-pun7"].ConvertText(u)), want456 = Hex(Fwd["shree-pun456"].ConvertText(u));
                if (Hex(b7) == want7) same7++; else if (bad.Count < 12) bad.Add($"'{seq}' uni [{u}] typed7 {Hex(b7)} conv {want7}");
                if (Hex(b456) == want456) same456++;
            }
            Console.WriteLine($"TYPERAND {lref.Id}{(layout.Phonetic ? " (phonetic)" : "")}: {words} random key runs + space: typed = converter(Unicode typed) pun7 {same7}, pun456 {same456}");
            foreach (var b in bad.Take(6)) Console.WriteLine("    " + b);
        }
    }

    // ------------------------------------------------------------------ Word spot check input
    static void DocxMode()
    {
        var rows = GroundTruth();
        var words = File.ReadAllLines(Path.Combine(Cap, "common-words.txt"), Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        string dir = Path.Combine(Check1, "word"); Directory.CreateDirectory(dir);
        foreach (var (fam, font) in new[] { ("shree-pun7", "SHREE-PUN7-0951"), ("shree-pun456", "SHREE-PUN-0950") })
        {
            var conv = Fwd[fam];
            var written = new SortedSet<char>();
            foreach (var r in rows) { var c = conv.Convert(r.U); foreach (char ch in c.Text) if (!c.UnmappedCharacters.Contains(ch.ToString())) written.Add(ch); }
            foreach (var p in conv.Table.Pairs) { var c = conv.Convert(p.From); foreach (char ch in c.Text) if (!c.UnmappedCharacters.Contains(ch.ToString())) written.Add(ch); }
            foreach (var w in words) foreach (char ch in conv.ConvertText(w)) written.Add(ch);
            written.Remove(' ');
            var lines = new List<string>();
            var cps = written.ToList();
            // every written code point between two ਕ (48), 12 per line
            for (int i = 0; i < cps.Count; i += 12) lines.Add(string.Join(" ", cps.Skip(i).Take(12).Select(c => "H" + c + "H")));
            for (int i = 0; i < words.Count; i += 10) lines.Add(conv.ConvertText(string.Join(" ", words.Skip(i).Take(10))));
            Docx.Write(Path.Combine(dir, fam + ".docx"), font, lines);
            File.WriteAllLines(Path.Combine(dir, fam + ".expected.txt"), lines.Select(l => Hex(l)), new UTF8Encoding(false));
            Console.WriteLine($"{fam}: {cps.Count} written code points, {lines.Count} lines -> {Path.Combine(dir, fam + ".docx")}");
            Console.WriteLine("  " + string.Join(" ", cps.Select(c => Hex(c.ToString()))));
        }
    }

    // ------------------------------------------------------------------ house policy for ASCII letters in the other Shree 7 tables
    static void House()
    {
        foreach (var enc in new[] { "shree-guj7", "shree-guj456", "shree-dev7", "shree-dev456", "shree-ban7", "shree-ban456", "shree-pun7", "shree-pun456" })
        {
            var c = B.CreateConverterToLegacy(enc);
            if (c is null) { Console.WriteLine($"HOUSE {enc}: no table"); continue; }
            var r = c.Convert("Hello world xyz");
            Console.WriteLine($"HOUSE {enc}: 'Hello world xyz' -> {Hex(r.Text)}  reported {string.Join("", r.UnmappedCharacters.OrderBy(x => x, StringComparer.Ordinal))}");
        }
    }
}
