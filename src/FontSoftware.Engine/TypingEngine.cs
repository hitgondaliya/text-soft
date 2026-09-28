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

    // ---- Multi-key runs (see KeySequence) ----
    // Vendor keyboards map runs of keys, not single keys: on ITR Gujarati "y" is
    // અ and "yt" is આ, replacing it. These hold the keys typed for the run in
    // progress and the text they produced, so the next key can replace it.
    private readonly StringBuilder _seqKeys = new();
    private string _seqOut = string.Empty;
    private int _seqMatched;   // how much of _seqKeys the text in _seqOut accounts for (Eng keyboard escape runs)

    // Windows' own key state is the truth about Ctrl: the injector lifts a held
    // Ctrl only for the length of what it types, and puts it back down in the
    // same breath (InputInjector.WithCtrlLifted), so it never reads "up" while
    // the finger is still on it.
    private static bool CtrlDown => IsDown(VK_CONTROL);

    // The key a run on the layer took while Ctrl was held. Held past the repeat
    // delay it repeats, and each repeat arrives with Ctrl still down - as Ctrl+V,
    // which pasted the clipboard thirty times a second. Its repeats are swallowed
    // until it comes up.
    private int _layerKey;

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

    // The Eng keyboard IS type-by-sound - that is what the vendor’s chart describes -
    // so choosing it turns transliteration on whether or not the toggle is set.
    private bool _layoutIsPhonetic;

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
        _layoutIsPhonetic = layout.Phonetic;
        _languageName = languageName;
        _fontName = fontName;
        _pendingDead = null;
        _lastEmitted = null;
        ResetPhonetic();
        ResetSequence();
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

    /// <summary>
    /// The application is about to receive this keystroke itself - a space, a
    /// punctuation mark, a shortcut. Anything we have typed for the word must be
    /// on screen before it lands, or the space could arrive between the last two
    /// letters. Only word boundaries wait like this, never a letter.
    /// </summary>
    private void FlushBeforeKey() => _injector.Drain(200);

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
        // In a legacy font the word being typed by sound is also the legacy word
        // on screen; once it is finished it is no longer ours to rewrite.
        if (PhoneticMode || _layoutIsPhonetic) ResetLegacy();
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
        ResetSequence();
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
        if (!e.Injected && !e.IsKeyDown && e.VirtualKey == _layerKey)
            _layerKey = 0;

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

        // Phonetic "type by sound" mode intercepts before the fixed-key layout -
        // except for Ctrl+Space and Ctrl+Shift+1. Those open the same extra layers
        // on the Eng keyboard as on every other (Shree's Eng chart has 24 keys on
        // them in Gujarati alone), and what is on them is fixed keys, not sounds.
        // Treating the escape as a shortcut, as this used to, sent Ctrl+Space on to
        // the application and the key after it typed by sound.
        if ((PhoneticMode || _layoutIsPhonetic) && _translit is not null)
        {
            if (_seqKeys.Length > 0)
            {
                // A run on the layer is in progress. Key-ups belong to it too: one
                // reaching the sound-typing code would end the word there, and with
                // it the legacy text the run is still rewriting.
                if (!e.IsKeyDown) return;
                if (ContinuePhoneticEscape(e)) return;
            }
            bool opensLayer = e.IsKeyDown && _layout is { HasSequences: true } && EscapeMarkFor(e.VirtualKey) != '\0';
            if (!opensLayer)
            {
                HandlePhonetic(e);
                return;
            }
            // The word typed so far stays as it is on screen - unless this escape
            // is the second of a run (ØmØma), whose text is still being rewritten.
            if (_seqKeys.Length == 0) ResetPhonetic();
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

        // Ctrl+Space reaches this keyboard's own third layer - the nukta letters,
        // the conjuncts, the alternates the two printed layers have no room for -
        // and Ctrl+Shift+1 the symbols every keyboard of the script shares (ॐ, the
        // danda, the rupee sign). The vendor charts write both inside the run of
        // keys like any other keystroke ("Øj", "kØqk"), so that is what they are
        // here: a mark appended to the run, which puts nothing on screen by itself.
        if (EscapeMarkFor(e.VirtualKey) is var mark and not '\0')
        {
            e.Handled = true;
            _pendingDead = null;
            _lastEmitted = null;
            // Pressed twice, the escape takes itself back: on Shree, Ctrl+Space
            // Ctrl+Space v types the plain v.
            if (_seqKeys.Length > 0 && _seqKeys[^1] == mark)
            {
                _seqKeys.Length--;
                if (_seqKeys.Length == 0) _seqOut = string.Empty;
                _log.Info($"Escape: {(mark == EscapeLayer.Mark ? "Ctrl+Space" : "Ctrl+Shift+1")} pressed again - taken back.");
                return;
            }
            if (_layout.HasSequences) _seqKeys.Append(mark);
            else ResetLegacy();
            // An escape puts nothing on screen, so when one does not work there is
            // nothing to look at. Write down that it was seen and what it opened.
            _log.Info($"Escape: {(mark == EscapeLayer.Mark ? "Ctrl+Space" : "Ctrl+Shift+1")} on '{_layout.Name}' " +
                      $"({_layout.Escape.Count} keys on its own layer, {_layout.Symbols.Count} on the shared one); waiting on '{_seqKeys}'.");
            return;
        }

        var mapping = _layout.Find(e.VirtualKey);
        char typed = KeyChar(e.VirtualKey);
        if (_seqKeys.Length > 0 && (_seqKeys[^1] == EscapeLayer.Mark || _seqKeys[^1] == EscapeLayer.SymbolMark))
            _log.Info($"Escape: then '{typed}' -> {(_layout.MatchSequence(_seqKeys.ToString() + typed) is { } t ? "'" + t + "'" : "nothing on that layer")}.");

        // Ctrl+A, Ctrl+S, Alt+F, Win+E and the rest are commands, not text. Without
        // this the layout would swallow them and type a letter instead, so no
        // shortcut worked while typing was on.
        //
        // Except straight after an escape. Ctrl+Space is a chord, and a typist
        // who holds Ctrl down through the next key - Ctrl, Space, V, let go - means
        // the V on that layer: Shree types it that way, all three ways of pressing
        // it. Here that V used to arrive as Ctrl+V, and Windows pasted whatever the
        // typist had last copied into the document instead of the letter.
        bool looksLikeShortcut = IsShortcut(mapping);
        if (looksLikeShortcut && !OnEscapedLayer(typed))
        {
            if (RepeatOfLayerKey(e)) return;
            if (_seqKeys.Length > 0)
                _log.Info($"Escape: the run '{_seqKeys}' was dropped - '{typed}' arrived looking like a shortcut " +
                          $"(ctrl={CtrlDown}, alt={IsDown(VK_MENU)}).");
            _pendingDead = null;
            _lastEmitted = null;
            ResetSequence();
            ResetLegacy(); // the shortcut may move the caret; start the next word fresh
            FlushBeforeKey();
            return;
        }
        if (looksLikeShortcut) _layerKey = e.VirtualKey;

        // A key with no glyph of its own can still finish a run: on the ITR Gujarati
        // keyboard "o" types nothing alone, but "fo" is ર્ક. So the run is resolved
        // before an unmapped key is handed back to the application.
        string output = mapping is null ? string.Empty : SelectOutput(mapping) ?? string.Empty;
        int replace = 0;
        if (_layout.HasSequences)
            output = ResolveSequence(typed, output, out replace);

        if (string.IsNullOrEmpty(output) && replace == 0)
        {
            _lastEmitted = null;  // navigation/space/punctuation breaks the chain
            ResetSequence();      // and ends the run being typed
            ResetLegacy();        // and commits the current legacy word

            // A letter key the active keyboard does not use types nothing on the
            // vendor's software, and nothing here: an English letter dropped into
            // Indic text is never what the typist meant. Everything else - digits,
            // punctuation, space, the navigation keys - still passes through.
            if (e.VirtualKey is >= 'A' and <= 'Z')
                e.Handled = true;
            else
                FlushBeforeKey();
            return;
        }

        // Respect per-app exclusions only when we would otherwise inject.
        if (IsForegroundExcluded())
        {
            _pendingDead = null;
            _lastEmitted = null;
            ResetSequence();
            ResetLegacy();
            FlushBeforeKey();
            return; // don't suppress — let the app receive the raw key
        }

        // Suppress the original key and inject the mapped character(s).
        e.Handled = true;

        if (mapping is { DeadKey: true })
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

        Emit(output, replace);
    }

    /// <summary>
    /// Put <paramref name="output"/> on screen, first removing
    /// <paramref name="replace"/> characters this layout had produced for the run
    /// in progress.
    /// </summary>
    private void Emit(string output, int replace)
    {
        // Legacy Font Layout: buffer the Unicode word, convert to the legacy
        // encoding, and inject that so it renders in the legacy font.
        if (_legacyConv is not null)
        {
            if (replace > 0)
                _uniWord.Length = Math.Max(0, _uniWord.Length - replace);
            _uniWord.Append(output);
            RerenderLegacy();
            _lastEmitted = null; // the legacy path owns backspacing for this word
            return;
        }

        // Replacing the run in progress: only the part that actually changed needs
        // to go. "k" then "i" on a phonetic keyboard shows क and then कि - one more
        // character, not both of them again.
        if (replace > 0)
        {
            string shown = _lastEmitted is not null && _lastEmitted.Length == replace ? _lastEmitted : string.Empty;
            int keep = CommonPrefix(shown, output);
            if (replace > keep)
                _injector.SendBackspace(replace - keep);
            if (output.Length > keep)
                _injector.SendText(output[keep..]);
        }
        else if (output.Length > 0)
        {
            _injector.SendText(output);
        }
        _lastEmitted = output;
    }

    /// <summary>
    /// Fold this keystroke into the run being typed. While the keys so far can
    /// still grow into a longer run the engine keeps composing, replacing what it
    /// showed before; when they cannot, the run ends and a new one starts with
    /// this key.
    /// </summary>
    private string ResolveSequence(char typed, string single, out int replace)
    {
        string grown = _seqKeys.ToString() + typed;
        if (typed != '\0' && (_layout!.SequenceCanGrow(grown) || _layout.MatchSequence(grown) is not null))
        {
            string text = _layout.MatchSequence(grown) ?? _seqOut + single;
            replace = _seqOut.Length;
            _seqKeys.Clear();
            _seqKeys.Append(grown);
            _seqOut = text;
            return text;
        }

        replace = 0;

        // An escape always starts a run of its own when the keys before it lead
        // nowhere: "f" then Ctrl+Space then "u" is क followed by श्र, not the matra
        // that "u" types on its own.
        int mark = Math.Max(grown.LastIndexOf(EscapeLayer.Mark),
                            grown.LastIndexOf(EscapeLayer.SymbolMark));
        if (mark > 0 && typed != '\0')
        {
            string tail = grown[mark..];
            string? escaped = _layout!.MatchSequence(tail);
            if (escaped is not null || _layout.SequenceCanGrow(tail))
            {
                _seqKeys.Clear();
                _seqKeys.Append(tail);
                _seqOut = escaped ?? single;   // show the plain key until the run finishes
                return _seqOut;
            }
        }

        _seqKeys.Clear();
        if (typed != '\0') _seqKeys.Append(typed);
        _seqOut = (typed == '\0' ? null : _layout!.MatchSequence(typed.ToString())) ?? single;
        return _seqOut;
    }

    private void ResetSequence()
    {
        _seqKeys.Clear();
        _seqOut = string.Empty;
        _seqMatched = 0;
    }

    /// <summary>
    /// The escape: Ctrl+Space, which is what the product's own settings name, and
    /// Ctrl+Shift+1, which is how the published key charts write it.
    /// </summary>
    private static char EscapeMarkFor(int vk)
    {
        if (!CtrlDown || IsDown(VK_MENU)) return '\0';
        if (vk == VK_SPACE) return EscapeLayer.Mark;               // this keyboard's third layer
        if (vk == '1' && IsDown(VK_SHIFT)) return EscapeLayer.SymbolMark;  // the shared symbols
        return '\0';
    }

    /// <summary>
    /// Is this key part of a run an escape opened? Only while the run has an
    /// escape in it and this key continues it - Ctrl+Space j f k on the Roma
    /// keyboard is one run, typed by some with Ctrl held all the way - and only
    /// when the other keys held are the Ctrl (and Shift) of the escape itself: Alt
    /// or the Windows key still make it a shortcut.
    /// </summary>
    private bool OnEscapedLayer(char typed)
    {
        if (typed == '\0' || _layout is null || _seqKeys.Length == 0) return false;
        string run = _seqKeys.ToString();
        if (run.IndexOf(EscapeLayer.Mark) < 0 && run.IndexOf(EscapeLayer.SymbolMark) < 0) return false;
        if (IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN)) return false;
        string grown = run + typed;
        if (_layout.MatchSequence(grown) is not null || _layout.SequenceCanGrow(grown)) return true;
        // An escape starts a run of its own when the keys before it lead nowhere
        // (see ResolveSequence): after ૠ, Ctrl+Space v is ૠ again.
        int mark = Math.Max(grown.LastIndexOf(EscapeLayer.Mark), grown.LastIndexOf(EscapeLayer.SymbolMark));
        string tail = grown[mark..];
        return mark > 0 && (_layout.MatchSequence(tail) is not null || _layout.SequenceCanGrow(tail));
    }

    /// <summary>
    /// A key-down for the key a layer run took with Ctrl held, before that key has
    /// come up: its auto-repeat. Swallowed - it is not the typist asking for Ctrl+V.
    /// </summary>
    private bool RepeatOfLayerKey(HookKeyEventArgs e)
    {
        if (e.VirtualKey != _layerKey) return false;
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// The Eng keyboard after Ctrl+Space: keys go to the chart's fixed runs on that
    /// layer ("Øn" ઙ્, "Ønga" ઙ) for as long as they keep making one, and the first
    /// key that does not is typed by sound again. True when this key was taken.
    /// </summary>
    private bool ContinuePhoneticEscape(HookKeyEventArgs e)
    {
        if (!e.IsKeyDown || IsModifier(e.VirtualKey)) return false;
        if (EscapeMarkFor(e.VirtualKey) != '\0') return false;    // a second escape: the usual handling

        char typed = KeyChar(e.VirtualKey);
        string grown = _seqKeys.ToString() + typed;
        string? hit = typed == '\0' ? null : _layout!.MatchSequence(grown);
        bool grows = typed != '\0' && _layout!.SequenceCanGrow(grown);
        if (hit is null && !grows)
        {
            if (_seqKeys[^1] == EscapeLayer.Mark || _seqKeys[^1] == EscapeLayer.SymbolMark)
                _log.Info($"Escape: then '{typed}' -> nothing on that layer.");
            // Keys that only looked like the start of a longer run showed nothing
            // while it might come: Ctrl+Space n g could still become ઙ્ગ. It did not,
            // so they are letters after all - typed by sound, ahead of this key.
            string pending = _seqKeys.ToString()[_seqMatched..];
            ResetSequence();
            ResetLegacy();
            foreach (char c in pending)
                if (char.IsAsciiLetter(c)) _roman.Append(c);
            if (_roman.Length > 0) Rerender();
            return false;                                          // typed by sound
        }

        if (IsForegroundExcluded()) { ResetSequence(); return false; }

        e.Handled = true;
        if (CtrlDown) _layerKey = e.VirtualKey;
        string text = hit ?? _seqOut;          // until the run finishes, keep what it shows
        int replace = _seqOut.Length;
        _log.Info($"Escape: then '{typed}' -> '{text}'.");
        _seqKeys.Clear();
        _seqKeys.Append(grown);
        if (hit is not null) _seqMatched = _seqKeys.Length;
        _seqOut = text;
        Emit(text, replace);
        if (!grows)
        {
            ResetSequence();                   // the run is complete
            ResetLegacy();
        }
        return true;
    }

    /// <summary>
    /// The character a US keyboard puts on this key, with the current Shift state -
    /// the notation the vendor charts use for their runs ("Y" means Shift+y).
    /// </summary>
    private char KeyChar(int vk)
    {
        // CapsLock only counts when it is not the toggle key, exactly as it does
        // when the single-key glyph is chosen.
        bool caps = IsToggled(VK_CAPITAL) && ToggleKey != VK_CAPITAL && vk is >= 'A' and <= 'Z';
        bool shift = IsDown(VK_SHIFT) ^ caps;
        if (vk is >= 'A' and <= 'Z') return shift ? (char)vk : char.ToLowerInvariant((char)vk);
        if (vk is >= '0' and <= '9') return shift ? ")!@#$%^&*("[vk - '0'] : (char)vk;
        return vk switch
        {
            186 => shift ? ':' : ';',
            187 => shift ? '+' : '=',
            188 => shift ? '<' : ',',
            189 => shift ? '_' : '-',
            190 => shift ? '>' : '.',
            191 => shift ? '?' : '/',
            192 => shift ? '~' : '`',
            219 => shift ? '{' : '[',
            220 => shift ? '|' : (char)92,
            221 => shift ? '}' : ']',
            222 => shift ? '"' : (char)39,
            _ => '\0',
        };
    }

    // Replace the legacy text shown for the current word with the conversion of the
    // Unicode buffer. Backspaces are UTF-16 units — legacy output is byte-range
    // characters, one unit each, so this is reliable in standard edit controls.
    private void RerenderLegacy()
    {
        string next = _legacyConv!.ConvertText(_uniWord.ToString());
        string shown = _legacyOut;
        if (string.Equals(next, shown, StringComparison.Ordinal))
            return;                        // the keystroke changed nothing on screen

        // One keystroke usually only adds to the end of the word, even though the
        // whole word has to be re-converted to find that out: the legacy byte order
        // of what came before can change when a matra arrives. So send the
        // difference, not the word. Erasing and retyping the whole word on every
        // key is what made the text visibly redraw itself, and on a slow machine
        // what made it lag: a ten-letter word cost twenty injected events per key.
        _legacyOut = next;
        int keep = CommonPrefix(shown, next);
        int remove = shown.Length - keep;
        string add = next[keep..];

        string? app = LegacyFontName is null ? null : ForegroundApp.GetProcessName();
        bool rich = app is not null && RichTextApps.Contains(app);

        _injector.Run(() =>
        {
            // Word: talk to it directly — a paste there may lose the font (Word's own
            // paste options), the object model never does.
            bool wordRefusedUs = false;
            if (app == "winword")
            {
                if (WordAutomation.TryType(add, remove, LegacyFontName!))
                    return;
                wordRefusedUs = true;
                _log.Info($"Word: typing into it directly did not happen - {WordAutomation.LastRefusal}; " +
                          "typing the characters instead, which means they take the font of the text around them.");
            }

            if (remove > 0)
                _injector.SendBackspace(remove);
            if (add.Length == 0)
                return;

            // The font has to travel with the text only when nothing of this word
            // is left in front of the caret for it to be inherited from. That is
            // the first keystroke of a word, and any later one that rewrites the
            // word from its start; the rest type on in the font already there,
            // which spares a clipboard round trip per key in a word processor.
            //
            // Not for Word, though. Carrying the font means putting the text on the
            // clipboard and pressing Ctrl+V, and Word is slow enough to start that
            // the typist's own last copy can win the race and land in the document
            // instead. A letter in the wrong font is a nuisance; somebody else's
            // paragraph pasted into their work is not. When Word will not talk to
            // us, the characters are typed and the font is the typist's to set.
            if (rich && keep == 0 && !wordRefusedUs)
                _injector.SendRichText(add, LegacyFontName!);
            else
                _injector.SendText(add);
        });
    }

    /// <summary>How much of the start of the two strings is the same.</summary>
    private static int CommonPrefix(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length), i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
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
            if (!e.IsKeyDown) return;
            if (RepeatOfLayerKey(e)) return;
            ResetPhonetic();
            FlushBeforeKey();
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

        // Boundary key (space, enter, punctuation, digit, arrows): commit and pass
        // through - once what we typed for the word is on screen. In Word a legacy
        // word goes in through its automation object, slowly enough that a space
        // let through at once could land between the last two letters.
        if (!e.IsKeyDown) return;
        ResetPhonetic();
        FlushBeforeKey();
    }

    // Replace the previously shown script for this word with the transliteration of
    // the current buffer. Backspaces are UTF-16 units — reliable in standard edit
    // controls (Notepad, WordPad, browsers); a few apps delete whole clusters.
    private void Rerender()
    {
        string next = _translit!.Convert(_roman.ToString());

        // A legacy font draws its own encoding, not Unicode: the word goes through
        // the same conversion the fixed keyboards use. Without this the Eng
        // keyboard typed Unicode into a Shree font and every letter came out wrong.
        if (_legacyConv is not null)
        {
            _uniWord.Clear();
            _uniWord.Append(next);
            RerenderLegacy();
            _phoneticOut = next;
            return;
        }

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
        bool altGr = CtrlDown && IsDown(VK_MENU); // Ctrl+Alt = AltGr

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

        bool ctrl = CtrlDown, alt = IsDown(VK_MENU);
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
    public static Func<int, bool> KeyDownProbe { get; set; } = vk => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsDown(int vk) => KeyDownProbe(vk);
    private static bool IsToggled(int vk) => (GetKeyState(vk) & 0x0001) != 0;

    public void Dispose()
    {
        _hook.KeyEvent -= OnKey;
        // The injection thread may still have the last keystroke in hand.
        _injector.Drain();
    }
}
