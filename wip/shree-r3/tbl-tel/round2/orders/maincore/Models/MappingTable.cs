using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>A single source→target substitution used by the converter (FR-4).</summary>
public sealed class MappingPair
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// The target cannot show <see cref="From"/>: the converter writes <see cref="To"/>
    /// in its place (usually nothing) and still reports <see cref="From"/> as unmapped.
    /// For a legacy font whose code for the character draws a letter - passing it
    /// through would print that letter, and leaving it out silently would lose it.
    /// </summary>
    [JsonPropertyName("unmapped")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Unmapped { get; set; }
}

/// <summary>
/// A regex-based reorder/cleanup rule applied before or after substitution.
/// Handles Indic edge cases (reordered i-matra, conjuncts) per FRD §FR-4 —
/// which are the client's responsibility to define and test.
/// </summary>
public sealed class RewriteRule
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("replace")]
    public string Replace { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Client-supplied mapping table converting text from one font/type to another
/// (FR-4). The engine only applies these rules; correctness of the table itself
/// is a client-side responsibility per the FRD.
///
/// Pipeline: preRules → longest-match pair substitution → postRules.
/// </summary>
public sealed class MappingTable
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Source font id (matches a <see cref="FontAsset.Id"/>).</summary>
    [JsonPropertyName("sourceFontId")]
    public string SourceFontId { get; set; } = string.Empty;

    /// <summary>Target font id.</summary>
    [JsonPropertyName("targetFontId")]
    public string TargetFontId { get; set; } = string.Empty;

    [JsonPropertyName("sourceType")]
    public FontType SourceType { get; set; }

    [JsonPropertyName("targetType")]
    public FontType TargetType { get; set; }

    /// <summary>Rules applied before substitution (e.g. normalize input).</summary>
    [JsonPropertyName("preRules")]
    public List<RewriteRule> PreRules { get; set; } = new();

    [JsonPropertyName("pairs")]
    public List<MappingPair> Pairs { get; set; } = new();

    /// <summary>Rules applied after substitution (e.g. reorder matras).</summary>
    [JsonPropertyName("postRules")]
    public List<RewriteRule> PostRules { get; set; } = new();
}
