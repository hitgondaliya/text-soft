using System.Collections.ObjectModel;
using FontSoftware.App.Infrastructure;
using System.Windows.Input;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;

namespace FontSoftware.App.ViewModels;

/// <summary>
/// Design a keyboard, the way the vendor's own Keyboard Generator does it: pick
/// a language, pick a layer (Normal, Shift, Ctrl+Space), click a key, and choose
/// the character it types from the language's character palette - or build a
/// conjunct of up to four characters with Link and Half. Existing keyboards can
/// be opened and modified. Saved keyboards appear in the keyboard list.
/// </summary>
public sealed class KeyboardEditorViewModel : ObservableObject
{
    /// <summary>The vendor's limits, kept so a keyboard designed here stays one their software would accept.</summary>
    public const int MaxConjunctLength = 4;
    public const int MaxConjuncts = 48;

    private readonly AssetBundle _bundle;
    private readonly UserKeyboards _store;
    private readonly IAppLog _log;
    private readonly List<KeyCapViewModel> _allKeys = new();

    /// <summary>Raised when a keyboard is saved, so the typing screen can list it.</summary>
    public event Action<string, LayoutRef>? Saved;

    public KeyboardEditorViewModel(AssetBundle bundle, UserKeyboards store, IAppLog log)
    {
        _bundle = bundle;
        _store = store;
        _log = log;

        foreach (var language in bundle.Languages) Languages.Add(language);

        NewCommand = new RelayCommand(() => StartNew());
        OpenCommand = new RelayCommand(OpenSelected, () => SelectedSource is not null);
        SaveCommand = new RelayCommand(Save, () => Name.Trim().Length > 0);
        FormConjunctCommand = new RelayCommand(() => ConjunctMode = true);
        LinkCommand = new RelayCommand(() => AddToConjunct(Virama), () => ConjunctMode && Conjunct.Length > 0);
        HalfCommand = new RelayCommand(() => AddToConjunct(Virama), () => ConjunctMode && Conjunct.Length > 0);
        BackspaceCommand = new RelayCommand(() => Conjunct = string.Empty, () => ConjunctMode);
        BlankCommand = new RelayCommand(() => Assign(string.Empty), () => SelectedKey is not null);
        CancelConjunctCommand = new RelayCommand(EndConjunct);
        ApplyConjunctCommand = new RelayCommand(() => Assign(Conjunct), () => ConjunctMode && Conjunct.Length > 0);

        BuildKeyboardShell();
        SelectedLanguage = Languages.FirstOrDefault();
    }

    // ---- what is being designed ----

    public ObservableCollection<LanguageProfile> Languages { get; } = new();

    private LanguageProfile? _selectedLanguage;
    public LanguageProfile? SelectedLanguage
    {
        get => _selectedLanguage;
        set { if (SetProperty(ref _selectedLanguage, value)) OnLanguageChanged(); }
    }

    /// <summary>Keyboards of this language that can be opened and modified.</summary>
    public ObservableCollection<LayoutRef> SourceLayouts { get; } = new();

