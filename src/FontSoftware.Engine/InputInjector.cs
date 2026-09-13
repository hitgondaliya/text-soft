using System.Runtime.InteropServices;
using FontSoftware.Core.Diagnostics;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>
/// Synthesizes keyboard input into the focused application (FR-3). Text is sent
/// as Unicode via SendInput so mapped script characters land correctly in Word,
/// Notepad, browsers, etc. regardless of the OS keyboard layout.
/// </summary>
public sealed class InputInjector
{
    private readonly IAppLog _log;

    public InputInjector(IAppLog? log = null) => _log = log ?? NullLog.Instance;

    /// <summary>
    /// Tag written into dwExtraInfo of every synthesized event. The hook checks
    /// for it to ignore our own injected input and avoid a feedback loop.
    /// </summary>
    public static readonly IntPtr InjectionSignature = new(0x46_53_54_31); // "FST1"

    /// <summary>Inject a string of Unicode text into the focused window.</summary>
    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Each UTF-16 code unit (including surrogate halves) is one keydown +
        // keyup Unicode event.
        var inputs = new INPUT[text.Length * 2];
        int n = 0;
        foreach (char c in text)
        {
            inputs[n++] = UnicodeInput(c, keyUp: false);
            inputs[n++] = UnicodeInput(c, keyUp: true);
        }

        Send(inputs, $"text '{text}'");
    }

    /// <summary>
    /// Inject text already carrying a font: the text goes on the clipboard as RTF
    /// (+ plain text) and a Ctrl+V is sent. Rich editors (Word, WordPad, Outlook)
    /// take the font from the RTF, so legacy-font text appears in its font without
    /// the user touching the font box. Whatever text was on the clipboard before is
    /// put back shortly after the paste.
    /// </summary>
    public void SendRichText(string text, string fontName)
    {
        if (string.IsNullOrEmpty(text))
            return;

        string? previous = RichTextClipboard.Set(text, fontName);

        var inputs = new INPUT[4];
        inputs[0] = VirtualKeyInput(VK_CONTROL, keyUp: false);
        inputs[1] = VirtualKeyInput(0x56 /* V */, keyUp: false);
        inputs[2] = VirtualKeyInput(0x56, keyUp: true);
        inputs[3] = VirtualKeyInput(VK_CONTROL, keyUp: true);
        Send(inputs, $"rich paste '{text}' in {fontName}");

        if (previous is not null)
        {
            // Give the target app time to read the clipboard, then restore the user's text.
            _ = Task.Delay(400).ContinueWith(_ =>
            {
                try { RichTextClipboard.SetText(previous); } catch { /* best effort */ }
            });
        }
    }

    /// <summary>Send <paramref name="count"/> backspace key presses.</summary>
    public void SendBackspace(int count = 1)
    {
        if (count <= 0)
            return;

        var inputs = new INPUT[count * 2];
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            inputs[n++] = VirtualKeyInput(VK_BACK, keyUp: false);
            inputs[n++] = VirtualKeyInput(VK_BACK, keyUp: true);
        }

        Send(inputs, $"{count} backspace(s)");
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

    private static INPUT VirtualKeyInput(int vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = InjectionSignature
            }
        }
    };
}
