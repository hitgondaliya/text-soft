using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>Learn, check and write both tables of every family.</summary>
public static class Build
{
    /// <summary>Words a reader would type, run through every family (bytes and the round trip are printed).</summary>
    public static readonly string[] RealWords =
    {
        "ਨੂੰ", "ਨੇ", "ਇੱਕ", "ਇਹ", "ਇਸ", "ਉੱਤੇ", "ਉੱਚਾ", "ਉੱਥੇ", "ਇੱਥੇ", "ਉਨ੍ਹਾਂ", "ਜਿਨ੍ਹਾਂ", "ਤੁਹਾਨੂੰ", "ਸਾਨੂੰ", "ਮੈਨੂੰ",
        "ਨੌਂ", "ਨੰਬਰ", "ਬਠਿੰਡਾ", "ਕੈਨੇਡਾ", "ਇੰਗਲੈਂਡ", "ਪੰਜਾਬ", "ਗੁਰਮੁਖੀ", "ਪ੍ਰਧਾਨ", "ਸ੍ਰੀ", "ਪ੍ਰੇਮ", "ਕ੍ਰਿ\u0A36ਨ", "ਸ੍ਵਰ",
        "\u0A59ਾਲਸਾ", "\u0A5Bਿਲ੍ਹਾ", "\u0A5Aਰੀਬ", "\u0A5Eੌਜ", "\u0A36ਹਿਰ", "ਪ\u0A5C੍ਹਾਈ", "ਚ\u0A5C੍ਹਦੀ", "ਬ੍ਰਹਮ", "ਵਿਦਿਆ", "ਉਹ", "ਊਠ", "ਐਨ", "ਔਰਤ", "ਈ\u0A36ਵਰ",
        "ਆਂਢ", "ਸਿੱਖ", "ਗੁਰੂ", "ਵਾਹਿਗੁਰੂ", "ਅਕਾਲ", "ਸਤਿ", "ਸ੍ਰੀ ਅਕਾਲ", "ਕਿਉਂ", "ਨਿੱਕਾ", "ਠੰਢ", "ਠੀਕ", "ਨ੍ਯਾਯ",
        // nukta letters typed decomposed (NFC text: the six nukta letters are composition exclusions)
        "\u0A16\u0A3C\u0A3E\u0A32\u0A38\u0A3E", "\u0A38\u0A3C\u0A39\u0A3F\u0A30", "\u0A1C\u0A3C\u0A3F\u0A32\u0A4D\u0A39\u0A3E",
        // a virama that ends a word, the halant spelled with a joiner, ੴ, ₹, punctuation
        "ਵਾਕ੍", "ਕ੍‌ਰ", "ਕ੍‍ਰ", "ੴ ਸਤਿ ਨਾਮੁ", "₹੧੦੦", "ਹਾਂ, ਜੀ।", "“ਪੰਜਾਬ”", "\"ਪੰਜਾਬ\"", "'ਪੰਜਾਬ'", "ਰਾਮ's", "੧੨/੦੮/੨੦੨੪", "੧੦:੩੦", "੨+੨=੪", "੫%",
    };

    public static void All(List<Truth> truth, string outDir, string[] only, bool quiet = false)
    {
        Directory.CreateDirectory(outDir);
        var limits = new Dictionary<string, List<string>>();
        var spellings = File.Exists(GroundTruth) ? Capture.Spellings(GroundTruth).Select(x => (x.U, x.Hex)).ToList() : null;
        Console.WriteLine(spellings is null ? $"no capture at {GroundTruth}: its spellings are not read back" : $"capture: {spellings.Count} distinct composer spellings");
        foreach (var fam in Fam.All.Where(f => only.Length == 0 || only.Contains(f.Id)))
            limits[fam.Id] = One(fam, fam.Truth(truth), outDir, spellings);
        foreach (var (id, l) in limits) Console.WriteLine($"limits {id}: {string.Join(" ", l)}");
        string limitsPath = outDir == MapDir ? Path.Combine(Root, "assets", "config", "encoding-limits.json") : Path.Combine(outDir, "encoding-limits.json");
        if (outDir != MapDir) File.Copy(Path.Combine(Root, "assets", "config", "encoding-limits.json"), limitsPath, true);
        Fonts.WriteLimits(limitsPath, limits);
    }

