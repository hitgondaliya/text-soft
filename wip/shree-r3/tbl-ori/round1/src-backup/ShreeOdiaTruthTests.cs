using System.Globalization;
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
/// The Shree-Lipi Odia table (shree-ori7, for both the SHREE-ORI7-* and the SHREE-ORI-* fonts) against what Shree's own
/// composers write. The fixture (Data/shree-ori7-truth.json) holds 41,470 Unicode strings - every letter with every sign
/// and tail, the e-sign struck first and ୈ ୋ ୌ around their cluster, the reph after its syllable, ra-/ya-/ba-phala, every
/// two-letter cluster through the link, the halant and the half code, the independent vowels - with the bytes S8Ori764
/// (Shree-Lipi 7) types for each and the other spellings it also accepts, as the table writes them: the composer's raw byte
/// 9C for ନ is 5B in every installed face (9C draws ଙ୍କ୍ତ there), and ₹ ୰ ଽ, whose codes no face shows in Word (3C is in
/// WORI7001/2 only; 8F 90 are C1 controls Word draws as nothing), are passed through as themselves, reported (P3b).
/// Nothing here needs Shree installed. tools/OriTruth builds the tables from the capture.
/// </summary>
public class ShreeOdiaTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    private static readonly Lazy<List<Row>> Truth = new(LoadTruth);
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();
    private const string Id = "shree-ori7";

    private static FontConverter To() => Assets.Value.CreateConverter("kalinga", Id)!;
    private static FontConverter From() => Assets.Value.CreateConverter(Id, "kalinga")!;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(41470, Truth.Value.Count);
        Assert.NotNull(Assets.Value.CreateConverter("kalinga", Id));
        Assert.NotNull(Assets.Value.CreateConverter(Id, "kalinga"));
        Assert.NotNull(Assets.Value.CreateConverterToLegacy(Id));
        // One byte per character: files in these fonts are Windows-1252.
        Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(Id));
    }

    // Both families are wired to the one table: the SHREE-ORI7-* and SHREE-ORI-* faces draw the same codes (Family.cs).
    [Fact]
    public void BothFamiliesUseTheOneTable()
    {
        var odia = Assets.Value.Languages.First(l => l.Id == "or");
        var shree = odia.Fonts.Where(f => f.Family.StartsWith("SHREE-ORI7-", StringComparison.Ordinal) || f.Family.StartsWith("SHREE-ORI-", StringComparison.Ordinal)).ToList();
        Assert.Equal(10, shree.Count);
        Assert.All(shree, f => Assert.Equal(Id, f.Encoding));
    }

    // (a) Every truth string alone comes out as the composer's preferred spelling (P1), reporting exactly what passes through.
    [Fact]
    public void TableWritesTheComposerSpelling()
    {
        var conv = To();
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Hex(c.Text);
            var reported = r.Pref.Split(' ').Where(t => t.StartsWith("U+")).Select(t => ((char)Convert.ToInt32(t[2..], 16)).ToString()).ToHashSet();
            if (got != r.Pref || !reported.SetEquals(c.UnmappedCharacters))
                wrong.Add($"{r.U} [{Cps(r.U)}]: want {r.Pref}, got {got} (reported {string.Join("", c.UnmappedCharacters)})");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (b) Strings joined into words: no pair or rule reaches across a syllable (the reph and the e-sign stay on their own).
    [Theory]
    [InlineData(2026)]
    [InlineData(7)]
    public void JoinedSyllablesConvertExactly(int seed)
    {
        var conv = To();
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, seed))
        {
            string want = string.Join(" ", parts.Select(p => p.Pref));
            string got = Hex(conv.ConvertText(u));
            if (got != want) wrong.Add($"{u} [{Cps(u)}]: want {want}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every spelling the composer writes - preferred and every other one it accepts, and Shree's own 3C 8F 90 for
    // ₹ ୰ ଽ - reads back as its text, or as text the table writes with those very bytes; never malformed where the text
    // was not, never Latin, a control code or a noncharacter (P5).
    [Fact]
    public void EveryComposerSpellingReadsBack()
    {
        var to = To();
        var from = From();
        var wrong = new List<string>();
        int n = 0, exact = 0;
        foreach (var r in Truth.Value)
            foreach (var spelling in r.Accepted.Distinct())
            {
                if (spelling.Contains("U+")) continue;    // passed through: no bytes to read
                n++;
                string got = from.ConvertText(FromHex(spelling));
                if (got == r.U) { exact++; continue; }
                var again = to.Convert(got);
                bool same = Hex(again.Text) == spelling && !again.HasUnmapped;
                bool wellFormed = Malformed(got).Count == 0 || Malformed(r.U).Count > 0;
                // two accepted spellings name the same bytes as another row's preferred one (7B 4C FF FA CC is କେ୍‌ଷ, 99 BF FA ends in a ZWNJ)
                if (!(same && wellFormed) && !(r.U == "କ୍ଷେ" && spelling == "7B 4C FF FA CC") && !(r.U == "ଧ୍ୟ୍‌" && spelling == "99 BF FA"))
                    wrong.Add($"{spelling}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}] (written back {Hex(again.Text)})");
                if (Malformed(got).Any(b => b.StartsWith("U+"))) wrong.Add($"{spelling}: read {Cps(got)}, not text");
            }
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.True(exact > 37000, $"only {exact} of {n} exact");
        Assert.Equal("₹୧୦୦", from.ConvertText(FromHex("3C 31 30 30")));
        Assert.Equal("୰", from.ConvertText(FromHex("8F")));
        Assert.Equal("ସୋଽହମ୍", from.ConvertText(FromHex("7B D3 E6 90 DC FF FE FA")));
    }

    [Fact]
    public void JoinedSpellingsReadBack()
    {
        var to = To();
        var from = From();
        var pool = Truth.Value.Where(r => !r.Pref.Contains("U+") && !r.U.Any(c => c is '‌' or '‍')).ToList();
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(pool, 20000, 11))
        {
            string bytes = string.Join(" ", parts.Select(p => p.Pref));
            string got = from.ConvertText(FromHex(bytes));
            if (got != u && Hex(to.ConvertText(got)) != bytes) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
            else if (Malformed(got).Count > 0 && Malformed(u).Count == 0) wrong.Add($"{bytes}: read {got} for {u}: {string.Join("; ", Malformed(got))}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (d) A virama that ends a word is the visible virama FA (P1): at the end, before a space, punctuation, the danda or a
    // digit; after a subjoined form that hangs below, with the lowering spacer 26 (କ୍ଟ୍ = 4C FF 75 26 FA).
    [Theory]
    [InlineData("ବାକ୍", "AF FF E6 4C FF FA")]
    [InlineData("କ୍ କ", "4C FF FA 20 4C FF")]
    [InlineData("କ୍।", "4C FF FA 3E")]
    [InlineData("କ୍,", "4C FF FA 2C")]
    [InlineData("କ୍୧", "4C FF FA 31")]
    [InlineData("କ୍ଟ୍", "4C FF 75 26 FA")]
    [InlineData("ର୍", "C0 FF FA")]                        // ର୍ that ends a word: ra + the visible virama, not the reph
    [InlineData("କ୍‌", "4C FF FA")]                  // the halant spelled with ZWNJ
    [InlineData("କ୍‍", "4C FF FA")]                  // and with ZWJ: the composer writes the same
    [InlineData("କ୍‌ତ", "4C FF FA 86 FF")]
    public void AViramaThatEndsAWordIsTheVisibleVirama(string unicode, string bytes)
    {
        var c = To().Convert(unicode);
        Assert.True(bytes == Hex(c.Text), $"{unicode} want {bytes}, got {Hex(c.Text)}");
        Assert.Empty(c.UnmappedCharacters);
        Assert.All(Truth.Value.Where(r => r.U.Length > 1 && r.U[^1] == '୍'), r => Assert.Contains("FA", r.Pref.Split(' ')));
    }

    // (e) Spellings worth naming, and back.
    [Theory]
    [InlineData("ନ", "5B FF", "ନ")]                              // the fonts' ନ (the composer's raw 9C draws ଙ୍କ୍ତ)
    [InlineData("ଜଗନ୍ନାଥ", "66 53 9F E6 24", "ଜଗନ୍ନାଥ")]
    [InlineData("କେ", "7B 4C FF", "କେ")]                        // the e-sign before its letter
    [InlineData("କ୍ରେ", "7B 4C FF F7", "କ୍ରେ")]                   // ... before the whole cluster
    [InlineData("କୈ", "7B 4C FF F0", "କୈ")]
    [InlineData("କୋ", "7B 4C FF E6", "କୋ")]
    [InlineData("କୌ", "7B 4C FF F2", "କୌ")]
    [InlineData("କୌ", "7B 4C FF F2", "କୌ")]           // ୌ typed as େ + ୗ
    [InlineData("ର୍କ", "4C FF F6", "ର୍କ")]                       // the reph after its letter
    [InlineData("ର୍କା", "4C FF E6 F6", "ର୍କା")]                  // ... and its sign
    [InlineData("ର୍କି", "4C FF 7D", "ର୍କି")]                     // ି + reph: one glyph
    [InlineData("ର୍କୈ", "7B 4C FF F1", "ର୍କୈ")]                  // ୖ + reph: one glyph
    [InlineData("ର୍କଂ", "4C FF F6 F3", "ର୍କଂ")]                  // the reph before ଂ ଁ ଃ
    [InlineData("ର୍କ୍ଷ", "E4 F6", "ର୍କ୍ଷ")]
    [InlineData("କର୍ମ", "4C FF FE F6", "କର୍ମ")]
    [InlineData("ଖି", "51 23", "ଖି")]                           // ି on ଖ ଥ ଧ is the hook 23
    [InlineData("କିଁ", "4C FF 5D", "କିଁ")]
    [InlineData("କ୍ଟୁ", "4C FF 75 EC", "କ୍ଟୁ")]                   // ୁ under a hanging subjoined form
    [InlineData("କ୍ଟୃ", "4C FF 75 26 F5", "କ୍ଟୃ")]
    [InlineData("କ୍ଟ୍ର", "4C FF 75 26 F7", "କ୍ଟ୍ର")]
    [InlineData("କ୍କ", "4D", "କ୍କ")]
    [InlineData("କ୍ଷ", "E4", "କ୍ଷ")]
    [InlineData("ଖ୍କ୍ଷ", "51 FA E4", "ଖ୍କ୍ଷ")]
    [InlineData("ସ୍ତ୍ର", "DA", "ସ୍ତ୍ର")]
    [InlineData("ମନ୍ତ୍ରୀ", "FE A6 EA", "ମନ୍ତ୍ରୀ")]
    [InlineData("କ୍ୟ", "4C FF BF", "କ୍ୟ")]                       // the ya-phala
    [InlineData("କ୍ଯ", "4C FF BF", "କ୍ୟ")]                       // typed with ଯ: the same ya-phala (Nirmala UI draws both alike)
    [InlineData("ଧ୍ୟ", "9A", "ଧ୍ୟ")]
    [InlineData("କ୍ର", "4C FF F7", "କ୍ର")]                       // the ra-phala
    [InlineData("ଶ୍ରୁ", "C9 F8", "ଶ୍ରୁ")]
    [InlineData("କ୍ବ", "4C FF B4", "କ୍ବ")]                       // the ba-phala stroke
    [InlineData("ସ୍ୱ", "D3 B4", "ସ୍ବ")]                          // ୍ୱ: the composer has no spelling; the same stroke
    [InlineData("ୱ", "48 B4", "ୱ")]
    [InlineData("ତତ୍ତ୍ୱ", "86 FF 89", "ତତ୍ତ୍ବ")]                  // the fonts' ତ୍ତ୍ୱ ligature
    [InlineData("ଙ୍କ୍ତ", "9C", "ଙ୍କ୍ତ")]                         // what 9C draws
    [InlineData("ଡ଼", "DD", "ଡ଼")]                         // ଡ଼ as the composer writes it (U+0B5C)
    [InlineData("ଡ଼", "DD", "ଡ଼")]                   // typed decomposed (NFC keeps ଡ + nukta apart): read back as the composer writes it
    [InlineData("ଆ", "41 E6", "ଆ")]
    [InlineData("ଓଁ", "48 F4", "ଓଁ")]
    [InlineData("।।", "3E 3E", "।।")]
    [InlineData("॥", "3E 3E", "।।")]                            // the composer types nothing for ॥: two dandas
    [InlineData("ଦୁଇ – ତିନି — ଚାରି", "92 FF EB 42 20 2D 20 86 FF E7 5B FF E7 20 2D 2D 20 60 FF E6 C0 FF E7", "ଦୁଇ - ତିନି -- ଚାରି")]
    public void NamedSpellings(string unicode, string bytes, string back)
    {
        var c = To().Convert(unicode);
        Assert.True(bytes == Hex(c.Text), $"{unicode} want {bytes}, got {Hex(c.Text)}");
        Assert.Empty(c.UnmappedCharacters);
        Assert.Equal(back, From().ConvertText(FromHex(bytes)));
    }

    // ନ: Shree's composer answers 9C FF; every installed face draws ନ at 5B and the ଙ୍କ୍ତ ligature at 9C. The table writes
    // 5B, never 9C for ନ (9C only for ଙ୍କ୍ତ), and reads 9C as the ligature the document shows.
    [Fact]
    public void NaIsWrittenAt5B()
    {
        var conv = To();
        Assert.All(Truth.Value.Where(r => r.U.Contains('ନ')).Select(r => conv.ConvertText(r.U)), s => Assert.DoesNotContain('œ', s));
        Assert.Equal("ଙ୍କ୍ତ", From().ConvertText(FromHex("9C")));
        Assert.Equal("ନ", From().ConvertText(FromHex("5B FF")));
        Assert.Equal("ନ", From().ConvertText(FromHex("5B")));
    }

    // (f) P3b: what no code of the fonts draws visibly is passed through as itself, reported - never left out: ₹ (3C is in
    // WORI7001/2 only), ୰ ଽ (their codes U+008F U+0090 are C1 controls, which Word draws as nothing), the signs and letters
    // the composer types nothing for; and read back as themselves, unreported (P5: text in a fallback font).
    [Theory]
    [InlineData("₹")]
    [InlineData("୰")]
    [InlineData("ଽ")]
    [InlineData("ୄ")]
    [InlineData("ୢ")]
    [InlineData("ୣ")]
    [InlineData("ଵ")]
    [InlineData("ୡ")]
    [InlineData("୲")]
    public void ACharacterTheFontsCannotShowPassesThrough(string ch)
    {
        var c = To().Convert("କ" + ch + "କ");
        Assert.Equal("4C FF " + Hex(ch) + " 4C FF", Hex(c.Text));
        Assert.Contains(ch, c.UnmappedCharacters);
        var back = From().Convert(FromHex("4C FF") + ch);
        Assert.True(back.Text == "କ" + ch && back.UnmappedCharacters.Count == 0, $"{Cps(ch)} reads back as {Cps(back.Text)}");
    }

    // The nukta on a letter with no nukta glyph passes through as itself, reported (the composer types nothing for it).
    [Fact]
    public void Nukta()
    {
        Assert.Equal("DD E7", Hex(To().ConvertText("ଡ଼ି")));
        var c = To().Convert("କ଼");
        Assert.Equal("4C FF U+0B3C", Hex(c.Text));
        Assert.Contains("଼", c.UnmappedCharacters);
    }

    // (g) Text that is not Odia (P2-P4): what the fonts draw as itself is written as itself, unreported (the digits draw
    // the Odia digits, as Shree writes them); the quotes as the fonts' quotes; any other ASCII - the Latin letters too,
    // whose codes draw Odia letters (L 4C = କ) - is left out and reported, never written as a code that draws something else.
    [Theory]
    [InlineData("!%()*,-./:?0123456789", "")]
    [InlineData("#$&+;<=>@[\\]^_`{|}~", "left out")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "left out")]
    public void AsciiInTheseFonts(string chars, string how)
    {
        foreach (char ch in chars)
        {
            var c = To().Convert(ch.ToString());
            string s = ch.ToString();
            if (how == "") Assert.True(c.Text == s && c.UnmappedCharacters.Count == 0, $"'{ch}': {Hex(c.Text)}");
            else Assert.True(c.Text == "" && c.UnmappedCharacters.Contains(s), $"'{ch}': {Hex(c.Text)}");
            Assert.Equal("4C FF " + (c.Text.Length > 0 ? Hex(c.Text) + " " : "") + "4C FF", Hex(To().ConvertText("କ" + ch + "କ")));
        }
        var mixed = To().Convert("ରାମ Ram");
        Assert.Equal("C0 FF E6 FE 20", Hex(mixed.Text));
        Assert.Contains("R", mixed.UnmappedCharacters);
    }

    [Theory]
    [InlineData("“ଓଡ଼ିଆ”", "22 22 48 DD E7 41 E6 27 27")]     // the fonts' ‘ ’ (22 27), twice
    [InlineData("\"ଓଡ଼ିଆ\"", "22 22 48 DD E7 41 E6 27 27")]
    [InlineData("'ଓଡ଼ିଆ'", "22 48 DD E7 41 E6 27")]
    [InlineData("ଓଡ଼ିଆ…", "48 DD E7 41 E6 2E 2E 2E")]
    [InlineData("• କ", "B7 20 4C FF")]
    [InlineData("କ—କ–କ", "4C FF 2D 2D 4C FF 2D 4C FF")]
    public void PunctuationIsWrittenAsTheLookAlikeTheFontsDraw(string unicode, string bytes)
    {
        var c = To().Convert(unicode);
        Assert.Equal(bytes, Hex(c.Text));
        Assert.Empty(c.UnmappedCharacters);
    }

    // Beyond ASCII, a decision each (tools/OriTruth Ascii.Beyond, the shared NonIndic.StandIns): the Unicode spaces are
    // the fonts' space; · and ∙ are B7 (the fonts' large bullet, a stand-in, reported); the soft hyphen and the control
    // characters are left out, reported; a noncharacter is never taken for one of the tables' marks: U+FFFD, reported.
    [Fact]
    public void BeyondAscii()
    {
        var to = To();
        void Is(int cp, string hex, bool reported)
        {
            string s = ((char)cp).ToString();
            var c = to.Convert(s);
            Assert.True(Hex(c.Text) == hex && c.UnmappedCharacters.Contains(s) == reported,
                $"U+{cp:X4}: want '{hex}'{(reported ? " reported" : "")}, got '{Hex(c.Text)}' reported [{string.Join(",", c.UnmappedCharacters)}]");
            Assert.Equal("4C FF " + (hex.Length > 0 ? hex + " " : "") + "4C FF", Hex(to.ConvertText("କ" + s + "କ")));
        }
        foreach (int sp in new[] { 0x2000, 0x2003, 0x2009, 0x200A, 0x202F, 0x205F, 0x3000, 0x1680 }) Is(sp, "20", false);
        Is(0x2219, "B7", true);
        Is(0x00B7, "B7", true);
        Is(0x00AD, "", true);
        Is(0x0001, "", true);
        Is(0x0085, "", true);
        Is(0x008F, "", true);
        Is(0x0090, "", true);
        Is(0x00A9, "", true);      // © A9 draws ପ୍ତ
        Is(0x00B0, "", true);      // ° B0 draws ବ୍ଜ
        foreach (int nc in new[] { 0xFDD0, 0xFDD1, 0xFDD2, 0xFDD3 })
        {
            var c = to.Convert("କ" + (char)nc + "କ");
            Assert.Equal("4C FF U+FFFD 4C FF", Hex(c.Text));
            Assert.Contains(((char)0xFFFD).ToString(), c.UnmappedCharacters);
        }
    }

    // Every BMP character outside ASCII and the Indic blocks: never written as a code the fonts draw as an Odia glyph; left
    // out (reported) only when its own code point is one of the fonts' (it would print another glyph) or a control; otherwise
    // passed through, reported (P3b), or written as a look-alike - and nothing reaches into the letters around it.
    [Fact]
    public void NonIndicSweep()
    {
        var to = To();
        var from = From();
        var codes = Cmaps.Value["common"].Codes;
        string alone = to.ConvertText("କ");
        var wrong = new List<string>();
        int n = 0;
        for (int cp = 0x80; cp <= 0xFFFD; cp++)
        {
            char c = (char)cp;
            if ((cp >= 0x0900 && cp <= 0x0DFF) || char.IsSurrogate(c) || (cp >= 0xFDD0 && cp <= 0xFDEF)) continue;
            n++;
            var r = to.Convert(c.ToString());
            foreach (char o in r.Text)
                if (from.ConvertText(o.ToString()).Any(x => x >= '଀' && x <= '୿')) wrong.Add($"U+{cp:X4}: writes {Hex(o.ToString())}, which reads as Odia");
            bool left = r.Text.Length == 0 && cp is not (0x200C or 0x200D) && !(cp >= 0xFE00 && cp <= 0xFE0F);
            if (left && !r.UnmappedCharacters.Contains(c.ToString())) wrong.Add($"U+{cp:X4}: left out unreported");
            var cat = char.GetUnicodeCategory(c);
            if (left && !codes.Contains(c) && cat is not (UnicodeCategory.Control or UnicodeCategory.Format) && !char.IsWhiteSpace(c))
                wrong.Add($"U+{cp:X4}: left out, though no code of the fonts is its own (P3b)");
            if (to.ConvertText("କ" + c + "କ") != alone + r.Text + alone) wrong.Add($"U+{cp:X4}: reaches into the letters");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (h) Every code point the table writes is in the cmap of every face of both families (Data/shree-ori-cmaps.json, read
    // from the installed faces), and none is one Word or RichEdit draws as nothing (U+00AD, a C1 code) or in another font
    // (U+2219): r3\word\final-invisible.txt for SHREE-ORI, SHREE-ORI7 and Shree-Ori7-00x.
    [Theory]
    [InlineData("^SHREE-ORI7-")]
    [InlineData("^SHREE-ORI-")]
    public void EveryCodePointWrittenIsInTheFontsCmapAndDrawnByWord(string family)
    {
        var cmap = Cmaps.Value[family].Codes;
        var to = To();
        var table = Assets.Value.FindMapping("kalinga", Id)!;
        var written = new HashSet<char>();
        void Add(string s) { foreach (char c in s) if (c < '﷐' || c > '﷯') written.Add(c); }
        foreach (var p in table.Pairs) Add(p.Unmapped ? new string(p.To.Where(c => !p.From.Contains(c)).ToArray()) : p.To);
        foreach (var r in table.PostRules) Add(System.Text.RegularExpressions.Regex.Replace(r.Replace, @"\$\d|\$\$", ""));
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Truth.Value, 5000, 99).Select(w => w.U)))
        {
            var c = to.Convert(u);
            Add(new string(c.Text.Where(ch => !c.UnmappedCharacters.Contains(ch.ToString())).ToArray()));
        }
        var missing = written.Where(c => !cmap.Contains(c) && !char.IsWhiteSpace(c)).Select(c => $"U+{(int)c:X4}").ToList();
        Assert.True(missing.Count == 0, $"{family} faces lack code points the table writes: {string.Join(" ", missing)}");
        var invisible = written.Where(c => c is '­' or '∙' || (c >= '\u0080' && c <= '\u009F')).ToList();
        Assert.Empty(invisible);
    }

    // The cmap fixture is what the installed faces hold (when they are installed here).
    [Fact]
    public void CmapFixtureMatchesTheInstalledFaces()
    {
        string fonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        foreach (var (family, (faces, codes)) in Cmaps.Value.Where(kv => kv.Key != "common"))
            foreach (var face in faces)
            {
                string path = Path.Combine(fonts, face);
                if (!File.Exists(path)) continue;
                var keys = new GlyphTypeface(new Uri(path)).CharacterToGlyphMap.Keys.Select(k => (char)k).ToHashSet();
                Assert.True(codes.IsSubsetOf(keys), $"{face} ({family}) lacks {string.Join(" ", codes.Except(keys).Select(c => $"U+{(int)c:X4}"))}");
                // ₹ at 3C in WORI7001/2 only
                Assert.Equal(face.StartsWith("WORI", StringComparison.OrdinalIgnoreCase), keys.Contains('<'));
            }
    }

    // (i) Every output path carries the same code points (P6): what the table writes is one Windows-1252 byte each, so a
    // .txt file in these fonts holds the very bytes the RTF, .docx and typing paths do.
    [Fact]
    public void EveryCodeWrittenIsOneWindows1252Byte()
    {
        var table = Assets.Value.FindMapping("kalinga", Id)!;
        var bad = new List<string>();
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
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-ori7-truth.json");
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
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-ori-cmaps.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var d = new Dictionary<string, (string[], HashSet<char>)>();
        foreach (var fam in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
            d[fam.Name] = (fam.Value.TryGetProperty("faces", out var f) ? f.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>(),
                fam.Value.GetProperty("codePoints").GetString()!.Split(' ').Select(h => (char)Convert.ToInt32(h, 16)).ToHashSet());
        return d;
    });

    private static bool IsLetter(char c) => (c >= 'କ' && c <= 'ହ') || c is 'ଡ଼' or 'ଢ଼' or 'ୟ' or 'ୱ';
    private static bool IsVowelSign(char c) => (c >= 'ା' && c <= 'ୄ') || c is 'େ' or 'ୈ' or 'ୋ' or 'ୌ' or 'ୖ' or 'ୗ' or 'ୢ' or 'ୣ';
    private static bool IsMark(char c) => IsVowelSign(c) || c is 'ଁ' or 'ଂ' or 'ଃ' or '଼' or '୍';
    private static bool IsVowel(char c) => (c >= 'ଅ' && c <= 'ଌ') || c is 'ଏ' or 'ଐ' or 'ଓ' or 'ଔ' or 'ୠ' or 'ୡ';

    /// <summary>Ways a string is not well formed Odia (empty when it is): a sign off its letter, Latin, controls or the tables' marks leaking through.</summary>
    private static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool onLetter = IsLetter(p) || p == '଼';
            if ((IsVowelSign(c) || c == '୍') && !onLetter && !(IsVowelSign(p) && c is 'ା' or 'ୖ' or 'ୗ')) bad.Add($"sign {c} at {i}");
            if (c is 'ଁ' or 'ଂ' or 'ଃ' && (i == 0 || p == '୍' || !(IsLetter(p) || IsVowel(p) || IsMark(p)))) bad.Add($"{c} at {i}");
            if (c is '‌' or '‍' && p != '୍') bad.Add($"joiner at {i}");
            if ((c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') || (c >= '\u0080' && c <= '\u009F') || (c >= '﷐' && c <= '﷯') || (c < ' ' && c is not ('\t' or '\n' or '\r')))
                bad.Add($"U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>Truth strings that can stand next to each other in a word: they start on a base and do not end on a virama or joiner.</summary>
    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] is not ('‌' or '‍') && r.U[^1] is not ('୍' or '‌' or '‍') &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '୍' && IsMark(r.U[i]) && r.U[i] != '୍'))
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

    /// <summary>Legacy text as hex bytes (U+008F U+0090 as 8F 90, the fonts' C1 codes); a character outside the fonts' code page shows as U+XXXX.</summary>
    internal static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            if (c is '\u008F' or '\u0090') { parts.Add(((int)c).ToString("X2")); continue; }
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    internal static string FromHex(string hex)
    {
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            byte b = Convert.ToByte(h, 16);
            sb.Append(b is 0x8F or 0x90 ? ((char)b).ToString() : Cp1252.GetString(new[] { b }));
        }
        return sb.ToString();
    }

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}

/// <summary>
/// Typing in a Shree-Lipi Odia font (P6): the real TypingEngine on the Odia keyboards, in a SHREE-ORI7 profile, puts down
/// the bytes the converter writes for the word - Shree's own spelling - at the end and after every key; and the keys the
/// per-key audit fixed (r3\tbl-ori\round1\patch-layouts.js, patch-eng.js) type what Shree's own keyboards type.
/// </summary>
[Collection("typing engine")]
public class ShreeOdiaTypingTests : IDisposable
{
    private const int VkShift = 0x10, VkControl = 0x11, VkSpace = 0x20;
    private const char CtrlSpace = (char)0xD8;   // EscapeLayer.Mark, as the charts write Ctrl+Space
    private readonly AssetBundle _config;
    private readonly FontConverter _conv;
    private readonly HashSet<int> _down = new();

    public ShreeOdiaTypingTests()
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);
        _config = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
        _conv = _config.CreateConverterToLegacy("shree-ori7")!;
    }

    public void Dispose() => TypingEngine.KeyDownProbe = vk => false;

    private static int Vk(char c, out bool shift)
    {
        shift = char.IsUpper(c) || "~!@#$%^&*()_+{}|:\"<>?".IndexOf(c) >= 0;
        if (char.IsAsciiLetter(c)) return char.ToUpperInvariant(c);
        if (char.IsAsciiDigit(c)) return c;
        int d = ")!@#$%^&*(".IndexOf(c);
        if (d >= 0) return '0' + d;
        return c switch
        {
            ';' or ':' => 186, '=' or '+' => 187, ',' or '<' => 188, '-' or '_' => 189, '.' or '>' => 190, '/' or '?' => 191,
            '`' or '~' => 192, '[' or '{' => 219, '|' => 220, ']' or '}' => 221, '\'' or '"' => 222, ' ' => VkSpace, _ => 0,
        };
    }

    /// <summary>
    /// What the document holds after each key, typed on a layout in a Shree font (<paramref name="legacy"/>) or in Unicode:
    /// Ø is Ctrl+Space; a key the engine does not take is typed by the application itself.
    /// </summary>
    private List<string> Type(string layoutId, string keys, bool legacy)
    {
        var odia = _config.Languages.First(l => l.Id == "or");
        var injector = new RecordingInjector();
        using var hook = new LowLevelKeyboardHook();
        using var engine = new TypingEngine(hook, injector);
        engine.SetProfile(_config.GetLayout(odia.Layouts.First(l => l.Id == layoutId)), odia.Name, "Unicode");
        engine.SetTransliterator(_config.GetTransliterator(odia));
        engine.SetLegacyConverter(legacy ? _conv : null, null);
        engine.SetEnabled(true);
        var field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(hook);
        var states = new List<string>();
        foreach (char c in keys)
        {
            if (c == CtrlSpace)
            {
                _down.Add(VkControl);
                handler!(hook, new HookKeyEventArgs { VirtualKey = VkSpace, IsKeyDown = true });
                handler!(hook, new HookKeyEventArgs { VirtualKey = VkSpace, IsKeyDown = false });
                _down.Remove(VkControl);
                continue;
            }
            int vk = Vk(c, out bool shift);
            if (shift) _down.Add(VkShift);
            var down = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
            handler!(hook, down);
            handler!(hook, new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = false });
            _down.Remove(VkShift);
            if (!down.Handled) injector.SendText(c.ToString());
            states.Add(injector.Text);
        }
        return states;
    }

    // Words typed key by key (plans the audit found on the real engine): the bytes are Shree's own spelling of the word
    // (its syllables joined, `oritruth common`), and after every key the document holds the converter's bytes for the
    // Unicode the same keys typed so far.
    [Theory]
    [InlineData("or-inscript", "BgbvsMdVj", "ଭୁବନେଶ୍ୱର", "B5 FF EB AF FF 7B 5B FF C9 B4 C0 FF")]
    [InlineData("or-inscript", "mUdirl", "ସଙ୍ଗୀତ", "D3 E8 FF EA 86 FF")]                  // U d i: was the chart's ସଙ୍ର
    [InlineData("or-inscript", "k$c", "କର୍ମ", "4C FF FE F6")]
    [InlineData("ori-typewrit", "cxedekF", "ଜଗନ୍ନାଥ", "66 53 9F E6 24")]                  // d: the link key (was a Latin d)
    [InlineData("ori-typewrit", "lsk", "କୋ", "7B 4C FF E6")]
    [InlineData("ori-typewrit", "s'a", "କର୍ମ", "4C FF FE F6")]
    [InlineData("ori-doe", "cvd^r", "ମନ୍ତ୍ରୀ", "FE A6 EA")]
    [InlineData("ori-newroma", "lok", "ଲୋକ", "7B E0 FF E6 4C FF")]                          // o after a letter: its sign (was ଲଓକ)
    [InlineData("ori-newroma", "jgn=nat=h", "ଜଗନ୍ନାଥ", "66 53 9F E6 24")]                  // =: the link key (was raw 3D, ଣ୍ଠ)
    [InlineData("ori-roma", "mnftRI", "ମନ୍ତ୍ରୀ", "FE A6 EA")]                                // f: the link key
    [InlineData("ori-eng", "ke", "କେ", "7B 4C FF")]                                          // Eng: the e-sign (was କ)
    [InlineData("ori-eng", "kj", "କ୍ଜ", "4C FF 6B")]
    [InlineData("ori-eng", "vidyaalaya", "ବିଦ୍ୟାଲୟ", "AF FF E7 92 FF BF E6 E0 FF DF")]
    public void TypingInAShreeFontWritesShreesBytes(string layoutId, string keys, string word, string bytes)
    {
        var uni = Type(layoutId, keys, false);
        var leg = Type(layoutId, keys, true);
        Assert.Equal(word.Normalize(NormalizationForm.FormC), uni[^1].Normalize(NormalizationForm.FormC));
        Assert.Equal(bytes, ShreeOdiaTruthTests.Hex(leg[^1]));
        Assert.Equal(bytes, ShreeOdiaTruthTests.Hex(_conv.ConvertText(word)));
        for (int i = 0; i < uni.Count; i++)
            Assert.True(_conv.ConvertText(uni[i]) == leg[i], $"after '{keys[..(i + 1)]}': typed {ShreeOdiaTruthTests.Hex(leg[i])}, the converter writes {ShreeOdiaTruthTests.Hex(_conv.ConvertText(uni[i]))}");
    }

    // The keys the audit gave their Shree code: DOE Z is the half key (virama + ZWJ: the visible virama, then the letter in
    // full), Crtronic Ctrl+Space l is the ba-phala (it typed ୀ, the l key's own sign).
    [Fact]
    public void HalfKeyAndBaPhalaKey()
    {
        Assert.Equal("4C FF FA 86 FF", ShreeOdiaTruthTests.Hex(Type("ori-doe", "kZl", true)[^1]));
        Assert.Equal("କ୍‍ତ", Type("ori-doe", "kZl", false)[^1]);
        Assert.Equal("4C FF B4", ShreeOdiaTruthTests.Hex(Type("ori-crtronic", "w" + CtrlSpace + "l", true)[^1]));
        Assert.Equal("କ୍ବ", Type("ori-crtronic", "w" + CtrlSpace + "l", false)[^1]);
    }

    // A key Shree's own keyboard types nothing for, or only a glyph with no Unicode (the hook, code 51), types nothing -
    // never the raw key, whose code draws an Odia glyph in these fonts ('=' 3D ଣ୍ଠ, ';' 3B ନ୍ତ, '`' 60 ଚ), nor the unshifted
    // key's letter, nor a Latin letter from the chart.
    [Theory]
    [InlineData("ori-doe", "B")]
    [InlineData("ori-doe", "=")]
    [InlineData("ori-doe", "]")]
    [InlineData("ori-typewrit", "Q")]
    [InlineData("ori-typewrit", "+")]
    [InlineData("ori-typewrit", ";")]
    [InlineData("ori-newroma", ";")]
    [InlineData("ori-newroma", "#")]
    [InlineData("ori-crtronic", "M")]
    [InlineData("ori-phonetic", "K")]
    [InlineData("ori-roma", "]")]
    [InlineData("or-inscript", "`")]
    public void AKeyShreeLeavesEmptyTypesNothing(string layoutId, string key)
    {
        Assert.Equal("", Type(layoutId, key, true)[^1]);
        Assert.Equal("", Type(layoutId, key, false)[^1]);
    }

    // InScript's Ctrl+Space q and w are % and ‘ (INSCRIPT.ORI codes 25 22), not the chart's repeat of the normal layer.
    [Fact]
    public void InScriptCtrlSpaceLayer()
    {
        Assert.Equal("25", ShreeOdiaTruthTests.Hex(Type("or-inscript", CtrlSpace + "q", true)[^1]));
        Assert.Equal("‘", Type("or-inscript", CtrlSpace + "w", false)[^1]);
    }

    // The ra + virama of a reph key struck before its letter, while it waits: a table that knows the waiting mark main's
    // engine puts after such a word writes the reph alone, F6, as Shree shows it; the word that ends there is ra + the
    // visible virama.
    [Fact]
    public void AWaitingRephIsTheRephAlone()
    {
        string waiting = ((char)0xFDEF).ToString();
        Assert.Equal("F6", ShreeOdiaTruthTests.Hex(_conv.ConvertText("ର୍" + waiting)));
        Assert.Equal("C0 FF FA", ShreeOdiaTruthTests.Hex(_conv.ConvertText("ର୍")));
        Assert.Equal("4C FF FA", ShreeOdiaTruthTests.Hex(_conv.ConvertText("କ୍" + waiting)));
        Assert.Equal("4C FF 75 26 FA", ShreeOdiaTruthTests.Hex(_conv.ConvertText("କ୍ଟ୍" + waiting)));
    }
}
