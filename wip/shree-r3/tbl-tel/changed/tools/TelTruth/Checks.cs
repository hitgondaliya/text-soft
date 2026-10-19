using FontSoftware.Core.Conversion;
using static Common;

public static class Checks
{
    /// <summary>
    /// Every truth string alone: preferred, another accepted spelling, or wrong. A string whose spelling passes a
    /// character through ("U+XXXX") must come out so, reporting exactly that character. Also counts the strings whose
    /// preferred spelling is the composer's own (no documented exception).
    /// </summary>
    public static string Truth(FontConverter conv, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        int pref = 0, acc = 0, own = 0;
        fails = new List<string>();
        foreach (var t in truth)
        {
            var r = conv.Convert(t.U);
            string got = Hex(r.Text);
            if (!ReportsAsSpelled(r, t.Pref)) fails.Add($"{t.U}\t[{Cps(t.U)}]\tunmapped {string.Join("", r.UnmappedCharacters)}\tgot {got}");
            else if (got == t.Pref) { pref++; if (t.Composer is null) own++; }
            else if (t.Acc.Contains(got)) acc++;
            else fails.Add($"{t.U}\t[{Cps(t.U)}]\twant {t.Pref}{(t.Acc.Length > 1 ? " (or " + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + ")" : "")}\tgot {got}");
        }
        return $"truth {truth.Count}: preferred {pref} ({own} of them the composer's own bytes, {pref - own} a documented exception), other accepted {acc}, wrong {fails.Count} ({100.0 * pref / truth.Count:F2}% preferred)";
    }

    /// <summary>The composer's own bytes against what the table writes, ignoring the documented exceptions: how far the table is from Shree, byte for byte.</summary>
    public static string ComposerBytes(FontConverter conv, IReadOnlyList<Truth> truth)
    {
        int same = 0, n = 0;
        foreach (var t in truth.Where(t => t.Why != "word-final"))
        {
            n++;
            if (Hex(conv.ConvertText(t.U)) == (t.Composer ?? t.Pref)) same++;
        }
        return $"the composer's own preferred bytes, byte for byte: {same}/{n} ({100.0 * same / n:F2}%); the rest are the documented exceptions";
    }

    /// <summary>
    /// Truth strings that can stand next to each other in a word: they start on a base (consonant, vowel, digit,
    /// punctuation), do not end on a virama or joiner (which would join the next consonant) and have no sign straight
    /// after a virama.
    /// </summary>
    public static List<Truth> Syllables(IEnumerable<Truth> truth) =>
        truth.Where(t => t.U.Length > 0 && t.Pref.Length > 0 && !IsMark(t.U[0]) && !IsJoiner(t.U[0]) && !IsVirama(t.U[^1]) && !IsJoiner(t.U[^1])
                         && !SignAfterVirama(t.U) && t.Why != "word-final")
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
    /// A virama that ends a word (P1, tools/Shared/ShreeWordFinal.cs): every truth row C + ్ + ZWNJ (the composer's visible
    /// halant) gives the spelling of C + ్ at a word end. Checked in running text: syllables, the letter + virama, then a
    /// space and a word, punctuation, a danda, a digit or the end - the halant spelling, and what follows unchanged.
    /// </summary>
    public static string WordFinal(FontConverter conv, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        var pool = Syllables(truth);
        var finals = truth.Where(t => t.U.Length > 2 && t.U[^1] == '\u200C' && IsVirama(t.U[^2]) && t.Pref.Length > 0)
            .Select(t => new Truth(t.U[..^1], t.Pref, new[] { t.Pref })).ToList();
        fails = new List<string>();
        int alone = 0;
        foreach (var f in finals)
        {
            string got = Hex(conv.ConvertText(f.U));
            if (got != f.Pref) { alone++; fails.Add($"ALONE\t{f.U}\t[{Cps(f.U)}]\twant {f.Pref} (the halant)\tgot {got}"); }
        }
        string[] after = { "", " ", "।", ",", ".", "?", "1", ")", "\n" };
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
        return $"word-final virama: forms {finals.Count} (C + ్ + ZWNJ in the truth), alone wrong {alone}; in running text {count} (seed {seed}): exact {ok}, wrong {count - ok}";
    }

    /// <summary>Learner.Vattus against the truth: క్C = క + the vattu, for every consonant with a vattu.</summary>
    public static string Vattus(IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        string ka = byU["\u0C15"].Pref;
        foreach (var (u, hex) in Learner.Vattus)
        {
            if (!byU.TryGetValue("\u0C15" + u, out var t)) { fails.Add($"{u}: no truth for క{u}"); continue; }
            if (!t.Acc.Contains(ka + " " + hex)) fails.Add($"{u}: క{u} = {t.Pref}, not {ka} {hex}");
        }
        return $"vattus {Learner.Vattus.Length}: read off క్C {Learner.Vattus.Length - fails.Count}, wrong {fails.Count}";
    }
}
