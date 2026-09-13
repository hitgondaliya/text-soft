namespace FontSoftware.Core.Conversion;

/// <summary>Which digits the converted text should carry (Converter nxt's "Select Output Numerals").</summary>
public enum NumeralStyle
{
    /// <summary>Digits of the selected script (૦૧૨ for Gujarati, ০১২ for Bangla…).</summary>
    Language = 0,
    /// <summary>ASCII 0-9.</summary>
    English = 1,
    /// <summary>Devanagari ०१२ regardless of script.</summary>
    Hindi = 2
}

/// <summary>
/// Digit rewriting on the Unicode side of a conversion. Every Indic script has
/// its ten digits in one contiguous block, so a digit is "value + block start".
/// </summary>
public static class Numerals
{
    /// <summary>Zero code point of each script's digit block, by script id prefix.</summary>
    private static readonly Dictionary<string, char> ZeroByScript = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dev"] = '०', ["hi"] = '०', ["mr"] = '०', ["sa"] = '०', ["ne"] = '०',
        ["guj"] = '૦', ["gu"] = '૦',
        ["ban"] = '০', ["bn"] = '০', ["as"] = '০', ["mni"] = '০',
        ["pun"] = '੦', ["pa"] = '੦',
        ["ori"] = '୦', ["or"] = '୦',
        ["tam"] = '௦', ["ta"] = '௦',
        ["tel"] = '౦', ["te"] = '౦',
        ["kan"] = '೦', ["kn"] = '೦',
        ["mal"] = '൦', ["ml"] = '൦',
    };

    private static readonly char[] Zeros =
    {
        '०', '૦', '০', '੦', '୦', '௦', '౦', '೦', '൦'
    };

    /// <summary>The zero digit of a language / script id, or null when unknown (e.g. Diacritical).</summary>
    public static char? ZeroOf(string? languageId)
    {
        if (string.IsNullOrEmpty(languageId)) return null;
        if (ZeroByScript.TryGetValue(languageId, out var z)) return z;
        int dash = languageId.IndexOf('-');
        if (dash > 0 && ZeroByScript.TryGetValue(languageId[..dash], out z)) return z;
        return null;
    }

    /// <summary>Value 0-9 if <paramref name="c"/> is an ASCII or Indic digit, else -1.</summary>
    public static int DigitValue(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        foreach (char zero in Zeros)
            if (c >= zero && c <= zero + 9) return c - zero;
        return -1;
    }

    /// <summary>Rewrite every digit in <paramref name="text"/> to the requested style.</summary>
    public static string Apply(string text, NumeralStyle style, string? languageId)
    {
        if (string.IsNullOrEmpty(text)) return text;
        char zero = style switch
        {
            NumeralStyle.English => '0',
            NumeralStyle.Hindi => '०',
            _ => ZeroOf(languageId) ?? '0'
        };
        var chars = text.ToCharArray();
        bool changed = false;
        for (int i = 0; i < chars.Length; i++)
        {
            int v = DigitValue(chars[i]);
            if (v < 0) continue;
            char n = (char)(zero + v);
            if (n != chars[i]) { chars[i] = n; changed = true; }
        }
        return changed ? new string(chars) : text;
    }
}
