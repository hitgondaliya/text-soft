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
    /// <summary>
    /// Every encoding the layout covers. One for an ordinary layout; several for one
    /// that gathers font products under one name (Bhasha Bharati), where the input
    /// is recognised from the text itself and the output follows the chosen font.
    /// </summary>
    public IReadOnlyList<string> Encodings { get; init; } = Array.Empty<string>();
    /// <summary>The fonts of the language that belong to the layout.</summary>
    public IReadOnlyList<FontAsset> Members { get; init; } = Array.Empty<FontAsset>();
    public bool IsCombined => Encodings.Count > 1;
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
        var r = _pipeline.Convert(text, InputKey(text), OutputKey(), Options());
        // What the typist is told (EndBatch), in three kinds:
        // - every character a table itself says the target font cannot show, ASCII or not: left out, or
        //   written as a stand-in (a slash in a Bangla Shree font, a '[' in a Gujarati one - it draws an
        //   i-sign - is gone from the output);
        // - every character kept as itself that the target font has no glyph for (₹ in a Shree font: Word
        //   draws it in another font) or, read out of a legacy font, a code the table could not read. ASCII
        //   punctuation and digits that pass through every layout unchanged are not worth a word; a Latin
        //   letter is (into a legacy font it draws a letter of the script, out of one it is a code unread);
        // - in a .txt file, what Windows-1252 has no byte for (FileConversion.LegacyBytes leaves it out).
        foreach (var u in r.LeftOutCharacters)
            if (u.Length > 0) lock (_report) _leftOut.Add(u);
        foreach (var u in r.KeptCharacters)
            if (u.Length > 0 && (u[0] > (char)0x7F || char.IsAsciiLetter(u[0]))) lock (_report) _kept.Add(u);
        if (_txtOut)
            for (int i = 0; i < r.Text.Length; i++)
            {
                bool pair = char.IsHighSurrogate(r.Text[i]) && i + 1 < r.Text.Length && char.IsLowSurrogate(r.Text[i + 1]);
                if (pair || !FileConversion.TxtHolds(r.Text[i])) lock (_report) _txtDropped.Add(r.Text.Substring(i, pair ? 2 : 1));
                if (pair) i++;
            }
        return r.Text;
    }

    private readonly object _report = new();
    private readonly HashSet<string> _leftOut = new(StringComparer.Ordinal);
    private readonly HashSet<string> _kept = new(StringComparer.Ordinal);
    private readonly HashSet<string> _txtDropped = new(StringComparer.Ordinal);

    // True while the file being converted is written as a legacy .txt (Windows-1252 bytes).
    private bool _txtOut;

    /// <summary>Call before a batch of <see cref="ConvertText"/> calls; then <see cref="EndBatch"/>.</summary>
    public void BeginBatch()
    {
        lock (_report)
        {
            _leftOut.Clear();
            _kept.Clear();
            _txtDropped.Clear();
        }
        _txtOut = false;
        _detected = null;
        lock (_otherLayoutFonts) _otherLayoutFonts.Clear();
    }

    // Runs of a document set in a font of another legacy layout of the language (SHREE-GUJ-0750 when
    // converting from Shree-Lipi 7.0): left as they are, and named in the report - they used to be
    // left without a word. Font name -> that layout's name.
    private readonly SortedDictionary<string, string> _otherLayoutFonts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Start a batch whose whole text is known up front, so an input layout that
    /// covers several encodings is recognised from all of it at once rather than
    /// from its first run.
    /// </summary>
    public void BeginBatch(string wholeText)
    {
        BeginBatch();
        if (SelectedInputLayout is { IsCombined: true } o && Letters(wholeText) > 0)
            _detected = Detect(wholeText, o);
    }

    // ---- layouts that cover several encodings (Bhasha Bharati) ----

    // The encoding recognised for the current batch; null until the text says.
    private string? _detected;

    private LayoutKey InputKey(string text)
    {
        var o = SelectedInputLayout!;
        if (!o.IsCombined) return o.Key;
        // A run of a few letters cannot tell Bhasha Bharati-1 from -2; wait for a
        // longer one and keep that answer for the rest of the batch.
        if (_detected is null && Letters(text) >= 8) _detected = Detect(text, o);
        return LayoutKey.Legacy(_detected ?? FontEncoding(o, SelectedInputFont) ?? o.Key.Encoding);
    }

    private LayoutKey OutputKey()
    {
        var o = SelectedOutputLayout!;
        if (!o.IsCombined) return o.Key;
        return LayoutKey.Legacy(FontEncoding(o, SelectedOutputFont) ?? o.Key.Encoding);
    }

    /// <summary>The encoding of the layout font drawn in <paramref name="f"/>, if it is one of them.</summary>
    private static string? FontEncoding(LayoutOption o, FontChoice? f) =>
        f is null ? null : o.Members.FirstOrDefault(m => string.Equals(m.Family, f.Name, StringComparison.OrdinalIgnoreCase))?.ConversionKey;

    private static int Letters(string text) => text.Count(char.IsLetterOrDigit);

    /// <summary>
    /// Which of the layout's encodings the text was typed in: read it with each and
    /// keep the reading with the least wrong with it (see <see cref="Oddness"/>). On a
    /// tie the chosen input font decides.
    /// </summary>
    private string Detect(string text, LayoutOption o)
    {
        string fallback = FontEncoding(o, SelectedInputFont) ?? o.Key.Encoding;
        string best = fallback;
        double bestScore = double.MinValue;
        var ties = new List<string>();
        foreach (var enc in o.Encodings)
        {
            ConversionResult r;
            try { r = _pipeline.Convert(text, LayoutKey.Legacy(enc), LayoutKey.Unicode, ConversionOptions.Default); }
            catch { continue; }
            double score = -Oddness(r.Text) - 3 * r.UnmappedCharacters.Count(u => u.Length > 0 && u[0] > 0x7F);
            if (score > bestScore + 1e-9) { bestScore = score; best = enc; ties.Clear(); ties.Add(enc); }
            else if (Math.Abs(score - bestScore) < 1e-9) ties.Add(enc);
        }
        return ties.Count > 1 && ties.Contains(fallback) ? fallback : best;
    }

    /// <summary>
    /// How much is wrong with a reading. A wrong table does not produce fewer letters -
    /// Bhasha Bharati-1 reads the -2 face's હ as the conjunct હ્ર, one letter MORE - so
    /// letters are never rewarded; only what text in the script does not do counts: a
    /// sign with nothing to sit on, a virama (every stray conjunct brings one), a letter
    /// the language almost never writes, and a Latin letter left inside a word (a byte
    /// the table does not know). Measured on Gujarati prose: the whole-text reading was
    /// right for every document, and no single sentence was read with the wrong table.
    /// </summary>
    internal static double Oddness(string s)
    {
        int bad = 0, virama = 0, rare = 0, latin = 0;
        int prevBlock = -1, pk = -1;
        char prev = ' ';
        foreach (char c in s)
        {
            int block = c & ~0x7F;
            bool script = block >= 0x0900 && block <= 0x0D00;
            bool asciiLetter = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            bool prevScript = (prev & ~0x7F) is >= 0x0900 and <= 0x0D00;
            bool prevAscii = prev is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            if ((asciiLetter && prevScript) || (script && prevAscii)) latin++;
            prev = c;
            if (!script) { prevBlock = -1; pk = -1; continue; }
            int k = c - block;
            bool matra = k >= 0x3E && k <= 0x4C && k != 0x4D;
            bool isVirama = k == 0x4D, nukta = k == 0x3C;
            bool mark = k >= 0x01 && k <= 0x03;                  // candrabindu, anusvara, visarga
            bool same = prevBlock == block;
            bool afterConsonant = same && ((pk >= 0x15 && pk <= 0x39) || (pk >= 0x58 && pk <= 0x5F) || pk == 0x3C);
            bool afterSyllable = afterConsonant || (same && ((pk >= 0x05 && pk <= 0x14) || (pk >= 0x3E && pk <= 0x4C && pk != 0x4D)));
            if ((matra || isVirama || nukta) && !afterConsonant) bad++;
            else if (mark && !afterSyllable) bad++;
            if (isVirama) virama++;
            // vocalic l/rr, the nasals ङ ञ, avagraha, the candra vowels: rare in running text
            if (k is 0x0C or 0x60 or 0x61 or 0x19 or 0x1E or 0x3D or 0x44 or 0x62 or 0x63 or 0x11 or 0x0D) rare++;
            prevBlock = block; pk = k;
        }
        return 4.0 * bad + 1.2 * virama + 3.0 * rare + 4.0 * latin;
    }

    public string EndBatch(string what)
    {
        StatusIsError = false;
        string others;
        lock (_otherLayoutFonts)
            others = _otherLayoutFonts.Count == 0 ? "" :
                " Left as it is: text in " + string.Join(", ", _otherLayoutFonts.Select(f => $"{f.Key} (a {f.Value} font)")) +
                " - convert it with that layout.";
        var parts = new List<string>();
        lock (_report)
        {
            static string Count(int n) => n == 1 ? "1 character" : $"{n} characters";
            static string Sample(IEnumerable<string> set) => string.Join(" ", set.Take(8)) + (set.Count() > 8 ? " …" : "");
            // Left out (or a stand-in), kept as itself, and the .txt: told apart, so the typist knows which
            // characters are gone and which are still there in another font (P3b) - or, out of a legacy font,
            // which codes the table could not read.
            if (_leftOut.Count > 0)
                parts.Add($"{Count(_leftOut.Count)} left out - the font cannot show {(_leftOut.Count == 1 ? "it" : "them")} (a stand-in where it has one): {Sample(_leftOut)}");
            var kept = _kept.Where(k => !_leftOut.Contains(k)).ToList();
            bool intoLegacy = SelectedOutputLayout is { Key.IsUnicode: false };
            if (kept.Count > 0)
                parts.Add(intoLegacy
                    ? $"{Count(kept.Count)} kept as {(kept.Count == 1 ? "itself" : "themselves")} - the font has no glyph for {(kept.Count == 1 ? "it" : "them")}, so Word draws {(kept.Count == 1 ? "it" : "them")} in another font: {Sample(kept)}"
                    : $"{Count(kept.Count)} had no mapping (passed through as {(kept.Count == 1 ? "it was" : "they were")}): {Sample(kept)}");
            if (_txtDropped.Count > 0)
                parts.Add($"{Count(_txtDropped.Count)} left out of the .txt - Windows-1252 has no byte for {(_txtDropped.Count == 1 ? "it" : "them")}: {Sample(_txtDropped)}");
        }
        if (parts.Count == 0) return $"{what} converted.{others}";
        return $"{what} converted. {string.Join(". ", parts)}.{others}";
    }

    private ConversionOptions Options() => new()
    {
        Numerals = Numerals,
        LanguageId = SelectedLanguage?.Id,
        Dictionary = UseDictionary ? Dictionary : null
    };

    public string TargetFontName => SelectedOutputFont?.Name ?? "Nirmala UI";

    /// <summary>
    /// Which text of a formatted document is converted (BUG-03): from Unicode into a legacy font,
    /// the stretches in the script (English keeps its own font); from a legacy font, the runs set
    /// in one of the input layout's fonts.
    /// </summary>
    public RunScope Scope =>
        SelectedInputLayout is null || SelectedOutputLayout is null ? RunScope.All
        : SelectedInputLayout.Key.IsUnicode ? (SelectedOutputLayout.Key.IsUnicode ? RunScope.All : new RunScope(true, null))
        : new RunScope(false, IsInputFont);

    /// <summary>Is a run's font (as Word or WPF names it) one of the input layout's fonts?</summary>
    private bool IsInputFont(string fontName)
    {
        string name = fontName.Contains('#') ? fontName[(fontName.LastIndexOf('#') + 1)..] : fontName;
        name = name.Split(',')[0].Trim();
        var layout = SelectedInputLayout;
        if (layout is null || name.Length == 0) return true;
        var names = layout.Members.Select(m => m.Family).Append(layout.Font?.Family).Append(SelectedInputFont?.Name);
        bool mine = names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (!mine && InputLayouts.FirstOrDefault(o => o != layout && !o.Key.IsUnicode &&
                o.Members.Any(m => string.Equals(m.Family, name, StringComparison.OrdinalIgnoreCase))) is { } other)
            lock (_otherLayoutFonts) _otherLayoutFonts[name] = other.Name;
        return mine;
    }

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
            // Two files picked from two folders can share a name; the second must not
            // quietly replace the first in the output folder.
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Files.Count; i++)
            {
                var f = Files[i];
                f.Status = "Converting…";
                _detected = SelectedInputLayout.IsCombined ? RecogniseFile(f.Path, srcUni, font, fontName) : null;
                Progress = (double)i / Files.Count * 100;
                await Task.Yield();
                try
                {
                    string outPath = Path.Combine(OutputFolder, f.Name);
                    if (string.Equals(Path.GetFullPath(outPath), Path.GetFullPath(f.Path), StringComparison.OrdinalIgnoreCase))
                        outPath = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(f.Name) + "-converted" + Path.GetExtension(f.Name));
                    outPath = Unused(outPath, taken);
                    // A .doc is read through Word: its table is recognised from the whole
                    // text Word hands over, before its runs are converted.
                    var layout = SelectedInputLayout;
                    Action<string>? readWhole = layout.IsCombined
                        ? t => { if (Letters(t) > 0) _detected = Detect(t, layout); }
                        : null;
                    // A legacy .txt holds Windows-1252 bytes only: what it has no byte for is left out of it, and said so.
                    string ext = Path.GetExtension(f.Path).ToLowerInvariant();
                    _txtOut = !tgtUni && (ext == ".txt" || (ext == ".rtf" && PlainText));
                    try
                    {
                        FileConversion.ConvertFile(f.Path, outPath, ConvertText, srcUni, tgtUni, font, fontName, PlainText, readWhole,
                            SelectedInputLayout.Key.IsUnicode ? null : IsInputFont);
                    }
                    finally { _txtOut = false; }
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

    /// <summary>
    /// Which of a many-encoding layout's tables a file was typed in, read from the
    /// whole file: its runs reach the converter one at a time, and a short first run
    /// cannot tell Bhasha Bharati-1 from -2. The file is read through the same code
    /// that converts it, into a scratch folder that is removed again.
    /// </summary>
    private string? RecogniseFile(string path, bool sourceIsUnicode, FontFamily font, string fontName)
    {
        // A .doc is converted by Word itself; opening it twice would double the wait. It
        // is recognised inside its conversion instead (FileConversion's readWhole).
        if (string.Equals(Path.GetExtension(path), ".doc", StringComparison.OrdinalIgnoreCase)) return null;
        var text = new System.Text.StringBuilder();
        string scratch = Path.Combine(Path.GetTempPath(), "TextSoft-read-" + Guid.NewGuid().ToString("N"));
        try
        {
            FileConversion.ConvertFile(path, Path.Combine(scratch, Path.GetFileName(path)),
                s => { text.Append(s).Append(' '); return s; }, sourceIsUnicode, sourceIsUnicode, font, fontName, plainText: true);
        }
        catch { /* unreadable here: the real conversion reports it */ }
        finally
        {
            try { if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true); } catch { }
        }
        return Letters(text.ToString()) > 0 ? Detect(text.ToString(), SelectedInputLayout!) : null;
    }

    /// <summary>A path in the output folder nothing has written to yet this batch.</summary>
    private static string Unused(string path, HashSet<string> taken)
    {
        string dir = Path.GetDirectoryName(path)!, name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        string candidate = path;
        for (int n = 2; !taken.Add(candidate); n++) candidate = Path.Combine(dir, $"{name} ({n}){ext}");
        return candidate;
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
            // A layout that gathers several font products renders in whichever of its
            // fonts this PC has, and converts with that font's table by default. A layout
            // of one family (Shree-Lipi 7.0, 4.0/5.0/6.0) lists that family's fonts only, and
            // an ordinary one the fonts that convert the way its own font does (BUG-14: a
            // SHREE-GUJ7 face could be stamped on 4/5/6 bytes, which it draws otherwise).
            string? ownKey = font?.ConversionKey;
            var members = lang.Fonts.Where(f => f.Type != FontType.Unicode && fl.Includes(f, ownKey)).ToList();
            if (members.Count > 0)
                font = members.FirstOrDefault(f => f.Id == fl.FontId && _fonts.IsAvailable(f))
                       ?? members.FirstOrDefault(f => _fonts.IsAvailable(f)) ?? font ?? members[0];
            string enc = font?.ConversionKey ?? fl.FontId ?? "";
            var encodings = fl.Encodings is { Count: > 0 } ? fl.Encodings : (enc.Length > 0 ? new List<string> { enc } : new List<string>());
            bool available = encodings.Count > 0 && encodings.All(e =>
                _pipeline.CanConvert(LayoutKey.Legacy(e), LayoutKey.Unicode, out _) && _pipeline.CanConvert(LayoutKey.Unicode, LayoutKey.Legacy(e), out _));
            list.Add(new LayoutOption
            {
                Name = fl.Name, Key = LayoutKey.Legacy(enc.Length > 0 ? enc : "fl:" + fl.Id), Font = font, Available = available,
                Encodings = encodings, Members = members,
            });
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
        // Every installed font of a many-font layout, right after its default.
        foreach (var m in layout?.Members ?? Array.Empty<FontAsset>())
        {
            if (!_fonts.IsAvailable(m) || target.Any(t => string.Equals(t.Name, m.Family, StringComparison.OrdinalIgnoreCase))) continue;
            target.Add(new FontChoice { Name = m.Family, Family = _fonts.Resolve(m) });
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
        // A legacy layout offers its own fonts only: its bytes in any other font are other
        // letters (a SHREE-GUJ7 face on 4/5/6 bytes draws પ for ઙ્ક). Every installed font is
        // offered only when none of the layout's is on this PC, so there is still a choice.
        bool ownOnly = layout is { Key.IsUnicode: false } && target.Count > 0;
        if (!ownOnly)
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
        // One many-font layout on both sides is a conversion between its fonts (Gopika text
        // to a Bhasha Bharti-2 face): the input is recognised from the text, the output
        // follows the output font.
        if (i.Key == o.Key && !(i.IsCombined && o.IsCombined)) { CanConvert = false; SetStatus("Input and output layouts are the same.", true); return; }
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
