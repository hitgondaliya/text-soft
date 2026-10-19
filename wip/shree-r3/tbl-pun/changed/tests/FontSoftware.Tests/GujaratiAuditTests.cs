using System.Reflection;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Models;
using FontSoftware.Engine;
using Xunit;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace FontSoftware.Tests;

/// <summary>
/// The fifteen bugs of the Gujarati audit of 2026-10-18 (r3\result-guj-audit.json), each from
/// its repro, with the bytes Shree's own composers wrote for it (S8GujU64 / S8Guj764, saved in
/// r3\guj-audit\results: composer-vendor-keys.json per key, composer-sequences.json per code
/// run) - never from TextSoft's own output. The engine is driven the way the keyboard hook
/// drives it; nothing touches the machine.
/// </summary>
[Collection("typing engine")]
public class GujaratiAuditTests : IDisposable
{
    private const int VkShift = 0x10, VkControl = 0x11, VkSpace = 0x20, VkBack = 0x08;

    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static AssetBundle Config => Assets.Value;
    private readonly HashSet<int> _down = new();

    public GujaratiAuditTests() => TypingEngine.KeyDownProbe = vk => _down.Contains(vk);

    public void Dispose() => TypingEngine.KeyDownProbe = vk => false;

    private sealed class Screen : InputInjector
    {
        public readonly StringBuilder Text = new();
        public int Sent;   // how many times the engine injected anything
        public override void SendText(string text) { Sent++; Text.Append(text); }
        public override void SendRichText(string text, string fontName) { Sent++; Text.Append(text); }
        public override void SendBackspace(int count = 1) { Sent++; Text.Length = Math.Max(0, Text.Length - count); }
        public override void Run(Action work) => work();
        public override void LiftHeldCtrl() { }
    }

    private static KeyboardLayout Layout(string language, string id) =>
        Config.GetLayout(Config.Languages.First(l => l.Id == language).Layouts.First(l => l.Id == id));

    /// <summary>
    /// Type keys in chart notation (Ø = Ctrl+Space, ¤ = Ctrl+Shift+1, \b = Backspace) on a fresh
    /// engine, and return what is on screen. A key the engine leaves alone is typed by the
    /// "application" as its own character (a space, a digit), and Backspace takes one character.
    /// </summary>
    private string Type(string language, string layoutId, string keys, string? encoding = null)
    {
        using var typist = new Typist(_down, language, layoutId, encoding);
        foreach (char c in keys) typist.Key(c);
        return typist.Screen.Text.ToString();
    }

    /// <summary>A fresh engine, driven the way the keyboard hook drives it, typing into a <see cref="Screen"/>.</summary>
    private sealed class Typist : IDisposable
    {
        private readonly HashSet<int> _down;
        private readonly LowLevelKeyboardHook _hook = new();
        private readonly TypingEngine _engine;
        private static readonly FieldInfo Field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        public readonly Screen Screen = new();
        public readonly List<bool> Handled = new();   // per key: did the engine take it (false: the application typed it)

        public Typist(HashSet<int> down, string language, string layoutId, string? encoding)
        {
            _down = down;
            _down.Clear();
            _engine = new TypingEngine(_hook, Screen);
            var lang = Config.Languages.First(l => l.Id == language);
            _engine.SetProfile(Layout(language, layoutId), lang.Name, "test");
            _engine.SetTransliterator(Config.GetTransliterator(lang));
            if (encoding is not null)
            {
                // A converter is built once per table: building one is most of what a fresh engine costs.
                lock (Converters)
                    if (!Converters.TryGetValue(encoding, out var conv)) Converters[encoding] = conv = Config.CreateConverterToLegacy(encoding)!;
                _engine.SetLegacyConverter(Converters[encoding], null);
            }
            _engine.SetEnabled(true);
        }

        private static readonly Dictionary<string, FontConverter> Converters = new();

        private bool Send(int vk, bool down)
        {
            var args = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down };
            ((EventHandler<HookKeyEventArgs>)Field.GetValue(_hook)!)(_hook, args);
            return args.Handled;
        }

        public void Key(char c)
        {
            if (c is 'Ø' or '¤')
            {
                _down.Add(VkControl);
                if (c == '¤') _down.Add(VkShift);
                int esc = c == 'Ø' ? VkSpace : '1';
                Handled.Add(Send(esc, true)); Send(esc, false);
                _down.Remove(VkControl); _down.Remove(VkShift);
                return;
            }
            var (vk, shift) = Vk(c);
            if (shift) _down.Add(VkShift);
            bool handled = Send(vk, true);
            Send(vk, false);
            _down.Remove(VkShift);
            Handled.Add(handled);
            if (!handled)
            {
                if (c == '\b') { if (Screen.Text.Length > 0) Screen.Text.Length--; }
                else Screen.Text.Append(c);
            }
        }

        /// <summary>A key pressed while <paramref name="mods"/> are held (Ctrl+S): true when the engine took it.</summary>
        public bool Chord(int vk, params int[] mods)
        {
            foreach (int m in mods) _down.Add(m);
            bool handled = Send(vk, true);
            Send(vk, false);
            foreach (int m in mods) _down.Remove(m);
            return handled;
        }

