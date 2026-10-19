/// <summary>
/// What the Odia tables write where the composer's own row is not the answer (2026-10-18). Every decision is in the
/// fixture's "about" and "exceptions".
/// </summary>
public static class Policy
{
    /// <summary>
    /// Characters no code point of the fonts draws visibly, passed through as themselves and reported (P3b) - never left
    /// out: ₹ (3C is in WORI7001/2 only; SHREE-ORI7-0600..0602 and every SHREE-ORI face show Word's fallback '&lt;' there), ୰
    /// and ଽ (their glyphs sit at U+008F U+0090, C1 controls Word and RichEdit draw as nothing, and no other code reaches
    /// them), and what the composer types nothing for: ୄ ୢ ୣ (codes F8 F6 F7), ଵ (EA), ୡ (E4), the nukta on a letter with no
    /// nukta glyph (E6), the overline ୕ and the fraction signs ୲-୷ (no code at all). Word draws them in a fallback font.
    /// </summary>
    public static readonly string[] NoGlyph =
    {
        "₹", "୰", "ଽ", "ୄ", "ୢ", "ୣ", "ଵ", "ୡ", "଼", "୕",
        "୲", "୳", "୴", "୵", "୶", "୷",
    };

    /// <summary>The truth rows the tables write otherwise than the composer (besides ନ 9C -> 5B, Family.cs), and why.</summary>
    public static readonly (string U, string Composer, string Why)[] Rows =
    {
        ("₹", "3C", "P3b: the composer writes 3C, which only WORI7001/2 draw as ₹ (SHREE-ORI7-0600..0602 and SHREE-ORI-* have no 3C; Word shows its fallback '<'); passed through as itself, reported - Ctrl+Shift+1 then $ keeps typing ₹"),
        ("୰", "8F", "P3b: the composer writes 8F, which the fonts hold at U+008F, a C1 control Word and RichEdit draw as nothing (r3\\word\\final-invisible.txt), and no other code point of the fonts reaches that glyph; passed through as itself, reported"),
        ("ଽ", "90", "P3b: the composer writes 90 (U+0090, a C1 control Word and RichEdit draw as nothing; no other code point reaches the glyph); passed through as itself, reported"),
    };

    /// <summary>
    /// The forward decisions of the output policy beyond the shared look-alikes (hex; null = left out and reported; "=" =
    /// passed through as itself and reported), whether it is reported, and why.
    /// </summary>
    public static readonly Dictionary<string, (string? Hex, bool Report, string Why)> Overrides = new(StringComparer.Ordinal)
    {
        ["–"] = ("2D", false, "P3/P4: the composer types nothing for – (code 7D), and its own code point U+2013 (96) draws the ligature ଦ୍ଧ୍ୱ in these fonts; the fonts' hyphen, which they draw as itself"),
        ["—"] = ("2D 2D", false, "P3/P4: the composer types nothing for — (code 7B), and U+2014 (97) draws ଦ୍ଭ; two hyphens"),
        ["‐"] = ("2D", false, "P3: a hyphen; the fonts' hyphen 2D"),
        ["‑"] = ("2D", false, "P3: a non-breaking hyphen; the fonts' hyphen 2D"),
    };
}
