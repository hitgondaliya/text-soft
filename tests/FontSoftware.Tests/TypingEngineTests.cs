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

    // The real injector hands this to its own thread so the keyboard hook can
    // return; a test wants the letters on the page before it looks at them.
    public override void Run(Action work) => work();

    /// <summary>How many times the engine asked for the held Ctrl to be let go.</summary>
    public int Lifts { get; private set; }

    public override void LiftHeldCtrl() => Lifts++;
}

/// <summary>
/// The engine decides, per key, whether it owns the keystroke or the application
/// does, and how the keys typed so far combine. Both are invisible in a
/// screenshot and very visible to a typist: Ctrl+A used to type a letter instead
/// of selecting all, and "y" then "t" on the ITR Gujarati keyboard used to leave
/// અ ા on screen where the vendor keyboard gives આ.
/// </summary>
// One keyboard hook, one static key-state probe: these two classes share both,
// so they must not run at the same time.
[Collection("typing engine")]
public class TypingEngineTests : IDisposable
{
    private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12, VkSpace = 0x20,
                      Vk1 = 0x31, VkA = 0x41, VkY = 0x59, VkT = 0x54, VkU = 0x55,
                      VkF = 0x46, VkO = 0x4F, VkI = 0x49, VkZ = 0x5A, VkD = 0x44,
                      VkV = 0x56, VkK = 0x4B, VkS = 0x53, VkN = 0x4E, VkG = 0x47, VkM = 0x4D,
                      VkL = 0x4C, VkE = 0x45, VkJ = 0x4A, VkX = 0x58, VkLControl = 0xA2;

    private readonly LowLevelKeyboardHook _hook = new();
    private readonly RecordingInjector _injector = new();
    private readonly AssetBundle _config;
    private readonly TypingEngine _engine;
    private readonly HashSet<int> _down = new();

