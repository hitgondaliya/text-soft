using System.Reflection;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Engine;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The engine decides, per key, whether it owns the keystroke or the application
/// does. Getting that wrong is invisible in a screenshot and very visible to a
/// typist: before the shortcut guard, Ctrl+A typed a letter instead of selecting
/// all, and every Ctrl shortcut was dead while typing was on.
/// </summary>
public class TypingEngineTests : IDisposable
{
    private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12, VkA = 0x41;

    private readonly LowLevelKeyboardHook _hook = new();
    private readonly TypingEngine _engine;
    private readonly HashSet<int> _down = new();

    public TypingEngineTests()
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);

        var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        var gujarati = bundle.Languages.First(l => l.Id == "gu");
        var layout = bundle.GetLayout(gujarati.Layouts.First(l => l.Id == "gu-inscript"));

        _engine = new TypingEngine(_hook, new InputInjector());
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
    private bool EngineHandled(int vk)
    {
        var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
        var field = typeof(LowLevelKeyboardHook)
            .GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(_hook);
        handler!(_hook, args);
        return args.Handled;
    }

    [Fact]
    public void PlainLetterIsTyped()
    {
        Assert.True(EngineHandled(VkA), "a mapped key with no modifier is the engine's to translate");
    }

    [Fact]
    public void ShiftedLetterIsTyped()
    {
        _down.Add(VkShift);
        Assert.True(EngineHandled(VkA), "Shift selects the upper glyph; it is still text");
    }

    [Theory]
    [InlineData(VkControl)]   // Ctrl+A - select all
    [InlineData(VkMenu)]      // Alt+A - menu accelerator
    [InlineData(0x5B)]        // Win+A - Windows shortcut
    public void ShortcutsPassThroughToTheApplication(int modifier)
    {
        _down.Add(modifier);
        Assert.False(EngineHandled(VkA),
            $"modifier 0x{modifier:X2} makes this a command; the application must receive it");
    }

    [Fact]
    public void CtrlAltPassesThroughWhenTheLayoutHasNoAltGrGlyph()
    {
        // Ctrl+Alt is AltGr on European keyboards. None of our layouts define an
        // AltGr glyph, so the combination stays a shortcut.
        _down.Add(VkControl);
        _down.Add(VkMenu);
        Assert.False(EngineHandled(VkA));
    }

    [Fact]
    public void DisabledEngineTouchesNothing()
    {
        _engine.SetEnabled(false);
        Assert.False(EngineHandled(VkA));
    }
}
