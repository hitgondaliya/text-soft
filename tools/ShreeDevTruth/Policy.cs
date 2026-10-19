/// <summary>
/// The output policy (tools/Shared/ShreeNonIndic.cs, P2-P4, 2026-10-18) for the Devanagari and
/// Sanskrit tables: the characters whose composer spelling holds a code Word and RichEdit draw as
/// nothing (the C1 codes 81 8D 8F 90 - Word-visibility map, r3\word), and the characters the fonts
/// have no glyph for. Only these policy items change; every syllable stays as the composer writes it.
/// </summary>
public static class Policy
{
    /// <summary>What the forward tables write instead (hex; null = left out and reported), whether it is still reported, and why.</summary>
    public static Dictionary<string, (string? Hex, bool Report, string Why)> Overrides(string family) => family switch
    {
        "dev7" or "dev456" => new(StringComparer.Ordinal)
        {
            ["–"] = ("2D", false, "P4: the composer writes 8D, which Word and RichEdit draw as nothing; the hyphen, which the fonts draw as itself"),
            ["—"] = ("2D 2D", false, "P4: the composer writes 8D 8D (drawn as nothing by Word); two hyphens"),
            ["×"] = (null, true, "P4: the composer writes 81, which Word draws as nothing, and no other code of the fonts draws ×"),
            ["÷"] = (null, true, "P4: the composer writes 8F, which Word draws as nothing, and no other code of the fonts draws ÷"),
            ["ऄ"] = ("=", true, "P3b: the composer writes अ + 90, a blank that Word draws as nothing (its PDF shows a bullet), and no code of the fonts draws ऄ: passed through as itself (Word draws it in a fallback font), reported - not अ, which reads back as another letter"),
            ["ऎ"] = ("=", true, "P3b: the composer writes ए + 90 (a blank Word draws as nothing), and no code of the fonts draws the short e: passed through as itself, reported - not ए"),
            ["ऒ"] = ("=", true, "P3b: the composer writes ओ's अ ा + 90 (a blank Word draws as nothing), and no code of the fonts draws the short o: passed through as itself, reported - not आ"),
            ["ॆ"] = ("=", true, "P3b: the composer writes only 90, a blank Word draws as nothing; the fonts have no short e sign: passed through as itself (Word draws it in a fallback font), reported"),
            ["ॊ"] = ("=", true, "P3b: the composer writes ा + 90 (a blank Word draws as nothing), and no code of the fonts draws the short o sign: passed through as itself, reported - not ा (कॊ read back as का)"),
        },
        "san7" => new(StringComparer.Ordinal)
        {
            // Dev7.ForwardPre(sanskrit) moves it before its letter (as for DEV7), where the cluster rules expect it.
            ["़"] = (null, true, "the Sanskrit fonts have no nukta and Shree's Sanskrit composer drops it (P1): its letter is written as the composer writes it, and the nukta is reported - it was dropped silently. Not passed through (P3b): the rules can only carry it before its letter, where Word would draw it on the letter before"),
        },
        _ => throw new ArgumentException("unknown family " + family),
    };

    /// <summary>
    /// The signs the forward table passes through as themselves (P3b: the fonts have no glyph for them; by a pair, or
    /// with no pair at all). A reph is written before them, on its letter - Word draws them in a fallback font, on a
    /// dotted circle of their own, and a reph after one would sit on that circle, not on its letter. Build checks the
    /// list: every sign the forward table passes through is here.
    /// </summary>
    public static string PassedSigns(string family) => family == "san7" ? "ऀऺऻॆॊॎॏ॓॔ॕॖॗ" : "ऀऺऻॆॊॎॏ॑॓॔ॕॖॗॢॣ";

    /// <summary>Characters the fonts draw nothing for: passed through as themselves and reported (P3b), never dropped silently.</summary>
    public static string[] NoGlyph(string family) => family == "san7"
        ? new[] { "ॡ", "ॆ" }
        : new[] { "ऌ", "ॡ", "ॢ", "ॣ", "॑" };

    /// <summary>A truth row the policy writes otherwise than the composer (the ones in <see cref="Overrides"/>), or null.</summary>
    public static (string Pref, string Why)? Adjust(string family, Truth t) =>
        Overrides(family).TryGetValue(t.U, out var o) ? (o.Hex ?? "", o.Why) : null;
}
