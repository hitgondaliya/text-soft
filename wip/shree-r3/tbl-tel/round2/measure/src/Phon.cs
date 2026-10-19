using static U;

static partial class Program
{
    /// <summary>Layouts whose consonant keys type the half form C్ (Phonetic): every run k+X = క+sign moved to every other half-form key.</summary>
    static void Phon()
    {
        var tr = B.GetTransliterator(Te);
        foreach (var lref in Te.Layouts)
        {
            var layout = B.GetLayout(lref);
            var half = new Dictionary<char, string>();
            foreach (var k in layout.Keys)
                foreach (var (txt, shift) in new[] { (k.Base, false), (k.Shift, true) })
                    if (txt is { Length: 2 } && IsDrawnCons(txt[0]) && txt[1] == '\u0C4D' && k.VirtualKey >= 'A' && k.VirtualKey <= 'Z' && !half.ContainsKey(txt[0]))
                        half[txt[0]] = (shift ? (char)k.VirtualKey : char.ToLowerInvariant((char)k.VirtualKey)).ToString();
            if (!half.TryGetValue('\u0C15', out var ka)) continue;
            int n = 0, ok = 0;
            var ex = new List<string>();
            foreach (var s in layout.Sequences.Where(s => s.Keys.StartsWith(ka) && s.To.Length >= 2 && s.To[0] == '\u0C15' && s.To.Count(c => c == '\u0C15') == 1 && !s.Keys.Contains('Ø') && (IsSign(s.To[1]) || IsTail(s.To[1]))))
                foreach (var (c, ck) in half)
                {
                    if (c is '\u0C15' or '\u0C30') continue;
                    string keys = ck + s.Keys[ka.Length..], want = c + s.To[1..];
                    var (u, _, _) = TypeAll(layout, tr, keys);
                    n++;
                    if (u == want) ok++; else if (ex.Count < 6) ex.Add($"{keys}={u} (want {want})");
                }
            Say($"   half-form keys {lref.Id}: {half.Count} consonant keys type C్; runs on క moved to the others: {n} probes, right {ok}; e.g. {string.Join(", ", ex)}");
        }
    }
}
