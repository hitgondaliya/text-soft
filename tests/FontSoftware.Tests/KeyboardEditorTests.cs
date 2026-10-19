using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The keyboard designer, as the vendor's own Keyboard Generator works it: click
/// a key, click a character, and it is assigned; Form Conjunct builds a cluster
/// of up to four parts; a saved keyboard joins the keyboard list.
/// </summary>
public class KeyboardEditorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "textsoft-kbd-" + Guid.NewGuid().ToString("N"));
    private readonly AssetBundle _bundle;
    private readonly UserKeyboards _store;
    private readonly KeyboardEditorViewModel _vm;

    public KeyboardEditorTests()
    {
        _bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        _store = new UserKeyboards(_dir);
        _vm = new KeyboardEditorViewModel(_bundle, _store, NullLog.Instance)
        {
            SelectedLanguage = _bundle.Languages.First(l => l.Id == "gu"),
        };
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private KeyCapViewModel Key(int virtualKey) =>
        _vm.KeyboardRows.SelectMany(r => r).First(k => k.VirtualKey == virtualKey);

    private KeyboardLayout Saved()
    {
        var reference = _vm.SelectedLanguage!.Layouts.First(l => l.Name == _vm.Name);
        return new ConfigLoader(_dir).LoadLayout(reference);   // the file path is absolute
    }

    [Fact]
    public void AKeyTakesTheCharacterThatIsClicked()
    {
        _vm.Name = "Test Board";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");

        _vm.SaveCommand.Execute(null);
        var layout = Saved();
        Assert.Equal("ક", layout.Find('K')?.Base);
    }

    [Fact]
    public void ShiftIsItsOwnLayer()
    {
        _vm.Name = "Test Board";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");
        _vm.Layer = KeyLayer.Shift;
        _vm.SelectedKey = Key('K');
        _vm.Choose("ખ");

        _vm.SaveCommand.Execute(null);
        var layout = Saved();
        Assert.Equal("ક", layout.Find('K')?.Base);
        Assert.Equal("ખ", layout.Find('K')?.Shift);
    }

    [Fact]
    public void FormConjunctBuildsOneCharacterOutOfSeveral()
    {
        _vm.Name = "Test Board";
        _vm.SelectedKey = Key('X');
        _vm.FormConjunctCommand.Execute(null);
        _vm.Choose("ક");
        _vm.LinkCommand.Execute(null);      // the virama that joins them
        _vm.Choose("ષ");
        Assert.Equal("ક્ષ", _vm.Conjunct);

        _vm.ApplyConjunctCommand.Execute(null);
        Assert.False(_vm.ConjunctMode);

        _vm.SaveCommand.Execute(null);
        Assert.Equal("ક્ષ", Saved().Find('X')?.Base);
    }

    [Fact]
    public void AConjunctStopsAtFourCharacters()
    {
        _vm.SelectedKey = Key('X');
        _vm.FormConjunctCommand.Execute(null);
        foreach (string part in new[] { "ક", "ષ", "ત", "ર", "બ", "મ" }) _vm.Choose(part);

        Assert.Equal(KeyboardEditorViewModel.MaxConjunctLength, _vm.Conjunct.Length);
        Assert.Contains("at most", _vm.Status);
    }

    [Fact]
    public void TheCtrlSpaceLayerIsSavedAsARunTheEngineCanType()
    {
        _vm.Name = "Test Board";
        _vm.Layer = KeyLayer.Escape;
        _vm.SelectedKey = Key('U');
        _vm.Choose("ૐ");

        _vm.SaveCommand.Execute(null);
        var layout = Saved();
        Assert.Equal("ૐ", layout.EscapeFor("u"));
        Assert.Equal("ૐ", layout.MatchSequence(EscapeLayer.Mark + "u"));
    }

    [Fact]
    public void BlankClearsAKey()
    {
        _vm.Name = "Test Board";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");
        _vm.BlankCommand.Execute(null);

        _vm.SaveCommand.Execute(null);
        Assert.Null(Saved().Find('K')?.Base);
    }

    [Fact]
    public void ASavedKeyboardJoinsTheLanguagesKeyboardList()
    {
        _vm.Name = "My Gujarati";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");

        LayoutRef? announced = null;
        _vm.Saved += (_, reference) => announced = reference;
        _vm.SaveCommand.Execute(null);

        Assert.NotNull(announced);
        Assert.Equal("My Gujarati", announced!.Name);
        Assert.Contains(_vm.SelectedLanguage!.Layouts, l => l.Id == announced.Id);
        Assert.True(File.Exists(announced.File));
    }

    [Fact]
    public void AnExistingKeyboardCanBeOpenedAndChanged()
    {
        _vm.SelectedSource = _vm.SourceLayouts.First(l => l.Id == "gu-inscript");
        _vm.OpenCommand.Execute(null);

        var original = _bundle.GetLayout(_vm.SelectedSource);
        Assert.Equal(original.Find('K')?.Base, Key('K').Display);

        _vm.Name = "InScript mine";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ૠ");
        _vm.SaveCommand.Execute(null);

        var layout = Saved();
        Assert.Equal("ૠ", layout.Find('K')?.Base);
        Assert.Equal(original.Find('J')?.Base, layout.Find('J')?.Base);   // the rest is untouched
    }

    /// <summary>
    /// "K now types nothing on the Shift layer", the designer says when Shift is
    /// cleared - and it must mean it: an unset Shift types the normal character.
    /// </summary>
    [Fact]
    public void ClearingTheShiftLayerMakesItTypeNothing()
    {
        _vm.Name = "Blank Shift";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");
        _vm.ShowShiftLayer = true;
        _vm.SelectedKey = Key('K');
        _vm.Choose("ખ");
        _vm.BlankCommand.Execute(null);
        _vm.SaveCommand.Execute(null);
        var k = Saved().Find('K');
        Assert.Equal("ક", k?.Base);
        Assert.Equal("", k?.Shift);
    }

    [Fact]
    public void ClearingBothLayersTakesTheKeyOffTheKeyboard()
    {
        _vm.Name = "Blank Both";
        _vm.SelectedKey = Key('K');
        _vm.Choose("ક");
        _vm.BlankCommand.Execute(null);
        _vm.SaveCommand.Execute(null);
        var k = Saved().Find('K');
        Assert.True(k is null || (k.Base is null && k.Shift is null));
    }
}
