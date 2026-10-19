using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Text that is not Indic in the Shree 7 and 4/5/6 tables. These are 8-bit fonts: every
/// code of the Windows-1252 range draws a glyph of the script (“ is 93 - ન in SHREE-GUJ7, the
/// conjunct ઙ્ગ in SHREE-GUJ - and © is A9, the vowel sign ૃ), so a character with no pair,
/// passed through as itself, printed a letter. One policy for every table:
/// punctuation the fonts can show is written as the look-alike they draw (“ ” as two single
/// quotes, … as three periods, • as the font's bullet B7, ‚ „ as commas); any other
/// character whose code the fonts map is reported as unmapped and left out; everything
/// else passes through and is reported (the fonts have no glyph for it, so it is drawn in
/// another font, as itself). ASCII passes through every table by design.
/// </summary>
public class ShreeNonIndicTests
{
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    public static readonly TheoryData<string, string> Tables = new()
    {
        { "shruti", "shree-guj7" }, { "shruti", "shree-guj456" },
        { "mangal", "shree-dev7" }, { "mangal", "shree-dev456" }, { "mangal", "shree-san7" },
        { "gautami", "shree-tel7" }, { "gautami", "shree-tel7w" },
    };

    /// <summary>
    /// The code points beyond ASCII the (3,1) cmap of the fonts of each family maps: the
    /// Windows-1252 range as Windows maps it (and U+2219, a second bullet); the unassigned
    /// bytes 81 8D 8F 90 (9D) as their C1 code points except in SAN7, which also lacks U+00AD;
    /// U+0120-012F and U+0200-021C in the 4/5/6 Devanagari fonts. ShreeFontCmapTests checks
    /// this against every installed font of the family.
    /// </summary>
    public static HashSet<char> FontCodes(string encoding)
    {
        var set = new HashSet<char>();
        for (char c = ' '; c <= 'ÿ'; c++) set.Add(c);
        foreach (char c in "ŒœŠšŸƒˆ˜–—‘’‚“”„†‡•…‰‹›™∙")
            set.Add(c);
        if (encoding is "shree-guj7" or "shree-guj456") set.UnionWith("\u0081\u008D\u008F\u0090");
        if (encoding is "shree-dev7" or "shree-dev456") set.UnionWith("\u0081\u008D\u008F\u0090\u009D");
        if (encoding == "shree-dev456")
        {
            for (char c = 'Ġ'; c <= 'į'; c++) set.Add(c);
            for (char c = 'Ȁ'; c <= 'Ȝ'; c++) set.Add(c);
        }
        if (encoding == "shree-san7") set.Remove('­');
        // The Telugu faces map byte 90 at U+0090 (a blank, the same glyph as A0).
        if (encoding is "shree-tel7" or "shree-tel7w") set.Add('\u0090');
        return set;
    }

    /// <summary>Codes that draw nothing (A0, AD) or a bullet ● (B7, U+2219) in every font of these families.</summary>
    private static readonly HashSet<char> Harmless = new() { ' ', '­', '·', '∙' };

    private static bool IsIndic(char c) => c >= 'ऀ' && c <= '෿';

