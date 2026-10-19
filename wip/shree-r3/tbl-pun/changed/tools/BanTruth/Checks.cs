using FontSoftware.Core.Conversion;
using static Common;

public static class Checks
{
    /// <summary>
    /// Every truth string alone: preferred, another accepted spelling, or wrong. A string the
    /// family has no code for (an empty spelling: % or : in some) must pass through reported.
    /// </summary>
    public static string Truth(FontConverter conv, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        int pref = 0, acc = 0;
        fails = new List<string>();
        foreach (var t in truth)
        {
            var r = conv.Convert(t.U);
            string got = Hex(r.Text);
            // No glyph: left out, or passed through where the fonts do not hold the code at all
            // (tools/Shared/ShreeNonIndic.cs says which) - reported either way.
            if (t.Pref.Length == 0) { if ((r.Text == t.U || r.Text.Length == 0) && r.UnmappedCharacters.Contains(t.U)) pref++; else fails.Add($"{t.U}\tno glyph: want it left out or passed through, reported\tgot {got}"); continue; }
            // Reported: exactly the characters the spelling passes through (ৱ in a Bangla font), none else.
            if (!ReportsAsSpelled(r, t.Pref)) fails.Add($"{t.U}\t[{Cps(t.U)}]\tunmapped {string.Join("", r.UnmappedCharacters)}\tgot {got}");
            else if (got == t.Pref) pref++;
            else if (t.Acc.Contains(got)) acc++;
            else fails.Add($"{t.U}\t[{Cps(t.U)}]\twant {t.Pref}{(t.Acc.Length > 1 ? " (or " + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + ")" : "")}\tgot {got}");
        }
        return $"truth {truth.Count}: preferred {pref}, other accepted {acc}, wrong {fails.Count} ({100.0 * (pref + acc) / truth.Count:F2}%)";
    }

    /// <summary>
    /// Truth strings that can stand next to each other in a word: they start on a base
    /// (consonant, vowel, digit, sign of punctuation), do not end on a virama (which would
    /// join the next consonant) and have no sign straight after a virama.
    /// </summary>
    public static List<Truth> Syllables(IEnumerable<Truth> truth) =>
        truth.Where(t => t.U.Length > 0 && t.Pref.Length > 0 && !IsMark(t.U[0]) && !IsJoiner(t.U[0]) && !IsVirama(t.U[^1]) && !SignAfterVirama(t.U))
             .OrderBy(t => t.U, StringComparer.Ordinal).ToList();

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

    /// <summary>
    /// A hasanta that ends a word (P1, tools/Shared/ShreeWordFinal.cs): Shree's Bengali composer
    /// already answers every row that ends in a hasanta with the visible halant F4 (1262 of 1262 -
    /// `bantruth wordfinal` shows the shared rule would only reorder 109 of them), so the table writes
    /// the composer's spelling. Checked in running text: syllables, the row, then a space and a word,
    /// punctuation, a danda, a digit or the end - the row's own bytes with the halant in them, and
    /// what follows unchanged.
    /// </summary>
    public static string WordFinal(FontConverter conv, Fam fam, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        var pool = Syllables(truth);
        var finals = truth.Where(t => t.U.Length > 1 && IsVirama(t.U[^1]) && t.Pref.Length > 0).ToList();
        string halant = fam.Code("F4");
        fails = new List<string>();
        foreach (var t in finals.Where(t => !t.Pref.Split(' ').Contains(halant)))
            fails.Add($"{t.U}\t{t.Pref}\tno halant {halant}");
        string[] after = { "", " ", "।", "॥", ",", ".", "?", "১", "1", ")", "\n" };
        var rng = new Random(seed);
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            var parts = Enumerable.Range(0, rng.Next(3)).Select(_ => pool[rng.Next(pool.Count)]).ToList();
            var fin = finals[rng.Next(finals.Count)];
            string tail = after[rng.Next(after.Length)];
            var next = tail == " " ? pool[rng.Next(pool.Count)] : null;
            string u = string.Concat(parts.Select(p => p.U)) + fin.U + tail + (next?.U ?? "");
            string want = Norm(string.Join(" ", parts.Select(p => p.Pref).Append(fin.Pref).Append(Hex(conv.ConvertText(tail))).Append(next?.Pref ?? "")));
            string got = Hex(conv.ConvertText(u));
            if (got == want) ok++;
            else fails.Add($"{u.Replace("\n", "\\n")}\t[{Cps(u)}]\twant {want}\tgot {got}");
        }
        return $"word-final hasanta: rows {finals.Count} (every one with the halant {halant}: {finals.Count(t => t.Pref.Split(' ').Contains(halant))}); in running text {count} (seed {seed}): exact {ok}, wrong {fails.Count}";
    }

    /// <summary>
    /// Decision 6, checked: every truth row with ড় or ঢ় and ু or ূ (with ঁ ং ঃ, a reph, a ra-phala) is
    /// written with the sign's glyph E6 or E9, and u and uu with different bytes; read back, it is the text.
    /// </summary>
    public static string USigns(FontConverter conv, FontConverter reader, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        var re = new System.Text.RegularExpressions.Regex("[ড়ঢ়](্[রৰ])?[ুূ]");
        int n = 0, ok = 0;
        foreach (var t in truth.Where(t => re.IsMatch(t.U) && t.Pref.Length > 0))
        {
            n++;
            string got = Hex(conv.ConvertText(t.U));
            var toks = got.Split(' ');
            string sign = t.U.Contains('ু') ? "E6" : "E9";
            string back = reader.ConvertText(conv.ConvertText(t.U));
            string other = t.U.Contains('ু') ? t.U.Replace('ু', 'ূ') : t.U.Replace('ূ', 'ু');
            if (!toks.Contains(sign)) fails.Add($"{t.U}\t{got}\tno {sign}");
            else if (Hex(conv.ConvertText(other)) == got) fails.Add($"{t.U}\t{got}\twritten as {other}");
            else if (back != t.U && !(back.Replace('ৰ', 'র') == t.U.Replace('ৰ', 'র'))) fails.Add($"{t.U}\t{got}\treads {back}");
            else ok++;
        }
        return $"ড় ঢ় + ু ূ rows {n}: the sign drawn (E6/E9), u and uu apart, read back {ok}; wrong {fails.Count}";
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
