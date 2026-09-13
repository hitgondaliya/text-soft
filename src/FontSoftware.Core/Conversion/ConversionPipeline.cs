using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Conversion;

/// <summary>What a conversion runs between: Unicode, or one legacy encoding.</summary>
public readonly record struct LayoutKey(string Encoding, bool IsUnicode)
{
    public static readonly LayoutKey Unicode = new("unicode", true);
    public static LayoutKey Legacy(string encoding) => new(encoding, false);
    public override string ToString() => IsUnicode ? "Unicode" : Encoding;
}

public sealed class ConversionOptions
{
    public NumeralStyle Numerals { get; init; } = NumeralStyle.Language;
    /// <summary>Language / script id, for <see cref="NumeralStyle.Language"/> and dictionary scoping.</summary>
    public string? LanguageId { get; init; }
    public ConversionDictionary? Dictionary { get; init; }
    public static readonly ConversionOptions Default = new();
}

/// <summary>
/// Any layout to any layout, the way Converter nxt does it. Every mapping table we
/// ship goes legacy↔Unicode, so Unicode is the hub: legacy→legacy is two hops, and
/// the digit style and the user's dictionary are applied in the middle, on Unicode
/// text, where both are script-agnostic.
/// </summary>
public sealed class ConversionPipeline
{
    private readonly AssetBundle _bundle;
    private readonly Dictionary<string, FontConverter?> _toUni = new();
    private readonly Dictionary<string, FontConverter?> _fromUni = new();

    public ConversionPipeline(AssetBundle bundle) => _bundle = bundle;

    /// <summary>True when a table exists for every hop of source→target.</summary>
    public bool CanConvert(LayoutKey source, LayoutKey target, out string? missing)
    {
        missing = null;
        if (!source.IsUnicode && ToUnicode(source.Encoding) is null) { missing = source.Encoding; return false; }
        if (!target.IsUnicode && FromUnicode(target.Encoding) is null) { missing = target.Encoding; return false; }
        return true;
    }

    public ConversionResult Convert(string text, LayoutKey source, LayoutKey target, ConversionOptions? options = null)
    {
        options ??= ConversionOptions.Default;
        if (string.IsNullOrEmpty(text)) return new ConversionResult { Text = string.Empty };

        var unmapped = new List<string>();
        string uni = text;

        // Hop 1: into Unicode.
        if (!source.IsUnicode)
        {
            var c = ToUnicode(source.Encoding) ?? throw new InvalidOperationException($"No table for {source.Encoding} → Unicode.");
            var r = c.Convert(text);
            uni = r.Text;
            unmapped.AddRange(r.UnmappedCharacters);
        }

        // Middle: script-agnostic fixes on Unicode text.
        if (options.Dictionary is { Count: > 0 })
            uni = options.Dictionary.Apply(uni, options.LanguageId);
        uni = Numerals.Apply(uni, options.Numerals, options.LanguageId);

        // Hop 2: out of Unicode.
        if (target.IsUnicode)
            return new ConversionResult { Text = uni, UnmappedCharacters = unmapped.Distinct().ToArray() };

        var to = FromUnicode(target.Encoding) ?? throw new InvalidOperationException($"No table for Unicode → {target.Encoding}.");
        var r2 = to.Convert(uni);
        unmapped.AddRange(r2.UnmappedCharacters);
        return new ConversionResult { Text = r2.Text, UnmappedCharacters = unmapped.Distinct().ToArray() };
    }

    private readonly Dictionary<string, bool> _byteEnc = new();

    /// <summary>
    /// True when a legacy encoding is an 8-bit font (Kruti Dev, Shree-Lipi 7.0, ISFOC…):
    /// its text lives in one byte per character and a .txt in it is Windows-1252.
    /// False for the private-use-area fonts (ShreeLipi-Ex), whose text is real
    /// Unicode code points and whose files are UTF-8.
    /// </summary>
    public bool IsByteEncoding(string encoding)
    {
        if (_byteEnc.TryGetValue(encoding, out var b)) return b;
        var table = _bundle.MappingTables.FirstOrDefault(m => m.TargetFontId == encoding && m.TargetType == FontType.Legacy);
        int total = 0, ok = 0;
        if (table is not null)
            foreach (var p in table.Pairs)
                foreach (char c in p.To) { total++; if (c <= 'ÿ' || Cp1252High.Contains(c)) ok++; }
        b = total == 0 || ok >= total * 0.9;
        _byteEnc[encoding] = b;
        return b;
    }

    // Characters Windows-1252 places at 0x80-0x9F.
    private static readonly HashSet<char> Cp1252High = new(
        "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ");

    private FontConverter? ToUnicode(string encoding)
    {
        if (!_toUni.TryGetValue(encoding, out var c))
        {
            var table = _bundle.MappingTables.FirstOrDefault(m =>
                m.SourceFontId == encoding && m.SourceType == FontType.Legacy && m.TargetType == FontType.Unicode);
            c = table is null ? null : new FontConverter(table);
            _toUni[encoding] = c;
        }
        return c;
    }

    private FontConverter? FromUnicode(string encoding)
    {
        if (!_fromUni.TryGetValue(encoding, out var c))
        {
            c = _bundle.CreateConverterToLegacy(encoding);
            _fromUni[encoding] = c;
        }
        return c;
    }
}
