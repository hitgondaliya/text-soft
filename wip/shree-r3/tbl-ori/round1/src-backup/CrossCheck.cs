using FontSoftware.Core.Conversion;
using static Common;

/// <summary>Hold-out: learn on all folds but one, convert the strings left out.</summary>
public static class CrossCheck
{
    /// <summary>
    /// The units text is built from are always known: single characters, a letter with a virama (and a joiner), and two
    /// letters joined by a virama - a subjoined form or one of the 77 ligatures, which nothing smaller predicts. Everything
    /// longer (a conjunct with a sign, a tail or a reph; three letters; e-signs; phalas) is held out in turn.
    /// </summary>
    public static bool Unit(string u) => u.Length <= 2 || Shape(u) is "CHN" or "CHJ" or "CHC";

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
