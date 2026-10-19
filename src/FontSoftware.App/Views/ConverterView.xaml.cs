using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using Microsoft.Win32;

namespace FontSoftware.App.Views;

/// <summary>
/// The converter page. The view model decides what to convert; this code-behind
/// owns the two RichTextBoxes (WPF has no binding for FlowDocument), the
/// clipboard, and the file dialogs.
/// </summary>
public partial class ConverterView : UserControl
{
    private ConverterViewModel? _vm;

    public ConverterView()
    {
        InitializeComponent();
        InputSizeBox.ItemsSource = ConverterViewModel.FontSizes;
        OutputSizeBox.ItemsSource = ConverterViewModel.FontSizes;
        DataContextChanged += (_, e) =>
        {
            if (_vm is not null)
            {
                _vm.ConvertRequested -= Convert;
                _vm.ClipboardRequested -= GetClipboard;
                _vm.CopyRequested -= CopyOutput;
                _vm.StartOverRequested -= StartOver;
                _vm.SwapRequested -= Swap;
            }
            _vm = e.NewValue as ConverterViewModel;
            if (_vm is null) return;
            _vm.ConvertRequested += Convert;
            _vm.ClipboardRequested += GetClipboard;
            _vm.CopyRequested += CopyOutput;
            _vm.StartOverRequested += StartOver;
            _vm.SwapRequested += Swap;
        };
    }

    private void Convert()
    {
        if (_vm is null) return;
        if (_vm.FilesMode)
        {
            _ = _vm.ConvertFilesAsync();
            return;
        }

        var input = InputBox.Document;
        if (string.IsNullOrWhiteSpace(RichTextConversion.PlainText(input)))
        {
            _vm.Status = "Type or paste something to convert, or press Get from clipboard.";
            _vm.StatusIsError = true;
            return;
        }

        _vm.BeginBatch(RichTextConversion.PlainText(input));
        FlowDocument output;
        try
        {
            output = RichTextConversion.Convert(input, _vm.ConvertText, _vm.OutputFontFamily, _vm.OutputFontSize, _vm.PlainText, _vm.Scope);
        }
        catch (Exception ex)
        {
            _vm.Status = "Conversion failed: " + ex.Message;
            _vm.StatusIsError = true;
            return;
        }

        OutputBox.Document = output;
        LegacyOutput.Text = RichTextConversion.PlainText(output);
        string status = _vm.EndBatch("Text");

        if (_vm.CopyToClipboard)
        {
            try
            {
                PutOnClipboard(output, _vm.PlainText);
                status += " Copied to clipboard.";
            }
            catch
            {
                status += " (Clipboard busy — press Copy to try again.)";
            }
        }
        _vm.Status = status;
        _vm.StatusIsError = false;
    }

    private void PutOnClipboard(FlowDocument doc, bool plainOnly)
    {
        string text = RichTextConversion.PlainText(doc);
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        if (!plainOnly)
            data.SetData(DataFormats.Rtf, RichTextConversion.ToRtf(doc));
        Clipboard.SetDataObject(data, true);
    }

    private void CopyOutput()
    {
        if (_vm is null) return;
        var doc = OutputBox.Document;
        if (string.IsNullOrWhiteSpace(RichTextConversion.PlainText(doc)))
        {
            _vm.Status = "Nothing to copy yet — convert first.";
            _vm.StatusIsError = true;
            return;
        }
        try
        {
            PutOnClipboard(doc, _vm.PlainText);
            _vm.Status = "Output copied to clipboard.";
            _vm.StatusIsError = false;
        }
        catch
        {
            _vm.Status = "Clipboard is busy. Try again.";
            _vm.StatusIsError = true;
        }
    }

    private void GetClipboard()
    {
        if (_vm is null) return;
        try
        {
            if (Clipboard.ContainsData(DataFormats.Rtf) && Clipboard.GetData(DataFormats.Rtf) is string rtf && rtf.Length > 0)
            {
                var doc = RichTextConversion.FromRtf(rtf);
                // Word names the font ("Shree-DevX-0001"); WPF can only find it through
                // our bundled-font URI, so every run is pointed at the input layout's font.
                RichTextConversion.Restamp(doc, _vm.InputFontFamily);
                InputBox.Document = doc;
                _vm.Status = "Formatted text taken from the clipboard.";
            }
            else if (Clipboard.ContainsText())
            {
                InputBox.Document = RichTextConversion.FromText(Clipboard.GetText(), _vm.InputFontFamily, _vm.InputFontSize);
                _vm.Status = "Text taken from the clipboard.";
            }
            else
            {
                _vm.Status = "The clipboard has no text.";
                _vm.StatusIsError = true;
                return;
            }
            _vm.StatusIsError = false;
        }
        catch
        {
            _vm.Status = "Could not read the clipboard. Try again.";
            _vm.StatusIsError = true;
        }
    }

    private void StartOver()
    {
        InputBox.Document = new FlowDocument();
        OutputBox.Document = new FlowDocument();
        LegacyOutput.Text = "";
        if (_vm is not null) { _vm.Status = ""; _vm.StatusIsError = false; }
    }

    private void Swap()
    {
        var outDoc = OutputBox.Document;
        if (string.IsNullOrWhiteSpace(RichTextConversion.PlainText(outDoc))) return;
        var doc = RichTextConversion.Clone(outDoc);
        RichTextConversion.Restamp(doc, _vm.InputFontFamily);
        InputBox.Document = doc;
        OutputBox.Document = new FlowDocument();
        LegacyOutput.Text = "";
    }

    // ---- files ----

    private void AddFilesClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Add files to convert",
            Filter = "Documents (*.txt;*.rtf;*.docx;*.doc)|*.txt;*.rtf;*.docx;*.doc|Text files (*.txt)|*.txt|Rich Text (*.rtf)|*.rtf|Word (*.docx;*.doc)|*.docx;*.doc|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) _vm?.AddFiles(dlg.FileNames);
    }

    private void AddFolderClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Add every .txt / .rtf / .docx / .doc in a folder" };
        if (dlg.ShowDialog() == true) _vm?.AddFolder(dlg.FolderName);
    }

    private void BrowseOutputClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Save converted files in", InitialDirectory = Directory.Exists(_vm?.OutputFolder) ? _vm!.OutputFolder : null };
        if (dlg.ShowDialog() == true && _vm is not null) _vm.OutputFolder = dlg.FolderName;
    }

    private void RemoveClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        foreach (var item in FileList.SelectedItems.OfType<ConvertFileItem>().ToList())
            _vm.RemoveFile(item);
    }

    private void FilesDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FilesDrop(object sender, DragEventArgs e)
    {
        if (_vm is null || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p)) _vm.AddFolder(p);
            else files.Add(p);
        }
        if (files.Count > 0) _vm.AddFiles(files);
    }

    private void EditDictionaryClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var w = new DictionaryWindow(_vm) { Owner = Window.GetWindow(this) };
        w.ShowDialog();
    }
}
