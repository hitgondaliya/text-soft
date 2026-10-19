using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>Learn, check and write both tables of every family.</summary>
public static class Build
{
    /// <summary>Words a reader would type, run through every family (bytes and the round trip are printed).</summary>
    public static readonly string[] RealWords =
    {
        "কর্ম", "ধর্ম", "প্রকাশ", "ক্ষমা", "জ্ঞান", "শ্রী", "বাংলা", "কিতাব", "স্থির", "রাষ্ট্র",
        "বিদ্যার্থী", "অর্ধ", "পৃথিবী", "রুপি", "রূপ", "ট্রাক", "কৃপা", "হৃদয়", "অসমীয়া", "ৰাজ্য",
        "তত্ত্ব", "উজ্জ্বল", "সান্ত্বনা", "উচ্ছ্বাস", "ধ্বনি", "উদ্ঘাটন",
        // Assamese, ra as ৰ: the reph and the ra-phala
        "কৰ্ম", "প্ৰকাশ", "শ্ৰী", "ৰাষ্ট্ৰ", "প্ৰধানমন্ত্ৰী", "কাৰ্য", "সৰ্বভাৰতীয়",
        // ৱ, the filler clusters, a conjunct with the ra-phala and ু ূ
        "ৱা", "ৰাৱণ", "ধ্বংস", "ঊর্ধ্ব", "উদ্গম", "উদ্ঘাটন", "বড়্গ", "ইনস্ট্রুমেন্ট", "স্ট্রুকচার", "ক্ক্রূ",
        // a hasanta that ends a word
        "বাক্", "সৎ", "দিক্ ", "ক্।", "কর্", "কৰ্",
        // characters the fonts cannot draw: passed through and reported
        "১২/০৮/২০২৪", "১০:৩০", "২+২=৪", "৫%",
        // English (left out: every Latin letter's code is a Bengali glyph), '<' and ₹, a soft hyphen, the dots
        "TextSoft বাংলা", "a:b", "x%", "১<২", "₹১০", "ক" + (char)0x00AD + "খ", "ক·খ", "ক∙খ", "ক" + (char)0x2003 + "খ",
        // ড় ঢ় with ু ূ and ঁ or a reph (decision 6), ি + ঁ on a conjunct, a reph on ঙ্ক্ষ
        "ড়ুঁ", "ড়ূঁ", "র্ড়ু", "র্ড়ূ", "র্ঢ়ুঁ", "স্তিঁ", "ন্তিঁ", "র্ঙ্ক্ষ",
    };

    public static void All(List<Truth> truth, string outDir, string[] only)
    {
        Directory.CreateDirectory(outDir);
        var limits = new Dictionary<string, List<string>>();
        // Every spelling of the composer in the whole capture, when it is here (it is not needed to build).
        var spellings = File.Exists(GroundTruth) ? Capture.Spellings(GroundTruth).Select(x => (x.U, x.Hex)).ToList() : null;
        Console.WriteLine(spellings is null ? $"no capture at {GroundTruth}: its spellings are not read back" : $"capture: {spellings.Count} distinct composer spellings");
        foreach (var fam in Fam.All.Where(f => only.Length == 0 || only.Contains(f.Id)))
            limits[fam.Id] = One(fam, fam.Truth(truth), outDir, spellings);
        foreach (var (id, l) in limits) Console.WriteLine($"limits {id}: {string.Join(" ", l)}");
        if (outDir == MapDir) Fonts.WriteLimits(Path.Combine(Root, "assets", "config", "encoding-limits.json"), limits);
    }