    private LayoutRef? _selectedSource;
    public LayoutRef? SelectedSource
    {
        get => _selectedSource;
        set { if (SetProperty(ref _selectedSource, value)) Refresh(); }
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) Refresh(); }
    }

    private KeyboardLayout _layout = new();

    // ---- the keyboard being shown ----

    public ObservableCollection<ObservableCollection<KeyCapViewModel>> KeyboardRows { get; } = new();

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
            OnPropertyChanged(nameof(LayerName));
            RefreshKeyboard();
        }
    }

    public bool ShowNormalLayer { get => Layer == KeyLayer.Normal; set { if (value) Layer = KeyLayer.Normal; } }
    public bool ShowShiftLayer { get => Layer == KeyLayer.Shift; set { if (value) Layer = KeyLayer.Shift; } }
    public bool ShowEscapeLayer { get => Layer == KeyLayer.Escape; set { if (value) Layer = KeyLayer.Escape; } }

    public string LayerName => Layer switch
    {
        KeyLayer.Shift => "Shift",
        KeyLayer.Escape => "Ctrl+Space",
        _ => "Normal",
    };

    private KeyCapViewModel? _selectedKey;
    /// <summary>The key waiting for a character, as the vendor's hint puts it: "Click to assign key".</summary>
    public KeyCapViewModel? SelectedKey
    {
        get => _selectedKey;
        set
        {
            var previous = _selectedKey;
            if (!SetProperty(ref _selectedKey, value)) return;
            if (previous is not null) previous.IsSelected = false;
            if (value is not null) value.IsSelected = true;
            OnPropertyChanged(nameof(SelectedKeyLabel));
            Refresh();
        }
    }

    public string SelectedKeyLabel => SelectedKey is null
        ? "Click a key, then pick its character"
        : $"{LayerName} · key {SelectedKey.Label}";

    // ---- the character palette ----

    /// <summary>Every character this language's keyboards can type - what the palette offers.</summary>
    public ObservableCollection<string> Palette { get; } = new();

    /// <summary>The punctuation and signs, behind the vendor's "Special" button.</summary>
    public ObservableCollection<string> Specials { get; } = new();

    private bool _showSpecials;
    public bool ShowSpecials
    {
        get => _showSpecials;
        set => SetProperty(ref _showSpecials, value);
    }

    // ---- building a conjunct ----

    private bool _conjunctMode;
    public bool ConjunctMode
    {
        get => _conjunctMode;
        private set
        {
            if (!SetProperty(ref _conjunctMode, value)) return;
            if (!value) Conjunct = string.Empty;
            Refresh();
        }
    }

    private string _conjunct = string.Empty;
    public string Conjunct
    {
        get => _conjunct;
        private set { if (SetProperty(ref _conjunct, value)) Refresh(); }
    }

    /// <summary>How many conjuncts this keyboard already carries, against the vendor's limit of 48.</summary>
    public string ConjunctCountText => $"{ConjunctCount} of {MaxConjuncts} conjuncts";

    public int ConjunctCount => _layout.Keys.Count(k => IsConjunct(k.Base)) +
                                _layout.Keys.Count(k => IsConjunct(k.Shift)) +
                                _layout.Escape.Count(e => IsConjunct(e.To));

    private static bool IsConjunct(string? text) => text is { Length: > 1 };

    /// <summary>
    /// The virama of this script - what Link and Half insert. Taken from the
    /// block most of the palette lives in, not the first character in it: the
    /// danda and ॐ are shared across scripts and sort ahead of every Gujarati
    /// letter, which would otherwise pick Devanagari's virama for Gujarati.
    /// </summary>
    private string Virama
    {
        get
        {
            var blocks = new Dictionary<int, int>();
            foreach (string s in Palette)
                foreach (char c in s)
                    if (c is >= 'ऀ' and <= '෿')
                        blocks[c & 0xFF80] = blocks.GetValueOrDefault(c & 0xFF80) + 1;
            int block = blocks.Count == 0 ? 0x0900 : blocks.MaxBy(b => b.Value).Key;
            return ((char)(block + 0x4D)).ToString();
        }
    }

    // ---- commands ----

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand FormConjunctCommand { get; }
    public ICommand LinkCommand { get; }
    public ICommand HalfCommand { get; }
    public ICommand BackspaceCommand { get; }
    public ICommand BlankCommand { get; }
    public ICommand CancelConjunctCommand { get; }
    public ICommand ApplyConjunctCommand { get; }

    private string _status = string.Empty;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Put a character on the selected key, or add it to the conjunct being built.</summary>
    public void Choose(string character)
    {
        if (character.Length == 0) return;
        if (ConjunctMode) { AddToConjunct(character); return; }
        Assign(character);
    }

    private void AddToConjunct(string character)
    {
        if (Conjunct.Length >= MaxConjunctLength)
        {
            Status = $"A conjunct is at most {MaxConjunctLength} characters.";
            return;
        }
        Conjunct += character;
    }

    private void Assign(string text)
    {
        if (SelectedKey is null) { Status = "Click a key first."; return; }
        if (IsConjunct(text) && ConjunctCount >= MaxConjuncts && !IsConjunct(Current(SelectedKey)))
        {
            Status = $"This keyboard already has {MaxConjuncts} conjuncts.";
            return;
        }

        Set(SelectedKey, text);
        EndConjunct();
        RefreshKeyboard();
        Status = text.Length == 0
            ? $"{SelectedKey.Label} now types nothing on the {LayerName} layer."
            : $"{SelectedKey.Label} types {text} on the {LayerName} layer.";
        OnPropertyChanged(nameof(ConjunctCountText));
    }

    private void EndConjunct() => ConjunctMode = false;

    private string? Current(KeyCapViewModel cap) => Layer switch
    {
        KeyLayer.Escape => _layout.EscapeFor(KeyChar(cap)),
        KeyLayer.Shift => _layout.Find(cap.VirtualKey)?.Shift,
        _ => _layout.Find(cap.VirtualKey)?.Base,
    };

    private void Set(KeyCapViewModel cap, string text)
    {
        if (Layer == KeyLayer.Escape)
        {
            string key = KeyChar(cap);
            _layout.Escape.RemoveAll(e => e.Keys == key);
            _layout.Sequences.RemoveAll(s => s.Keys == EscapeLayer.Mark + key);
            if (text.Length > 0)
            {
                _layout.Escape.Add(new KeySequence { Keys = key, To = text });
                _layout.Sequences.Add(new KeySequence { Keys = EscapeLayer.Mark + key, To = text });
            }
        }
        else
        {
            var mapping = _layout.Find(cap.VirtualKey);
            if (mapping is null)
            {
                mapping = new KeyMapping { VirtualKey = cap.VirtualKey };
                _layout.Keys.Add(mapping);
            }
            if (Layer == KeyLayer.Shift) mapping.Shift = text.Length == 0 ? null : text;
            else mapping.Base = text.Length == 0 ? null : text;
        }
        _layout.Reindex();
    }

    /// <summary>The character this key types on a US keyboard, with the layer's shift state.</summary>
    private string KeyChar(KeyCapViewModel cap)
    {
        (char bas, char shifted) = UsKey(cap.VirtualKey);
        return (Layer == KeyLayer.Shift ? shifted : bas).ToString();
    }

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

    // ---- loading and saving ----

    private void StartNew()
    {
        _layout = new KeyboardLayout { Name = Name, FontId = SelectedLanguage?.Fonts.FirstOrDefault()?.Id };
        SelectedKey = null;
        EndConjunct();
        RefreshKeyboard();
        Status = "Blank keyboard. Click a key to assign it a character.";
        OnPropertyChanged(nameof(ConjunctCountText));
    }

    private void OpenSelected()
    {
        if (SelectedSource is null) return;
        try
        {
            var source = _bundle.GetLayout(SelectedSource);
            _layout = Copy(source);
            Name = source.Name + " (copy)";
            Status = $"Opened {source.Name}. Changes are saved as a new keyboard.";
        }
        catch (ConfigException ex)
        {
            Status = ex.Message;
            return;
        }
        SelectedKey = null;
        EndConjunct();
        RefreshKeyboard();
        OnPropertyChanged(nameof(ConjunctCountText));
    }

    private static KeyboardLayout Copy(KeyboardLayout source) => new()
    {
        Name = source.Name,
        FontId = source.FontId,
        Keys = source.Keys.Select(k => new KeyMapping
        {
            VirtualKey = k.VirtualKey, Base = k.Base, Shift = k.Shift, AltGr = k.AltGr, DeadKey = k.DeadKey,
        }).ToList(),
        Sequences = source.Sequences.Select(s => new KeySequence { Keys = s.Keys, To = s.To }).ToList(),
        Escape = source.Escape.Select(s => new KeySequence { Keys = s.Keys, To = s.To }).ToList(),
        Symbols = source.Symbols.Select(s => new KeySequence { Keys = s.Keys, To = s.To }).ToList(),
        DeadKeyCombinations = source.DeadKeyCombinations.Select(d => new DeadKeyCombination
        {
            Dead = d.Dead, Next = d.Next, Result = d.Result,
        }).ToList(),
    };

    private void Save()
    {
        if (SelectedLanguage is null) return;
        _layout.Name = Name.Trim();
        try
        {
            var reference = _store.Save(SelectedLanguage.Id, _layout);
            if (!SelectedLanguage.Layouts.Any(l => l.Id == reference.Id))
                SelectedLanguage.Layouts.Add(reference);
            if (!SourceLayouts.Any(l => l.Id == reference.Id)) SourceLayouts.Add(reference);
            Saved?.Invoke(SelectedLanguage.Id, reference);
            Status = $"Saved. '{reference.Name}' is now in the keyboard list.";
            _log.Info($"Keyboard saved: {reference.Id} -> {reference.File}");
        }
        catch (Exception ex)
        {
            Status = $"Could not save: {ex.Message}";
            _log.Warn($"Keyboard save failed: {ex.Message}");
        }
    }

    // ---- the grid and the palette ----

    private void OnLanguageChanged()
    {
        SourceLayouts.Clear();
        Palette.Clear();
        Specials.Clear();
        if (SelectedLanguage is null) return;

        foreach (var l in SelectedLanguage.Layouts) SourceLayouts.Add(l);

        // Everything this language's keyboards can type, which is the vendor's
        // "screen showing all the characters available in that language".
        var letters = new SortedSet<string>(StringComparer.Ordinal);
        var specials = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reference in SelectedLanguage.Layouts)
        {
            KeyboardLayout layout;
            try { layout = _bundle.GetLayout(reference); }
            catch (ConfigException) { continue; }

            foreach (var k in layout.Keys)
            {
                Add(k.Base);
                Add(k.Shift);
            }
            foreach (var s in layout.Sequences) Add(s.To);
            foreach (var s in layout.Symbols) Add(s.To);
        }

        foreach (string s in letters) Palette.Add(s);
        foreach (string s in specials) Specials.Add(s);
        SelectedSource = SourceLayouts.FirstOrDefault();
        StartNew();

        void Add(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Any(c => c is >= 'ऀ' and <= '෿')) letters.Add(text);
            else if (text.Length == 1 && text[0] > ' ') specials.Add(text);
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
        foreach (var cap in _allKeys)
        {
            string? text = Current(cap);
            cap.Display = string.IsNullOrEmpty(text) ? string.Empty : text;
            cap.IsMapped = !string.IsNullOrEmpty(text);
        }
        OnPropertyChanged(nameof(SelectedKeyLabel));
    }

    private void Refresh()
    {
        (NewCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LinkCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HalfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BackspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BlankCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyConjunctCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
