using System.IO;
using System.Windows;
using Microsoft.Win32;
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

    /// <summary>
    /// Pick a keyboard file made in Shree-Lipi's Keyboard Generator. It opens in
    /// the Shree-Lipi folder, because that is where Shree tells its users to copy
    /// the keyboards they make.
    /// </summary>
    private void ImportClick(object sender, RoutedEventArgs e)
    {
        if (Model is null) return;
        var dlg = new OpenFileDialog
        {
            Title = "Import a Shree-Lipi keyboard",
            Filter = "Shree-Lipi keyboards|*.guj;*.dev;*.san;*.pun;*.ban;*.ass;*.ori;*.tam;*.tel;*.kan;*.mal|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(@"C:\Shree-Lipi nxt") ? @"C:\Shree-Lipi nxt" : @"C:\",
        };
        if (dlg.ShowDialog() == true) Model.Import(dlg.FileName);
    }

    private void CharacterClicked(object sender, RoutedEventArgs e)
    {
        if (Model is null || sender is not Button { Tag: string character }) return;
        Model.Choose(character);
    }
}
