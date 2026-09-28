using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Config;

/// <summary>
/// In-memory view of all loaded assets. Central lookup used by the selection UI
/// (FR-1), keyboard viewer (FR-2), typing engine (FR-3) and converter (FR-4).
/// Layouts are cached lazily on first access.
/// </summary>
public sealed class AssetBundle
{
    private readonly ConfigLoader _loader;
    private readonly Dictionary<string, KeyboardLayout> _layoutCache = new();
    private readonly Dictionary<string, Transliterator> _schemeCache = new();
    private readonly Dictionary<string, MappingTable> _mappingsById;
    private readonly Dictionary<string, Keyboards.ShreeCodeTable?> _shreeTables = new(StringComparer.OrdinalIgnoreCase);

    public AssetCatalog Catalog { get; }
    public IReadOnlyList<MappingTable> MappingTables { get; }

    public AssetBundle(AssetCatalog catalog, IReadOnlyList<MappingTable> mappings, ConfigLoader loader)
    {
        Catalog = catalog;
        MappingTables = mappings;
        _loader = loader;
        _mappingsById = mappings.ToDictionary(m => m.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<LanguageProfile> Languages => Catalog.Languages;

    /// <summary>Fonts valid for a language (FR-1: only valid fonts are shown).</summary>
    public IReadOnlyList<FontAsset> FontsFor(string languageId) =>
        Catalog.FindLanguage(languageId)?.Fonts ?? (IReadOnlyList<FontAsset>)Array.Empty<FontAsset>();

    public IReadOnlyList<LayoutRef> LayoutsFor(string languageId) =>
        Catalog.FindLanguage(languageId)?.Layouts ?? (IReadOnlyList<LayoutRef>)Array.Empty<LayoutRef>();

    /// <summary>
    /// Get (and cache) the phonetic "type by sound" transliterator for a language,
    /// or null if the language defines no scheme.
    /// </summary>
    public Transliterator? GetTransliterator(LanguageProfile language)
    {
        if (string.IsNullOrEmpty(language.SchemeFile))
            return null;
        if (_schemeCache.TryGetValue(language.SchemeFile, out var cached))
            return cached;
        var scheme = _loader.LoadScheme(language.SchemeFile);
        var translit = new Transliterator(scheme);
        _schemeCache[language.SchemeFile] = translit;
        return translit;
    }

    /// <summary>The table that reads Shree-Lipi keyboard files of a script (GUJ, DEV, ...), or null.</summary>
    public Keyboards.ShreeCodeTable? GetShreeCodeTable(string script)
    {
        if (!_shreeTables.TryGetValue(script, out var table))
            _shreeTables[script] = table = _loader.LoadShreeCodeTable(script);
        return table;
    }

    /// <summary>Load (and cache) a full keyboard layout by its reference.</summary>
    public KeyboardLayout GetLayout(LayoutRef reference)
    {
        if (_layoutCache.TryGetValue(reference.Id, out var cached))
            return cached;
        var layout = _loader.LoadLayout(reference);
        _layoutCache[reference.Id] = layout;
        return layout;
    }

    /// <summary>
    /// Find the mapping table converting between two font ENCODINGS, if one exists
    /// (see <see cref="FontAsset.ConversionKey"/>). Fonts sharing an encoding share
    /// a table. Both directions are separate tables (FR-4 supports each explicitly).
    /// </summary>
    public MappingTable? FindMapping(string sourceEncoding, string targetEncoding) =>
        MappingTables.FirstOrDefault(m =>
            m.SourceFontId == sourceEncoding && m.TargetFontId == targetEncoding);

    public MappingTable? GetMappingById(string id) =>
        _mappingsById.TryGetValue(id, out var t) ? t : null;

    /// <summary>Build a converter for an encoding pair, or null if no table exists.</summary>
    public FontConverter? CreateConverter(string sourceEncoding, string targetEncoding)
    {
        var table = FindMapping(sourceEncoding, targetEncoding);
        return table is null ? null : new FontConverter(table);
    }

    /// <summary>
    /// Build a Unicode → legacy converter targeting <paramref name="targetEncoding"/>,
    /// regardless of which named Unicode font the table lists as its source. Used to
    /// type live into a legacy Font Layout (e.g. ShreeLipi-Ex) so text shows in that
    /// legacy font. Null when no Unicode→legacy table exists for that encoding.
    /// </summary>
    public FontConverter? CreateConverterToLegacy(string targetEncoding)
    {
        var table = MappingTables.FirstOrDefault(m =>
            m.TargetFontId == targetEncoding &&
            m.TargetType == FontType.Legacy &&
            m.SourceType == FontType.Unicode);
        return table is null ? null : new FontConverter(table);
    }
}
