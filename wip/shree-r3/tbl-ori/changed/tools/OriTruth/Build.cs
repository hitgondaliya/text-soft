using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>Learn, check and write both tables.</summary>
public static class Build
{
    /// <summary>Words a reader would type, run through the tables (bytes and the round trip are printed).</summary>
    public static readonly string[] RealWords =
    {
        "ଓଡ\u0B3Cିଆ", "ଓଡ\u0B3Cିଶା", "ଭାରତ", "ଭୁବନେଶ୍ୱର", "କଟକ", "ପୁରୀ", "ଜଗନ୍ନାଥ", "ସ୍ୱାସ୍ଥ୍ୟ", "ବିଶ୍ୱ", "ଦ୍ୱିତୀୟ", "ତତ୍ତ୍ୱ", "ମନ୍ତ୍ରୀ", "ସ୍ତ୍ରୀ",
        "ସନ୍ଧ୍ୟା", "ଅକ୍ଟୋବର", "ସରକାର", "କର୍ମ", "ଧର୍ମ", "ପୂର୍ବ", "ଶର୍ମା", "କାର୍ଯ୍ୟ", "ବିଦ୍ୟାଳୟ", "ବିଦ୍ଯାଳୟ", "ପ୍ରଧାନ", "ଶ୍ରୀ", "ରାଷ୍ଟ୍ର",
        "କ୍ଷମା", "ଲକ୍ଷ୍ମୀ", "ଜ୍ଞାନ", "ଚିତ୍ର", "ପୌଷ", "ଗୌରବ", "କୈଳାସ", "ଦେଶ", "ସେ", "ତୁମେ", "ଆମେ", "ଏବଂ", "ସିଂହ", "ଗାଁ", "ଦୁଃଖ",
        "ନଦୀ", "ନୂଆ", "ଅନ୍ନ", "ଚନ୍ଦ୍ର", "ଇନ୍ଦ୍ର", "ଅଙ୍କ", "ଶଙ୍କର", "ପଞ୍ଚ", "ଘଣ୍ଟା", "ମଣ୍ଡଳ", "ଖ୍ରୀଷ୍ଟାବ୍ଦ", "ବର୍ଷ", "ଯୁଦ୍ଧ",
        // nukta letters typed decomposed (NFC text: ଡ଼ ଢ଼ are composition exclusions), e/ai/o/au decomposed
        "ଓଡ\u0B3Cିଆ", "ପଢ\u0B3Cିବା", "କୈଳାସ", "ଗୌରବ",
        // a virama that ends a word, the halant spelled with a joiner, ₹, ୰, ଽ, punctuation
        "ବାକ୍", "ବାକ୍\u200C", "ର୍", "କ୍\u200Cର", "₹୧୦୦", "୰", "ସୋଽହମ୍", "ହଁ, ଠିକ୍।", "“ଓଡ\u0B3Cିଆ”", "\"ଓଡ\u0B3Cିଆ\"", "'ଓଡ\u0B3Cିଆ'", "ରାମ's", "୧୨/୦୮/୨୦୨୪", "୧୦:୩୦", "୨+୨=୪", "୫%", "ଦୁଇ – ତିନି — ଚାରି", "॥",
    };

    public static void All(List<Truth> truth, string outDir, string[] only, bool quiet = false)
    {
        Directory.CreateDirectory(outDir);
        var limits = new Dictionary<string, List<string>>();
        foreach (var fam in Fam.All.Where(f => only.Length == 0 || only.Contains(f.Id)))
        {
            var spellings = File.Exists(GroundTruth) ? Capture.Spellings(GroundTruth).Select(x => (x.U, fam.Doc(x.Hex))).Distinct().ToList() : null;
            Console.WriteLine(spellings is null ? $"no capture at {GroundTruth}: its spellings are not read back" : $"capture: {spellings.Count} distinct composer spellings (as documents hold them)");
            limits[fam.Id] = One(fam, fam.Truth(truth), outDir, spellings);
        }
        foreach (var (id, l) in limits) Console.WriteLine($"limits {id}: {string.Join(" ", l)}");
        string limitsPath = outDir == MapDir ? Path.Combine(Root, "assets", "config", "encoding-limits.json") : Path.Combine(outDir, "encoding-limits.json");
        if (outDir != MapDir) File.Copy(Path.Combine(Root, "assets", "config", "encoding-limits.json"), limitsPath, true);
        Fonts.WriteLimits(limitsPath, limits);
    }

