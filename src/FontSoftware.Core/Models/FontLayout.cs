using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// For a layout that gathers several font products under one name (Bhasha
    /// Bharati = the Gopika face, the Bhasha Bharati-1 faces and the Bhasha
    /// Bharti-2 faces): every encoding it covers. Any font of the language whose
    /// encoding is listed belongs to the layout, and each keeps converting with its
    /// OWN table, so whichever of them the client has installed types correctly.
    /// Null for an ordinary layout, whose only encoding is that of <see cref="FontId"/>.
    /// </summary>
    [JsonPropertyName("encodings")]
    public List<string>? Encodings { get; set; }

    /// <summary>
    /// Ids of layouts this one took over (the separate Bhasha Bharti-1/-2 and
    /// Gopika rows), so a default saved under one of them still opens here.
    /// </summary>
    [JsonPropertyName("replaces")]
    public List<string>? Replaces { get; set; }

    /// <summary>
    /// Which fonts belong to this layout, as a pattern over their family name, when
    /// the encoding alone cannot say: Shree-Lipi 7.0 (SHREE-GUJ7-0750) and
    /// Shree-Lipi 4.0/5.0/6.0 (SHREE-GUJ-0750) are two families, and both used to
    /// be listed under either. Matched without regard to case.
    /// </summary>
    [JsonPropertyName("families")]
    public string? Families { get; set; }

    private Regex? _families;

    /// <summary>True when the layout covers more than one encoding.</summary>
    [JsonIgnore]
    public bool IsCombined => Encodings is { Count: > 1 };

    /// <summary>
    /// Does this font belong to the layout? A combined layout goes by encoding, a
    /// layout with <see cref="Families"/> by family name; any other by its own font
    /// and - given <paramref name="ownKey"/>, the conversion key of that font -
    /// every font converting the same way (all the ShreeLipi-Ex faces).
    /// </summary>
    public bool Includes(FontAsset font, string? ownKey = null)
    {
        if (Encodings is { Count: > 0 })
            return Encodings.Contains(font.ConversionKey, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(Families))
        {
            _families ??= new Regex(Families, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return _families.IsMatch(font.Family ?? string.Empty);
        }
        return string.Equals(font.Id, FontId, StringComparison.OrdinalIgnoreCase)
               || (ownKey is not null && string.Equals(font.ConversionKey, ownKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when this is the universal Unicode layout (always works).</summary>
    [JsonIgnore]
    public bool IsUnicode => Type == FontType.Unicode;

    public override string ToString() => Name;
}
