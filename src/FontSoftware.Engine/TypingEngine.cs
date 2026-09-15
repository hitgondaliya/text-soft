using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>Raised when the engine is toggled ON/OFF (drives the FR-3 notification).</summary>
public sealed class ToggleEventArgs : EventArgs
{
    public bool Enabled { get; init; }
    public string LanguageName { get; init; } = string.Empty;
    public string FontName { get; init; } = string.Empty;
}

/// <summary>
/// FR-3 system-wide typing engine. Sits on the low-level keyboard hook and,
/// when enabled, maps physical keys to the active layout's characters and
/// injects them into the focused app. Toggled by a configurable key
/// (default CapsLock).
///
/// Must be constructed on the WPF UI thread (the hook needs its message pump).
/// </summary>
public sealed class TypingEngine : IDisposable
{
    private readonly LowLevelKeyboardHook _hook;
    private readonly InputInjector _injector;
    private readonly IAppLog _log;

    private KeyboardLayout? _layout;
    private string _languageName = string.Empty;
    private string _fontName = string.Empty;

    // Pending dead-key output, combined with the next character.
    private string? _pendingDead;

    // The most recently injected cluster, so one Backspace can remove all of it
    // rather than a single UTF-16 unit. Null once consumed or invalidated.
    private string? _lastEmitted;

    // ---- Phonetic "type by sound" mode ----
    private Transliterator? _translit;
    private readonly StringBuilder _roman = new();   // roman letters typed for the current word
    private string _phoneticOut = string.Empty;      // script currently shown for that buffer

    // ---- Legacy Font Layout output (e.g. ShreeLipi-Ex) ----
    // When a legacy font layout is active, keys still map to Unicode, but we then
    // convert the current word Unicode→legacy bytes and inject THAT, so the text
    // shows correctly in the installed legacy font. Word-buffered because legacy
    // conversion reorders matras across the cluster.
    private FontConverter? _legacyConv;
    private readonly StringBuilder _uniWord = new(); // Unicode typed for the current word
    private string _legacyOut = string.Empty;        // legacy text currently shown for it

    /// <summary>When true (and a scheme is set), keys are transliterated by sound instead of the layout.</summary>
    public bool PhoneticMode { get; private set; }

    public bool Enabled { get; private set; }

    /// <summary>
    /// Licence gate, consulted on every key while enabled. Returns false when the
    /// licence is not active; the engine then switches itself off and lets the key
    /// through as plain English. Set by the app to the licence manager.
    /// </summary>
    public Func<bool>? Gate { get; set; }

    /// <summary>Virtual-key code of the toggle key. Default CapsLock (FR-6 config).</summary>
    public int ToggleKey { get; set; } = VK_CAPITAL;

    /// <summary>
    /// Process names (lower-case, no extension) where typing injection is
    /// suppressed — e.g. password managers, remote-desktop, games. Keys pass
    /// through untouched in these apps (FR-3 out-of-guaranteed-scope apps).
    /// </summary>
    public HashSet<string> ExcludedProcesses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ToggleEventArgs>? Toggled;

