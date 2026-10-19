using System.Text.RegularExpressions;
using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Shree-Lipi 7.0 and Shree-Lipi 4.0/5.0/6.0 are two font families (SHREE-GUJ7-0750
/// and SHREE-GUJ-0750), and on five codes they draw different letters. Both font
/// layouts used to point at a 4/5/6 font, and the Font box listed every font of the
/// language whatever the layout - so each layout offered the other family's fonts.
/// </summary>
public class FontFamilyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "textsoft-fonts-" + Guid.NewGuid().ToString("N"));
    private readonly string _configRoot = Path.Combine(AppContext.BaseDirectory, "config");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private MainViewModel Open(string language)
    {
        var bundle = new ConfigLoader(_configRoot).Load();
        var vm = new MainViewModel(bundle, new AppSettings(), new SettingsStore(_dir), NullLog.Instance, new FontResolver(_configRoot));
        vm.SelectedLanguage = vm.Languages.First(l => l.Id == language);
        return vm;
    }

    private static readonly Regex Seven = new("^SHREE-[A-Z]{3}7-", RegexOptions.IgnoreCase);
    private static readonly Regex FourFiveSix = new("^SHREE-[A-Z]{3}-", RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("gu-fl-shree-lipi-7-0", "SHREE-GUJ7-0750", true)]
    [InlineData("gu-fl-shree-lipi-4-0-5-0-6-0", "SHREE-GUJ-0750", false)]
    public void EachShreeLayoutListsItsOwnFamilyOnly(string layoutId, string defaultFamily, bool seven)
    {
        using var vm = Open("gu");
        vm.SelectedFontLayout = vm.FontLayouts.First(l => l.Id == layoutId);

        Assert.Equal(defaultFamily, vm.SelectedFont?.Family);
        Assert.NotEmpty(vm.Fonts);
        Assert.All(vm.Fonts, f => Assert.Matches(seven ? Seven : FourFiveSix, f.Family));
    }

    [Fact]
    public void TheUnicodeLayoutListsUnicodeFontsOnly()
    {
        using var vm = Open("gu");
        vm.SelectedFontLayout = vm.FontLayouts.First(l => l.IsUnicode);
        Assert.All(vm.Fonts, f => Assert.Equal(FontType.Unicode, f.Type));
        Assert.Contains(vm.Fonts, f => f.Id == "unicode");
    }

    [Fact]
    public void ShreeLipiExListsItsOwnFaces()
    {
        using var vm = Open("gu");
        vm.SelectedFontLayout = vm.FontLayouts.First(l => l.Id == "gu-fl-shreelipi-ex");
        Assert.True(vm.Fonts.Count > 1, "every Ex face, not only the layout's default");
        Assert.All(vm.Fonts, f => Assert.Equal("shree-gujx", f.ConversionKey));
    }

    /// <summary>Switching between the two Shree layouts never leaves the other family's font selected.</summary>
    [Fact]
    public void SwitchingLayoutsSwitchesTheFamily()
    {
        using var vm = Open("gu");
        var seven = vm.FontLayouts.First(l => l.Id == "gu-fl-shree-lipi-7-0");
        var old = vm.FontLayouts.First(l => l.Id == "gu-fl-shree-lipi-4-0-5-0-6-0");
        vm.SelectedFontLayout = seven;
        vm.SelectedFontLayout = old;
        Assert.Matches(FourFiveSix, vm.SelectedFont!.Family);
        vm.SelectedFontLayout = seven;
        Assert.Matches(Seven, vm.SelectedFont!.Family);
    }

    /// <summary>In every language: a 7.0 row's own font is a 7.0 font, a 4.0/5.0/6.0 row's a 4/5/6 one.</summary>
    [Fact]
    public void EveryLanguagesShreeRowsPointAtTheirOwnFamily()
    {
        var bundle = new ConfigLoader(_configRoot).Load();
        foreach (var lang in bundle.Languages)
        {
            foreach (var row in lang.FontLayouts.Where(l => l.Id.EndsWith("shree-lipi-7-0") || l.Id.EndsWith("4-0-5-0-6-0")))
            {
                bool seven = row.Id.EndsWith("7-0");
                Assert.False(string.IsNullOrEmpty(row.Families), $"{lang.Id} {row.Id} says which fonts are its own");
                if (row.FontId is null) continue;   // no font of that family installed anywhere: shown as not provided
                var font = lang.Fonts.FirstOrDefault(f => f.Id == row.FontId);
                Assert.True(font is not null, $"{lang.Id} {row.Id}: {row.FontId} is not a font of the language");
                Assert.Matches(seven ? Seven : FourFiveSix, font!.Family);
                Assert.True(row.Includes(font), $"{lang.Id} {row.Id} lists its own font");
            }
        }
    }
}
