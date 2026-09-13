using System.Collections.ObjectModel;
using System.Windows;
using FontSoftware.App.Infrastructure;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

namespace FontSoftware.App.ViewModels;

/// <summary>
/// FR-4 font converter panel. Picks a source and target font, runs the
/// client-supplied mapping table, and copies the result. Both directions are
/// just different font pairs, so no explicit direction switch is needed.
/// </summary>
public sealed class ConverterViewModel : ObservableObject
{
    private readonly AssetBundle _bundle;
    private readonly FontResolver _fonts;

    public ObservableCollection<FontAsset> Fonts { get; } = new();

    private FontAsset? _sourceFont;
    public FontAsset? SourceFont
    {
        get => _sourceFont;
        set { if (SetProperty(ref _sourceFont, value)) { OnPairChanged(); OnPropertyChanged(nameof(SourceFontFamily)); } }
    }

    private FontAsset? _targetFont;
    public FontAsset? TargetFont
    {
        get => _targetFont;
        set { if (SetProperty(ref _targetFont, value)) { OnPairChanged(); OnPropertyChanged(nameof(TargetFontFamily)); OnPropertyChanged(nameof(TargetIsLegacy)); } }
    }

    /// <summary>
    /// Draw the result by glyph index rather than as Unicode text. A legacy font
    /// puts letters on bytes Unicode reserves for invisible characters — ISFOC
    /// Gujarati has ર on U+00AD SOFT HYPHEN — and WPF honours the Unicode meaning,
    /// so the letter disappears from the box even though the converted text is
    /// correct and Word prints it. See <see cref="Controls.LegacyTextView"/>.
    /// </summary>
    public bool TargetIsLegacy => TargetFont?.Type == FontType.Legacy;

    /// <summary>Font to render the input box in (the source font, incl. bundled legacy fonts).</summary>
    public System.Windows.Media.FontFamily SourceFontFamily => _fonts.Resolve(SourceFont);

    /// <summary>Font to render the output box in (the target font).</summary>
    public System.Windows.Media.FontFamily TargetFontFamily => _fonts.Resolve(TargetFont);

    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    private string _outputText = string.Empty;
    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, value);
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private bool _canConvert;
    public bool CanConvert
    {
        get => _canConvert;
        private set { if (SetProperty(ref _canConvert, value)) ConvertCommand.RaiseCanExecuteChanged(); }
    }

    // ---- Premium-gated feature (FR-5 demo): auto-copy output after converting ----

    private bool _premiumUnlocked;
    /// <summary>True when a premium license is active; enables premium toggles.</summary>
    public bool PremiumUnlocked
    {
        get => _premiumUnlocked;
        set
        {
            if (!SetProperty(ref _premiumUnlocked, value)) return;
            if (!value) AutoCopyOutput = false; // locked: the feature goes too
            ConvertCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _autoCopyOutput;
    /// <summary>Premium: copy the result to the clipboard automatically after Convert.</summary>
    public bool AutoCopyOutput
    {
        get => _autoCopyOutput;
        set => SetProperty(ref _autoCopyOutput, value && PremiumUnlocked);
    }

    public RelayCommand ConvertCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand SwapCommand { get; }

    public ConverterViewModel(AssetBundle bundle, FontResolver fonts)
    {
        _bundle = bundle;
        _fonts = fonts;

        // All fonts across all languages are selectable as convert endpoints.
        foreach (var font in bundle.Languages.SelectMany(l => l.Fonts)
                                   .GroupBy(f => f.Id).Select(g => g.First()))
            Fonts.Add(font);

        ConvertCommand = new RelayCommand(Convert, () => CanConvert && PremiumUnlocked);
        CopyCommand = new RelayCommand(Copy);
        SwapCommand = new RelayCommand(Swap);

        if (Fonts.Count > 0) SourceFont = Fonts[0];
        if (Fonts.Count > 1) TargetFont = Fonts[1];
    }

    private void OnPairChanged()
    {
        if (SourceFont is null || TargetFont is null || SourceFont.ConversionKey == TargetFont.ConversionKey)
        {
            CanConvert = false;
            Status = "Pick two fonts with different encodings.";
            return;
        }

        var table = _bundle.FindMapping(SourceFont.ConversionKey, TargetFont.ConversionKey);
        if (table is null)
        {
            CanConvert = false;
            Status = $"No mapping table for {SourceFont.Name} → {TargetFont.Name}.";
        }
        else
        {
            CanConvert = true;
            Status = string.Empty;
        }
    }

    private void Convert()
    {
        if (SourceFont is null || TargetFont is null)
            return;

        var converter = _bundle.CreateConverter(SourceFont.ConversionKey, TargetFont.ConversionKey);
        if (converter is null)
        {
            Status = "No mapping table available for this pair.";
            return;
        }

        ConversionResult result = converter.Convert(InputText);
        OutputText = result.Text;
        Status = result.HasUnmapped
            ? $"Converted. {result.UnmappedCharacters.Count} character(s) had no mapping and were passed through."
            : "Converted.";

        // Premium: auto-copy the result if enabled.
        if (PremiumUnlocked && AutoCopyOutput && !string.IsNullOrEmpty(OutputText))
        {
            Clipboard.SetText(OutputText);
            Status += " Copied.";
        }
    }

    private void Copy()
    {
        if (!string.IsNullOrEmpty(OutputText))
        {
            Clipboard.SetText(OutputText);
            Status = "Output copied to clipboard.";
        }
    }

    private void Swap()
    {
        (SourceFont, TargetFont) = (TargetFont, SourceFont);
        (InputText, OutputText) = (OutputText, InputText);
    }
}
