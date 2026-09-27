using System.Reflection;
using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Engine;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>Every injected event, in order, so a test can count them.</summary>
internal sealed class CountingInjector : InputInjector
{
    private readonly StringBuilder _screen = new();

    public List<string> Ops { get; } = new();

    public string Screen => _screen.ToString();

    /// <summary>Keystrokes the target application has to process.</summary>
    public int Events => Ops.Sum(o => o.StartsWith("back ") ? int.Parse(o[5..]) : o.Length - 5);

    public override void SendText(string text) { Ops.Add("text " + text); _screen.Append(text); }

    public override void SendRichText(string text, string fontName) { Ops.Add("rich " + text); _screen.Append(text); }

    public override void SendBackspace(int count = 1)
    {
        Ops.Add("back " + count);
        _screen.Length = Math.Max(0, _screen.Length - count);
    }

    public override void Run(Action work) => work();
}

/// <summary>
/// What a keystroke costs the machine it is typed on.
///
/// With a legacy font layout the engine re-converts the whole word on every key -
/// it has to, because a matra can change the legacy bytes of everything before it
/// - but it must only send what actually changed. Sending the whole word instead
/// cost twenty injected events per keystroke deep into a word, which a slow laptop
/// showed as lag and as the word visibly rewriting itself.
/// </summary>
// One keyboard hook, one static key-state probe: these two classes share both,
// so they must not run at the same time.
[Collection("typing engine")]
public class InjectionCostTests : IDisposable
{
    private const int VkK = 0x4B, VkM = 0x4D, VkL = 0x4C, VkA = 0x41;

    private readonly LowLevelKeyboardHook _hook = new();
    private readonly CountingInjector _injector = new();
    private readonly AssetBundle _config;
    private readonly TypingEngine _engine;

    public InjectionCostTests()
    {
        TypingEngine.KeyDownProbe = _ => false;
        _config = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        var hindi = _config.Languages.First(l => l.Id == "hi");
        _engine = new TypingEngine(_hook, _injector);
        _engine.SetProfile(_config.GetLayout(hindi.Layouts.First(l => l.Name == "InScript")),
                           hindi.Name, "Unicode");
        _engine.SetEnabled(true);
    }

    private void Press(int vk)
    {
        var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
        var field = typeof(LowLevelKeyboardHook)
            .GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(_hook);
        handler!(_hook, args);
    }

    [Fact]
    public void LegacyKeystrokeSendsOnlyWhatChanged()
    {
        var legacy = _config.CreateConverterToLegacy("shree-devx");
        Assert.NotNull(legacy);
        _engine.SetLegacyConverter(legacy, "Shree-DevX-0001");

        // Four letters of one word: क म ल and the आ matra. The first puts its
        // character on screen; each of the next three should add to what is there,
        // not replace all of it. Four keystrokes that each retyped the word would
        // cost 1 + 3 + 5 + 7 = 16 events; only what changed costs about four.
        Press(VkK); Press(VkM); Press(VkL); Press(VkA);

        Assert.True(_injector.Screen.Length >= 4, "the word should be on screen: " + _injector.Screen);
        Assert.True(_injector.Events <= 8,
            $"four keystrokes cost {_injector.Events} injected events: {string.Join(" | ", _injector.Ops)}");
    }

    [Fact]
    public void UnicodeRunGrowsInsteadOfBeingRetyped()
    {
        // No legacy converter: plain Unicode, where a multi-key run is replaced as
        // it grows. Growing it must not retype the part already on screen.
        Press(VkK); Press(VkM); Press(VkL); Press(VkA);

        Assert.True(_injector.Events <= 8,
            $"four keystrokes cost {_injector.Events} injected events: {string.Join(" | ", _injector.Ops)}");
    }

    public void Dispose() => _engine.Dispose();
}
