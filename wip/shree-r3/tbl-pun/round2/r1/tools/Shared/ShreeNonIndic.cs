// Shared by tools/Guj7Truth and tools/ShreeDevTruth (linked into both projects).

using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>
/// Text that is not Indic - typographic punctuation, symbols, accented Latin letters - in
/// a Shree 7 or 4/5/6 font. These are 8-bit fonts: every code of the Windows-1252 range
/// is a glyph of the script (“ is 93, which draws ન in SHREE-GUJ7 and ઙ્ગ in SHREE-GUJ;
/// © is A9, the vowel sign ૃ). A character with no pair passes through the converter as
/// itself, so it would print that glyph. The policy, the same for every table:
/// <list type="number">
/// <item>Punctuation the fonts can show is written as the look-alike they draw: “ ” as
///   two single quotes (as the Devanagari composer writes them), … as three periods,
///   • as the font's own bullet B7, ‚ „ as commas. (– — ‘ ’ × ÷ already have the
///   composer's own codes where the script has them.)</item>
/// <item>Any other character whose code the fonts map is reported as unmapped and left
///   out - never written as the letter its code draws (a pair marked "unmapped").</item>
/// <item>Everything else passes through unmapped and is reported, as before: the fonts
///   have no glyph for it, so it is drawn in a fallback font, as itself.</item>
/// </list>
/// ASCII is left alone (it passes through every table by design; the fonts' Latin
/// letters are the script's letters, so English text is set in an English font).
/// </summary>
public static class NonIndic
{
    /// <summary>
    /// The code points beyond ASCII that the (3,1) cmap of the fonts of each family maps -
    /// read from every SHREE-GUJ7-*, Shree-Guj7-*, SHREE-GUJ-*, SHREE-DEV7-*, Shree-Dev7-*,
    /// SHREE-DEV-* and SHREE-SAN7-* font in C:\Windows\Fonts (2026-10-12; all fonts of a
    /// family map the same set): the Windows-1252 range as Windows maps it, the unassigned
    /// bytes 81 8D 8F 90 (9D) as their C1 code points, and U+2219 (a second bullet). The
    /// 4/5/6 Devanagari fonts add U+0120-012F (signs) and U+0200-021C (ligatures). The
    /// Sanskrit fonts lack the C1 codes and U+00AD.
    /// </summary>
    public static SortedSet<char> FontCodes(string family)
    {
        var set = new SortedSet<char>();
        for (char c = '\u00A0'; c <= '\u00FF'; c++) set.Add(c);
        foreach (char c in "\u0152\u0153\u0160\u0161\u0178\u0192\u02C6\u02DC\u2013\u2014\u2018\u2019\u201A\u201C\u201D\u201E\u2020\u2021\u2022\u2026\u2030\u2039\u203A\u2122\u2219")
            set.Add(c);
        switch (family)
        {
            case "guj7" or "guj456":
                foreach (char c in "\u0081\u008D\u008F\u0090") set.Add(c);
                break;
            case "dev7" or "dev456":
                foreach (char c in "\u0081\u008D\u008F\u0090\u009D") set.Add(c);
                if (family == "dev456")
                {
                    for (char c = '\u0120'; c <= '\u012F'; c++) set.Add(c);
                    for (char c = '\u0200'; c <= '\u021C'; c++) set.Add(c);
                }
                break;
            case "san7":
                set.Remove('\u00AD');
                break;
            default:
                throw new ArgumentException("unknown family " + family);
        }
        return set;
    }

    /// <summary>
    /// Codes whose glyph is blank or draws the character itself, in every font of these
    /// families: A0 and AD are empty, B7 and U+2219 a bullet ●. Passing them through is right.
    /// </summary>
    public static readonly HashSet<char> Harmless = new() { '\u00A0', '\u00AD', '\u00B7', '\u2219' };

    /// <summary>Typographic punctuation and the codes of the look-alike the fonts draw (hex); q1 q2 are the font's single quotes ‘ ’.</summary>
    public static (char C, string Hex, string Why)[] LookAlikes(string q1, string q2) => new[]
    {
        ('\u201C', $"{q1} {q1}", "“: two single quotes ‘‘"),
        ('\u201D', $"{q2} {q2}", "”: two single quotes ’’"),
        ('\u2026', "2E 2E 2E", "…: three periods"),
        ('\u2022', "B7", "•: the font's bullet ● (B7)"),
        ('\u201A', "2C", "‚: a comma"),
        ('\u201E', "2C 2C", "„: two commas"),
    };

