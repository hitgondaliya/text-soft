using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

public class ConverterTests
{
    private static AssetBundle Bundle()
        => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();

    [Theory]
    [InlineData("dey", "कमल")]
    [InlineData("jke", "राम")]
    [InlineData("fnu", "दिन")]       // i-matra reorder
    [InlineData("ueLrs", "नमस्ते")]   // half-form conjunct स्त
    [InlineData("fganh", "हिंदी")]
    public void KrutiDevToUnicode(string kruti, string expected)
        => Assert.Equal(expected, Bundle().CreateConverter("krutidev", "mangal")!.ConvertText(kruti));

    [Theory]
    [InlineData("भारत")]
    [InlineData("कौन")]
    [InlineData("विद्यालय")]
    [InlineData("नमस्ते")]
    [InlineData("हिंदी")]
    [InlineData("भगवान")]
    public void RoundTripUnicodeKrutiUnicode(string word)
    {
        var b = Bundle();
        var toKruti = b.CreateConverter("mangal", "krutidev")!;
        var toUnicode = b.CreateConverter("krutidev", "mangal")!;
        Assert.Equal(word, toUnicode.ConvertText(toKruti.ConvertText(word)));
    }

    // Shree-Lipi legacy fonts convert via their shared ENCODING, not per-font id:
    // one table (shree-devx-to-unicode) serves every Shree-DevX weight/italic.
    //
    // These codes are the X-series' own: it is a PUA layout, not an ASCII one, and
    // the table is captured from Shree-Lipi's Converter nxt. The earlier ASCII
    // expectations here came from the scheme-derived table, which never matched
    // what the font actually draws.
    [Theory]
    [InlineData("", "कमल")]                      // क म ल
    [InlineData("", "कमलयन")]        // + य न
    [InlineData("", "कि")]                             // i-matra stored before its consonant
    public void ShreeDevxToUnicode(string legacy, string expected)
        => Assert.Equal(expected, Bundle().CreateConverter("shree-devx", "mangal")!.ConvertText(legacy));

    [Theory]
    [InlineData("कमल")]
    [InlineData("कि")]      // i-matra: post-base (Unicode) -> pre-base (legacy) -> post-base
    [InlineData("कमलयन")]
    public void ShreeDevxRoundTrip(string word)
    {
        var b = Bundle();
        var toLegacy = b.CreateConverter("mangal", "shree-devx")!;
        var toUnicode = b.CreateConverter("shree-devx", "mangal")!;
        Assert.Equal(word, toUnicode.ConvertText(toLegacy.ConvertText(word)));
    }

    // Live round-trip across every wired Shree script: Unicode -> legacy -> Unicode
    // must be lossless AND must not leave any character unmapped (a bare Unicode mark
    // in "legacy" output means a missing byte, e.g. the halant/matra bugs we fixed).
    [Theory]
    [InlineData("shree-devx", "mangal",  "विद्यालय")]
    [InlineData("shree-devx", "mangal",  "कर्म")]
    [InlineData("shree-gujx", "shruti",  "નમસ્તે")]
    [InlineData("shree-banx", "vrinda",  "বিদ্যা")]
    [InlineData("shree-telx", "gautami", "విద్య")]
    [InlineData("shree-punx", "raavi",   "ਵਿਦਿਆ")]
    [InlineData("shree-kanx", "tunga",   "ವಿದ್ಯಾ")]
    [InlineData("shree-malx", "kartika", "വിദ്യ")]
    [InlineData("shree-orix", "kalinga", "ବିଦ୍ୟା")]
    [InlineData("shree-tamx", "latha",   "வித்தை")]
    // A plain ra after a vowel sign is a ra, not a reph. Fused write pairs used to
    // swallow it, and these are the everyday words that came back without it.
    [InlineData("shree-gujx", "shruti",  "શેરી")]
    [InlineData("shree-gujx", "shruti",  "મોર")]
    [InlineData("shree-gujx", "shruti",  "દોરી")]
    [InlineData("shree-gujx", "shruti",  "કર્મ")]
    [InlineData("shree-guj7", "shruti",  "શેરી")]
    [InlineData("shree-guj7", "shruti",  "ધીરજ")]
    [InlineData("shree-orix", "kalinga", "ଶିର")]
    [InlineData("shree-ori7", "kalinga", "ଶିର")]
    // Hindi writes its half letters as single glyphs and its i-matra in two widths.
    [InlineData("shree-dev456", "mangal", "दस्तावेज")]
    [InlineData("shree-dev456", "mangal", "कर्म")]
    [InlineData("shree-dev456", "mangal", "विद्यालय")]
    [InlineData("shree-tel7", "gautami", "విద్య")]
    [InlineData("shree-telx", "gautami", "ద్దాత")]
    public void ShreeRoundTripNoUnmapped(string enc, string uni, string word)
    {
        var b = Bundle();
        var toLegacy = b.CreateConverter(uni, enc)!;
        var toUnicode = b.CreateConverter(enc, uni)!;
        var legacy = toLegacy.Convert(word);
        Assert.False(legacy.HasUnmapped, $"legacy '{legacy.Text}' left unmapped: {string.Join("", legacy.UnmappedCharacters)}");
        Assert.Equal(word, toUnicode.ConvertText(legacy.Text));
    }