    /// <summary>
    /// The code points the non-Indic policy completes the table for: what every face of the family maps, and the
    /// typographic punctuation the fonts have a look-alike for but no code point of their own (“ ” … ‚ „ •: not in the
    /// cmap, so Word draws them in another font, but RichEdit draws the .notdef box for “ ” … ‚ - r3\word\final-invisible.txt).
    /// </summary>
    public static SortedSet<char> PolicyCodes(Fam fam)
    {
        var codes = new SortedSet<char>(Fonts.Codes(fam));
        foreach (char c in "“”…‚„•") codes.Add(c);
        return codes;
    }

    /// <summary>Code points Word or RichEdit draw nothing (or another font) for in these faces (r3\word: SHREE-PUN, SHREE-PUN7).</summary>
    public const string WordInvisible = "­∙";

    static List<string> One(Fam fam, List<Truth> truth, string outDir, List<(string U, string Hex)>? spellings)
    {
        Console.WriteLine($"==== {fam.Id} ({fam.Fonts})");
        var learner = new Learner(fam);
        var pairs = learner.Learn(truth, out var s);
        Console.WriteLine(s);
        var fwdTable = learner.Build(pairs);
        // Text that is not Gurmukhi: ASCII punctuation the fonts draw as itself, and the digits, written as themselves;
        // every other code the fonts map - ASCII or beyond - written as the look-alike they draw, or left out and reported.
        var cmap = Fonts.Codes(fam);
        var codes = PolicyCodes(fam);
        Ascii.Complete(fwdTable, cmap, fam.Itself, learner.Stager());
        NonIndic.Complete(fwdTable, codes, "22", "27");
        var fwd = new FontConverter(fwdTable);
        Console.WriteLine($"  font code points (every face): {cmap.Count}; ASCII written as itself: {fam.Itself} and 0-9; " +
                          $"pairs marked unmapped: {string.Join("", fwdTable.Pairs.Where(p => p.Unmapped).Select(p => p.From))}");
        Console.WriteLine("  " + Checks.Truth(fwd, truth, out var tf));
        foreach (var f in tf.Take(10)) Console.WriteLine("    " + f);
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
        var leaks = Leaks(fam, fwdTable, fwd, truth);
        Console.WriteLine($"  codes written that {fam.Fonts} draw as something else or not at all: {leaks.Count}");
        foreach (var l in leaks.Take(10)) Console.WriteLine("    " + l);

        // Every code point written must be in the cmap of every installed face of the family, and drawn by Word and RichEdit.
        var probe = truth.Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)).Concat(RealWords)
            .Concat(Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA0, 0x60)).Concat(Enumerable.Range(0x0A00, 0x80))
                .Concat(Enumerable.Range(0x2000, 0x70)).Concat(new[] { 0x20B9, 0x25CC, 0x0964, 0x0965 }).Select(c => ((char)c).ToString()));
        var written = Fonts.Written(fwdTable, fwd, probe);
        var cmapBad = Fonts.Check(fam, written);
        Console.WriteLine($"  code points written: {written.Count} ({string.Join(" ", written.Select(c => Hex(((char)c).ToString())))}); faces {Fonts.Faces(fam).Count}; missing from a face's cmap: {cmapBad.Count}");
        foreach (var l in cmapBad.Take(10)) Console.WriteLine("    " + l);
        var invisible = written.Where(c => WordInvisible.Contains((char)c) || (c >= 0x80 && c <= 0x9F)).ToList();
        Console.WriteLine($"  code points written that Word or RichEdit draw as nothing or in another font: {invisible.Count}");

        var reader = new Reader(fam);
        var back = reader.Learn(truth, fwd, out var rs);
        Console.WriteLine(rs);
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
        if (spellings is not null)
        {
            Console.WriteLine("  " + Reader.CheckCapture(rev, fwd, spellings, out var cf));
            foreach (var f in cf.Where(f => !f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
            foreach (var f in cf.Where(f => f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
        }
        if (File.Exists(GroundTruth))
        {
            var gaps = Capture.Gaps(GroundTruth);
            int derivedSame = 0, derivedOther = 0, none = 0;
            var lines = new List<string>();
            foreach (var (u, wrote, derived, from) in gaps)
            {
                string got = fam.Apply(Hex(fwd.ConvertText(u)));
                string d = derived is null ? "" : fam.Apply(derived);
                if (derived is null) none++;
                else if (got == d) derivedSame++;
                else derivedOther++;
                lines.Add($"{u}\tours {got}\tcomposer wrote {string.Join(" | ", wrote)}\tderived {(derived ?? "-")}\tread back {rev.ConvertText(fwd.ConvertText(u))}");
            }
            Console.WriteLine($"  composer gaps {gaps.Count}: ours = the spelling derived from the composer's rules {derivedSame}, ours differs {derivedOther}, no derived spelling {none}");
            foreach (var l in lines.Take(6)) Console.WriteLine("    " + l);
        }

        foreach (var q in new[] { "\"ਪੰਜਾਬ\"", "'ਪੰਜਾਬ'", "ਉਸ ਨੇ ਕਿਹਾ, \"ਠੀਕ ਹੈ।\"", "ਰਾਮ's" })
        {
            var r = fwd.Convert(q);
            Console.WriteLine($"  quote {q}\t{Hex(r.Text)}\tback {rev.ConvertText(r.Text)}{(r.HasUnmapped ? "\tUNMAPPED " + string.Join("", r.UnmappedCharacters) : "")}");
        }
        Console.WriteLine("  " + NonIndic.Check(fwd, rev, codes, "ਕ", out var ni));
        foreach (var f in ni.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  between ਨ (its spacer): " + NonIndic.Check(fwd, rev, codes, "ਨ", out var ni2));
        foreach (var f in ni2.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Ascii.Check(fwd, cmap, fam.Itself, "ਕ", out var na));
        foreach (var f in na.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  between ਨ: " + Ascii.Check(fwd, cmap, fam.Itself, "ਨ", out var na2));
        foreach (var f in na2.Take(10)) Console.WriteLine("    " + f);
        var q1252 = Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Where(ch => fwd.ConvertText(ch.ToString()).Length > 0).Concat(written.Select(cp => (char)cp)).Where(ch => !char.IsWhiteSpace(ch))
            .Where(ch => !Encodes1252(ch)).Distinct().Select(ch => $"{ch}(U+{(int)ch:X4})").ToList();
        Console.WriteLine($"  characters a .txt output in this family writes as '?' (unmapped, passed through, no Windows-1252 byte): {string.Join(" ", q1252)}");

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
    public static List<string> Leaks(Fam fam, MappingTable table, FontConverter fwd, IReadOnlyList<Truth> truth)
    {
        var never = fam.NeverCodes;
        var bad = new List<string>();
        bool Bad(string legacy) => Hex(legacy.Replace(Rules.Keep, "")).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(c => never.Contains(c));
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
    /// (syllables join by concatenation: 128 of 128 word probes of the capture). Null when no split exists.
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
                // a piece may not end in a virama or joiner unless the word does (it would join the next letter)
                if (j < n && (IsVirama(piece[^1]) || IsJoiner(piece[^1]))) continue;
                if (j < n && IsMark(word[j])) continue;
                if (best[i].Count + 1 < best[j].Count) best[j] = (best[i].Count + 1, i);
            }
        }
        if (best[n].Count == int.MaxValue) return null;
        var parts = new List<string>();
        for (int k = n; k > 0; k = best[k].Prev) parts.Insert(0, word[best[k].Prev..k]);
        return (Norm(string.Join(" ", parts.Select(p => byU[p].Pref))), string.Join("+", parts));
    }
}
