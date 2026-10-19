using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree-Guj -> Unicode pairs: every preferred composer spelling must
/// read back to its Unicode, or to a Unicode string the composer writes the same way.
/// The pre rules undo what the composer does to the order (spacers, nukta, reph); the
/// pairs answer in visual order (the i-sign before its cluster, as the bytes are), and
/// one post rule moves the i-sign to its Unicode place.
/// </summary>
public static class Reader
{
    /// <summary>In the 7 fonts these five codes are copies of other glyphs.</summary>
    public static readonly (string Hex, string Seven, string Hex7)[] SevenCopies =
    {
        ("91", "પ", "60"),                         // પ
        ("92", "થ", "5C"),                         // થ
        ("93", "ન", "5F"),                         // ન
        ("94", "સ્ત્ર", "F7"), // સ્ત્ર
        ("B6", "્રૂ", "B4"),             // ્રૂ (zero-width mark)
    };

    /// <summary>
    /// Codes of the font the composer does not write (its own ligatures, variants and
    /// punctuation), as the font draws them - so text typed with other Shree tools reads.
    /// </summary>
    public static readonly (string Hex, string Unicode)[] FontCodes =
    {
        ("22", "‘"), ("23", "×"), ("26", "।"), ("27", "’"), ("81", "–"), ("8D", "÷"), ("B7", "•"),
        ("5D", "ીં"), ("74", "િં"), ("77", "ૂ"), ("7A", "ુ"), ("BB", "ં"), ("B5", "ૅ"),
        ("78", "ડ્ઢ"), ("7B", "રુ"), ("EA", "રૂ"),
        ("82", "ગ્ન"), ("86", "ઞ્ચ"), ("87", "ઞ્જ"), ("88", "જ્જ"),
        ("89", "લ્લ"), ("8A", "હ્લ"), ("8B", "હ્વ"), ("8C", "હ્ન"),
        ("95", "દ્ગ"), ("96", "ઢ્ઢ"), ("98", "દ્ભ"), ("9B", "ષ્ઠ"),
        ("AA", "હ્ણ"), ("AC", "ઙ્મ"), ("BD", "ક્ક"), ("CB", "ટ્ટ"),
        ("CC", "ઠ્ઠ"), ("CD", "ડ્ડ"), ("D8", "દ્દ"), ("D9", "દ્ધ"),
        ("DA", "દ્મ"), ("DB", "દ્ય"), ("DC", "દ્વ"), ("DF", "ન્ન"),
        ("E1", "પ્ત"), ("F0", "શ્વ"), ("F1", "શ્ન"), ("F2", "શ્ચ"),
        ("F4", "ષ્ટ"), ("F7", "સ્ત્ર"), ("FB", "હ્મ"), ("FC", "હ્ય"),
        ("FE", "ળ્"), ("B4", "્રૂ"), ("B3", "્રુ"),
        // Signs the composer only ever writes inside a longer spelling.
        ("72", "િ"), ("5B", "િ"), ("BA", "ુ"), ("9F", "ૂ"), ("AE", "ર્"),
        ("B0", "્ર"), ("B2", "્ર"), ("E9", "્ય"), ("99", "્વ"),
        // P5 (2026-10-18): every spelling Shree or these tables ever wrote reads back - the new ruu (code EA = 7E),
        // the rupee (code F8 = 3C, which the installed fonts cannot draw), the short dash (code EE = 90), the
        // abbreviation sign (8F, which the tables no longer write), and a double quote typed as two single quotes.
        ("7E", Policy.NewRuu), ("3C", "₹"), ("90", "‐"), ("8F", "૰"), ("22 22", "“"), ("27 27", "”"),
    };

    /// <summary>In the 4/5/6 fonts the same five codes are the ઙ conjuncts.</summary>
    public static readonly (string Hex, string Unicode)[] NgaConjuncts =
    {
        ("91", "ઙ્ક"),             // ઙ્ક
        ("92", "ઙ્ખ"),             // ઙ્ખ
        ("93", "ઙ્ગ"),             // ઙ્ગ
        ("94", "ઙ્ઘ"),             // ઙ્ઘ
        ("B6", "ઙ્ક્ષ"), // ઙ્ક્ષ
    };

