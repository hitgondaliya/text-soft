using System.Reflection;
using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using FontSoftware.Engine;

/// <summary>A pretend application: applies what the engine sends; nothing reaches the machine.</summary>
sealed class FakeApp : InputInjector
{
    public readonly StringBuilder Buf = new();
    public override void SendText(string text) => Buf.Append(text);
    public override void SendRichText(string text, string fontName) => Buf.Append(text);
    public override void SendBackspace(int count = 1) => Buf.Length = Math.Max(0, Buf.Length - count);
    public override void Run(Action work) => work();
    public override void LiftHeldCtrl() { }
}

/// <summary>Drives the real TypingEngine through its hook's KeyEvent (no hook installed, no SendInput).</summary>
sealed class Keys : IDisposable
{
    const int SHIFT = 0x10, CTRL = 0x11, SPACE = 0x20;
    static readonly HashSet<int> Held = new();
    static Keys() { TypingEngine.KeyDownProbe = vk => Held.Contains(vk); }
    readonly LowLevelKeyboardHook _hook = new();
    public readonly FakeApp App = new();
    readonly TypingEngine _engine;
    readonly FieldInfo _ev = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public Keys(KeyboardLayout layout, Transliterator tr, FontConverter legacy, string font)
    {
        Held.Clear();
        _engine = new TypingEngine(_hook, App);
        _engine.SetProfile(layout, "Punjabi", font);
        _engine.SetTransliterator(tr);
        _engine.SetLegacyConverter(legacy, null);
        _engine.SetEnabled(true);
    }

    bool Raw(int vk, bool down)
    {
        var a = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down };
        ((EventHandler<HookKeyEventArgs>)_ev.GetValue(_hook))!(_hook, a);
        return a.Handled;
    }

    static (int, bool) Vk(char c)
    {
        if (c >= 'a' && c <= 'z') return (char.ToUpperInvariant(c), false);
        if (c >= 'A' && c <= 'Z') return (c, true);
        if (c >= '0' && c <= '9') return (c, false);
        int i = ")!@#$%^&*(".IndexOf(c);
        if (i >= 0) return ('0' + i, true);
        return c switch
        {
            ';' => (186, false), ':' => (186, true), '=' => (187, false), '+' => (187, true),
            ',' => (188, false), '<' => (188, true), '-' => (189, false), '_' => (189, true),
            '.' => (190, false), '>' => (190, true), '/' => (191, false), '?' => (191, true),
            '`' => (192, false), '~' => (192, true), '[' => (219, false), '{' => (219, true),
            '\\' => (220, false), '|' => (220, true), ']' => (221, false), '}' => (221, true),
            '\'' => (222, false), '"' => (222, true), ' ' => (SPACE, false),
            _ => throw new ArgumentException("no key " + c),
        };
    }

    /// <summary>A key, after layer 1 = Ctrl+Space, layer 2 = Ctrl+Shift+1. Returns whether the engine took it.</summary>
    public bool Press(char c, int layer = 0)
    {
        if (layer == 1) { Held.Add(CTRL); Raw(SPACE, true); Raw(SPACE, false); Held.Remove(CTRL); }
        if (layer == 2) { Held.Add(CTRL); Held.Add(SHIFT); Raw('1', true); Raw('1', false); Held.Remove(CTRL); Held.Remove(SHIFT); }
        var (vk, sh) = Vk(c);
        if (sh) Held.Add(SHIFT);
        bool h = Raw(vk, true); Raw(vk, false);
        Held.Remove(SHIFT);
        if (!h) App.Buf.Append(c);   // the application types the key itself
        return h;
    }

    public void Dispose() { _engine.Dispose(); _hook.Dispose(); }
}