    /// <summary>
    /// Completes a Unicode -> Shree table: a pair for every font code a non-Indic character
    /// would otherwise pass through onto - its look-alike, or nothing, reported as unmapped.
    /// Characters the table already has a pair for keep it. The pairs stay in the house order.
    /// </summary>
    public static void Complete(MappingTable table, string family, string q1, string q2) =>
        Complete(table, FontCodes(family), q1, q2);

    /// <summary>
    /// <see cref="Complete(MappingTable, string, string, string)"/> for a family whose code points
    /// beyond ASCII are given (read from its fonts' cmaps: tools/BanTruth for Bangla / Assamese).
    /// </summary>
    public static void Complete(MappingTable table, IEnumerable<char> fontCodes, string q1, string q2)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        var look = LookAlikes(q1, q2).ToDictionary(l => l.C, l => l.Hex);
        foreach (char c in fontCodes.Where(c => c > '\u007F' && !IsIndic(c)).Distinct())
        {
            if (have.Contains(c.ToString()) || Harmless.Contains(c)) continue;
            table.Pairs.Add(look.TryGetValue(c, out var hex)
                ? new MappingPair { From = c.ToString(), To = Common.FromHex(hex) }
                : new MappingPair { From = c.ToString(), To = "", Unmapped = true });
        }
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
    }

    static bool IsIndic(char c) => c >= '\u0900' && c <= '\u0DFF';

    /// <summary>
    /// Every BMP character outside ASCII and the Indic blocks (not the surrogates or the
    /// noncharacters, which the tables use as their own markers and are never text), alone
    /// and between two letters: no code the table writes for it may be one the fonts draw
    /// as an Indic glyph - one the reverse table reads as Indic text, or a font code the
    /// reverse table cannot read at all - and what the table leaves out is reported.
    /// </summary>
    public static string Check(FontConverter forward, FontConverter reverse, string family, string letter, out List<string> fails) =>
        Check(forward, reverse, FontCodes(family), letter, out fails);

    /// <summary>
    /// <see cref="Check(FontConverter, FontConverter, string, string, out List{string})"/> for a family whose
    /// code points are given; <paramref name="blank"/> are more codes whose glyph is blank in every font of
    /// the family (the zero-width filler FF of the Bangla 4/5/6 fonts, in the composer's dash 2D FF 2D).
    /// </summary>
    public static string Check(FontConverter forward, FontConverter reverse, ISet<char> codes, string letter, out List<string> fails,
        string blank = "")
    {
        fails = new List<string>();
        int n = 0, lookAlike = 0, reported = 0, passed = 0;
        string alone = forward.ConvertText(letter);
        for (int cp = 0x80; cp <= 0xFFFD; cp++)
        {
            char c = (char)cp;
            if (IsIndic(c) || char.IsSurrogate(c) || (cp >= 0xFDD0 && cp <= 0xFDEF)) continue;
            n++;
            var r = forward.Convert(c.ToString());
            foreach (char o in r.Text)
            {
                if (blank.Contains(o)) continue;
                string read = reverse.ConvertText(o.ToString());
                if (read.Any(IsIndic)) fails.Add($"U+{cp:X4} {c}: writes {Common.Hex(o.ToString())}, which reads {read}");
                else if (codes.Contains(o) && !Harmless.Contains(o) && reverse.Convert(o.ToString()).HasUnmapped)
                    fails.Add($"U+{cp:X4} {c}: writes {Common.Hex(o.ToString())}, a font code the reverse table cannot read");
            }
            bool left = r.Text.Length == 0 && cp is not (0x200C or 0x200D) && !(cp >= 0xFE00 && cp <= 0xFE0F);
            if (left && !r.UnmappedCharacters.Contains(c.ToString())) fails.Add($"U+{cp:X4} {c}: left out without being reported");
            if (left) reported++;
            else if (r.HasUnmapped) passed++;
            else if (r.Text != c.ToString()) lookAlike++;
            // Between two letters: the same codes, and nothing reaches into the letters.
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + r.Text + alone) fails.Add($"U+{cp:X4} {c}: between two letters {Common.Hex(mid)}, alone {Common.Hex(r.Text)}");
        }
        return $"non-Indic characters {n}: written as a look-alike or the font's own code {lookAlike}, left out and reported {reported}, passed through and reported {passed}; wrong {fails.Count}";
    }
}
