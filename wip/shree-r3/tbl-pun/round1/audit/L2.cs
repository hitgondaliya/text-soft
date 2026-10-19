using System.Text;
using System.Text.Json;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;

// Words typed key by key (adapted from the Gujarati audit's L2: r3\guj-audit\harness\L2.cs).
static partial class Program
{
    sealed record Unit(string Keys, string Text, int Rank);

    static bool IsCons(char c) => (c >= 'ਕ' && c <= 'ਹ') || (c >= 'ਖ਼' && c <= 'ਫ਼');
    static bool IsSign(char c) => (c >= 'ਁ' && c <= 'ਃ') || c == '਼' || (c >= 'ਾ' && c <= 'ੌ') || c is 'ੰ' or 'ੱ' or 'ੑ' or 'ੵ';

    static List<Unit> Units(KeyboardLayout layout)
    {
        var units = new List<Unit>();
        foreach (var (vk, normal, shift) in ShreeKeyboardFile.PhysicalKeys)
        {
            var m = layout.Find(vk);
            if (m is null) continue;
            if (!string.IsNullOrEmpty(m.Base)) units.Add(new(normal.ToString(), m.Base!, 0));
            string? sh = m.Shift ?? m.Base;
            if (!string.IsNullOrEmpty(sh)) units.Add(new(shift.ToString(), sh!, 0));
        }
        foreach (var s in layout.Sequences) if (!string.IsNullOrEmpty(s.Keys) && !string.IsNullOrEmpty(s.To)) units.Add(new(s.Keys, s.To, 1));
        foreach (var s in layout.Escape) if (!string.IsNullOrEmpty(s.To)) units.Add(new(EscapeLayer.Mark + s.Keys, s.To, 2));
        foreach (var s in layout.Symbols) if (!string.IsNullOrEmpty(s.To)) units.Add(new(EscapeLayer.SymbolMark + s.Keys, s.To, 2));
        var extra = new List<Unit>();
        foreach (var u in units)
            if (u.Text.Length == 1 && SignOfVowel(u.Text[0]) is { } sign) extra.Add(new(u.Keys, sign.ToString(), 3));
        units.AddRange(extra);
        return units.Distinct().ToList();
    }

    static char? SignOfVowel(char c) => c switch
    {
        'ਆ' => 'ਾ', 'ਇ' => 'ਿ', 'ਈ' => 'ੀ', 'ਉ' => 'ੁ', 'ਊ' => 'ੂ', 'ਏ' => 'ੇ', 'ਐ' => 'ੈ', 'ਓ' => 'ੋ', 'ਔ' => 'ੌ', _ => null,
    };

    /// <summary>The word in the order this keyboard strikes it: a pre-sign (the i-sign) before its cluster.</summary>
    static string TypedOrder(string w, TypingOrder order)
    {
        if (!order.Active) return w;
        var pre = order.PreSigns.Where(p => p.Length == 1).Select(p => p[0]).ToHashSet();
        var sb = new StringBuilder();
        int i = 0;
        while (i < w.Length)
        {
            if (IsCons(w[i]))
            {
                int cs = i;
                i++;
                while (i < w.Length && (w[i] == '਼' || (w[i] == '੍' && i + 1 < w.Length && IsCons(w[i + 1]))))
                    i += w[i] == '੍' ? 2 : 1;
                if (i < w.Length && w[i] == '੍') { i++; if (i < w.Length && (w[i] == '‍' || w[i] == '‌')) i++; }
                string cluster = w[cs..i];
                var signs = new StringBuilder();
                while (i < w.Length && IsSign(w[i])) signs.Append(w[i++]);
                string sg = signs.ToString();
                sb.Append(new string(sg.Where(pre.Contains).ToArray())).Append(cluster).Append(new string(sg.Where(s => !pre.Contains(s)).ToArray()));
                continue;
            }
            sb.Append(w[i++]);
        }
        return sb.ToString();
    }

    static IEnumerable<string> Plans(string target, List<Unit> units, int max)
    {
        var byFirst = units.GroupBy(u => u.Text[0]).ToDictionary(g => g.Key, g => g.OrderBy(u => u.Rank).ThenByDescending(u => u.Text.Length).ThenBy(u => u.Keys.Length).ToList());
        var found = new List<string>();
        var stack = new List<string>();
        int budget = 20000;
        void Dfs(int pos)
        {
            if (found.Count >= max || --budget < 0) return;
            if (pos == target.Length) { found.Add(string.Concat(stack)); return; }
            if (!byFirst.TryGetValue(target[pos], out var list)) return;
            foreach (var u in list)
            {
                if (pos + u.Text.Length > target.Length || string.CompareOrdinal(target, pos, u.Text, 0, u.Text.Length) != 0) continue;
                stack.Add(u.Keys);
                Dfs(pos + u.Text.Length);
                stack.RemoveAt(stack.Count - 1);
                if (found.Count >= max) return;
            }
        }
        Dfs(0);
        return found.OrderBy(p => p.Length);
    }

