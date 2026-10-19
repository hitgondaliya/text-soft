using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FontSoftware.Tests;

/// <summary>
/// What the Shree truth fixtures (Data/shree-*-truth.json) say beyond their rows: the
/// "exceptions", each "the table writes &lt;hex&gt;: why" (a row the tables spell otherwise
/// than the composer - a word-final virama, a NUL the composer writes; a U+XXXX token is a character
/// the table passes through, P3b) or "not checked: why".
/// </summary>
internal static class ShreeFixture
{
    private static readonly Regex Writes = new(@"^the table writes ((?:[0-9A-F]{2}|U\+[0-9A-F]{4})(?: (?:[0-9A-F]{2}|U\+[0-9A-F]{4}))*): ", RegexOptions.Compiled);

    public static Dictionary<string, string> LoadExceptions(string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", file);
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        return doc.RootElement.TryGetProperty("exceptions", out var ex)
            ? ex.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>The bytes an exception says the table writes ("" when it leaves the character out and reports it).</summary>
    public static string TableWrites(string why)
    {
        if (LeavesOut(why)) return "";
        var m = Writes.Match(why);
        if (!m.Success) throw new InvalidOperationException("not a 'the table writes' exception: " + why);
        return m.Groups[1].Value;
    }

    /// <summary>True when the exception is about a virama that ends a word.</summary>
    public static bool IsWordFinal(string why) => why.Contains("a virama that ends a word", StringComparison.Ordinal);

    /// <summary>
    /// True when the table leaves the row out and reports it (the output policy: its composer
    /// spelling is a code Word draws as nothing, and the fonts have no other).
    /// </summary>
    public static bool LeavesOut(string why) => why.StartsWith("the table leaves it out and reports it: ", StringComparison.Ordinal);

    /// <summary>True when the exception is a decision of the output policy (P2-P4: "P4: ...", "P3b: ...").</summary>
    public static bool IsPolicy(string why) => why.Contains(": P4: ", StringComparison.Ordinal) || why.Contains(": P3: ", StringComparison.Ordinal) || why.Contains(": P3b: ", StringComparison.Ordinal);

    /// <summary>
    /// True when the table passes a character of the row through as itself and reports it (P3b: the fonts
    /// have no glyph for it that Word draws - its bytes hold a U+XXXX token).
    /// </summary>
    public static bool PassesThrough(string why) => !LeavesOut(why) && TableWrites(why).Contains("U+", StringComparison.Ordinal);

    /// <summary>True when the exception is one of the ru/ruu ligatures Shree's ru and ruu keys type (Gujarati).</summary>
    public static bool IsRuLigature(string why) => why.Contains(" ligature", StringComparison.Ordinal) && (why.Contains("the ru ", StringComparison.Ordinal) || why.Contains("the ruu ", StringComparison.Ordinal));
}
