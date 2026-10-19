using System.Reflection;
using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using FontSoftware.Engine;

/// <summary>
/// The target application: a text buffer that applies what the engine injects (text,
/// backspaces) and, for keys the engine leaves alone, what the key itself would type.
/// Nothing reaches the real machine: every injector method is overridden.
/// </summary>
sealed class SimApp : InputInjector
{
    public readonly StringBuilder Buf = new();
    public readonly List<string> Events = new();
    public override void SendText(string text) { Buf.Append(text); Events.Add("T:" + text); }
    public override void SendRichText(string text, string fontName) { Buf.Append(text); Events.Add("R:" + text); }
    public override void SendBackspace(int count = 1) { Buf.Length = Math.Max(0, Buf.Length - count); Events.Add("B:" + count); }
    public override void Run(Action work) => work();
    public override void LiftHeldCtrl() { Events.Add("LIFT"); }
}

sealed record KeyStep(string Key, bool Handled, string Events, string Buffer);

/// <summary>A typist at the real TypingEngine, the way TypingEngineTests drive it (hook event via reflection).</summary>
sealed class Typist : IDisposable
{
    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_SPACE = 0x20, VK_BACK = 0x08, VK_RETURN = 0x0D;
    public const char Click = '\u0001', Toggle = '\u0002';
    static readonly HashSet<int> Down = new();
    static Typist() { TypingEngine.KeyDownProbe = vk => Down.Contains(vk); }

    readonly LowLevelKeyboardHook _hook = new();
    public readonly SimApp App = new();
    public readonly TypingEngine Engine;
    readonly FieldInfo _field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
    long _clicks;
    public readonly List<KeyStep> Steps = new();

    public Typist(KeyboardLayout layout, Transliterator? translit, FontConverter? legacy, string fontName)
    {
        Down.Clear();
        Engine = new TypingEngine(_hook, App);
        Engine.Clicks = () => _clicks;
        Engine.SetProfile(layout, "Gujarati", fontName);
        Engine.SetTransliterator(translit);
        Engine.SetLegacyConverter(legacy, null);   // no font name: never looks at the foreground window / Word
        Engine.SetEnabled(true);
    }

    bool Send(int vk, bool down)
    {
        var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down };
        var h = (EventHandler<HookKeyEventArgs>?)_field.GetValue(_hook);
        h!(_hook, args);
        return args.Handled;
    }

    public static (int Vk, bool Shift) VkOf(char c)
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
            '\'' => (222, false), '"' => (222, true), ' ' => (VK_SPACE, false), '\b' => (VK_BACK, false),
            '\n' => (VK_RETURN, false),
            _ => (0, false),
        };
    }

    /// <summary>
    /// One keystroke in chart notation: a US-keyboard character, Ø = Ctrl+Space, ¤ = Ctrl+Shift+1,
    /// \b = Backspace, \n = Enter, \u0001 = a mouse click, \u0002 = the typist toggles TextSoft off,
    /// types the next character in English and toggles it back on.
    /// </summary>
    public KeyStep Key(char c, char? english = null)
    {
        int before = App.Events.Count;
        bool handled;
        if (c == Click) { _clicks++; handled = true; }
        else if (c == EscapeLayer.Mark || c == EscapeLayer.SymbolMark)
        {
            Down.Add(VK_CONTROL);
            if (c == EscapeLayer.SymbolMark) Down.Add(VK_SHIFT);
            int vk = c == EscapeLayer.Mark ? VK_SPACE : '1';
            handled = Send(vk, true);
            Send(vk, false);
            Down.Remove(VK_CONTROL); Down.Remove(VK_SHIFT);
            if (!handled) App.Buf.Append(c == EscapeLayer.Mark ? " " : "!");
        }
        else
        {
            var (vk, shift) = VkOf(c);
            if (vk == 0) throw new ArgumentException("no key for " + (int)c);
            if (shift) Down.Add(VK_SHIFT);
            handled = Send(vk, true);
            Send(vk, false);
            Down.Remove(VK_SHIFT);
            if (!handled)
            {
                if (c == '\b') { if (App.Buf.Length > 0) App.Buf.Length--; }
                else App.Buf.Append(c);
                App.Events.Add("APP:" + (c == '\b' ? "BS" : c == '\n' ? "ENTER" : c.ToString()));
            }
        }
        var st = new KeyStep(c == Click ? "CLICK" : c.ToString(), handled, string.Join(" ", App.Events.Skip(before)), App.Buf.ToString());
        Steps.Add(st);
        return st;
    }

    /// <summary>The typist switches TextSoft off (CapsLock), types English characters, switches it on.</summary>
    public KeyStep English(string text)
    {
        int before = App.Events.Count;
        Send(0x14, true); Send(0x14, false);          // CapsLock = toggle key: off
        foreach (char ch in text)
        {
            var (vk, shift) = VkOf(ch);
            if (shift) Down.Add(VK_SHIFT);
            bool h = Send(vk, true); Send(vk, false);
            Down.Remove(VK_SHIFT);
            if (!h) App.Buf.Append(ch);
        }
        Send(0x14, true); Send(0x14, false);          // on again
        var st = new KeyStep("EN:" + text, false, string.Join(" ", App.Events.Skip(before)), App.Buf.ToString());
        Steps.Add(st);
        return st;
    }

    public void Dispose()
    {
        Engine.Dispose();
        _hook.Dispose();
    }
}
