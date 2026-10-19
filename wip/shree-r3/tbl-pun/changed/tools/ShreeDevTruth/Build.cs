using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>Learns, checks and writes the tables of one script (DEV7 also writes the 4/5/6 pair).</summary>
public static class Build
{
    public static void All(Script S, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var truth = S.LoadTruth();
        Console.WriteLine($"{S.Name}: {truth.Count} truth strings");

        // Unicode -> 7.
        var L = new Learner(S);
        var fwdPairs = L.Learn(truth, out var fs);
        Console.WriteLine("forward: " + fs);
        var fwd = L.Build(fwdPairs);
        var (q1, q2) = Quotes(S);
        NonIndic.Complete(fwd, S.Name, q1, q2);
        foreach (var l in NonIndic.Policy(fwd, S.Name, q1, q2, Policy.Overrides(S.Name), Policy.NoGlyph(S.Name))) Console.WriteLine("  policy: " + l);
        var conv = new FontConverter(fwd);

        // 7 -> Unicode.
        var Rd = new Reader(S, truth);
        var back = Rd.Learn(truth, conv, out var rs);
        Console.WriteLine("reverse: " + rs);
        foreach (var (code, u) in S.FontCodes) back.TryAdd(Rd.Staged(FromHex(code)), Rd.Visual(u));
        var rev = Rd.Build(back);

        Save(fwd, Path.Combine(outDir, S.ForwardId + ".json"));
        Save(rev, Path.Combine(outDir, S.ReverseId + ".json"));
        Console.WriteLine($"wrote {S.ForwardId}.json ({fwd.Pairs.Count} pairs, {fwd.PreRules.Count}+{fwd.PostRules.Count} rules), {S.ReverseId}.json ({rev.Pairs.Count} pairs, {rev.PreRules.Count}+{rev.PostRules.Count} rules)");

        if (S.Name != "dev7") return;

        // The 4/5/6 fonts (SHREE-DEV-*): the 7 composer never writes the four codes they draw
        // differently, so the forward table is the 7 one; the reverse reads those codes - and
        // the ligatures at U+0200-0218 - as the 4/5/6 fonts draw them.
        var fwd456 = L.Build(fwdPairs, "unicode-to-shree-dev456", "Unicode -> Shree-Lipi 4.0/5.0/6.0 Devanagari", "shree-dev456");
        // The 4/5/6 fonts also map U+0120-012F and U+0200-021C (their signs and ligatures).
        NonIndic.Complete(fwd456, "dev456", q1, q2);
        NonIndic.Policy(fwd456, "dev456", q1, q2, Policy.Overrides("dev456"), Policy.NoGlyph("dev456"));
        var back456 = new Dictionary<string, string>(back, StringComparer.Ordinal);
        foreach (var (code, _) in Dev7.SevenCopies) back456.Remove(FromHex(code));
        var pre456 = Dev7.ReversePre("91 92 B6 " + string.Join(" ", Enumerable.Range(0x200, 25).Select(c => $"U+{c:X4}")), "", "B3");
        var R456 = new Reader(Dev7.Script456(pre456), truth);
        foreach (var (code, u) in Dev7.FourFiveSixCodes) back456[R456.Staged(FromHex(code))] = R456.Visual(u);
        for (int i = 0; i < Dev7.FourFiveSixLigatures.Length; i++)
            back456[((char)(0x200 + i)).ToString()] = R456.Visual(Dev7.FourFiveSixLigatures[i]);
        var rev456 = R456.Build(back456, "shree-dev456-to-unicode", "Shree-Lipi 4.0/5.0/6.0 Devanagari -> Unicode", "shree-dev456", pre456);
        Save(fwd456, Path.Combine(outDir, "unicode-to-shree-dev456.json"));
        Save(rev456, Path.Combine(outDir, "shree-dev456-to-unicode.json"));
        Console.WriteLine($"wrote unicode-to-shree-dev456.json ({fwd456.Pairs.Count} pairs), shree-dev456-to-unicode.json ({rev456.Pairs.Count} pairs)");
    }

    /// <summary>The fonts' single quotes ‘ ’: 22 27 in DEV7 and DEV, 91 92 in SAN7 (as the composers write them).</summary>
    static (string, string) Quotes(Script S) => S.Name == "san7" ? ("91", "92") : ("22", "27");

