using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// FR-6: the app reopens exactly as the user left it. The selection is restored
/// in the constructor, but the window then rebinds the language box while it
/// comes up - and that second pass used to overwrite the restored keyboard with
/// the language's first one, so a typist who left the app on InScript found it
/// on Eng the next morning.
/// </summary>
public class SelectionMemoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "textsoft-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _configRoot = Path.Combine(AppContext.BaseDirectory, "config");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private MainViewModel Open(AppSettings settings)
    {
        var bundle = new ConfigLoader(_configRoot).Load();
        var store = new SettingsStore(_dir);
        return new MainViewModel(bundle, settings, store, NullLog.Instance, new FontResolver(_configRoot));
    }

    private static AppSettings Saved() => new()
    {
        // deliberately none of them the first entry of its list, or falling back
        // to the first would look like a successful restore
        DefaultLanguageId = "gu",
        DefaultFontLayoutId = "gu-fl-shreelipi-ex",
        DefaultLayoutId = "guj-itr",
    };

    [Fact]
    public void TheSavedSelectionIsWhatTheAppOpensOn()
    {
        using var vm = Open(Saved());

        Assert.Equal("gu", vm.SelectedLanguage?.Id);
        Assert.Equal("gu-fl-shreelipi-ex", vm.SelectedFontLayout?.Id);
        Assert.Equal("guj-itr", vm.SelectedLayout?.Id);
    }

    [Fact]
    public void RebindingTheLanguageBoxDoesNotLoseTheKeyboard()
    {
        using var vm = Open(Saved());
        var language = vm.SelectedLanguage;

        // what the window does as it comes up: clear the box, then put it back
        vm.SelectedLanguage = null;
        vm.SelectedLanguage = language;

        Assert.Equal("guj-itr", vm.SelectedLayout?.Id);
        Assert.Equal("gu-fl-shreelipi-ex", vm.SelectedFontLayout?.Id);
    }

    [Fact]
    public void AnotherLanguageStillGetsItsOwnDefaults()
    {
        using var vm = Open(Saved());

        var hindi = vm.Languages.First(l => l.Id == "hi");
        vm.SelectedLanguage = hindi;

        Assert.Equal(hindi.Layouts.First().Id, vm.SelectedLayout?.Id);
    }
}
