using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using FontSoftware.App.Infrastructure;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

namespace FontSoftware.App.ViewModels;

/// <summary>A layout choice in the converter: a Font Layout of the script plus what it converts as.</summary>
public sealed class LayoutOption
{
    public required string Name { get; init; }
    public required LayoutKey Key { get; init; }
    /// <summary>The font that renders text in this layout (bundled or installed).</summary>
    public FontAsset? Font { get; init; }
    /// <summary>False when no mapping table exists for this family yet — still listed, so the
    /// client sees what Shree offers, but conversion says why it cannot run.</summary>
    public bool Available { get; init; }
    public string Label => Available ? Name : $"{Name}  —  not available";
    public override string ToString() => Label;
}

/// <summary>A render font for the input/output boxes.</summary>
public sealed class FontChoice
{
    public required string Name { get; init; }
    public required FontFamily Family { get; init; }
    public override string ToString() => Name;
}

public sealed class ConvertFileItem : ObservableObject
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    private string _status = "";
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

/// <summary>
/// The converter, modelled on Shree-Lipi's Converter nxt: pick a script, an input
/// layout and an output layout; feed it typed/pasted text, the clipboard, or a
/// list of files; choose the digits, the dictionary and plain-vs-formatted output.
/// Text and clipboard conversion keep formatting (run by run); files come out as
/// the same kind of file with the target font applied.
/// </summary>
public sealed class ConverterViewModel : ObservableObject
{
    public static readonly double[] FontSizes = { 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

    private readonly AssetBundle _bundle;
    private readonly FontResolver _fonts;
    private readonly ConversionPipeline _pipeline;
    private readonly string _dictionaryPath;

    public ConversionDictionary Dictionary { get; }

    public ObservableCollection<LanguageProfile> Languages { get; } = new();
    public ObservableCollection<LayoutOption> InputLayouts { get; } = new();
    public ObservableCollection<LayoutOption> OutputLayouts { get; } = new();
    public ObservableCollection<FontChoice> InputFonts { get; } = new();
    public ObservableCollection<FontChoice> OutputFonts { get; } = new();
    public ObservableCollection<ConvertFileItem> Files { get; } = new();

    public ConverterViewModel(AssetBundle bundle, FontResolver fonts)
    {
        _bundle = bundle;
        _fonts = fonts;
        _pipeline = new ConversionPipeline(bundle);
        _dictionaryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextSoft", "conversion-dictionary.json");
        Dictionary = ConversionDictionary.Load(_dictionaryPath);

        foreach (var lang in bundle.Languages) Languages.Add(lang);

        ConvertCommand = new RelayCommand(() => ConvertRequested?.Invoke(), () => CanConvert && PremiumUnlocked && !IsBusy);
        GetClipboardCommand = new RelayCommand(() => ClipboardRequested?.Invoke(), () => PremiumUnlocked);
        CopyCommand = new RelayCommand(() => CopyRequested?.Invoke());
        StartOverCommand = new RelayCommand(StartOver);
        SwapCommand = new RelayCommand(Swap);
        RemoveAllFilesCommand = new RelayCommand(() => { Files.Clear(); RefreshCanConvert(); });
        ClearDictionaryCommand = new RelayCommand(() => { Dictionary.Clear(); SaveDictionary(); Status = "Dictionary cleared."; OnPropertyChanged(nameof(DictionaryCountText)); });

        OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TextSoft Converted");
        _selectedLanguage = Languages.FirstOrDefault();
        OnLanguageChanged();
    }

    // ---- the view does clipboard / rich text on the UI thread ----
    public event Action? ConvertRequested;
    public event Action? ClipboardRequested;
    public event Action? CopyRequested;

    // ---- selection ----

    private LanguageProfile? _selectedLanguage;
    public LanguageProfile? SelectedLanguage
    {
        get => _selectedLanguage;
        set { if (SetProperty(ref _selectedLanguage, value)) OnLanguageChanged(); }
    }

    private LayoutOption? _selectedInputLayout;
    public LayoutOption? SelectedInputLayout
    {
        get => _selectedInputLayout;
        set
        {
            if (!SetProperty(ref _selectedInputLayout, value)) return;
            RebuildFonts(InputFonts, value, f => SelectedInputFont = f);
            OnPropertyChanged(nameof(SourceIsLegacy));
            RefreshCanConvert();
        }
    }

    private LayoutOption? _selectedOutputLayout;
    public LayoutOption? SelectedOutputLayout
    {
        get => _selectedOutputLayout;
        set
        {
            if (!SetProperty(ref _selectedOutputLayout, value)) return;
            RebuildFonts(OutputFonts, value, f => SelectedOutputFont = f);
            OnPropertyChanged(nameof(TargetIsLegacy));
            RefreshCanConvert();
        }
    }

    private FontChoice? _selectedInputFont;
    public FontChoice? SelectedInputFont
    {
        get => _selectedInputFont;
        set { if (SetProperty(ref _selectedInputFont, value)) OnPropertyChanged(nameof(InputFontFamily)); }
    }

    private FontChoice? _selectedOutputFont;
    public FontChoice? SelectedOutputFont
    {
        get => _selectedOutputFont;
        set { if (SetProperty(ref _selectedOutputFont, value)) OnPropertyChanged(nameof(OutputFontFamily)); }
    }

    public FontFamily InputFontFamily => SelectedInputFont?.Family ?? new FontFamily("Nirmala UI, Segoe UI");
    public FontFamily OutputFontFamily => SelectedOutputFont?.Family ?? new FontFamily("Nirmala UI, Segoe UI");

    private double _inputFontSize = 20;
    public double InputFontSize { get => _inputFontSize; set => SetProperty(ref _inputFontSize, value); }

    private double _outputFontSize = 20;
    public double OutputFontSize { get => _outputFontSize; set => SetProperty(ref _outputFontSize, value); }

    public bool SourceIsLegacy => SelectedInputLayout is { Key.IsUnicode: false };
    public bool TargetIsLegacy => SelectedOutputLayout is { Key.IsUnicode: false };

    // ---- mode ----

    private bool _filesMode;
    /// <summary>Input Data Format: false = text / clipboard, true = files.</summary>
    public bool FilesMode
    {
        get => _filesMode;
        set
        {
            if (!SetProperty(ref _filesMode, value)) return;
            OnPropertyChanged(nameof(TextMode));
            RefreshCanConvert();
        }
    }
    public bool TextMode { get => !FilesMode; set => FilesMode = !value; }

    private string _outputFolder = "";
    public string OutputFolder { get => _outputFolder; set => SetProperty(ref _outputFolder, value); }

    private bool _includeSubdirectories;
    public bool IncludeSubdirectories { get => _includeSubdirectories; set => SetProperty(ref _includeSubdirectories, value); }

    // ---- advanced ----

    private NumeralStyle _numerals = NumeralStyle.Language;
    public NumeralStyle Numerals
    {
        get => _numerals;
        set
        {
            if (!SetProperty(ref _numerals, value)) return;
            OnPropertyChanged(nameof(NumeralsLanguage));
            OnPropertyChanged(nameof(NumeralsEnglish));
            OnPropertyChanged(nameof(NumeralsHindi));
        }
    }
    public bool NumeralsLanguage { get => Numerals == NumeralStyle.Language; set { if (value) Numerals = NumeralStyle.Language; } }
    public bool NumeralsEnglish { get => Numerals == NumeralStyle.English; set { if (value) Numerals = NumeralStyle.English; } }
    public bool NumeralsHindi { get => Numerals == NumeralStyle.Hindi; set { if (value) Numerals = NumeralStyle.Hindi; } }

    private bool _useDictionary = true;
    public bool UseDictionary { get => _useDictionary; set => SetProperty(ref _useDictionary, value); }

    private bool _plainText;
    public bool PlainText { get => _plainText; set => SetProperty(ref _plainText, value); }

    private bool _copyToClipboard = true;
    /// <summary>Output Data Format "Clipboard": put the result on the clipboard after converting.</summary>
    public bool CopyToClipboard { get => _copyToClipboard; set => SetProperty(ref _copyToClipboard, value); }

    public string DictionaryCountText => Dictionary.Count == 0 ? "No corrections yet" : $"{Dictionary.Count} correction{(Dictionary.Count == 1 ? "" : "s")}";

    // ---- state ----

    private string _status = "";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private bool _statusIsError;
    public bool StatusIsError { get => _statusIsError; set => SetProperty(ref _statusIsError, value); }

    private bool _canConvert;
    public bool CanConvert
    {
        get => _canConvert;
        private set { if (SetProperty(ref _canConvert, value)) ConvertCommand.RaiseCanExecuteChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) ConvertCommand.RaiseCanExecuteChanged(); }
    }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    private bool _premiumUnlocked;
    /// <summary>True while the licence is active; the converter is fully locked otherwise.</summary>
    public bool PremiumUnlocked
    {
        get => _premiumUnlocked;
        set
        {
            if (!SetProperty(ref _premiumUnlocked, value)) return;
            ConvertCommand.RaiseCanExecuteChanged();
            GetClipboardCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand ConvertCommand { get; }
    public RelayCommand GetClipboardCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand StartOverCommand { get; }
    public RelayCommand SwapCommand { get; }
    public RelayCommand RemoveAllFilesCommand { get; }
    public RelayCommand ClearDictionaryCommand { get; }

    // ---- conversion ----

    /// <summary>The text transform for the current selection; used for every run, line and file.</summary>
    public string ConvertText(string text)
    {
        if (SelectedInputLayout is null || SelectedOutputLayout is null) return text;
        var r = _pipeline.Convert(text, SelectedInputLayout.Key, SelectedOutputLayout.Key, Options());
        // ASCII punctuation and digits pass through every layout unchanged; only real
        // letters that found no mapping are worth reporting.
        foreach (var u in r.UnmappedCharacters)
            if (u.Length > 0 && u[0] > '') _unmapped.Add(u);
        return r.Text;
    }

    private readonly HashSet<string> _unmapped = new(StringComparer.Ordinal);

    /// <summary>Call before a batch of <see cref="ConvertText"/> calls; then <see cref="EndBatch"/>.</summary>
    public void BeginBatch() => _unmapped.Clear();

    public string EndBatch(string what)
    {
        StatusIsError = false;
        if (_unmapped.Count == 0) return $"{what} converted.";
        string sample = string.Join(" ", _unmapped.Take(8));
        return $"{what} converted. {_unmapped.Count} character{(_unmapped.Count == 1 ? "" : "s")} had no mapping and passed through: {sample}";
    }

    private ConversionOptions Options() => new()
    {
        Numerals = Numerals,
        LanguageId = SelectedLanguage?.Id,
        Dictionary = UseDictionary ? Dictionary : null
    };

    public string TargetFontName => SelectedOutputFont?.Name ?? "Nirmala UI";

    /// <summary>Files mode: convert every listed file into <see cref="OutputFolder"/>.</summary>
    public async Task ConvertFilesAsync()
    {
        if (Files.Count == 0 || SelectedInputLayout is null || SelectedOutputLayout is null) return;
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            Status = "Choose an output folder.";
            StatusIsError = true;
            return;
        }
        IsBusy = true;
        BeginBatch();
        int ok = 0, failed = 0;
        try
        {
            Directory.CreateDirectory(OutputFolder);
            // Which text encoding the files are in: 8-bit fonts → Windows-1252, else UTF-8.
            bool srcUni = SelectedInputLayout.Key.IsUnicode || !_pipeline.IsByteEncoding(SelectedInputLayout.Key.Encoding);
            bool tgtUni = SelectedOutputLayout.Key.IsUnicode || !_pipeline.IsByteEncoding(SelectedOutputLayout.Key.Encoding);
            var font = OutputFontFamily;
            string fontName = TargetFontName;
            for (int i = 0; i < Files.Count; i++)
            {
                var f = Files[i];
                f.Status = "Converting…";
                Progress = (double)i / Files.Count * 100;
                await Task.Yield();
                try
                {
                    string outPath = Path.Combine(OutputFolder, f.Name);
                    if (string.Equals(Path.GetFullPath(outPath), Path.GetFullPath(f.Path), StringComparison.OrdinalIgnoreCase))
                        outPath = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(f.Name) + "-converted" + Path.GetExtension(f.Name));
                    FileConversion.ConvertFile(f.Path, outPath, ConvertText, srcUni, tgtUni, font, fontName, PlainText);
                    f.Status = "Done";
                    ok++;
                }
                catch (Exception ex)
                {
                    f.Status = "Failed: " + ex.Message;
                    failed++;
                }
            }
            Progress = 100;
            string tail = EndBatch($"{ok} file{(ok == 1 ? "" : "s")}");
            Status = failed == 0 ? tail + $" Saved in {OutputFolder}." : $"{tail} {failed} failed — see the list.";
            StatusIsError = failed > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        int added = 0, skipped = 0;
        foreach (var p in paths)
        {
            if (!FileConversion.IsSupported(p)) { skipped++; continue; }
            if (Files.Any(f => string.Equals(f.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
            Files.Add(new ConvertFileItem { Path = p });
            added++;
        }
        Status = skipped > 0 ? $"Added {added}. Skipped {skipped} (only .txt, .rtf, .docx and .doc can be converted)." : $"Added {added} file{(added == 1 ? "" : "s")}.";
        StatusIsError = false;
        RefreshCanConvert();
    }

    public void AddFolder(string folder)
    {
        var opt = IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = FileConversion.Extensions.SelectMany(ext => Directory.EnumerateFiles(folder, "*" + ext, opt)).OrderBy(x => x).ToList();
        AddFiles(files);
    }

    public void RemoveFile(ConvertFileItem item)
    {
        Files.Remove(item);
        RefreshCanConvert();
    }

    public void SaveDictionary()
    {
        try { Dictionary.Save(_dictionaryPath); } catch { /* best effort */ }
        OnPropertyChanged(nameof(DictionaryCountText));
    }

    // ---- internals ----

    private void OnLanguageChanged()
    {
        InputLayouts.Clear();
        OutputLayouts.Clear();
        OnPropertyChanged(nameof(SelectedLanguage));
        if (SelectedLanguage is null) return;

        var options = BuildLayoutOptions(SelectedLanguage);
        foreach (var o in options) { InputLayouts.Add(o); OutputLayouts.Add(o); }

        // Shree's default: legacy (ShreeLipi-Ex) in, Unicode out.
        SelectedInputLayout = options.FirstOrDefault(o => !o.Key.IsUnicode && o.Available) ?? options.FirstOrDefault();
        SelectedOutputLayout = options.FirstOrDefault(o => o.Key.IsUnicode) ?? options.Skip(1).FirstOrDefault();
    }

    private List<LayoutOption> BuildLayoutOptions(LanguageProfile lang)
    {
        var list = new List<LayoutOption>();
        var nirmala = new FontAsset { Id = "unicode", Name = "Unicode", Family = "Nirmala UI", Type = FontType.Unicode };

        foreach (var fl in lang.FontLayouts)
        {
            if (fl.IsUnicode)
            {
                // The layout's own Unicode font when it is on this PC, else Windows' Nirmala UI.
                var uf = lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId && _fonts.IsAvailable(f)) ?? nirmala;
                list.Add(new LayoutOption { Name = fl.Name, Key = LayoutKey.Unicode, Font = uf, Available = true });
                continue;
            }
            FontAsset? font = string.IsNullOrEmpty(fl.FontId) ? null : lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId)
                              ?? _bundle.Languages.SelectMany(l => l.Fonts).FirstOrDefault(f => f.Id == fl.FontId);
            string enc = font?.ConversionKey ?? fl.FontId ?? "";
            bool available = enc.Length > 0 && _pipeline.CanConvert(LayoutKey.Legacy(enc), LayoutKey.Unicode, out _)
                             && _pipeline.CanConvert(LayoutKey.Unicode, LayoutKey.Legacy(enc), out _);
            list.Add(new LayoutOption { Name = fl.Name, Key = LayoutKey.Legacy(enc.Length > 0 ? enc : "fl:" + fl.Id), Font = font, Available = available });
        }
        if (!list.Any(o => o.Key.IsUnicode))
            list.Insert(0, new LayoutOption { Name = "Unicode", Key = LayoutKey.Unicode, Font = nirmala, Available = true });
        return list;
    }

    private void RebuildFonts(ObservableCollection<FontChoice> target, LayoutOption? layout, Action<FontChoice> select)
    {
        target.Clear();
        FontChoice? first = null;
        if (layout?.Font is { } f && _fonts.IsAvailable(f))
        {
            first = new FontChoice { Name = f.Family, Family = _fonts.Resolve(f) };
            target.Add(first);
        }
        if (layout is { Key.IsUnicode: true })
        {
            foreach (var name in new[] { "Nirmala UI", "Mangal", "Shruti", "Vrinda", "Raavi", "Kalinga", "Latha", "Gautami", "Tunga", "Kartika" })
            {
                if (target.Any(t => t.Name == name) || !SystemFontService.IsInstalled(name)) continue;
                var c = new FontChoice { Name = name, Family = new FontFamily(name) };
                target.Add(c);
                first ??= c;
            }
        }
        foreach (var fam in SystemFontService.InstalledFamilies())
        {
            if (target.Any(t => string.Equals(t.Name, fam, StringComparison.OrdinalIgnoreCase))) continue;
            target.Add(new FontChoice { Name = fam, Family = new FontFamily(fam) });
        }
        select(first ?? target.FirstOrDefault() ?? new FontChoice { Name = "Nirmala UI", Family = new FontFamily("Nirmala UI") });
    }

    private void RefreshCanConvert()
    {
        var i = SelectedInputLayout;
        var o = SelectedOutputLayout;
        if (i is null || o is null) { CanConvert = false; return; }
        if (!i.Available) { CanConvert = false; SetStatus($"{i.Name}: no conversion table for this family yet.", true); return; }
        if (!o.Available) { CanConvert = false; SetStatus($"{o.Name}: no conversion table for this family yet.", true); return; }
        if (i.Key == o.Key) { CanConvert = false; SetStatus("Input and output layouts are the same.", true); return; }
        if (FilesMode && Files.Count == 0) { CanConvert = false; SetStatus("Add the files to convert.", false); return; }
        CanConvert = true;
        SetStatus($"{i.Name} → {o.Name}", false);
    }

    private void SetStatus(string s, bool error)
    {
        Status = s;
        StatusIsError = error;
    }

    private void StartOver()
    {
        Files.Clear();
        Progress = 0;
        StartOverRequested?.Invoke();
        RefreshCanConvert();
    }

    public event Action? StartOverRequested;

    private void Swap()
    {
        var (a, b) = (SelectedInputLayout, SelectedOutputLayout);
        SelectedInputLayout = b;
        SelectedOutputLayout = a;
        SwapRequested?.Invoke();
    }

    public event Action? SwapRequested;
}
