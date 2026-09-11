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

    /// <summary>Distinct source characters that were passed through unmapped.</summary>
    public IReadOnlyList<string> UnmappedCharacters { get; init; } = Array.Empty<string>();

    public bool HasUnmapped => UnmappedCharacters.Count > 0;
}
