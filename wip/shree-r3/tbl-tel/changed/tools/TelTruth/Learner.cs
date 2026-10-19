using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree-Tel pairs of one family from its truth. It starts from atoms (one pair per character,
/// the subjoined forms, the lone signs and the marks the rules place), then walks the truth strings shortest first and
/// adds a pair only for a string the table so far does not already get right. So a pair is in the table because
/// composing it from smaller pieces gave the wrong bytes - which keeps the table small, and keeps composition honest for
/// strings the composer was never asked about.
/// </summary>
public sealed class Learner
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules = Rules.ForwardPre();
    public readonly List<RewriteRule> PostRules = Rules.ForwardPost();

    public Learner(Fam f) => F = f;

    /// <summary>The joiners: no glyph of their own (a virama before one is the visible halant, the rules' business).</summary>
    public static readonly string[] Dropped = { "\u200C", "\u200D" };

    /// <summary>
    /// Telugu no face has a glyph for: passed through as themselves and reported (P3b) - the combining candrabindu above,
    /// the combining anusvara above, ఌ ౡ and the vocalic-l signs, ఴ, the nukta, ౄ (the composer writes nothing for it, or
    /// the spacer A0 alone), the length mark ౕ, ౘ ౙ ౚ ౝ, the Telugu fraction digits and ౿.
    /// </summary>
    public static readonly string[] NoGlyph =
    {
        "ఀ", "ఄ", "ఌ", "ఴ", "఼", "ౄ", "ౕ", "ౘ", "ౙ", "ౚ", "ౝ",
        "ౡ", "ౢ", "ౣ", "౷", "౸", "౹", "౺", "౻", "౼", "౽", "౾", "౿",
    };

    /// <summary>
    /// The subjoined letters (vattu), read off the composer's క్C rows (క్C = 4D E6 FC + the vattu; Checks.Vattus proves it
    /// against the truth): every consonant but ర (the ra-vattu is the hook, Rules) and ఝ (no face has one). ్ఙ is 94 (the
    /// composer's 5D draws the visarga) and ్ఱ 3C (the composer's CA draws a ూ tail): Capture.Exceptions.
    /// </summary>
    public static readonly (string U, string Hex)[] Vattus =
    {
        ("్క", "50"), ("్ఖ", "55"), ("్గ", "59"), ("్ఘ", "83"), ("్ఙ", "94"), ("్చ", "61"), ("్ఛ", "65"), ("్జ", "6A"), ("్ఞ", "71"),
        ("్ట", "74"), ("్ఠ", "78"), ("్డ", "7A"), ("్ఢ", "7C"), ("్ణ", "7E"), ("్త", "A2"), ("్థ", "A6"), ("్ద", "AA"), ("్ధ", "AE"),
        ("్న", "B2"), ("్ప", "B5"), ("్ఫ", "B9"), ("్బ", "BE"), ("్భ", "C2"), ("్మ", "C3"), ("్య", "C5"), ("్ఱ", "3C"), ("్ల", "CF"),
        ("్ళ", "E5"), ("్వ", "D3"), ("్శ", "D8"), ("్ష", "DB"), ("్స", "DE"), ("్హ", "E1"),
    };

    /// <summary>
    /// Pieces every learning run starts from beyond the truth's single characters: the vattus; the length mark \u0C56 4F; the
    /// visible halant mark 8A for a virama on nothing; a vowel sign on nothing (after a character the table passes through)
    /// as the sign's own piece of క's shape (ా 3E, ి EC FC ...); ॥ as two dandas (the composer types nothing for it); the
    /// dashes as the fonts' hyphen 26 (the composer's 9B is a blank glyph); per family ₹ or *.
    /// </summary>
    public (string U, string Hex)[] Extra => Vattus.Concat(new[]
    {
        ("\u0C56", "4F"), ("్\u200C", "8A"), ("్", "8A"),
        ("ా", "3E"), ("ి", "EC FC"), ("ీ", "EE FC"), ("ు", "24"), ("ూ", "2A"),
        ("ె", "F0 FC"), ("ే", "F3 FC"), ("ొ", "F6"), ("ో", "F8"), ("ౌ", "FA"),
        ("॥", "91 91"), ("–", "26"), ("—", "26 26"),
    }).Concat(F.RupeeAt92 ? new[] { ("₹", "92") } : new[] { ("*", "92") }).ToArray();

    public FontConverter Stager() =>
        new(Table("s", "s", "a", "b", FontType.Unicode, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));

    /// <summary>Atoms every learning run starts from.</summary>
    public Dictionary<string, string> Atoms(IEnumerable<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in train.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0)) atoms[t.U] = FromHex(t.Pref);
        foreach (var (u, hex) in Extra) atoms[u] = FromHex(hex);
        foreach (var d in Dropped) atoms[d] = Rules.Keep;
        foreach (var (mark, hex, _) in Rules.Marks) atoms[mark] = FromHex(hex);
        return atoms;
    }

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table("unicode-to-" + F.Id, "Unicode -> " + F.Name, "gautami", F.Id, FontType.Unicode, FontType.Legacy, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    public Dictionary<string, string> Learn(IEnumerable<Truth> trainSet, out string stats, List<string>? log = null)
    {
        var train = trainSet.ToList();
        var pairs = Atoms(train);
        int atoms = pairs.Count;
        var stager = Stager();
        var staged = train.ToDictionary(t => t.U, t => stager.ConvertText(t.U), StringComparer.Ordinal);
        int added = 0, conflicts = 0;
        foreach (var level in train.GroupBy(t => staged[t.U].Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs));
            foreach (var t in level.OrderBy(t => t.U, StringComparer.Ordinal))
            {
                if (t.Pref.Length == 0) continue;
                if (Reported(t.Pref).Count > 0) continue;
                string got = Hex(conv.ConvertText(t.U));
                if (got == t.Pref) continue;
                string key = staged[t.U];
                if (key.Length == 0) continue;
                if (pairs.TryGetValue(key, out var have) && Hex(have.Replace(Rules.Keep, "")) != t.Pref) conflicts++;
                log?.Add($"{t.U}\t{Show(key)}\t{t.Pref}\tcomposed {got}");
                pairs[key] = FromHex(t.Pref);
                added++;
            }
        }
        stats = $"{F.Id}: atoms {atoms}, learned {added}, conflicts {conflicts}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>A staged key, readable: the rules' marks shown by name.</summary>
    public static string Show(string key)
    {
        foreach (var (mark, hex, _) in Rules.Marks) key = key.Replace(mark, "<" + hex + ">");
        return key.Replace(Rules.Keep, "<K>");
    }
}
