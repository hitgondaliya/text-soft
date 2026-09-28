using System.Runtime.InteropServices;
using FontSoftware.Core.Diagnostics;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>
/// Synthesizes keyboard input into the focused application (FR-3). Text is sent
/// as Unicode via SendInput so mapped script characters land correctly in Word,
/// Notepad, browsers, etc. regardless of the OS keyboard layout.
/// </summary>
public class InputInjector
{
    private readonly IAppLog _log;

    public InputInjector(IAppLog? log = null) => _log = log ?? NullLog.Instance;

    /// <summary>
    /// Tag written into dwExtraInfo of every synthesized event. The hook checks
    /// for it to ignore our own injected input and avoid a feedback loop.
    /// </summary>
    public static readonly IntPtr InjectionSignature = new(0x46_53_54_31); // "FST1"

    // ---- the injection thread ----
    //
    // Every one of these calls is made from inside the low-level keyboard hook,
    // and until that callback returns the whole system's keyboard input waits on
    // it. Sending the input from there means the keystroke the typist just pressed
    // is held up by our own SendInput, the target app's repaint, and - in a word
    // processor - a clipboard round trip. So the hook only decides what to type;
    // one thread does the typing, in the order the decisions were made.
    //
    // The thread is STA because the clipboard and Word's automation object expect
    // it, and it is created on first use so nothing pays for it until typing is on.

    private readonly object _gate = new();
    private System.Collections.Concurrent.BlockingCollection<Action>? _queue;
    private int _pumpThread;

    /// <summary>
    /// Run <paramref name="work"/> on the injection thread, in its turn among
    /// everything else queued. Work posted from inside that thread runs where it
    /// stands, so a step that sends several things keeps them together instead of
    /// letting the next keystroke's output slip in between.
    /// </summary>
    public virtual void Run(Action work) => Post(work);

    private void Post(Action work)
    {
        if (Environment.CurrentManagedThreadId == _pumpThread) { work(); return; }

        var queue = _queue;
        if (queue is null)
            lock (_gate)
            {
                if (_queue is null)
                {
                    _queue = new System.Collections.Concurrent.BlockingCollection<Action>();
                    var thread = new Thread(Pump) { IsBackground = true, Name = "TextSoft input" };
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                }
                queue = _queue;
            }

        try { queue.Add(work); }
        catch (InvalidOperationException) { work(); }   // shutting down: do it here
    }

    private void Pump()
    {
        _pumpThread = Environment.CurrentManagedThreadId;
        foreach (var work in _queue!.GetConsumingEnumerable())
        {
            try { work(); }
            catch (Exception ex) { _log.Error($"input injection failed: {ex.Message}"); }
        }
    }

    /// <summary>Wait for everything queued so far to be sent. For tests and shutdown.</summary>
    public void Drain(int millisecondsTimeout = 2000)
    {
        var queue = _queue;
        if (queue is null) return;
        using var done = new ManualResetEventSlim(false);
        try { queue.Add(() => done.Set()); } catch (InvalidOperationException) { return; }
        done.Wait(millisecondsTimeout);
    }

