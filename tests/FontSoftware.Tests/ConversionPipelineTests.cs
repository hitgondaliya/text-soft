using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>Any-to-any conversion through the Unicode hub, digits and the dictionary.</summary>
public class ConversionPipelineTests
{
    private static AssetBundle Bundle()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "config");
        return new ConfigLoader(root).Load();
    }

    [Fact]
    public void LegacyToLegacyGoesThroughUnicode()
    {
        var p = new ConversionPipeline(Bundle());
        var gujx = LayoutKey.Legacy("shree-gujx");
        var guj7 = LayoutKey.Legacy("shree-guj7");
        Assert.True(p.CanConvert(gujx, guj7, out _));

        const string uni = "ગુજરાત નિશાંત";
        string x = p.Convert(uni, LayoutKey.Unicode, gujx).Text;
        string seven = p.Convert(x, gujx, guj7).Text;
        string back = p.Convert(seven, guj7, LayoutKey.Unicode).Text;
        Assert.Equal(uni, back);
        Assert.NotEqual(x, seven);
    }

    [Fact]
    public void NumeralsFollowTheChoice()
    {
        var p = new ConversionPipeline(Bundle());
        var lang = new ConversionOptions { Numerals = NumeralStyle.Language, LanguageId = "gu" };
        var eng = new ConversionOptions { Numerals = NumeralStyle.English, LanguageId = "gu" };
        var hin = new ConversionOptions { Numerals = NumeralStyle.Hindi, LanguageId = "gu" };
        Assert.Equal("કિંમત ૧૨૩", p.Convert("કિંમત 123", LayoutKey.Unicode, LayoutKey.Unicode, lang).Text);
        Assert.Equal("કિંમત 123", p.Convert("કિંમત ૧૨૩", LayoutKey.Unicode, LayoutKey.Unicode, eng).Text);
        Assert.Equal("કિંમત १२३", p.Convert("કિંમત ૧૨૩", LayoutKey.Unicode, LayoutKey.Unicode, hin).Text);
    }

    [Fact]
    public void DictionaryFixesWholeWordsOnly()
    {
        var d = new ConversionDictionary();
        d.Set("નિશાત", "નિશાંત", "gu");
        var p = new ConversionPipeline(Bundle());
        var opt = new ConversionOptions { Dictionary = d, LanguageId = "gu" };
        Assert.Equal("હું નિશાંત છું", p.Convert("હું નિશાત છું", LayoutKey.Unicode, LayoutKey.Unicode, opt).Text);
        // Inside a longer word it is left alone; another script's entry does not apply.
        Assert.Equal("નિશાતપુર", p.Convert("નિશાતપુર", LayoutKey.Unicode, LayoutKey.Unicode, opt).Text);
        Assert.Equal("હું નિશાત છું", p.Convert("હું નિશાત છું", LayoutKey.Unicode, LayoutKey.Unicode, new ConversionOptions { Dictionary = d, LanguageId = "hi" }).Text);
    }

    [Fact]
    public void DictionaryRoundTripsThroughJson()
    {
        string path = Path.Combine(Path.GetTempPath(), "txsf-dict-" + Guid.NewGuid().ToString("N") + ".json");
        var d = new ConversionDictionary();
        d.Set("a", "b");
        d.Set("ક", "ખ", "gu");
        d.Save(path);
        var back = ConversionDictionary.Load(path);
        Assert.Equal(2, back.Count);
        Assert.Equal("b", back.Apply("x a y", null).Replace("x ", "").Replace(" y", ""));
        File.Delete(path);
    }

    [Fact]
    public void UnknownFamilyIsReportedNotThrown()
    {
        var p = new ConversionPipeline(Bundle());
        Assert.False(p.CanConvert(LayoutKey.Legacy("fl:nope"), LayoutKey.Unicode, out var missing));
        Assert.Equal("fl:nope", missing);
    }
}
