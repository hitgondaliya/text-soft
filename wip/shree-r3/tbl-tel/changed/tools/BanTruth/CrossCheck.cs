using FontSoftware.Core.Conversion;
using static Common;

/// <summary>Hold-out: learn on all folds but one, convert the strings left out.</summary>
public static class CrossCheck
{
    /// <summary>
    /// The units text is built from are always known: single characters, pending letters
    /// (C + virama, with or without a joiner), and the ra- and ya-phala on one letter.
    /// Everything longer is held out in turn.
    /// </summary>
    public static bool Unit(string u) => u.Length <= 2 || Shape(u) is "CHN" or "CHJ" or "RHN" or "RHJ" or "CmH" ||
        (u.Length is 3 or 4 && (u.EndsWith("্র") || u.EndsWith("্য")) && IsCons(u[0])) ||
        u is "ক্ষ" or "জ্ঞ";   // ক্ষ and জ্ঞ: typed with a code of their own

    /// <summary>A bare cluster (letters and virama only): a ligature no rule can guess.</summary>
    public static bool BareCluster(string u) => System.Text.RegularExpressions.Regex.IsMatch(Shape(u), "^[CR](H[CR])+H?$");

    public static string Run(Fam fam, List<Truth> truth, int folds, int seed, int show, System.Text.RegularExpressions.Regex? only)
    {
        var rng = new Random(seed);
        var fold = truth.ToDictionary(t => t.U, t => Unit(t.U) ? -1 : rng.Next(folds), StringComparer.Ordinal);
        int ok = 0, n = 0, known = 0, knownOk = 0;
        var bad = new List<string>();
        var learner = new Learner(fam);
        for (int f = 0; f < folds; f++)
        {
            var pairs = learner.Learn(truth.Where(t => fold[t.U] != f), out _);
            var conv = new FontConverter(learner.Build(pairs));
            foreach (var t in truth.Where(t => fold[t.U] == f && t.U.Length > 1))
            {
                if (only is not null && !only.IsMatch(t.U)) continue;
                n++;
                string got = Hex(conv.ConvertText(t.U));
                bool right = got == t.Pref || t.Acc.Contains(got);
                // Its cluster (the letters without signs, reph or phala) is in the training set.
                string b = Base(t.U);
                if (b != t.U && b.Length > 1 && fold.TryGetValue(b, out int bf) && bf != f) { known++; if (right) knownOk++; }
                if (right) ok++;
                else bad.Add($"{Shape(t.U)}\t{t.U}\twant {t.Pref}\tgot {got}");
            }
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{fam.Id}: held out {n}: {ok} right ({100.0 * ok / n:F2}%), {bad.Count} wrong");
        // Strings built on a cluster the table knows (or needs no ligature): what the rules must get right.
        int bare = truth.Count(t => fold[t.U] >= 0 && t.U.Length > 1 && BareCluster(t.U) && (only is null || only.IsMatch(t.U)));
        int bareBad = bad.Count(b => BareCluster(b.Split('\t')[1]));
        sb.AppendLine($"  without the {bare} bare clusters (a ligature is learned, not guessed): {n - bare - (bad.Count - bareBad)}/{n - bare} right " +
                      $"({100.0 * (n - bare - (bad.Count - bareBad)) / (n - bare):F2}%)");
        sb.AppendLine($"  on a known cluster (the cluster in training, the string with its signs held out): {knownOk}/{known} right ({100.0 * knownOk / Math.Max(1, known):F2}%)");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(show)) sb.AppendLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        return sb.ToString();
    }

    /// <summary>The cluster of a syllable: without the reph, the signs and a trailing ra/ya-phala.</summary>
    public static string Base(string u)
    {
        string s = u.StartsWith("র্") && u.Length > 2 && IsCons(u[2]) ? u[2..] : u;
        s = new string(s.Where(c => !IsMark(c) || IsVirama(c)).ToArray());
        if (s.EndsWith("্র") || s.EndsWith("্য")) s = s[..^2];
        return s.TrimEnd((char)0x09CD);
    }
}
