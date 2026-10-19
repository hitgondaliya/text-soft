using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Models;
using static U;

// Round 2: whole words typed key by key on each of the 14 fixed keyboards, planned the way Shree's composer takes them -
// each consonant by its key (Ctrl+Space where only that layer has it), a conjunct with the link key, the ra-vattu key
// struck BEFORE its consonant, the vowel sign by its sign key, ం ః ఁ by their keys, an independent vowel by the key that
// types it, a word-final virama by the halant key - then compared with what the converter writes for the word (Unicode
// and SHREE-TEL7 bytes). Words: cap-tel\common-words.txt and words of 2-4 truth syllables.
static partial class Program
{
    static void Words2()
    {
        var tr = B.GetTransliterator(Te);
        string fixture = Environment.GetEnvironmentVariable("TELKEYS") is { Length: > 0 } tk ? tk
            : Path.GetFullPath(Path.Combine(Cfg, "..", "..", "tests", "FontSoftware.Tests", "Data", "shree-tel-keys.json"));
        var doc = JsonNode.Parse(File.ReadAllText(fixture, Encoding.UTF8))!["layouts"]!.AsObject();
        var words = File.ReadAllLines(Path.Combine(CapDir, "common-words.txt"), Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        // random joins of truth syllables of the plain shapes (C, C+sign, C్C(+sign), C్ర(+sign), + tail)
        var rnd = new Random(2027);
        var pool = Cap.Truth.Keys.Where(u => u.All(c => IsLetter(c) || IsSign(c) || IsVirama(c) || IsTail(c)) && IsLetter(u[0]) && !IsVirama(u[^1])
                                            && !u.Contains('ఴ') && !u.Contains('ౄ') && !u.Contains('ఌ') && u.Count(IsVirama) <= 1).ToList();
        for (int i = 0; i < 400; i++) words.Add(string.Concat(Enumerable.Range(0, 2 + rnd.Next(3)).Select(_ => pool[rnd.Next(pool.Count)])));
        int planned = 0, unplannable = 0, okU = 0, okL = 0;
        var bad = new List<string>();
        var noPlan = new SortedDictionary<string, int>();
        foreach (var (id, node) in doc)
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == id));
            var consKey = node!["consonants"]!.AsObject().ToDictionary(kv => (string)kv.Value!, kv => kv.Key);
            var signKey = new Dictionary<string, string>();
            foreach (var (k, v) in node["signs"]!.AsObject()) signKey.TryAdd((string)v!, k);
            var tailKey = new Dictionary<string, string>();
            foreach (var (k, v) in node["tails"]!.AsObject()) tailKey.TryAdd((string)v!, k);
            string? link = node["links"]!.AsArray().Select(x => (string?)x).FirstOrDefault();
            string? halant = node["halants"]!.AsArray().Select(x => (string?)x).FirstOrDefault();
            string? ra = node["ravattu"]!.AsArray().Select(x => (string?)x).FirstOrDefault();
            // independent vowels: the key that types the vowel alone (a sign key alone types its vowel; అ by its own key)
            var vowelKey = new Dictionary<char, string>();
            foreach (var m in layout.Keys)
                foreach (var (t, shift) in new[] { (m.Base, false), (m.Shift, true) })
                    if (t is { Length: 1 } && IsVowel(t[0]) && m.VirtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9' or >= 0xBA)
                    {
                        char ch = KeyCharOf(m.VirtualKey, shift);
                        if (ch != '\0') vowelKey.TryAdd(t[0], ch.ToString());
                    }
            foreach (var w in words)
            {
                string? keys = Plan(w, consKey, signKey, tailKey, vowelKey, link, halant, ra, out string why);
                if (keys is null) { unplannable++; noPlan[id + ": " + why] = noPlan.GetValueOrDefault(id + ": " + why) + 1; continue; }
                planned++;
                var (u, t7, _) = TypeAll(layout, tr, keys);
                string w7 = Fwd["shree-tel7"].ConvertText(w);
                bool a = u == w, b = t7 == w7;
                if (a) okU++;
                if (b) okL++;
                if (!a || !b) bad.Add($"{id}\t{keys}\twant {w} [{Cps(w)}] {Hex(w7)}\tgot {u} [{Cps(u)}] {Hex(t7)}");
            }
        }
        Tsv("words2.tsv", bad);
        Say($"[words2] {words.Count} words (210 common + 400 truth-syllable joins) planned key by key on the 14 fixed keyboards: planned {planned}, " +
            $"Unicode = the word {okU}, SHREE-TEL7 = converter(word) {okL}; not plannable {unplannable}");
        foreach (var (k, n) in noPlan.OrderByDescending(kv => kv.Value).Take(12)) Say($"   no plan {k}: {n}");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0])) Say($"   {g.Key}: {g.Count()} wrong, e.g. {g.First()}");
    }

    static char KeyCharOf(int vk, bool shift)
    {
        if (vk is >= 'A' and <= 'Z') return shift ? (char)vk : char.ToLowerInvariant((char)vk);
        if (vk is >= '0' and <= '9') return shift ? ")!@#$%^&*("[vk - '0'] : (char)vk;
        return vk switch
        {
            186 => shift ? ':' : ';', 187 => shift ? '+' : '=', 188 => shift ? '<' : ',', 189 => shift ? '_' : '-', 190 => shift ? '>' : '.',
            191 => shift ? '?' : '/', 192 => shift ? '~' : '`', 219 => shift ? '{' : '[', 220 => shift ? '|' : '\\', 221 => shift ? '}' : ']',
            222 => shift ? '"' : '\'', _ => '\0',
        };
    }

    static string? Plan(string w, Dictionary<string, string> cons, Dictionary<string, string> signs, Dictionary<string, string> tails,
                        Dictionary<char, string> vowels, string? link, string? halant, string? ra, out string why)
    {
        var sb = new StringBuilder();
        why = "";
        int i = 0;
        while (i < w.Length)
        {
            char c = w[i];
            if (c == ' ') { sb.Append(' '); i++; continue; }
            if (IsVowel(c)) { if (!vowels.TryGetValue(c, out var vk)) { why = "vowel " + c; return null; } sb.Append(vk); i++; }
            else if (IsLetter(c))
            {
                // the cluster: C (్ C)*, its last subjoined ర as the ra-vattu
                int j = i + 1;
                while (j + 1 < w.Length && IsVirama(w[j]) && IsLetter(w[j + 1])) j += 2;
                string cluster = w[i..j];
                bool raVattu = cluster.Length >= 3 && cluster.EndsWith("్ర", StringComparison.Ordinal);
                if (raVattu) { if (ra is null) { why = "no ra-vattu key"; return null; } sb.Append(ra); cluster = cluster[..^2]; }
                for (int k = 0; k < cluster.Length; k += 2)
                {
                    if (k > 0) { if (link is null) { why = "no link key"; return null; } sb.Append(link); }
                    if (!cons.TryGetValue(cluster[k].ToString(), out var ck)) { why = "consonant " + cluster[k]; return null; }
                    sb.Append(ck);
                }
                i = j;
                if (i < w.Length && IsVirama(w[i]))   // word-final virama (or before a space / a non-letter)
                {
                    if (halant is null) { why = "no halant key"; return null; }
                    sb.Append(halant); i++;
                    continue;
                }
                if (i < w.Length && IsSign(w[i]))
                {
                    if (!signs.TryGetValue(w[i].ToString(), out var sk)) { why = "sign " + w[i]; return null; }
                    sb.Append(sk); i++;
                }
            }
            else { why = "char U+" + ((int)c).ToString("X4"); return null; }
            while (i < w.Length && IsTail(w[i]))
            {
                if (!tails.TryGetValue(w[i].ToString(), out var tk)) { why = "tail " + w[i]; return null; }
                sb.Append(tk); i++;
            }
        }
        return sb.ToString();
    }
}
