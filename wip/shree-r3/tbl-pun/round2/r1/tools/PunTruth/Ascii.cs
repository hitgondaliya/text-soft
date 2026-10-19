using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>ASCII that is not a letter in a Shree-Lipi Gurmukhi font (policy P2/P3; the same as tools/BanTruth/Ascii.cs, which is not shared).</summary>
public static class Ascii
{
    /// <summary>
    /// ASCII punctuation and digits (policy P2/P3; Bangla / Assamese only - kept out of
    /// tools/Shared/ShreeNonIndic.cs, whose Gujarati and Devanagari generators do not call it). An 8-bit font draws a glyph of
    /// its script at most ASCII codes too: in SHREE-BAN '/' 2F is a piece of \u09AC\u09CD\u09AD, '=' is \u09A5,
    /// '[' the i-sign, '"' \u0985. So, for each ASCII character that is not a letter:
    /// <list type="number">
    /// <item>a digit, or a character every font of the family draws as itself
    ///   (<paramref name="itself"/>): written as its own code and not reported - a pair to
    ///   itself (an ASCII digit draws the script's digit there, as Shree's composer writes it);</item>
    /// <item>one the fonts' cmaps do not hold (<paramref name="fontCodes"/>): no pair - it
    ///   passes through, reported, and is drawn in another font as itself;</item>
    /// <item>any other (its code draws something else): left out and reported - a pair to
    ///   nothing marked "unmapped".</item>
    /// </list>
    /// A character the table's pre rules rewrite (ASCII quotes as the fonts' quotes) or that
    /// already has a pair keeps it. ASCII letters are left alone: they are English text,
    /// which is set in an English font (and a converter reports them).
    /// </summary>
    public static void Complete(MappingTable table, ISet<char> fontCodes, string itself, FontConverter stager)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        for (char c = '!'; c <= '~'; c++)
        {
            if (char.IsLetter(c) || have.Contains(c.ToString())) continue;
            if (stager.ConvertText(c.ToString()) != c.ToString()) continue;
            if (char.IsDigit(c) || itself.Contains(c)) table.Pairs.Add(new MappingPair { From = c.ToString(), To = c.ToString() });
            else if (fontCodes.Contains(c)) table.Pairs.Add(new MappingPair { From = c.ToString(), To = "", Unmapped = true });
        }
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The ASCII side of the policy, checked: every ASCII character that is not a letter, alone
    /// and between two letters, is written as itself (a digit, or one of <paramref name="itself"/>),
    /// as the look-alike the pre rules make of it, passed through and reported (not in the
    /// fonts), or left out and reported - never as another code.
    /// </summary>
    public static string Check(FontConverter forward, ISet<char> fontCodes, string itself, string letter, out List<string> fails)
    {
        fails = new List<string>();
        int own = 0, look = 0, passed = 0, left = 0;
        string alone = forward.ConvertText(letter);
        for (char c = '!'; c <= '~'; c++)
        {
            if (char.IsLetter(c)) continue;
            var r = forward.Convert(c.ToString());
            string s = c.ToString();
            bool reported = r.UnmappedCharacters.Contains(s);
            if (char.IsDigit(c) || itself.Contains(c))
            {
                if (r.Text == s && !reported) own++;
                else fails.Add($"'{c}': drawn as itself, want it written {Common.Hex(s)} unreported, got {Common.Hex(r.Text)}{(reported ? " reported" : "")}");
            }
            else if (r.Text.Length == 0)
            {
                if (reported) left++;
                else fails.Add($"'{c}': left out without being reported");
            }
            else if (r.Text == s)
            {
                if (fontCodes.Contains(c)) fails.Add($"'{c}': passed through as {Common.Hex(s)}, which the fonts draw as something else");
                else if (!reported) fails.Add($"'{c}': passed through without being reported");
                else passed++;
            }
            else if (!reported) look++;
            else fails.Add($"'{c}': written {Common.Hex(r.Text)} and reported");
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + r.Text + alone && !(c == '"' || c == '\''))
                fails.Add($"'{c}': between two letters {Common.Hex(mid)}, alone {Common.Hex(r.Text)}");
        }
        return $"ASCII punctuation and digits: written as itself {own}, as a look-alike {look}, passed through and reported (not in the fonts) {passed}, left out and reported {left}; wrong {fails.Count}";
    }
}
