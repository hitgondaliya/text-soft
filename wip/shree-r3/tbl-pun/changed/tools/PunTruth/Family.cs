using static Common;

/// <summary>
/// The Shree-Lipi Gurmukhi font families. The capture's family check (cap-pun\fonts\family-check.json: (3,1) cmap,
/// glyph id, advance and outline bytes of every installed face) found them the same design code for code - same
/// design pairs WPUN7001/S09510F2 and WPUN7002/S09520F2 identical in 118 of 119 codes - except ₹ at 3C, which only
/// the 7 faces have:
///
///   SHREE-PUN7-0951/0952 (WPUN7001/2.TTF)          the truth as it is: ₹ = 3C.
///   SHREE-PUN-0950/0951/0952/0955/0956 (S09xx0F2)  no glyph at 3C (and no U+003C in the cmap), so ₹ passes through
///                                                  as U+20B9, reported, and Word draws it in another font; '<'
///                                                  passes through too (3C is not in these fonts at all).
///
/// Itself: the ASCII punctuation every face of the family draws as itself at its own code (the Word-visibility map
/// of every installed face, r3\word\final-ascii.txt, 2026-10-18: ! % ( ) + , - . / : ; = ? [ ] in both families),
/// plus * - drawn as a star (an asterisk in two faces) and the composer's own spelling of * (code 2A -> 2A). Every
/// other ASCII code but the letters and digits draws something else: " ‘, ' ’, # ×, $ ÷, &amp; ।, &gt; ਃ, @ an avagraha,
/// \ ^ _ ` | } ~ letters and signs, &lt; ₹ in the 7 faces; x and { are in no face.
/// </summary>
public sealed record Fam(string Id, string Name, string Fonts, string FilePattern, IReadOnlyDictionary<string, string> Map,
    string Never, string Itself)
{
    /// <summary>A truth spelling (7 codes) in this family's codes.</summary>
    public string Apply(string hex)
    {
        if (hex.Length == 0) return hex;
        return Norm(string.Join(" ", hex.Split(' ').Select(t => Map.TryGetValue(t, out var m) ? m : t)));
    }

    /// <summary>Composer bytes as a document in this family holds them: the bytes themselves (Shree writes 3C for ₹ whatever the font).</summary>
    public string Doc(string hex) => hex;

    public List<Truth> Truth(IEnumerable<Truth> truth) => truth.Select(t =>
    {
        string pref = Apply(t.Pref);
        var acc = t.Acc.Select(Apply).Distinct().ToList();
        acc.Remove(pref); acc.Insert(0, pref);
        return new Truth(t.U, pref, acc.ToArray()) { Composer = t.Composer is null ? null : Apply(t.Composer) };
    }).ToList();

    /// <summary>Codes this family's tables must never write: they draw something else here, or nothing.</summary>
    public HashSet<string> NeverCodes => Never.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public bool IsSeven => !Map.ContainsKey("3C");

    /// <summary>
    /// Codes the composer writes only in another typing order, or never, that the reverse table still reads (as both
    /// families draw them): the onkar arc B0 alone (code 51 alone types it - 11 vendor keys), ₹ 3C (Shree's composer
    /// writes it whatever the face; a 4/5/6 face draws nothing there), the avagraha-like 40 and the low dash A9 the
    /// 7 composer types for codes 41 and EE (the Unicode composer answers nothing for them), and the u/uu signs 75 77
    /// and the tippi-like A6 the fonts draw but no composer answer holds.
    /// </summary>
    public (string Hex, string Unicode)[] FontCodes => new[]
    {
        ("B0", "ੴ"), ("3C", "₹"), ("40", "ऽ"), ("A9", "-"), ("75", "ੁ"), ("77", "ੂ"), ("A6", "ੰ"),
    };

    public static readonly Fam Pun7 = new("shree-pun7", "Shree-Lipi 7.0 Punjabi", "SHREE-PUN7-0951/0952", @"^WPUN700[12]\.TTF$",
        new Dictionary<string, string>(), "", "!%()*+,-./:;=?[]");
    public static readonly Fam Pun456 = new("shree-pun456", "Shree-Lipi 4.0/5.0/6.0 Punjabi", "SHREE-PUN-0950/0951/0952/0955/0956", @"^S09\d\d0F2\.TTF$",
        new Dictionary<string, string> { ["3C"] = "U+20B9" }, "3C", "!%()*+,-./:;=?[]");

    public static readonly Fam[] All = { Pun7, Pun456 };

    public static Fam Get(string id) => All.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("no family " + id);
}
