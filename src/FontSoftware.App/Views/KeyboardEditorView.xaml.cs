using System.Windows;
using System.Windows.Controls;
using FontSoftware.App.ViewModels;

namespace FontSoftware.App.Views;

/// <summary>
/// Design a keyboard: click a key, then click the character it should type -
/// the same two steps the vendor's Keyboard Generator asks for.
/// </summary>
public partial class KeyboardEditorView : UserControl
{
    public KeyboardEditorView() => InitializeComponent();

    private KeyboardEditorViewModel? Model => DataContext as KeyboardEditorViewModel;

    private void KeyClicked(object sender, RoutedEventArgs e)
    {
        if (Model is null || sender is not FrameworkElement { Tag: KeyCapViewModel cap }) return;
        Model.SelectedKey = cap;
    }

    private void CharacterClicked(object sender, RoutedEventArgs e)
    {
        if (Model is null || sender is not Button { Tag: string character }) return;
        Model.Choose(character);
    }
}
