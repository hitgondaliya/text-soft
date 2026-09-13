using System.Windows;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Conversion;

namespace FontSoftware.App.Views;

/// <summary>Add / remove the converter's word corrections (Converter nxt's "Use Dictionary").</summary>
public partial class DictionaryWindow : Window
{
    private readonly ConverterViewModel _vm;

    public DictionaryWindow(ConverterViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Refresh();
        FromBox.Focus();
    }

    private void Refresh()
    {
        List.ItemsSource = null;
        List.ItemsSource = _vm.Dictionary.Entries.ToList();
        Count.Text = _vm.DictionaryCountText;
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        string from = FromBox.Text.Trim();
        string to = ToBox.Text.Trim();
        if (from.Length == 0) { FromBox.Focus(); return; }
        _vm.Dictionary.Set(from, to, _vm.SelectedLanguage?.Id);
        _vm.SaveDictionary();
        FromBox.Clear();
        ToBox.Clear();
        FromBox.Focus();
        Refresh();
    }

    private void RemoveClick(object sender, RoutedEventArgs e)
    {
        foreach (var entry in List.SelectedItems.OfType<DictionaryEntry>().ToList())
            _vm.Dictionary.Remove(entry);
        _vm.SaveDictionary();
        Refresh();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
