using FontSoftware.Core.Conversion;
using static Common;

public static class Checks
{
    /// <summary>Every truth string alone: preferred, another accepted spelling, or wrong.</summary>
    public static string Truth(FontConverter conv, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        int pref = 0, acc = 0;
        fails = new List<string>();
        foreach (var t in truth)
        {
            string got = Hex(conv.ConvertText(t.U));
            if (got == t.Pref) pref++;
            else if (t.Acc.Contains(got)) acc++;
            else fails.Add($"{t.U}\t[{Cps(t.U)}]\twant {t.Pref}{(t.Acc.Length > 1 ? " (or " + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + ")" : "")}\tgot {got}");
        }
        return $"truth {truth.Count}: preferred {pref}, other accepted {acc}, wrong {fails.Count} ({100.0 * (pref + acc) / truth.Count:F2}%)";
    }

    /// <summary>
    /// Truth strings that can stand next to each other in a word: they start on a
    /// base (consonant, vowel, digit, sign of punctuation) and do not end on a virama,
    /// which would join the next consonant into a conjunct.
    /// </summary>
    public static List<Truth> Syllables(IEnumerable<Truth> truth) =>
        truth.Where(t => t.U.Length > 0 && !IsMark(t.U[0]) && !IsJoiner(t.U[0]) && !IsVirama(t.U[^1]) && !SignAfterVirama(t.U))
             .OrderBy(t => t.U, StringComparer.Ordinal).ToList();

    /// <summary>A virama straight before a vowel sign (ર્ે): a reph or half letter with no letter to sit on.</summary>
    static bool SignAfterVirama(string u)
    {
        for (int i = 1; i < u.Length; i++)
            if (IsVirama(u[i - 1]) && IsMark(u[i]) && !IsVirama(u[i])) return true;
        return false;
    }

    public static List<(string U, string Want)> MakeWords(IReadOnlyList<Truth> truth, int count, int seed)
    {
        var pool = Syllables(truth);
        var rng = new Random(seed);
        var words = new List<(string, string)>(count);
        for (int i = 0; i < count; i++)
        {
            int n = 2 + rng.Next(3);
            var u = new System.Text.StringBuilder();
            var want = new List<string>();
            for (int k = 0; k < n; k++)
            {
                var s = pool[rng.Next(pool.Count)];
                u.Append(s.U);
                if (s.Pref.Length > 0) want.Add(s.Pref);
            }
            words.Add((u.ToString(), string.Join(" ", want)));
        }
        return words;
    }

    /// <summary>The rows the table writes with the halant at the end of a word (the composer answered them with its waiting half form).</summary>
    public static List<Truth> WordFinalRows(IEnumerable<Truth> truth) =>
        truth.Where(t => t.Composer is not null && t.U.Length > 1 && IsVirama(t.U[^1])).ToList();

