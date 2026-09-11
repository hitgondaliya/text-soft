using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// Top-level catalog of all client-supplied languages/fonts/layouts (FR-1).
/// Deserialized from <c>languages.json</c>. Mapping tables and full layout
/// bodies live in separate files referenced from here.
/// </summary>
public sealed class AssetCatalog
{
    [JsonPropertyName("languages")]
    public List<LanguageProfile> Languages { get; set; } = new();

    /// <summary>Paths (relative to config root) of mapping-table JSON files.</summary>
    [JsonPropertyName("mappingFiles")]
    public List<string> MappingFiles { get; set; } = new();

    public LanguageProfile? FindLanguage(string id) =>
        Languages.FirstOrDefault(l => l.Id == id);
}
