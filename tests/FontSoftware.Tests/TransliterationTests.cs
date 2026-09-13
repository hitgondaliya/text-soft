using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Phonetic typing follows Shree-Lipi's own Eng tables in every language, so a
/// customer who knows Shree spells words the same way here. The rules those tables
/// use: a consonant is written with its inherent vowel ("ka" is ક, not "k"), "ee"
/// is the long i, "M" the anusvara, and retroflex or long forms are capitals.
/// </summary>
public class TransliterationTests
{
    private static Transliterator Load(string file)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "config");
        return new Transliterator(new ConfigLoader(root).LoadScheme(file));
    }

    [Theory]
    [InlineData("namaste", "नमस्ते")]
    [InlineData("bhaarat", "भारत")]
    [InlineData("dhanyavaad", "धन्यवाद")]  // n+y must form न्य, not the ञ letter
    [InlineData("kshamaa", "क्षमा")]
    [InlineData("gujaraatee", "गुजराती")]
    [InlineData("hiMdee", "हिंदी")]        // M -> anusvara, after any vowel
    [InlineData("maraaThee", "मराठी")]      // capital T is the retroflex
    [InlineData("kya", "क्य")]
    [InlineData("Om", "ॐ")]
    public void DevanagariPhonetic(string roman, string expected)
        => Assert.Equal(expected, Load("translit/devanagari-shree-eng.json").Convert(roman));

    [Theory]
    [InlineData("gujaraatee", "ગુજરાતી")]
    [InlineData("namaste", "નમસ્તે")]
    [InlineData("helo", "હેલો")]
    [InlineData("shaaLaa", "શાળા")]
    [InlineData("cho", "છો")]              // Shree spells છ "cha", so છો is "cho"
    [InlineData("nishaaMta", "નિશાંત")]
    public void GujaratiPhonetic(string roman, string expected)
        => Assert.Equal(expected, Load("translit/gujarati-shree-eng.json").Convert(roman));

    // One word per remaining script: every language offers phonetic typing now,
    // not just the four that once had a hand-written scheme.
    [Theory]
    [InlineData("translit/punjabi-shree-eng.json", "kamala", "ਕਮਲ")]
    [InlineData("translit/bengali-shree-eng.json", "kamala", "কমল")]
    [InlineData("translit/telugu-shree-eng.json", "kamala", "కమల")]
    [InlineData("translit/kannada-shree-eng.json", "kamala", "ಕಮಲ")]
    [InlineData("translit/malayalam-shree-eng.json", "kamala", "കമല")]
    [InlineData("translit/odia-shree-eng.json", "kamaLa", "କମଳ")]
    [InlineData("translit/tamil-shree-eng.json", "kamala", "கமல")]
    public void EveryScriptTypesBySound(string scheme, string roman, string expected)
        => Assert.Equal(expected, Load(scheme).Convert(roman));

    [Fact]
    public void DeadKeyCombinationCombinesWhenDefined()
    {
        var layout = new KeyboardLayout
        {
            DeadKeyCombinations =
            {
                new DeadKeyCombination { Dead = "्", Next = "क", Result = "्क" }
            }
        };
        layout.Reindex();

        Assert.Equal("्क", layout.CombineDeadKey("्", "क"));
        Assert.Null(layout.CombineDeadKey("x", "y"));
    }
}