    /// <summary>
    /// Words that end in a virama, in running text: syllables, then a row that ends in a
    /// virama, then a space and another word, punctuation, a danda, a digit or the end of
    /// the text. Each must be the syllables' bytes, the row's halant spelling and what follows.
    /// </summary>
    public static string WordFinal(FontConverter conv, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        var pool = Syllables(truth.Where(t => t.Pref.Length > 0));
        var finals = WordFinalRows(truth);
        string[] after = { "", " ", "।", "॥", ",", ".", "?", "૧", "1", ")", "\n" };
        var rng = new Random(seed);
        fails = new List<string>();
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            var parts = Enumerable.Range(0, rng.Next(3)).Select(_ => pool[rng.Next(pool.Count)]).ToList();
            var fin = finals[rng.Next(finals.Count)];
            string tail = after[rng.Next(after.Length)];
            var next = tail == " " ? pool[rng.Next(pool.Count)] : null;
            string u = string.Concat(parts.Select(p => p.U)) + fin.U + tail + (next?.U ?? "");
            string want = string.Concat(parts.Select(p => FromHex(p.Pref))) + FromHex(fin.Pref) + conv.ConvertText(tail) + (next is null ? "" : FromHex(next.Pref));
            string got = conv.ConvertText(u);
            if (got == want) ok++;
            else fails.Add($"{u.Replace("\n", "\\n")}\t[{Cps(u)}]\twant {Hex(want)}\tgot {Hex(got)}");
        }
        return $"word-final viramas in running text {count} (seed {seed}): exact {ok}, wrong {fails.Count}";
    }

    /// <summary>The Gujarati consonants ક-હ (U+0AA9 U+0AB1 U+0AB4 are not assigned).</summary>
    public static readonly string[] Consonants = Enumerable.Range(0x0A95, 0x0AB9 - 0x0A95 + 1)
        .Where(c => c is not (0x0AA9 or 0x0AB1 or 0x0AB4)).Select(c => ((char)c).ToString()).ToArray();

    static string RoundTrip(string u, FontConverter forward, FontConverter reverse, List<string> fails)
    {
        var r = forward.Convert(u);
        string back = reverse.ConvertText(r.Text);
        if (back != u || r.HasUnmapped) fails.Add($"{u}\t[{Cps(u)}]\t-> {Hex(r.Text)}\t-> {back} [{Cps(back)}]");
        return r.Text;
    }

    /// <summary>A reph on a ઙ conjunct (ઙ્ક ઙ્ખ ઙ્ગ ઙ્ઘ ઙ્ક્ષ - in 4/5/6 the codes 91 92 93 94 B6), with every sign, alone and before a letter: read back exactly.</summary>
    public static string RephOnNga(FontConverter forward, FontConverter reverse, out List<string> fails)
    {
        fails = new List<string>();
        string[] nga = { "ઙ્ક", "ઙ્ખ", "ઙ્ગ", "ઙ્ઘ", "ઙ્ક્ષ" };
        string[] signs = { "", "ા", "િ", "ી", "ુ", "ૂ", "ૃ", "ૅ", "ે", "ૈ", "ૉ", "ો", "ૌ", "ં", "ઁ", "ઃ", "ેં", "ૈં", "ીં", "િં", "ોં" };
        string[] after = { "", "ક", "કા", "મ" };
        int n = 0;
        foreach (var g in nga) foreach (var s in signs) foreach (var a in after) { n++; RoundTrip("ર્" + g + s + a, forward, reverse, fails); }
        return $"reph on a ઙ conjunct, {n} strings: read back exactly {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>The 4/5/6 codes of the ઙ conjuncts with a reph after them, as other Shree tools type them.</summary>
    public static string NgaRephBytes(FontConverter reverse456, out List<string> fails)
    {
        fails = new List<string>();
        var cases = new (string Hex, string Want)[]
        {
            ("91 AE", "ર્ઙ્ક"), ("92 AE", "ર્ઙ્ખ"), ("93 AE", "ર્ઙ્ગ"), ("94 AE", "ર્ઙ્ઘ"), ("B6 AE", "ર્ઙ્ક્ષ"),
            ("93 70 AE", "ર્ઙ્ગા"), ("93 7D", "ર્ઙ્ગી"), ("91 70 AE 4C 24", "ર્ઙ્કાક"), ("4C 24 92 A3", "કર્ઙ્ખે"), ("B6 70 AE", "ર્ઙ્ક્ષા"),
        };
        foreach (var (hex, want) in cases)
        {
            string got = reverse456.ConvertText(FromHex(hex));
            if (got != want) fails.Add($"{hex}\twant {want}\tgot {got} [{Cps(got)}]");
        }
        return $"4/5/6 bytes with a reph on a ઙ conjunct, {cases.Length}: right {cases.Length - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>
    /// A word that ends in a reph cluster and a virama (આર્ત્, ધર્મ્ છે): the reph sits on the
    /// last letter, before its halant - never on the letter before. Every consonant, after a
    /// vowel, a letter and a letter with a sign, at the end of the text and before a word.
    /// </summary>
    public static string RephBeforeFinalHalant(FontConverter forward, FontConverter reverse, out List<string> fails)
    {
        fails = new List<string>();
        string reph = FromHex("AE");
        int n = 0;
        foreach (var c in Consonants)
            foreach (var p in new[] { "આ", "ધ", "ઊ", "કા" })
                foreach (var tail in new[] { "", " છે" })
                {
                    n++;
                    string u = p + "ર્" + c + "્" + tail;
                    string bytes = RoundTrip(u, forward, reverse, fails);
                    if (bytes.StartsWith(forward.ConvertText(p) + reph, StringComparison.Ordinal))
                        fails.Add($"{u}\t-> {Hex(bytes)}: the reph on {p}");
                }
        return $"reph on a word-final virama cluster, {n} strings ({Consonants.Length} consonants): right {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>Every consonant with a nukta and each sign (and the ra-, ya-, va-signs): read back exactly, the nukta right after its letter.</summary>
    public static string NuktaReadBack(FontConverter forward, FontConverter reverse, out List<string> fails)
    {
        fails = new List<string>();
        string[] signs = { "", "ા", "િ", "ી", "ુ", "ૂ", "ૃ", "ૄ", "ૅ", "ે", "ૈ", "ૉ", "ો", "ૌ", "ં", "ઁ", "ઃ", "્ર", "્ય", "્વ", "િં", "ીં", "ેં", "ૈં", "ોં", "્રા", "્યા" };
        int n = 0;
        foreach (var c in Consonants) foreach (var s in signs) { n++; RoundTrip(c + "઼" + s, forward, reverse, fails); }
        return $"nukta + sign, {n} strings: read back exactly {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>
    /// A virama before ન after any cluster the truth has (a letter, a half letter and a
    /// letter, a ligature): kept, as before મ - the ન where the મ is (જ્ઞ્ન = 6F B9 5F as
    /// જ્ઞ્મ = 6F B9 64). Only ્ન with no letter before it is the composer's lone ન 5F.
    /// </summary>
    public static string ViramaBeforeNa(FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0;
        string ma = Hex(forward.ConvertText("મ")), na = Hex(forward.ConvertText("ન"));
        foreach (var t in truth.Where(t => t.U.Length > 0 && IsCons(t.U[^1]) && t.U.All(c => IsCons(c) || IsVirama(c))))
        {
            string m = Hex(forward.ConvertText(t.U + "્મ"));
            if (!m.EndsWith(" " + ma)) continue;   // a ligature with મ
            n++;
            string want = m[..^ma.Length] + na, got = Hex(forward.ConvertText(t.U + "્ન"));
            if (got != want) fails.Add($"{t.U}્ન\twant {want} (as {t.U}્મ = {m})\tgot {got}");
        }
        if (Hex(forward.ConvertText("્ન")) != "5F") fails.Add($"્ન alone: want 5F (the composer's), got {Hex(forward.ConvertText("્ન"))}");
        return $"virama before ન after {n} clusters: right {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>
    /// The ru/ruu ligatures (Policy.RuLigatures, the new ruu) both ways, in words - and never on a ra-sign:
    /// every truth cluster that ends in the ra-sign (ક્ર, સ્ત્ર ...) with u or uu keeps the ra-sign.
    /// </summary>
    public static string Ligatures(FontConverter forward, FontConverter reverse, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        var cases = Policy.RuLigatures.Select(l => (l.U, l.Hex)).Concat(new (string, string)[]
        {
            (Policy.NewRuu, "7E 24"), (Policy.NewRuu + "ં", "7E 84 24"), ("રુઃ", "7B 24 3A"), ("રૂઃ", "EA 24 3A"),
            ("ગુરુ", "4E 79 7B 24"), ("રૂપિયા", "EA 24 72 60 65 70"), ("શરૂ", "69 EA 24"), ("ગુરુવાર", "4E 79 7B 24 68 70 66"),
        }).ToList();
        int n = 0;
        foreach (var (u, hex) in cases)
        {
            n++;
            string got = Hex(forward.ConvertText(u));
            if (got != hex) fails.Add($"{u}: writes {got}, want {hex}");
            string back = reverse.ConvertText(FromHex(hex));
            if (back != u) fails.Add($"{hex}: reads {back} [{Cps(back)}], want {u}");
        }
        // (ર્ર before u/uu is the reph on the ru/ruu ligature, as Shree types E9 40 - see Policy.RuLigatures.)
        foreach (var t in truth.Where(t => t.U.EndsWith("્ર") && t.U.Length > 2 && t.U != "ર્ર"))
            foreach (var sign in new[] { "ુ", "ૂ" })
            {
                n++;
                string got = Hex(forward.ConvertText(t.U + sign));
                if (got.Split(' ').Any(h => h is "7B" or "EA" or "7E")) fails.Add($"{t.U}{sign}: writes {got}, a ru/ruu ligature on a ra-sign");
            }
        return $"ru/ruu ligatures, {n} strings: right {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>
    /// A word still being typed (the engine's marker Rules.Waiting after it): every truth row that ends in a
    /// virama is written as the composer shows it while it waits for the next letter - the half form, the reph.
    /// </summary>
    public static string Waiting(FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0;
        foreach (var t in truth)
        {
            if (Learner.Waiting(t) is not { } waits) continue;
            n++;
            var r = forward.Convert(t.U + Rules.Waiting);
            if (Hex(r.Text) != waits) fails.Add($"{t.U}+marker [{Cps(t.U)}]: writes {Hex(r.Text)}, the composer shows {waits}");
        }
        return $"words still being typed that end in a virama, {n}: as the composer shows them {n - fails.Count}, wrong {fails.Count}";
    }

    /// <summary>Shree's own spellings (S8Guj764 code runs, 2026-10-18) and the tables' policy spellings, read back (P5).</summary>
    public static string ReadsShree(FontConverter reverse, FontConverter forward, out List<string> fails)
    {
        fails = new List<string>();
        var cases = new (string Hex, string Want)[]
        {
            ("4C B9 24 4C 24", "ક્‌ક"), ("5F B9 5F", "ન્‌ન"), ("4C B9 24 73", "ક્‌ત"), ("66 B9 4C 24", "ર્‌ક"), ("4C B9 24 71 4C 24", "ક્‌કિ"),
            ("BC", "ક્‍"), ("BC 20 4C 24", "ક્‍ ક"), ("E5 2C", "બ્‍,"), ("BC 4C 24", "ક્ક"), ("50 B9 24 4C 24", "ઙ્ક"), ("6F B9 5F", "જ્ઞ્ન"),
            ("22 22 6C 70 27 27", "“હા”"), ("22 4C 24 27", "‘ક’"), ("4C 24 97 26 26", "ક॥"), ("4C 24 97 26", "ક।"),
            ("3C 31 32", "₹૧૨"), ("4C 24 90 4C 24", "ક‐ક"), ("4C 24 81 4C 24", "ક–ક"), ("4C 24 81 81 4C 24", "ક—ક"),
            ("31 8D 32", "૧÷૨"), ("4C 24 8F", "ક૰"), ("7E 24", Policy.NewRuu), ("7B 24", "રુ"), ("EA 24", "રૂ"),
            ("4C 24 3A", "કઃ"), ("4C 24 F8 65 79 84 3A 20 22 22 6C 70 27 27", "કહ્યું: “હા”"), ("31 30 3A 33 30", "૧૦:૩૦"), ("20 3A 20", " : "),
        };
        foreach (var (hex, want) in cases)
        {
            string got = reverse.ConvertText(FromHex(hex));
            if (got != want) fails.Add($"{hex}: reads {got} [{Cps(got)}], want {want} [{Cps(want)}]");
        }
        // What the tables write for each of those reads back the same (the policy's own spellings).
        foreach (var (_, want) in cases)
        {
            var w = forward.Convert(want);
            if (w.HasUnmapped) continue;
            string again = reverse.ConvertText(w.Text);
            if (forward.ConvertText(again) != w.Text) fails.Add($"{want}: writes {Hex(w.Text)}, reads {again}, writes {Hex(forward.ConvertText(again))}");
        }
        return $"Shree's spellings read back, {cases.Length}: right {cases.Length - fails.Count(f => !f.Contains("writes"))}, wrong {fails.Count}";
    }

    public static string Words(FontConverter conv, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        fails = new List<string>();
        int ok = 0;
        foreach (var (u, want) in MakeWords(truth, count, seed))
        {
            string got = Hex(conv.ConvertText(u));
            if (got == want) ok++;
            else fails.Add($"{u}\t[{Cps(u)}]\twant {want}\tgot {got}");
        }
        return $"words {count} (seed {seed}): exact {ok}, wrong {fails.Count} ({100.0 * ok / count:F2}%)";
    }
}
