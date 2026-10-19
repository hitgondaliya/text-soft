using FontSoftware.Core.Conversion;
using static Common;

public static class Build
{
    public static void All(IReadOnlyList<Truth> truth, string outDir)
    {
        Directory.CreateDirectory(outDir);
        if (Rules.Waiting != FontSoftware.Core.Conversion.FontConverter.WaitingMark.ToString()) throw new InvalidOperationException("Rules.Waiting must be FontConverter.WaitingMark");

        // Unicode -> 7.
        var seven = Learner.Learn(truth, truth, out var s7);
        Console.WriteLine("7 forward: " + s7);
        var f7 = Learner.Build(seven);
        NonIndic.Complete(f7, "guj7", "22", "27");
        var policy7 = NonIndic.Policy(f7, "guj7", "22", "27", Policy.Overrides, Policy.NoGlyph);
        Console.WriteLine($"  output policy (P2-P4), {policy7.Count} decisions:");
        foreach (var l in policy7) Console.WriteLine("    " + l);
        // P3b: the table passes the no-glyph characters through; every check against the composer's
        // bytes runs on the table as the composer spells (conv7c), and CheckPassThrough proves the
        // real table is exactly that plus the characters themselves.
        var conv7 = new FontConverter(f7);
        var conv7c = new FontConverter(NonIndic.AsComposer(f7, Policy.NoGlyph));
        Report("7", conv7c, truth);
        var leaks7 = Family.Leaks(seven, false);
        Console.WriteLine($"  family codes written by the 7 table: {leaks7.Count}");
        foreach (var l in leaks7.Take(10)) Console.WriteLine("    " + l);

        // Unicode -> 4/5/6.
        var f456pairs = Family.Forward456(seven, truth, out var s456);
        Console.WriteLine(s456);
        var f456 = Family.Table456(f456pairs);
        NonIndic.Complete(f456, "guj456", "22", "27");
        NonIndic.Policy(f456, "guj456", "22", "27", Policy.Overrides, Policy.NoGlyph);
        var conv456 = new FontConverter(f456);
        var conv456c = new FontConverter(NonIndic.AsComposer(f456, Policy.NoGlyph));
        int ok456 = truth.Count(t => Family.Accept456(Hex(conv456c.ConvertText(t.U)), t));
        Console.WriteLine($"  4/5/6 truth: {ok456}/{truth.Count} accepted");
        var leaks456 = Family.Leaks(f456pairs, true);
        Console.WriteLine($"  4/5/6 codes written for anything but their ઙ conjunct: {leaks456.Count}");
        Console.WriteLine("  " + Words456(conv456c, truth, 20000, 7));

        // 7 -> Unicode.
        var back = Reader.Learn(truth, conv7c, out var r7);
        Console.WriteLine("7 reverse: " + r7);
        var back7 = new Dictionary<string, string>(back, StringComparer.Ordinal);
        int copies = 0;
        foreach (var (code, u, code7) in Reader.SevenCopies)
        {
            back7[FromHex(code)] = u;
            foreach (var (from, to) in back.Where(p => p.Key.Length > 1 && p.Key.Contains(FromHex(code7))))
            {
                back7.TryAdd(from.Replace(FromHex(code7), FromHex(code)), to);
                copies++;
            }
        }
        Console.WriteLine($"  7 copies (91-94, B6) read as their originals, {copies} longer spellings duplicated");
        var r7t = Reader.Build(back7, "shree-guj7-to-unicode", "Shree-Lipi 7.0 Gujarati -> Unicode", "shree-guj7");
        var read7 = new FontConverter(r7t);
        Console.WriteLine("  " + Reader.Check(read7, conv7c, truth, out var rf7));
        foreach (var f in rf7.Take(15)) Console.WriteLine("    " + f);
        foreach (bool vis in new[] { false, true })
            foreach (int seed in new[] { 7, 11 })
            {
                Console.WriteLine("  " + Reader.CheckWords(read7, conv7c, truth, 20000, seed, vis, out var rw7));
                foreach (var f in rw7.Take(6)) Console.WriteLine("    " + f);
            }
        Console.WriteLine("  " + Reader.CheckAccepted(read7, conv7c, truth, out var ra7));
        foreach (var f in ra7.Take(10)) Console.WriteLine("    " + f);

        // 4/5/6 -> Unicode.
        var back456 = new Dictionary<string, string>(back, StringComparer.Ordinal);
        foreach (var (code, u) in Reader.NgaConjuncts) back456[FromHex(code)] = u;
        var r456t = Reader.Build(back456, "shree-guj456-to-unicode", "Shree-Lipi 4.0/5.0/6.0 Gujarati -> Unicode", "shree-guj456", Reader.PreRules456);
        var read456 = new FontConverter(r456t);
        int rb456 = 0; var rbf = new List<string>();
        foreach (var t in truth)
        {
            string legacy = conv456c.ConvertText(t.U);
            if (legacy.Length == 0) continue;
            string got = read456.ConvertText(legacy);
            if (Reader.Same(got, t.U, Hex(legacy), conv456c)) rb456++;
            else rbf.Add($"{t.U}\t{Hex(legacy)}\tgot {got}");
        }
        Console.WriteLine($"  4/5/6 read back of its own spellings: {rb456}/{rb456 + rbf.Count}");
        foreach (var f in rbf.Take(10)) Console.WriteLine("    " + f);
        // P3b (P5): the tables' own spelling of every truth row with a vocalic-l character - passed through as
        // itself - reads back as that row.
        foreach (var (name, real, read) in new[] { ("7", conv7, read7), ("4/5/6", conv456, read456) })
        {
            var rows = truth.Where(t => t.U.Any(c => Policy.NoGlyph.Contains(c.ToString()))).ToList();
            var pf = rows.Select(t => (t.U, Legacy: real.ConvertText(t.U))).Select(x => (x.U, x.Legacy, Got: read.ConvertText(x.Legacy)))
                         .Where(x => x.Got != x.U).Select(x => $"{x.U}\t{Hex(x.Legacy)}\tgot {x.Got} [{Cps(x.Got)}]").ToList();
            Console.WriteLine($"  {name} read back of its own spellings with a vocalic-l character passed through: {rows.Count - pf.Count}/{rows.Count} exact");
            foreach (var f in pf.Take(10)) Console.WriteLine("    " + f);
        }

        // What the skeptics found, each over every case of its kind.
        Console.WriteLine("checks:");
        foreach (var (name, real, fwd, rev) in new[] { ("7", conv7, conv7c, read7), ("4/5/6", conv456, conv456c, read456) })
        {
            // P3b: the real table = the composer's spelling + the no-glyph characters themselves, over the
            // truth, 20,000 words, and each no-glyph character alone, between letters and with the signs.
            var inputs = truth.Select(t => t.U)
                .Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U))
                .Concat(Policy.NoGlyph.SelectMany(g => new[] { g, "ક" + g + "ક", "ર્" + g, g + "ં", "કિ" + g, "ક્" + g, g + " ક", "ર્ક" + g + "ં" }))
                .ToList();
            Console.WriteLine($"  {name} " + NonIndic.CheckPassThrough(real, fwd, inputs, Policy.NoGlyph, out var fp)); foreach (var f in fp.Take(8)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.RephOnNga(fwd, rev, out var f1)); foreach (var f in f1.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.RephBeforeFinalHalant(fwd, rev, out var f3)); foreach (var f in f3.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.NuktaReadBack(fwd, rev, out var f4)); foreach (var f in f4.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.ViramaBeforeNa(fwd, truth, out var f5)); foreach (var f in f5.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + NonIndic.Check(real, rev, name == "7" ? "guj7" : "guj456", "ક", out var f2)); foreach (var f in f2.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + NonIndic.CheckAscii(real, rev, name == "7" ? "guj7" : "guj456", "22", "27", "ક", out var f6)); foreach (var f in f6.Take(6)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.Ligatures(fwd, rev, truth, out var f7l)); foreach (var f in f7l.Take(8)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.Waiting(fwd, truth, out var f8)); foreach (var f in f8.Take(8)) Console.WriteLine("    " + f);
            Console.WriteLine($"  {name} " + Checks.ReadsShree(rev, fwd, out var f9)); foreach (var f in f9.Take(12)) Console.WriteLine("    " + f);
        }
        Console.WriteLine("  4/5/6 " + Checks.NgaRephBytes(read456, out var f1b)); foreach (var f in f1b.Take(6)) Console.WriteLine("    " + f);

        Save(f7, Path.Combine(outDir, "unicode-to-shree-guj7.json"));
        Save(f456, Path.Combine(outDir, "unicode-to-shree-guj456.json"));
        Save(r7t, Path.Combine(outDir, "shree-guj7-to-unicode.json"));
        Save(r456t, Path.Combine(outDir, "shree-guj456-to-unicode.json"));
        Console.WriteLine("wrote 4 tables to " + outDir);
    }

    static void Report(string name, FontConverter conv, IReadOnlyList<Truth> truth)
    {
        Console.WriteLine("  " + Checks.Truth(conv, truth, out var fails));
        foreach (var f in fails.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  as the composer itself writes them (the rows the table writes otherwise left out): " +
                          Checks.Truth(conv, truth.Where(t => t.Composer is null).ToList(), out _));
        foreach (int seed in new[] { 7, 11, 2026 })
        {
            Console.WriteLine("  " + Checks.Words(conv, truth, 20000, seed, out var wf));
            foreach (var f in wf.Take(10)) Console.WriteLine("    " + f);
        }
        Console.WriteLine($"  word-final rows (the composer's waiting half form, written with the halant): {Checks.WordFinalRows(truth).Count}");
        foreach (int seed in new[] { 5, 13 })
        {
            Console.WriteLine("  " + Checks.WordFinal(conv, truth, 20000, seed, out var ff));
            foreach (var f in ff.Take(10)) Console.WriteLine("    " + f);
        }
    }

    static string Words456(FontConverter conv, IReadOnlyList<Truth> truth, int count, int seed)
    {
        var pool = Checks.Syllables(truth);
        var alone = pool.ToDictionary(t => t.U, t => conv.ConvertText(t.U), StringComparer.Ordinal);
        var rng = new Random(seed);
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            int n = 2 + rng.Next(3);
            string u = "", want = "";
            for (int k = 0; k < n; k++) { var s = pool[rng.Next(pool.Count)]; u += s.U; want += alone[s.U]; }
            if (conv.ConvertText(u) == want) ok++;
        }
        return $"4/5/6 words {count}: exact {ok}";
    }
}
