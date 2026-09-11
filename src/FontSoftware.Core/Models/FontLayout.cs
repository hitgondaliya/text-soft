using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// A "Font Layout" entry as shown in Shree-Lipi's top Font-Layout dropdown
/// (Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Terafont, ISFOC, Anu, …). This is the
/// font PRODUCT / encoding family axis — separate from the keyboard layout.
///
/// Selecting one decides how text is rendered (and, for legacy families, the
/// output encoding). "Unicode" is universal: it renders in any installed Unicode
/// font. Families whose font files are not present are still listed (so the client
/// sees everything the software supports) and marked not-installed at selection.
/// </summary>
public sealed class FontLayout
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public FontType Type { get; set; } = FontType.Legacy;

    /// <summary>
    /// Id of the <see cref="FontAsset"/> this layout renders/encodes with, when we
    /// already ship or map it (e.g. ShreeLipi-Ex → shree-devx). Null for families
    /// whose fonts the client has not provided yet — listed but not yet functional.
    /// </summary>
    [JsonPropertyName("fontId")]
    public string? FontId { get; set; }

    /// <summary>True when this is the universal Unicode layout (always works).</summary>
    [JsonIgnore]
    public bool IsUnicode => Type == FontType.Unicode;

    public override string ToString() => Name;
}
