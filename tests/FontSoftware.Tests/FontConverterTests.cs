using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

public class FontConverterTests
{
    private static MappingTable Table() => new()
    {
        Id = "t",
        Pairs =
        {
            new MappingPair { From = "d", To = "क" },
            new MappingPair { From = "e", To = "म" },
            new MappingPair { From = "f", To = "ि" },
            new MappingPair { From = "kshi", To = "क्षि" } // multi-char, tests longest match
        },
        PostRules =
        {
            new RewriteRule { Pattern = "ि([\\u0915-\\u0939])", Replace = "$1ि" }
        }
    };

    [Fact]
    public void ReordersIMatraAfterConsonant()
    {
        var c = new FontConverter(Table());
        Assert.Equal("कि", c.ConvertText("fd")); // f(ि)+d(क) -> reordered to कि
    }

    [Fact]
    public void ConvertsSimplePairs()
    {
        var c = new FontConverter(Table());
        Assert.Equal("कम", c.ConvertText("de"));
    }

    [Fact]
    public void PrefersLongestMatch()
    {
        var c = new FontConverter(Table());
        // "kshi" must win over "k"/others and not be split.
        Assert.Equal("क्षि", c.ConvertText("kshi"));
    }

    [Fact]
    public void UnmappedCharactersArePassedThroughAndFlagged()
    {
        var c = new FontConverter(Table());
        var result = c.Convert("dZ");
        Assert.Equal("कZ", result.Text);
        Assert.True(result.HasUnmapped);
        Assert.Contains("Z", result.UnmappedCharacters);
    }

    // A pair marked unmapped: the target cannot show the character (its code draws a
    // letter there), so the converter writes the pair's text in its place - here
    // nothing - and still reports the character, as it would one with no pair at all.
    [Fact]
    public void APairMarkedUnmappedIsWrittenAndStillReported()
    {
        var t = Table();
        t.Pairs.Add(new MappingPair { From = "©", To = "", Unmapped = true });
        var result = new FontConverter(t).Convert("d©e");
        Assert.Equal("कम", result.Text);
        Assert.Equal(new[] { "©" }, result.UnmappedCharacters);
        Assert.False(new FontConverter(Table()).Convert("de").HasUnmapped);
    }

    [Fact]
    public void EmptyInputReturnsEmpty()
    {
        var c = new FontConverter(Table());
        Assert.Equal(string.Empty, c.ConvertText(""));
    }
}