    public TypingEngineTests()
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);

        _config = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        var layout = _config.GetLayout(gujarati.Layouts.First(l => l.Id == "guj-itr"));

        _engine = new TypingEngine(_hook, _injector);
        _engine.SetProfile(layout, gujarati.Name, "Unicode");
        _engine.SetEnabled(true);
    }

    /// <summary>
    /// The keys the vendor's charts put on the shifted letters are as much a part
    /// of a keyboard as the plain ones, and a build that shipped them empty typed
    /// nothing at all there: Shift+D on the Gujarati Typewriter keyboard is the
    /// half ka, not silence.
    /// </summary>
    [Fact]
    public void ShiftedLettersTypeWhatTheChartPrints()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        _engine.SetProfile(_config.GetLayout(gujarati.Layouts.First(l => l.Name == "Typewriter")),
                           gujarati.Name, "Unicode");

        _down.Add(VkShift);
        Assert.True(Press(VkD), "a mapped key belongs to the engine");
        _down.Remove(VkShift);

        Assert.Equal("ક્‍", _injector.Text);
    }

    /// <summary>Switch to a keyboard that has the vendor's third layer.</summary>
    private void UseHindiItr()
    {
        var hindi = _config.Languages.First(l => l.Id == "hi");
        _engine.SetProfile(_config.GetLayout(hindi.Layouts.First(l => l.Id == "dev-itr")),
                           hindi.Name, "Unicode");
    }

    /// <summary>Press the escape combination the vendor keyboards use.</summary>
    private void Escape(int vk = VkSpace, bool shift = false)
    {
        _down.Add(VkControl);
        if (shift) _down.Add(VkShift);
        Assert.True(Press(vk), "the escape belongs to the engine, not to the application");
        Send(vk, down: false);
        _down.Remove(VkControl);
        if (shift) _down.Remove(VkShift);
    }

    public void Dispose()
    {
        TypingEngine.KeyDownProbe = vk => false;
        _engine.Dispose();
        _hook.Dispose();
    }

    /// <summary>Feed the engine a key press the way the hook would, and report whether it took it.</summary>
    private bool Press(int vk) => Send(vk, down: true);

    /// <summary>Any key event: down or up, the typist's or one of our own injected ones.</summary>
    private bool Send(int vk, bool down, bool injected = false, bool lift = false)
    {
        var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down, Injected = injected || lift, Lift = lift };
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
        Escape();                         // the Gujarati keyboards have no third layer
        Assert.Equal("અ", _injector.Text);

        Press(VkT);                       // no longer part of the run, so it is the bare matra
        Assert.Equal("અા", _injector.Text);
    }

    [Fact]
    public void ALetterKeyTheKeyboardDoesNotUseTypesNothing()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        _engine.SetProfile(_config.GetLayout(gujarati.Layouts.First(l => l.Id == "gu-inscript")),
                           gujarati.Name, "Unicode");

        // Shree's own Gujarati InScript leaves z empty; typing an English z into
        // Gujarati text is never what the typist meant.
        Assert.True(Press(VkZ), "the engine owns the key, it just types nothing");
        Assert.Equal("", _injector.Text);
    }

    // ---- the two layers past the printed keyboard ----

    [Fact]
    public void CtrlSpaceReachesTheKeyboardsOwnThirdLayer()
    {
        UseHindiItr();
        Press(VkU);                       // u on its own is the e-matra
        Assert.Equal("े", _injector.Text);

        Escape();
        Assert.True(Press(VkU));          // and on this keyboard's third layer it is shra
        Assert.Equal("ेश्र", _injector.Text);
    }

    [Fact]
    public void CtrlShiftOneReachesTheSharedSymbols()
    {
        UseHindiItr();
        Escape(Vk1, shift: true);         // the layer the charts print as "Ctrl+shift+1, u"
        Assert.True(Press(VkU));
        Assert.Equal("ॐ", _injector.Text);
    }

    [Fact]
    public void TheThirdLayerCarriesTheConjuncts()
    {
        UseHindiItr();
        Escape();
        Press(VkI);                       // i is the ai-matra; on the third layer it is त्र
        Assert.Equal("त्र", _injector.Text);
    }

    [Fact]
    public void AKeyTheThirdLayerDoesNotDefineTypesNormally()
    {
        UseHindiItr();
        Escape();
        Assert.True(Press(VkY));          // nothing on the layer, so y is still अ
        Assert.Equal("अ", _injector.Text);
    }

    [Fact]
    public void TheEscapeLastsForOneKeyOnly()
    {
        UseHindiItr();
        Escape();
        Press(VkU);                       // श्र
        Press(VkU);                       // back to the keyboard
        Assert.Equal("श्रे", _injector.Text);
    }

    [Fact]
    public void TheEscapeStartsItsOwnRunAfterALetter()
    {
        UseHindiItr();
        Press(VkF);                       // क
        Escape();
        Press(VkU);                       // "fØu" is not a run of its own, so this is क then श्र
        Assert.Equal("कश्र", _injector.Text);
    }

    /// <summary>The Gujarati keyboards do have a third layer, and it has to type.</summary>
    [Fact]
    public void CtrlSpaceTypesOnGujaratiInScript()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        _engine.SetProfile(_config.GetLayout(gujarati.Layouts.First(l => l.Name == "InScript")),
                           gujarati.Name, "Unicode");
        Escape();
        Assert.True(Press(VkV));          // Ctrl+Space then v is the vocalic RR
        Assert.Equal("ૠ", _injector.Text);
    }

    private void UseGujarati(string keyboard, bool bySound = false)
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        if (bySound) _engine.SetTransliterator(_config.GetTransliterator(gujarati));
        _engine.SetProfile(_config.GetLayout(gujarati.Layouts.First(l => l.Name == keyboard)),
                           gujarati.Name, "Unicode");
    }

    /// <summary>
    /// Ctrl+Space is a chord, and most typists keep Ctrl down through the next key.
    /// Shree types the layer's letter all three ways of pressing it; this used to
    /// see Ctrl+V, hand it to Windows, and the typist's clipboard was pasted into
    /// the document instead of ૠ.
    /// </summary>
    [Fact]
    public void CtrlHeldThroughTheNextKeyStillTypesTheLayer()
    {
        UseGujarati("InScript");
        _down.Add(VkControl);
        Assert.True(Press(VkSpace));
        Assert.True(Press(VkV), "with Ctrl still down, V after Ctrl+Space is the layer's key, not Ctrl+V");
        _down.Remove(VkControl);
        Assert.Equal("ૠ", _injector.Text);
    }

    /// <summary>...and a key that has nothing on the layer is still the shortcut it looks like.</summary>
    [Fact]
    public void CtrlHeldOnAKeyTheLayerDoesNotHaveIsStillAShortcut()
    {
        UseGujarati("InScript");
        _down.Add(VkControl);
        Assert.True(Press(VkSpace));
        Assert.False(Press(VkJ), "Ctrl+J with nothing on the layer belongs to the application");
        _down.Remove(VkControl);
        Assert.Equal("", _injector.Text);
    }

    /// <summary>Pressed twice, the escape takes itself back - Shree then types the plain key.</summary>
    [Fact]
    public void CtrlSpaceTwiceTakesTheEscapeBack()
    {
        UseGujarati("InScript");
        Escape();
        Escape();
        Assert.True(Press(VkV));
        Assert.Equal("ન", _injector.Text);    // InScript v, not ૠ
    }

    /// <summary>
    /// The Eng keyboard has the same extra layers as every other, and the escape
    /// used to be treated as a shortcut there: Ctrl+Space went to the application
    /// and the key after it was typed by sound.
    /// </summary>
    [Fact]
    public void CtrlSpaceWorksOnTheEngKeyboard()
    {
        UseGujarati("Eng", bySound: true);
        Escape();
        Assert.True(Press(VkU));
        Assert.Equal("ૐ", _injector.Text);
    }

    /// <summary>A run on the layer grows like any other, and sound typing picks up after it.</summary>
    [Fact]
    public void AnEscapedRunOnTheEngKeyboardGrowsThenHandsBackToSound()
    {
        UseGujarati("Eng", bySound: true);
        Escape();
        foreach (int vk in new[] { VkN, VkG, VkA }) Press(vk);     // Ønga -> ઙ્ગ
        foreach (int vk in new[] { VkK, VkA }) Press(vk);          // then ka by sound
        Assert.Equal("ઙ્ગક", _injector.Text);
    }

    /// <summary>Ctrl held through the key works on the Eng keyboard too.</summary>
    [Fact]
    public void CtrlHeldOnTheEngKeyboardTypesTheLayer()
    {
        UseGujarati("Eng", bySound: true);
        foreach (int vk in new[] { VkK, VkA }) Press(vk);          // ક
        _down.Add(VkControl);
        Press(VkSpace);
        Assert.True(Press(VkE));                                   // Øe -> the candra e
        _down.Remove(VkControl);
        Assert.Equal("કૅ", _injector.Text);
    }

    /// <summary>
    /// A legacy font draws its own encoding. The Eng keyboard used to type Unicode
    /// into it regardless, and every letter came out as the wrong glyph.
    /// </summary>
    [Fact]
    public void TheEngKeyboardTypesTheLegacyEncodingInALegacyFont()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        UseGujarati("Eng", bySound: true);
        var toLegacy = _config.CreateConverterToLegacy("shree-gujx")!;
        _engine.SetLegacyConverter(toLegacy, "SHREE-GUJX-1120");

        foreach (int vk in new[] { VkK, VkA, VkM, VkA, VkL, VkA }) Press(vk);   // kamala
        Assert.Equal(toLegacy.ConvertText("કમલ"), _injector.Text);
        Assert.DoesNotContain(_injector.Text, c => c is >= '\u0A80' and <= '\u0AFF');
    }

    // ---- the Ctrl the typist holds ----
    // Before it types, the real injector lets go of a held Ctrl (tagged as a lift)
    // and never presses it back, so Windows then reads Ctrl as up while the finger
    // is still on it. WeLiftCtrl models exactly that.

    private void HoldCtrl() { _down.Add(VkControl); _down.Add(VkLControl); Send(VkLControl, down: true); }
    private void WeLiftCtrl() { Send(VkLControl, down: false, lift: true); _down.Remove(VkControl); _down.Remove(VkLControl); }
    private void ReleaseCtrl() { Send(VkLControl, down: false); _down.Remove(VkControl); _down.Remove(VkLControl); }

    /// <summary>A second Ctrl+Space on the same hold is an escape again.</summary>
    [Fact]
    public void TwoEscapesOnOneHoldOfCtrlBothWork()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Send(VkSpace, down: false);
        Assert.True(Press(VkV));
        WeLiftCtrl();
        Send(VkV, down: false);
        Assert.True(Press(VkSpace), "the finger is still on Ctrl: this is Ctrl+Space");
        Assert.True(Press(VkV));
        ReleaseCtrl();
        Assert.Equal("ૠૠ", _injector.Text);
    }

    /// <summary>
    /// A layer key held past the repeat delay repeats with Ctrl still down. Those
    /// repeats are not the typist asking for Ctrl+V: they are swallowed, and the
    /// clipboard is never pasted.
    /// </summary>
    [Fact]
    public void AHeldLayerKeyDoesNotRepeatAsAShortcut()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Press(VkV);                                   // ૠ
        Assert.True(Press(VkV), "a repeat of the layer key belongs to us, not to the application as Ctrl+V");
        Assert.True(Press(VkV));
        Send(VkV, down: false);
        ReleaseCtrl();
        Assert.Equal("ૠ", _injector.Text);
    }

    /// <summary>The Typewriter chart's ØxØdx, typed holding Ctrl through the first Space, X and the second Space.</summary>
    [Fact]
    public void AChartRunWithTwoEscapesTypedOnOneHold()
    {
        UseGujarati("Typewriter");
        HoldCtrl();
        Press(VkSpace);
        Send(VkSpace, down: false);
        Press(VkX);                                   // ઙ
        WeLiftCtrl();
        Send(VkX, down: false);
        Press(VkSpace);
        ReleaseCtrl();
        Press(VkD);
        Press(VkX);                                   // ØxØdx -> ઙ્ગ
        Assert.Equal("ઙ્ગ", _injector.Text);
    }

    /// <summary>
    /// Space held past the repeat delay during Ctrl+Space repeats; the repeat is
    /// not the second press that takes the escape back, so V is still ૠ - not
    /// Ctrl+V pasting the clipboard.
    /// </summary>
    [Fact]
    public void AHeldEscapeKeyDoesNotTakeItselfBack()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Press(VkSpace);                               // auto-repeat, no key-up between
        Press(VkSpace);
        Send(VkSpace, down: false);
        Assert.True(Press(VkV), "the layer is still open");
        ReleaseCtrl();
        Assert.Equal("ૠ", _injector.Text);
    }

    /// <summary>A real second press - Space let go and pressed again - still takes the escape back.</summary>
    [Fact]
    public void ASecondRealPressStillTakesTheEscapeBack()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Send(VkSpace, down: false);
        Press(VkSpace);
        Send(VkSpace, down: false);
        ReleaseCtrl();
        Assert.True(Press(VkV));
        Assert.Equal("ન", _injector.Text);
    }

    /// <summary>
    /// After we have let go of the held Ctrl, a shortcut on that same hold would
    /// reach the application as a plain letter; it is swallowed, not typed.
    /// </summary>
    [Fact]
    public void AShortcutOnACtrlWeLetGoOfIsNotTypedAsALetter()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Send(VkSpace, down: false);
        Press(VkV);
        WeLiftCtrl();
        Send(VkV, down: false);
        Assert.True(Press(VkJ), "J is not passed on without its Ctrl");
        ReleaseCtrl();
        Assert.Equal("ૠ", _injector.Text);
        Assert.True(Press(VkJ), "after the finger lets go, J is the keyboard's again");
        Assert.Equal("ૠર", _injector.Text);
    }

    /// <summary>
    /// Our lift arriving after the finger already let go (it was on its way) must
    /// not leave a phantom Ctrl behind: plain Space is then a space, not an escape.
    /// </summary>
    [Fact]
    public void ALiftAfterTheFingerLetGoLeavesNoPhantomCtrl()
    {
        UseGujarati("InScript");
        HoldCtrl();
        ReleaseCtrl();                                // the real key-up comes first...
        Send(VkLControl, down: false, lift: true);    // ...then our lift, with nothing left to lift
        Assert.False(Press(VkSpace), "Space with no Ctrl is the application's");
    }

    /// <summary>
    /// Only a key of a Ctrl+Space run typed under the escape's Ctrl has that Ctrl
    /// let go. A Ctrl pressed for a shortcut is never touched, or the next
    /// shortcut on it would be lost.
    /// </summary>
    [Fact]
    public void OnlyALayerKeyUnderCtrlLiftsIt()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Send(VkSpace, down: false);
        Press(VkV);                                   // layer key under Ctrl: lift
        Send(VkV, down: false);
        Assert.Equal(1, _injector.Lifts);
        Assert.False(Press(VkJ), "a shortcut after the run is the application's");
        Assert.Equal(1, _injector.Lifts);
        ReleaseCtrl();
        Press(VkK);                                   // no Ctrl at all: nothing to lift
        Assert.Equal(1, _injector.Lifts);
    }

    /// <summary>Ctrl+Backspace belongs to the application (delete a word), not to our cluster backspacing.</summary>
    [Fact]
    public void CtrlBackspaceIsTheApplications()
    {
        UseGujarati("InScript");
        Press(VkK);                                   // ક
        Press(VkJ);                                   // ર
        HoldCtrl();
        Assert.False(Press(0x08), "Ctrl+Backspace goes to the application");
        ReleaseCtrl();
        Assert.Equal(0, _injector.Lifts);
    }

    /// <summary>A shortcut on the same hold, once the layer run is over, is the application's.</summary>
    [Fact]
    public void AShortcutOnTheSameHoldAfterALayerKeyIsTheApplications()
    {
        UseGujarati("InScript");
        HoldCtrl();
        Press(VkSpace);
        Press(VkV);
        Send(VkV, down: false);
        Assert.False(Press(VkJ), "Ctrl+J goes to the application");
        ReleaseCtrl();
        Assert.Equal("ૠ", _injector.Text);
    }

    /// <summary>
    /// A run on the layer longer than one key, Ctrl held all the way (Roma: Ctrl+Space j f k is ઙ્ક).
    /// Only the first key used to count; f then went to the application as Ctrl+F.
    /// </summary>
    [Fact]
    public void ALongerRunOnTheLayerTypedWithCtrlHeld()
    {
        UseGujarati("Roma");
        HoldCtrl();
        Press(VkSpace);
        Assert.True(Press(VkJ));
        Assert.True(Press(VkF), "f continues the run; it is not Ctrl+F");
        Assert.True(Press(VkK), "k finishes it; it is not Ctrl+K");
        ReleaseCtrl();
        Assert.Equal("ઙ્ક", _injector.Text);
    }

    /// <summary>
    /// Eng keyboard, legacy font: letting go of Space in the middle of a run on the
    /// layer (ordinary rollover) used to end the word and empty the legacy buffer,
    /// and the run's text was then typed a second time.
    /// </summary>
    [Fact]
    public void AKeyUpInTheMiddleOfAnEscapedRunChangesNothing()
    {
        UseGujarati("Eng", bySound: true);
        var toLegacy = _config.CreateConverterToLegacy("shree-gujx")!;
        _engine.SetLegacyConverter(toLegacy, "SHREE-GUJX-1120");

        HoldCtrl();
        Press(VkSpace);
        Press(VkN);                                   // Øn -> ઙ્
        Send(VkSpace, down: false);                   // Space comes up first
        ReleaseCtrl();
        Send(VkN, down: false);
        Press(VkA);                                   // Øna -> ઙ
        Assert.Equal(toLegacy.ConvertText("ઙ"), _injector.Text);
    }

    /// <summary>A second escape inside a run on the Eng keyboard (Kannada ØmØma, ಞ್ಞ) in a legacy font.</summary>
    [Fact]
    public void ASecondEscapeInsideARunKeepsTheRunsText()
    {
        var kannada = _config.Languages.First(l => l.Id == "kn");
        _engine.SetTransliterator(_config.GetTransliterator(kannada));
        _engine.SetProfile(_config.GetLayout(kannada.Layouts.First(l => l.Name == "Eng")), kannada.Name, "Legacy");
        var toLegacy = _config.CreateConverterToLegacy("shree-kanx")!;
        _engine.SetLegacyConverter(toLegacy, "SHREE-KANX-0850");

        Escape();
        Press(VkM);
        Escape();
        Press(VkM);
        Press(VkA);
        Assert.Equal(toLegacy.ConvertText("ಞ್ಞ"), _injector.Text);
    }

    /// <summary>
    /// Keys that only looked like the start of a longer run on the layer are letters
    /// after all when it does not come: Ctrl+Space n g i is ઙ્ then ગિ, not ઙ્ઇ.
    /// </summary>
    [Fact]
    public void KeysThatStartedARunThatNeverCameAreTypedBySound()
    {
        UseGujarati("Eng", bySound: true);
        Escape();
        foreach (int vk in new[] { VkN, 0x47 /* g */, VkI }) Press(vk);
        Assert.Equal("ઙ્ગિ", _injector.Text);
    }

    /// <summary>Choosing the Eng keyboard is choosing to type by sound.</summary>
    [Fact]
    public void TheEngKeyboardTypesBySound()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        _engine.SetTransliterator(_config.GetTransliterator(gujarati));
        _engine.SetProfile(_config.GetLayout(gujarati.Layouts.First(l => l.Name == "Eng")),
                           gujarati.Name, "Unicode");
        foreach (int vk in new[] { VkV, VkI, VkK, VkA, VkA, VkS, VkA }) Press(vk);
        Assert.Equal("વિકાસ", _injector.Text);   // વિકાસ
    }

    // ---- the order a typewriter keyboard is typed in (see TypingOrder) ----
    // The charts show the i-sign, the reph and the vowel signs on ka alone, and
    // the layouts used to carry exactly that: only ka came out right.

    /// <summary>Type a run of keys the way the charts write them ("rh", "O$c").</summary>
    private void TypeKeys(string keys)
    {
        foreach (char c in keys)
        {
            bool shift = char.IsUpper(c) || "~!@#$%^&*()_+{}|:<>?".IndexOf(c) >= 0 || c == (char)34;
            int vk = char.IsLetter(c) ? char.ToUpperInvariant(c)
                   : char.IsDigit(c) ? c
                   : ")!@#$%^&*(".IndexOf(c) is var d and >= 0 ? '0' + d
                   : c switch
                   {
                       ';' or ':' => 186, '=' or '+' => 187, ',' or '<' => 188, '-' or '_' => 189,
                       '.' or '>' => 190, '/' or '?' => 191, '`' or '~' => 192, '[' or '{' => 219,
                       ']' or '}' => 221, '|' => 220, (char)39 or (char)34 => 222, _ => 0,
                   };
            if (shift) _down.Add(VkShift);
            Press(vk);
            Send(vk, down: false);
            if (shift) _down.Remove(VkShift);
        }
    }

    private void UseHindi(string keyboard)
    {
        var hindi = _config.Languages.First(l => l.Id == "hi");
        _engine.SetProfile(_config.GetLayout(hindi.Layouts.First(l => l.Name == keyboard)), hindi.Name, "Unicode");
    }

    /// <summary>
    /// "rf" is કિ on the ITR keyboard: the i-sign is struck before its letter. It
    /// goes in behind every letter, not only ka - and behind the whole cluster.
    /// </summary>
    [Theory]
    [InlineData("rh", "રિ")]          // r then h used to type િર
    [InlineData("rf", "કિ")]
    [InlineData("rPg", "ન્યિ")]       // before a half letter: after the cluster it starts
    [InlineData("rfTf", "ક્કિ")]      // through a run's link key
    [InlineData("rhq", "રિૂ")]        // a sign after the letter stays after it
    public void TheISignStruckFirstGoesBehindItsLetter(string keys, string word)
    {
        UseGujarati("Itr");
        TypeKeys(keys);
        Assert.Equal(word, _injector.Text);
    }

    /// <summary>"fo" is ર્ક: the reph is struck after its letter, on every letter.</summary>
    [Theory]
    [InlineData("bo", "ર્મ")]
    [InlineData("dbo", "ગર્મ")]       // on the letter it follows, not the word
    [InlineData("rbo", "ર્મિ")]
    [InlineData("rPgo", "ર્ન્યિ")]
    public void TheRephStruckAfterGoesInFrontOfItsLetter(string keys, string word)
    {
        UseGujarati("Itr");
        TypeKeys(keys);
        Assert.Equal(word, _injector.Text);
    }

    [Fact]
    public void TheSameOrderHoldsOnTheHindiTypewriter()
    {
        UseHindi("Typewriter");
        TypeKeys("fj");
        Assert.Equal("रि", _injector.Text);
    }

    /// <summary>
    /// InScript's chart has "k$" for र्क as well, but InScript is typed in
    /// Unicode's order and its typists strike the reph first: धर्म is ध, र्, म.
    /// </summary>
    [Fact]
    public void InScriptKeepsTheRephWhereItIsTyped()
    {
        UseHindi("InScript");
        TypeKeys("O$c");
        Assert.Equal("धर्म", _injector.Text);
    }

    /// <summary>રૂ is ર then ૂ on every keyboard - on Modular too, whose ૂ key typed nothing alone.</summary>
    [Theory]
    [InlineData("Itr", "hq")]
    [InlineData("InScript", "jt")]
    [InlineData("Typewriter", "jw")]
    [InlineData("Newroma", "rU")]
    [InlineData("Modular", "jn")]
    public void RuTypesOnEveryKeyboard(string keyboard, string keys)
    {
        UseGujarati(keyboard);
        TypeKeys(keys);
        Assert.Equal("રૂ", _injector.Text);
    }

    /// <summary>Ctrl+Shift+1 then $ is the rupee sign, on the Gujarati keyboards as on every other script's.</summary>
    [Fact]
    public void CtrlShiftOneThenDollarIsTheRupeeInGujarati()
    {
        UseGujarati("Itr");
        Escape(Vk1, shift: true);
        TypeKeys("$");
        Assert.Equal("₹", _injector.Text);
    }

    /// <summary>On Newroma "e" is એ, and after a consonant its sign: "ke" is કે, so "re" is રે.</summary>
    [Theory]
    [InlineData("e", "એ")]
    [InlineData("re", "રે")]
    [InlineData("ri", "રિ")]
    public void AVowelKeyTypesItsSignAfterAConsonant(string keys, string word)
    {
        UseGujarati("Newroma");
        TypeKeys(keys);
        Assert.Equal(word, _injector.Text);
    }

    /// <summary>"ftu" is કો: ા then ે are one sign, on every letter.</summary>
    [Fact]
    public void TwoSignsTheChartJoinsAreJoinedOnEveryLetter()
    {
        UseGujarati("Itr");
        TypeKeys("htu");
        Assert.Equal("રો", _injector.Text);
    }

    /// <summary>A legacy font is sent the word in Unicode's order, converted - what the converter makes of રિ.</summary>
    [Fact]
    public void ALegacyFontGetsTheWordInOrder()
    {
        UseGujarati("Itr");
        var toLegacy = _config.CreateConverterToLegacy("gopika-guj")!;
        _engine.SetLegacyConverter(toLegacy, "Gopika");
        TypeKeys("rbo");
        Assert.Equal(toLegacy.ConvertText("ર્મિ"), _injector.Text);
    }

    /// <summary>Backspace takes back the last key struck, and the rest stays in order.</summary>
    [Fact]
    public void BackspaceTakesBackTheLastKeyStruck()
    {
        UseGujarati("Itr");
        TypeKeys("rbo");
        Assert.True(Press(0x08));
        Assert.Equal("મિ", _injector.Text);
    }

    [Fact]
    public void AKeyboardTypedInUnicodesOrderLearnsNoOrder()
    {
        var gujarati = _config.Languages.First(l => l.Id == "gu");
        Assert.False(_config.GetLayout(gujarati.Layouts.First(l => l.Name == "InScript")).Order.Active);
        Assert.True(_config.GetLayout(gujarati.Layouts.First(l => l.Name == "Itr")).Order.Active);
    }

    /// <summary>Whole words, struck the way an ITR typist strikes them.</summary>
    [Theory]
    [InlineData("r'Jm", "દિવસ")]
    [InlineData("rfkb;", "કિંમત")]
    [InlineData("\"bo", "ધર્મ")]
    [InlineData("rmT:h", "સ્થિર")]
    [InlineData("hq'", "રૂદ")]
    public void WordsTypeOnTheItrKeyboard(string keys, string word)
    {
        UseGujarati("Itr");
        TypeKeys(keys);
        Assert.Equal(word, _injector.Text);
    }
}