    public TypingEngine(LowLevelKeyboardHook hook, InputInjector injector, IAppLog? log = null)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        _log = log ?? NullLog.Instance;
        _hook.KeyEvent += OnKey;
    }

    /// <summary>Set the active typing profile (from FR-1 selection).</summary>
    public void SetProfile(KeyboardLayout layout, string languageName, string fontName)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _languageName = languageName;
        _fontName = fontName;
        _pendingDead = null;
        _lastEmitted = null;
        ResetPhonetic();
        ResetLegacy();
        _log.Info($"Profile set: {languageName} / {fontName} / layout '{layout.Name}'.");
    }

    /// <summary>Set the phonetic scheme for the active language (null disables it).</summary>
    public void SetTransliterator(Transliterator? translit)
    {
        _translit = translit;
        ResetPhonetic();
    }

    /// <summary>
    /// Set the live Unicode→legacy converter for the active Font Layout (null = type
    /// plain Unicode). When set, typed text is injected in the legacy encoding so it
    /// renders in that legacy font.
    /// </summary>
    public void SetLegacyConverter(FontConverter? converter, string? fontName = null)
    {
        _legacyConv = converter;
        LegacyFontName = converter is null ? null : fontName;
        ResetLegacy();
    }

    /// <summary>
    /// Installed font that draws the active legacy layout (e.g. "SHREE-GUJX-1120").
    /// When set, legacy text typed into a rich editor (see <see cref="RichTextApps"/>)
    /// is pasted as RTF in this font, so it shows correctly without the user
    /// picking the font by hand — the way Shree-Lipi does it.
    /// </summary>
    public string? LegacyFontName { get; private set; }

    /// <summary>
    /// Process names (lower-case, no extension) that take RTF from the clipboard and
    /// keep its font. Everything else (Notepad, browsers, chat apps) gets plain
    /// characters and shows them in whatever font it uses.
    /// </summary>
    public HashSet<string> RichTextApps { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "winword", "wordpad", "outlook", "powerpnt", "excel", "onenote", "onenoteim",
        "soffice", "soffice.bin", "swriter", "wps", "wpp", "et", "notepad++", "textmaker"
    };

    private void ResetLegacy()
    {
        _uniWord.Clear();
        _legacyOut = string.Empty;
    }

    /// <summary>Turn phonetic "type by sound" mode on/off.</summary>
    public void SetPhoneticMode(bool on)
    {
        PhoneticMode = on;
        ResetPhonetic();
    }

    private void ResetPhonetic()
    {
        _roman.Clear();
        _phoneticOut = string.Empty;
    }

    /// <summary>Force the engine on/off (e.g. from a UI button).</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled && Gate is { } gate && !gate())
            enabled = false; // no licence, no typing
        if (Enabled == enabled)
            return;
        Enabled = enabled;
        _pendingDead = null;
        _lastEmitted = null;
        ResetPhonetic();
        ResetLegacy();
        _log.Info($"Typing {(enabled ? "ON" : "OFF")}.");
        Toggled?.Invoke(this, new ToggleEventArgs
        {
            Enabled = Enabled,
            LanguageName = _languageName,
            FontName = _fontName
        });
    }

    private void OnKey(object? sender, HookKeyEventArgs e)
    {
        // Never react to our own injected input.
        if (e.Injected)
            return;

        // Toggle key: act on key-down, and always suppress so CapsLock/etc.
        // does not also perform its native action.
        if (e.VirtualKey == ToggleKey)
        {
            if (e.IsKeyDown)
                SetEnabled(!Enabled);
            e.Handled = true;
            return;
        }

        if (!Enabled)
            return; // OFF: normal English typing (FR-3)

        // Licence gone (expired, revoked, moved): stop, and do not consume the key.
        if (Gate is { } gate && !gate())
        {
            SetEnabled(false);
            return;
        }

        // Phonetic "type by sound" mode intercepts before the fixed-key layout.
        if (PhoneticMode && _translit is not null)
        {
            HandlePhonetic(e);
            return;
        }

        if (_layout is null)
            return;

        if (!e.IsKeyDown)
            return; // map on key-down only

        // Backspace removes the whole last injected cluster (e.g. a consonant +
        // matra pair) instead of a single code unit.
        if (e.VirtualKey == VK_BACK)
        {
            HandleBackspace(e);
            return;
        }

        // Let modifier keys pass through untouched.
        if (IsModifier(e.VirtualKey))
            return;

        var mapping = _layout.Find(e.VirtualKey);
        if (mapping is null)
        {
            _lastEmitted = null; // navigation/space/etc. breaks the cluster chain
            ResetLegacy();       // and commits the current legacy word
            return;              // unmapped key → default English behaviour
        }

        // Ctrl+A, Ctrl+S, Alt+F, Win+E and the rest are commands, not text. Without
        // this the layout would swallow them and type a letter instead, so no
        // shortcut worked while typing was on.
        if (IsShortcut(mapping))
        {
            _pendingDead = null;
            _lastEmitted = null;
            ResetLegacy(); // the shortcut may move the caret; start the next word fresh
            return;
        }

        string? output = SelectOutput(mapping);
        if (string.IsNullOrEmpty(output))
        {
            _lastEmitted = null;
            return; // no glyph for this modifier combo → pass through
        }

        // Respect per-app exclusions only when we would otherwise inject.
        if (IsForegroundExcluded())
        {
            _pendingDead = null;
            _lastEmitted = null;
            ResetLegacy();
            return; // don't suppress — let the app receive the raw key
        }

        // Suppress the original key and inject the mapped character(s).
        e.Handled = true;

        if (mapping.DeadKey)
        {
            _pendingDead = output; // hold; combine with the next character
            return;
        }

        if (_pendingDead is not null)
        {
            // Prefer a layout-defined combination; otherwise emit them in order.
            output = _layout.CombineDeadKey(_pendingDead, output) ?? (_pendingDead + output);
            _pendingDead = null;
        }

        // Legacy Font Layout: buffer the Unicode word, convert to the legacy
        // encoding, and inject that so it renders in the legacy font.
        if (_legacyConv is not null)
        {
            _uniWord.Append(output);
            RerenderLegacy();
            _lastEmitted = null; // the legacy path owns backspacing for this word
            return;
        }

        _injector.SendText(output);
        _lastEmitted = output;
    }

    // Replace the legacy text shown for the current word with the conversion of the
    // Unicode buffer. Backspaces are UTF-16 units — legacy output is byte-range
    // characters, one unit each, so this is reliable in standard edit controls.
    private void RerenderLegacy()
    {
        string next = _legacyConv!.ConvertText(_uniWord.ToString());
        string? app = LegacyFontName is null ? null : ForegroundApp.GetProcessName();

        // Word: talk to it directly — a paste there may lose the font (Word's own
        // paste options), the object model never does.
        if (app == "winword" && WordAutomation.TryType(next, _legacyOut.Length, LegacyFontName!))
        {
            _legacyOut = next;
            return;
        }

        if (_legacyOut.Length > 0)
            _injector.SendBackspace(_legacyOut.Length);
        if (next.Length > 0)
        {
            if (app is not null && RichTextApps.Contains(app))
                _injector.SendRichText(next, LegacyFontName!);
            else
                _injector.SendText(next);
        }
        _legacyOut = next;
    }

    private void HandleBackspace(HookKeyEventArgs e)
    {
        // Legacy Font Layout: shrink the Unicode word buffer and re-convert.
        if (_legacyConv is not null)
        {
            if (_uniWord.Length > 0)
            {
                e.Handled = true;
                _uniWord.Length--;
                RerenderLegacy();
                return;
            }
            // Nothing buffered → the word is already committed; let Backspace act
            // normally on the document from here.
            _legacyOut = string.Empty;
        }

        // A pending dead key is cancelled by backspace before anything was emitted.
        if (_pendingDead is not null)
        {
            _pendingDead = null;
            e.Handled = true;
            return;
        }

        // Multi-unit cluster: suppress the single backspace and delete the lot.
        if (_lastEmitted is { Length: > 1 })
        {
            e.Handled = true;
            _injector.SendBackspace(_lastEmitted.Length);
            _lastEmitted = null;
            return;
        }

        // Single unit or nothing tracked: let the normal backspace through.
        _lastEmitted = null;
    }

    /// <summary>
    /// Phonetic mode: buffer roman letters for the current word and re-render the
    /// script live. Backspace shrinks the buffer; any non-letter (space, punctuation,
    /// digit, navigation) commits the word and passes through untouched.
    /// </summary>
    private void HandlePhonetic(HookKeyEventArgs e)
    {
        // Backspace: shrink the pending roman buffer and re-render.
        if (e.VirtualKey == VK_BACK)
        {
            if (_roman.Length == 0)
            {
                ResetPhonetic();
                return; // nothing pending → let Backspace act normally
            }
            e.Handled = true; // suppress down and up while we own the buffer
            if (!e.IsKeyDown)
                return;
            _roman.Length--;
            Rerender();
            return;
        }

        // Let modifier keys through so Shift selects capitals (T, N, A, M, …).
        if (IsModifier(e.VirtualKey))
            return;

        // A shortcut ends the word and belongs to the application, not to us.
        if (IsShortcut(null))
        {
            ResetPhonetic();
            return;
        }

        // A–Z are the roman letters we buffer; anything else commits the word.
        if (TryRomanChar(e.VirtualKey, out char ch))
        {
            e.Handled = true; // suppress the raw Latin letter (down and up)
            if (!e.IsKeyDown)
                return;
            _roman.Append(ch);
            Rerender();
            return;
        }

        // Boundary key (space, enter, punctuation, digit, arrows): commit and pass through.
        ResetPhonetic();
    }

    // Replace the previously shown script for this word with the transliteration of
    // the current buffer. Backspaces are UTF-16 units — reliable in standard edit
    // controls (Notepad, WordPad, browsers); a few apps delete whole clusters.
    private void Rerender()
    {
        string next = _translit!.Convert(_roman.ToString());
        if (_phoneticOut.Length > 0)
            _injector.SendBackspace(_phoneticOut.Length);
        if (next.Length > 0)
            _injector.SendText(next);
        _phoneticOut = next;
    }

    private bool TryRomanChar(int vk, out char ch)
    {
        if (vk >= 'A' && vk <= 'Z')
        {
            bool shift = IsDown(VK_SHIFT);
            bool caps = IsToggled(VK_CAPITAL) && ToggleKey != VK_CAPITAL;
            bool upper = shift ^ caps;
            ch = upper ? (char)vk : char.ToLowerInvariant((char)vk);
            return true;
        }
        ch = '\0';
        return false;
    }

    private bool IsForegroundExcluded()
    {
        if (ExcludedProcesses.Count == 0)
            return false;
        string? name = ForegroundApp.GetProcessName();
        return name is not null && ExcludedProcesses.Contains(name);
    }

    private string? SelectOutput(KeyMapping mapping)
    {
        bool shift = IsDown(VK_SHIFT);
        bool caps = IsToggled(VK_CAPITAL) && ToggleKey != VK_CAPITAL;
        bool altGr = IsDown(VK_CONTROL) && IsDown(VK_MENU); // Ctrl+Alt = AltGr

        if (altGr && !string.IsNullOrEmpty(mapping.AltGr))
            return mapping.AltGr;

        // Shift XOR CapsLock selects the shifted glyph (letter-style behaviour).
        bool useShift = shift ^ caps;
        if (useShift && !string.IsNullOrEmpty(mapping.Shift))
            return mapping.Shift;

        return mapping.Base;
    }

    /// <summary>
    /// True when the key press is an application shortcut rather than text, so the
    /// engine must leave it alone: Ctrl+key, Alt+key and Win+key.
    /// Ctrl+Alt is AltGr on European keyboards, so that pair counts as text only
    /// when the layout actually defines an AltGr glyph for the key.
    /// </summary>
    private static bool IsShortcut(KeyMapping? mapping)
    {
        if (IsDown(VK_LWIN) || IsDown(VK_RWIN))
            return true;

        bool ctrl = IsDown(VK_CONTROL), alt = IsDown(VK_MENU);
        if (ctrl && alt)
            return string.IsNullOrEmpty(mapping?.AltGr);
        return ctrl || alt;
    }

    private static bool IsModifier(int vk) =>
        vk is VK_SHIFT or VK_CONTROL or VK_MENU
            or 0xA0 or 0xA1  // L/R Shift
            or 0xA2 or 0xA3  // L/R Ctrl
            or 0xA4 or 0xA5; // L/R Alt

    /// <summary>
    /// Reads whether a key is physically held down. Replaceable so the Shift and
    /// shortcut behaviour can be tested without a real keyboard; production code
    /// never changes it.
    /// </summary>
    public static Func<int, bool> KeyDownProbe { get; set; } = vk => (GetKeyState(vk) & 0x8000) != 0;

    private static bool IsDown(int vk) => KeyDownProbe(vk);
    private static bool IsToggled(int vk) => (GetKeyState(vk) & 0x0001) != 0;

    public void Dispose()
    {
        _hook.KeyEvent -= OnKey;
    }
}
