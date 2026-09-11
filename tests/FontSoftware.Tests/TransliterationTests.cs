using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

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
    [InlineData("kShamaa", "क्षमा")]
    [InlineData("gujaraatii", "गुजराती")]
    [InlineData("hiMdii", "हिंदी")]        // M -> anusvara
    [InlineData("kya", "क्य")]
    [InlineData("OM", "ॐ")]
    public void HindiPhonetic(string roman, string expected)
        => Assert.Equal(expected, Load("translit/hindi-phonetic.json").Convert(roman));

    [Theory]
    [InlineData("gujaraatii", "ગુજરાતી")]
    [InlineData("namaste", "નમસ્તે")]
    [InlineData("helo", "હેલો")]
    [InlineData("shaaLaa", "શાળા")]
    [InlineData("chho", "છો")]
    public void GujaratiPhonetic(string roman, string expected)
        => Assert.Equal(expected, Load("translit/gujarati-phonetic.json").Convert(roman));

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
