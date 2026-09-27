using FontSoftware.Core.Config;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The "Eng" keyboard is Shree's romanised one: you type the sound and the letters
/// follow. It is not a fixed key map - no table of key combinations can cover
/// thirty-odd consonants against a dozen vowels, let alone the conjuncts - so it
/// has to be driven by the language's phonetic scheme, which is exactly the scheme
/// the vendor's own chart describes.
/// </summary>
public class EnglishKeyboardTests
{
    private static AssetBundle Bundle()
        => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();

    [Theory]
    [InlineData("gu", "vikaasa", "\u0AB5\u0ABF\u0A95\u0ABE\u0AB8")]        // વિકાસ
    [InlineData("gu", "kamala", "\u0A95\u0AAE\u0AB2")]                      // કમલ
    [InlineData("gu", "sheree", "\u0AB6\u0AC7\u0AB0\u0AC0")]                // શેરી
    [InlineData("hi", "kamala", "\u0915\u092E\u0932")]                      // कमल
    [InlineData("hi", "vidyaalaya", "\u0935\u093F\u0926\u094D\u092F\u093E\u0932\u092F")]
    [InlineData("bn", "bhaarata", "\u09AD\u09BE\u09B0\u09A4")]              // ভারত
    [InlineData("ta", "paarata", "\u0BAA\u0BBE\u0BB0\u0BA4")]
    public void TypingBySoundSpellsTheWord(string language, string roman, string expected)
    {
        var bundle = Bundle();
        var lang = bundle.Languages.First(l => l.Id == language);
        var translit = bundle.GetTransliterator(lang);
        Assert.NotNull(translit);
        Assert.Equal(expected, translit!.Convert(roman));
    }

    /// <summary>Every language that offers an Eng keyboard has the scheme to drive it.</summary>
    [Fact]
    public void EveryEnglishKeyboardHasItsScheme()
    {
        var bundle = Bundle();
        foreach (var lang in bundle.Languages)
        {
            var eng = lang.Layouts.FirstOrDefault(l => l.Name.Equals("Eng", StringComparison.OrdinalIgnoreCase));
            if (eng is null) continue;
            Assert.True(bundle.GetTransliterator(lang) is not null,
                $"{lang.Id} offers an Eng keyboard but has no phonetic scheme to drive it.");
        }
    }
}