        public void Dispose()
        {
            _engine.Dispose();
            _hook.Dispose();
            _down.Clear();
        }
    }

    private static (int Vk, bool Shift) Vk(char c)
    {
        if (c is >= 'a' and <= 'z') return (char.ToUpperInvariant(c), false);
        if (c is >= 'A' and <= 'Z') return (c, true);
        if (c is >= '0' and <= '9') return (c, false);
        int i = ")!@#$%^&*(".IndexOf(c);
        if (i >= 0) return ('0' + i, true);
        return c switch
        {
            ';' => (186, false), ':' => (186, true), '=' => (187, false), '+' => (187, true), ',' => (188, false), '<' => (188, true),
            '-' => (189, false), '_' => (189, true), '.' => (190, false), '>' => (190, true), '/' => (191, false), '?' => (191, true),
            '`' => (192, false), '~' => (192, true), '[' => (219, false), '{' => (219, true), '\\' => (220, false), '|' => (220, true),
            ']' => (221, false), '}' => (221, true), '\'' => (222, false), '"' => (222, true), ' ' => (VkSpace, false), '\b' => (VkBack, false),
            _ => throw new ArgumentException("no key for " + c),
        };
    }

    /// <summary>
    /// Legacy text as the bytes a Windows-1252 document holds (the C1 code points as their own bytes); a
    /// character no byte holds - one a table passes through, P3b - as U+XXXX.
    /// </summary>
    private static string Bytes(string legacy)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var cp = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
        return string.Join(" ", legacy.Select(c =>
        {
            if (c is >= '\u0080' and <= '\u009F' && cp.GetString(cp.GetBytes(c.ToString())) != c.ToString()) return ((int)c).ToString("X2");
            try { return cp.GetBytes(c.ToString())[0].ToString("X2"); }
            catch (EncoderFallbackException) { return "U+" + ((int)c).ToString("X4"); }
        }));
    }

    private static string FromHex(string hex)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return hex.Length == 0 ? "" : Encoding.GetEncoding(1252).GetString(hex.Split(' ').Select(h => Convert.ToByte(h, 16)).ToArray());
    }

    private static FontConverter To(string enc) => Config.CreateConverterToLegacy(enc)!;
    private static FontConverter From(string enc) => Config.CreateConverter(enc, "shruti") ?? new FontConverter(Config.MappingTables.First(m => m.SourceFontId == enc && m.TargetType == FontType.Unicode));

    // ---------- BUG-01: Backspace takes back exactly the last keystroke, also inside a chart run ----------

    [Theory]
    // InScript k s is કે; Backspace leaves ક (the composer's ક = 4C 24), m d is સ્, Backspace leaves સ (6B).
    [InlineData("gu", "gu-inscript", "ks\b", "ક", "shree-guj7", "4C 24")]
    [InlineData("gu", "gu-inscript", "md\b", "સ", "shree-guj7", "6B")]
    [InlineData("gu", "gu-inscript", "ke\b", "ક", "shree-guj456", "4C 24")]
    [InlineData("gu", "guj-bpmk", "sf\b", "ક", "shree-guj7", "4C 24")]
    [InlineData("gu", "guj-phonetic", "ga\b", "ગ્", null, null)]
    public void BackspaceInsideARunTakesBackOnlyTheLastKey(string lang, string layout, string keys, string unicode, string? enc, string? bytes)
    {
        Assert.Equal(unicode, Type(lang, layout, keys));
        if (enc is not null) Assert.Equal(bytes, Bytes(Type(lang, layout, keys, enc)));
    }

    /// <summary>
    /// Every two- and three-key run of a chart, on keyboards of three scripts, in Unicode and in a
    /// Shree font: the run then Backspace is the run without its last key, and that key struck
    /// again gives the run back. It used to take the whole run (InScript k s Backspace left nothing).
    /// </summary>
    [Theory]
    [InlineData("gu", "gu-inscript", "shree-guj7")]
    [InlineData("gu", "guj-itr", "shree-guj7")]
    [InlineData("gu", "guj-typewrit", "shree-guj456")]
    [InlineData("gu", "guj-phonetic", "shree-guj7")]
    [InlineData("hi", "hi-inscript", "shree-dev7")]
    [InlineData("hi", "dev-itr", "shree-dev7")]
    [InlineData("bn", "bn-inscript", null)]
    public void EveryRunTakesBackOneKeyAtATime(string lang, string layoutId, string? enc)
    {
        var layout = Layout(lang, layoutId);
        var runs = layout.Sequences.Where(s => s.Keys.Length is 2 or 3 && s.Keys.All(c => c < 0x7F && c != ' ')).Select(s => s.Keys).Distinct().Take(120).ToList();
        Assert.NotEmpty(runs);
        var wrong = new List<string>();
        foreach (var e in new[] { null, enc }.Distinct())
            foreach (var run in runs)
            {
                string shorter = Type(lang, layoutId, run[..^1], e), whole = Type(lang, layoutId, run, e);
                string back = Type(lang, layoutId, run + "\b", e), again = Type(lang, layoutId, run + "\b" + run[^1], e);
                if (back != shorter || again != whole) wrong.Add($"{e ?? "Unicode"} {run}: Backspace gives [{back}], want [{shorter}]; again [{again}], want [{whole}]");
            }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(20)));
    }

    // ---------- BUG-02: the ru/ruu ligatures Shree's ru and ruu keys type ----------

    [Theory]
    // S8Guj764, composer-sequences.json runs2: E9 -> 7B 24, E9 69 -> 7B 84 24, 7E -> EA 24, 7E 69 -> EA 84 24,
    // EA -> 7E 24, EA 69 -> 7E 84 24, E9 40 -> 7B AE 24, 7E 40 -> EA AE 24, E9 D4 -> 7B 24 3A, 7E D4 -> EA 24 3A.
    [InlineData("રુ", "7B 24")]
    [InlineData("રું", "7B 84 24")]
    [InlineData("રૂ", "EA 24")]
    [InlineData("રૂં", "EA 84 24")]
    [InlineData("રૂ︀", "7E 24")]
    [InlineData("રૂ︀ં", "7E 84 24")]
    [InlineData("ર્રુ", "7B AE 24")]
    [InlineData("ર્રૂ", "EA AE 24")]
    [InlineData("રુઃ", "7B 24 3A")]
    [InlineData("રૂઃ", "EA 24 3A")]
    public void RuAndRuuAreTheLigaturesShreeTypes(string unicode, string bytes)
    {
        foreach (var enc in new[] { "shree-guj7", "shree-guj456" })
        {
            Assert.Equal(bytes, Bytes(To(enc).ConvertText(unicode)));
            Assert.Equal(unicode, From(enc).ConvertText(FromHex(bytes)));
        }
    }

    [Theory]
    // The keys (composer-vendor-keys.json): ITR Shift+H and InScript Shift+J hold code 7E (EA 24),
    // Typewriter Shift+3 code E9 (7B 24), Typewriter Shift+; code EA (7E 24).
    [InlineData("guj-itr", "H", "EA 24")]
    [InlineData("gu-inscript", "J", "EA 24")]
    [InlineData("guj-typewrit", "#", "7B 24")]
    [InlineData("guj-typewrit", ":", "7E 24")]
    public void TheRuAndRuuKeysTypeTheLigatures(string layout, string keys, string bytes) =>
        Assert.Equal(bytes, Bytes(Type("gu", layout, keys, "shree-guj7")));

    [Fact]
    public void RuOnARaSignStaysTheRaSign()
    {
        // ક્રુ (the composer's BE 79 24) is ક with the ra-sign and u, never the ru ligature.
        Assert.Equal("BE 79 24", Bytes(To("shree-guj7").ConvertText("ક્રુ")));
        Assert.Equal("4E 79 7B 24", Bytes(To("shree-guj7").ConvertText("ગુરુ")));
    }

    // ---------- BUG-03: a .docx keeps English in its own font ----------

    [Fact]
    public void ADocxKeepsEnglishRunsInTheirOwnFont()
    {
        string dir = Path.Combine(Path.GetTempPath(), "textsoft-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "mixed.docx"), output = Path.Combine(dir, "out.docx");
            using (var doc = WordprocessingDocument.Create(input, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body(new Paragraph(
                    new Run(new RunProperties(new RunFonts { Ascii = "Nirmala UI", HighAnsi = "Nirmala UI" }), new Text("ગુજરાત સરકાર ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new RunProperties(new Bold(), new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }), new Text("Email: info@gujarat.gov.in [1] & \"Notice\" #5 ") { Space = SpaceProcessingModeValues.Preserve })),
                    new Paragraph(new Run(new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }), new Text("ગુરુ Email 2026")))));
            }
            var to7 = To("shree-guj7");
            FileConversion.ConvertFile(input, output, t => to7.ConvertText(t), true, false, new System.Windows.Media.FontFamily("Arial"), "SHREE-GUJ7-0750", false);

            using var outDoc = WordprocessingDocument.Open(output, false);
            var runs = outDoc.MainDocumentPart!.Document.Body!.Descendants<Run>().Select(r => (Font: r.RunProperties?.RunFonts?.Ascii?.Value, Text: string.Concat(r.Elements<Text>().Select(t => t.Text)))).ToList();
            // The Gujarati run: converted, in the Shree font.
            Assert.Contains(runs, r => r.Font == "SHREE-GUJ7-0750" && r.Text == to7.ConvertText("ગુજરાત સરકાર "));
            // The English run: its own text, its own font (it used to be stamped SHREE-GUJ7-0750, drawn as ઊળફશલ...).
            Assert.Contains(runs, r => r.Font == "Arial" && r.Text == "Email: info@gujarat.gov.in [1] & \"Notice\" #5 ");
            // A run that mixes them is split: ગુરુ converted, " Email 2026" left in Calibri.
            Assert.Contains(runs, r => r.Font == "SHREE-GUJ7-0750" && r.Text == to7.ConvertText("ગુરુ"));
            Assert.Contains(runs, r => r.Font == "Calibri" && r.Text == " Email 2026");
            Assert.DoesNotContain(runs, r => r.Font == "SHREE-GUJ7-0750" && (r.Text.Contains("Email") || r.Text.Contains("Notice") || r.Text.Contains('@')));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void RichTextKeepsEnglishRunsInTheirOwnFont()
    {
        var to7 = To("shree-guj7");
        var arial = new System.Windows.Media.FontFamily("Arial");
        var doc = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("ગુજરાત Email")));
        ((System.Windows.Documents.Paragraph)doc.Blocks.FirstBlock).Inlines.Add(new System.Windows.Documents.Run(" Notice") { FontFamily = arial });
        var target = new System.Windows.Media.FontFamily("SHREE-GUJ7-0750");
        var outDoc = RichTextConversion.Convert(doc, t => to7.ConvertText(t), target, null, false, new RunScope(true, null));
        var runs = RichTextConversion.Runs(outDoc).Select(r => (Font: r.FontFamily.Source, r.Text)).ToList();
        Assert.Contains(runs, r => r.Font == "SHREE-GUJ7-0750" && r.Text == to7.ConvertText("ગુજરાત"));
        Assert.Contains(runs, r => r.Font != "SHREE-GUJ7-0750" && r.Text == " Email");
        Assert.Contains(runs, r => r.Font == "Arial" && r.Text == " Notice");
    }

    // ---------- BUG-04: the Eng keyboard types what Shree's ENG.GUJ types ----------

    [Theory]
    // composer-vendor-keys.json ENG, S8Guj764 bytes per key (G ૰ is 8F there, which Word draws as nothing:
    // passed through as itself, P3b; > is EE = 90, likewise invisible: the hyphen).
    [InlineData("x", "ક્ષ", "6E")]
    [InlineData("z", "ઝ", "54")]
    [InlineData("I", "ઇ", "42")]
    [InlineData("Y", "્ય", "E9")]
    [InlineData("<", "ઽ", "40")]
    [InlineData("@", "રુ", "7B 24")]
    [InlineData("#", "રૂ", "EA 24")]
    [InlineData("$", "રૂ︀", "7E 24")]
    [InlineData("&", "।", "97 26")]
    [InlineData("~", "ૃ", "A9")]
    [InlineData(">", "‐", "2D")]
    [InlineData("1", "૧", "31")]
    [InlineData("B", "", "")]                 // Shree types nothing (FF) - it used to type a Latin B
    [InlineData("q", "", "")]
    [InlineData("[", "", "")]
    [InlineData("laxmi", "લક્ષ્મિ", null)]
    public void TheEngKeyboardTypesWhatShreesEngTypes(string keys, string unicode, string? bytes)
    {
        Assert.Equal(unicode, Type("gu", "guj-eng", keys));
        if (bytes is not null) Assert.Equal(bytes, Bytes(Type("gu", "guj-eng", keys, "shree-guj7")));
    }

    // ---------- BUG-05, 06, 09, 11, 12 and P2-P4: the tables ----------

    [Theory]
    [InlineData("[", "", true)]             // draws the i-sign: left out, reported
    [InlineData("#", "", true)]             // draws ×
    [InlineData("A", "", true)]             // a Latin letter's code draws અ
    [InlineData("<", "", true)]             // no glyph, and Shree's rupee code
    [InlineData("\"", "22 22", false)]      // the fonts' look-alike: two single quotes
    [InlineData(":", "3A", false)]          // the two dots of a colon
    [InlineData(".", "2E", false)]          // drawn as itself: written, not reported
    [InlineData("1", "31", false)]          // the script's own digit, as Shree writes digits
    [InlineData("–", "2D", false)]          // P4: Shree's 81 is drawn as nothing by Word
    [InlineData("—", "2D 2D", false)]
    [InlineData("‐", "2D", false)]          // the short dash (code EE, byte 90 - invisible in Word)
    [InlineData("÷", "", true)]             // 8D: invisible in Word, no other code - and its own code point F7 is a letter of the fonts
    [InlineData("૰", "U+0AF0", true)]       // 8F: likewise, but ૰ is no code of the fonts: itself, Word's fallback draws it (P3b)
    [InlineData("₹", "U+20B9", true)]       // Shree's 3C: no glyph in any installed GUJ7/GUJ font: itself (P3b)
    [InlineData("૱", "U+0AF1", true)]       // no glyph, no composer code: itself (P3b)
    [InlineData("ૢ", "U+0AE2", true)]       // the fonts have no vocalic-l sign: itself (P3b)
    [InlineData("\u0092", "", true)]        // a C1 control (’ mis-decoded): Word draws nothing, a .txt would write byte 92, થ
    [InlineData("\u0001", "", true)]        // a C0 control: left out, reported
    [InlineData("॥", "97 26 26", false)]    // two dandas, as Shree types D3 D3
    [InlineData("×", "23", false)]          // the fonts' own ×
    [InlineData("્નિ", "72 5F", false)]     // BUG-12: the marker U+FDD3 never reaches the bytes
    [InlineData("્ન઼", "9C 5F", false)]
    public void TheGujaratiTablesFollowTheOutputPolicy(string unicode, string bytes, bool reported)
    {
        foreach (var enc in new[] { "shree-guj7", "shree-guj456" })
        {
            var r = To(enc).Convert(unicode);
            Assert.Equal(bytes, Bytes(r.Text));
            Assert.Equal(reported, r.CannotShow.Count > 0);
            Assert.DoesNotContain(r.Text, c => c is >= '﷐' and <= '﷯');
        }
    }

    [Theory]
    // P5, BUG-09/10/11: Shree's own spellings read back (composer-sequences.json).
    [InlineData("4C B9 24 4C 24", "ક્‌ક")]        // 73 44 73: a halant typed inside a word
    [InlineData("5F B9 5F", "ન્‌ન")]
    [InlineData("66 B9 4C 24", "ર્‌ક")]           // not a reph on ક
    [InlineData("BC 20 4C 24", "ક્‍ ક")]          // a half form before a space
    [InlineData("4C 24 97 26 26", "ક॥")]
    [InlineData("3C 31 32", "₹૧૨")]               // F8 31 32
    [InlineData("4C 24 90 4C 24", "ક‐ક")]          // 73 EE 73
    [InlineData("4C 24 81 81 4C 24", "ક—ક")]       // 73 7B 73
    [InlineData("22 22 6C 70 27 27", "“હા”")]       // a double quote typed as two single quotes
    [InlineData("4C 24 F8 65 79 84 3A 20", "કહ્યું: ")] // after the anusvara 3A is the colon
    [InlineData("4C 24 3A", "કઃ")]                  // after a letter, the visarga
    [InlineData("7E 24", "રૂ︀")]
    public void ShreesSpellingsReadBack(string bytes, string unicode)
    {
        Assert.Equal(unicode, From("shree-guj7").ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From("shree-guj456").ConvertText(FromHex(bytes)));
    }

    [Theory]
    // Devanagari and Sanskrit: the policy items only (their syllables are as the composer writes them).
    [InlineData("shree-dev7", "–", "2D", false)]
    [InlineData("shree-dev7", "×", "", true)]
    [InlineData("shree-dev7", "÷", "", true)]
    [InlineData("shree-dev456", "—", "2D 2D", false)]
    [InlineData("shree-dev7", "ऎ", "45", true)]
    [InlineData("shree-dev7", "ॆ", "U+0946", true)]   // no short e sign in the fonts: itself (P3b)
    [InlineData("shree-dev7", "ॢ", "U+0962", true)]   // no glyph: itself (P3b)
    [InlineData("shree-san7", "ॡ", "U+0961", true)]
    [InlineData("shree-san7", "क\u093C", "40 A1", true)]   // no nukta in the Sanskrit fonts: क as the composer writes it, the nukta reported
    [InlineData("shree-dev7", "#", "", true)]
    [InlineData("shree-dev7", "'", "27", false)]
    [InlineData("shree-san7", "'", "92", false)]
    [InlineData("shree-san7", "%", "", true)]
    [InlineData("shree-san7", "(", "28", false)]
    public void TheDevanagariTablesFollowTheOutputPolicy(string enc, string unicode, string bytes, bool reported)
    {
        var r = To(enc).Convert(unicode);
        Assert.Equal(bytes, Bytes(r.Text));
        Assert.Equal(reported, r.CannotShow.Count > 0);
        Assert.DoesNotContain(r.Text, c => c is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D');
    }

    [Theory]
    [InlineData("shree-guj7")]
    [InlineData("shree-guj456")]
    [InlineData("shree-dev7")]
    [InlineData("shree-dev456")]
    [InlineData("shree-san7")]
    public void NoShreeTableWritesACodeWordDrawsAsNothing(string enc)
    {
        var table = Config.FindMapping("shruti", enc) ?? Config.MappingTables.First(m => m.TargetFontId == enc && m.TargetType == FontType.Legacy);
        Assert.DoesNotContain(table.Pairs, p => p.To.Any(c => c is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D'));
        Assert.DoesNotContain(table.PostRules, r => r.Replace.Any(c => c is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D'));
    }

    // ---------- BUG-05/06 in typing: the shared symbol layer ----------

    [Theory]
    [InlineData("¤|", "97 26 26")]     // ॥: two dandas
    [InlineData("¤$", "U+20B9")]       // ₹: no code of the fonts draws it - typed as itself, Word's fallback font draws it (P3b)
    [InlineData("¤[", "")]             // [ draws the i-sign: nothing
    public void TheSymbolLayerTypesWhatTheFontCanShow(string keys, string bytes) =>
        Assert.Equal(bytes, Bytes(Type("gu", "guj-itr", keys, "shree-guj7")));

    // ---------- BUG-07: a chart's "A or B" is two runs ----------

    [Fact]
    public void NoLayoutHasAnOrRun()
    {
        var bad = Config.Languages.SelectMany(l => l.Layouts).Select(r => Config.GetLayout(r)).Distinct()
            .SelectMany(l => l.Sequences.Where(s => s.Keys.Length > 2 && s.Keys.IndexOf("or", 1, StringComparison.Ordinal) is var at && at > 0 && at < s.Keys.Length - 2)
                                        .Select(s => $"{l.Id}: {s.Keys}"))
            .ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void TheRephAfterTtaLandsOnIt()
    {
        // ITR ; T ; is ત્ત, o the reph struck after it: ર્ત્ત = D1 AE (the composer's "fo" = 4C AE 24 pattern).
        // It used to be D1 66 B9 - ત્ત and a visible ર્ - because of the run ";T;orØo".
        Assert.Equal("ર્ત્ત", Type("gu", "guj-itr", ";T;o"));
        Assert.Equal("D1 AE 20", Bytes(Type("gu", "guj-itr", ";T;o ", "shree-guj7")));
    }

    // ---------- BUG-08: a run may start on a key that types nothing alone ----------

    [Theory]
    [InlineData("ka", "કા")]
    [InlineData("ba", "બ")]
    [InlineData("ja", "જ")]
    [InlineData("ga", "ગ")]
    [InlineData("baka", "બકા")]
    public void TheEklKeyboardTypes(string keys, string word) => Assert.Equal(word, Type("gu", "guj-ekl", keys));

    // ---------- BUG-09: code EE ----------

    [Theory]
    [InlineData("guj-itr", "Ø3")]
    [InlineData("guj-bpmk", "Ø3")]
    [InlineData("guj-typewrit", "Øp")]
    public void ShreesShortDashKeyTypesTheHyphen(string layout, string keys)
    {
        Assert.Equal("‐", Type("gu", layout, keys));
        Assert.Equal("2D", Bytes(Type("gu", layout, keys, "shree-guj7")));   // its own byte 90 is invisible in Word
    }

    // ---------- BUG-13: while a half form, link or reph waits for its letter ----------

    [Fact]
    public void AHalfLetterShowsTheHalfFormWhileItWaits()
    {
        // BPMK Shift+A: Shree shows E5 (the half બ) while it waits; the halant only once the word ends there.
        Assert.Equal("E5", Bytes(Type("gu", "guj-bpmk", "A", "shree-guj7")));
        Assert.Equal("62 B9 20", Bytes(Type("gu", "guj-bpmk", "A ", "shree-guj7")));
        // and the next letter joins it: બ્ + ક
        Assert.Equal(Bytes(To("shree-guj7").ConvertText("બ્ક")), Bytes(Type("gu", "guj-bpmk", "As", "shree-guj7")));
    }

    [Fact]
    public void TheRephKeyAloneShowsTheReph()
    {
        // ITR o (the reph, struck after its letter) alone: the composer shows AE; it used to be 66 B9.
        Assert.Equal("AE", Bytes(Type("gu", "guj-itr", "o", "shree-guj7")));
        Assert.Equal("AE", Bytes(Type("gu", "guj-bpmk", "\"", "shree-guj7")));    // BPMK's reph key, code 40
        // InScript $ (ra + virama, struck before its letter) alone: the composer shows 66 B9, and k after it is ર્ક.
        Assert.Equal("66 B9", Bytes(Type("gu", "gu-inscript", "$", "shree-guj7")));
        Assert.Equal("4C AE 24", Bytes(Type("gu", "gu-inscript", "$k", "shree-guj7")));
    }

    [Theory]
    // Shree's Eng keyboard, Ctrl+Space (composer-vendor-keys.json ENG layer 1): b and r are the old ruu
    // (code 7E = EA 24); k, s, 1 are empty (FF) - nothing, where they used to be typed by sound.
    [InlineData("Øb", "EA 24")]
    [InlineData("Ør", "EA 24")]
    [InlineData("Øk", "")]
    [InlineData("Øs", "")]
    [InlineData("Ø1", "")]
    public void TheEngKeyboardsThirdLayerIsShrees(string keys, string bytes) =>
        Assert.Equal(bytes, Bytes(Type("gu", "guj-eng", keys, "shree-guj7")));

    // ---------- BUG-15: empty vendor slots ----------

    [Theory]
    [InlineData("guj-itr", "Ø0", "")]          // Ctrl+Space 0: the slot is FF - nothing (it was ૦)
    [InlineData("guj-bpmk", "Øq", "")]
    [InlineData("guj-newroma", "$", "")]       // Shift+4: FF - nothing (it was ૪)
    [InlineData("guj-roma", "@", "")]
    [InlineData("gu-inscript", "z", " ")]      // code 20: Shree types a space
    public void EmptyVendorSlotsTypeWhatShreeTypes(string layout, string keys, string unicode) =>
        Assert.Equal(unicode, Type("gu", layout, keys));

    // ---------- BUG-14: the converter's fonts, and an old saved setting ----------

    [Fact]
    public void TheConverterOffersALegacyLayoutsOwnFontsOnly()
    {
        var vm = new ConverterViewModel(Config, new FontResolver(Path.Combine(AppContext.BaseDirectory, "config")));
        vm.SelectedLanguage = vm.Languages.First(l => l.Id == "gu");
        vm.SelectedOutputLayout = vm.OutputLayouts.First(o => o.Name == "Shree-Lipi 4.0/5.0/6.0");
        Assert.DoesNotContain(vm.OutputFonts, f => f.Name.StartsWith("SHREE-GUJ7-", StringComparison.OrdinalIgnoreCase));
        vm.SelectedOutputLayout = vm.OutputLayouts.First(o => o.Name == "Shree-Lipi 7.0");
        Assert.DoesNotContain(vm.OutputFonts, f => System.Text.RegularExpressions.Regex.IsMatch(f.Name, "^SHREE-GUJ-", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public void AnOldSavedFontIsMovedToItsOwnLayoutVisibly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "textsoft-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            // The committed build saved "Shree-Lipi 7.0 + SHREE-GUJ-0750" (its 7.0 row defaulted to the 4/5/6 face).
            var settings = new AppSettings { DefaultLanguageId = "gu", DefaultFontLayoutId = "gu-fl-shree-lipi-7-0", DefaultFontId = "shree-guj-0750", DefaultLayoutId = "guj-itr" };
            string root = Path.Combine(AppContext.BaseDirectory, "config");
            using var vm = new MainViewModel(new ConfigLoader(root).Load(), settings, new SettingsStore(dir), NullLog.Instance, new FontResolver(root));
            Assert.Equal("gu-fl-shree-lipi-4-0-5-0-6-0", vm.SelectedFontLayout?.Id);
            Assert.Equal("shree-guj-0750", vm.SelectedFont?.Id);
            Assert.Contains("Shree-Lipi 4.0/5.0/6.0", vm.SettingsNote);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ---------- Round 2 (the skeptic's r3\guj-check1 findings) ----------

    /// <summary>
    /// A shortcut while a word waits on its virama: the shortcut is the application's, and the engine
    /// injects nothing while Ctrl, Alt or Windows is held - the word keeps the half form Shree shows.
    /// It used to rewrite it to the halant (B:1 T:4C B9 24) under the held key, and in Notepad the
    /// Backspace became Ctrl+Backspace (the word before deleted) or Alt+Backspace (undo).
    /// </summary>
    [Theory]
    [InlineData("guj-itr", "JtfT", "shree-guj7", "68 70 BC")]
    [InlineData("guj-itr", "JtfT", "shree-guj456", "68 70 BC")]
    [InlineData("guj-bpmk", "A", "shree-guj7", "E5")]
    [InlineData("guj-bpmk", "A", "shree-guj456", "E5")]
    public void AShortcutWhileAWordWaitsInjectsNothing(string layout, string keys, string enc, string waiting)
    {
        const int VkMenu = 0x12, VkLWin = 0x5B, VkTab = 0x09;
        var chords = new (string Name, int Vk, int[] Mods)[]
        {
            ("Ctrl+S", 'S', new[] { VkControl }), ("Ctrl+B", 'B', new[] { VkControl }), ("Ctrl+Z", 'Z', new[] { VkControl }),
            ("Alt+Tab", VkTab, new[] { VkMenu }), ("Alt+F", 'F', new[] { VkMenu }), ("Win+D", 'D', new[] { VkLWin }),
        };
        foreach (var (name, vk, mods) in chords)
        {
            using var t = new Typist(_down, "gu", layout, enc);
            foreach (char c in keys) t.Key(c);
            Assert.Equal(waiting, Bytes(t.Screen.Text.ToString()));
            int sent = t.Screen.Sent;
            Assert.False(t.Chord(vk, mods), name + " is the application's");
            Assert.True(sent == t.Screen.Sent, $"{name}: the engine injected {t.Screen.Sent - sent} time(s) under the held key");
            Assert.Equal(waiting, Bytes(t.Screen.Text.ToString()));
        }
    }

    /// <summary>
    /// Backspace takes back exactly the last keystroke, whole: a key of the Eng keyboard's layers, a
    /// fixed key, a mark typed through the font's table, a key that types nothing. Each repro is the
    /// skeptic's, with what Backspace used to leave.
    /// </summary>
    [Theory]
    [InlineData("gu", "guj-eng", "Øn\b", "shree-guj7", "")]          // 50 B9 24 (ઙ્): left 50 B9
    [InlineData("gu", "guj-eng", "Øo\b", "shree-guj7", "")]          // 70 B8: left 70
    [InlineData("gu", "guj-eng", "Ø=\b", "shree-guj7", "")]          // 2D 2D: left 2D
    [InlineData("gu", "guj-eng", "¤R\b", "shree-guj7", "")]          // 47 3E: left 47
    [InlineData("gu", "guj-eng", "Øb\b", "shree-guj456", "")]        // EA 24: left EA
    [InlineData("gu", "guj-eng", "¤|\b", "shree-guj7", "")]          // 97 26 26 (॥): left 97 26
    [InlineData("gu", "guj-eng", "Øb\b", null, "")]                  // રૂ: left ર
    [InlineData("gu", "guj-eng", "Øa\b", null, "")]                  // ્ + ZWNJ: left ્
    [InlineData("gu", "guj-eng", "ka1\b\b", "shree-guj7", "4C 24")]  // the fixed key ૧, then back into the word typed by sound
    [InlineData("gu", "guj-eng", "kaØn\b\b\b", null, "ક")]           // the run, the escape, then the word's last letter
    [InlineData("gu", "guj-ekl", "\"\b", "shree-guj7", "")]          // '"' typed through the table, 22 22: left 22
    [InlineData("gu", "guj-ekl", "7\"\b", "shree-guj456", "37")]
    [InlineData("gu", "guj-akshar", "&tQ\b", "shree-guj7", "25 53 3E")]  // Q types nothing: it used to leave 25 53 (જ without its spacer)
    [InlineData("hi", "dev-eng", "8Ø?\b", null, "८")]                // the empty slot is the keystroke taken back, not the ८
    public void BackspaceTakesBackExactlyTheLastKeystroke(string lang, string layout, string keys, string? enc, string want)
    {
        string got = Type(lang, layout, keys, enc);
        Assert.Equal(want, enc is null ? got : Bytes(got));
        // and that is what the keys show without as many of their last keystrokes as there were Backspaces
        string typed = keys.TrimEnd('\b');
        Assert.Equal(Type(lang, layout, typed[..^(keys.Length - typed.Length)], enc), got);
    }

    /// <summary>
    /// From a legacy font, a .docx run in a font of the language's other Shree layout (SHREE-GUJ-0750 in a
    /// Shree-Lipi 7.0 conversion) is left as it is - its bytes mean other letters - and the report says so,
    /// where it used to be left without a word.
    /// </summary>
    [Fact]
    public void TextInAnotherLayoutsFontIsNamedInTheReport()
    {
        string dir = Path.Combine(Path.GetTempPath(), "textsoft-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "two-families.docx"), outDir = Path.Combine(dir, "out");
            string bytes = FromHex("4E 79 53 3E 66 70 73");   // ગુજરાત in SHREE-GUJ7 (composer bytes)
            using (var doc = WordprocessingDocument.Create(input, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body(new Paragraph(
                    new Run(new RunProperties(new RunFonts { Ascii = "SHREE-GUJ7-0750", HighAnsi = "SHREE-GUJ7-0750" }), new Text(bytes + " ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new RunProperties(new RunFonts { Ascii = "SHREE-GUJ-0750", HighAnsi = "SHREE-GUJ-0750" }), new Text(bytes)))));
            }
            var vm = new ConverterViewModel(Config, new FontResolver(Path.Combine(AppContext.BaseDirectory, "config")));
            vm.SelectedLanguage = vm.Languages.First(l => l.Id == "gu");
            vm.SelectedInputLayout = vm.InputLayouts.First(o => o.Name == "Shree-Lipi 7.0");
            vm.SelectedOutputLayout = vm.OutputLayouts.First(o => o.Key.IsUnicode);
            vm.OutputFolder = outDir;
            vm.Files.Add(new ConvertFileItem { Path = input });
            vm.ConvertFilesAsync().GetAwaiter().GetResult();

            Assert.Contains("SHREE-GUJ-0750 (a Shree-Lipi 4.0/5.0/6.0 font)", vm.Status);
            using var outDoc = WordprocessingDocument.Open(Path.Combine(outDir, "two-families.docx"), false);
            var runs = outDoc.MainDocumentPart!.Document.Body!.Descendants<Run>().Select(r => (Font: r.RunProperties?.RunFonts?.Ascii?.Value, Text: string.Concat(r.Elements<Text>().Select(t => t.Text)))).ToList();
            Assert.Contains(runs, r => r.Text.StartsWith("ગુજરાત"));               // the 7.0 run, converted
            Assert.Contains(runs, r => r.Font == "SHREE-GUJ-0750" && r.Text == bytes); // the 4/5/6 run, as it was
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// Shree's link keys (composer-vendor-keys.json: file code 64, the tutor's "Lk", is the virama; D7, "Hf",
    /// virama + ZWJ), where the key fell through to something else: an unset Shift typed the normal key's
    /// character (Akshar Shift+2 was ૨, DOE Shift+B વ), a key with no mapping passed as ASCII (Crtronic \ and |,
    /// Phonetic Shift+Q), an unused letter typed nothing (Newroma f), MTNK's - was a hyphen.
    /// </summary>
    [Theory]
    [InlineData("guj-akshar", "@", "્")]
    [InlineData("guj-doe", "B", "્‍")]
    [InlineData("guj-crtronic", "\\", "્")]
    [InlineData("guj-crtronic", "|", "્‍")]
    [InlineData("guj-mtnk", "-", "્‍")]
    [InlineData("guj-newroma", "f", "્‍")]
    [InlineData("guj-phonetic", "Q", "્‍")]
    [InlineData("guj-phonetic", "Øf", "્‍")]
    public void ShreesLinkKeysTypeTheLink(string layout, string keys, string unicode) =>
        Assert.Equal(unicode, Type("gu", layout, keys));

    /// <summary>
    /// The Unicode spaces no face of these families draws used to pass through unreported (a .txt could only
    /// write them as '?'; U+0085 as its raw byte, a letter): they are written as the fonts' space, and the
    /// line and paragraph separators, which a run cannot hold, as a space too and reported (P3, P6).
    /// </summary>
    [Theory]
    [InlineData("shree-guj7", "ક")]
    [InlineData("shree-guj456", "ક")]
    [InlineData("shree-dev7", "क")]
    [InlineData("shree-dev456", "क")]
    [InlineData("shree-san7", "क")]
    public void UnicodeSpacesAreWrittenAsTheFontsSpace(string enc, string letter)
    {
        var conv = To(enc);
        int[] spaces = { 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A, 0x202F, 0x205F, 0x3000 };
        foreach (int cp in spaces)
        {
            var r = conv.Convert(letter + (char)cp + letter);
            Assert.True(!r.Text.Contains((char)cp) && r.Text.Contains(' ') && r.UnmappedCharacters.Count == 0, $"U+{cp:X4}: {Bytes(r.Text)}");
        }
        foreach (int cp in new[] { 0x0085, 0x2028, 0x2029 })
        {
            var r = conv.Convert(((char)cp).ToString());
            Assert.Equal(" ", r.Text);
            Assert.Contains(((char)cp).ToString(), r.UnmappedCharacters);
        }
    }

    /// <summary>
    /// The skeptic's random test as a test: random keystrokes (Ctrl+Space among them) on keyboards of
    /// four scripts, the Eng ones too, in Unicode and Shree fonts. Typed then Backspace is typed without
    /// the last key; and so for two Backspaces when the application typed neither of the last two keys.
    /// (A key the application typed itself is its own to take back: before it, keys still waiting for
    /// a run were typed as themselves - Ekl '~' then '^' is "~^", and Backspace leaves "~".)
    /// </summary>
    [Theory]
    [InlineData("gu", "guj-eng", "shree-guj7", "shree-guj456")]
    [InlineData("gu", "guj-ekl", "shree-guj7", "shree-guj456")]
    [InlineData("gu", "guj-akshar", "shree-guj7", "shree-guj456")]
    [InlineData("gu", "guj-itr", "shree-guj7", "shree-guj456")]
    [InlineData("gu", "guj-newroma", "shree-guj7", null)]
    [InlineData("hi", "dev-eng", "shree-dev7", "shree-dev456")]
    [InlineData("sa", "san-eng", "shree-san7", null)]
    [InlineData("bn", "ban-eng", null, null)]
    public void EveryKeystrokeIsOneBackspace(string lang, string layout, string? enc1, string? enc2)
    {
        const string alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-=[];',./`~!@#$%^&*()_+{}|:\"<>?\\";
        var wrong = new List<string>();
        foreach (var enc in new[] { null, enc1, enc2 }.Distinct())
        {
            var rng = new Random(2026 ^ layout.Length);
            for (int i = 0; i < 150; i++)
            {
                var sb = new StringBuilder();
                int len = 1 + rng.Next(6);
                for (int k = 0; k < len; k++) sb.Append(rng.Next(10) == 0 ? (rng.Next(3) == 0 ? '¤' : 'Ø') : alpha[rng.Next(alpha.Length)]);
                string s = sb.ToString().Replace("ØØ", "Ø").Replace("¤¤", "¤");
                if (s[^1] is 'Ø' or '¤') s += "a";
                using var t = new Typist(_down, lang, layout, enc);
                foreach (char c in s) t.Key(c);
                if (!t.Handled[^1]) continue;
                string one = Type(lang, layout, s + "\b", enc), prev = Type(lang, layout, s[..^1], enc);
                if (one != prev) wrong.Add($"{enc ?? "Unicode"} '{s}' + Backspace: [{Bytes(one)}], want [{Bytes(prev)}]");
                if (s.Length >= 2 && t.Handled[^1] && t.Handled[^2])
                {
                    string two = Type(lang, layout, s + "\b\b", enc), prev2 = Type(lang, layout, s[..^2], enc);
                    if (two != prev2) wrong.Add($"{enc ?? "Unicode"} '{s}' + 2 Backspaces: [{Bytes(two)}], want [{Bytes(prev2)}]");
                }
            }
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(15)));
    }
}
