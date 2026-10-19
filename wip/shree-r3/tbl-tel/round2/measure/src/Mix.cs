using System.Text;
using static U;

static partial class Program
{
    /// <summary>P5 on mixed running text: composer syllables, avagraha, danda, digits, punctuation, spaces - written, read back.</summary>
    static void Mix(string enc, int seed)
    {
        var fwd = Fwd[enc];
        var rev = Rev[enc];
        TruthCheck(enc);
        var pool = TruthRes[enc].Where(kv => IsSyllable(kv.Key) && kv.Value.Cls[0] is '1' or '2' or '3' or '4').Select(kv => kv.Key).ToList();
        string[] extra = { "\u0C3D", "\u0964", "\u0965", " ", ", ", ". ", "? ", "! ", "(", ")", "\u2018", "\u2019", "\u201C", "\u201D", "-", "\u2013", "\u2014", "\u0C67\u0C68", "12", ":", "%", "\u20B9", "*", "/" };
        var rng = new Random(seed);
        int n = 20000, same = 0, exact = 0, mal = 0, forb = 0;
        var bad = new List<string>();
        var byLast = new SortedDictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            var sb = new StringBuilder();
            int k = 3 + rng.Next(6);
            for (int j = 0; j < k; j++) sb.Append(rng.Next(3) == 0 ? extra[rng.Next(extra.Length)] : pool[rng.Next(pool.Count)]);
            string u = sb.ToString();
            var w = fwd.Convert(u);
            string back = rev.ConvertText(w.Text);
            string again = fwd.ConvertText(back);
            bool ok = again == w.Text;
            if (back == u) exact++;
            if (ok) same++;
            if (Malformed(back).Count > Malformed(u).Count) mal++;
            if (back.Any(c => Forbidden(c) is not null && !u.Contains(c))) forb++;
            if (!ok)
            {
                bad.Add($"{u}\t{Hex(w.Text)}\tread {back}\twritten again {Hex(again)}");
                string key = u.Contains('\u0C3D') ? "with avagraha" : "other";
                byLast[key] = byLast.GetValueOrDefault(key) + 1;
            }
        }
        Tsv($"mix-{enc}-{seed}.tsv", bad);
        Say($"[mix {enc} seed {seed}] {n} running texts (syllables + ఽ । ॥ digits punctuation spaces ₹ *): read back exact {exact}, same text {same}, wrong {n - same} ({string.Join(", ", byLast.Select(kv => kv.Key + " " + kv.Value))}); malformed added {mal}; forbidden chars {forb}");
        foreach (var b in bad.Where(b => !b.Split('\t')[0].Contains('\u0C3D')).Take(8)) Say("   " + b);
    }
}
