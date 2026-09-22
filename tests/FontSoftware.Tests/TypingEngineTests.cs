using System.Reflection;
using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Engine;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Records what the engine would type instead of injecting it, so the tests can
/// read the result and never touch the machine they run on.
/// </summary>
internal sealed class RecordingInjector : InputInjector
{
    private readonly StringBuilder _text = new();

    public string Text => _text.ToString();

    public override void SendText(string text) => _text.Append(text);

    public override void SendRichText(string text, string fontName) => _text.Append(text);

    public override void SendBackspace(int count = 1)
        => _text.Length = Math.Max(0, _text.Length - count);
}

/// <summary>
/// The engine decides, per key, whether it owns the keystroke or the application
/// does, and how the keys typed so far combine. Both are invisible in a
/// screenshot and very visible to a typist: Ctrl+A used to type a letter instead
/// of selecting all, and "y" then "t" on the ITR Gujarati keyboard used to leave
/// અ ા on screen where the vendor keyboard gives આ.
/// </summary>
public class TypingEngineTests : IDisposable
{
    private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12, VkSpace = 0x20,
                      VkA = 0x41, VkY = 0x59, VkT = 0x54, VkU = 0x55, VkF = 0x46, VkO = 0x4F;

    private readonly LowLevelKeyboardHook _hook = new();
    private readonly RecordingInjector _injector = new();
    private readonly TypingEngine _engine;
    private readonly HashSet<int> _down = new();

    public TypingEngineTests()
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);

        var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        var gujarati = bundle.Languages.First(l => l.Id == "gu");
        var layout = bundle.GetLayout(gujarati.Layouts.First(l => l.Id == "guj-itr"));

        _engine = new TypingEngine(_hook, _injector);
        _engine.SetProfile(layout, gujarati.Name, "Unicode");
        _engine.SetEnabled(true);
    }

    public void Dispose()
    {
        TypingEngine.KeyDownProbe = vk => false;
        _engine.Dispose();
        _hook.Dispose();
    }

    /// <summary>Feed the engine a key press the way the hook would, and report whether it took it.</summary>
    private bool Press(int vk)
    {
        var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
        var field = typeof(LowLevelKeyboardHook)
            .GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(_hook);
        handler!(_hook, args);
        return args.Handled;
    }

    // ---- what the engine owns ----

    [Fact]
    public void PlainLetterIsTyped()
    {
        Assert.True(Press(VkY));
        Assert.Equal("અ", _injector.Text);
    }

    [Theory]
    [InlineData(VkControl)]   // Ctrl+A - select all
    [InlineData(VkMenu)]      // Alt+A - menu accelerator
    [InlineData(0x5B)]        // Win+A - Windows shortcut
    public void ShortcutsPassThroughToTheApplication(int modifier)
    {
        _down.Add(modifier);
        Assert.False(Press(VkA),
            $"modifier 0x{modifier:X2} makes this a command; the application must receive it");
        Assert.Equal("", _injector.Text);
    }

    [Fact]
    public void CtrlAltPassesThroughWhenTheLayoutHasNoAltGrGlyph()
    {
        _down.Add(VkControl);
        _down.Add(VkMenu);
        Assert.False(Press(VkA));
    }

    [Fact]
    public void DisabledEngineTouchesNothing()
    {
        _engine.SetEnabled(false);
        Assert.False(Press(VkY));
        Assert.Equal("", _injector.Text);
    }

    // ---- multi-key runs, as the vendor charts define them ----

    [Fact]
    public void SecondKeyReplacesWhatTheFirstProduced()
    {
        Press(VkY);                       // y  -> અ
        Assert.Equal("અ", _injector.Text);
        Press(VkT);                       // yt -> આ, replacing it
        Assert.Equal("આ", _injector.Text);
    }

    [Fact]
    public void AThreeKeyRunKeepsGrowing()
    {
        Press(VkY);
        Press(VkT);
        Press(VkU);                       // ytu -> ઓ
        Assert.Equal("ઓ", _injector.Text);
    }

    [Fact]
    public void RunEndsWhenTheKeysCannotGrow()
    {
        Press(VkY);                       // અ
        Press(VkF);                       // "yf" is not a run: ક starts a new one
        Assert.Equal("અક", _injector.Text);
    }

    [Fact]
    public void RephIsARunOverTheConsonant()
    {
        Press(VkF);                       // f  -> ક
        Press(VkO);                       // fo -> ર્ક
        Assert.Equal("ર્ક", _injector.Text);
    }

    [Fact]
    public void CtrlSpaceEndsTheRunAndTypesNothing()
    {
        Press(VkY);                       // અ
        _down.Add(VkControl);
        Assert.True(Press(VkSpace), "Ctrl+Space is the vendor escape; the engine swallows it");
        _down.Remove(VkControl);
        Assert.Equal("અ", _injector.Text);

        Press(VkT);                       // no longer part of the run, so it is the bare matra
        Assert.Equal("અા", _injector.Text);
    }
}
