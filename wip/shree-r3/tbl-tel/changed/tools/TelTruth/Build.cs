using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>Learn, check and write both tables of every family.</summary>
public static class Build
{
    /// <summary>Words a reader would type, run through every family (bytes and the round trip are printed).</summary>
    public static readonly string[] RealWords =
    {
        "తెలుగు", "భాష", "ఆంధ్రప్రదేశ్", "హైదరాబాద్", "రాష్ట్రం", "కంప్యూటర్", "ఇంటర్నెట్", "శ్రీ", "స్త్రీ", "లక్ష్మి", "విద్యార్థి",
        "ప్రభుత్వం", "క్రికెట్", "నమస్కారం", "ధన్యవాదాలు", "కృష్ణ", "బ్యాంకు", "స్వాతంత్ర్యం", "శాస్త్రం", "ఫోన్", "అమ్మ", "నాన్న",
        "పుస్తకం", "రైలు", "కైలాసం", "మైసూరు", "ప్రపంచం", "సంస్కృతం", "క్షమించండి", "అక్షరం", "రక్షణ", "జ్ఞానం", "ఉద్యోగం",
        "ముఖ్యమంత్రి", "దేశం", "భారతదేశం", "తిరుపతి", "విజయవాడ", "విశాఖపట్నం", "ఎన్నికలు", "సమాచారం", "ఉత్తరం", "దక్షిణం",
        "పశ్చిమ", "తూర్పు", "గౌరవం", "ఔషధం", "ఋషి", "ఐదు", "ఒకటి", "ఓటు", "ఏమిటి", "ఈరోజు", "ఊరు", "సూర్యుడు", "చంద్రుడు",
        "నక్షత్రం", "వృక్షం", "శ్రమ", "ద్రాక్ష", "కుక్క", "పిల్లి", "ఆవు", "గుర్రం", "ఏనుగు", "సింహం", "దుఃఖం", "అంతఃపురం",
        "హంసలు", "ఆఁ", "కర్ర్", "ఫిల్మ్",
        // a virama that ends a word, the halant spelled with a joiner, punctuation, digits, ₹
        "ఫోన్ కాల్", "క్\u200Cష", "క్\u200Dష", "₹౧౦౦", "₹100", "౧౨/౦౮/౨౦౨౪", "12:30", "తెలుగు, భాష।", "“తెలుగు”", "\"తెలుగు\"", "'తెలుగు'",
        "రాము's", "అ–ఆ", "అ—ఆ", "2*3", "క॥", "కౄ", "ౘ", "ఌ",
    };

    public static void All(List<Truth> truth, string outDir, string[] only, bool quiet = false)
    {
        Directory.CreateDirectory(outDir);
        var limits = new Dictionary<string, List<string>>();
        var spellings = File.Exists(GroundTruth) ? Capture.Spellings(GroundTruth).Select(x => (x.U, x.Hex)).ToList() : null;
        Console.WriteLine(spellings is null ? $"no capture at {GroundTruth}: its spellings are not read back" : $"capture: {spellings.Count} distinct composer spellings");
        Console.WriteLine("  " + Checks.Vattus(truth, out var vf));
        foreach (var f in vf) Console.WriteLine("    " + f);
        foreach (var fam in Fam.All.Where(f => only.Length == 0 || only.Contains(f.Id)))
            limits[fam.Id] = One(fam, fam.Truth(truth), outDir, spellings);
        foreach (var (id, l) in limits) Console.WriteLine($"limits {id}: {string.Join(" ", l)}");
        string limitsPath = outDir == MapDir ? Path.Combine(Root, "assets", "config", "encoding-limits.json") : Path.Combine(outDir, "encoding-limits.json");
        if (outDir != MapDir) File.Copy(Path.Combine(Root, "assets", "config", "encoding-limits.json"), limitsPath, true);
        Fonts.WriteLimits(limitsPath, limits);
    }

    /// <summary>
    /// The code points the non-Indic policy completes the table for: what every face of the family maps, and the
    /// typographic punctuation the fonts have a look-alike for but no code point of their own.
    /// </summary>
    public static SortedSet<char> PolicyCodes(Fam fam)
    {
        var codes = new SortedSet<char>(Fonts.Codes(fam));
        foreach (char c in "“”…‚„•") codes.Add(c);
        return codes;
    }

