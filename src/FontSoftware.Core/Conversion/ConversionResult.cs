namespace FontSoftware.Core.Conversion;

/// <summary>
/// Output of a converter run (FR-4). Carries the converted text plus any
/// characters that had no mapping, so the UI can flag rather than silently
/// drop them (acceptance criterion: "Unmapped characters are passed through
/// or flagged, not dropped silently").
/// </summary>
public sealed class ConversionResult
{
    public required string Text { get; init; }

    /// <summary>
    /// Every distinct source character worth reporting: the union of <see cref="LeftOutCharacters"/>
    /// (not in <see cref="Text"/> as itself) and <see cref="KeptCharacters"/> (in it as itself).
    /// </summary>
    public IReadOnlyList<string> UnmappedCharacters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The part of <see cref="UnmappedCharacters"/> a table pair marked <see cref="Models.MappingPair.Unmapped"/>
    /// writes WITHOUT the character itself: left out - no trace of it is in <see cref="Text"/> - or written
    /// as a stand-in the target draws (a space for a line separator, the fonts' large bullet for a middle
    /// dot, U+FFFD for a noncharacter). Worth reporting whatever they are, ASCII included: a slash, a plus
    /// or a '[' a legacy font would draw as a letter or a sign is gone from the output, not merely
    /// unconverted, and a screen that hides plain punctuation passing through must still show it.
    /// </summary>
    public IReadOnlyList<string> LeftOutCharacters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The part of <see cref="UnmappedCharacters"/> that is in <see cref="Text"/> as itself: a character no
    /// pair maps (passed through), or one a pair marked unmapped writes as itself because no code of the
    /// target draws it (P3b: ₹ or a vocalic-l sign in a Shree font, which Word draws in a fallback font).
    /// Whitespace is never listed. Into a legacy font these are characters the font has no glyph for; out
    /// of one, codes the table could not read.
    /// </summary>
    public IReadOnlyList<string> KeptCharacters { get; init; } = Array.Empty<string>();

    /// <summary>The same list as <see cref="LeftOutCharacters"/> (the name the Gujarati work gave it).</summary>
    public IReadOnlyList<string> CannotShow => LeftOutCharacters;

    public bool HasUnmapped => UnmappedCharacters.Count > 0;
}
