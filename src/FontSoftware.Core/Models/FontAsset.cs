using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// A client-supplied font, referenced by the selection UI (FR-1) and the
/// converter (FR-4). Backs the "font is flagged Unicode or Legacy" rule.
/// </summary>
public sealed class FontAsset
{
    /// <summary>Stable id used in config references (e.g. mapping tables).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name shown in the font list.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Installed font family name used to render text in this font.</summary>
    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    /// <summary>Optional path to the font file bundled with the app.</summary>
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("type")]
    public FontType Type { get; set; } = FontType.Unicode;

    /// <summary>
    /// Byte→glyph encoding this font shares with others (FR-4). Fonts with the
    /// same encoding convert with the SAME mapping table, so one table serves a
    /// whole family (e.g. all Shree-DevX weights/italics). Mapping tables are
    /// matched by encoding, not by individual font id. Defaults to <see cref="Id"/>.
    /// </summary>
    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    /// <summary>
    /// Listed only when installed on this PC. For a large family (the Bhasha
    /// Bharati faces run to dozens) a row per missing face would bury the fonts the
    /// client actually has; a font without this flag is always listed, marked
    /// "not installed" when missing.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; set; }

    /// <summary>The key used to look up conversion tables: <see cref="Encoding"/> or, if unset, <see cref="Id"/>.</summary>
    [JsonIgnore]
    public string ConversionKey => string.IsNullOrEmpty(Encoding) ? Id : Encoding;

    /// <summary>
    /// Optional UI-only override for the text shown in the selection list (e.g. a
    /// "not installed" marker). Never persisted; falls back to <see cref="Name"/>.
    /// </summary>
    [JsonIgnore]
    public string? Display { get; set; }

    /// <summary>Text shown in the font dropdown (<see cref="Display"/> or <see cref="Name"/>).</summary>
    [JsonIgnore]
    public string Label => string.IsNullOrEmpty(Display) ? Name : Display;

    /// <summary>
    /// A shallow copy tagged as not installed on this PC. The full supported font
    /// list is always shown so the client can see what the software integrates;
    /// a missing font is marked, not hidden ("not installed" is not our fault).
    /// </summary>
    public FontAsset AsNotInstalled() => new()
    {
        Id = Id,
        Name = Name,
        Family = Family,
        File = File,
        Type = Type,
        Encoding = Encoding,
        Optional = Optional,
        Display = $"{Name}  —  not installed"
    };

    public override string ToString() => Name;
}
