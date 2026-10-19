using System.Reflection;
using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using FontSoftware.Engine;

/// <summary>A pretend application: whatever the engine injects lands in a buffer. Nothing reaches the machine.</summary>
sealed class FakeApp : InputInjector
{
    public readonly StringBuilder Buf = new();
    public override void SendText(string text) => Buf.Append(text);
    public override void SendRichText(string text, string fontName) => Buf.Append(text);
    public override void SendBackspace(int count = 1) => Buf.Length = Math.Max(0, Buf.Length - count);
    public override void Run(Action work) => work();
    public override void LiftHeldCtrl() { }
}

/// <summary>Drives the real TypingEngine through its hook event (the hook itself is never installed).</summary>
sealed class Typist : IDisposable
{
    const int SHIFT = 0x10, CTRL = 0x11, SPACE = 0x20, BACK = 0x08, RET = 0x0D;
    public const char CtrlSpace = 'Ø', CtrlShift1 = '¤';
    static readonly HashSet<int> Held = new();
    static Typist() => TypingEngine.KeyDownProbe = vk => Held.Contains(vk);

    readonly LowLevelKeyboardHook _hook = new();
    public readonly FakeApp App = new();
    readonly TypingEngine _engine;
    static readonly FieldInfo Event = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public Typist(KeyboardLayout layout, Transliterator? translit, FontConverter? legacy)
    {
        Held.Clear();
        _engine = new TypingEngine(_hook, App);
        _engine.SetProfile(layout, "Telugu", legacy is null ? "Unicode" : "legacy");
        _engine.SetTransliterator(translit);
        _engine.SetLegacyConverter(legacy, null);
        _engine.SetEnabled(true);
    }

    bool Fire(int vk, bool down)
    {
        var a = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down };
        ((EventHandler<HookKeyEventArgs>?)Event.GetValue(_hook))!(_hook, a);
        return a.Handled;
    }

    static (int Vk, bool Shift) Vk(char c)
    {
        if (c >= 'a' && c <= 'z') return (char.ToUpperInvariant(c), false);
        if (c >= 'A' && c <= 'Z') return (c, true);
        if (c >= '0' && c <= '9') return (c, false);
        int i = ")!@#$%^&*(".IndexOf(c);
        if (i >= 0) return ('0' + i, true);
        return c switch
        {
            ';' => (0xBA, false), ':' => (0xBA, true), '=' => (0xBB, false), '+' => (0xBB, true),
            ',' => (0xBC, false), '<' => (0xBC, true), '-' => (0xBD, false), '_' => (0xBD, true),
            '.' => (0xBE, false), '>' => (0xBE, true), '/' => (0xBF, false), '?' => (0xBF, true),
            '`' => (0xC0, false), '~' => (0xC0, true), '[' => (0xDB, false), '{' => (0xDB, true),
            '\\' => (0xDC, false), '|' => (0xDC, true), ']' => (0xDD, false), '}' => (0xDD, true),
            '\'' => (0xDE, false), '"' => (0xDE, true), ' ' => (SPACE, false), '\b' => (BACK, false), '\n' => (RET, false),
            _ => (0, false),
        };
    }

    /// <summary>One keystroke: a US-keyboard character, Ø = Ctrl+Space, ¤ = Ctrl+Shift+1, \b = Backspace. Returns handled.</summary>
    public bool Key(char c)
    {
        bool handled;
        if (c == CtrlSpace || c == CtrlShift1)
        {
            Held.Add(CTRL);
            if (c == CtrlShift1) Held.Add(SHIFT);
            int vk = c == CtrlSpace ? SPACE : '1';
            handled = Fire(vk, true);
            Fire(vk, false);
            Held.Remove(CTRL); Held.Remove(SHIFT);
            return handled;   // an unhandled shortcut types nothing in the application
        }
        var (v, shift) = Vk(c);
        if (v == 0) throw new ArgumentException("no key for U+" + ((int)c).ToString("X4"));
        if (shift) Held.Add(SHIFT);
        handled = Fire(v, true);
        Fire(v, false);
        Held.Remove(SHIFT);
        if (!handled)
        {
            if (c == '\b') { if (App.Buf.Length > 0) App.Buf.Length--; }
            else App.Buf.Append(c == '\n' ? '\n' : c);
        }
        return handled;
    }

    public string Type(string keys) { foreach (char k in keys) Key(k); return App.Buf.ToString(); }

    public void Dispose() { _engine.Dispose(); _hook.Dispose(); }
}