    public static readonly List<RewriteRule> PreRules = Rules.ReversePre();
    /// <summary>The 4/5/6 pre rules: 91 92 93 94 B6 are its ઙ conjuncts, letters a reph or nukta moves past.</summary>
    public static readonly List<RewriteRule> PreRules456 = Rules.ReversePre(fourFiveSix: true);
    public static readonly List<RewriteRule> PostRules = Rules.ReversePost();

    static bool Plain(string u) => !u.Any(c => IsJoiner(c) || Learner.Dropped.Contains(c.ToString()));

    /// <summary>
    /// Every spelling of a row the reverse table must read: the one the table writes, and the
    /// composer's own answer for a row that ends in a virama (the half form it types while it
    /// waits for the next letter is still a reading of that letter + virama).
    /// </summary>
    public static IEnumerable<(Truth T, string Hex)> Spellings(IEnumerable<Truth> truth) =>
        truth.SelectMany(t => t.Composer is { } c && c != t.Pref && IsVirama(t.U[^1]) ? new[] { (t, t.Pref), (t, c) } : new[] { (t, t.Pref) });

    /// <summary>The Unicode a spelling reads back as, when several strings share it.</summary>
    public static Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        Spellings(truth).GroupBy(s => s.Hex)
             .ToDictionary(g => g.Key, g => g.Select(s => s.T.U).OrderBy(u => Plain(u) ? 0 : 1).ThenBy(u => u.Count(c => c == '\u0A81')).ThenBy(u => u.Length)
                                              .ThenBy(u => u, StringComparer.Ordinal).First(), StringComparer.Ordinal);