    /// <summary>Every check, on the tables as written (loaded back from disk).</summary>
    public static bool Verify(Script S, string dir)
    {
        var truth = S.LoadTruth();
        bool ok = true;
        var fwd = new FontConverter(LoadTable(Path.Combine(dir, S.ForwardId + ".json")));
        var rev = new FontConverter(LoadTable(Path.Combine(dir, S.ReverseId + ".json")));
        var Rd = new Reader(S, truth);
        Console.WriteLine($"== {S.Name} (tables in {dir})");
        Console.WriteLine($"   forward pairs {fwd.Table.Pairs.Count}, rules {fwd.Table.PreRules.Count}+{fwd.Table.PostRules.Count}; reverse pairs {rev.Table.Pairs.Count}, rules {rev.Table.PreRules.Count}+{rev.Table.PostRules.Count}");

        string r = Checks.Truth(fwd, truth, out var tf, S.Excluded);
        Console.WriteLine("   " + r); ok &= tf.Count == 0 && r.Contains("other accepted 0");
        foreach (var f in tf.Take(10)) Console.WriteLine("      " + f);
        Console.WriteLine("   as the composer itself writes them (the rows the table writes otherwise left out): " +
                          Checks.Truth(fwd, truth.Where(t => t.Composer is null).ToList(), out _, S.Excluded));
        foreach (int seed in new[] { 7, 11, 2026 })
        {
            Console.WriteLine("   " + Checks.Words(fwd, truth.Where(t => S.Excluded(t) is null).ToList(), 20000, seed, out var wf)); ok &= wf.Count == 0;
            foreach (var f in wf.Take(5)) Console.WriteLine("      " + f);
        }
        Console.WriteLine($"   word-final rows (the composer's waiting half form, written with the halant): {Checks.WordFinalRows(truth).Count}");
        foreach (int seed in new[] { 5, 13 })
        {
            Console.WriteLine("   " + Checks.WordFinal(fwd, truth.Where(t => S.Excluded(t) is null).ToList(), 20000, seed, out var ff)); ok &= ff.Count == 0;
            foreach (var f in ff.Take(5)) Console.WriteLine("      " + f);
        }
        Console.WriteLine("   " + RealWords(S, fwd, rev, truth, out bool rw)); ok &= rw;

        Console.WriteLine("   " + Rd.Check(rev, fwd, truth, out var rf)); ok &= rf.Count == 0;
        foreach (var f in rf.Take(10)) Console.WriteLine("      " + f);
        foreach (int seed in new[] { 7, 11 })
        {
            Console.WriteLine("   " + Rd.CheckWords(rev, fwd, truth, 20000, seed, out var wf)); ok &= wf.Count == 0;
            foreach (var f in wf.Take(5)) Console.WriteLine("      " + f);
        }
        Console.WriteLine("   " + Rd.CheckAccepted(rev, fwd, truth, out var af));
        foreach (var f in af.Take(5)) Console.WriteLine("      " + f);

        Console.WriteLine("   " + Adjacent(S, fwd, rev, truth, out var adj)); ok &= adj.Count == 0;
        foreach (var f in adj.Take(8)) Console.WriteLine("      " + f);

        // Family safety: never a code that draws something else in a sister family.
        var leaks = Leaks(fwd, S.NeverWrite, truth);
        Console.WriteLine($"   codes {string.Join(" ", S.NeverWrite)} written: {leaks.Count}"); ok &= leaks.Count == 0;
        foreach (var l in leaks.Take(5)) Console.WriteLine("      " + l);

        // Text that is not Devanagari never lands on a code the fonts draw as a letter.
        Console.WriteLine("   " + NonIndic.Check(fwd, rev, S.Name, "क", out var ni)); ok &= ni.Count == 0;
        var (aq1, aq2) = Quotes(S);
        Console.WriteLine("   " + NonIndic.CheckAscii(fwd, rev, S.Name, aq1, aq2, "क", out var na)); ok &= na.Count == 0;
        foreach (var f in na.Take(8)) Console.WriteLine("      " + f);
        foreach (var f in ni.Take(8)) Console.WriteLine("      " + f);
        // P3b: the characters the fonts have no glyph for pass through as themselves, reported - and the
        // rest of the text is written exactly as the table spells it without them.
        var noGlyph = Policy.NoGlyph(S.Name);
        var asComposer = new FontConverter(NonIndic.AsComposer(fwd.Table, noGlyph));
        var pInputs = noGlyph.SelectMany(g => new[] { g, "क" + g + "क", "र्" + g, g + "ं", "कि" + g, "क्" + g, g + " क", "र्क" + g + "ं" })
            .Concat(truth.Select(t => t.U)).ToList();
        Console.WriteLine("   " + NonIndic.CheckPassThrough(fwd, asComposer, pInputs, noGlyph, out var pt)); ok &= pt.Count == 0;
        foreach (var f in pt.Take(8)) Console.WriteLine("      " + f);
        if (S.Name == "san7")
        {
            // The nukta (no glyph; the composer drops it): the letter as without it, and the nukta reported - never silently.
            // (But ऱ before a virama: the nukta keeps it from becoming a reph, as the eyelash ra in DEV7 - र with its
            // halant, or with the ra-sign before र; only reported, so it is checked for the report alone.)
            int nk = 0; var nf = new List<string>();
            foreach (var (pre, letter) in Dev7.NuktaLetters)
                foreach (var w in new[] { "", "ि", "ा", "्", "्ष", "्र", "ं" }.Where(s => !(letter == "र" && s.StartsWith(Dev7.Virama)))
                             .Select(s => (With: letter + Dev7.Nukta + s, Pre: pre + s, Without: letter + s))
                             .SelectMany(x => new[] { (x.With, x.Without), (x.Pre, x.Without), ("र्" + x.With, "र्" + x.Without), ("क" + x.With + "क", "क" + x.Without + "क") }))
                {
                    nk++;
                    var rn = fwd.Convert(w.Item1);
                    if (rn.Text != fwd.ConvertText(w.Item2) || !rn.UnmappedCharacters.Contains(Dev7.Nukta))
                        nf.Add($"{w.Item1}: writes {Hex(rn.Text)}{(rn.UnmappedCharacters.Contains(Dev7.Nukta) ? "" : ", nukta not reported")}, without the nukta {Hex(fwd.ConvertText(w.Item2))}");
                }
            foreach (var w in new[] { "ऱ्क", "र" + Dev7.Nukta + "्ष", "कऱ्रक" })
            {
                nk++;
                if (!fwd.Convert(w).UnmappedCharacters.Contains(Dev7.Nukta)) nf.Add($"{w}: the nukta not reported");
            }
            Console.WriteLine($"   nukta {nk} strings: written as without it and reported {nk - nf.Count}, wrong {nf.Count}"); ok &= nf.Count == 0;
            foreach (var f in nf.Take(8)) Console.WriteLine("      " + f);
        }
        Console.WriteLine("   " + HalantReadBack(fwd, rev, out var hr)); ok &= hr.Count == 0;
        foreach (var f in hr.Take(8)) Console.WriteLine("      " + f);

        if (S.Name == "dev7")
        {
            var fwd456 = new FontConverter(LoadTable(Path.Combine(dir, "unicode-to-shree-dev456.json")));
            var rev456 = new FontConverter(LoadTable(Path.Combine(dir, "shree-dev456-to-unicode.json")));
            Console.WriteLine("== dev456");
            r = Checks.Truth(fwd456, truth, out tf, S.Excluded);
            Console.WriteLine("   " + r); ok &= tf.Count == 0;
            Console.WriteLine("   " + Checks.Words(fwd456, truth.Where(t => S.Excluded(t) is null).ToList(), 20000, 2026, out var wf)); ok &= wf.Count == 0;
            Console.WriteLine("   " + Checks.WordFinal(fwd456, truth.Where(t => S.Excluded(t) is null).ToList(), 20000, 5, out var ff456)); ok &= ff456.Count == 0;
            leaks = Leaks(fwd456, S.NeverWrite, truth);
            Console.WriteLine($"   codes 91 92 B3 B6 written: {leaks.Count}"); ok &= leaks.Count == 0;
            Console.WriteLine("   " + Rd.Check(rev456, fwd456, truth, out rf)); ok &= rf.Count == 0;
            foreach (var f in rf.Take(10)) Console.WriteLine("      " + f);
            Console.WriteLine("   " + Rd.CheckWords(rev456, fwd456, truth, 20000, 7, out wf)); ok &= wf.Count == 0;
            Console.WriteLine("   " + NonIndic.Check(fwd456, rev456, "dev456", "क", out var ni456)); ok &= ni456.Count == 0;
            Console.WriteLine("   " + NonIndic.CheckAscii(fwd456, rev456, "dev456", aq1, aq2, "क", out var na456)); ok &= na456.Count == 0;
            foreach (var f in na456.Take(8)) Console.WriteLine("      " + f);
            foreach (var f in ni456.Take(8)) Console.WriteLine("      " + f);
            Console.WriteLine("   " + HalantReadBack(fwd456, rev456, out var hr456)); ok &= hr456.Count == 0;
            foreach (var f in hr456.Take(8)) Console.WriteLine("      " + f);
            foreach (var (code, u) in Dev7.FourFiveSixCodes.Select(c => (c.Hex, c.Unicode)))
            {
                string g7 = rev.ConvertText(FromHex(code)), g456 = rev456.ConvertText(FromHex(code));
                string w7 = Dev7.SevenCopies.First(c => c.Hex == code).Unicode;
                Console.WriteLine($"   {code}: 7 reads {g7} (want {w7}), 4/5/6 reads {g456} (want {u})");
                ok &= g7 == w7 && g456 == u;
            }
            for (int i = 0; i < Dev7.FourFiveSixLigatures.Length; i++)
            {
                string got = rev456.ConvertText(((char)(0x200 + i)).ToString());
                if (got != Dev7.FourFiveSixLigatures[i]) { Console.WriteLine($"   U+{0x200 + i:X4}: got {got}, want {Dev7.FourFiveSixLigatures[i]}"); ok = false; }
            }
        }
        Console.WriteLine(ok ? "   ALL CHECKS PASS" : "   SOME CHECKS FAIL");
        return ok;
    }

