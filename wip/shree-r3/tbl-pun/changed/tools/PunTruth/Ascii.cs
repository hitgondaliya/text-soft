using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>
/// ASCII in a Shree-Lipi Gurmukhi font (policy P2/P3/P4), the Unicode spaces, and a few codes beyond ASCII. The same
/// policy as tools/BanTruth/Ascii.cs (Bangla / Assamese; it is not in tools/Shared/ShreeNonIndic.cs, whose ASCII
/// function knows the Gujarati and Devanagari families only), with the Gurmukhi faces' own glyphs.
/// </summary>
public static class Ascii
{
    /// <summary>
    /// An 8-bit font draws a glyph of its script at most ASCII codes: in SHREE-PUN7 '#' 23 is ×, '&amp;' 26 the danda,
    /// '\' 5C ਪ, '`' 60 ਮ, and every letter is a letter of the script ('C' 43 = ੳ, 'H' 48 = ਕ). So, for each printable
    /// ASCII character:
    /// <list type="number">
    /// <item>a digit, or a character every face of the family draws as itself (<paramref name="itself"/>): written as
    ///   its own code, unreported (an ASCII digit draws the script's digit there, as Shree's composer writes digits);</item>
    /// <item>a character the table's pre rules rewrite (the ASCII quotes, as the fonts' quotes ‘ ’ 22 27) or that already
    ///   has a pair keeps it;</item>
    /// <item>any other - the letters A-Z a-z too: left out and reported, a pair to nothing marked "unmapped". Its code
    ///   draws something else in these faces (P3), or - 'x', '{', and '&lt;' in the 4/5/6 faces - no face maps it and
    ///   RichEdit draws the .notdef box for it (P4, r3\word\final-invisible.txt); '&lt;' is also where Shree's composer
    ///   writes ₹, so the reverse tables read 3C as ₹. English in a Gurmukhi Shree run is never written as Gurmukhi
    ///   glyphs: it is set in an English font.</item>
    /// </list>
    /// Beyond ASCII (<see cref="Beyond"/>): the Unicode spaces are the fonts' space 20; the soft hyphen is left out and
    /// reported. (· and ∙ are the shared policy's, NonIndic.StandIns: B7, the fonts' large bullet, a stand-in, reported.)
    /// </summary>
    public static void Complete(MappingTable table, string itself, FontConverter stager)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        for (char c = '!'; c <= '~'; c++)
        {
            if (have.Contains(c.ToString())) continue;
            if (stager.ConvertText(c.ToString()) != c.ToString()) continue;
            table.Pairs.Add(char.IsAsciiDigit(c) || itself.Contains(c)
                ? new MappingPair { From = c.ToString(), To = c.ToString() }
                : new MappingPair { From = c.ToString(), To = "", Unmapped = true });
        }
        foreach (var (c, to, unmapped, _) in Beyond)
            if (!have.Contains(c.ToString()))
                table.Pairs.Add(new MappingPair { From = c.ToString(), To = to, Unmapped = unmapped });
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The whitespace beyond ASCII that no SHREE-PUN7 / SHREE-PUN face maps (they map U+00A0, a blank). The converter
    /// passes whitespace through unreported, so these would reach a legacy run as code points no face has, and a .txt
    /// file not at all. U+0085 is a C1 control (left out and reported with the others, NonIndic.LeaveOutControls); the
    /// line and paragraph separators U+2028 U+2029 are line breaks, which the tables leave as they are (as they do \n):
    /// the .txt writer (FileConversion.LegacyBytes) writes them as CR LF.
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
            ((char)0x00AD, "", true, "the soft hyphen: blank at AD in every face, but Word's PDF export draws the face's '-' for it: " +
                "left out, reported (P3/P4)"),
        }).ToArray();
    // (· and ∙ are the shared policy's: NonIndic.StandIns writes both as B7, the fonts' large bullet, a stand-in, reported.)

    /// <summary>
    /// The ASCII side of the policy, checked: every printable ASCII character, alone and between two letters, is written
    /// as itself (a digit, or one of <paramref name="itself"/>), as the look-alike the pre rules make of it (the quotes),
    /// or left out and reported (the letters among them) - never as another code. And each of <see cref="Beyond"/> as it
    /// says, and an input noncharacter as U+FFFD, reported.
    /// </summary>
    public static string Check(FontConverter forward, string itself, string letter, out List<string> fails)
    {
        fails = new List<string>();
        int own = 0, look = 0, left = 0, letters = 0;
        string alone = forward.ConvertText(letter);
        for (char c = '!'; c <= '~'; c++)
        {
            var r = forward.Convert(c.ToString());
            string s = c.ToString();
            bool reported = r.UnmappedCharacters.Contains(s);
            if (char.IsAsciiDigit(c) || itself.Contains(c))
            {
                if (r.Text == s && !reported) own++;
                else fails.Add($"'{c}': drawn as itself, want it written {Common.Hex(s)} unreported, got {Common.Hex(r.Text)}{(reported ? " reported" : "")}");
            }
            else if (r.Text.Length == 0)
            {
                if (reported) { left++; if (char.IsAsciiLetter(c)) letters++; }
                else fails.Add($"'{c}': left out without being reported");
            }
            else if (r.Text == s) fails.Add($"'{c}': passed through as {Common.Hex(s)}, which the fonts draw as something else (or RichEdit as a box)");
            else if (!reported && c is '"' or '\'') look++;
            else fails.Add($"'{c}': written {Common.Hex(r.Text)}{(reported ? " and reported" : "")}");
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
        // The tables' own marks are noncharacters (U+FDD0-U+FDDF): one in the input is never taken for a mark (the
        // spacer BE, the far addak ...). It is written as U+FFFD, reported.
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
        return $"ASCII: written as itself {own}, as a look-alike {look}, left out and reported {left} (letters {letters}); " +
               $"beyond ASCII with a decision {Beyond.Length} (Unicode spaces as the space {Spaces.Length}); " +
               $"input noncharacters U+FDD0-U+FDDF written U+FFFD and reported {marks}/16; wrong {fails.Count}";
    }
}
