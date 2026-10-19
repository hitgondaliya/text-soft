using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Keyboards;
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

    // Ctrl as the finger has it. Before typing a key of a Ctrl+Space run that Ctrl
    // is still held through, the injector lets go of it and never presses it back
    // (a key we press can stay stuck; see InputInjector.LiftHeldCtrl), so from then on Windows reads it
    // as up while the finger is still on it - and a second Ctrl+Space on the same
    // hold, which Shree takes, went through as a plain space. The hook sees our
    // lift (its own tag) and, if Windows still had that Ctrl down at that point in
    // the input, the finger was on it: it counts as held until any real Ctrl event
    // comes along. A Ctrl-up the hook never sees (let go over an elevated window)
    // is covered by a time limit longer than anyone holds a chord.
    private bool _ctrlLifted;
    private long _ctrlLiftedAt;
    private const long CtrlLiftLimitMs = 8000;

    private bool CtrlDown => IsDown(VK_CONTROL) || CtrlOnlyByLift;

    /// <summary>Ctrl is held by the finger, but Windows reads it as up because we lifted it.</summary>
    private bool CtrlOnlyByLift =>
        _ctrlLifted && !IsDown(VK_CONTROL) && Environment.TickCount64 - _ctrlLiftedAt < CtrlLiftLimitMs;

    // The key a run on the layer took while Ctrl was held. Held past the repeat
    // delay it repeats, and each repeat arrives with Ctrl still down - as Ctrl+V,
    // which pasted the clipboard thirty times a second. Its repeats are swallowed
    // until it comes up.
    private int _layerKey;

    // The escape key itself (Space, or the 1 of Ctrl+Shift+1) while it is down.
    // Held past the repeat delay it repeats too, and a repeat looked like the
    // second press that takes an escape back - the layer closed, and the V after
    // it went out as Ctrl+V.
    private int _escapeKey;

    // ---- Legacy Font Layout output (e.g. ShreeLipi-Ex) ----
    // When a legacy font layout is active, keys still map to Unicode, but we then
    // convert the current word Unicode→legacy bytes and inject THAT, so the text
    // shows correctly in the installed legacy font. Word-buffered because legacy
    // conversion reorders matras across the cluster.
    private FontConverter? _legacyConv;
    private readonly StringBuilder _uniWord = new(); // Unicode typed for the current word
    private string _legacyOut = string.Empty;        // legacy text currently shown for it

    // The word as it stood before each keystroke of it, newest last - so Backspace
    // takes back exactly the last keystroke: a whole one (Shift+A's બ્, not only its
    // virama, which left the full બ behind), and only that one inside a chart run
    // (k then s is કે on InScript; Backspace leaves the ક, and the run the k began,
    // where taking the run's single piece used to delete both keys). Each state is
    // the whole engine as it stood: the run in progress, the word in every path's units
    // (_raw on a keyboard typed out of Unicode's order, _uniWord/_legacyOut in a legacy
    // font, _plainWord otherwise, _roman typed by sound), and what was on screen.
    // A key that types nothing is a keystroke too, and Backspace takes it back.
    private sealed record WordState(string SeqKeys, string SeqOut, int SeqMatched, string UniWord, string LegacyOut,
        string Raw, string Word, string PlainWord, string? LastEmitted, string Roman, string PhoneticOut, string Done);
    private readonly Stack<WordState> _undo = new();

    // What the keystrokes since the caret last moved have put on screen for words already
    // finished by a key the engine typed itself: a mark typed through a legacy font's table
    // ('"' is 22 22), a fixed key or a run on the layer of the Eng keyboard, the word typed
    // by sound before it. Backspace goes back into them a keystroke at a time, as inside a
    // word; a key the application gets itself, a shortcut or a click ends them.
    private string _done = string.Empty;

    // The state a keystroke the Eng keyboard's layer let go of began in (see ContinuePhoneticEscape).
    private WordState? _carried;

    // What the word in progress has put on screen, in plain Unicode typing (no
    // legacy font, no typing order) - the state Backspace restores is compared with it.
    private readonly StringBuilder _plainWord = new();

    /// <summary>
    /// How many mouse clicks there have been (see MouseClickWatch). A click can move
    /// the caret without a key, so when the count has moved since the last key the
    /// word in progress is forgotten - what Backspace would take back whole, the run
    /// a key would rewrite - rather than applied wherever the caret now is.
    /// </summary>
    public Func<long> Clicks { get; set; } = () => 0;
    private long _clicksSeen;

    // ---- Typing order (see TypingOrder) ----
    // A typewriter keyboard strikes the i-sign before its letter and the reph after
    // it. On such a keyboard the word is kept as typed, with those two still marked,
    // and what goes on screen is that word put into Unicode's order.
    private TypingOrder _order = TypingOrder.None;
    private readonly StringBuilder _raw = new();     // the word as typed
    private string _word = string.Empty;             // the word in Unicode's order, as shown

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
        _order = layout.Order;
        _languageName = languageName;
        _fontName = fontName;
        _pendingDead = null;
        _lastEmitted = null;
        ResetPhonetic();
        ResetSequence();
        ResetLegacy();
        _log.Info($"Profile set: {languageName} / {fontName} / layout '{layout.Name}'.");
        if (_order.Active) _log.Info($"Typing order of '{layout.Name}': {_order}.");
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
        "winword", "wordpad", "outlook", "powerpnt", "onenote", "onenoteim",
        "soffice", "soffice.bin", "swriter", "wps", "wpp", "notepad++", "textmaker"
    };
    // Not Excel, WPS Spreadsheets or LibreOffice Calc: a paste into a cell that is
    // not being edited becomes the whole cell, and the next letter - typed, to go
    // on in the same font - starts an edit that replaces it. The first letter of
    // every entry was lost. In a spreadsheet the characters are typed, and take
    // the cell's font.

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
        _raw.Clear();
        _word = string.Empty;
        _plainWord.Clear();
        _undo.Clear();
        _done = string.Empty;
        _carried = null;
    }

    /// <summary>The engine as it stands now (see <see cref="_undo"/>).</summary>
    private WordState CurrentState() => new(_seqKeys.ToString(), _seqOut, _seqMatched, _uniWord.ToString(), _legacyOut,
        _raw.ToString(), _word, _plainWord.ToString(), _lastEmitted, _roman.ToString(), _phoneticOut, _done);

    /// <summary>What the keystrokes since the caret last moved have put on screen: the finished words and the one in progress.</summary>
    private string Shown() =>
        _done + (_legacyConv is not null ? _legacyOut : _phoneticOut + (_order.Active ? _word : _plainWord.ToString()));

    /// <summary>
    /// The word is finished, but the caret has not moved: what it put on screen joins what the
    /// keystrokes before it did (<see cref="_done"/>), and Backspace can still go back into it.
    /// </summary>
    private void CommitWord()
    {
        if (_uniWord.Length > 0) _prevOut = _uniWord[^1];
        _done = Shown();
        _uniWord.Clear();
        _legacyOut = string.Empty;
        _raw.Clear();
        _word = string.Empty;
        _plainWord.Clear();
        _lastEmitted = null;
        _roman.Clear();
        _phoneticOut = string.Empty;
    }

    /// <summary>
    /// Backspace: the engine goes back to how it stood before the last keystroke, and the
    /// screen with it - inside the word, into a word a key of ours finished, and the run in
    /// progress too, so the key typed next carries the run on exactly as it would have then.
    /// </summary>
    private void RestoreState(WordState s)
    {
        string shown = Shown();
        _seqKeys.Clear();
        _seqKeys.Append(s.SeqKeys);
        _seqOut = s.SeqOut;
        _seqMatched = s.SeqMatched;
        _uniWord.Clear();
        _uniWord.Append(s.UniWord);
        _legacyOut = s.LegacyOut;
        _raw.Clear();
        _raw.Append(s.Raw);
        _word = s.Word;
        _plainWord.Clear();
        _plainWord.Append(s.PlainWord);
        _lastEmitted = s.LastEmitted;
        _roman.Clear();
        _roman.Append(s.Roman);
        _phoneticOut = s.PhoneticOut;
        _done = s.Done;
        string next = Shown();
        int keep = CommonPrefix(shown, next);
        if (_legacyConv is not null)
        {
            InjectLegacy(shown.Length - keep, next[keep..], keep == 0);
            return;
        }
        if (shown.Length > keep) _injector.SendBackspace(shown.Length - keep);
        if (next.Length > keep) _injector.SendText(next[keep..]);
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
        long clicks = Clicks();
        if (clicks != _clicksSeen)
        {
            _clicksSeen = clicks;
            ForgetWord();
        }

        if (e.VirtualKey is VK_CONTROL or VK_LCONTROL or VK_RCONTROL)
        {
            if (e.Lift)
            {
                // Our key-up for a held Ctrl. Only if Windows still had it down at
                // this point was the finger on it; a finger that let go just before
                // left nothing to lift.
                if (!e.IsKeyDown && IsDown(e.VirtualKey)) { _ctrlLifted = true; _ctrlLiftedAt = Environment.TickCount64; }
            }
            else if (!e.Injected) _ctrlLifted = false;   // the finger moved: Windows' state is the truth again
        }

        if (!e.Injected && !e.IsKeyDown && e.VirtualKey == _layerKey)
            _layerKey = 0;
        if (!e.Injected && !e.IsKeyDown && e.VirtualKey == _escapeKey)
            _escapeKey = 0;

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
            // The word typed so far stays as it is on screen, finished (below, once the
            // escape is a keystroke Backspace can take back) - unless this escape is the
            // second of a run (ØmØma), whose text is still being rewritten.
        }

        if (_layout is null)
            return;

        if (!e.IsKeyDown)
            return; // map on key-down only

        // Backspace removes the whole last injected cluster (e.g. a consonant +
        // matra pair) instead of a single code unit. Ctrl+Backspace and
        // Alt+Backspace are the application's (delete a word, undo): taking them
        // over sent our Backspaces under the typist's Ctrl, a word each.
        if (e.VirtualKey == VK_BACK)
        {
            if (IsDown(VK_CONTROL) || IsDown(VK_MENU))
            {
                _pendingDead = null;
                _lastEmitted = null;
                ResetSequence();
                ResetLegacy();
                FlushBeforeKey();
                return;
            }
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
            if (e.VirtualKey == _escapeKey) return;   // its auto-repeat, not a second press
            _escapeKey = e.VirtualKey;
            _pendingDead = null;
            _lastEmitted = null;
            // Pressed twice, the escape takes itself back: on Shree, Ctrl+Space
            // Ctrl+Space v types the plain v.
            if (_seqKeys.Length > 0 && _seqKeys[^1] == mark)
            {
                if (_undo.Count > 0) RestoreState(_undo.Pop());   // the escape's own keystroke, taken back with it
                else
                {
                    _seqKeys.Length--;
                    if (_seqKeys.Length == 0) _seqOut = string.Empty;
                }
                _log.Info($"Escape: {(mark == EscapeLayer.Mark ? "Ctrl+Space" : "Ctrl+Shift+1")} pressed again - taken back.");
                return;
            }
            if (_layout.HasSequences)
            {
                _undo.Push(CurrentState());         // an escape is a keystroke: Backspace takes it back
                // Typing by sound, the word so far ends here; Backspace still goes back into it.
                if (_seqKeys.Length == 0 && (PhoneticMode || _layoutIsPhonetic) && _translit is not null) CommitWord();
                _seqKeys.Append(mark);
            }
            else
            {
                ResetPhonetic();   // the word typed by sound, if any, is the application's now
                ResetLegacy();
            }
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
            if (ShortcutOnALiftedCtrl(e)) return;
            if (_seqKeys.Length > 0)
                _log.Info($"Escape: the run '{_seqKeys}' was dropped - '{typed}' arrived looking like a shortcut " +
                          $"(ctrl={CtrlDown}, alt={IsDown(VK_MENU)}).");
            _pendingDead = null;
            _lastEmitted = null;
            ResetSequence();
            // A word waiting on its virama keeps the half form Shree shows: rewriting it to the
            // halant now would send a Backspace under the typist's Ctrl, Alt or Windows key -
            // Ctrl+Backspace deletes the word before it, Alt+Backspace is undo.
            ResetLegacy(); // the shortcut may move the caret; start the next word fresh
            _prevOut = '\0';
            FlushBeforeKey();
            return;
        }
        if (looksLikeShortcut) LayerKeyUnderCtrl(e.VirtualKey);

        // A key with no glyph of its own can still finish a run: on the ITR Gujarati
        // keyboard "o" types nothing alone, but "fo" is ર્ક. So the run is resolved
        // before an unmapped key is handed back to the application.
        string? selected = mapping is null ? null : SelectOutput(mapping);
        string output = selected ?? string.Empty;
        int replace = 0;
        var before = CurrentState();          // the word as it stood before this keystroke
        // Keys of a run that have put nothing on screen yet (see below). Should this key
        // leave that run, the ones that are not letters are typed as themselves - as
        // they were before a run could start on them (Malayalam Ismeng's ".", for ".ra").
        string silent = _seqOut.Length == 0 && _seqKeys.Length > 0
                        && _seqKeys.ToString().IndexOfAny(new[] { EscapeLayer.Mark, EscapeLayer.SymbolMark }) < 0
            ? new string(_seqKeys.ToString().Where(c => !char.IsAsciiLetter(c)).ToArray()) : string.Empty;
        bool escapedToNothing = false;
        if (_layout.HasSequences)
            output = ResolveSequence(typed, output, out replace, out escapedToNothing);
        bool typedSilent = false;
        if (silent.Length > 0 && replace == 0 && !_seqKeys.ToString().StartsWith(before.SeqKeys, StringComparison.Ordinal)
            && !IsForegroundExcluded())
        {
            Emit(silent, 0);
            _lastEmitted = null;
            typedSilent = true;
        }

        // Ctrl+Space then a key its layer leaves empty types nothing, as on Shree (the
        // vendor file's slot is FF): it used to type the key's own character. The key
        // is swallowed, and the word goes on.
        if (escapedToNothing)
        {
            e.Handled = !IsForegroundExcluded();
            _undo.Push(before);
            _log.Info($"Escape: '{typed}' has nothing on that layer - types nothing.");
            return;
        }

        // A key that types nothing alone but starts (or carries on) a run of the chart -
        // most of the Ekl keyboard: "k" alone is nothing, "ka" is કા - waits for the
        // rest of the run instead of ending it: it used to be dropped, run and all.
        if (string.IsNullOrEmpty(output) && replace == 0 && typed != '\0' && _seqKeys.Length > 0
            && _seqKeys[^1] == typed && _layout.SequenceCanGrow(_seqKeys.ToString()) && !IsForegroundExcluded())
        {
            e.Handled = true;
            _undo.Push(before);
            return;
        }

        // Straight after a consonant, a key that types a vowel types the vowel's
        // sign instead - the run the chart shows on ka, applied to every letter.
        if (_order.Active && replace == 0
            && _order.SignAfter(typed, output, _word.Length > 0 ? _word[^1] : null) is { } sign)
        {
            output = sign;
            _seqOut = sign;
        }

        // A key that types nothing - a letter the keyboard does not use, or a slot it leaves
        // empty ("") - reaches nothing on Shree either (the keyboard file's slot is FF: no code
        // gets to the composer). So it is swallowed and nothing else changes: the word goes on,
        // and so does the run in progress. Ending the word there made the next sign start a word
        // of its own - DOE V d w (ਨੈ) wrote B2 BE B1 in a Shree font, where the table and Shree
        // write B2 B1 BE. It is a keystroke all the same: Backspace takes it back.
        if (string.IsNullOrEmpty(output) && replace == 0 && !typedSilent
            && (e.VirtualKey is >= 'A' and <= 'Z' || selected is "") && !IsForegroundExcluded())
        {
            e.Handled = true;
            _seqKeys.Clear();
            _seqKeys.Append(before.SeqKeys);
            _seqOut = before.SeqOut;
            _seqMatched = before.SeqMatched;
            _undo.Push(before);
            return;
        }

        if (string.IsNullOrEmpty(output) && replace == 0)
        {
            bool excluded = IsForegroundExcluded();
            // A printable key in a legacy font goes through the font's table (P6): "" where the
            // font cannot show it, null where it is written as itself and passes as it is.
            string word = TextBefore();   // what this key follows: a table may spell the key by it
            string? tableText = excluded ? null : TableText(typed, word);
            // The word ends here: a virama it ends in is now the visible halant, not the
            // half form shown while it waited for its next letter.
            if (!excluded) FinishLegacyWord();
            _lastEmitted = null;  // navigation/space/punctuation breaks the chain
            ResetSequence();      // and ends the run being typed

            // A letter key the active keyboard does not use types nothing on the
            // vendor's software, and nothing here: an English letter dropped into
            // Indic text is never what the typist meant. Nor does a key whose layer
            // the keyboard says types nothing ("" - an imported Shree file's empty
            // slot), nor one the legacy font cannot show. A key the table writes is
            // typed as it writes it. Each is a keystroke of ours, and the caret has not
            // moved: Backspace takes it back whole, and then goes on into the word.
            // Everything else - digits, punctuation, space, the navigation keys - still
            // passes through, and the word is the application's from here.
            // An app the typist excluded gets every key as it is.
            if (!excluded && (e.VirtualKey is >= 'A' and <= 'Z' || selected is "" || tableText is not null))
            {
                e.Handled = true;
                _undo.Push(before);
                CommitWord();
                if (tableText is { Length: > 0 })
                {
                    _log.Info($"Key '{typed}' in a legacy font: typed as the table writes it.");
                    TypeAlone(typed.ToString(), word);
                }
                else if (tableText is not null) _log.Info($"Key '{typed}' in a legacy font: the font cannot show it - not typed.");
                return;
            }
            ResetLegacy();        // the word is the application's now
            _prevOut = e.VirtualKey == VK_SPACE ? ' ' : typed;
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

        _undo.Push(before);
        Emit(output, replace);
    }

    /// <summary>
    /// Put <paramref name="output"/> on screen, first removing
    /// <paramref name="replace"/> characters this layout had produced for the run
    /// in progress.
    /// </summary>
    private void Emit(string output, int replace)
    {
        // A link key struck after a letter that already carries its virama (the Phonetic keyboard types
        // every consonant as ક્; its Shift+Q and Ctrl+Space f type ્‍): one virama, not two - Shree's
        // composer writes k Q k a as ક્‍ક (BC 4C 24 in SHREE-GUJ7). Only the joiner is added.
        if (!_order.Active)
        {
            var word = _legacyConv is not null ? _uniWord : _plainWord;
            int end = word.Length - replace;
            if (output.Length > 0 && IsVirama(output[0]) && end > 0 && end <= word.Length && IsVirama(word[end - 1]))
                output = output[1..];
        }

        if (_order.Active)
        {
            _raw.Length = Math.Max(0, _raw.Length - replace);
            _raw.Append(_order.Mark(output));
            ShowWord();
            return;
        }

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
        _plainWord.Length = Math.Max(0, _plainWord.Length - replace);
        _plainWord.Append(output);
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
    /// Put the word typed so far on screen in Unicode's order: only what changed,
    /// since a sign struck before its letter moves in behind it as the letter comes.
    /// </summary>
    private void ShowWord()
    {
        string word = _order.Compose(_raw.ToString());
        _lastEmitted = null;   // Backspace takes back keystrokes of this word, not clusters
        if (_legacyConv is not null)
        {
            _word = word;
            _uniWord.Clear();
            _uniWord.Append(word);
            RerenderLegacy();
            return;
        }
        int keep = CommonPrefix(_word, word);
        if (_word.Length > keep) _injector.SendBackspace(_word.Length - keep);
        if (word.Length > keep) _injector.SendText(word[keep..]);
        _word = word;
    }

    /// <summary>
    /// Fold this keystroke into the run being typed. While the keys so far can
    /// still grow into a longer run the engine keeps composing, replacing what it
    /// showed before; when they cannot, the run ends and a new one starts with
    /// this key.
    /// </summary>
    private string ResolveSequence(char typed, string single, out int replace, out bool escapedToNothing)
    {
        escapedToNothing = false;
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

        // Ctrl+Space, then a key its layer leaves empty: on Shree it types nothing (the
        // keyboard file's slot is FF, and the composer answers nothing for it).
        if (typed != '\0' && grown.Length >= 2 && grown[^2] == EscapeLayer.Mark)
        {
            escapedToNothing = true;
            _seqKeys.Clear();
            _seqOut = string.Empty;
            return string.Empty;
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
    private char EscapeMarkFor(int vk)
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
    /// A key of a Ctrl+Space run, typed with the escape's Ctrl still held: remember
    /// it (its auto-repeat is ours), and have the injector let go of that Ctrl
    /// before what it types goes out.
    /// </summary>
    private void LayerKeyUnderCtrl(int vk)
    {
        _layerKey = vk;
        if (IsDown(VK_CONTROL)) _injector.LiftHeldCtrl();
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
    /// A shortcut on a Ctrl we lifted. Windows no longer has that Ctrl down, so the
    /// application would get a plain letter - typed into the document where the
    /// typist meant Ctrl+S. It is swallowed instead; pressing Ctrl again makes it
    /// a shortcut as usual.
    /// </summary>
    private bool ShortcutOnALiftedCtrl(HookKeyEventArgs e)
    {
        if (!CtrlOnlyByLift || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN)) return false;
        e.Handled = true;
        _log.Info($"Key {e.VirtualKey:X2} with a Ctrl we had let go of for the typist - not passed on as a letter.");
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

        // Backspace takes back the run's last keystroke, the escape too, as on every other
        // keyboard: it used to end the run and delete one character of what it had typed
        // (Ctrl+Space n is ઙ્; Backspace left ઙ).
        if (e.VirtualKey == VK_BACK)
        {
            if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || _undo.Count == 0) { ResetSequence(); return false; }
            e.Handled = true;
            RestoreState(_undo.Pop());
            return true;
        }

        var before = CurrentState();
        char typed = KeyChar(e.VirtualKey);
        string grown = _seqKeys.ToString() + typed;
        string? hit = typed == '\0' ? null : _layout!.MatchSequence(grown);
        bool grows = typed != '\0' && _layout!.SequenceCanGrow(grown);
        if (hit is null && !grows)
        {
            // Ctrl+Space and a key its layer leaves empty: Shree's Eng keyboard types nothing
            // there (the file's slot is FF) - it used to type the key by sound.
            if (typed != '\0' && _seqKeys.Length == 1 && _seqKeys[0] == EscapeLayer.Mark && !IsForegroundExcluded())
            {
                _log.Info($"Escape: then '{typed}' -> nothing on that layer - types nothing.");
                _undo.Push(before);            // a keystroke all the same: Backspace takes it back, and nothing before it
                ResetSequence();
                e.Handled = true;
                return true;
            }
            if (_seqKeys[^1] == EscapeLayer.Mark || _seqKeys[^1] == EscapeLayer.SymbolMark)
                _log.Info($"Escape: then '{typed}' -> nothing on that layer.");
            // Keys that only looked like the start of a longer run showed nothing
            // while it might come: Ctrl+Space n g could still become ઙ્ગ. It did not,
            // so they are letters after all - typed by sound, ahead of this key.
            // What the run typed stays, finished; this keystroke's Backspace goes back to it.
            string pending = _seqKeys.ToString()[_seqMatched..];
            ResetSequence();
            CommitWord();
            foreach (char c in pending)
                if (char.IsAsciiLetter(c)) _roman.Append(c);
            if (_roman.Length > 0) Rerender();
            _carried = before;
            return false;                                          // typed by sound
        }

        if (IsForegroundExcluded()) { ResetSequence(); return false; }

        e.Handled = true;
        if (CtrlDown) LayerKeyUnderCtrl(e.VirtualKey);
        string text = hit ?? _seqOut;          // until the run finishes, keep what it shows
        int replace = _seqOut.Length;
        _log.Info($"Escape: then '{typed}' -> '{text}'.");
        _undo.Push(before);
        _seqKeys.Clear();
        _seqKeys.Append(grown);
        if (hit is not null) _seqMatched = _seqKeys.Length;
        _seqOut = text;
        Emit(text, replace);
        if (!grows)
        {
            ResetSequence();                   // the run is complete
            CommitWord();                      // and what it typed is finished; Backspace still goes back into it
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
    // The last report of characters the legacy table could not write as the font's own codes (logged once).
    private string? _lastNotShown;

    private void RerenderLegacy(bool wordEnds = false)
    {
        string word = _uniWord.ToString();
        // A word still being typed that ends in a virama is waiting for its next letter:
        // Shree shows the half form (Shift+A on BPMK is E5), the reph (AE) or the link's
        // half letter meanwhile, and only a word that ends there gets the visible halant.
        // A table that knows the marker writes that; for one that does not, nothing changes.
        // (Not a ra + virama on a keyboard typed in Unicode's order - no sign struck before its
        // letter, no reph after it: InScript and Indica, whose ર્ key holds Shree's code BE. Shree
        // shows that ra with its halant until the letter comes; the reph glyph alone (AE) is what
        // the typewriter keyboards' reph key shows, Shree's code 40.)
        bool rephFirst = word.Length > 1 && (word[^2] & 0x7F) == 0x30 && word[^2] is >= 'ऀ' and <= '෿'
                         && (word.Length < 3 || !IsVirama(word[^3])) && _order.PreSigns.Count == 0 && _order.Reph is null;
        if (!wordEnds && word.Length > 0 && IsVirama(word[^1]) && !rephFirst && _legacyConv!.KnowsWaiting)
            word += FontConverter.WaitingMark;
        var converted = _legacyConv!.Convert(word);
        string next = converted.Text;
        string shown = _legacyOut;
        if (string.Equals(next, shown, StringComparison.Ordinal))
            return;                        // the keystroke changed nothing on screen
        // What the table reports is said, as the converter says it (round 2): left out - the font cannot show it -
        // or typed as itself, which the font has no glyph for (₹ in a Shree font; Word draws it in another font).
        if (converted.HasUnmapped)
        {
            string said = $"left out: {string.Join(" ", converted.LeftOutCharacters)}; typed as itself: {string.Join(" ", converted.KeptCharacters)}";
            if (!string.Equals(said, _lastNotShown, StringComparison.Ordinal))
                _log.Info($"Typed into a legacy font - {said} (the font has no glyph for these).");
            _lastNotShown = said;
        }

        // One keystroke usually only adds to the end of the word, even though the
        // whole word has to be re-converted to find that out: the legacy byte order
        // of what came before can change when a matra arrives. So send the
        // difference, not the word. Erasing and retyping the whole word on every
        // key is what made the text visibly redraw itself, and on a slow machine
        // what made it lag: a ten-letter word cost twenty injected events per key.
        _legacyOut = next;
        int keep = CommonPrefix(shown, next);
        InjectLegacy(shown.Length - keep, next[keep..], keep == 0);
    }

    /// <summary>
    /// Take <paramref name="remove"/> characters back from in front of the caret and type
    /// <paramref name="add"/> there, in the legacy font. <paramref name="fromWordStart"/>: nothing
    /// we typed is left in front of the caret, so the font has to travel with the text.
    /// </summary>
    private void InjectLegacy(int remove, string add, bool fromWordStart)
    {
        if (remove == 0 && add.Length == 0) return;
        string? app = LegacyFontName is null ? null : ForegroundApp.GetProcessName();
        bool rich = app is not null && RichTextApps.Contains(app) && !ForegroundApp.IsSpreadsheet();

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
            if (rich && fromWordStart && !wordRefusedUs)
                _injector.SendRichText(add, LegacyFontName!);
            else
                _injector.SendText(add);
        });
    }

    /// <summary>The virama of every Indic script Unicode has (U+094D, U+09CD ... U+0D4D).</summary>
    private static bool IsVirama(char c) => c is >= 'ऀ' and <= '෿' && (c & 0x7F) == 0x4D;

    /// <summary>
    /// The word in a legacy font ends here (a space, punctuation, Enter, a shortcut): if it
    /// ends in a virama it was shown waiting for its next letter, and now gets the visible halant.
    /// </summary>
    private void FinishLegacyWord()
    {
        if (_legacyConv is null || _uniWord.Length == 0 || !IsVirama(_uniWord[^1]) || !_legacyConv.KnowsWaiting) return;
        // Never under a held Ctrl, Alt or Windows key: the rewrite's Backspace would be the
        // application's Ctrl+Backspace (delete a word) or Alt+Backspace (undo).
        if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN)) return;
        RerenderLegacy(wordEnds: true);
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
        // A pending dead key is cancelled by backspace before anything was emitted.
        if (_pendingDead is not null)
        {
            _pendingDead = null;
            e.Handled = true;
            return;
        }

        // The word being typed: Backspace takes back exactly its last keystroke - the
        // whole of it (Shift+A's બ્, not only its virama), and only it inside a chart run
        // (InScript k s is કે; Backspace leaves ક, where it used to take the run whole).
        // The word goes back to how it stood before that key, in every font and order,
        // and so does the run, so the key typed next carries it on as it would have then.
        if (_undo.Count > 0)
        {
            e.Handled = true;
            RestoreState(_undo.Pop());
            return;
        }

        // Nothing of this word is ours to take back: the word is committed, and the
        // application's own Backspace acts on the document from here.
        _legacyOut = string.Empty;
        _done = string.Empty;
        ResetSequence();
        _lastEmitted = null;
    }

    /// <summary>
    /// Phonetic mode: buffer roman letters for the current word and re-render the
    /// script live. Backspace shrinks the buffer; any non-letter (space, punctuation,
    /// digit, navigation) commits the word and passes through untouched.
    /// </summary>
    private void HandlePhonetic(HookKeyEventArgs e)
    {
        // The state this keystroke began in - the run on the layer it ended, if it ended one.
        var before = _carried ?? CurrentState();
        _carried = null;

        // Backspace: back to how it stood before the last keystroke - a roman letter of the
        // word, a fixed key, a run on the layer (see _done). Ctrl+ and Alt+Backspace are the
        // application's; the word is finished as it stands.
        if (e.VirtualKey == VK_BACK && (IsDown(VK_CONTROL) || IsDown(VK_MENU)))
        {
            if (e.IsKeyDown) { ResetSequence(); ResetPhonetic(); FlushBeforeKey(); }
            return;
        }
        if (e.VirtualKey == VK_BACK)
        {
            if (!e.IsKeyDown)
                return;
            if (_undo.Count == 0)
            {
                ResetPhonetic();
                return; // nothing of ours → let Backspace act normally
            }
            e.Handled = true;
            RestoreState(_undo.Pop());
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
            if (ShortcutOnALiftedCtrl(e)) { ResetPhonetic(); return; }
            ResetPhonetic();
            FlushBeforeKey();
            return;
        }

        // The roman letters, and the symbols the scheme spells with (Shree's Eng keyboard
        // types ઽ on '<', the ru ligature on '@', ...), are buffered for the word.
        if (TryRomanChar(e.VirtualKey, out char ch) || ReadsBySound(ch = KeyChar(e.VirtualKey)))
        {
            e.Handled = true; // suppress the raw Latin letter (down and up)
            if (!e.IsKeyDown)
                return;
            _undo.Push(before);   // a keystroke, even one that types nothing: Backspace takes it back
            // A letter the scheme cannot read types nothing, as on Shree's Eng keyboard
            // (B, C, K, P, Q, V, W, X, Z, q) - it used to be typed as itself, in Latin.
            if (!ReadsBySound(ch)) return;
            _roman.Append(ch);
            Rerender();
            return;
        }

        // Boundary key (space, enter, punctuation, digit, arrows): commit and pass
        // through - once what we typed for the word is on screen. In Word a legacy
        // word goes in through its automation object, slowly enough that a space
        // let through at once could land between the last two letters.
        if (!e.IsKeyDown) return;
        FinishLegacyWord();
        // A key the keyboard itself types (Shree's Eng: the digits are the script's, '&' the
        // danda) is typed as the keyboard says - or nothing, where the keyboard leaves it
        // empty (""); a printable key in a legacy font goes through the font's table. Either
        // is a keystroke of ours, and the caret has not moved: the word ends, but Backspace
        // takes this key back whole and then goes on into the word. Any other key passes through.
        string? alone = null;   // what the key types (Unicode; in a legacy font TypeAlone converts it)
        string after = string.Empty;   // the word it ends, when the table spells the key by it
        if (_layoutIsPhonetic && _layout?.Find(e.VirtualKey) is { } m && ch != '\0' && SelectOutput(m) is { } fixedText && !IsForegroundExcluded())
            alone = fixedText;
        else if (!IsForegroundExcluded() && TableText(ch, TextBefore()) is { } written)
        {
            alone = written.Length > 0 ? ch.ToString() : string.Empty;
            after = TextBefore();
        }
        if (alone is not null)
        {
            e.Handled = true;
            _undo.Push(before);
            CommitWord();
            if (alone.Length > 0) TypeAlone(alone, after);
            return;
        }
        ResetPhonetic();
        _prevOut = e.VirtualKey == VK_SPACE ? ' ' : ch;
        FlushBeforeKey();
    }

    /// <summary>True when the phonetic scheme spells with this character (a letter anywhere in a token, a symbol at its start).</summary>
    private bool ReadsBySound(char c)
    {
        if (c == '\0' || _translit is null) return false;
        if (!ReferenceEquals(_soundCharsOf, _translit))
        {
            _soundCharsOf = _translit;
            _soundChars.Clear();
            var s = _translit.Scheme;
            foreach (var token in s.Consonants.Keys.Concat(s.Vowels.Keys).Concat(s.Specials.Keys))
            {
                if (token.Length == 0) continue;
                foreach (char t in token) if (char.IsAsciiLetter(t)) _soundChars.Add(t);
                _soundChars.Add(token[0]);
            }
        }
        return _soundChars.Contains(c);
    }
    private readonly HashSet<char> _soundChars = new();
    private Transliterator? _soundCharsOf;

    /// <summary>
    /// Text a key types on its own, outside any word (the word before it is finished,
    /// <see cref="CommitWord"/>): in a legacy font, converted. It is finished too, and stays
    /// ours for Backspace to take back whole - '"' is 22 22 in a Shree font, two characters.
    /// </summary>
    private void TypeAlone(string text, string context = "")
    {
        if (_legacyConv is not null)
        {
            _uniWord.Append(text);
            // Spelled after what it follows, where that changes it (an ASCII quote closes after a letter).
            string spelled = context.Length > 0 ? ConvertAfter(context, text) : string.Empty;
            if (context.Length > 0 && spelled != _legacyConv.ConvertText(text))
            {
                int remove = _legacyOut.Length;
                _legacyOut = spelled;
                InjectLegacy(remove, spelled, true);
            }
            else RerenderLegacy(wordEnds: true);
            CommitWord();
            return;
        }
        _injector.SendText(text);
        _done = Shown() + text;
    }

    // The last character put in front of the caret since it last moved, as far as the engine knows: the end of a
    // word it finished, a key it typed on its own, a key it let through (a space for the space bar); '\0' when
    // unknown. A key typed on its own is spelled after it (see ConvertAfter): an ASCII quote after "3" closes.
    private char _prevOut;

    /// <summary>The text a key typed on its own follows: the word in progress, else the last character before the caret.</summary>
    private string TextBefore() => _uniWord.Length > 0 ? _uniWord.ToString() : _prevOut != '\0' ? _prevOut.ToString() : string.Empty;

    /// <summary>
    /// A printable key the keyboard passes through, in a legacy font: it reaches the document as
    /// its byte, which the font may draw as a letter of the script ('[' is an i-sign in a Shree font).
    /// So it goes through the font's table as the converter's text does (P6): what the table writes
    /// for it after <paramref name="context"/> (the word it ends), "" where the table leaves it out,
    /// null where it is written as itself and can pass as it is.
    /// </summary>
    private string? TableText(char typed, string context = "")
    {
        if (_legacyConv is null || typed is < '!' or > '~') return null;
        string written = ConvertAfter(context, typed.ToString());
        return written == typed.ToString() ? null : written;
    }

    /// <summary>
    /// What the legacy table writes for <paramref name="text"/> where it follows <paramref name="context"/>,
    /// which is on screen already: a table can spell a mark by what comes before it - in the Shree tables an
    /// ASCII quote opens after a space and closes after a letter (ਪ' is 5C 27, a ' alone 22). Converted alone,
    /// the apostrophe of a word came out as the opening quote, where the converter writes the closing one.
    /// </summary>
    private string ConvertAfter(string context, string text)
    {
        if (context.Length == 0) return _legacyConv!.ConvertText(text);
        string both = _legacyConv!.ConvertText(context + text), first = _legacyConv.ConvertText(context);
        return both.StartsWith(first, StringComparison.Ordinal) ? both[first.Length..] : _legacyConv.ConvertText(text);
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

        // Shift XOR CapsLock selects the shifted glyph (letter-style behaviour). A
        // Shift the layout leaves unset types the normal character; one it sets to
        // "" types nothing - an imported Shree file's empty slot, where Shift+5 on
        // a client's keyboard used to type the 5 key's letter.
        bool useShift = shift ^ caps;
        if (useShift && mapping.Shift is not null)
            return mapping.Shift;

        return mapping.Base;
    }

    /// <summary>
    /// True when the key press is an application shortcut rather than text, so the
    /// engine must leave it alone: Ctrl+key, Alt+key and Win+key.
    /// Ctrl+Alt is AltGr on European keyboards, so that pair counts as text only
    /// when the layout actually defines an AltGr glyph for the key.
    /// </summary>
    private bool IsShortcut(KeyMapping? mapping)
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

    /// <summary>
    /// Forget the word in progress. Called when the hook was put back after Windows
    /// had dropped it: the keys typed meanwhile went past the engine, so what it
    /// remembers of the word no longer matches what is on screen.
    /// </summary>
    public void Resync()
    {
        _ctrlLifted = false;
        _layerKey = 0;
        _escapeKey = 0;
        ForgetWord();
    }

    /// <summary>The caret may be somewhere else: the word in progress is no longer ours to rewrite.</summary>
    private void ForgetWord()
    {
        _prevOut = '\0';
        _pendingDead = null;
        _lastEmitted = null;
        ResetPhonetic();
        ResetSequence();
        ResetLegacy();
    }

    public void Dispose()
    {
        _hook.KeyEvent -= OnKey;
        // The injection thread may still have the last keystroke in hand.
        _injector.Drain();
    }
}