    /// <summary>Inject a string of Unicode text into the focused window.</summary>
    public virtual void SendText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Post(() =>
        {
            // Each UTF-16 code unit (including surrogate halves) is one keydown +
            // keyup Unicode event.
            var inputs = new INPUT[text.Length * 2];
            int n = 0;
            foreach (char c in text)
            {
                inputs[n++] = UnicodeInput(c, keyUp: false);
                inputs[n++] = UnicodeInput(c, keyUp: true);
            }

            Send(WithCtrlLifted(inputs), $"text '{text}'");
        });
    }

    /// <summary>
    /// Inject text already carrying a font: the text goes on the clipboard as RTF
    /// (+ plain text) and a Ctrl+V is sent. Rich editors (Word, WordPad, Outlook)
    /// take the font from the RTF, so legacy-font text appears in its font without
    /// the user touching the font box. Whatever text was on the clipboard before is
    /// put back shortly after the paste.
    /// </summary>
    public virtual void SendRichText(string text, string fontName)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Post(() =>
        {
            var handover = RichTextClipboard.Set(text, fontName);

            // Ctrl+V pastes whatever is on the clipboard. If ours did not get on to
            // it - another program was holding it - then pasting sends the typist's
            // own last copy into their document instead of the letter they typed.
            // Type the characters one by one instead and let them take the font
            // from the text around them.
            if (!handover.Taken)
            {
                _log.Info($"The clipboard was not free, so '{text}' was typed rather than pasted; " +
                          "it takes the font of the text it lands in.");
                SendText(text);
                return;
            }

            // Scan codes as well as virtual keys: some editors only honour a shortcut
            // that carries the physical key.
            // A Ctrl the typist is holding (through a Ctrl+Space run) is already
            // down: pressing and releasing ours around the V would leave Windows
            // reading it as up while their finger is still on it.
            bool held = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
            var inputs = new List<INPUT>(4);
            if (!held) inputs.Add(VirtualKeyInput(VK_CONTROL, keyUp: false, scan: 0x1D));
            inputs.Add(VirtualKeyInput(0x56 /* V */, keyUp: false, scan: 0x2F));
            inputs.Add(VirtualKeyInput(0x56, keyUp: true, scan: 0x2F));
            if (!held) inputs.Add(VirtualKeyInput(VK_CONTROL, keyUp: true, scan: 0x1D));
            Send(inputs.ToArray(), $"rich paste '{text}' in {fontName}");

            if (handover.Previous is not null)
            {
                // Give the target app time to read the clipboard, then put the
                // typist's own text back - but only while the clipboard still holds
                // ours. If something else has changed it since, that something is
                // the typist copying again, and overwriting it would be the second
                // way to lose what they copied.
                _ = Task.Delay(1500).ContinueWith(_ =>
                {
                    try
                    {
                        if (RichTextClipboard.HoldsText(text))
                            RichTextClipboard.SetText(handover.Previous);
                    }
                    catch { /* best effort */ }
                });
            }
        });
    }

    /// <summary>Send <paramref name="count"/> backspace key presses.</summary>
    public virtual void SendBackspace(int count = 1)
    {
        if (count <= 0)
            return;

        Post(() =>
        {
            var inputs = new INPUT[count * 2];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                inputs[n++] = VirtualKeyInput(VK_BACK, keyUp: false);
                inputs[n++] = VirtualKeyInput(VK_BACK, keyUp: true);
            }

            Send(WithCtrlLifted(inputs), $"{count} backspace(s)");
        });
    }

    /// <summary>
    /// What we type, with a Ctrl the typist is holding lifted around it.
    ///
    /// Ctrl+Space is a chord, and after it the typist may keep Ctrl down through
    /// the next key. What we type for that key would then arrive with Ctrl held:
    /// our Backspace would be Ctrl+Backspace, which deletes the whole word before
    /// the caret, and a word processor may read a character as a shortcut. So the
    /// held side (left, right, or both) comes up just before and goes back down
    /// just after, in the one SendInput call - nothing can come between, and
    /// Windows never reads Ctrl as up while the finger is on it. Only a Ctrl that
    /// is down the moment this runs is lifted, so none is ever pressed that the
    /// typist is not holding.
    /// </summary>
    private static INPUT[] WithCtrlLifted(INPUT[] inputs)
    {
        bool left = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0;
        bool right = (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
        if (!left && !right) return inputs;
        var all = new List<INPUT>(inputs.Length + 4);
        if (left) all.Add(VirtualKeyInput(VK_LCONTROL, keyUp: true, scan: 0x1D));
        if (right) all.Add(VirtualKeyInput(VK_RCONTROL, keyUp: true, scan: 0x1D, extended: true));
        all.AddRange(inputs);
        if (left) all.Add(VirtualKeyInput(VK_LCONTROL, keyUp: false, scan: 0x1D));
        if (right) all.Add(VirtualKeyInput(VK_RCONTROL, keyUp: false, scan: 0x1D, extended: true));
        return all.ToArray();
    }

    /// <summary>
    /// Send the events and verify the OS accepted them. A short read here means
    /// the input was blocked or rejected (e.g. an elevated foreground window, or
    /// a wrong INPUT struct size) — surface it instead of dropping keys silently,
    /// so a suppressed keystroke that produced nothing is never invisible.
    /// </summary>
    private void Send(INPUT[] inputs, string what)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, INPUT.Size);
        if (sent != inputs.Length)
            _log.Error($"SendInput injected {sent}/{inputs.Length} events for {what} " +
                       $"(Win32 error {Marshal.GetLastWin32Error()}). Input may be blocked.");
    }

    private static INPUT UnicodeInput(char c, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = c,
                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = InjectionSignature
            }
        }
    };

    private static INPUT VirtualKeyInput(int vk, bool keyUp, ushort scan = 0, bool extended = false) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = scan,
                dwFlags = (keyUp ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
                time = 0,
                dwExtraInfo = InjectionSignature
            }
        }
    };
}
