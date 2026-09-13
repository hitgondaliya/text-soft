using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FontSoftware.Core.Conversion;

/// <summary>One user-taught correction: whenever the converted Unicode text contains
/// <see cref="From"/> as a whole word, write <see cref="To"/> instead.</summary>
public sealed class DictionaryEntry
{
    [JsonPropertyName("from")] public string From { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
    [JsonPropertyName("language")] public string? Language { get; set; }
}

/// <summary>
/// Converter nxt's "Use Dictionary": a list of word corrections the user builds up
/// for words a mapping table gets wrong (proper nouns, old spellings, a legacy
/// font's private ligature). Applied on the Unicode side of every conversion, so
/// one entry fixes the word for every source and target layout. Persisted as JSON
/// in the user's profile.
/// </summary>
public sealed class ConversionDictionary
{
    private readonly List<DictionaryEntry> _entries = new();
    private Regex? _matcher;
    private Dictionary<string, string>? _lookup;
    private string? _compiledFor;

    public IReadOnlyList<DictionaryEntry> Entries => _entries;
    public int Count => _entries.Count;

    public static ConversionDictionary Load(string path)
    {
        var d = new ConversionDictionary();
        try
        {
            if (File.Exists(path))
            {
                var list = JsonSerializer.Deserialize<List<DictionaryEntry>>(File.ReadAllText(path));
                if (list is not null)
                    foreach (var e in list)
                        if (!string.IsNullOrWhiteSpace(e.From)) d._entries.Add(e);
            }
        }
        catch
        {
            // a damaged file is an empty dictionary, never a crash
        }
        return d;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    /// <summary>Add or replace the correction for <paramref name="from"/>.</summary>
    public void Set(string from, string to, string? language = null)
    {
        from = from.Trim();
        to = to.Trim();
        if (from.Length == 0) return;
        var existing = _entries.FirstOrDefault(e => e.From == from && (e.Language ?? "") == (language ?? ""));
        if (existing is null) _entries.Add(new DictionaryEntry { From = from, To = to, Language = language });
        else existing.To = to;
        _matcher = null;
    }

    public bool Remove(DictionaryEntry entry)
    {
        bool ok = _entries.Remove(entry);
        _matcher = null;
        return ok;
    }

    public void Clear()
    {
        _entries.Clear();
        _matcher = null;
    }

    /// <summary>Apply every whole-word correction that applies to <paramref name="languageId"/> (or to all).</summary>
    public string Apply(string text, string? languageId)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(text)) return text;
        Compile(languageId);
        if (_matcher is null || _lookup is null) return text;
        return _matcher.Replace(text, m => _lookup.TryGetValue(m.Value, out var to) ? to : m.Value);
    }

    private void Compile(string? languageId)
    {
        string key = languageId ?? "";
        if (_matcher is not null && _compiledFor == key) return;
        var applicable = _entries
            .Where(e => string.IsNullOrEmpty(e.Language) || string.Equals(e.Language, languageId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.From.Length)
            .ToList();
        if (applicable.Count == 0) { _matcher = null; _lookup = null; _compiledFor = key; return; }
        _lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in applicable) _lookup.TryAdd(e.From, e.To);
        // Word boundaries by whitespace/punctuation, not \b — Indic combining marks
        // are not "word characters" to .NET's \b and would split a word.
        string alt = string.Join("|", applicable.Select(e => Regex.Escape(e.From)));
        _matcher = new Regex($@"(?<![\p{{L}}\p{{M}}\p{{N}}])(?:{alt})(?![\p{{L}}\p{{M}}\p{{N}}])", RegexOptions.Compiled);
        _compiledFor = key;
    }
}