    /// <summary>
    /// Every one-character syllable (letters, vowels, digits, signs of punctuation) before
    /// 4000 other syllables: both ways the pair must convert as its two halves do alone.
    /// </summary>
    static string Adjacent(Script S, FontConverter fwd, FontConverter rev, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        var pool = Checks.Syllables(truth.Where(t => S.Excluded(t) is null && t.Pref.Length > 0));
        var leads = pool.Where(t => t.U.Length == 1).ToList();
        var rng = new Random(5);
        var tails = Enumerable.Range(0, 4000).Select(_ => pool[rng.Next(pool.Count)]).ToList();
        var fAlone = new Dictionary<string, string>(StringComparer.Ordinal);
        var rAlone = new Dictionary<string, string>(StringComparer.Ordinal);
        string F(Truth t) => fAlone.TryGetValue(t.U, out var v) ? v : fAlone[t.U] = fwd.ConvertText(t.U);
        string Rv(Truth t) => rAlone.TryGetValue(t.Pref, out var v) ? v : rAlone[t.Pref] = rev.ConvertText(FromHex(t.Pref));
        int n = 0, dashes = 0;
        foreach (var a in leads)
            foreach (var b in tails)
            {
                n++;
                string f = fwd.ConvertText(a.U + b.U);
                if (f != F(a) + F(b)) fails.Add($"forward {a.U}+{b.U}: {Hex(f)}, alone {Hex(F(a))} + {Hex(F(b))}");
                string r = rev.ConvertText(FromHex(a.Pref + " " + b.Pref));
                // The em dash is two en dash glyphs: a run of dashes reads back two at a time.
                if (r != Rv(a) + Rv(b) && a.U is "–" or "—" && b.U is "–" or "—") { dashes++; continue; }
                if (r != Rv(a) + Rv(b)) fails.Add($"reverse {a.Pref} + {b.Pref}: {r}, alone {Rv(a)} + {Rv(b)}");
            }
        return $"adjacent syllables {n} ({leads.Count} one-character syllables before 4000 others), both ways: wrong {fails.Count}{(dashes > 0 ? $" (and {dashes} dash runs, which read two dashes at a time)" : "")}";
    }