    [Theory]
    [MemberData(nameof(Tables))]
    public void NoTableWritesALetterForTextThatIsNotIndic(string unicodeFont, string encoding)
    {
        var to = Assets.Value.CreateConverter(unicodeFont, encoding)!;
        var from = Assets.Value.CreateConverter(encoding, unicodeFont)!;
        var codes = FontCodes(encoding);
        string letter = encoding.StartsWith("shree-guj") ? "ક" : encoding.StartsWith("shree-tel") ? "క" : "क", alone = to.ConvertText(letter);
        var wrong = new List<string>();
        int n = 0, left = 0;
        for (int cp = 0x80; cp <= 0xFFFD; cp++)
        {
            char c = (char)cp;
            // The noncharacters U+FDD0-FDEF are the tables' own markers, never text.
            if (IsIndic(c) || char.IsSurrogate(c) || (cp >= 0xFDD0 && cp <= 0xFDEF)) continue;
            n++;
            var r = to.Convert(c.ToString());
            foreach (char o in r.Text)
            {
                string read = from.ConvertText(o.ToString());
                if (read.Any(IsIndic)) wrong.Add($"U+{cp:X4} {c}: wrote {Hex(o.ToString())}, which the fonts draw as {read}");
                else if (codes.Contains(o) && !Harmless.Contains(o) && from.Convert(o.ToString()).HasUnmapped)
                    wrong.Add($"U+{cp:X4} {c}: wrote {Hex(o.ToString())}, a glyph of the font nothing reads");
            }
            bool invisible = cp is 0x200C or 0x200D || (cp >= 0xFE00 && cp <= 0xFE0F);
            if (r.Text.Length == 0 && !invisible)
            {
                left++;
                if (!r.UnmappedCharacters.Contains(c.ToString())) wrong.Add($"U+{cp:X4} {c}: left out without being reported");
            }
            if (to.ConvertText(letter + c + letter) != alone + r.Text + alone)
                wrong.Add($"U+{cp:X4} {c}: between two letters {Hex(to.ConvertText(letter + c + letter))}");
        }
        Assert.True(wrong.Count == 0, $"{encoding}: {wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.True(left > 100, $"{encoding}: only {left} characters left out");
    }

    [Theory]
    [InlineData("shruti", "shree-guj7", "“ગુજરાત”", "22 22 4E 79 53 3E 66 70 73 27 27")]   // was 93 ... 94: ન ... સ્ત્ર
    [InlineData("shruti", "shree-guj456", "“ગુજરાત”", "22 22 4E 79 53 3E 66 70 73 27 27")] // was 93 ... 94: ઙ્ગ ... ઙ્ઘ
    [InlineData("shruti", "shree-guj7", "ગુજરાત…", "4E 79 53 3E 66 70 73 2E 2E 2E")]       // was 85: ર્ીં
    [InlineData("shruti", "shree-guj456", "• ‚„", "B7 20 2C 2C 2C")]                    // was 95 20 82 84: દ્ગ ગ્ન ં
    [InlineData("mangal", "shree-dev7", "“भारत”", "22 22 5E 6D 61 56 27 27")]
    [InlineData("mangal", "shree-dev456", "राम…", "61 6D 5F 2E 2E 2E")]                  // was 85: ः
    [InlineData("mangal", "shree-dev7", "• क", "B7 20 48 24")]
    [InlineData("mangal", "shree-san7", "“भारत”", "91 91 57 22 70 5A 4F 22 92 92")]
    [InlineData("mangal", "shree-san7", "राम…", "5A 70 58 22 2E 2E 2E")]
    [InlineData("gautami", "shree-tel7", "“తెలుగు”", "22 22 99 F0 FE CB 24 56 E6 FC 24 27 27")]
    [InlineData("gautami", "shree-tel7w", "తెలుగు… •", "99 F0 FE CB 24 56 E6 FC 24 2E 2E 2E 20 B7")]
    public void PunctuationIsWrittenAsTheLookAlikeTheFontsDraw(string unicodeFont, string encoding, string unicode, string bytes)
    {
        var r = Assets.Value.CreateConverter(unicodeFont, encoding)!.Convert(unicode);
        Assert.Equal(bytes, Hex(r.Text));
        Assert.False(r.HasUnmapped);
    }

    // © ® ° ½ ™ § have no look-alike the fonts draw: reported, and left out rather than
    // written as the letter their code draws (© A9 was ૃ in Gujarati, ° B0 the ra-sign).
    [Theory]
    [MemberData(nameof(Tables))]
    public void ACharacterTheFontsCannotShowIsReportedAndLeftOut(string unicodeFont, string encoding)
    {
        var to = Assets.Value.CreateConverter(unicodeFont, encoding)!;
        foreach (char c in "©®°½™§€ƒ")
        {
            var r = to.Convert(c.ToString());
            Assert.Contains(c.ToString(), r.UnmappedCharacters);
            // € is not in these fonts at all: it passes through, drawn in another font.
            Assert.Equal(c == '€' ? "€" : "", r.Text);
        }
        // Latin letters with marks beyond Windows-1252 pass through - except where the 4/5/6
        // Devanagari fonts draw their signs and ligatures (ī U+012B, Ș U+0218).
        foreach (char c in "īȘ")
        {
            var r = to.Convert(c.ToString());
            Assert.Contains(c.ToString(), r.UnmappedCharacters);
            Assert.Equal(encoding == "shree-dev456" ? "" : c.ToString(), r.Text);
        }
    }

    private static Encoding GetCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }
}
