using System.Text;
using System.Text.Json;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;

static partial class Program
{
    sealed record Unit(string Keys, string Text, int Rank);

    static bool IsCons(char c) => (c >= 'କ' && c <= 'ହ') || c is 'ଡ଼' or 'ଢ଼' or 'ୟ' or 'ୱ';
    static bool IsSign(char c) => (c >= 'ଁ' && c <= 'ଃ') || (c >= '଼' && c <= 'ୌ') || c is 'ୖ' or 'ୗ' or 'ୢ' or 'ୣ';

    /// <summary>Every key run a layout offers, with what it types (alone, in Unicode).</summary>
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
        return units.Select(u => u with { Text = u.Text.Normalize(NormalizationForm.FormC) }).Where(u => u.Text.Length > 0).Distinct().ToList();
    }

    /// <summary>
    /// round 2 (env ORI_PHONETIC_UNITS=1): the plans a keyboard that types every consonant in its virama form offers once
    /// its vowel keys join (TypingOrder.AfterVirama): a consonant run as its cluster with the virama the next vowel key
    /// takes (d = ଦ, then e = େ), the cluster + the a key (the inherent a), and what each vowel key or pair of them types
    /// after a consonant - read off the real engine by typing it after d (ଦ୍).
    /// </summary>
    static List<Unit> PhoneticUnits(KeyboardLayout layout, Transliterator? tr, List<Unit> units)
    {
        var extra = new List<Unit>();
        if (layout.Order.AfterVirama.Count == 0) return extra;
        // what each vowel key, or pair of them, types after a consonant's virama form: read off the real engine after d (ଦ୍)
        static bool VowelOrSign(char c) => IsSign(c) || (c >= 'ଅ' && c <= 'ଔ') || c is 'ୠ' or 'ୡ';
        var vowelKeys = units.Where(u => u.Rank == 0 && u.Keys.Length == 1 && u.Text.Length >= 1 && u.Text.All(VowelOrSign)).Select(u => u.Keys).Distinct().ToList();
        var after = new List<(string Keys, string Sign)>();
        foreach (var v in vowelKeys.Concat(from a in vowelKeys from b in vowelKeys select a + b))
        {
            string got = TypeWith(layout, tr, null, "Unicode", "d" + v, out _);
            if (got.Length >= 1 && got[0] == 'ଦ' && (got.Length == 1 || got[1..].All(IsSign)) && !got.Contains('୍')) after.Add((v, got[1..]));
        }
        // each consonant run in its virama form, then each of them: the cluster with that sign (or with the inherent a)
        foreach (var u in units.Where(u => u.Rank <= 1 && u.Text.Length >= 2 && u.Text[^1] == '୍' && IsCons(u.Text[^2]) && !u.Keys.Contains(EscapeLayer.Mark)).ToList())
            foreach (var (v, sign) in after)
                extra.Add(new(u.Keys + v, u.Text[..^1] + sign, 3));
        return extra.Distinct().ToList();
    }

    static char? SignOfVowel(char c) => c switch
    {
        'ଆ' => 'ା', 'ଇ' => 'ି', 'ଈ' => 'ୀ', 'ଉ' => 'ୁ', 'ଊ' => 'ୂ', 'ଋ' => 'ୃ', 'ୠ' => 'ୄ', 'ଏ' => 'େ', 'ଐ' => 'ୈ', 'ଓ' => 'ୋ', 'ଔ' => 'ୌ', _ => null,
    };

    /// <summary>The word in the order a keyboard strikes it: pre-signs before their cluster (ୈ ୋ ୌ split around it), a reph after its syllable.</summary>
    static string TypedOrder(string w, bool preE, bool rephAfter, bool split)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < w.Length)
        {
            bool reph = false;
            if (rephAfter && i + 2 < w.Length && w[i] == 'ର' && w[i + 1] == '୍' && IsCons(w[i + 2])) { reph = true; i += 2; }
            if (i < w.Length && IsCons(w[i]))
            {
                int cs = i;
                i++;
                while (i < w.Length && (w[i] == '଼' || (w[i] == '୍' && i + 1 < w.Length && IsCons(w[i + 1])))) i += w[i] == '୍' ? 2 : 1;
                if (i < w.Length && w[i] == '୍') { i++; if (i < w.Length && (w[i] == '‍' || w[i] == '‌')) i++; }
                string cluster = w[cs..i];
                var signs = new StringBuilder();
                while (i < w.Length && IsSign(w[i]) && w[i] != '୍') signs.Append(w[i++]);
                string sg = signs.ToString();
                string before = "", after = sg;
                if (preE)
                {
                    if (sg.StartsWith('େ')) { before = "େ"; after = sg[1..]; }
                    else if (split && sg.StartsWith('ୈ')) { before = "େ"; after = "ୖ" + sg[1..]; }
                    else if (split && sg.StartsWith('ୋ')) { before = "େ"; after = "ା" + sg[1..]; }
                    else if (split && sg.StartsWith('ୌ')) { before = "େ"; after = "ୗ" + sg[1..]; }
                }
                sb.Append(before).Append(cluster);
                // the reph goes after the vowel signs, before ଂ ଁ ଃ
                string vs = new(after.TakeWhile(c => c is not ('ଁ' or 'ଂ' or 'ଃ')).ToArray());
                sb.Append(vs);
                if (reph) sb.Append("ର୍");
                sb.Append(after[vs.Length..]);
                continue;
            }
            if (reph) { sb.Append("ର୍"); continue; }
            sb.Append(w[i++]);
        }
        return sb.ToString();
    }

    /// <summary>Key plans for a target (in typed order), fewest keys first, at most <paramref name="max"/>.</summary>
    static IEnumerable<string> Plans(string target, List<Unit> units, int max)
    {
        var byFirst = units.GroupBy(u => u.Text[0]).ToDictionary(g => g.Key, g => g.OrderBy(u => u.Rank).ThenByDescending(u => u.Text.Length).ThenBy(u => u.Keys.Length).ToList());
        var found = new List<string>();
        var stack = new List<string>();
        int budget = int.TryParse(Environment.GetEnvironmentVariable("ORI_PLAN_BUDGET"), out var b) ? b : 20000;   // round 2: settable
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
        var targets = new List<string>();
        foreach (bool preE in new[] { true, false })
            foreach (bool reph in new[] { true, false })
                foreach (bool split in new[] { true, false })
                    foreach (string v in new[] { w, w.Replace("ଆ", "ଅା") })
                        targets.Add(TypedOrder(v, preE, reph, split).Normalize(NormalizationForm.FormC));
        foreach (string target in targets.Distinct())
            foreach (var plan in Plans(target, units, 40))
                if (TypeWith(layout, tr, null, "Unicode", plan, out _).Normalize(NormalizationForm.FormC) == w.Normalize(NormalizationForm.FormC)) return plan;
        return null;
    }

    static int L2(string wordsFile, string outFile)
    {
        var words = File.ReadAllLines(wordsFile, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))
            .SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToList();
        var tr = Bundle.GetTransliterator(Or);
        var rows = new List<object>();
        var summary = new List<object>();
        int totalPlans = 0, finalOk = 0, interBad = 0, bsPositions = 0, bsOverDelete = 0;
        foreach (var lref in Or.Layouts)
        {
            var layout = Bundle.GetLayout(lref);
            if (layout.Phonetic) { summary.Add(new { layout = lref.Id, skipped = "type-by-sound" }); continue; }
            var units = Units(layout);
            if (Environment.GetEnvironmentVariable("ORI_PHONETIC_UNITS") == "1") units.AddRange(PhoneticUnits(layout, tr, units));
            int planned = 0, ok = 0, inter = 0, bsn = 0, bsBad = 0;
            var noPlan = new List<string>();
            foreach (var w in words)
            {
                // the word in NFC as the layouts type it (ଡ଼ ଢ଼ precomposed, as the charts print them)
                string word = w;
                string? plan = PlanWord(layout, tr, units, word) ?? PlanWord(layout, tr, units, word.Replace("ଡ଼", "ଡ଼").Replace("ଢ଼", "ଢ଼"));
                if (plan is null) { noPlan.Add(w); continue; }
                planned++;
                string uni = TypeWith(layout, tr, null, "Unicode", plan, out var ustep);
                string leg = TypeWith(layout, tr, Fwd, "SHREE-ORI7-0600", plan, out var lstep);
                string want = Fwd.ConvertText(uni);
                bool final = leg == want;
                if (final) ok++;
                // every intermediate state: the legacy buffer = the converter's bytes for the Unicode shown after the same keys
                int badAt = -1;
                for (int i = 0; i < Math.Min(ustep.Count, lstep.Count); i++)
                    if (Fwd.ConvertText(ustep[i].Buffer) != lstep[i].Buffer) { badAt = i; break; }
                if (badAt >= 0) inter++;
                // Backspace after each key takes back that key only: the buffer it leaves is what the keys before it typed
                var bsBadAt = new List<int>();
                for (int k = 1; k <= plan.Length; k++)
                {
                    string prefix = plan[..k];
                    string withBs = TypeWith(layout, tr, Fwd, "SHREE-ORI7-0600", prefix + "\b", out _);
                    string expect = k == 1 ? "" : TypeWith(layout, tr, Fwd, "SHREE-ORI7-0600", plan[..(k - 1)], out _);
                    bsn++;
                    if (withBs != expect) { bsBad++; bsBadAt.Add(k); }
                }
                rows.Add(new { layout = lref.Id, word = w, plan, uni = UHex(uni), legacy = Hex(leg), converter = Hex(want), final, intermediateBadAt = badAt,
                    intermediate = badAt >= 0 ? $"after '{plan[..(badAt + 1)]}': typed {Hex(lstep[badAt].Buffer)}, converter {Hex(Fwd.ConvertText(ustep[badAt].Buffer))} for {UHex(ustep[badAt].Buffer)}" : null,
                    backspaceOverDeleteAfterKey = bsBadAt });
            }
            totalPlans += planned; finalOk += ok; interBad += inter; bsPositions += bsn; bsOverDelete += bsBad;
            summary.Add(new { layout = lref.Id, words = words.Count, planned, finalEqualsConverter = ok, intermediateMismatch = inter, backspacePositions = bsn, backspaceOverDelete = bsBad, noPlan = noPlan.Take(40) });
            Console.WriteLine($"{lref.Id}: planned {planned}/{words.Count}, final = converter {ok}, intermediate mismatch {inter}, backspace over-delete {bsBad}/{bsn}");
        }
        File.WriteAllText(outFile, JsonSerializer.Serialize(new { totalPlans, finalOk, interBad, bsPositions, bsOverDelete, summary, rows }, Json));
        Console.WriteLine($"total: plans {totalPlans}, final = converter {finalOk}, intermediate mismatch {interBad}, backspace over-delete {bsOverDelete}/{bsPositions}");
        return 0;
    }
}
