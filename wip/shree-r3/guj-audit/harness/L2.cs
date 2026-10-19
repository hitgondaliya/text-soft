using System.Text;
using System.Text.Json;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;

static partial class Program
{
    sealed record Unit(string Keys, string Text, int Rank);

    static bool IsGuj(char c) => c >= '઀' && c <= '૿';
    static bool IsCons(char c) => c >= 'ક' && c <= 'હ';
    static bool IsSign(char c) => (c >= 'ઁ' && c <= 'ઃ') || (c >= '઼' && c <= 'ૌ') || c == 'ૢ' || c == 'ૣ';

    /// <summary>Every key run a layout offers, with what it types (alone, in Unicode).</summary>
    static List<Unit> Units(KeyboardLayout layout)
    {
        var units = new List<Unit>();
        foreach (var (vk, normal, shift) in ShreeKeyboardFile.PhysicalKeys)
        {
            var m = layout.Find(vk);
            if (m is null) continue;
            if (!string.IsNullOrEmpty(m.Base)) units.Add(new(normal.ToString(), m.Base!, 0));
            string? sh = m.Shift ?? m.Base;           // an unset Shift types the normal character
            if (!string.IsNullOrEmpty(sh)) units.Add(new(shift.ToString(), sh!, 0));
        }
        foreach (var s in layout.Sequences) if (!string.IsNullOrEmpty(s.Keys) && !string.IsNullOrEmpty(s.To)) units.Add(new(s.Keys, s.To, 1));
        foreach (var s in layout.Escape) if (!string.IsNullOrEmpty(s.To)) units.Add(new(EscapeLayer.Mark + s.Keys, s.To, 2));
        foreach (var s in layout.Symbols) if (!string.IsNullOrEmpty(s.To)) units.Add(new(EscapeLayer.SymbolMark + s.Keys, s.To, 2));
        // a vowel key types its sign after a consonant on some keyboards (TypingOrder.SignAfter)
        var extra = new List<Unit>();
        foreach (var u in units)
            if (u.Text.Length == 1 && SignOfVowel(u.Text[0]) is { } sign) extra.Add(new(u.Keys, sign.ToString(), 3));
        units.AddRange(extra);
        return units.Distinct().ToList();
    }

    static char? SignOfVowel(char c) => c switch
    {
        'આ' => 'ા', 'ઇ' => 'િ', 'ઈ' => 'ી', 'ઉ' => 'ુ', 'ઊ' => 'ૂ', 'ઋ' => 'ૃ', 'ૠ' => 'ૄ', 'ઍ' => 'ૅ', 'એ' => 'ે', 'ઐ' => 'ૈ',
        'ઑ' => 'ૉ', 'ઓ' => 'ો', 'ઔ' => 'ૌ', _ => null,
    };

