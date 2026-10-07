using System.Text.Json.Serialization;
using FontSoftware.Core.Keyboards;

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
/// A run of keys that together produce one piece of text, the way the vendor
/// layouts are actually documented: on the ITR Gujarati keyboard "y" gives અ and
/// "yt" gives આ, replacing it. Keys are written as the characters a US keyboard
/// produces (so "Y" means Shift+y), because that is the notation the vendor
/// charts use.
/// </summary>
public sealed class KeySequence
{
    [JsonPropertyName("keys")]
    public string Keys { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}

/// <summary>
/// The two combinations that reach past the printed keyboard, written inside a
/// run of keys as a mark of their own because that is how the vendor charts
/// write them: "Øj" is the escape and then j.
/// </summary>
public static class EscapeLayer
{
    /// <summary>
    /// Ctrl+Space - this keyboard's own third layer, and different on every
    /// keyboard: the nukta consonants, the conjuncts and the alternate letters
    /// that the two printed layers have no room for.
    /// </summary>
    public const char Mark = 'Ø';

    /// <summary>
    /// Ctrl+Shift+1 - the symbols every keyboard of a script shares: ॐ, the
    /// danda, the rupee sign, the accents, the dashes.
    /// </summary>
    public const char SymbolMark = '¤';
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

    /// <summary>
    /// Shree's romanised keyboard: you type the sound and the letters follow. It is
    /// not a fixed key map, and no table of key combinations can stand in for one -
    /// thirty-odd consonants against a dozen vowels, before the conjuncts - so the
    /// language's phonetic scheme drives it instead of <see cref="Keys"/>. The key
    /// map is still what the on-screen keyboard draws.
    /// </summary>
    [JsonPropertyName("phonetic")]
    public bool Phonetic { get; set; }

    [JsonPropertyName("keys")]
    public List<KeyMapping> Keys { get; set; } = new();

    /// <summary>Optional dead-key combination rules (see <see cref="DeadKeyCombination"/>).</summary>
    [JsonPropertyName("deadKeyCombinations")]
    public List<DeadKeyCombination> DeadKeyCombinations { get; set; } = new();

    /// <summary>
    /// Multi-key runs for this layout (see <see cref="KeySequence"/>). Most vendor
    /// keyboards need them: about two thirds of the entries in the published charts
    /// are two or more keystrokes.
    /// </summary>
    [JsonPropertyName("sequences")]
    public List<KeySequence> Sequences { get; set; } = new();

    /// <summary>
    /// What Ctrl+Space puts on a single key, for the keyboard viewer.
    /// <see cref="KeySequence.Keys"/> is the key pressed after it. The engine does
    /// not read this - every one of these is also in <see cref="Sequences"/>,
    /// written the way the charts write it ("Øj").
    /// </summary>
    [JsonPropertyName("escape")]
    public List<KeySequence> Escape { get; set; } = new();

    /// <summary>What Ctrl+Shift+1 puts on a single key. Same arrangement as <see cref="Escape"/>.</summary>
    [JsonPropertyName("symbols")]
    public List<KeySequence> Symbols { get; set; } = new();

    private Dictionary<int, KeyMapping>? _index;
    private Dictionary<(string, string), string>? _deadIndex;
    private Dictionary<string, string>? _seqIndex;
    private HashSet<string>? _seqPrefixes;
    private Dictionary<string, string>? _escIndex;
    private Dictionary<string, string>? _symIndex;
    private TypingOrder? _order;

    /// <summary>
    /// The order this keyboard is typed in - a sign struck before its letter, a
    /// reph struck after it - read off its own runs (see <see cref="TypingOrder"/>).
    /// </summary>
    [JsonIgnore]
    public TypingOrder Order => _order ??= TypingOrder.Learn(this);

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

    /// <summary>The text a run of keys produces, or null when the layout has no such run.</summary>
    public string? MatchSequence(string keys)
    {
        BuildSequenceIndex();
        return _seqIndex!.TryGetValue(keys, out var text) ? text : null;
    }

    /// <summary>
    /// True when more keys could still extend this run into a longer one, so the
    /// engine keeps composing instead of committing what is on screen.
    /// </summary>
    public bool SequenceCanGrow(string keys)
    {
        BuildSequenceIndex();
        return _seqPrefixes!.Contains(keys);
    }

    public bool HasSequences => Sequences.Count > 0;

    /// <summary>What Ctrl+Space (or, with <paramref name="symbols"/>, Ctrl+Shift+1) puts on one key.</summary>
    public string? EscapeFor(string key, bool symbols = false)
    {
        BuildEscapeIndex();
        var index = symbols ? _symIndex! : _escIndex!;
        return index.TryGetValue(key, out var text) ? text : null;
    }

    public bool HasEscape => Escape.Count > 0;

    public bool HasSymbols => Symbols.Count > 0;

    private void BuildSequenceIndex()
    {
        if (_seqIndex is not null) return;
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in Sequences)
        {
            if (string.IsNullOrEmpty(s.Keys) || string.IsNullOrEmpty(s.To)) continue;
            index[s.Keys] = s.To;
            for (int i = 1; i < s.Keys.Length; i++) prefixes.Add(s.Keys[..i]);
        }
        _seqIndex = index;
        _seqPrefixes = prefixes;
    }

    private void BuildEscapeIndex()
    {
        if (_escIndex is not null) return;
        _escIndex = Index(Escape);
        _symIndex = Index(Symbols);

        static Dictionary<string, string> Index(List<KeySequence> layer)
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in layer)
            {
                if (string.IsNullOrEmpty(e.Keys) || string.IsNullOrEmpty(e.To)) continue;
                index[e.Keys] = e.To;
            }
            return index;
        }
    }

    /// <summary>Invalidate the cached lookups after mutating the collections.</summary>
    public void Reindex()
    {
        _index = null;
        _deadIndex = null;
        _seqIndex = null;
        _seqPrefixes = null;
        _escIndex = null;
        _symIndex = null;
        _order = null;
    }
}
