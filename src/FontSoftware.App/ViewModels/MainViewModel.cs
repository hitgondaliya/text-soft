using System.Collections.ObjectModel;
using System.Windows.Threading;
using FontSoftware.App.Infrastructure;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;
using FontSoftware.Engine;
using FontSoftware.Licensing;

namespace FontSoftware.App.ViewModels;

/// <summary>
/// Root view model. Owns FR-1 selection, drives the FR-2 keyboard viewer,
/// hosts the FR-3 typing engine, exposes the FR-4 converter panel, and applies
/// FR-6 settings.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AssetBundle _bundle;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly IAppLog _log;
    private readonly StartupService _startup;
    private readonly FontResolver _fonts;

    private readonly KeyboardThread _keyboard;   // the hook and the engine run there, never on the UI thread
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private readonly InputInjector _injector;
    private readonly TypingEngine _engine;
    private readonly DispatcherTimer _licenseTimer;

    private readonly List<KeyCapViewModel> _allKeys = new();

    /// <summary>
    /// The always-available "Unicode" font offered for every language, and the
    /// default selection. Nirmala UI ships with Windows 8+ and covers every
    /// supported Indic script, so Unicode text renders even when none of the
    /// client's own fonts are installed. Every keyboard layout already emits
    /// Unicode, so picking this types correctly into any app.
    /// </summary>
    private static readonly FontAsset UnicodeFont = new()
    {
        Id = "unicode",
        Name = "Unicode",
        Family = "Nirmala UI",
        Type = FontType.Unicode
    };

    /// <summary>Raised when typing is toggled, so the view can show the FR-3 toast.</summary>
    public event Action<bool, string, string>? ToggleNotified;

    // ---- FR-1 selection ----
    public ObservableCollection<LanguageProfile> Languages { get; } = new();
    public ObservableCollection<FontLayout> FontLayouts { get; } = new();
    public ObservableCollection<FontAsset> Fonts { get; } = new();
    public ObservableCollection<LayoutRef> Layouts { get; } = new();

    private LanguageProfile? _selectedLanguage;
    public LanguageProfile? SelectedLanguage
    {
        get => _selectedLanguage;
        set { if (SetProperty(ref _selectedLanguage, value)) OnLanguageChanged(); }
    }

    private FontAsset? _selectedFont;
    public FontAsset? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetProperty(ref _selectedFont, value))
            {
                OnPropertyChanged(nameof(SelectedFontFamily));
                OnPropertyChanged(nameof(SelectedFontMissing));
                ApplyProfile();
            }
        }
    }

    private FontLayout? _selectedFontLayout;
    /// <summary>The Shree-style Font Layout (font product / encoding family) in use.</summary>
    public FontLayout? SelectedFontLayout
    {
        get => _selectedFontLayout;
        set
        {
            if (SetProperty(ref _selectedFontLayout, value))
            {
                OnFontLayoutChanged();
                // When the new layout keeps the font that is already selected (Unicode,
                // typically) the font setter does not fire, so push the profile and the
                // status line here — otherwise the status still names the old layout.
                ApplyProfile();
                PersistSelection();
            }
        }
    }

    private LayoutRef? _selectedLayout;
    public LayoutRef? SelectedLayout
    {
        get => _selectedLayout;
        set { if (SetProperty(ref _selectedLayout, value)) ApplyProfile(); }
    }

    private string _legacyFontHint = "";
    /// <summary>
    /// A legacy layout types characters only its own fonts can draw; in Word (or any
    /// app) the user must pick one of those fonts or they see boxes. This names the
    /// installed ones so the choice is obvious — the same step Shree-Lipi users take.
    /// </summary>
    public string LegacyFontHint
    {
        get => _legacyFontHint;
        private set { if (SetProperty(ref _legacyFontHint, value)) OnPropertyChanged(nameof(ShowLegacyFontHint)); }
    }
    public bool ShowLegacyFontHint => _legacyFontHint.Length > 0;

    /// <summary>The installed font Word gets when legacy text is typed (null = none installed).</summary>
    public string? LegacyFontName { get; private set; }

    private void UpdateLegacyFontHint()
    {
        LegacyFontName = null;
        if (!LegacyTypingActive || SelectedFont is null || SelectedLanguage is null)
        {
            LegacyFontHint = "";
            return;
        }
        string key = SelectedFont.ConversionKey;
        var installed = _bundle.Languages.SelectMany(l => l.Fonts)
            .Where(f => f.ConversionKey == key && !string.IsNullOrWhiteSpace(f.Family) && SystemFontService.IsInstalled(f.Family))
            .Select(f => f.Family)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Prefer the font chosen in the Render box when it is installed, else the first installed one.
        LegacyFontName = installed.FirstOrDefault(f => string.Equals(f, SelectedFont.Family, StringComparison.OrdinalIgnoreCase)) ?? installed.FirstOrDefault();
        // Other installed fonts of the same layout that type with a different table
        // (a Bhasha Bharti-2 face while a Bhasha Bharati-1 face is selected).
        var siblings = SelectedFontLayout is { IsCombined: true } layout
            ? SelectedLanguage.Fonts.Where(f => layout.Includes(f) && !string.IsNullOrWhiteSpace(f.Family) && SystemFontService.IsInstalled(f.Family))
                .Select(f => f.Family).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        LegacyFontHint = installed.Count > 0
            ? $"Word, Outlook and WordPad get the font {LegacyFontName} automatically. In Notepad or a browser, set the font to {LegacyFontName} yourself — {SelectedFontLayout?.Name} text shows as boxes in any other font."
            : siblings.Count > 0
                ? $"{SelectedFont.Name} is not installed on this PC. These {SelectedFontLayout?.Name} fonts are: {string.Join(", ", siblings.Take(6))}{(siblings.Count > 6 ? "…" : "")} — pick one of them in the Font box."
                : $"No {SelectedFontLayout?.Name} font is installed on this PC, so typed text will show as boxes in other apps. Install one of its fonts.";
    }

    private bool _legacyTypingActive;
    /// <summary>True when the active Font Layout types live in a legacy encoding (ShreeLipi-Ex etc.).</summary>
    public bool LegacyTypingActive
    {
        get => _legacyTypingActive;
        private set => SetProperty(ref _legacyTypingActive, value);
    }

    /// <summary>Representative script character for the active language (app badge).</summary>
    public string ActiveScriptChar => SelectedLanguage?.Sample ?? "अ";

    /// <summary>
    /// Font for the on-screen keyboard, which always shows the layout's Unicode
    /// characters. For a legacy Font Layout the selected font is a legacy (byte-
    /// mapped) font that can't render Unicode, so the preview uses a Unicode font;
    /// the legacy font is what the TYPED output lands in, in the target app.
    /// </summary>
    public System.Windows.Media.FontFamily SelectedFontFamily =>
        SelectedFont is { Type: FontType.Legacy }
            ? new System.Windows.Media.FontFamily("Nirmala UI, Segoe UI")
            : _fonts.Resolve(SelectedFont);

    /// <summary>True unless the selected font can be rendered (bundled or installed); shows an install hint.</summary>
    public bool SelectedFontMissing => SelectedFont is not null && !_fonts.IsAvailable(SelectedFont);

    private bool _showAllInstalledFonts;
    /// <summary>Include every font installed on this PC in the Font list (FR-1 convenience).</summary>
    public bool ShowAllInstalledFonts
    {
        get => _showAllInstalledFonts;
        set { if (SetProperty(ref _showAllInstalledFonts, value)) OnLanguageChanged(); }
    }

    // ---- FR-2 keyboard viewer ----
    public ObservableCollection<ObservableCollection<KeyCapViewModel>> KeyboardRows { get; } = new();

    /// <summary>
    /// Which layer of the keyboard the viewer is showing. A vendor keyboard has
    /// four: the two printed ones, the keyboard's own third layer on Ctrl+Space,
    /// and the symbols every keyboard of the script shares on Ctrl+Shift+1.
    /// </summary>
    private KeyLayer _layer = KeyLayer.Normal;
    public KeyLayer Layer
    {
        get => _layer;
        set
        {
            if (!SetProperty(ref _layer, value)) return;
            OnPropertyChanged(nameof(ShowNormalLayer));
            OnPropertyChanged(nameof(ShowShiftLayer));
            OnPropertyChanged(nameof(ShowEscapeLayer));
            OnPropertyChanged(nameof(ShowSymbolLayer));
            RefreshKeyboard();
        }
    }

    public bool ShowNormalLayer
    {
        get => Layer == KeyLayer.Normal;
        set { if (value) Layer = KeyLayer.Normal; }
    }

    public bool ShowShiftLayer
    {
        get => Layer == KeyLayer.Shift;
        set { if (value) Layer = KeyLayer.Shift; }
    }

    public bool ShowEscapeLayer
    {
        get => Layer == KeyLayer.Escape;
        set { if (value) Layer = KeyLayer.Escape; }
    }

    public bool ShowSymbolLayer
    {
        get => Layer == KeyLayer.Symbols;
        set { if (value) Layer = KeyLayer.Symbols; }
    }

    /// <summary>True when the selected keyboard has a Ctrl+Space layer of its own.</summary>
    public bool HasEscapeLayer { get => _hasEscapeLayer; private set => SetProperty(ref _hasEscapeLayer, value); }
    private bool _hasEscapeLayer;

    /// <summary>True when the selected keyboard has the shared Ctrl+Shift+1 symbols.</summary>
    public bool HasSymbolLayer { get => _hasSymbolLayer; private set => SetProperty(ref _hasSymbolLayer, value); }
    private bool _hasSymbolLayer;

    // ---- FR-3 typing ----
    private bool _typingEnabled;
    public bool TypingEnabled
    {
        get => _typingEnabled;
        set
        {
            // Full-lock: never let typing run while the app is unlicensed.
            if (value && License is { IsActive: false }) value = false;
            if (SetProperty(ref _typingEnabled, value))
                _keyboard.Invoke(() => _engine.SetEnabled(value));
        }
    }

    public RelayCommand ToggleTypingCommand { get; }

    /// <summary>True when the app is locked (no valid license) — drives the lock overlay.</summary>
    public bool IsAppLocked => !License.IsActive;

    // Keep premium gating + typing + the overlay in sync with the license state.
    private void OnLicenseActiveChanged()
    {
        Converter.PremiumUnlocked = License.IsActive;
        OnPropertyChanged(nameof(IsAppLocked));
        if (!License.IsActive)
            TypingEnabled = false;
    }

    // ---- Phonetic "type by sound" ----
    private bool _hasPhonetic;
    /// <summary>True when the selected language has a phonetic scheme (enables the toggle).</summary>
    public bool HasPhonetic
    {
        get => _hasPhonetic;
        private set => SetProperty(ref _hasPhonetic, value);
    }

    private bool _phoneticMode;
    /// <summary>Type-by-sound mode: roman input transliterates to the script.</summary>
    public bool PhoneticMode
    {
        get => _phoneticMode;
        set
        {
            if (SetProperty(ref _phoneticMode, value))
            {
                _keyboard.Invoke(() => _engine.SetPhoneticMode(value));
                Status = value ? "Phonetic mode: type words the way they sound." : Status;
            }
        }
    }

    // ---- FR-4 converter ----
    public ConverterViewModel Converter { get; }

    /// <summary>The keyboard designer (FR-2: keyboards are data, so the user can make their own).</summary>
    public KeyboardEditorViewModel Editor { get; }

    // ---- FR-5 licensing ----
    public LicenseViewModel License { get; }

    // ---- FR-6 settings ----
    public IReadOnlyList<ToggleKeyOption> ToggleKeyOptions { get; } = ToggleKeyOption.All;

    private ToggleKeyOption _selectedToggleKey;
    public ToggleKeyOption SelectedToggleKey
    {
        get => _selectedToggleKey;
        set
        {
            if (SetProperty(ref _selectedToggleKey, value) && value is not null)
            {
                _keyboard.Invoke(() => _engine.ToggleKey = value.VirtualKey);
                _settings.ToggleKey = value.VirtualKey;
                Persist();
            }
        }
    }

    private bool _showToggleNotification;
    public bool ShowToggleNotification
    {
        get => _showToggleNotification;
        set
        {
            if (SetProperty(ref _showToggleNotification, value))
            {
                _settings.ShowToggleNotification = value;
                Persist();
            }
        }
    }

    private bool _autoStart;
    /// <summary>Start the app automatically at Windows login (FRD NFR: optional auto-start).</summary>
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (!SetProperty(ref _autoStart, value))
                return;

            // The registry write is the source of truth; only persist/keep the
            // toggle if it actually applied, so the checkbox never claims a state
            // the OS didn't accept.
            if (_startup.SetEnabled(value))
            {
                _settings.AutoStart = value;
                Persist();
            }
            else
            {
                Status = "Could not change the auto-start setting. See the log for details.";
                SetProperty(ref _autoStart, !value, nameof(AutoStart)); // revert UI
            }
        }
    }

    private string _excludedProcesses = string.Empty;
    /// <summary>Comma-separated process names where typing is suppressed (FR-3).</summary>
    public string ExcludedProcesses
    {
        get => _excludedProcesses;
        set
        {
            if (!SetProperty(ref _excludedProcesses, value))
                return;

            var names = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(n => n.Trim().ToLowerInvariant())
                             .Where(n => n.Length > 0)
                             .Distinct()
                             .ToList();

            _keyboard.Invoke(() =>
            {
                _engine.ExcludedProcesses.Clear();
                foreach (var n in names)
                    _engine.ExcludedProcesses.Add(n);
            });

            _settings.ExcludedProcesses = names;
            Persist();
        }
    }

    public RelayCommand SaveDefaultsCommand { get; }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public MainViewModel(AssetBundle bundle, AppSettings settings, SettingsStore store, IAppLog log, FontResolver fonts)
    {
        _bundle = bundle;
        _settings = settings;
        _store = store;
        _log = log;
        _fonts = fonts;

        _startup = new StartupService(log);
        _keyboard = new KeyboardThread(log);
        _injector = new InputInjector(log);
        _engine = new TypingEngine(_keyboard.Hook, _injector, log) { ToggleKey = settings.ToggleKey };
        _keyboard.Restored = _engine.Resync;
        _engine.Toggled += OnEngineToggled;
        foreach (var name in settings.ExcludedProcesses)
            _engine.ExcludedProcesses.Add(name);

        Converter = new ConverterViewModel(bundle, fonts);

        // Keyboards the user designed are listed alongside the shipped ones.
        var keyboards = new UserKeyboards();
        int mine = keyboards.Attach(bundle);
        if (mine > 0) log.Info($"Keyboards of your own: {mine}.");
        Editor = new KeyboardEditorViewModel(bundle, keyboards, log);
        Editor.Saved += OnKeyboardSaved;

        string appVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        LicenseApiClient.Log = msg => _log.Warn(msg);
        var licenseManager = new LicenseManager(
            new LicenseApiClient(LicenseEndpoints.Resolve()),
            new TokenReader(new Ed25519Verifier()),
            new WindowsDeviceIdProvider(),
            new FileLicenseStore(),
            appVersion);
        License = new LicenseViewModel(licenseManager);

        // The engine asks the licence manager itself on every key, so a patched
        // view model alone cannot switch typing back on.
        _engine.Gate = () => licenseManager.IsActive;

        // Full-lock gating: the app is usable only while licensed (demo or full).
        Converter.PremiumUnlocked = License.IsActive;
        License.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LicenseViewModel.IsActive))
                OnLicenseActiveChanged();
        };

        // Re-validate against the server every 6h while running, and re-check
        // expiry locally every minute so a licence that ends while the app is open
        // stops it within the minute.
        _licenseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        int ticks = 0;
        _licenseTimer.Tick += (_, _) =>
        {
            ticks++;
            if (ticks % 360 == 0) _ = License.RefreshAsync();
            else _ = License.ReevaluateAsync();
        };
        _licenseTimer.Start();

        // Load + re-validate now; the lock overlay stays up until this resolves.
        _ = License.InitializeAsync();

        ToggleTypingCommand = new RelayCommand(() => TypingEnabled = !TypingEnabled);
        SaveDefaultsCommand = new RelayCommand(SaveDefaults);

        _showToggleNotification = settings.ShowToggleNotification;
        // The registry is authoritative (the installer may have set auto-start),
        // so reflect the real state and reconcile the saved setting to match.
        _autoStart = _startup.IsEnabled();
        if (_autoStart != settings.AutoStart)
        {
            settings.AutoStart = _autoStart;
            _store.Save(settings);
        }
        _excludedProcesses = string.Join(", ", settings.ExcludedProcesses);
        _selectedToggleKey = ToggleKeyOption.All.FirstOrDefault(o => o.VirtualKey == settings.ToggleKey)
                             ?? ToggleKeyOption.All[0];

        foreach (var lang in bundle.Languages)
            Languages.Add(lang);

        BuildKeyboardShell();
        RestoreDefaults();
    }

    /// <summary>Install the global hook. Call once the UI (message pump) is up.</summary>
    public void Start()
    {
        try
        {
            _keyboard.Start();
            _log.Info("Keyboard hook installed (on its own thread, with a watchdog).");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to install keyboard hook.", ex);
            Status = "Could not start the typing hook. See the log for details.";
        }
    }

    private void OnLanguageChanged()
    {
        // Read the saved selection before touching anything. Filling the lists
        // below picks each list's first entry, and those picks write themselves
        // back as the new defaults - over the very ids about to be restored.
        bool sameAsSaved = SelectedLanguage is not null && SelectedLanguage.Id == _settings.DefaultLanguageId;
        string? savedFontLayout = _settings.DefaultFontLayoutId;
        string? savedFont = _settings.DefaultFontId;
        string? savedLayout = _settings.DefaultLayoutId;

        Fonts.Clear();
        Layouts.Clear();
        FontLayouts.Clear();
        OnPropertyChanged(nameof(ActiveScriptChar));

        if (SelectedLanguage is null)
            return;

        // A "Unicode" option is ALWAYS offered for every language, and is the
        // default. Every keyboard layout already emits Unicode, so this types
        // correctly into any app and renders in Windows' built-in Indic font
        // even when none of the client's legacy fonts are installed. This is
        // what makes "pick Unicode and whatever font the client has just works".
        Fonts.Add(UnicodeFont);

        // FR-1: show the language's FULL supported font list so the client can
        // see everything the software integrates. Fonts not installed on this PC
        // are shown too, just marked "not installed" — that is the client's
        // machine, not our fault, and hiding them looked like missing features.
        // Bundled legacy fonts (e.g. Shree) render without a system install.
        foreach (var font in SelectedLanguage.Fonts)
        {
            if (string.Equals(font.Family, UnicodeFont.Family, StringComparison.OrdinalIgnoreCase))
                continue; // already covered by the universal Unicode entry
            bool available = _fonts.IsAvailable(font);
            if (!available && font.Optional)
                continue; // a large family (Bhasha Bharati) lists only the faces this PC has
            Fonts.Add(available ? font : font.AsNotInstalled());
        }
        foreach (var layout in SelectedLanguage.Layouts)
            Layouts.Add(layout);

        // Optionally append every other font installed on this PC (deduped by
        // family), so a client's own/private Unicode font can be picked too.
        if (ShowAllInstalledFonts)
        {
            var have = new HashSet<string>(Fonts.Select(f => f.Family), StringComparer.OrdinalIgnoreCase);
            foreach (var fam in SystemFontService.InstalledFamilies())
            {
                if (have.Add(fam))
                    Fonts.Add(new FontAsset { Id = "sys:" + fam, Name = fam, Family = fam, Type = FontType.Unicode });
            }
        }

        // Font Layout list (Shree-style product/encoding families) for this language.
        foreach (var fl in SelectedLanguage.FontLayouts)
            FontLayouts.Add(fl);

        SelectedFont = Fonts.FirstOrDefault();
        SelectedLayout = Layouts.FirstOrDefault();
        // Setting this drives the render font via OnFontLayoutChanged (defaults to Unicode).
        SelectedFontLayout = FontLayouts.FirstOrDefault();

        if (_restoring || sameAsSaved)
        {
            // Put back the font layout, font and keyboard last used with this
            // language. This has to run on every pass over the saved language, not
            // only the first: while the window is coming up WPF rebinds the
            // language box, which re-enters this method, and the fresh defaults
            // above would otherwise stand - which is why the app kept reopening on
            // a keyboard the user had never chosen.
            RestoreDefaultsForLanguage(savedFontLayout, savedFont, savedLayout);
        }
        else
        {
            // The user picked another language: that language, with its fresh
            // defaults, is what we reopen with next time.
            PersistSelection();
        }
    }

    /// <summary>
    /// A Font Layout was chosen: point the render font at the family it implies.
    /// Unicode → the universal Unicode font (or the user's own pick). A legacy
    /// family we ship → that font. A third-party family we don't have yet → a
    /// placeholder entry marked not provided, so it is visible but honest.
    /// </summary>
    private void OnFontLayoutChanged()
    {
        if (SelectedFontLayout is null)
            return;

        if (SelectedFontLayout.IsUnicode)
        {
            SelectedFont = Fonts.FirstOrDefault(f => f.Id == "unicode") ?? Fonts.FirstOrDefault();
            return;
        }

        // A layout that covers several fonts (Bhasha Bharati: Gopika, the -1 faces
        // and the -2 faces) goes to whichever of its fonts this PC has, and each
        // font types with its own table - so a client with only a Bhasha Bharti-2
        // face gets that face, and correct text, without picking anything.
        if (PickLayoutFont(SelectedFontLayout) is { } installed)
        {
            SelectedFont = installed;
            return;
        }

        if (!string.IsNullOrEmpty(SelectedFontLayout.FontId))
        {
            var existing = Fonts.FirstOrDefault(x => x.Id == SelectedFontLayout.FontId);
            if (existing is not null) { SelectedFont = existing; return; }

            var cat = SelectedLanguage?.Fonts.FirstOrDefault(x => x.Id == SelectedFontLayout.FontId);
            var add = cat is null
                ? new FontAsset { Id = SelectedFontLayout.FontId!, Name = SelectedFontLayout.Name, Family = SelectedFontLayout.Name, Type = FontType.Legacy, Display = $"{SelectedFontLayout.Name}  —  not installed" }
                : (_fonts.IsAvailable(cat) ? cat : cat.AsNotInstalled());
            Fonts.Add(add);
            SelectedFont = add;
            return;
        }

        // Third-party family whose font/encoding the client hasn't provided yet.
        string synId = "fl:" + SelectedFontLayout.Id;
        var syn = Fonts.FirstOrDefault(x => x.Id == synId)
                  ?? AddAndReturn(new FontAsset { Id = synId, Name = SelectedFontLayout.Name, Family = SelectedFontLayout.Name, Type = FontType.Legacy, Display = $"{SelectedFontLayout.Name}  —  font not provided" });
        SelectedFont = syn;
    }

    private FontAsset AddAndReturn(FontAsset f) { Fonts.Add(f); return f; }

    /// <summary>
    /// The installed font a legacy layout should type in: the one already selected
    /// if it belongs to the layout, else the layout's own default, else the first
    /// of its fonts this PC has. Null when none of them is installed.
    /// </summary>
    private FontAsset? PickLayoutFont(FontLayout layout)
    {
        if (layout.Encodings is not { Count: > 0 })
            return null; // an ordinary layout: its one font, handled by the caller
        var members = Fonts.Where(f => !f.Id.StartsWith("fl:", StringComparison.Ordinal) && layout.Includes(f)).ToList();
        if (SelectedFont is not null && members.Contains(SelectedFont) && _fonts.IsAvailable(SelectedFont))
            return SelectedFont;
        var installed = members.Where(f => _fonts.IsAvailable(f)).ToList();
        return installed.FirstOrDefault(f => f.Id == layout.FontId) ?? installed.FirstOrDefault();
    }

    /// <summary>Push the current selection into the engine and keyboard viewer.</summary>
    private void ApplyProfile()
    {
        if (SelectedLanguage is null || SelectedLayout is null)
            return;

        try
        {
            var layout = _bundle.GetLayout(SelectedLayout);
            string languageName = SelectedLanguage.Name, fontName = SelectedFont?.Name ?? string.Empty;
            _keyboard.Invoke(() => _engine.SetProfile(layout, languageName, fontName));

            // Phonetic scheme for this language (null if none) drives the type-by-sound toggle.
            var translit = _bundle.GetTransliterator(SelectedLanguage);
            _keyboard.Invoke(() => _engine.SetTransliterator(translit));
            HasPhonetic = translit is not null;
            if (!HasPhonetic && PhoneticMode)
                PhoneticMode = false;

            // Legacy Font Layout: type live in that legacy encoding so it shows in
            // the legacy font. Unicode layout (or no map available) types plain Unicode.
            FontConverter? legacy = null;
            if (SelectedFontLayout is not null && !SelectedFontLayout.IsUnicode && SelectedFont is not null)
                legacy = _bundle.CreateConverterToLegacy(SelectedFont.ConversionKey);
            LegacyTypingActive = legacy is not null;
            UpdateLegacyFontHint();
            string? legacyFont = LegacyFontName;
            _keyboard.Invoke(() => _engine.SetLegacyConverter(legacy, legacyFont));

            RefreshKeyboard();
            PersistSelection(); // reopen with exactly this language/font/keyboard
            _log.Info($"Selection: language={SelectedLanguage.Id} fontLayout={SelectedFontLayout?.Id} " +
                      $"font={SelectedFont?.Id} keyboard={SelectedLayout.Id}");
            string mode = LegacyTypingActive ? " (legacy)" : SelectedFontLayout is not null && !SelectedFontLayout.IsUnicode ? " (font not provided — types Unicode)" : string.Empty;
            Status = $"Active: {SelectedLanguage.Name} · {SelectedFontLayout?.Name}{mode} · {SelectedLayout.Name}";
        }
        catch (ConfigException ex)
        {
            Status = ex.Message; // FR-1: graceful, clear message on bad asset
        }
    }

    private void BuildKeyboardShell()
    {
        _allKeys.Clear();
        KeyboardRows.Clear();
        foreach (var physRow in PhysicalKeyboard.Rows)
        {
            var row = new ObservableCollection<KeyCapViewModel>();
            foreach (var key in physRow)
            {
                var cap = new KeyCapViewModel(key.VirtualKey, key.Label);
                row.Add(cap);
                _allKeys.Add(cap);
            }
            KeyboardRows.Add(row);
        }
    }

    private void RefreshKeyboard()
    {
        KeyboardLayout? layout = null;
        if (SelectedLayout is not null)
        {
            try { layout = _bundle.GetLayout(SelectedLayout); }
            catch (ConfigException) { layout = null; }
        }

        HasEscapeLayer = layout?.HasEscape ?? false;
        HasSymbolLayer = layout?.HasSymbols ?? false;
        if ((Layer == KeyLayer.Escape && !HasEscapeLayer) || (Layer == KeyLayer.Symbols && !HasSymbolLayer))
            Layer = KeyLayer.Normal;   // this keyboard has no such layer

        foreach (var cap in _allKeys)
        {
            var mapping = layout?.Find(cap.VirtualKey);
            string? mapped = Layer switch
            {
                KeyLayer.Escape => EscapeText(layout, cap, symbols: false),
                KeyLayer.Symbols => EscapeText(layout, cap, symbols: true),
                KeyLayer.Shift => mapping?.Shift ?? mapping?.Base,
                _ => mapping?.Base,
            };

            if (!string.IsNullOrEmpty(mapped))
            {
                cap.Display = mapped;
                cap.IsMapped = true;
            }
            else
            {
                // A letter key this keyboard does not use types nothing at all, so
                // printing the English letter on it is a lie - that is what made a
                // Gujarati keyboard look like it had a Z on it. Digits, punctuation
                // and the symbol keys do pass through, so they keep their label.
                bool letter = cap.VirtualKey is >= 'A' and <= 'Z';
                cap.Display = letter ? string.Empty : cap.Label;
                cap.IsMapped = false;
            }
        }
    }

    /// <summary>
    /// What an escape layer puts on this key. Those layers are keyed by the
    /// character the key types on a US keyboard, and a key can carry an entry on
    /// its own and another on its shifted twin (ॢ on l, ॣ on k, ₹ on $).
    /// </summary>
    private static string? EscapeText(KeyboardLayout? layout, KeyCapViewModel cap, bool symbols)
    {
        if (layout is null) return null;
        (char bas, char shifted) = UsKey(cap.VirtualKey);
        if (bas == '\0') return null;
        return layout.EscapeFor(bas.ToString(), symbols) ?? layout.EscapeFor(shifted.ToString(), symbols);
    }

    /// <summary>The characters a US keyboard puts on a key, unshifted and shifted.</summary>
    private static (char Base, char Shift) UsKey(int vk) => vk switch
    {
        >= 'A' and <= 'Z' => (char.ToLowerInvariant((char)vk), (char)vk),
        >= '0' and <= '9' => ((char)vk, ")!@#$%^&*("[vk - '0']),
        186 => (';', ':'),
        187 => ('=', '+'),
        188 => (',', '<'),
        189 => ('-', '_'),
        190 => ('.', '>'),
        191 => ('/', '?'),
        192 => ('`', '~'),
        219 => ('[', '{'),
        220 => ((char)92, '|'),
        221 => (']', '}'),
        222 => ((char)39, '"'),
        _ => ('\0', '\0'),
    };

    /// <summary>A keyboard was just designed: offer it in the typing screen straight away.</summary>
    private void OnKeyboardSaved(string languageId, LayoutRef reference)
    {
        if (SelectedLanguage?.Id != languageId) return;
        if (Layouts.All(l => l.Id != reference.Id)) Layouts.Add(reference);
        // The keyboard being typed with was just changed: type with the new one.
        else if (SelectedLayout?.Id == reference.Id) ApplyProfile();
    }

    private void OnEngineToggled(object? sender, ToggleEventArgs e)
    {
        // The engine runs on the keyboard thread; the view model belongs to the UI one.
        if (!_ui.CheckAccess())
        {
            _ui.BeginInvoke(() => OnEngineToggled(sender, e));
            return;
        }
        SetProperty(ref _typingEnabled, e.Enabled, nameof(TypingEnabled));
        if (ShowToggleNotification)
            ToggleNotified?.Invoke(e.Enabled, e.LanguageName, e.FontName);
    }

    // ---- FR-6 ----
    /// <summary>
    /// True while the saved selection is being applied at launch. Selection changes
    /// made during that pass must not be written back, or the intermediate fallback
    /// picks (first language / first font) would overwrite what the user had.
    /// </summary>
    private bool _restoring;

    private void RestoreDefaults()
    {
        _restoring = true;
        try { SelectedLanguage = ResolveDefaultLanguage(); }
        finally { _restoring = false; }
    }

    /// <summary>
    /// Remember the whole current selection immediately (FR-6). The user expects the
    /// app to reopen exactly as they left it after a shutdown, so this runs on every
    /// change rather than waiting for the Save Defaults button.
    /// </summary>
    private void PersistSelection()
    {
        if (_restoring || SelectedLanguage is null)
            return;

        if (_settings.DefaultLanguageId == SelectedLanguage.Id
            && _settings.DefaultFontLayoutId == SelectedFontLayout?.Id
            && _settings.DefaultFontId == SelectedFont?.Id
            && _settings.DefaultLayoutId == SelectedLayout?.Id)
            return; // nothing moved — don't rewrite the file on every keystroke-free redraw

        _settings.DefaultLanguageId = SelectedLanguage.Id;
        _settings.DefaultFontLayoutId = SelectedFontLayout?.Id;
        _settings.DefaultFontId = SelectedFont?.Id;
        _settings.DefaultLayoutId = SelectedLayout?.Id;
        try
        {
            Persist();
        }
        catch (Exception ex)
        {
            // A locked/read-only settings file must not break typing.
            _log.Warn($"Could not save the selection: {ex.Message}");
        }
    }

    private LanguageProfile? ResolveDefaultLanguage()
    {
        var lang = _settings.DefaultLanguageId is null
            ? Languages.FirstOrDefault()
            : Languages.FirstOrDefault(l => l.Id == _settings.DefaultLanguageId) ?? Languages.FirstOrDefault();
        return lang;
    }

    private void RestoreDefaultsForLanguage(string? fontLayoutId, string? fontId, string? layoutId)
    {
        // Font Layout first: choosing one repoints the render font, so applying it
        // after the saved font would undo that font.
        if (fontLayoutId is not null)
        {
            // A layout folded into another (Bhasha Bharti-2 into Bhasha Bharati)
            // still finds its way back.
            var fl = FontLayouts.FirstOrDefault(x => x.Id == fontLayoutId)
                     ?? FontLayouts.FirstOrDefault(x => x.Replaces?.Contains(fontLayoutId, StringComparer.OrdinalIgnoreCase) == true);
            if (fl is not null) SelectedFontLayout = fl;
        }
        if (fontId is not null)
        {
            var f = Fonts.FirstOrDefault(x => x.Id == fontId);
            // The saved font wins unless it is not on this PC while another font of
            // the same layout is - then the installed one, already picked, stays.
            bool keepPicked = f is not null && !_fonts.IsAvailable(f)
                              && SelectedFontLayout is { Encodings.Count: > 0 } && SelectedFont is not null
                              && SelectedFontLayout.Includes(SelectedFont) && _fonts.IsAvailable(SelectedFont);
            if (f is not null && !keepPicked) SelectedFont = f;
        }
        if (layoutId is not null)
        {
            var l = Layouts.FirstOrDefault(x => x.Id == layoutId);
            if (l is not null) SelectedLayout = l;
        }
    }

    private void SaveDefaults()
    {
        PersistSelection();
        Status = "Defaults saved.";
    }

    private void Persist() => _store.Save(_settings);

    public void Dispose()
    {
        _licenseTimer.Stop();
        _engine.Toggled -= OnEngineToggled;
        _keyboard.Invoke(_engine.Dispose);
        _keyboard.Dispose();
    }
}

/// <summary>A selectable toggle key for the settings panel (FR-6).</summary>
public sealed class ToggleKeyOption
{
    public string Name { get; }
    public int VirtualKey { get; }

    public ToggleKeyOption(string name, int virtualKey)
    {
        Name = name;
        VirtualKey = virtualKey;
    }

    public override string ToString() => Name;

    public static IReadOnlyList<ToggleKeyOption> All { get; } = new List<ToggleKeyOption>
    {
        new("CapsLock", 0x14),
        new("Scroll Lock", 0x91),
        new("Right Ctrl", 0xA3),
        new("Right Alt", 0xA5),
        new("Pause/Break", 0x13),
        new("F1", 0x70),
        new("F2", 0x71),
        new("F3", 0x72),
        new("F4", 0x73),
        new("F5", 0x74),
        new("F6", 0x75),
        new("F7", 0x76),
        new("F8", 0x77),
        new("F9", 0x78),
        new("F10", 0x79),
        new("F11", 0x7A),
        new("F12", 0x7B)
    };
}
