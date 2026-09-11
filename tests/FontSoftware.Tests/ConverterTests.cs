using FontSoftware.Core.Config;
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
    public void ShreeRoundTripNoUnmapped(string enc, string uni, string word)
    {
        var b = Bundle();
        var toLegacy = b.CreateConverter(uni, enc)!;
        var toUnicode = b.CreateConverter(enc, uni)!;
        var legacy = toLegacy.Convert(word);
        Assert.False(legacy.HasUnmapped, $"legacy '{legacy.Text}' left unmapped: {string.Join("", legacy.UnmappedCharacters)}");
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
}
