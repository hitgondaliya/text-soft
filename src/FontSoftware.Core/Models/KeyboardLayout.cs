using System.Text.Json.Serialization;

namespace FontSoftware.Core.Models;

/// <summary>
/// One physical key's output in a layout. Base is the unshifted character;
/// Shift/AltGr are optional per FR-2 ("optionally reflects Shift/AltGr states").
/// Empty/absent string means the key falls back to its default English char.
/// </summary>
public sealed class KeyMapping
{
    /// <summary>Windows virtual-key code the mapping is keyed by (e.g. 0x41 = 'A').</summary>
    [JsonPropertyName("vk")]
    public int VirtualKey { get; set; }

    [JsonPropertyName("base")]
    public string? Base { get; set; }

    [JsonPropertyName("shift")]
    public string? Shift { get; set; }

    [JsonPropertyName("altGr")]
    public string? AltGr { get; set; }

    /// <summary>
    /// Marks a dead key: its output combines with the next key rather than
    /// emitting immediately. Combination is resolved by the layout tables.
    /// </summary>
    [JsonPropertyName("deadKey")]
    public bool DeadKey { get; set; }
}

/// <summary>
/// Defines how a dead key's output combines with the character typed after it
/// (FR-3: "dead keys behave per the active layout"). If <see cref="Dead"/>
/// followed by <see cref="Next"/> is typed, the two are replaced by
/// <see cref="Result"/> instead of being emitted separately.
/// </summary>
public sealed class DeadKeyCombination
{
    /// <summary>The dead key's own output (its <see cref="KeyMapping.Base"/> etc.).</summary>
    [JsonPropertyName("dead")]
    public string Dead { get; set; } = string.Empty;

    /// <summary>The character produced by the following key.</summary>
    [JsonPropertyName("next")]
    public string Next { get; set; } = string.Empty;

    /// <summary>The combined character(s) to emit instead.</summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
}

/// <summary>
/// A full keyboard layout definition (FR-2). Rendering and the typing engine
/// (FR-3) are driven entirely by this client-supplied data — no hardcoding.
/// </summary>
public sealed class KeyboardLayout
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Font id this layout is designed to type in (Unicode or Legacy).</summary>
    [JsonPropertyName("fontId")]
    public string? FontId { get; set; }

    [JsonPropertyName("keys")]
    public List<KeyMapping> Keys { get; set; } = new();

    /// <summary>Optional dead-key combination rules (see <see cref="DeadKeyCombination"/>).</summary>
    [JsonPropertyName("deadKeyCombinations")]
    public List<DeadKeyCombination> DeadKeyCombinations { get; set; } = new();

    private Dictionary<int, KeyMapping>? _index;
    private Dictionary<(string, string), string>? _deadIndex;

    /// <summary>Fast lookup of a key mapping by virtual-key code.</summary>
    public KeyMapping? Find(int virtualKey)
    {
        _index ??= Keys
            .GroupBy(k => k.VirtualKey)
            .ToDictionary(g => g.Key, g => g.First());
        return _index.TryGetValue(virtualKey, out var m) ? m : null;
    }

    /// <summary>
    /// Resolve a dead-key + next-character pair to its combined output, or null
    /// if the layout defines no such combination.
    /// </summary>
    public string? CombineDeadKey(string dead, string next)
    {
        _deadIndex ??= DeadKeyCombinations
            .GroupBy(c => (c.Dead, c.Next))
            .ToDictionary(g => g.Key, g => g.First().Result);
        return _deadIndex.TryGetValue((dead, next), out var r) ? r : null;
    }

    /// <summary>Invalidate the cached lookups after mutating the collections.</summary>
    public void Reindex()
    {
        _index = null;
        _deadIndex = null;
    }
}
