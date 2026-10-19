using FontSoftware.Core.Conversion;
using static Common;

public static class Checks
{
    /// <summary>
    /// Every truth string alone: preferred, another accepted spelling, or wrong. A string whose spelling passes a
    /// character through (₹ in a 4/5/6 font: "U+20B9") must come out so, reporting exactly that character.
    /// </summary>
    public static string Truth(FontConverter conv, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        int pref = 0, acc = 0;
        fails = new List<string>();
        foreach (var t in truth)
        {
            var r = conv.Convert(t.U);
            string got = Hex(r.Text);
            if (!ReportsAsSpelled(r, t.Pref)) fails.Add($"{t.U}\t[{Cps(t.U)}]\tunmapped {string.Join("", r.UnmappedCharacters)}\tgot {got}");
            else if (got == t.Pref) pref++;
            else if (t.Acc.Contains(got)) acc++;
            else fails.Add($"{t.U}\t[{Cps(t.U)}]\twant {t.Pref}{(t.Acc.Length > 1 ? " (or " + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + ")" : "")}\tgot {got}");
        }
        return $"truth {truth.Count}: preferred {pref}, other accepted {acc}, wrong {fails.Count} ({100.0 * pref / truth.Count:F2}% preferred)";
    }

    /// <summary>
    /// Truth strings that can stand next to each other in a word: they start on a base (consonant, vowel, digit,
    /// punctuation), do not end on a virama or joiner (which would join the next consonant) and have no sign straight
    /// after a virama.
    /// </summary>
    public static List<Truth> Syllables(IEnumerable<Truth> truth) =>
        truth.Where(t => t.U.Length > 0 && t.Pref.Length > 0 && !IsMark(t.U[0]) && !IsJoiner(t.U[0]) && !IsVirama(t.U[^1]) && !IsJoiner(t.U[^1])
                         && !SignAfterVirama(t.U))
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

    /// <summary>
    /// A virama that ends a word (P1, tools/Shared/ShreeWordFinal.cs): Shree's Odia composer answers every row that
    /// ends in a virama with the visible virama FA (typed with the link, the halant or the half code alike)
    /// - the visible halant a Unicode font draws there - so the table writes it. Checked in running text: syllables, the row,
    /// then a space and a word, punctuation, a danda, a digit or the end - the row's own bytes with the halant, and what
    /// follows unchanged.
    /// </summary>
    public static string WordFinal(FontConverter conv, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        var pool = Syllables(truth);
        var finals = truth.Where(t => t.U.Length > 1 && IsVirama(t.U[^1]) && t.Pref.Length > 0).ToList();
        fails = new List<string>();
        foreach (var t in finals.Where(t => !t.Pref.Split(' ').Contains("FA")))
            fails.Add($"{t.U}\t{t.Pref}\tno visible virama FA");
        string[] after = { "", " ", "।", ",", ".", "?", "୧", "1", ")", "\n" };
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
        return $"word-final virama: rows {finals.Count} (with the visible virama FA: {finals.Count(t => t.Pref.Split(' ').Contains("FA"))}); in running text {count} (seed {seed}): exact {ok}, wrong {fails.Count}";
    }
}
