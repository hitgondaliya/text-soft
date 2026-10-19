using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// Lightweight pointer from a language to a keyboard layout definition file.
/// The full <see cref="KeyboardLayout"/> is loaded on demand.
/// </summary>
public sealed class LayoutRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Path (relative to config root) to the layout JSON file.</summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    public override string ToString() => Name;
}
