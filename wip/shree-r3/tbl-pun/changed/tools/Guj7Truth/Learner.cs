using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree-Guj7 pairs from the composer truth. It starts from
/// atoms (one pair per character), then walks the truth strings shortest first and
/// adds a pair only for a string the table so far does not already get right. So a
/// pair is in the table because composing it from smaller pieces gave the wrong
/// bytes - which keeps the table small, and keeps composition honest for strings
/// the composer was never asked about.
/// </summary>
public static class Learner
{
    public const string Cons = "ક-હ";

    public static readonly RewriteRule ISignRule = new()
    {
        Pattern = $"((?:[{Cons}]઼?્)*[{Cons}]઼?)િ",
        Replace = "િ$1",
        Note = "Move pre-base matra U+0ABF before the consonant cluster (Unicode logical order -> legacy visual order).",
    };

    /// <summary>Characters the 7 composer writes nothing for (the font has no glyph).</summary>
    public static readonly string[] Dropped = { "ૢ", "ૣ", "ઌ", "ૡ", "‌", "‍" };

    /// <summary>Characters outside the truth, as the earlier hand-made table wrote them.</summary>
    public static readonly (string U, string Hex)[] Extra =
    {
        ("‘", "22"), ("×", "23"), ("’", "27"), ("÷", "8D"),
        ("્", "B9"), ("–", "81"), ("—", "81 81"),
        // Shree's own spellings of characters the truth does not hold (2026-10-18, S8Guj764 code runs):
        // the new ruu shape (code EA), the double danda (two dandas, codes D3 D3), the short dash (code EE).
        // What the table writes for those Word cannot show is decided by Policy.Overrides; these stay what
        // the reverse tables read (P5).
        (Policy.NewRuu, "7E 24"), ("॥", "97 26 26"), ("‐", "90"),
    };

    /// <summary>Codes that draw something else in the 4/5/6 fonts than in the 7 fonts.</summary>
    public static readonly HashSet<string> FamilyCodes = new() { "91", "92", "93", "94", "B6" };

    public static bool Unsafe(string hex) => hex.Length > 0 && hex.Split(' ').Any(FamilyCodes.Contains);

    /// <summary>GUJ7_RULES=0 learns with the i-sign rule alone (for comparison).</summary>
    public static readonly bool UseRules = Environment.GetEnvironmentVariable("GUJ7_RULES") != "0";
    public static List<RewriteRule> PreRules = UseRules ? Rules.ForwardPre() : new() { ISignRule };
    public static List<RewriteRule> PostRules = UseRules ? Rules.ForwardPost() : new();

    public static FontConverter Stager() =>
        new(Table("s", "s", "a", "b", FontType.Unicode, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));

    /// <summary>Atoms every learning run starts from.</summary>
    public static Dictionary<string, string> Atoms(IReadOnlyList<Truth> all, IEnumerable<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        // Characters the composer was never asked about: what the earlier table said.
        foreach (var (u, hex) in Extra) atoms[u] = FromHex(hex);
        foreach (var d in Dropped) atoms[d] = "";
        if (UseRules)
        {
            // The vocalic-l letters and signs and the joiners have no glyph, but they still
            // stand where they were typed: the composer keeps the signs around them where
            // they are, and a joiner keeps two letters from joining.
            foreach (var d in Dropped) atoms[d] = Rules.Keep;
            atoms[Rules.RephMark] = FromHex("AE");
            atoms[Rules.YaMark] = FromHex("E9");
            // A virama with no letter before ન that the i-sign or nukta rule has moved away from its
            // ન: nothing (the composer writes ્નિ as નિ = 72 5F) - the marker never reaches the bytes.
            atoms[Rules.LoneVirama] = "";
            // The engine's 'the word goes on' marker (see Rules.Waiting): nothing.
            atoms[Rules.Waiting] = "";
        }
        foreach (var t in train.Where(t => t.U.Length == 1)) atoms[t.U] = FromHex(t.Pref);
        // The half letters: what the composer answers for a letter + virama while it waits for
        // the next letter. At the end of a word the table writes the halant instead (the row's
        // Pref, which the pre rules key as letter + virama + ZWNJ), but before a letter this is
        // the half form that letter joins.
        int kept = 0;
        foreach (var t in train.Where(t => Waiting(t) is { Length: > 0 } && t.U.Length == 2 && IsCons(t.U[0]) && IsVirama(t.U[1])))
            atoms[t.U] = Spell(Waiting(t)!, ref kept);
        return atoms;
    }