    /// <summary>
    /// The code points the non-Indic policy completes the table for: what every face maps, and the typographic punctuation
    /// the fonts have a look-alike for (“ ” … ‚ „ • are in the cmap and draw Odia ligatures; they are written as the
    /// look-alikes the fonts draw).
    /// </summary>
    public static SortedSet<char> PolicyCodes(Fam fam)
    {
        var codes = new SortedSet<char>(Fonts.Codes(fam));
        foreach (char c in "“”…‚„•") codes.Add(c);
        return codes;
    }

    /// <summary>Code points Word or RichEdit draw nothing (or a box) for in these faces (r3\word: SHREE-ORI, SHREE-ORI7, Shree-Ori7-00x).</summary>
    public const string WordInvisible = "\u008F\u0090­";

    /// <summary>The finished forward table: learned pairs, then ASCII, the output policy's decisions, the shared non-Indic policy, the controls.</summary>
    public static MappingTable Forward(Fam fam, List<Truth> truth, out Learner learner, out string stats, List<string>? decisions = null)
    {
        learner = new Learner(fam);
        var pairs = learner.Learn(truth, out stats);
        var fwdTable = learner.Build(pairs);
        Ascii.Complete(fwdTable, fam.Itself, learner.Stager());
        foreach (var (from, (hex, report, why)) in Policy.Overrides)
        {
            fwdTable.Pairs.RemoveAll(p => p.From == from);
            fwdTable.Pairs.Add(hex is null ? new MappingPair { From = from, To = "", Unmapped = true }
                : hex == "=" ? new MappingPair { From = from, To = from, Unmapped = true }
                : new MappingPair { From = from, To = FromHex(hex), Unmapped = report });
            decisions?.Add($"{from} -> {(hex ?? "left out")}{(report ? ", reported" : "")}: {why}");
        }
        NonIndic.Complete(fwdTable, PolicyCodes(fam), "22", "27");
        var lc = NonIndic.LeaveOutControls(fwdTable);
        decisions?.AddRange(lc);
        // The engine's waiting mark (FontConverter.WaitingMark, U+FDEF) on its own - not after the ra + virama of a waiting
        // reph, which has a pair of its own - writes nothing and is reported: a noncharacter in the text is never dropped
        // silently. The engine leaves its own mark out of what it reports.
        foreach (var p in fwdTable.Pairs.Where(p => p.From == Learner.Waiting)) p.Unmapped = true;
        decisions?.Add("U+FDEF (the engine's waiting mark) on its own -> left out, reported");
        fwdTable.Pairs = fwdTable.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
        return fwdTable;
    }

