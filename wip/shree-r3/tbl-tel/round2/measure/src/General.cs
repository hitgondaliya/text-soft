using System.Text;
using FontSoftware.Core.Models;
using static U;

static partial class Program
{
    /// <summary>
    /// The charts teach a sign on క only. Every chart run on క, with క's keys replaced by another consonant's keys, must type
    /// that consonant with the same sign (the typing-order generalisation), on every layout, in Unicode and in SHREE-TEL7.
    /// </summary>
    static void General()
    {
        var tr = B.GetTransliterator(Te);
        int total = 0, uni = 0, leg = 0;
        var bad = new List<string>();
        foreach (var lref in Te.Layouts)
        {
            var layout = B.GetLayout(lref);
            // shortest key string typing exactly one consonant: a single key, else a chart run
            var consKeys = new Dictionary<char, string>();
            foreach (var k in layout.Keys)
                foreach (var (txt, shift) in new[] { (k.Base, false), (k.Shift, true) })
                    if (txt is { Length: 1 } && IsDrawnCons(txt[0]) && k.VirtualKey >= 'A' && k.VirtualKey <= 'Z' && !consKeys.ContainsKey(txt[0]))
                        consKeys[txt[0]] = (shift ? (char)k.VirtualKey : char.ToLowerInvariant((char)k.VirtualKey)).ToString();
            foreach (var s in layout.Sequences.OrderBy(s => s.Keys.Length))
                if (s.To.Length == 1 && IsDrawnCons(s.To[0]) && !consKeys.ContainsKey(s.To[0]) && !s.Keys.Contains('Ø') && !s.Keys.Contains('¤'))
                    consKeys[s.To[0]] = s.Keys;
            if (!consKeys.TryGetValue('\u0C15', out var ka)) { Say($"   general {lref.Id}: no key types క alone"); continue; }
            int lt = 0, lu = 0, ll = 0;
            foreach (var seq in layout.Sequences)
            {
                string t = seq.To, keys = seq.Keys;
                if (t.Length < 2 || t[0] != '\u0C15' || t.Count(c => c == '\u0C15') != 1 || t.Contains('\u0C30') || t.Contains('\u0C37')) continue;
                if (!(IsSign(t[1]) || IsTail(t[1]) || (t[1] == '\u0C4D' && t.Length == 3 && t[2] == '\u200C'))) continue;
                int at = keys.IndexOf(ka, StringComparison.Ordinal);
                if (at < 0 || keys.IndexOf(ka, at + 1, StringComparison.Ordinal) >= 0) continue;
                foreach (var (c, ck) in consKeys)
                {
                    if (c == '\u0C15' || c == '\u0C30') continue;
                    string k2 = keys[..at] + ck + keys[(at + ka.Length)..], want = c + t[1..];
                    var (u, t7, _) = TypeAll(layout, tr, k2);
                    string w7 = Fwd["shree-tel7"].ConvertText(want);
                    total++; lt++;
                    if (u == want) { uni++; lu++; } else bad.Add($"UNI\t{lref.Id}\t{k2}\t(chart {keys}={t})\twant {want}\tgot {u} [{Cps(u)}]");
                    if (t7 == w7) { leg++; ll++; } else bad.Add($"T7\t{lref.Id}\t{k2}\twant {Hex(w7)}\tgot {Hex(t7)}");
                }
            }
            Say($"   general {lref.Id}: {lt} probes (chart runs on క moved to {consKeys.Count - 1} other consonants), Unicode right {lu}, SHREE-TEL7 right {ll}");
        }
        Tsv("general.tsv", bad);
        Say($"[general] chart runs on క moved to every other consonant (not ర), all 18 layouts: {total} probes; Unicode right {uni}; SHREE-TEL7 = converter {leg}");
        foreach (var g in bad.Where(b => b.StartsWith("UNI")).GroupBy(b => b.Split('\t')[1])) Say($"   {g.Key}: {g.Count()} wrong, e.g. {g.First().Split('\t', 3)[2]}");
    }
}
