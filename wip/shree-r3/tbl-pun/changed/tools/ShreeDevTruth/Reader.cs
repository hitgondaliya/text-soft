using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree -> Unicode pairs: every preferred composer spelling must read back
/// to its Unicode, or to a Unicode string the composer writes the same way. The pre
/// rules undo what the composer does to the order (spacers, sign + reph glyphs, the
/// reph after its syllable); the pairs answer in visual order (the i-sign and the nukta
/// before their letter, as the bytes are), and the post rules put those in Unicode order.
/// </summary>
public sealed class Reader
{
    public readonly Script S;
    readonly FastConv _stager, _visual;
    readonly Dictionary<string, HashSet<string>> _accepted = new(StringComparer.Ordinal);

    public Reader(Script s, IEnumerable<Truth>? truth = null)
    {
        S = s;
        _stager = new FastConv(s.ReversePre, Array.Empty<RewriteRule>(), new Dictionary<string, string>());
        _visual = new FastConv(s.VisualRules, Array.Empty<RewriteRule>(), new Dictionary<string, string>());
        foreach (var t in truth ?? Array.Empty<Truth>()) _accepted[t.U] = t.Acc.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>A legacy spelling after the pre rules: what the pairs see.</summary>
    public string Staged(string legacy) => _stager.Pre(legacy);

    /// <summary>Unicode in visual order (i-sign before its cluster, nukta before its letter), as a pair answers.</summary>
    public string Visual(string unicode) => _visual.Pre(unicode);

    static bool Plain(string u) => !u.Any(c => IsJoiner(c));

    /// <summary>
    /// Every spelling of a row the reverse table must read: the one the table writes, and the
    /// composer's own answer when the table writes another (a half form the composer types
    /// while it waits for the next letter is still a reading of that letter + virama).
    /// </summary>
    public static IEnumerable<(Truth T, string Hex)> Spellings(IEnumerable<Truth> truth) =>
        truth.SelectMany(t => t.Composer is { } c && c != t.Pref && IsVirama(t.U[^1]) ? new[] { (t, t.Pref), (t, c) }
            // A row the output policy writes otherwise (– as a hyphen, × left out): the reverse table reads what
            // Shree wrote for it (8D, 81), never the policy's look-alike (two hyphens stay two hyphens).
            : t.Composer is { } p && p != t.Pref && Policy.Overrides("dev7").ContainsKey(t.U) ? new[] { (t, p) }
            : new[] { (t, t.Pref) });

    /// <summary>The Unicode a spelling reads back as, when several strings share it: what the bytes draw.</summary>
    public Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        Spellings(truth.Where(t => S.Excluded(t) is null)).GroupBy(s => s.Hex)
             .ToDictionary(g => g.Key, g => g.Select(s => S.Visible(s.T.U, s.Hex)).OrderBy(u => Plain(u) ? 0 : 1).ThenBy(u => u.Length)
                                              .ThenBy(u => u, StringComparer.Ordinal).First(), StringComparer.Ordinal);

    public MappingTable Build(Dictionary<string, string> pairs, string? id = null, string? name = null, string? source = null,
        IEnumerable<RewriteRule>? pre = null) =>
        Table(id ?? S.ReverseId, name ?? $"{S.Title} -> Unicode", source ?? S.Encoding, "mangal", FontType.Legacy, FontType.Unicode,
            pre ?? S.ReversePre,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            S.ReversePost);

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> in this
    /// font: equal (nukta letters precomposed or not), equal to what the bytes draw (the
    /// composer drops some marks), a string the composer also writes with these bytes, or
    /// well-formed Devanagari the forward table writes with the very same codes.
    /// </summary>
    public bool Same(string got, string want, string hex, FontConverter forward)
    {
        if (got == want || Nfd(got) == Nfd(want)) return true;
        string vis = S.Visible(want, hex);
        if (Nfd(got) == Nfd(vis)) return true;
        // Where the composer dropped a mark, a joiner more or less draws the same.
        if (vis != want && NoJoiners(Nfd(got)) == NoJoiners(Nfd(vis))) return true;
        if (_accepted.TryGetValue(got, out var acc) && acc.Contains(hex)) return true;
        if (!WellFormed(got)) return false;
        var r = forward.Convert(got);
        if (!r.HasUnmapped && Hex(r.Text) == hex) return true;
        // Text that ends in a virama, read from the half form the composer types while it waits
        // for the next letter: the table writes the halant there at the end of a word, but with
        // a letter after it, the very same codes.
        if (got.Length == 0 || !IsVirama(got[^1])) return false;
        var n = forward.Convert(got + "क");
        return !n.HasUnmapped && Hex(n.Text) == (hex + " " + Hex(forward.ConvertText("क"))).Trim();
    }

    static string Nfd(string s) => Nukta.Spell(s);
    static string NoJoiners(string s) => new(s.Where(c => !IsJoiner(c)).ToArray());

    /// <summary>No sign without a letter before it, no sign straight after a virama, no two vowel signs in a row.</summary>
    public static bool WellFormed(string u)
    {
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i];
            bool sign = IsMark(c) && c != '्' && c != '़';
            if (!sign) continue;
            if (i == 0) return false;
            char p = u[i - 1];
            if (p == '्') return false;
            bool matra = c >= 'ा' && c <= 'ौ';
            bool pMatra = p >= 'ा' && p <= 'ौ';
            if (matra && (pMatra || p == 'ं' || p == 'ँ' || p == 'ः')) return false;
        }
        return true;
    }

    public Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (hex, u) in S.FontCodes) pairs[Staged(FromHex(hex))] = Visual(u);
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && S.Excluded(t) is null))
            if (canon.TryGetValue(t.Pref, out var cu) && cu == t.U) pairs[Staged(FromHex(t.Pref))] = t.U;
        foreach (var (u, hex, _) in S.Extra.Where(e => e.Hex.Length > 0 && !e.Hex.StartsWith("U+FD"))) pairs.TryAdd(Staged(FromHex(hex)), Visual(u));
        pairs.Remove("");
        int atoms = pairs.Count, added = 0, wrong = 0;

        var fc = new FastConv(Array.Empty<RewriteRule>(), S.ReversePost, pairs);
        var byPref = Spellings(truth.Where(t => S.Excluded(t) is null)).GroupBy(s => s.Hex).ToDictionary(g => g.Key, g => g.Select(s => s.T.U).ToList(), StringComparer.Ordinal);
        foreach (var (hex, u) in canon.Where(kv => kv.Key.Length > 0).OrderBy(kv => Staged(FromHex(kv.Key)).Length).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string key = Staged(FromHex(hex));
            string got = fc.ConvertStaged(key);
            if (Same(got, u, hex, forward) || byPref[hex].Any(w => Same(got, w, hex, forward))) continue;
            if (key.Length == 0) { wrong++; continue; }
            log?.Add($"{u}\t{hex}\t{Hex(key)}\tread {got}");
            fc.Set(key, Visual(u));
            added++;
        }
        stats = $"atoms {atoms}, learned {added}, unkeyable {wrong}, total {pairs.Count}";
        return pairs;
    }

    public List<string> SameText { get; } = new();

    public string Check(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0;
        SameText.Clear();
        foreach (var (t, hex) in Spellings(truth))
        {
            if (hex.Length == 0 || S.Excluded(t) is not null) continue;
            string got = reader.ConvertText(FromHex(hex));
            if (got == t.U) exact++;
            else if (Same(got, t.U, hex, forward)) { same++; SameText.Add($"{Shape(t.U)}\t{hex}\twant {t.U} [{Cps(t.U)}]\tgot {got} [{Cps(got)}]"); }
            else fails.Add($"{Shape(t.U)}\t{hex}\twant {t.U} [{Cps(t.U)}]\tgot {got} [{Cps(got)}]");
        }
        int n = exact + same + fails.Count;
        return $"read back {n}: exact {exact}, same text {same}, wrong {fails.Count} ({100.0 * (exact + same) / n:F2}%)";
    }

    public string CheckWords(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        fails = new List<string>();
        int ok = 0;
        var pool = Checks.Syllables(truth.Where(t => S.Excluded(t) is null));
        var alone = new Dictionary<string, string>(StringComparer.Ordinal);
        var rng = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            // A word of 2-4 syllables reads back as its syllables read alone: no pair or rule reaches across.
            int n = 2 + rng.Next(3);
            var parts = Enumerable.Range(0, n).Select(_ => pool[rng.Next(pool.Count)]).ToList();
            string u = string.Concat(parts.Select(p => p.U));
            string bytes = string.Join(" ", parts.Select(p => p.Pref).Where(p => p.Length > 0));
            string want = string.Concat(parts.Select(p => alone.TryGetValue(p.Pref, out var r) ? r : alone[p.Pref] = reader.ConvertText(FromHex(p.Pref))));
            string got = reader.ConvertText(FromHex(bytes));
            if (got == want || Same(got, u, bytes, forward)) ok++;
            else fails.Add($"{bytes}\twant {u} (syllables read {want})\tgot {got} [{Cps(got)}]");
        }
        return $"words read back {count} (seed {seed}): right {ok}, wrong {fails.Count} ({100.0 * ok / count:F2}%)";
    }

    /// <summary>The other spellings the composer accepts, read back.</summary>
    public string CheckAccepted(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int ok = 0, n = 0;
        foreach (var t in truth.Where(t => S.Excluded(t) is null))
            foreach (var a in t.Acc.Where(a => a != t.Pref && a != t.Composer && a.Length > 0))
            {
                n++;
                string got = reader.ConvertText(FromHex(a));
                if (Same(got, t.U, a, forward)) ok++;
                else fails.Add($"{a}\twant {t.U}\tgot {got} [{Cps(got)}]");
            }
        return $"other accepted spellings read back {n}: right {ok}, wrong {fails.Count}";
    }
}
