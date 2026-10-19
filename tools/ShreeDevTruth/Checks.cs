using FontSoftware.Core.Conversion;
using static Common;

public static class Checks
{
    /// <summary>Every truth string alone: preferred, another accepted spelling, or wrong.</summary>
    public static string Truth(FontConverter conv, IReadOnlyList<Truth> truth, out List<string> fails, Func<Truth, string?>? excluded = null)
    {
        int pref = 0, acc = 0, skipped = 0;
        fails = new List<string>();
        foreach (var t in truth)
        {
            if (excluded?.Invoke(t) is not null) { skipped++; continue; }
            string got = Hex(conv.ConvertText(t.U));
            if (got == t.Pref) pref++;
            else if (t.Acc.Contains(got)) acc++;
            else fails.Add($"{Shape(t.U)}\t{t.U}\t[{Cps(t.U)}]\twant {t.Pref}{(t.Acc.Length > 1 ? " (or " + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + ")" : "")}\tgot {got}");
        }
        int n = truth.Count - skipped;
        return $"truth {n}: preferred {pref}, other accepted {acc}, wrong {fails.Count} ({100.0 * pref / n:F2}% preferred){(skipped > 0 ? $", {skipped} excluded" : "")}";
    }

    /// <summary>
    /// Truth strings that can stand next to each other in a word: they start on a
    /// base (consonant, vowel, digit, punctuation) and do not end on a virama or joiner,
    /// which would join the next consonant into a conjunct.
    /// </summary>
    public static List<Truth> Syllables(IEnumerable<Truth> truth) =>
        truth.Where(t => t.U.Length > 0 && !IsMark(t.U[0]) && !IsJoiner(t.U[0]) && !IsVirama(t.U[^1]) && !IsJoiner(t.U[^1]) && !SignAfterVirama(t.U))
             .OrderBy(t => t.U, StringComparer.Ordinal).ToList();

    /// <summary>A virama straight before a vowel sign (र्े): a reph or half letter with no letter to sit on.</summary>
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
        string[] after = { "", " ", "।", "॥", ",", ".", "?", "१", "1", ")", "\n" };
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

    /// <summary>Real words, with the bytes the truth syllables say (null when a syllable is not in the truth).</summary>
    public static readonly string[] RealWords =
    {
        "कर्म", "धर्म", "प्रकाश", "क्षत्रिय", "ज्ञान", "श्री", "द्वार", "हिंदी", "किताब", "स्थिर",
        "राष्ट्र", "विद्यार्थी", "अर्ध", "पृथ्वी", "रुपया", "रूप", "ड्रामा", "ट्रक", "कृपा", "हृदय",
    };
}
