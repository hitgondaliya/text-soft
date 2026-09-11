using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// A phonetic "type by sound" scheme (like Google Indic / Gboard transliteration).
/// The user types roman letters and they compose into the script by rules, rather
/// than each key producing a fixed glyph (that is what a <see cref="KeyboardLayout"/>
/// does). Client-supplied data; the engine only applies it.
/// </summary>
public sealed class TransliterationScheme
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Font this scheme is designed to type in (usually a Unicode font).</summary>
    [JsonPropertyName("fontId")]
    public string? FontId { get; set; }

    /// <summary>The script's virama/halant, inserted between two consonants to form a conjunct.</summary>
    [JsonPropertyName("virama")]
    public string Virama { get; set; } = string.Empty;

    /// <summary>roman token → consonant character(s). Longer tokens win (e.g. "kh" over "k").</summary>
    [JsonPropertyName("consonants")]
    public Dictionary<string, string> Consonants { get; set; } = new();

    /// <summary>
    /// roman token → [independent, matra]. The independent form is used at the start
    /// of a syllable; the matra attaches after a consonant. The inherent "a" uses an
    /// empty matra so a bare consonant keeps its built-in vowel.
    /// </summary>
    [JsonPropertyName("vowels")]
    public Dictionary<string, string[]> Vowels { get; set; } = new();

    /// <summary>roman token → standalone mark (anusvara, visarga, om, digits, …).</summary>
    [JsonPropertyName("specials")]
    public Dictionary<string, string> Specials { get; set; } = new();
}
