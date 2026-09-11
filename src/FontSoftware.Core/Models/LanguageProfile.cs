using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// A language entry in the asset catalog (FR-1). Owns the fonts and keyboard
/// layouts valid for that language; selection lists are filtered from these.
/// </summary>
public sealed class LanguageProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>A representative script character shown as the app's language badge (e.g. अ, અ, অ).</summary>
    [JsonPropertyName("sample")]
    public string Sample { get; set; } = "अ";

    [JsonPropertyName("fonts")]
    public List<FontAsset> Fonts { get; set; } = new();

    /// <summary>
    /// The Font-Layout dropdown entries (font product / encoding families), copied
    /// from Shree-Lipi's UI — e.g. Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Terafont.
    /// Separate axis from <see cref="Layouts"/> (the keyboard layouts).
    /// </summary>
    [JsonPropertyName("fontLayouts")]
    public List<FontLayout> FontLayouts { get; set; } = new();

    [JsonPropertyName("layouts")]
    public List<LayoutRef> Layouts { get; set; } = new();

    /// <summary>Optional phonetic "type by sound" scheme file (relative to config root).</summary>
    [JsonPropertyName("scheme")]
    public string? SchemeFile { get; set; }

    public override string ToString() => Name;
}