    public static MappingTable Build(Dictionary<string, string> pairs, string id, string name, string source, List<RewriteRule>? pre = null) =>
        Table(id, name, source, "shruti", FontType.Legacy, FontType.Unicode, pre ?? PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    static FontConverter? _stager, _visual;

    /// <summary>A legacy spelling after the pre rules: what the pairs see.</summary>
    public static string Staged(string legacy)
    {
        _stager ??= new FontConverter(Table("s", "s", "a", "b", FontType.Legacy, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));
        return _stager.ConvertText(legacy);
    }

    /// <summary>Unicode with the i-sign before its cluster, as a pair answers.</summary>
    public static string Visual(string unicode)
    {
        _visual ??= new FontConverter(Table("v", "v", "a", "b", FontType.Unicode, FontType.Unicode, new[] { Learner.ISignRule }, Array.Empty<(string, string)>()));
        return _visual.ConvertText(unicode);
    }

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> for this
    /// font: equal, equal but for the characters the font has no glyph for (the vocalic-l
    /// signs and letters, the joiners - nothing in the bytes says they were there), or
    /// Gujarati that the composer writes with the very same codes.
    /// </summary>
    public static bool Same(string got, string want, string prefHex, FontConverter forward)
    {
        if (got == want || got == Visible(want)) return true;
        var r = forward.Convert(got);
        if (!r.HasUnmapped && Hex(r.Text) == prefHex) return true;
        // Text that ends in a virama, read from the half form the composer types while it waits
        // for the next letter: the table writes the halant there at the end of a word, but with
        // a letter after it, the very same codes.
        if (got.Length == 0 || !IsVirama(got[^1])) return false;
        var n = forward.Convert(got + "ક");
        return !n.HasUnmapped && Hex(n.Text) == (prefHex + " " + Hex(forward.ConvertText("ક"))).Trim();
    }

    /// <summary>Text without the characters the font cannot draw.</summary>
    public static string Visible(string u) => new(u.Where(c => !Learner.Dropped.Contains(c.ToString())).ToArray());

    public static Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0))
            if (canon[t.Pref] == t.U) pairs[Staged(FromHex(t.Pref))] = t.U;
        foreach (var (u, hex) in Learner.Extra.Where(e => e.Hex.Length > 0)) pairs.TryAdd(Staged(FromHex(hex)), u);
        foreach (var (hex, u) in FontCodes) pairs.TryAdd(FromHex(hex), Visual(u));
        // The markers the pre rules leave: a halant typed inside a word, a half form with no letter after it.
        pairs[Rules.ZwnjMark] = "‌";
        pairs[Rules.ZwjMark] = "‍";
        int atoms = pairs.Count, added = 0, wrong = 0;

        // (By the length of the key a spelling is learned under - without the ZWJ marker a half form
        // with nothing after it stages with - so a half form is learned before the clusters it begins.)
        foreach (var level in canon.Where(kv => kv.Key.Length > 0).GroupBy(kv => Staged(FromHex(kv.Key)).TrimEnd(Rules.ZwjMark[0]).Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs, "r", "r", "x"));
            foreach (var (hex, u) in level.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                string got = conv.ConvertText(FromHex(hex));
                if (Same(got, u, hex, forward)) continue;
                // A half form with no letter after it stages with the ZWJ marker after it; the marker has a
                // pair of its own, so the key is the half form alone (it is the same half form before a letter).
                string key = Staged(FromHex(hex)).TrimEnd(Rules.ZwjMark[0]);
                if (key.Length == 0) { wrong++; continue; }
                log?.Add($"{u}\t{hex}\t{Hex(key)}\tread {got}");
                pairs[key] = Visual(u);
                added++;
            }
        }
        stats = $"atoms {atoms}, learned {added}, unkeyable {wrong}, total {pairs.Count}";
        return pairs;
    }

    public static string Check(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0;
        foreach (var (t, hex) in Spellings(truth))
        {
            if (hex.Length == 0) continue;
            string got = reader.ConvertText(FromHex(hex));
            if (got == t.U) exact++;
            else if (Same(got, t.U, hex, forward)) same++;
            else fails.Add($"{hex}\twant {t.U} [{Cps(t.U)}]\tgot {got} [{Cps(got)}]");
        }
        int n = exact + same + fails.Count;
        return $"read back {n}: exact {exact}, same text {same}, wrong {fails.Count} ({100.0 * (exact + same) / n:F2}%)";
    }

    /// <summary>
    /// Joined syllables read back. <paramref name="visibleOnly"/> leaves out the syllables
    /// with a character the font has no glyph for: their bytes carry no trace of it, so
    /// a reph on an invisible ૡ reads as a reph on the letter before, as it looks.
    /// </summary>
    public static string CheckWords(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, int count, int seed,
        bool visibleOnly, out List<string> fails)
    {
        var pool = visibleOnly ? truth.Where(t => Visible(t.U) == t.U).ToList() : truth.ToList();
        fails = new List<string>();
        int ok = 0;
        foreach (var (u, want) in Checks.MakeWords(pool, count, seed))
        {
            string got = reader.ConvertText(FromHex(want));
            if (Same(got, u, want, forward)) ok++;
            else fails.Add($"{want}\twant {u}\tgot {got} [{Cps(got)}]");
        }
        return $"words read back {count}{(visibleOnly ? " (visible characters only)" : "")}: right {ok}, wrong {fails.Count} ({100.0 * ok / count:F2}%)";
    }

    /// <summary>The other spellings the composer accepts, read back.</summary>
    public static string CheckAccepted(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int ok = 0, n = 0;
        foreach (var t in truth)
            foreach (var a in t.Acc.Where(a => a != t.Pref && a != t.Composer && a.Length > 0))
            {
                n++;
                string got = reader.ConvertText(FromHex(a));
                // (or the same text as the spelling read: a half form at the end of a text reads as letter + virama + ZWJ)
                if (Same(got, t.U, t.Pref, forward) || Same(got, t.U, a, forward)) ok++;
                else fails.Add($"{a}\twant {t.U}\tgot {got} [{Cps(got)}]");
            }
        return $"other accepted spellings read back {n}: right {ok}, wrong {fails.Count}";
    }
}