    static List<string> One(Fam fam, List<Truth> truth, string outDir, List<(string U, string Hex)>? spellings)
    {
        Console.WriteLine($"==== {fam.Id} ({fam.Fonts})");
        var decisions = new List<string>();
        var fwdTable = Forward(fam, truth, out var learner, out var s, decisions);
        Console.WriteLine(s);
        foreach (var d in decisions) Console.WriteLine("  " + d);
        var fwd = new FontConverter(fwdTable);
        var cmap = Fonts.Codes(fam);
        var codes = PolicyCodes(fam);
        Console.WriteLine($"  font code points (every face): {cmap.Count}; ASCII written as itself: {fam.Itself} and 0-9; " +
                          $"pairs marked unmapped: {string.Join(" ", fwdTable.Pairs.Where(p => p.Unmapped && p.From.Length == 1 && p.From[0] > ' ' && p.From[0] < '\u0080').Select(p => p.From))} " +
                          $"+ {fwdTable.Pairs.Count(p => p.Unmapped && !(p.From.Length == 1 && p.From[0] > ' ' && p.From[0] < '\u0080'))} beyond ASCII");
        Console.WriteLine("  " + Checks.Truth(fwd, truth, out var tf));
        foreach (var f in tf.Take(20)) Console.WriteLine("    " + f);
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

        // Every code point written must be in the cmap of every installed face, and drawn by Word and RichEdit.
        var probe = truth.Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)).Concat(RealWords)
            .Concat(Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA0, 0x60)).Concat(Enumerable.Range(0x0B00, 0x80))
                .Concat(Enumerable.Range(0x2000, 0x70)).Concat(new[] { 0x20B9, 0x25CC, 0x0964, 0x0965 }).Select(c => ((char)c).ToString()));
        var written = Fonts.Written(fwdTable, fwd, probe);
        var cmapBad = Fonts.Check(fam, written);
        Console.WriteLine($"  code points written: {written.Count} ({string.Join(" ", written.Select(c => Hex(((char)c).ToString())))}); faces {Fonts.Faces(fam).Count}; missing from a face's cmap: {cmapBad.Count}");
        foreach (var l in cmapBad.Take(10)) Console.WriteLine("    " + l);
        var invisible = written.Where(c => WordInvisible.Contains((char)c) || (c >= 0x80 && c <= 0x9F)).ToList();
        Console.WriteLine($"  code points written that Word or RichEdit draw as nothing: {invisible.Count} {string.Join(" ", invisible.Select(c => $"U+{c:X4}"))}");
        var cs238 = written.Where(c => c is 0x152 or 0x153 or 0x160 or 0x161 or 0x178 or 0x192).ToList();
        Console.WriteLine($"  code points written that RichEdit (only) binds to Arial, charset 238 (Word draws them): {string.Join(" ", cs238.Select(c => $"U+{c:X4}={Hex(((char)c).ToString())}"))}");

        var reader = new Reader(fam);
        var back = reader.Learn(truth, fwd, out var rs);
        Console.WriteLine(rs);
        // P5, our own output (shared policy): what the forward table passes through as itself (P3b: ₹ ୰ ଽ, the signs no face
        // has a glyph for) reads back as itself, unreported - text in a fallback font.
        var passed = NonIndic.PassedThrough(fwd, 0x0B00);
        foreach (var c in passed) back.TryAdd(reader.Staged(c), c);
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
        if (spellings is not null)
        {
            Console.WriteLine("  " + Reader.CheckCapture(rev, fwd, spellings, out var cf));
            foreach (var f in cf.Where(f => !f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
            foreach (var f in cf.Where(f => f.StartsWith("OTHER")).Take(12)) Console.WriteLine("    " + f);
            var ev = Capture.EvidenceRows();
            int evText = 0, evBa = 0, evBaSame = 0;
            var evBad = new List<string>();
            foreach (var (u, hex, tag) in ev)
            {
                string got = rev.ConvertText(FromHex(fam.Doc(hex)));
                var bad = Malformed(got).Where(b => b.StartsWith("Latin") || b.StartsWith("C1") || b.StartsWith("control") || b.StartsWith("noncharacter")).ToList();
                if (bad.Count == 0) evText++; else evBad.Add($"{hex}\t{tag}\tread {got}\t{string.Join("; ", bad)}");
                if (u is not null) { evBa++; if (got == u || Reader.Same(got, u, Hex(fwd.ConvertText(u)), fwd) || got == u.Replace("୍ବ", "୍ବ")) evBaSame++; }
            }
            Console.WriteLine($"  7-only codes (E1 51 E0) in {ev.Count} composer spellings: read as Odia text {evText}, Latin/control {evBad.Count}; E1 rows read as their chart text (୍ବ) {evBaSame}/{evBa}");
            foreach (var f in evBad.Take(5)) Console.WriteLine("    " + f);
        }

        foreach (var q in new[] { "\"ଓଡ\u0B3Cିଆ\"", "'ଓଡ\u0B3Cିଆ'", "ସେ କହିଲେ, \"ଠିକ୍ ଅଛି।\"", "ରାମ's" })
        {
            var r = fwd.Convert(q);
            Console.WriteLine($"  quote {q}\t{Hex(r.Text)}\tback {rev.ConvertText(r.Text)}{(r.HasUnmapped ? "\tUNMAPPED " + string.Join("", r.UnmappedCharacters) : "")}");
        }
        // P5, ନ as the composer writes it: 9C FF (the raw byte of code 65). Every raw composer spelling that holds 9C must read
        // as its document spelling (9C -> 5B) does; and the forward table never writes 9C before the spacer FF (ଙ୍କ୍ତ is a bare 9C).
        if (File.Exists(GroundTruth))
        {
            int raw9c = 0, raw9cSame = 0;
            var raw9cBad = new List<string>();
            foreach (var (u, rawHex, _) in Capture.Spellings(GroundTruth).Where(x => x.Hex.Split(' ').Contains("9C")).DistinctBy(x => x.Hex))
            {
                raw9c++;
                string a = rev.ConvertText(FromHex(rawHex)), b = rev.ConvertText(FromHex(fam.Doc(rawHex)));
                if (a == b) raw9cSame++; else raw9cBad.Add($"{rawHex}\ttyped {u}\tread {a}\tthe document spelling reads {b}");
            }
            Console.WriteLine($"  raw composer spellings holding 9C (ନ = 9C FF): {raw9c}, read as their document spelling (5B FF) reads: {raw9cSame}");
            foreach (var f in raw9cBad.Take(5)) Console.WriteLine("    " + f);
        }
        var na9cff = truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U)).Concat(RealWords)
            .Where(u => (" " + Hex(fwd.ConvertText(u)) + " ").Contains(" 9C FF ")).ToList();
        Console.WriteLine($"  strings the forward table writes with 9C before the spacer FF (they would read back as ନ): {na9cff.Count}");
        foreach (var f in na9cff.Take(5)) Console.WriteLine("    " + f);
        var fdef = fwd.Convert(Learner.Waiting);
        Console.WriteLine($"  U+FDEF alone: written '{Hex(fdef.Text)}', reported left out: {fdef.LeftOutCharacters.Contains(Learner.Waiting)}");

        Console.WriteLine("  " + NonIndic.Check(fwd, rev, codes, "କ", out var ni));
        foreach (var f in ni.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  between ନ (5B FF): " + NonIndic.Check(fwd, rev, codes, "ନ", out var ni2));
        foreach (var f in ni2.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Ascii.Check(fwd, fam.Itself, "କ", out var na));
        foreach (var f in na.Take(10)) Console.WriteLine("    " + f);
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
    public static List<string> Leaks(Fam fam, MappingTable table, FontConverter fwd, IReadOnlyList<Truth> truth)
    {
        var never = fam.NeverCodes;
        var bad = new List<string>();
        bool Bad(string legacy) => Hex(legacy.Replace(Rules.Keep, "")).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(c => never.Contains(c));
        foreach (var p in table.Pairs) if (Bad(p.Unmapped ? new string(p.To.Where(c => !p.From.Contains(c)).ToArray()) : p.To)) bad.Add($"pair {p.From} -> {Hex(p.To)}");
        foreach (var u in truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)))
        {
            var r = fwd.Convert(u);
            if (Bad(new string(r.Text.Where(c => !r.UnmappedCharacters.Contains(c.ToString())).ToArray()))) bad.Add($"{u}\t{Hex(r.Text)}");
        }
        return bad;
    }

    /// <summary>
    /// A word as the composer writes it: split into truth strings (fewest pieces), their preferred spellings joined
    /// (syllables join by concatenation: 201 of 201 word probes of the capture). Null when no split exists.
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