    /// <summary>The characters no code of the family's fonts draws, passed through as themselves and reported (P3b).</summary>
    public static string[] PassedThrough(Fam fam) => Learner.NoGlyph.Concat(fam.RupeeAt92 ? Array.Empty<string>() : new[] { "₹" }).ToArray();

    /// <summary>ASCII written as a look-alike, not as itself: '-' as the hyphen 26 (2D is blank); '*' as the star 92 where 92 is one.</summary>
    public static Dictionary<char, string> AsciiLookAlikes(Fam fam)
    {
        var d = new Dictionary<char, string> { ['-'] = "26" };
        if (!fam.RupeeAt92) d['*'] = "92";
        return d;
    }

    /// <summary>Codes no table may write: 2D (a blank, '-' in ASCII), 40 and B6 (talakattu ticks: the composer's ః ఁ), the blank 90, AD.</summary>
    public const string Never = "2D 40 B6 90 AD";

    static List<string> One(Fam fam, List<Truth> truth, string outDir, List<(string U, string Hex)>? spellings)
    {
        Console.WriteLine($"==== {fam.Id} ({fam.Fonts})");
        var learner = new Learner(fam);
        var pairs = learner.Learn(truth, out var s);
        Console.WriteLine(s);
        var fwdTable = learner.Build(pairs);
        var cmap = Fonts.Codes(fam);
        var codes = PolicyCodes(fam);
        Ascii.Complete(fwdTable, fam.Itself, learner.Stager());
        NonIndic.Complete(fwdTable, codes, "22", "27");
        foreach (var l in NonIndic.LeaveOutControls(fwdTable)) Console.WriteLine("  " + l);
        // P3b: what no code of the fonts draws is passed through as itself and reported - a pair marked unmapped that writes
        // the character: the Telugu the faces have no glyph for, and ₹ in Shree-Tel7-001/002 (92 is a star there).
        foreach (var g in PassedThrough(fam).Where(g => !fwdTable.Pairs.Any(p => p.From == g)))
            fwdTable.Pairs.Add(new MappingPair { From = g, To = g, Unmapped = true });
        fwdTable.Pairs = fwdTable.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
        var fwd = new FontConverter(fwdTable);
        Console.WriteLine($"  font code points (every face): {cmap.Count}; ASCII written as itself: {fam.Itself} and 0-9; " +
                          $"pairs marked unmapped: {string.Join("", fwdTable.Pairs.Where(p => p.Unmapped && p.From.All(c => c >= ' ')).Select(p => p.From))}");
        Console.WriteLine("  " + Checks.Truth(fwd, truth, out var tf));
        foreach (var f in tf.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Checks.ComposerBytes(fwd, truth));
        foreach (var g in truth.Where(t => t.Why is not null).GroupBy(t => t.Why!).OrderBy(g => g.Key))
            Console.WriteLine($"    exception {g.Key}: {g.Count()} rows, written as documented {g.Count(t => Hex(fwd.ConvertText(t.U)) == t.Pref)}");
        foreach (int seed in new[] { 7, 11, 2026 })
        {
            Console.WriteLine("  " + Checks.Words(fwd, truth, 20000, seed, out var wf));
            foreach (var f in wf.Take(5)) Console.WriteLine("    " + f);
        }
        foreach (int seed in new[] { 7, 11 })
        {
            Console.WriteLine("  " + Checks.WordFinal(fwd, truth, 20000, seed, out var wff));
            foreach (var f in wff.Take(5)) Console.WriteLine("    " + f);
        }
        var leaks = Leaks(fwdTable, fwd, truth);
        Console.WriteLine($"  codes written that {fam.Fonts} draw as something else or not at all ({Never}): {leaks.Count}");
        foreach (var l in leaks.Take(10)) Console.WriteLine("    " + l);

        // Every code point written must be in the cmap of every installed face of the family, and drawn by Word and RichEdit.
        var probe = truth.Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)).Concat(RealWords)
            .Concat(Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA0, 0x60)).Concat(Enumerable.Range(0x0C00, 0x80))
                .Concat(Enumerable.Range(0x2000, 0x70)).Concat(new[] { 0x20B9, 0x25CC, 0x0964, 0x0965 }).Select(c => ((char)c).ToString()));
        var written = Fonts.Written(fwdTable, fwd, probe);
        var cmapBad = Fonts.Check(fam, written);
        Console.WriteLine($"  code points written: {written.Count} ({string.Join(" ", written.Select(c => Hex(((char)c).ToString())))}); faces {Fonts.Faces(fam).Count}; missing from a face's cmap: {cmapBad.Count}");
        foreach (var l in cmapBad.Take(10)) Console.WriteLine("    " + l);
        var invisible = written.Where(c => NonIndic.InvisibleInWord.Contains((char)c) || c == 0xAD).ToList();
        Console.WriteLine($"  code points written that Word or RichEdit draw as nothing: {invisible.Count}");

        var reader = new Reader(fam);
        var back = reader.Learn(truth, fwd, cmap, out var rs);
        Console.WriteLine(rs);
        // P5, our own output (shared policy): what the forward table passes through as itself (P3b) reads back as itself, unreported.
        var passed = NonIndic.PassedThrough(fwd, 0x0C00);
        foreach (var c in passed) back.TryAdd(reader.Staged(c), reader.Visual(c));
        Console.WriteLine($"  read back as themselves (the forward table passes them through): {string.Join(" ", passed)}");
        var revTable = reader.Build(back);
        var rev = new FontConverter(revTable);
        Console.WriteLine("  " + Reader.Check(rev, fwd, truth, out var rf));
        foreach (var f in rf.Take(10)) Console.WriteLine("    " + f);
        foreach (int seed in new[] { 7, 11 })
        {
            Console.WriteLine("  " + Reader.CheckWords(rev, fwd, truth, 20000, seed, out var rw));
            foreach (var f in rw.Take(5)) Console.WriteLine("    " + f);
        }
        Console.WriteLine("  " + Reader.CheckAccepted(rev, fwd, truth, out var ra));
        foreach (var f in ra.Take(5)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Reader.CheckWellFormed(rev, truth, out var wfm));
        foreach (var f in wfm.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Reader.CheckAvagraha(rev, fwd, truth, out var avf));
        foreach (var g in avf.GroupBy(f => f.Split('\t')[0])) foreach (var f in g.Take(6)) Console.WriteLine("    " + f);
        foreach (var dash in new[] { "9B 7B 4D E6 FC", "9B 9B 7B 4D E6 FC", "20 9B 7B 4D E6 FC" })
            Console.WriteLine($"  dash before a hook: {dash} reads {rev.ConvertText(FromHex(dash))}");
        if (spellings is not null)
        {
            Console.WriteLine("  " + Reader.CheckCapture(rev, fwd, spellings, out var cf));
            foreach (var f in cf.Where(f => !f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
            foreach (var f in cf.Where(f => f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
        }
        if (File.Exists(GroundTruth))
        {
            var gaps = Capture.Gaps(GroundTruth);
            int same = 0, other = 0;
            foreach (var (u, derived, _) in gaps)
            {
                string got = Hex(fwd.ConvertText(u));
                if (derived is not null && got == derived.Replace("40", "5D").Replace("B6", "97")) same++;
                else { other++; Console.WriteLine($"    gap {u}\tours {got}\tderived {derived}"); }
            }
            Console.WriteLine($"  composer gaps {gaps.Count} (tel7-composer-gaps.json): ours = the derived spelling {same}, other {other}");
        }

        foreach (var q in new[] { "\"తెలుగు\"", "'తెలుగు'", "అతను అన్నాడు, \"సరే.\"", "రాము's" })
        {
            var r = fwd.Convert(q);
            Console.WriteLine($"  quote {q}\t{Hex(r.Text)}\tback {rev.ConvertText(r.Text)}{(r.HasUnmapped ? "\tUNMAPPED " + string.Join("", r.UnmappedCharacters) : "")}");
        }
        Console.WriteLine("  " + NonIndic.Check(fwd, rev, codes, "క", out var ni));
        foreach (var f in ni.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  between క్ష: " + NonIndic.Check(fwd, rev, codes, "క్ష", out var ni2));
        foreach (var f in ni2.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Ascii.Check(fwd, fam.Itself, AsciiLookAlikes(fam), "క", out var na));
        foreach (var f in na.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  between క్ష: " + Ascii.Check(fwd, fam.Itself, AsciiLookAlikes(fam), "క్ష", out var na2));
        foreach (var f in na2.Take(10)) Console.WriteLine("    " + f);
        var q1252 = Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Where(ch => fwd.ConvertText(ch.ToString()).Length > 0).Concat(written.Select(cp => (char)cp)).Where(ch => !char.IsWhiteSpace(ch))
            .Where(ch => !Encodes1252(ch)).Distinct().Select(ch => $"{ch}(U+{(int)ch:X4})").ToList();
        Console.WriteLine($"  characters passed through (reported) that a .txt output cannot hold - no Windows-1252 byte; FileConversion.LegacyBytes leaves them out, never '?': {string.Join(" ", q1252)}");

        Console.WriteLine("  real words:");
        foreach (var w in RealWords)
        {
            var r = fwd.Convert(w);
            string again = rev.ConvertText(r.Text);
            Console.WriteLine($"    {w}\t{Hex(r.Text)}\t{(again == w ? "=" : "-> " + again)}{(r.HasUnmapped ? "\tUNMAPPED " + string.Join("", r.UnmappedCharacters) : "")}");
        }

        Save(fwdTable, Path.Combine(outDir, $"unicode-to-{fam.Id}.json"));
        Save(revTable, Path.Combine(outDir, $"{fam.Id}-to-unicode.json"));
        Console.WriteLine($"  wrote unicode-to-{fam.Id}.json ({fwdTable.Pairs.Count} pairs) and {fam.Id}-to-unicode.json ({revTable.Pairs.Count} pairs)");
        return Fonts.Limits(fwd);
    }

    /// <summary>True when Windows-1252 has a byte for the character (FileConversion.WriteTxt writes '?' for the rest).</summary>
    public static bool Encodes1252(char c)
    {
        try { Cp1252.GetBytes(c.ToString()); return true; }
        catch (System.Text.EncoderFallbackException) { return false; }
    }

    /// <summary>Codes a table writes that its family draws as something else (or not at all).</summary>
    public static List<string> Leaks(MappingTable table, FontConverter fwd, IReadOnlyList<Truth> truth)
    {
        var never = Never.Split(' ').ToHashSet();
        var bad = new List<string>();
        bool Bad(string legacy) => Hex(legacy.Replace(Rules.Keep, "")).Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(c => never.Contains(c));
        foreach (var p in table.Pairs) if (Bad(p.To)) bad.Add($"pair {p.From} -> {Hex(p.To)}");
        foreach (var u in truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)))
        {
            var r = fwd.Convert(u);
            if (Bad(new string(r.Text.Where(c => !r.UnmappedCharacters.Contains(c.ToString())).ToArray()))) bad.Add($"{u}\t{Hex(r.Text)}");
        }
        return bad;
    }

    /// <summary>
    /// A word as the composer writes it: split into truth strings (fewest pieces), their preferred spellings joined
    /// (syllables join by concatenation: tel7-patterns.txt section 9). Null when no split exists.
    /// </summary>
    public static (string Hex, string Split)? ByTruth(string word, IReadOnlyDictionary<string, Truth> byU)
    {
        int n = word.Length;
        var best = new (int Count, int Prev)[n + 1];
        for (int i = 1; i <= n; i++) best[i] = (int.MaxValue, -1);
        best[0] = (0, -1);
        for (int i = 0; i < n; i++)
        {
            if (best[i].Count == int.MaxValue) continue;
            for (int j = i + 1; j <= n; j++)
            {
                string piece = word[i..j];
                if (!byU.TryGetValue(piece, out var t) || t.Pref.Length == 0) continue;
                if (j < n && (IsVirama(piece[^1]) || IsJoiner(piece[^1]))) continue;
                if (j < n && (IsMark(word[j]) || IsJoiner(word[j]))) continue;
                if (best[i].Count + 1 < best[j].Count) best[j] = (best[i].Count + 1, i);
            }
        }
        if (best[n].Count == int.MaxValue) return null;
        var parts = new List<string>();
        for (int k = n; k > 0; k = best[k].Prev) parts.Insert(0, word[best[k].Prev..k]);
        return (Norm(string.Join(" ", parts.Select(p => byU[p].Pref))), string.Join("+", parts));
    }
}
