namespace FontSoftware.Core.Conversion;

/// <summary>
/// ड़ can be written as one character (U+095D) or as ड plus the nukta dot
/// (U+0922 U+093C). The two are the same text - Unicode calls them canonically
/// equivalent - but it excludes the single character from composition, so the two
/// pieces are the normal spelling and what every other program will produce.
///
/// A legacy font has one glyph for the letter, so the tables answer with the single
/// character, and a document that went out through the converter and came back
/// changed spelling even though it read the same. Unicode text leaving the
/// converter is written the normal way.
/// </summary>
public static class Nukta
{
    private static readonly Dictionary<char, string> Spelling = new()
    {
        ['\u0929'] = "\u0928\u093C", ['\u0931'] = "\u0930\u093C", ['\u0934'] = "\u0933\u093C",
        ['\u0958'] = "\u0915\u093C", ['\u0959'] = "\u0916\u093C", ['\u095A'] = "\u0917\u093C",
        ['\u095B'] = "\u091C\u093C", ['\u095C'] = "\u0921\u093C", ['\u095D'] = "\u0922\u093C",
        ['\u095E'] = "\u092B\u093C", ['\u095F'] = "\u092F\u093C",
        ['\u09DC'] = "\u09A1\u09BC", ['\u09DD'] = "\u09A2\u09BC", ['\u09DF'] = "\u09AF\u09BC",
        ['\u0A33'] = "\u0A32\u0A3C", ['\u0A36'] = "\u0A38\u0A3C", ['\u0A59'] = "\u0A16\u0A3C",
        ['\u0A5A'] = "\u0A17\u0A3C", ['\u0A5B'] = "\u0A1C\u0A3C", ['\u0A5E'] = "\u0A2B\u0A3C",
        ['\u0B5C'] = "\u0B21\u0B3C", ['\u0B5D'] = "\u0B22\u0B3C",
    };

    /// <summary>Write every precomposed nukta letter as its letter and its dot.</summary>
    public static string Spell(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int at = -1;
        for (int i = 0; i < text.Length; i++) if (Spelling.ContainsKey(text[i])) { at = i; break; }
        if (at < 0) return text;

        var sb = new System.Text.StringBuilder(text.Length + 8);
        sb.Append(text, 0, at);
        for (int i = at; i < text.Length; i++)
        {
            if (Spelling.TryGetValue(text[i], out string? pieces)) sb.Append(pieces);
            else sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