    static List<string> One(Fam fam, List<Truth> truth, string outDir, List<(string U, string Hex)>? spellings)
    {
        Console.WriteLine($"==== {fam.Id} ({fam.Fonts})");
        var learner = new Learner(fam);
        var pairs = learner.Learn(truth, out var s);
        Console.WriteLine(s);
        var fwdTable = learner.Build(pairs);
        // Text that is not Bengali (tools/Shared/ShreeNonIndic.cs): ASCII punctuation the fonts draw
        // as itself, and the digits, written as themselves; every other code the fonts map - ASCII
        // or beyond - written as the look-alike they draw, or left out and reported.
        var codes = new SortedSet<char>(Fonts.Codes(fam));
        // Two more the reverse tables must read as Bengali, so they are never written for text that
        // is not: the C1 control codes (Word and RichEdit draw nothing for them, and a document typed
        // with Shree's composer or an earlier table holds ব্ঢ ব্ড চ্ঞ there - also in an Assamese font,
        // which has no ব্ড at 8D), and U+017E, the ž a .txt file decodes the byte 9E as - the byte the
        // Bangla fonts draw ৱ at (an earlier table wrote ৱ as U+017E); every family reads both as ৱ.
        for (char c = '\u0080'; c <= '\u009F'; c++) codes.Add(c);
        codes.Add('ž');
        Ascii.Complete(fwdTable, codes, fam.Itself, learner.Stager());
        NonIndic.Complete(fwdTable, codes, fam.Code("91"), fam.Code("92"));
        // P4 (shared policy, 2026-10-18 merge): the C0 controls and DEL are left out and reported as well
        // (Word draws them as nothing); the C1 codes already are, above.
        foreach (var l in NonIndic.LeaveOutControls(fwdTable)) Console.WriteLine("  " + l);
        var fwd = new FontConverter(fwdTable);
        Console.WriteLine($"  font code points (every face): {codes.Count}; ASCII written as itself: {fam.Itself} and 0-9; " +
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
            Console.WriteLine("  " + Checks.WordFinal(fwd, fam, truth, 20000, seed, out var wff));
            foreach (var f in wff.Take(5)) Console.WriteLine("    " + f);
        }
        var c1 = C1Written(fwd, truth);
        Console.WriteLine($"  outputs with a C1 code point (U+0080-U+009F), over the truth, 20000 words, every single character of the Bengali block, ASCII, Latin-1 and general punctuation: {c1.Count}");
        foreach (var l in c1.Take(10)) Console.WriteLine("    " + l);
        var leaks = Leaks(fam, fwdTable, fwd, truth);
        Console.WriteLine($"  codes written that draw something else in {fam.Fonts}: {leaks.Count}");
        foreach (var l in leaks.Take(10)) Console.WriteLine("    " + l);

        // Every code point written must be in the cmap of every installed face of the family.
        var written = Fonts.Written(fwdTable, fwd, truth.Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)).Concat(RealWords));
        var cmapBad = Fonts.Check(fam, written);
        Console.WriteLine($"  code points written: {written.Count}; faces {Fonts.Faces(fam).Count}; missing from a face's cmap: {cmapBad.Count}");
        foreach (var l in cmapBad.Take(10)) Console.WriteLine("    " + l);
        // ...and each has a byte in a .txt file (FileConversion.LegacyBytes: U+0000-U+00FF as themselves, then Windows-1252):
        // a code point written unreported with no byte would become a silent '?'.
        var noByte = written.Where(cp => cp > 0xFF && !Encodes1252((char)cp)).Select(cp => $"U+{cp:X4}").ToList();
        Console.WriteLine($"  code points written (unreported) that a .txt file has no byte for: {noByte.Count} {string.Join(" ", noByte)}");

        var reader = new Reader(fam, truth);
        var back = reader.Learn(truth, fwd, out var rs);
        Console.WriteLine(rs);
        var revTable = reader.Build(back);
        var rev = new FontConverter(revTable);
        Console.WriteLine("  " + Reader.Check(rev, fwd, truth, out var rf));
        foreach (var f in rf.Take(10)) Console.WriteLine("    " + f);
        foreach (bool vis in new[] { false, true })
            foreach (int seed in new[] { 7, 11 })
            {
                Console.WriteLine("  " + Reader.CheckWords(rev, fwd, truth, 20000, seed, vis, out var rw));
                foreach (var f in rw.Take(5)) Console.WriteLine("    " + f);
            }
        Console.WriteLine("  " + Reader.CheckAccepted(rev, fwd, truth, out var ra));
        foreach (var f in ra.Take(5)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Reader.CheckWellFormed(rev, fwd, truth, out var wfm));
        foreach (var f in wfm.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Reader.CheckRaPhalaU(rev, fwd, truth, fam.Ra, out var rpu));
        foreach (var f in rpu.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Reader.CheckSyllables(rev, fwd, truth, fam.Ra, out var syl));
        foreach (var f in syl.Take(12)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Checks.USigns(fwd, rev, truth, out var usf));
        foreach (var f in usf.Take(12)) Console.WriteLine("    " + f);
        if (spellings is not null)
        {
            Console.WriteLine("  " + Reader.CheckCapture(rev, fam, spellings, out var cf));
            foreach (var f in cf.Take(12)) Console.WriteLine("    " + f);
        }

        // Quotes, and the ASCII punctuation that is left unmapped but whose byte draws a letter.
        foreach (var q in new[] { "\"উদ্ধৃতি\"", "'উদ্ধৃতি'", "সে বলল, \"ঠিক আছে।\"", "রাম's" })
        {
            var r = fwd.Convert(q);
            Console.WriteLine($"  quote {q}\t{Hex(r.Text)}\tback {rev.ConvertText(r.Text)}{(r.HasUnmapped ? "\tUNMAPPED " + string.Join("", r.UnmappedCharacters) : "")}");
        }
        var drawsLetter = new List<string>();
        var leftOut = new List<string>();
        // (the fonts' cmaps hold the Windows-1252 code points, so a character passed through draws the glyph at its byte)
        foreach (char c in Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Where(c => !(c >= 'ঀ' && c <= '৿')))
        {
            string wrote = fwd.ConvertText(c.ToString());
            if (wrote.Length == 0) { leftOut.Add(c.ToString()); continue; }
            string read = rev.ConvertText(wrote);
            if (read.Any(ch => ch >= 'ঀ' && ch <= '৿')) drawsLetter.Add($"{c}={read}");
        }
        Console.WriteLine($"  punctuation and symbols left out (reported): {string.Join(" ", leftOut)}");
        Console.WriteLine($"  punctuation and symbols written (reported) as a code that draws a Bengali glyph: {drawsLetter.Count} {string.Join(" ", drawsLetter)}");
        Console.WriteLine("  " + NonIndic.Check(fwd, rev, codes, "ক", out var ni, fam.IsFourFiveSix ? "ÿ" : ""));
        foreach (var f in ni.Take(10)) Console.WriteLine("    " + f);
        Console.WriteLine("  " + Ascii.Check(fwd, codes, fam.Itself, "ক", out var na));
        foreach (var f in na.Take(10)) Console.WriteLine("    " + f);
        // A .txt file in these fonts is Windows-1252 (FileConversion.WriteTxt, Encoding.GetEncoding(1252)): what becomes '?'.
        var q1252 = Fonts.Limits(fwd).Select(h => (char)Convert.ToInt32(h, 16)).Concat(written.Select(cp => (char)cp))
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

    /// <summary>Every character a reader could convert alone, and the truth and words: none may come out with a C1 code point.</summary>
    public static IEnumerable<string> Probe(IReadOnlyList<Truth> truth) =>
        truth.Select(t => t.U).Concat(Checks.MakeWords(truth, 20000, 7).Select(w => w.U)).Concat(RealWords)
            .Concat(Enumerable.Range(0x0980, 0x80).Concat(Enumerable.Range(0x20, 0x5F)).Concat(Enumerable.Range(0xA0, 0x60))
                .Concat(Enumerable.Range(0x2000, 0x70)).Concat(new[] { 0x20B9, 0x25CC }).Select(c => ((char)c).ToString()));

    public static List<string> C1Written(FontConverter fwd, IReadOnlyList<Truth> truth)
    {
        var bad = new List<string>();
        foreach (var u in Probe(truth))
        {
            string got = fwd.ConvertText(u);
            if (got.Any(IsC1)) bad.Add($"{u} [{Cps(u)}]\t{Hex(got)}");
        }
        return bad;
    }

    /// <summary>Codes a table writes that its family draws as something else (or not at all).</summary>
    public static List<string> Leaks(Fam fam, MappingTable table, FontConverter fwd, IReadOnlyList<Truth> truth)
    {
        var never = fam.NeverCodes;
        var bad = new List<string>();
        bool Bad(string legacy) => Hex(legacy.Replace(Rules.Keep, "")).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(c => never.Contains(c) || c.StartsWith("U+"));
        foreach (var p in table.Pairs) if (Bad(p.To)) bad.Add($"pair {p.From} -> {Hex(p.To)}");
        foreach (var r in table.PostRules) if (Bad(r.Replace.Replace("$1", "").Replace("$2", "").Replace("$$", "$"))) bad.Add($"rule {r.Pattern} -> {r.Replace}");
        foreach (var u in truth.Where(t => t.Pref.Length > 0).Select(t => t.U).Concat(Checks.MakeWords(truth, 5000, 99).Select(w => w.U)))
        {
            var r = fwd.Convert(u);
            // what passes through, reported, is not written by the table (ৱ in a Bangla font)
            if (Bad(new string(r.Text.Where(c => !r.UnmappedCharacters.Contains(c.ToString())).ToArray()))) bad.Add($"{u}\t{Hex(r.Text)}");
        }
        return bad;
    }
}
