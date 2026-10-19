using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>ASCII in a Bangla / Assamese Shree font (policy P2/P3), the Unicode spaces, and a few codes beyond ASCII.</summary>
public static class Ascii
{
    /// <summary>
    /// ASCII (policy P2/P3; Bangla / Assamese only - kept out of tools/Shared/ShreeNonIndic.cs,
    /// whose Gujarati and Devanagari generators do not call it). An 8-bit font draws a glyph of
    /// its script at most ASCII codes: in SHREE-BAN '/' 2F is a piece of ব্ভ, '=' is থ,
    /// '[' the i-sign, '"' অ, and every letter is a letter of the script (A 41 = ক, a = জ্ব,
    /// T = ঙ্খ). So, for each ASCII character:
    /// <list type="number">
    /// <item>a digit, or a character every font of the family draws as itself
    ///   (<paramref name="itself"/>): written as its own code and not reported - a pair to
    ///   itself (an ASCII digit draws the script's digit there, as Shree's composer writes it);</item>
    /// <item>'&lt;': left out and reported. No face has a glyph at 3C (Word draws a fallback '&lt;',
    ///   RichEdit the face's default glyph ॥ in SHREE-BAN / SHREE-ASS, a box elsewhere), and 3C is
    ///   where Shree's composer writes ₹, so the reverse tables read 3C as ₹;</item>
    /// <item>one the fonts' cmaps do not hold (<paramref name="fontCodes"/>; % and : in SHREE-ASS7):
    ///   no pair - it passes through, reported, and Word draws it in another font as itself;</item>
    /// <item>any other (its code draws something else) - the letters A-Z a-z too: left out and
    ///   reported, a pair to nothing marked "unmapped". The fonts have no Latin letter to write
    ///   instead, so English in a Bangla Shree run is never written as Bengali glyphs.</item>
    /// </list>
    /// A character the table's pre rules rewrite (ASCII quotes as the fonts' quotes) or that
    /// already has a pair keeps it. Beyond ASCII (<see cref="Beyond"/>): the Unicode spaces are the
    /// fonts' space 20; · and ∙ are written as B7, the large bullet ● every face draws there, as a
    /// stand-in, reported (round 2, as the other scripts); the soft hyphen is left out and reported.
    /// </summary>
    public static void Complete(MappingTable table, ISet<char> fontCodes, string itself, FontConverter stager)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        for (char c = '!'; c <= '~'; c++)
        {
            if (have.Contains(c.ToString())) continue;
            if (stager.ConvertText(c.ToString()) != c.ToString()) continue;
            if (char.IsDigit(c) || itself.Contains(c)) table.Pairs.Add(new MappingPair { From = c.ToString(), To = c.ToString() });
            else if (fontCodes.Contains(c) || c == '<') table.Pairs.Add(new MappingPair { From = c.ToString(), To = "", Unmapped = true });
        }
        foreach (var (c, to, unmapped, _) in Beyond)
            if (!have.Contains(c.ToString()))
                table.Pairs.Add(new MappingPair { From = c.ToString(), To = to, Unmapped = unmapped });
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The whitespace beyond ASCII that no Bangla / Assamese Shree face maps (U+0085 and U+00A0 are font
    /// codes). The converter passes whitespace through unreported, so these would reach a legacy run as
    /// code points no face has, and a .txt file as '?'. The line and paragraph separators U+2028 U+2029
    /// are line breaks, not spaces: the tables leave them as they are (as they do \n), and the .txt
    /// writer (FileConversion.LegacyBytes) writes them as the CR LF a Windows-1252 file has.
    /// </summary>
    public static readonly string Spaces = new(new[] { 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008,
        0x2009, 0x200A, 0x202F, 0x205F, 0x3000 }.Select(c => (char)c).ToArray());

    /// <summary>U+FFFD: what an input noncharacter U+FDD0-U+FDDF becomes (Rules.NoMarksIn).</summary>
    static readonly string Replacement = ((char)0xFFFD).ToString();

    /// <summary>Characters beyond ASCII with a decision of their own: what is written, whether it is reported, and why.</summary>
    public static readonly (char C, string To, bool Unmapped, string Why)[] Beyond =
        Spaces.Select(c => (c, " ", false, "a Unicode space no face maps: the fonts' space 20 (its look-alike, P3)"))
        .Concat(new[]
        {
            ((char)0x00B7, "" + (char)0x00B7, true, "every face draws a large bullet ● at B7 (the bullet the tables write for •), not a middle dot: written B7 as a " +
                "stand-in, reported - as the Gujarati and Devanagari tables do (round 2; NonIndic.StandIns)"),
            ((char)0x2219, "" + (char)0x00B7, true, "every face draws the same bullet ● at U+2219 and B7 (same outline, advance 600): written B7, a stand-in, reported - " +
                "RichEdit moves U+2219 to Cambria Math, and a .txt file has no byte for it (P2/P4/P6)"),
            ((char)0x00AD, "", true, "the soft hyphen: blank at AD in SHREE-BAN* SHREE-ASS*, a visible hyphen in Shree-Ban7-00x / Shree-Ass7-00x, " +
                "and Word's PDF export draws a hyphen for it in the blank faces too: left out, reported (P3/P4)"),
        }).ToArray();

    /// <summary>
    /// The ASCII side of the policy, checked: every ASCII character, alone and between two letters,
    /// is written as itself (a digit, or one of <paramref name="itself"/>), as the look-alike the pre
    /// rules make of it, passed through and reported (not in the fonts), or left out and reported
    /// (the letters among them, and '&lt;') - never as another code. And each of <see cref="Beyond"/> as it says.
    /// </summary>
    public static string Check(FontConverter forward, ISet<char> fontCodes, string itself, string letter, out List<string> fails)
    {
        fails = new List<string>();
        int own = 0, look = 0, passed = 0, left = 0, letters = 0;
        string alone = forward.ConvertText(letter);
        for (char c = '!'; c <= '~'; c++)
        {
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
                if (reported) { left++; if (char.IsLetter(c)) letters++; }
                else fails.Add($"'{c}': left out without being reported");
            }
            else if (r.Text == s)
            {
                if (fontCodes.Contains(c) || c == '<') fails.Add($"'{c}': passed through as {Common.Hex(s)}, which the fonts draw as something else");
                else if (!reported) fails.Add($"'{c}': passed through without being reported");
                else passed++;
            }
            else if (!reported) look++;
            else fails.Add($"'{c}': written {Common.Hex(r.Text)} and reported");
            if (char.IsLetter(c) && r.Text.Length > 0) fails.Add($"'{c}': a Latin letter written {Common.Hex(r.Text)}, a Bengali glyph in these fonts");
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + r.Text + alone && !(c == '"' || c == '\''))
                fails.Add($"'{c}': between two letters {Common.Hex(mid)}, alone {Common.Hex(r.Text)}");
        }
        foreach (var (c, to, unmapped, _) in Beyond)
        {
            var r = forward.Convert(c.ToString());
            if (r.Text != to || r.UnmappedCharacters.Contains(c.ToString()) != unmapped)
                fails.Add($"U+{(int)c:X4}: want {(to.Length == 0 ? "nothing" : Common.Hex(to))}{(unmapped ? " reported" : "")}, got {Common.Hex(r.Text)}{(r.HasUnmapped ? " reported" : "")}");
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + to + alone) fails.Add($"U+{(int)c:X4}: between two letters {Common.Hex(mid)}");
        }
        // The tables' own marks are noncharacters (U+FDD0-U+FDDF): one in the input is never taken for a
        // mark (a reph, a ya-phala ...). It is written as U+FFFD, reported.
        int marks = 0;
        for (char c = (char)0xFDD0; c <= (char)0xFDDF; c++)
        {
            var r = forward.Convert(letter + c + letter);
            if (r.Text != alone + Replacement + alone || !r.UnmappedCharacters.Contains(Replacement))
                fails.Add($"U+{(int)c:X4} between two letters: {Common.Hex(r.Text)}, unmapped {string.Join(" ", r.UnmappedCharacters.Select(Common.Cps))}");
            else marks++;
        }
        string ls = ((char)0x2028).ToString(), ps = ((char)0x2029).ToString();
        var line = forward.Convert(letter + ls + letter + ps);
        if (line.Text != alone + ls + alone + ps || line.HasUnmapped) fails.Add($"U+2028 U+2029: {Common.Hex(line.Text)}");
        return $"ASCII: written as itself {own}, as a look-alike {look}, passed through and reported (not in the fonts) {passed}, " +
               $"left out and reported {left} (letters {letters}); beyond ASCII with a decision {Beyond.Length} (Unicode spaces as the space {Spaces.Length}); " +
               $"input noncharacters U+FDD0-U+FDDF written U+FFFD and reported {marks}/16; wrong {fails.Count}";
    }
}
