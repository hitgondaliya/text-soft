using static Common;

/// <summary>
/// What the Gujarati tables write where the composer's own row is not the answer (2026-10-18):
/// the ru and ruu ligatures Shree's ru/ruu keys type, the characters the output policy
/// (tools/Shared/ShreeNonIndic.cs, P2-P4) decides, and the noncharacter markers the engine
/// and the rules use. Every decision is written into the fixture's "exceptions".
/// </summary>
public static class Policy
{
    /// <summary>
    /// રુ and રૂ: Shree's own ru and ruu keys type ligatures - the codes E9 (ru) and 7E (the old ruu,
    /// the shape the Shree-Lipi 7 fonts draw for ruu) - and the 7 composer writes them as below
    /// (S8Guj764, saved code runs, r3\guj-audit\results\composer-sequences.json runs2, 2026-10-18).
    /// The truth rows were captured by feeding ર and the sign as two codes (66 79 / 66 7C: ર with
    /// the sign hanging under it), which the composer also writes and which stay accepted.
    /// </summary>
    public static readonly (string U, string Hex, string Why)[] RuLigatures =
    {
        ("રુ", "7B 24", "the ru ligature, as Shree's ru key (code E9) types it: 7B 24"),
        ("રું", "7B 84 24", "the ru ligature with the anusvara, as Shree types code E9 then 69: 7B 84 24"),
        ("રૂ", "EA 24", "the ruu ligature, as Shree's ruu key (code 7E, the old ruu shape the Shree-Lipi 7 fonts draw for ruu) types it: EA 24"),
        ("રૂં", "EA 84 24", "the ruu ligature with the anusvara, as Shree types code 7E then 69: EA 84 24"),
        ("ર્રુ", "7B AE 24", "the ru ligature with the reph, as Shree types code E9 then 40: 7B AE 24"),
        ("ર્રૂ", "EA AE 24", "the ruu ligature with the reph, as Shree types code 7E then 40: EA AE 24"),
        ("ર્રું", "7B AF 24", "the ru ligature with the anusvara and the reph - not captured; the composer's own rules give it (anusvara + reph = AF, before the spacer)"),
        ("ર્રૂં", "EA AF 24", "the ruu ligature with the anusvara and the reph - not captured; the composer's own rules give it (anusvara + reph = AF, before the spacer)"),
    };

    /// <summary>The new ruu shape (રૂ + variation selector 1, as in shree-gujx): Shree's code EA, written 7E 24 (with the anusvara 7E 84 24).</summary>
    public const string NewRuu = "રૂ︀";

    /// <summary>
    /// Truth rows the policy writes otherwise than the composer: ૰, whose code 8F Word and RichEdit
    /// draw as nothing, is passed through as itself and reported (P3b; "" would be left out).
    /// </summary>
    public static readonly (string U, string Hex, string Why)[] Rows =
    {
        ("૰", "U+0AF0", "P3b: the composer writes 8F, which Word and RichEdit draw as nothing, and no other code of the fonts reaches that glyph; ૰ itself is not one of the fonts' codes, so it is passed through as itself (Word draws it in a fallback font) and reported"),
    };

    /// <summary>A truth row the tables write otherwise than the composer, or null.</summary>
    public static (string Pref, string Why)? Adjust(Truth t)
    {
        foreach (var (u, hex, why) in RuLigatures) if (t.U == u) return (hex, why);
        foreach (var (u, hex, why) in Rows) if (t.U == u) return (hex, why);
        return null;
    }

    /// <summary>
    /// The forward decisions of the output policy for the Gujarati families (beyond the ASCII rules
    /// in NonIndic.Ascii): hex (null = left out and reported), whether it is still reported, why.
    /// </summary>
    public static readonly Dictionary<string, (string? Hex, bool Report, string Why)> Overrides = new(StringComparer.Ordinal)
    {
        ["–"] = ("2D", false, "P4: the composer writes 81, which Word and RichEdit draw as nothing; the fonts' only visible dash is the hyphen, which they draw as itself"),
        ["—"] = ("2D 2D", false, "P4: the composer writes 81 81 (drawn as nothing by Word); two hyphens"),
        ["‐"] = ("2D", false, "P4: Shree's short dash (code EE) is byte 90, which Word draws as nothing; the hyphen"),
        ["÷"] = (null, true, "P4: the composer writes 8D, which Word draws as nothing, and no other code of the fonts draws ÷; its own code point is F7, which the fonts draw as a letter, so it is left out"),
        ["૰"] = ("=", true, "P3b: the composer writes 8F, which Word draws as nothing, and no other code of the fonts draws ૰; passed through as itself (Word draws it in a fallback font)"),
        ["₹"] = ("=", true, "P3b: the composer writes 3C, where none of the installed SHREE-GUJ7/SHREE-GUJ/Shree-Guj7 fonts has a glyph (Word draws a '<' there), and they have no rupee sign; passed through as itself (Word draws it in a fallback font) - Ctrl+Shift+1 then $ keeps typing ₹"),
        ["૱"] = ("=", true, "P3b: the fonts have no glyph for it, and the composer types nothing for its code (EB); passed through as itself"),
    };

    /// <summary>Characters the fonts draw nothing for: passed through as themselves and reported (P3b), never dropped silently.</summary>
    public static readonly string[] NoGlyph = { "ઌ", "ૡ", "ૢ", "ૣ" };
}
