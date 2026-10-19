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
    /// Distinct source characters that were passed through unmapped - or, where the
    /// table says the target cannot show them (<see cref="Models.MappingPair.Unmapped"/>),
    /// left out rather than written as a letter of the legacy font.
    /// </summary>
    public IReadOnlyList<string> UnmappedCharacters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The part of <see cref="UnmappedCharacters"/> the table itself says the target cannot show (a pair
    /// marked <see cref="Models.MappingPair.Unmapped"/>): left out - no trace of it is in <see cref="Text"/> -
    /// or written as a stand-in (a space for a line separator). Worth reporting whatever they are, ASCII
    /// included: a slash, a plus or a '[' a legacy font would draw as a letter or a sign is gone from the
    /// output, not merely unconverted, and a screen that hides plain punctuation passing through must
    /// still show it.
    /// </summary>
    public IReadOnlyList<string> LeftOutCharacters { get; init; } = Array.Empty<string>();

    /// <summary>The same list as <see cref="LeftOutCharacters"/> (the name the Gujarati work gave it).</summary>
    public IReadOnlyList<string> CannotShow => LeftOutCharacters;

    public bool HasUnmapped => UnmappedCharacters.Count > 0;
}