    /// <summary>
    /// A word that ends in a virama, before nothing, a space, punctuation, a danda or a digit:
    /// written with the halant and read back as the plain virama it was typed with - no ZWNJ.
    /// Inside a word an explicit halant (्+ZWNJ) keeps its ZWNJ. Every consonant.
    /// </summary>
    static string HalantReadBack(FontConverter fwd, FontConverter rev, out List<string> fails)
    {
        fails = new List<string>();
        var cons = Enumerable.Range(0x0915, 0x0939 - 0x0915 + 1).Where(c => c is not (0x0929 or 0x0931 or 0x0934)).Select(c => ((char)c).ToString()).ToList();
        int n = 0;
        foreach (var c in cons)
            foreach (var pre in new[] { "", "अ", "सा" })
                foreach (var tail in new[] { "", " ", ",", ".", "।", "1", "१", " इति", "\n" })
                {
                    n++;
                    string u = pre + c + "्" + tail;
                    string back = rev.ConvertText(fwd.ConvertText(u));
                    // What follows reads as the font draws it (an ASCII digit is the font's १).
                    string want = pre + c + "्" + rev.ConvertText(fwd.ConvertText(tail));
                    if (back != want) fails.Add($"{u.Replace("\n", "\\n")}\t-> {Hex(fwd.ConvertText(u))}\t-> {back.Replace("\n", "\\n")} [{Cps(back)}]");
                }
        foreach (var c in cons)
        {
            string u = c +"्\u200Cक";
            // A letter with no half form is written so before a letter anyway (ङ्क = ङ्‌क, and ङ्‍क in SAN7): nothing tells them apart.
            if (fwd.ConvertText(u) == fwd.ConvertText(u.Replace("‌", "")) || fwd.ConvertText(u) == fwd.ConvertText(u.Replace("‌", "‍"))) continue;
            n++;
            string back = rev.ConvertText(fwd.ConvertText(u));
            if (back != u) fails.Add($"{u}\t-> {Hex(fwd.ConvertText(u))}\t-> {back} [{Cps(back)}]");
        }
        return $"word-final halant read back, {n} strings: right {n - fails.Count}, wrong {fails.Count}";
    }

