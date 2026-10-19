using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Engine;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The Shree-Lipi Punjabi (Gurmukhi) tables against what Shree's own composers write. The fixture
/// (Data/shree-pun7-truth.json) holds 27,752 Unicode strings - every letter with every sign and tail (bindi, tippi,
/// addak, visarga), ਿ first, the subjoined ੍ਰ ੍ਹ ੍ਵ ੍ਯ, the nukta letters, every two-letter cluster through the halant,
/// the independent vowels and carriers - with the bytes S8Pun764 (Shree-Lipi 7) types for each and the other
/// spellings it also accepts. Nothing here needs Shree installed.
///
/// The 7 faces (SHREE-PUN7-0951/0952) and the 4/5/6 faces (SHREE-PUN-0950/0951/0952/0955/0956) are the same design
/// code for code, but ₹ (3C) is in the 7 faces only: the shree-pun456 tables pass ₹ through, reported.
/// tools/PunTruth builds both table pairs from the capture.
/// </summary>
public class ShreePunjabiTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    private static readonly Lazy<List<Row>> Truth = new(LoadTruth);
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    public static readonly TheoryData<string> Families = new() { "shree-pun7", "shree-pun456" };

    private static FontConverter To(string id) => Assets.Value.CreateConverter("raavi", id)!;
    private static FontConverter From(string id) => Assets.Value.CreateConverter(id, "raavi")!;

    /// <summary>A truth spelling (7 codes) in the family's codes: in a 4/5/6 face ₹ (3C) passes through as itself.</summary>
    private static string Apply(string id, string hex) =>
        id == "shree-pun456" ? string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t == "3C" ? "U+20B9" : t)) : hex;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(27752, Truth.Value.Count);
        Assert.All(new[] { "shree-pun7", "shree-pun456" }, id =>
        {
            Assert.NotNull(Assets.Value.CreateConverter("raavi", id));
            Assert.NotNull(Assets.Value.CreateConverter(id, "raavi"));
            // One byte per character: files in these fonts are Windows-1252.
            Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(id));
        });
    }

    // (a) Every truth string alone comes out as the composer's preferred spelling (P1), reporting exactly what passes through.
    [Theory]
    [MemberData(nameof(Families))]
    public void TableWritesTheComposerSpelling(string id)
    {
        var conv = To(id);
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            string want = Apply(id, r.Pref);
            var c = conv.Convert(r.U);
            string got = Hex(c.Text);
            var reported = want.Split(' ').Where(t => t.StartsWith("U+")).Select(t => ((char)Convert.ToInt32(t[2..], 16)).ToString()).ToHashSet();
            if (got != want || !reported.SetEquals(c.UnmappedCharacters))
                wrong.Add($"{r.U} [{Cps(r.U)}]: want {want}, got {got} (reported {string.Join("", c.UnmappedCharacters)})");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (b) Strings joined into words: no pair or rule reaches across a syllable.
    [Theory]
    [InlineData("shree-pun7", 2026)]
    [InlineData("shree-pun7", 7)]
    [InlineData("shree-pun456", 2026)]
    [InlineData("shree-pun456", 7)]
    public void JoinedSyllablesConvertExactly(string id, int seed)
    {
        var conv = To(id);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, seed))
        {
            string want = string.Join(" ", parts.Select(p => Apply(id, p.Pref)));
            string got = Hex(conv.ConvertText(u));
            if (got != want) wrong.Add($"{u} [{Cps(u)}]: want {want}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every spelling the composer writes - preferred and every other one it accepts (the link spelling, with no glyph
    // for the virama, too) - reads back as its text, or as text the table writes with those very bytes; never Latin,
    // a control code or a noncharacter (P5).
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryComposerSpellingReadsBack(string id)
    {
        var to = To(id);
        var from = From(id);
        var wrong = new List<string>();
        int n = 0, exact = 0;
        foreach (var r in Truth.Value)
            foreach (var spelling in r.Accepted.Select(a => Apply(id, a)).Distinct())
            {
                if (spelling.Contains("U+")) continue;    // ₹ in a 4/5/6 face: no bytes to read
                n++;
                string got = from.ConvertText(FromHex(spelling));
                if (got == r.U) { exact++; continue; }
                var again = to.Convert(got);
                if (Hex(again.Text) != spelling || again.HasUnmapped || Malformed(got).Count > 0)
                    wrong.Add($"{spelling}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}] (written back {Hex(again.Text)})");
            }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.True(exact > 27000, $"{id}: only {exact} of {n} exact");
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void JoinedSpellingsReadBack(string id)
    {
        var to = To(id);
        var from = From(id);
        var pool = Truth.Value.Where(r => !Apply(id, r.Pref).Contains("U+") && !r.U.Any(c => c is '‌' or '‍')).ToList();
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(pool, 20000, 11))
        {
            string bytes = string.Join(" ", parts.Select(p => p.Pref));
            string got = from.ConvertText(FromHex(bytes));
            if (got != u && Hex(to.ConvertText(got)) != bytes) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
            else if (Malformed(got).Count > 0 && Malformed(u).Count == 0) wrong.Add($"{bytes}: read {got} for {u}: {string.Join("; ", Malformed(got))}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (d) A virama that ends a word is the visible halant B5 (P1): at the end, before a space, punctuation, the danda or a digit.
    [Theory]
    [InlineData("ਵਾਕ੍", "65 70 48 B5")]
    [InlineData("ਕ੍ ਕ", "48 B5 20 48")]
    [InlineData("ਕ੍।", "48 B5 26")]
    [InlineData("ਕ੍,", "48 B5 2C")]
    [InlineData("ਕ੍੧", "48 B5 31")]
    [InlineData("ਨ੍", "B2 BE B5")]                 // after ਨ's spacer
    [InlineData("ਕ੍‌", "48 B5")]               // the halant spelled with ZWNJ
    [InlineData("ਕ੍ਤ", "48 B5 57")]                 // before a letter with no subjoined form: the halant too (the composer's preferred)
    public void AViramaThatEndsAWordIsTheVisibleHalant(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-pun7", "shree-pun456" })
        {
            var c = To(id).Convert(unicode);
            Assert.True(bytes == Hex(c.Text), $"{id}: {unicode} want {bytes}, got {Hex(c.Text)}");
            Assert.Empty(c.UnmappedCharacters);
        }
        Assert.All(Truth.Value.Where(r => r.U.Length > 1 && r.U[^1] == '੍'), r => Assert.Contains("B5", r.Pref.Split(' ')));
    }

    // (e) Spellings worth naming, in both families, and back.
    [Theory]
    [InlineData("ਨੂੰ", "B2 A5 BE 76")]             // the tippi before ਨ's spacer, ੂ after it
    [InlineData("ਨੇ", "B2 79 BE")]
    [InlineData("ਨੁ", "B2 BE AE")]                 // ੁ after the spacer, with nothing on top: AE
    [InlineData("ਨੁਂ", "B2 A3 BE 74")]
    [InlineData("ਨ੍ਹਾਂ", "B2 BE AA 70 A3")]          // a composer gap: the spelling its own rules give
    [InlineData("ਉਨ੍ਹਾਂ", "45 B2 BE AA 70 A3")]
    [InlineData("ਉੱਤੇ", "45 A8 57 79")]             // the far addak after ਉ
    [InlineData("ਇੱਕ", "71 42 A7 48")]              // ਇ = ਿ + ੲ
    [InlineData("ਕੀਂ", "48 73")]                    // ੀ + bindi: one glyph
    [InlineData("ਕੀੰ", "48 73")]                    // a tippi after ੀ is the bindi
    [InlineData("ਕਂੱ", "48 A3 A8")]
    [InlineData("ਕਁ", "48 A7 A3")]
    [InlineData("ਉਂ", "45 A4")]
    [InlineData("ੳਂ", "44")]
    [InlineData("ਈਂ", "42 73")]
    [InlineData("ਕ੍ਰਿ", "71 6E")]                   // ਕ੍ਰ is one glyph, the i-sign before it
    [InlineData("ਸ੍ਤ੍ਰ", "67 B5 57 6F")]
    [InlineData("\u0A5Bਿਲ੍ਹਾ", "71 6B 64 AA 70")]
    [InlineData("ੴ", "31 B0")]
    [InlineData("ਕ੍‌ਰ", "48 B5 63")]           // a visible halant before ਰ: kept apart with ZWNJ
    [InlineData("ऽ", "40")]                        // the avagraha-like glyph the 7 composer types for code 41
    [InlineData("।।", "26 26")]
    public void NamedSpellings(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-pun7", "shree-pun456" })
        {
            var c = To(id).Convert(unicode);
            Assert.True(bytes == Hex(c.Text), $"{id}: {unicode} want {bytes}, got {Hex(c.Text)}");
            Assert.Empty(c.UnmappedCharacters);
            // read back as the composer writes the text: a tippi after ੀ is the bindi there
            Assert.Equal(unicode.Replace("ੀੰ", "ੀਂ"), From(id).ConvertText(FromHex(bytes)));
        }
    }

    // The nukta letters, typed precomposed (as the composer writes them) or as letter + nukta (normalised text keeps
    // them apart); a nukta on a letter with no nukta glyph is left out and reported, never dropped unseen.
    [Fact]
    public void NuktaLetters()
    {
        foreach (var id in new[] { "shree-pun7", "shree-pun456" })
        {
            Assert.Equal("69 70", Hex(To(id).ConvertText("ਖ" + "਼" + "ਾ")));
            Assert.Equal("66", Hex(To(id).ConvertText("ਸ" + "਼")));
            var c = To(id).Convert("ਕ" + "਼");
            Assert.Equal("48", Hex(c.Text));
            Assert.Contains("਼", c.UnmappedCharacters);
        }
    }

    // ₹: 3C in the 7 faces; the 4/5/6 faces have no glyph there, so ₹ passes through, reported. Both read 3C as ₹.
    [Fact]
    public void Rupee()
    {
        var seven = To("shree-pun7").Convert("₹੧੦੦");
        Assert.Equal("3C 31 30 30", Hex(seven.Text));
        Assert.Empty(seven.UnmappedCharacters);
        var four = To("shree-pun456").Convert("₹੧੦੦");
        Assert.Equal("U+20B9 31 30 30", Hex(four.Text));
        Assert.Contains("₹", four.UnmappedCharacters);
        Assert.Equal("₹੧੦੦", From("shree-pun7").ConvertText(FromHex("3C 31 30 30")));
        Assert.Equal("₹੧੦੦", From("shree-pun456").ConvertText(FromHex("3C 31 30 30")));
    }

    // (f) Text that is not Gurmukhi (P2-P4): what the fonts draw as itself is written as itself, unreported (the digits
    // draw the script's digits, as Shree writes them); punctuation they have a look-alike for is written so; any other
    // ASCII - the Latin letters too, whose codes draw Gurmukhi letters (C 43 = ੳ, H 48 = ਕ) - is left out and reported:
    // never written as a code that draws something else, nor ('x', '{', and '<' in the 4/5/6 faces: in no face) as one
    // RichEdit draws a box for.
    [Theory]
    [InlineData("shree-pun7", "!%()*+,-./:;=?[]0123456789", "")]
    [InlineData("shree-pun7", "#$&<>@\\^_`|}~x{", "left out")]
    [InlineData("shree-pun7", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "left out")]
    [InlineData("shree-pun456", "!%()*+,-./:;=?[]0123456789", "")]
    [InlineData("shree-pun456", "#$&<>@\\^_`|}~x{", "left out")]
    [InlineData("shree-pun456", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "left out")]
    public void AsciiInTheseFonts(string id, string chars, string how)
    {
        foreach (char ch in chars)
        {
            var c = To(id).Convert(ch.ToString());
            string s = ch.ToString();
            switch (how)
            {
                case "": Assert.True(c.Text == s && c.UnmappedCharacters.Count == 0, $"{id} '{ch}': {Hex(c.Text)}"); break;
                default: Assert.True(c.Text == "" && c.UnmappedCharacters.Contains(s), $"{id} '{ch}': {Hex(c.Text)}"); break;
            }
            // between two letters, nothing reaches into them
            Assert.Equal("48 " + (c.Text.Length > 0 ? Hex(c.Text) + " " : "") + "48", Hex(To(id).ConvertText("ਕ" + ch + "ਕ")));
        }
        // English in a Gurmukhi run is left out, reported - never written as the Gurmukhi letters its codes draw.
        var mixed = To(id).Convert("ਰਾਮ Ram");
        Assert.Equal("63 70 60 20", Hex(mixed.Text));
        Assert.Contains("R", mixed.UnmappedCharacters);
    }

    // Beyond ASCII, a decision each (Ascii.Beyond in tools/PunTruth, NonIndic.StandIns in the shared policy): the Unicode
    // spaces are the fonts' space; · and ∙ are written B7, the fonts' large bullet, as a stand-in, reported (RichEdit moves
    // U+2219 to Cambria Math, a .txt has no byte for it); the soft hyphen is left out (Word's PDF draws a '-' for it), and
    // so is a control character; a noncharacter is never taken for one of the tables' marks: U+FFFD, reported. The
    // Gurmukhi signs no face has a glyph for pass through as themselves, reported (P3b), and so does ₹ in a 4/5/6 face -
    // and read back as themselves, unreported (P5: text in a fallback font).
    [Theory]
    [MemberData(nameof(Families))]
    public void BeyondAscii(string id)
    {
        var to = To(id);
        void Is(int cp, string hex, bool reported)
        {
            string s = ((char)cp).ToString();
            var c = to.Convert(s);
            Assert.True(Hex(c.Text) == hex && c.UnmappedCharacters.Contains(s) == reported,
                $"{id} U+{cp:X4}: want '{hex}'{(reported ? " reported" : "")}, got '{Hex(c.Text)}' reported [{string.Join(",", c.UnmappedCharacters)}]");
            Assert.Equal("48 " + (hex.Length > 0 ? hex + " " : "") + "48", Hex(to.ConvertText("ਕ" + s + "ਕ")));
        }
        foreach (int sp in new[] { 0x2000, 0x2003, 0x2009, 0x200A, 0x202F, 0x205F, 0x3000, 0x1680 }) Is(sp, "20", false);
        Is(0x2219, "B7", true);
        Is(0x00B7, "B7", true);
        Is(0x00AD, "", true);
        Is(0x0001, "", true);
        Is(0x0085, "", true);
        Is(0x0092, "", true);
        Is(0x0A51, "U+0A51", true);
        Is(0x0A75, "U+0A75", true);
        foreach (int nc in new[] { 0xFDD1, 0xFDD8, 0xFDDB, 0xFDDD })
        {
            var c = to.Convert("ਨ" + (char)nc + "ਕ");
            Assert.Equal("B2 BE U+FFFD 48", Hex(c.Text));
            Assert.Contains(((char)0xFFFD).ToString(), c.UnmappedCharacters);
        }
        var rupee = to.Convert("₹");
        Assert.Equal(id == "shree-pun7" ? "3C" : "U+20B9", Hex(rupee.Text));
        Assert.Equal(id != "shree-pun7", rupee.UnmappedCharacters.Contains("₹"));
        // read back: what the forward table passes through is text, read as itself and not reported (P5)
        foreach (string passed in id == "shree-pun7" ? new[] { "ੑ", "ੵ" } : new[] { "ੑ", "ੵ", "₹" })
        {
            var back = From(id).Convert(FromHex("48") + passed);
            Assert.True(back.Text == "ਕ" + passed && back.UnmappedCharacters.Count == 0, $"{id} {Cps(passed)} reads back as {Cps(back.Text)}");
        }
    }

    // A virama before a ZWJ is the visible halant, as before a ZWNJ (Gurmukhi has no half forms, so a Unicode font draws
    // the halant there): at the end of a word and before a letter. The composer's own link spelling - the bare letter,
    // which reads back without the virama - stays accepted in the fixture (Capture.HalantBeforeZwj, 38 rows).
    [Fact]
    public void AViramaBeforeAZwjIsTheVisibleHalant()
    {
        string zwj = ((char)0x200D).ToString(), zwnj = ((char)0x200C).ToString();
        foreach (var id in new[] { "shree-pun7", "shree-pun456" })
        {
            var to = To(id);
            foreach (var (u, hex) in new[] { ("ਕ੍" + zwj, "48 B5"), ("ਵਾਕ੍" + zwj, "65 70 48 B5"), ("ਨ੍" + zwj, "B2 BE B5"),
                                              ("ਕ੍" + zwj + "ਰ", "48 B5 63"), ("ਕ੍" + zwj + " ਕ", "48 B5 20 48") })
            {
                var c = to.Convert(u);
                Assert.True(Hex(c.Text) == hex && c.UnmappedCharacters.Count == 0, $"{id} {Cps(u)}: want {hex}, got {Hex(c.Text)}");
            }
            // read back: the halant, kept apart from a following ਰ by a ZWNJ (the same bytes)
            Assert.Equal("ਵਾਕ੍", From(id).ConvertText(FromHex("65 70 48 B5")));
            Assert.Equal("ਕ੍" + zwnj + "ਰ", From(id).ConvertText(FromHex("48 B5 63")));
        }
        var rows = Truth.Value.Where(r => r.U.Length > 2 && r.U[^1] == (char)0x200D && r.U[^2] == '੍').ToList();
        Assert.Equal(38, rows.Count);
        Assert.All(rows, r => Assert.EndsWith("B5", r.Pref));
        Assert.All(rows, r => Assert.Contains(r.Pref[..^3], r.Accepted));     // the composer's link spelling, accepted
    }

    [Theory]
    [InlineData("“ਪੰਜਾਬ”", "22 22 5C A5 4F 70 5E 27 27")]   // the fonts' ‘ ’ (22 27), twice
    [InlineData("\"ਪੰਜਾਬ\"", "22 22 5C A5 4F 70 5E 27 27")]
    [InlineData("'ਪੰਜਾਬ'", "22 5C A5 4F 70 5E 27")]
    [InlineData("ਪੰਜਾਬ…", "5C A5 4F 70 5E 2E 2E 2E")]
    [InlineData("• ਕ", "B7 20 48")]
    [InlineData("ਕ—ਕ–ਕ", "48 B9 48 BA 48")]
    [InlineData("੨×੩÷੬", "32 23 33 24 36")]
    [InlineData("ਹਾਂ॥", "68 70 A3 26 26")]
    public void PunctuationIsWrittenAsTheLookAlikeTheFontsDraw(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-pun7", "shree-pun456" })
        {
            var c = To(id).Convert(unicode);
            Assert.Equal(bytes, Hex(c.Text));
            Assert.Empty(c.UnmappedCharacters);
        }
    }

    // (g) Every code point a table writes is in the cmap of every face of its family (Data/shree-pun-cmaps.json, read
    // from the installed faces), and none is one Word or RichEdit draws as nothing (U+00AD, a C1 code) or in another
    // font (U+2219): r3\word\final-invisible.txt for SHREE-PUN / SHREE-PUN7.
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodePointWrittenIsInTheFontsCmapAndDrawnByWord(string id)
    {
        var cmap = Cmaps.Value[id].Codes;
        var to = To(id);
        var table = Assets.Value.FindMapping("raavi", id)!;
        var written = new HashSet<char>();
        void Add(string s) { foreach (char c in s) if (c < '﷐' || c > '﷯') written.Add(c); }
        // (a pair marked unmapped that writes its own character passes it through, reported - P3b: ₹ in a 4/5/6 face)
        foreach (var p in table.Pairs) Add(p.Unmapped ? new string(p.To.Where(c => !p.From.Contains(c)).ToArray()) : p.To);
        foreach (var r in table.PostRules) Add(System.Text.RegularExpressions.Regex.Replace(r.Replace, @"\$\d|\$\$", ""));
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Truth.Value, 5000, 99).Select(w => w.U)))
        {
            var c = to.Convert(u);
            Add(new string(c.Text.Where(ch => !c.UnmappedCharacters.Contains(ch.ToString())).ToArray()));
        }
        var missing = written.Where(c => !cmap.Contains(c)).Select(c => $"U+{(int)c:X4}").ToList();
        Assert.True(missing.Count == 0, $"{id} writes code points no face of its family has: {string.Join(" ", missing)}");
        var invisible = written.Where(c => c is '­' or '∙' || (c >= '\u0080' && c <= '\u009F')).ToList();
        Assert.Empty(invisible);
    }

    // The cmap fixture is what the installed faces hold (when they are installed here).
    [Fact]
    public void CmapFixtureMatchesTheInstalledFaces()
    {
        string fonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        foreach (var (id, (faces, codes)) in Cmaps.Value)
            foreach (var face in faces)
            {
                string path = Path.Combine(fonts, face);
                if (!File.Exists(path)) continue;
                var keys = new GlyphTypeface(new Uri(path)).CharacterToGlyphMap.Keys.Select(k => (char)k).ToHashSet();
                Assert.True(codes.IsSubsetOf(keys), $"{face} ({id}) lacks {string.Join(" ", codes.Except(keys).Select(c => $"U+{(int)c:X4}"))}");
                Assert.Equal(id == "shree-pun7", keys.Contains('<'));     // ₹ at 3C in the 7 faces only
            }
    }

    // (h) Every output path carries the same code points (P6): what the tables write is one Windows-1252 byte each, so a
    // .txt file in these fonts (FileConversion writes Windows-1252) holds the very bytes the RTF, .docx and typing paths do.
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodeWrittenIsOneWindows1252Byte(string id)
    {
        var table = Assets.Value.FindMapping("raavi", id)!;
        var bad = new List<string>();
        // (not a character a pair passes through, reported: no Windows-1252 byte holds it, and FileConversion.LegacyBytes leaves it out of a .txt)
        foreach (var p in table.Pairs)
            foreach (char c in p.To.Where(c => (c < '﷐' || c > '﷯') && !(p.Unmapped && p.From.Contains(c))))
            {
                byte[] b;
                try { b = Cp1252.GetBytes(c.ToString()); }
                catch (EncoderFallbackException) { bad.Add($"{p.From}: U+{(int)c:X4}"); continue; }
                if (Cp1252.GetString(b) != c.ToString()) bad.Add($"{p.From}: U+{(int)c:X4} does not come back");
            }
        Assert.Empty(bad);
    }

    // ---------- helpers ----------

    private static List<Row> LoadTruth()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-pun7-truth.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var rows = new List<Row>();
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            string u = row[0].GetString()!, pref = row[1].GetString()!;
            var acc = new List<string> { pref };
            if (row.GetArrayLength() > 2) acc.AddRange(row[2].EnumerateArray().Select(a => a.GetString()!));
            rows.Add(new Row(u, pref, acc.ToArray()));
        }
        return rows;
    }

    private static readonly Lazy<Dictionary<string, (string[] Faces, HashSet<char> Codes)>> Cmaps = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-pun-cmaps.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var d = new Dictionary<string, (string[], HashSet<char>)>();
        foreach (var fam in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
            d[fam.Name] = (fam.Value.GetProperty("faces").EnumerateArray().Select(f => f.GetString()!).ToArray(),
                fam.Value.GetProperty("codePoints").GetString()!.Split(' ').Select(h => (char)Convert.ToInt32(h, 16)).ToHashSet());
        return d;
    });

    /// <summary>The code points every face of an encoding's family maps (ShreeNonIndicTests uses it for these families).</summary>
    internal static HashSet<char> CmapCodes(string id) => Cmaps.Value[id].Codes;

    private static bool IsLetter(char c) => (c >= 'ਕ' && c <= 'ਹ') || (c >= '\u0A59' && c <= '\u0A5E');
    private static bool IsVowelSign(char c) => (c >= 'ਾ' && c <= 'ੂ') || c is 'ੇ' or 'ੈ' or 'ੋ' or 'ੌ';
    private static bool IsMark(char c) => IsVowelSign(c) || c is 'ਁ' or 'ਂ' or 'ਃ' or 'ੰ' or 'ੱ' or '਼' or '੍' or 'ੑ' or 'ੵ';
    private static bool IsVowel(char c) => (c >= 'ਅ' && c <= 'ਊ') || c is 'ਏ' or 'ਐ' or 'ਓ' or 'ਔ' or 'ੲ' or 'ੳ';

    /// <summary>Ways a string is not well formed Gurmukhi (empty when it is): a sign off its letter, Latin, controls or the tables' marks leaking through.</summary>
    private static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool onLetter = IsLetter(p) || p == '਼';
            if ((IsVowelSign(c) || c == '੍') && !onLetter) bad.Add($"sign {c} at {i}");
            if (c is 'ਁ' or 'ਂ' or 'ਃ' or 'ੰ' or 'ੱ' && (i == 0 || p == '੍' || !(IsLetter(p) || IsVowel(p) || IsMark(p)))) bad.Add($"{c} at {i}");
            if (c is '‌' or '‍' && p != '੍') bad.Add($"joiner at {i}");
            if ((c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') || (c >= '\u0080' && c <= '\u009F') || (c >= '﷐' && c <= '﷯') || (c < ' ' && c is not ('\t' or '\n' or '\r')))
                bad.Add($"U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>Truth strings that can stand next to each other in a word: they start on a base and do not end on a virama or joiner.</summary>
    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] is not ('‌' or '‍') && r.U[^1] is not ('੍' or '‌' or '‍') &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '੍' && IsMark(r.U[i]) && r.U[i] != '੍'))
        .OrderBy(r => r.U, StringComparer.Ordinal).ToList();

    private static IEnumerable<(string U, List<Row> Parts)> Words(IEnumerable<Row> rows, int count, int seed)
    {
        var pool = Syllables(rows);
        var rng = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            int n = 2 + rng.Next(3);
            var parts = new List<Row>(n);
            for (int k = 0; k < n; k++) parts.Add(pool[rng.Next(pool.Count)]);
            yield return (string.Concat(parts.Select(p => p.U)), parts);
        }
    }

    private static Encoding GetCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    /// <summary>Legacy text as hex bytes; a character outside the fonts' code page shows as U+XXXX.</summary>
    internal static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    internal static string FromHex(string hex) =>
        hex.Length == 0 ? "" : Cp1252.GetString(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h => Convert.ToByte(h, 16)).ToArray());

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}