    static string TypeWith(KeyboardLayout layout, Transliterator? tr, FontConverter? conv, string font, string keys, out List<KeyStep> steps)
    {
        using var t = new Typist(layout, tr, conv, font);
        foreach (char k in keys) t.Key(k);
        steps = t.Steps;
        return t.App.Buf.ToString();
    }

    /// <summary>Keys that type <paramref name="w"/> on this layout in Unicode, verified on the real engine; null when none found.</summary>
    static string? PlanWord(KeyboardLayout layout, Transliterator? tr, List<Unit> units, string w)
    {
        // vowels typed as a carrier plus a sign
        static string Split(string s) => s.Replace("ਆ", "ਅਾ").Replace("ਐ", "ਅੈ").Replace("ਔ", "ਅੌ").Replace("ਈ", "ੲੀ").Replace("ਏ", "ੲੇ").Replace("ਇ", "ਿੲ").Replace("ਉ", "ੳੁ").Replace("ਊ", "ੳੂ");
        // nukta letters typed as letter + nukta
        string decomposed = w.Normalize(NormalizationForm.FormD);
        var targets = new List<string>();
        foreach (string t0 in new[] { TypedOrder(w, layout.Order), w, TypedOrder(decomposed, layout.Order), decomposed }) { targets.Add(t0); targets.Add(Split(t0)); }
        foreach (string target in targets.Distinct())
            foreach (var plan in Plans(target, units, 60))
            {
                string got = TypeWith(layout, tr, null, "Unicode", plan, out _);
                if (got == w || got == decomposed || got.Normalize(NormalizationForm.FormC) == w.Normalize(NormalizationForm.FormC)) return plan;
            }
        return null;
    }

    static int L2(string[] args)
    {
        string wordsFile = Path.GetFullPath(Path.Combine(Repo, "..", "..", "cap-pun", "common-words.txt"));
        var words = File.ReadAllLines(wordsFile, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        string[] only = args.Length > 1 ? args[1].Split(',') : Array.Empty<string>();
        var outRows = new List<object>();
        var summary = new List<object>();
        foreach (var lref in Pa.Layouts)
        {
            if (only.Length > 0 && !only.Contains(lref.Id)) continue;
            var layout = Bundle.GetLayout(lref);
            if (layout.Phonetic) { summary.Add(new { layout = lref.Id, skipped = "phonetic (type-by-sound) layout: no fixed key plan" }); continue; }
            var units = Units(layout);
            int planned = 0, finalBad = 0, stepBad = 0, bsBad = 0, n = 0, bsPositions = 0;
            var unplannedWords = new List<string>();
            foreach (string w in words)
            {
                n++;
                string? plan = PlanWord(layout, null, units, w);
                if (plan is null) { unplannedWords.Add(w); continue; }
                planned++;
                var rec = new Dictionary<string, object?> { ["layout"] = lref.Id, ["word"] = w, ["plan"] = plan };
                string uniFinal = TypeWith(layout, null, null, "Unicode", plan, out var uniSteps);
                rec["unicode"] = UHex(uniFinal);
                foreach (var (pname, conv, font) in Profiles.Where(p => p.Conv is not null))
                {
                    string final = TypeWith(layout, null, conv, font, plan, out var steps);
                    string expect = conv!.ConvertText(uniFinal);
                    var prof = new Dictionary<string, object?> { ["final"] = Hex(final), ["converter"] = Hex(expect), ["ok"] = final == expect };
                    if (final != expect) finalBad++;
                    var stepIssues = new List<object>();
                    for (int k = 0; k < steps.Count; k++)
                    {
                        string want = conv.ConvertText(uniSteps[k].Buffer);
                        if (steps[k].Buffer != want) stepIssues.Add(new { after = plan[..(k + 1)], shown = Hex(steps[k].Buffer), converterOfUnicode = Hex(want) });
                    }
                    if (stepIssues.Count > 0) { stepBad++; prof["steps"] = stepIssues; }
                    if (pname == "p7")
                    {
                        // Backspace after every keystroke takes back that keystroke (engine-wide: counted, not fixed here)
                        int bad = 0;
                        for (int j = 1; j <= plan.Length; j++)
                        {
                            bsPositions++;
                            string got = TypeWith(layout, null, conv, font, plan[..j] + "\b", out _);
                            string before = j == 1 ? "" : steps[j - 2].Buffer;
                            if (got != before) bad++;
                        }
                        if (bad > 0) { bsBad += bad; prof["backspaceWrongPositions"] = bad; }
                    }
                    rec[pname] = prof;
                }
                outRows.Add(rec);
            }
            summary.Add(new { layout = lref.Id, words = n, planned, unplannedWords, finalNotConverter = finalBad, wordsWithWrongIntermediate = stepBad, backspacePositions = bsPositions, backspaceTookBackMore = bsBad });
            Console.WriteLine($"{lref.Id}: planned {planned}/{n}, final!=converter {finalBad}, wrong intermediate {stepBad}, backspace wrong {bsBad}/{bsPositions}");
        }
        File.WriteAllText(Path.Combine(Out, "l2-words.json"), JsonSerializer.Serialize(new { summary, words = outRows }, Json));
        return 0;
    }
}