    // Gopika (Oasis Systems, 1993) is the one Gujarati table built from the font
    // itself rather than captured from a reference converter: its bytes follow the
    // ITR typewriter layout, and the reordering is done by rules instead of an
    // enumerated cluster list. These pin both the letters and the reordering.
    [Theory]
    [InlineData("કમલ", "fbj")]
    [InlineData("ગુજરાત", "dwsht;")]
    [InlineData("કિ", "rf")]              // i-matra is typed before its consonant
    [InlineData("કર્મ", "fbo")]           // reph moves to the end of the cluster
    [InlineData("બાળક", "ct¤f")]     // la is a high byte, not shift+B
    [InlineData("શ્રીફળ", "§eV¤")]
    [InlineData("પ્રેમ", "¡ub")]
    public void UnicodeToGopika(string unicode, string expected)
        => Assert.Equal(expected, Bundle().CreateConverter("shruti", "gopika-guj")!.ConvertText(unicode));

    [Theory]
    [InlineData("કમલ")]
    [InlineData("નમસ્તે")]
    [InlineData("ગુજરાત")]
    [InlineData("બાળક")]
    [InlineData("કર્મ")]
    [InlineData("ક્ષત્રિય")]
    [InlineData("જ્ઞાન")]
    [InlineData("વિદ્યાર્થી")]
    [InlineData("અમદાવાદ")]
    [InlineData("૧૨૩૪૫૬૭૮૯૦")]
    public void RoundTripUnicodeGopikaUnicode(string word)
    {
        var b = Bundle();
        var toGopika = b.CreateConverter("shruti", "gopika-guj")!;
        var toUnicode = b.CreateConverter("gopika-guj", "shruti")!;
        var legacy = toGopika.Convert(word);
        Assert.False(legacy.HasUnmapped,
            $"legacy '{legacy.Text}' left unmapped: {string.Join("", legacy.UnmappedCharacters)}");
        Assert.Equal(word, toUnicode.ConvertText(legacy.Text));
    }

    [Fact]
    public void EveryRegisteredLegacyFontHasBothDirections()
    {
        var b = Bundle();
        foreach (var lang in b.Languages)
        {
            var unicode = lang.Fonts.FirstOrDefault(f => f.Type == FontSoftware.Core.Models.FontType.Unicode);
            if (unicode is null) continue;
            foreach (var legacy in lang.Fonts.Where(f => f.Type == FontSoftware.Core.Models.FontType.Legacy))
            {
                // Old bundled 0700/1120 series use a different byte layout than the
                // Shree-*X scheme; their tables are a later pass.
                if (legacy.Id is "shree-dev-0700" or "shree-guj-1120") continue;
                Assert.True(b.FindMapping(legacy.ConversionKey, unicode.ConversionKey) is not null,
                    $"{lang.Name}: no legacy->unicode for {legacy.ConversionKey}");
                Assert.True(b.FindMapping(unicode.ConversionKey, legacy.ConversionKey) is not null,
                    $"{lang.Name}: no unicode->legacy for {legacy.ConversionKey}");
            }
        }
    }

    /// <summary>
    /// A legacy font has one glyph for ड़, so the table answers with the single
    /// character U+095D. Unicode excludes that one from composition, so a document
    /// that went out through the converter and came back used to change spelling
    /// even though it read the same.
    /// </summary>
    [Theory]
    [InlineData("shree-dev456", "विद्यालय में पढ़ाई")]
    [InlineData("shree-devx", "ख़बर क़लम")]
    [InlineData("shree-ban7", "বিদ্যালয় ভারত")]
    [InlineData("shree-banx", "বিদ্যালয় ভারত")]
    public void NuktaLettersKeepTheirNormalSpelling(string enc, string word)
    {
        var p = new ConversionPipeline(Bundle());
        var legacy = LayoutKey.Legacy(enc);
        string there = p.Convert(word, LayoutKey.Unicode, legacy).Text;
        Assert.Equal(word, p.Convert(there, legacy, LayoutKey.Unicode).Text);
    }
}
