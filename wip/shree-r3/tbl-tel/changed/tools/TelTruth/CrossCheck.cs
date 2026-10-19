using FontSoftware.Core.Conversion;
using static Common;

/// <summary>Hold-out: learn on all folds but one, convert the strings left out.</summary>
public static class CrossCheck
{
    /// <summary>
    /// The units text is built from are always known: single characters, a letter with a sign or a tail (the
    /// letter-specific shapes, gunintham), a letter with the visible halant (its halant form), క్ష alone, with a sign or
    /// with the halant (one letter), and the ligature శ్రీ. Everything longer - clusters, the ra-vattu, signs and tails on
    /// clusters - is held out in turn.
    /// </summary>
    public static bool Unit(string u) => u.Length <= 2 || Shape(u) is "CHN" || u == "శ్రీ" ||
        (u.StartsWith("క్ష", StringComparison.Ordinal) && (u.Length == 3 || (u.Length == 4 && IsVowelSign(u[3])) || u == "క్ష్\u200C"));

    public static string Run(Fam fam, List<Truth> truth, int folds, int seed, int show, System.Text.RegularExpressions.Regex? only)
    {
        var rng = new Random(seed);
        var fold = truth.ToDictionary(t => t.U, t => Unit(t.U) ? -1 : rng.Next(folds), StringComparer.Ordinal);
        int ok = 0, n = 0;
        var bad = new List<string>();
        var learner = new Learner(fam);
        for (int f = 0; f < folds; f++)
        {
            var pairs = learner.Learn(truth.Where(t => fold[t.U] != f), out _);
            var conv = new FontConverter(learner.Build(pairs));
            foreach (var t in truth.Where(t => fold[t.U] == f))
            {
                if (only is not null && !only.IsMatch(t.U)) continue;
                n++;
                var r = conv.Convert(t.U);
                string got = Hex(r.Text);
                bool right = ReportsAsSpelled(r, t.Pref) && (got == t.Pref || t.Acc.Contains(got));
                if (right) ok++;
                else bad.Add($"{Shape(t.U)}\t{t.U}\twant {t.Pref}\tgot {got}");
            }
        }
        var sb = new System.Text.StringBuilder();
        int units = fold.Count(kv => kv.Value < 0);
        sb.AppendLine($"{fam.Id}: {folds}-fold hold-out (seed {seed}), {units} units always trained; held out {n}: {ok} right ({100.0 * ok / Math.Max(1, n):F2}%), {bad.Count} wrong");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(show)) sb.AppendLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        return sb.ToString();
    }
}