    static List<string> Leaks(FontConverter fwd, HashSet<string> never, IReadOnlyList<Truth> truth)
    {
        var bad = new List<string>();
        var chars = never.Select(h => FromHex(h)[0]).ToArray();
        foreach (var p in fwd.Table.Pairs.Where(p => p.To.IndexOfAny(chars) >= 0)) bad.Add($"pair {p.From} -> {Hex(p.To)}");
        foreach (var rr in fwd.Table.PostRules.Where(rr => rr.Replace.IndexOfAny(chars) >= 0)) bad.Add($"rule {rr.Pattern} -> {rr.Replace}");
        foreach (var u in truth.Select(t => t.U).Concat(Checks.MakeWords(truth.Where(t => t.Pref.Length > 0).ToList(), 5000, 99).Select(w => w.U)))
        {
            string s = fwd.ConvertText(u);
            if (s.IndexOfAny(chars) >= 0) bad.Add($"{u} -> {Hex(s)}");
        }
        return bad;
    }

    /// <summary>Real words: their bytes are the truth syllables' bytes joined, and they read back.</summary>
    static string RealWords(Script S, FontConverter fwd, FontConverter rev, IReadOnlyList<Truth> truth, out bool ok)
    {
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        int right = 0, n = 0;
        var notes = new List<string>();
        foreach (var w in Checks.RealWords)
        {
            n++;
            // Split into the longest truth syllables, left to right.
            var parts = new List<Truth>();
            int i = 0;
            while (i < w.Length)
            {
                Truth? hit = null;
                for (int len = Math.Min(8, w.Length - i); len > 0 && hit is null; len--)
                    if (byU.TryGetValue(w.Substring(i, len), out var t) && (i + len == w.Length || !IsMark(w[i + len]))) hit = t;
                if (hit is null) break;
                parts.Add(hit); i += hit.U.Length;
            }
            if (i < w.Length) { notes.Add($"{w}: no truth split"); continue; }
            string want = string.Join(" ", parts.Select(p => p.Pref));
            string got = Hex(fwd.ConvertText(w));
            string back = rev.ConvertText(FromHex(got));
            bool good = got == want && Nukta.Spell(back) == Nukta.Spell(w);
            if (good) right++;
            notes.Add($"{w} = {string.Join(" + ", parts.Select(p => p.U))}: {got}{(got == want ? "" : " WANT " + want)}{(Nukta.Spell(back) == Nukta.Spell(w) ? "" : " READS " + back)}");
        }
        ok = right == n;
        return $"real words {n}: right {right}\n      " + string.Join("\n      ", notes);
    }
}