/// <summary>
/// Typing in a Shree-Lipi Punjabi font (P6): the real TypingEngine on the ITR keyboard, in a SHREE-PUN7 and a SHREE-PUN
/// profile, puts down the bytes the converter writes for the word - Shree's own spelling - at the end and after every key.
/// </summary>
[Collection("typing engine")]
public class ShreePunjabiTypingTests : IDisposable
{
    private const int VkShift = 0x10;
    private readonly LowLevelKeyboardHook _hook = new();
    private readonly RecordingInjector _injector = new();
    private readonly AssetBundle _config;
    private readonly TypingEngine _engine;
    private readonly HashSet<int> _down = new();

    public ShreePunjabiTypingTests()
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);
        _config = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        var punjabi = _config.Languages.First(l => l.Id == "pa");
        _engine = new TypingEngine(_hook, _injector);
        _engine.SetProfile(_config.GetLayout(punjabi.Layouts.First(l => l.Id == "pun-itr")), punjabi.Name, "Unicode");
        _engine.SetEnabled(true);
    }

    public void Dispose()
    {
        TypingEngine.KeyDownProbe = vk => false;
        _engine.Dispose();
        _hook.Dispose();
    }

    private readonly Dictionary<string, FontConverter> _conv = new();
    private FontConverter Conv(string enc) => _conv.TryGetValue(enc, out var c) ? c : _conv[enc] = _config.CreateConverterToLegacy(enc)!;

    /// <summary>One key, as a typist strikes it; true when the engine took it (else the application types it itself).</summary>
    private bool Key(char c)
    {
        bool shift = char.IsUpper(c) || "~!@#$%^&*()_+{}|:\"<>?".IndexOf(c) >= 0;
        int vk = c == ' ' ? 0x20 : char.IsLetter(c) ? char.ToUpperInvariant(c) : char.IsDigit(c) ? c
               : ")!@#$%^&*(".IndexOf(c) is var d and >= 0 ? '0' + d
               : c switch
               {
                   ';' or ':' => 186, '=' or '+' => 187, ',' or '<' => 188, '-' or '_' => 189, '.' or '>' => 190, '/' or '?' => 191,
                   '`' or '~' => 192, '[' or '{' => 219, '\\' or '|' => 220, ']' or '}' => 221, '\'' or '"' => 222, _ => 0,
               };
        if (shift) _down.Add(VkShift);
        var field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(_hook);
        var down = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
        handler!(_hook, down);
        handler!(_hook, new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = false });
        _down.Remove(VkShift);
        return down.Handled;
    }

    /// <summary>
    /// The document after typing <paramref name="keys"/> on a keyboard, in a Shree font (<paramref name="enc"/>) or in
    /// Unicode (null): what the engine typed, and each key it did not take as the application types it itself.
    /// </summary>
    private string Document(string layoutId, string keys, string? enc) => Document(layoutId, keys, enc, out _);

    /// <summary>The same; <paramref name="passed"/> = the keys the engine let through to the application.</summary>
    private string Document(string layoutId, string keys, string? enc, out string passed)
    {
        passed = string.Empty;
        var punjabi = _config.Languages.First(l => l.Id == "pa");
        var injector = new RecordingInjector();
        using var hook = new LowLevelKeyboardHook();
        using var engine = new TypingEngine(hook, injector);
        engine.SetProfile(_config.GetLayout(punjabi.Layouts.First(l => l.Id == layoutId)), punjabi.Name, "Unicode");
        engine.SetTransliterator(_config.GetTransliterator(punjabi));
        engine.SetLegacyConverter(enc is null ? null : Conv(enc), enc is null ? null : "SHREE-PUN7-0951");
        engine.SetEnabled(true);
        var field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(hook);
        foreach (char c in keys)
        {
            bool shift = char.IsUpper(c) || "~!@#$%^&*()_+{}|:\"<>?".IndexOf(c) >= 0;
            int vk = c == ' ' ? 0x20 : char.IsLetter(c) ? char.ToUpperInvariant(c) : char.IsDigit(c) ? c
                   : ")!@#$%^&*(".IndexOf(c) is var d and >= 0 ? '0' + d
                   : c switch
                   {
                       ';' or ':' => 186, '=' or '+' => 187, ',' or '<' => 188, '-' or '_' => 189, '.' or '>' => 190, '/' or '?' => 191,
                       '`' or '~' => 192, '[' or '{' => 219, '\\' or '|' => 220, ']' or '}' => 221, '\'' or '"' => 222, _ => 0,
                   };
            if (shift) _down.Add(VkShift);
            var down = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
            handler!(hook, down);
            handler!(hook, new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = false });
            _down.Remove(VkShift);
            if (!down.Handled) { injector.SendText(c.ToString()); passed += c; }
        }
        return injector.Text;
    }

    // P3/P6 on the keyboards that leave keys to the application (Modular) or type by sound (Eng): in a Shree font no key
    // reaches the document as a code that draws something else - each is typed as the converter writes the Unicode the same
    // keys type, or not at all (the 13 + 16 keys that used to reach the font raw: ' drew ’ as a quote mark and # ×, \ ਪ, ` ਮ).
    [Theory]
    [InlineData("pun-modular")]
    [InlineData("pun-eng")]
    public void NoKeyReachesAShreeFontRaw(string layoutId)
    {
        const string itself = "!%()*+,-./:;=?[]0123456789";
        var bad = new List<string>();
        for (char k = '!'; k <= '~'; k++)
            foreach (var enc in new[] { "shree-pun7", "shree-pun456" })
            {
                string uni = Document(layoutId, k.ToString(), null), legacy = Document(layoutId, k.ToString(), enc, out string passed);
                string want = Conv(enc).ConvertText(uni);
                if (legacy != want) bad.Add($"{enc} '{k}': typed {ShreePunjabiTruthTests.Hex(legacy)}, the converter writes {ShreePunjabiTruthTests.Hex(want)} for {uni}");
                // a key the engine lets through reaches the font as its own code: only one the fonts draw as itself may
                // (and ', whose code 27 is the fonts' closing quote, where the table writes it so)
                if (passed.Any(ch => itself.IndexOf(ch) < 0 && ch != '\''))
                    bad.Add($"{enc} '{k}': let through as {ShreePunjabiTruthTests.Hex(passed)}, a code the fonts draw as something else");
            }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(20)));
    }

    // Keys typed in a run, in a Shree font, put down what the converter writes for the Unicode the same keys type (P6):
    // a quote after a word or a digit is the closing one (ਪ' = 5C 27), a key that types nothing in the middle of a word
    // (DOE d, Phonetic `) leaves the word as it was (ਨੈ = B2 B1 BE, not B2 BE B1).
    [Theory]
    [InlineData("pun-modular", "w' 3' \\ #$")]
    [InlineData("pun-modular", "zrm'cd ']H'=l")]
    [InlineData("pun-doe", "vdw n\\] vdw")]
    [InlineData("pun-phonetic", "n`u gen`uT")]
    [InlineData("pun-eng", "unHaaM sVr pCkaa kaa zaaR \"pa\" p'")]
    [InlineData("pun-eng", "BCEHILPQRVWXZqxz")]
    public void KeysInARunTypeWhatTheConverterWrites(string layoutId, string keys)
    {
        string uni = Document(layoutId, keys + " ", null);
        foreach (var enc in new[] { "shree-pun7", "shree-pun456" })
        {
            string want = Conv(enc).ConvertText(uni), got = Document(layoutId, keys + " ", enc);
            Assert.True(want == got, $"{layoutId} {enc} '{keys}' (Unicode {uni}): typed {ShreePunjabiTruthTests.Hex(got)}, the converter writes {ShreePunjabiTruthTests.Hex(want)}");
        }
    }

    // What these keys type, against Shree's own keyboards (cap-pun\pun-vendor-keys.json): a key whose slot in the .PUN file is
    // FF types nothing (it typed the unshifted key's character: DOE Shift+3 typed ੩); Shree's Eng keys C H Q R V W z type
    // what Shree's do (they typed nothing, or raw Latin into the Shree font); aa after a letter is ਾ (no key could type it).
    [Theory]
    [InlineData("pun-doe", "#", "")]
    [InlineData("pun-doe", "N", "")]
    [InlineData("pun-crtronic", "\"", "")]
    [InlineData("pun-raming", "0", "")]
    [InlineData("pun-doe", "vdw", "ਨੈ")]
    [InlineData("pun-eng", "kaa", "ਕਾ")]
    [InlineData("pun-eng", "UnHaa", "ਉਨ੍ਹਾ")]
    [InlineData("pun-eng", "pCkaa", "ਪੱਕਾ")]
    [InlineData("pun-eng", "sVar", "ਸ੍ਵਰ")]
    [InlineData("pun-eng", "jhaaRaa", "ਝਾੜਾ")]
    [InlineData("pun-eng", "kQ", "ਕੰ")]
    [InlineData("pun-eng", "kamala", "ਕਮਲ")]
    public void KeysTypeWhatShreesKeyboardsType(string layoutId, string keys, string unicode)
    {
        Assert.Equal(unicode, Document(layoutId, keys, null));
    }

    // The key plans are the ITR keys that type the word (round1/audit: found on the real engine).
    [Theory]
    [InlineData("lqp", "ਨੂੰ", "B2 A5 BE 76")]
    [InlineData("ROf", "ਇੱਕ", "71 42 A7 48")]
    [InlineData("WO;u", "ਉੱਤੇ", "45 A8 57 79")]
    [InlineData("rsl=ntk", "ਜਿਨ੍ਹਾਂ", "71 4F B2 BE AA 70 A3")]
    [InlineData("rfWk", "ਕਿਉਂ", "71 48 45 A4")]
    [InlineData(";wntlqp", "ਤੁਹਾਨੂੰ", "57 74 68 70 B2 A5 BE 76")]
    [InlineData("crXpzt", "ਬਠਿੰਡਾ", "5E 71 53 A5 BE 54 70")]
    [InlineData("B", "ऽ", "40")]                      // Shift+B: Shree's code 41, the avagraha-like 40
    public void TypingInAShreeFontWritesTheConverterBytes(string keys, string word, string bytes)
    {
        foreach (var (enc, family) in new[] { ("shree-pun7", "SHREE-PUN7-0951"), ("shree-pun456", "SHREE-PUN-0950") })
        {
            var conv = Conv(enc);
            Assert.Equal(bytes, ShreePunjabiTruthTests.Hex(conv.ConvertText(word)));
            _engine.SetLegacyConverter(conv, family);
            foreach (char k in keys) Key(k);
            Assert.True(bytes == ShreePunjabiTruthTests.Hex(_injector.Text), $"{enc} {keys}: want {bytes}, got {ShreePunjabiTruthTests.Hex(_injector.Text)}");
            _engine.SetLegacyConverter(null);
            Key(' ');
            _injector.SendBackspace(_injector.Text.Length);
        }
    }
}