    /// <summary>The word in the order this keyboard strikes it: a pre-sign before its cluster, a reph after its cluster and signs.</summary>
    static string TypedOrder(string w, TypingOrder order)
    {
        if (!order.Active) return w;
        var pre = order.PreSigns.Where(p => p.Length == 1).Select(p => p[0]).ToHashSet();
        bool rephAfter = order.Reph is not null;
        var sb = new StringBuilder();
        int i = 0;
        while (i < w.Length)
        {
            // syllable: [ર્] cluster signs*
            int start = i;
            bool reph = false;
            if (rephAfter && i + 2 < w.Length && w[i] == 'ર' && w[i + 1] == '્' && IsCons(w[i + 2])) { reph = true; i += 2; }
            if (i < w.Length && IsCons(w[i]))
            {
                int cs = i;
                i++;
                while (i < w.Length && (w[i] == '઼' || (w[i] == '્' && i + 1 < w.Length && IsCons(w[i + 1]))))
                    i += w[i] == '્' ? 2 : 1;
                // a cluster ending in a virama (+ZWJ/ZWNJ)
                if (i < w.Length && w[i] == '્') { i++; if (i < w.Length && (w[i] == '‍' || w[i] == '‌')) i++; }
                string cluster = w[cs..i];
                var signs = new StringBuilder();
                while (i < w.Length && IsSign(w[i])) signs.Append(w[i++]);
                string sg = signs.ToString();
                var before = new StringBuilder();
                foreach (char s in sg) if (pre.Contains(s)) before.Append(s);
                string after = new string(sg.Where(s => !pre.Contains(s)).ToArray());
                sb.Append(before).Append(cluster).Append(after);
                if (reph) sb.Append("ર્");
                continue;
            }
            if (reph) { sb.Append("ર્"); continue; }
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
        // signs a keyboard types as two strokes that TypingOrder joins (ા then ે is ો), vowels typed as અ/આ plus a sign
        static string Split(string s, bool alt) => s.Replace("ો", alt ? "ેા" : "ાે").Replace("ૌ", "ાૈ").Replace("ૉ", "ાૅ")
            .Replace("ઓ", alt ? "અાે" : "આે").Replace("ઔ", "આૈ").Replace("ઑ", "આૅ").Replace("ઍ", "અૅ");
        var targets = new List<string>();
        foreach (string t0 in new[] { TypedOrder(w, layout.Order), w }) { targets.Add(t0); targets.Add(Split(t0, false)); targets.Add(Split(t0, true)); }
        foreach (string target in targets.Distinct())
        foreach (var plan in Plans(target, units, 60))
        {
            if (TypeWith(layout, tr, null, "Unicode", plan, out _) == w) return plan;
        }
        return null;
    }

    sealed class WordsFile { public List<string> Words { get; set; } = new(); public List<string> Lines { get; set; } = new(); }

    static int L2(string[] args)
    {
        var wf = JsonSerializer.Deserialize<WordsFile>(File.ReadAllText(Path.Combine(A, "harness", "words.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var tr = Bundle.GetTransliterator(Gu);
        string[] only = args.Length > 1 ? args[1].Split(',') : Array.Empty<string>();
        var outRows = new List<object>();
        var summary = new List<object>();
        foreach (var lref in Gu.Layouts)
        {
            if (only.Length > 0 && !only.Contains(lref.Id)) continue;
            var layout = Bundle.GetLayout(lref);
            if (layout.Phonetic) { summary.Add(new { layout = lref.Id, skipped = "phonetic (type-by-sound) layout: no fixed key plan" }); continue; }
            var units = Units(layout);
            int planned = 0, unplanned = 0, finalBad = 0, stepBad = 0, bsBad = 0, bsRetypeBad = 0, clickBad = 0, n = 0;
            var unplannedWords = new List<string>();
            foreach (string w in wf.Words)
            {
                n++;
                string? plan = PlanWord(layout, tr, units, w);
                if (plan is null) { unplanned++; unplannedWords.Add(w); outRows.Add(new { layout = lref.Id, word = w, plan = (string?)null }); continue; }
                planned++;
                var rec = new Dictionary<string, object?> { ["layout"] = lref.Id, ["word"] = w, ["plan"] = plan };
                TypeWith(layout, tr, null, "Unicode", plan, out var uniSteps);
                foreach (var (pname, conv, font) in Profiles)
                {
                    Func<string, string> show = conv is null ? UHex : Hex;
                    string final = TypeWith(layout, tr, conv, font, plan, out var steps);
                    string expect = conv is null ? w : conv.ConvertText(w);
                    var prof = new Dictionary<string, object?> { ["final"] = show(final), ["converter"] = show(expect), ["ok"] = final == expect };
                    if (final != expect && conv is not null) finalBad++;
                    // every intermediate state: what is on screen = the converter's spelling of what Unicode shows
                    var stepIssues = new List<object>();
                    for (int k = 0; conv is not null && k < steps.Count; k++)
                    {
                        string want = conv.ConvertText(uniSteps[k].Buffer);
                        if (steps[k].Buffer != want)
                            stepIssues.Add(new { after = plan[..(k + 1)], shown = Hex(steps[k].Buffer), unicodeShown = UHex(uniSteps[k].Buffer), converterOfUnicode = Hex(want), events = steps[k].Events });
                    }
                    if (stepIssues.Count > 0) { stepBad++; prof["steps"] = stepIssues; }
                    // Backspace after every keystroke: it takes back that keystroke, and typing it again gives the word back
                    var bsIssues = new List<object>();
                    for (int j = 1; j <= plan.Length; j++)
                    {
                        string keys = plan[..j] + "\b";
                        string got = TypeWith(layout, tr, conv, font, keys, out var bst);
                        string before = j == 1 ? "" : steps[j - 2].Buffer;
                        string again = TypeWith(layout, tr, conv, font, plan[..j] + "\b" + plan[(j - 1)..], out _);
                        if (got != before || again != final)
                        {
                            if (conv is not null) { if (got != before) bsBad++; else bsRetypeBad++; }
                            string kind = got == before ? "retype differs" : before.StartsWith(got, StringComparison.Ordinal) ? "took back more than the last keystroke" : got.StartsWith(before, StringComparison.Ordinal) ? "took back less" : "different text";
                            bsIssues.Add(new { kind, keysThenBackspace = plan[..j], lastKey = plan[j - 1].ToString(), shownAfterBackspace = show(got), shownBeforeThatKey = show(before), retypedRest = show(again), finalWithoutBackspace = show(final), events = bst[^1].Events });
                        }
                    }
                    if (bsIssues.Count > 0) prof["backspace"] = bsIssues;
                    // a mouse click before the word: nothing of the word before the click is touched
                    string clicked = TypeWith(layout, tr, conv, font, Typist.Click + plan, out _);
                    if (clicked != final) { if (conv is not null) clickBad++; prof["click"] = show(clicked); }
                    rec[pname] = prof;
                }
                // typing order: a keyboard that strikes the reph after its letter, typed with the reph first (Unicode's order)
                if (layout.Order.Reph is not null && w.Contains("ર્") && TypedOrder(w, layout.Order) != w)
                {
                    var alt = Plans(w, units, 30).FirstOrDefault(p => p != plan);
                    if (alt is not null)
                    {
                        string u2 = TypeWith(layout, tr, null, "Unicode", alt, out _);
                        string l2 = TypeWith(layout, tr, Fwd7, "g7", alt, out _);
                        rec["rephFirst"] = new { keys = alt, unicode = u2, unicodeHex = UHex(u2), sameWord = u2 == w, g7 = Hex(l2), g7OfWord = Hex(Fwd7.ConvertText(w)) };
                    }
                }
                outRows.Add(rec);
            }
            summary.Add(new { layout = lref.Id, words = n, planned, unplanned, unplannedWords, finalNotConverter = finalBad, wordsWithWrongIntermediate = stepBad, backspaceWrong = bsBad, backspaceRetypeWrong = bsRetypeBad, clickWrong = clickBad });
            Console.WriteLine($"{lref.Id}: planned {planned}/{n}, final!=converter {finalBad}, wrong intermediate {stepBad}, backspace {bsBad}/{bsRetypeBad}, click {clickBad}");
        }
        // lines: words + spaces + punctuation + digits + Enter, typed as a typist would
        var lineRows = new List<object>();
        foreach (var lref in Gu.Layouts)
        {
            if (only.Length > 0 && !only.Contains(lref.Id)) continue;
            var layout = Bundle.GetLayout(lref);
            if (layout.Phonetic) continue;
            var units = Units(layout);
            foreach (string line in wf.Lines)
            {
                var (keys, how) = PlanLine(layout, tr, units, line);
                var rec = new Dictionary<string, object?> { ["layout"] = lref.Id, ["line"] = line, ["how"] = how };
                if (keys is null) { lineRows.Add(rec); continue; }
                string uni = TypeLine(layout, tr, null, "Unicode", keys, out var uSteps);
                rec["unicode"] = uni; rec["unicodeOk"] = uni == line;
                foreach (var (pname, conv, font) in Profiles.Where(p => p.Conv is not null))
                {
                    string got = TypeLine(layout, tr, conv, font, keys, out var lSteps);
                    rec[pname] = new { typed = Hex(got), converterOfLine = Hex(conv!.ConvertText(line)), converterOfTypedUnicode = Hex(conv.ConvertText(uni)), ok = got == conv.ConvertText(uni), steps = lSteps.Select(s => s.Key + " => " + s.Events).ToArray() };
                }
                lineRows.Add(rec);
            }
        }
        File.WriteAllText(Path.Combine(A, "results", "l2-words.json"), JsonSerializer.Serialize(new { summary, words = outRows }, Json));
        File.WriteAllText(Path.Combine(A, "results", "l2-lines.json"), JsonSerializer.Serialize(lineRows, Json));
        return 0;
    }

    /// <summary>A line as keystroke tokens: Gujarati words by plan, everything else by a key that types it, or typed in English with TextSoft toggled off.</summary>
    static (List<(string Keys, bool English)>? Keys, List<string> How) PlanLine(KeyboardLayout layout, Transliterator? tr, List<Unit> units, string line)
    {
        var keys = new List<(string, bool)>();
        var how = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (IsGuj(c) && !(c >= '૦' && c <= '૯') && c != '૱' || c == '‌' || c == '‍')
            {
                int s = i;
                while (i < line.Length && (IsGuj(line[i]) && !(line[i] >= '૦' && line[i] <= '૯') || line[i] == '‌' || line[i] == '‍')) i++;
                string w = line[s..i];
                string? plan = PlanWord(layout, tr, units, w);
                if (plan is null) { how.Add("no plan for " + w); return (null, how); }
                keys.Add((plan, false)); how.Add(w + " = " + plan);
                continue;
            }
            if (c == ' ' || c == '\n') { keys.Add((c.ToString(), false)); how.Add(c == ' ' ? "space" : "Enter"); i++; continue; }
            // a key of the layout that types this character alone (single keys first)
            var u = units.Where(x => x.Text == c.ToString() && x.Rank < 3).OrderBy(x => x.Rank).ThenBy(x => x.Keys.Length).FirstOrDefault();
            if (u is not null) { keys.Add((u.Keys, false)); how.Add($"{c} = key {u.Keys}"); i++; continue; }
            // an ASCII character on a key this layout leaves to the application
            if (c < 0x80 && Typist.VkOf(c).Vk != 0 && !units.Any(x => x.Keys == c.ToString()))
            { keys.Add((c.ToString(), false)); how.Add($"{c} = its own key, not used by the layout"); i++; continue; }
            if (c < 0x80) { keys.Add((c.ToString(), true)); how.Add($"{c} = typed in English (TextSoft toggled off)"); i++; continue; }
            how.Add($"no key types {c} (U+{(int)c:X4})");
            return (null, how);
        }
        return (keys, how);
    }

    static string TypeLine(KeyboardLayout layout, Transliterator? tr, FontConverter? conv, string font, List<(string Keys, bool English)> keys, out List<KeyStep> steps)
    {
        using var t = new Typist(layout, tr, conv, font);
        foreach (var (k, en) in keys)
        {
            if (en) t.English(k);
            else foreach (char c in k) t.Key(c);
        }
        steps = t.Steps;
        return t.App.Buf.ToString();
    }
}

static partial class Program
{
    /// <summary>type &lt;layoutId&gt; &lt;keys&gt; [more keys...]: what the engine types for these keys, every profile, step by step. Keys: chart notation; \b = Backspace, \1 = click.</summary>
    static int TypeMode(string[] args)
    {
        var layout = Layout(args[1]);
        var tr = Bundle.GetTransliterator(Gu);
        for (int i = 2; i < args.Length; i++)
        {
            string keys = args[i].Replace("<BS>", "\b").Replace("<CLICK>", "\u0001").Replace("<ENTER>", "\n").Replace("<CS>", "Ø").Replace("<CS1>", "¤");
            Console.WriteLine($"== {args[1]} keys {args[i]}   (order: {layout.Order})");
            foreach (var (name, conv, font) in Profiles)
            {
                using var t = new Typist(layout, tr, conv, font);
                foreach (char k in keys) t.Key(k);
                Console.WriteLine($"  {name,-5} " + string.Join("  ", t.Steps.Select(s => $"[{(s.Key == "\b" ? "BS" : s.Key)}{(s.Handled ? "" : "*")}] {(name == "uni" ? s.Buffer : Hex(s.Buffer))}")));
            }
        }
        return 0;
    }
}

static partial class Program
{
    /// <summary>typeu &lt;layoutId&gt; &lt;keys&gt;...: the Unicode the engine shows after each key, as code points.</summary>
    static int TypeUMode(string[] args)
    {
        var layout = Layout(args[1]);
        var tr = Bundle.GetTransliterator(Gu);
        for (int i = 2; i < args.Length; i++)
        {
            string keys = args[i].Replace("<BS>", "\b").Replace("<CS>", "Ø").Replace("<CS1>", "¤");
            using var t = new Typist(layout, tr, null, "Unicode");
            foreach (char k in keys) t.Key(k);
            Console.WriteLine($"{args[1]} {args[i]}: " + string.Join("  ", t.Steps.Select(s => $"[{(s.Key == "\b" ? "BS" : s.Key)}] {UHex(s.Buffer)}")));
        }
        return 0;
    }
}