    /// <summary>FontConverter's greedy longest match over already staged text, then the post rules.</summary>
    static string Compose(string staged, Dictionary<string, string> pairs)
    {
        int max = pairs.Keys.Max(k => k.Length);
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < staged.Length)
        {
            int len = Math.Min(max, staged.Length - i);
            for (; len > 0; len--)
                if (pairs.TryGetValue(staged.Substring(i, len), out var to)) { sb.Append(to); break; }
            if (len == 0) { sb.Append(staged[i]); len = 1; }
            i += len;
        }
        return Post(sb.ToString());
    }

    public static MappingTable Build(Dictionary<string, string> pairs, string id = "unicode-to-shree-guj7",
        string name = "Unicode -> Shree-Lipi 7.0 Gujarati", string target = "shree-guj7") =>
        Table(id, name, "shruti", target, FontType.Unicode, FontType.Legacy, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    public static Dictionary<string, string> Learn(IReadOnlyList<Truth> all, IEnumerable<Truth> trainSet, out string stats,
        List<string>? log = null)
    {
        var train = trainSet.ToList();
        var pairs = Atoms(all, train);
        int atoms = pairs.Count;
        var stager = Stager();
        var staged = train.ToDictionary(t => t.U, t => stager.ConvertText(t.U), StringComparer.Ordinal);
        // The composer's waiting answers (a row that ends in a bare virama, which the table
        // writes with the halant at the end of a word) are still what it writes for those
        // letters when the next letter comes - ક્ક્ = BC BC, ગ્ર્ = 4E B0. Each is learned as
        // well, keyed as the pre rules leave the letters with no word end after them (all the
        // pre rules but the first), at its own length like any other string.
        var waitStager = new FontConverter(Table("w", "w", "a", "b", FontType.Unicode, FontType.Legacy, PreRules.Skip(1), Array.Empty<(string, string)>()));
        var items = train.Select(t => (U: t.U, Key: staged[t.U], Pref: t.Pref, Waits: false)).ToList();
        foreach (var t in train.Where(t => Waiting(t) is { Length: > 0 }))
            items.Add((t.U, waitStager.ConvertText(t.U), Waiting(t)!, true));
        int added = 0, conflicts = 0, kept = 0, waiting = 0;
        foreach (var level in items.GroupBy(x => x.Key.Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs));
            var snap = new Dictionary<string, string>(pairs, StringComparer.Ordinal);
            foreach (var x in level.OrderBy(x => x.U, StringComparer.Ordinal).ThenBy(x => x.Waits))
            {
                string got = Hex(x.Waits ? Compose(x.Key, snap) : conv.ConvertText(x.U));
                if (got == x.Pref) continue;
                if (pairs.TryGetValue(x.Key, out var have) && Hex(Post(have)) != x.Pref) conflicts++;
                log?.Add($"{x.U}\t{x.Key}\t{x.Pref}\t{(x.Waits ? "waiting, " : "")}composed {got}");
                pairs[x.Key] = Spell(x.Pref, ref kept);
                if (x.Waits) waiting++; else added++;
            }
        }
        stats = $"atoms {atoms}, learned {added} ({kept} kept from the rules), conflicts {conflicts}, waiting half forms {waiting}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>
    /// For a row that ends in a virama: what the composer answers while it waits for the next
    /// letter (its own preferred answer - the table writes the halant at the end of a word).
    /// </summary>
    public static string? Waiting(Truth t) => t.U.Length > 1 && IsVirama(t.U[^1]) ? t.Composer ?? t.Pref : null;

    static FontConverter? _post;

    /// <summary>What the post rules make of a legacy string.</summary>
    public static string Post(string legacy)
    {
        _post ??= new FontConverter(Table("p", "p", "a", "b", FontType.Legacy, FontType.Legacy,
            Array.Empty<RewriteRule>(), Array.Empty<(string, string)>(), PostRules));
        return _post.ConvertText(legacy);
    }

    /// <summary>The pair output for a composer spelling: as is, or kept away from the rules when they would change it.</summary>
    public static string Spell(string prefHex, ref int kept)
    {
        string legacy = FromHex(prefHex);
        if (Post(legacy) == legacy) return legacy;
        kept++;
        return Rules.Protect(legacy);
    }
}
