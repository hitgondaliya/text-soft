using FontSoftware.Core.Config;
using Xunit;

namespace FontSoftware.Tests;

public class ConfigTests
{
    private static AssetBundle LoadSample()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "config");
        return new ConfigLoader(root).Load();
    }

    [Fact]
    public void LoadsSampleCatalog()
    {
        var bundle = LoadSample();
        Assert.NotEmpty(bundle.Languages);
        var hi = bundle.Catalog.FindLanguage("hi");
        Assert.NotNull(hi);
        Assert.Contains(hi!.Fonts, f => f.Id == "mangal");
        Assert.Contains(hi.Fonts, f => f.Id == "krutidev");
    }

    [Fact]
    public void LoadsLayoutAndDeadKeyCombination()
    {
        var bundle = LoadSample();
        var hi = bundle.Catalog.FindLanguage("hi")!;
        var layout = bundle.GetLayout(hi.Layouts[0]);

        // Real Devanagari InScript: K -> क, D -> virama (halant). Conjuncts compose
        // in the target app for Unicode, so this layout defines no dead-key table.
        Assert.Equal("क", layout.Find(75)?.Base); // 'K' -> क
        Assert.Equal("्", layout.Find(68)?.Base); // 'D' -> ्  (virama)
        Assert.Null(layout.CombineDeadKey("्", "क"));
    }

    [Fact]
    public void EndToEndConversionThroughBundle()
    {
        var bundle = LoadSample();
        var converter = bundle.CreateConverter("krutidev", "mangal");
        Assert.NotNull(converter);
        Assert.Equal("कि", converter!.ConvertText("fd"));
    }

    [Fact]
    public void MissingConfigThrowsClearError()
    {
        var loader = new ConfigLoader("/no/such/dir");
        var ex = Assert.Throws<ConfigException>(() => loader.Load());
        Assert.Contains("not found", ex.Message);
    }
}
