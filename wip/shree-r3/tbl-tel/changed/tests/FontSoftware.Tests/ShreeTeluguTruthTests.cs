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
/// The Shree-Lipi Telugu tables against what Shree's own composers write. The fixture (Data/shree-tel7-truth.json) holds
/// 26,618 Unicode strings - every letter with every vowel sign (gunintham) and tail, every two-letter cluster (vattu) with
/// every sign and tail, the ra-vattu struck before its letter, క్ష and its clusters, the halant, the independent vowels,
/// digits and punctuation, and the word-final virama - with the bytes S8Tel764 (Shree-Lipi 7) types for each and the
/// other spellings it accepts. Nothing here needs Shree installed.
///
/// 6,235 rows are documented exceptions (the fixture's "exceptions"): where the composer's bytes draw something other than
/// the text in every installed face - ః 40 and ఁ B6 are talakattu ticks (the glyphs are 5D and 97), its ్ఙ 5D draws the
/// visarga (the subjoined ఙ is 94), its ్ఱ CA draws a ూ tail (the subjoined ఱ is 3C), it drops ఝ after the link - the table
/// writes what the fonts draw for the text, and the composer's own spelling stays accepted and is read back.
///
/// The 7 faces (SHREE-TEL7-Apoorva/Asmitha) and the 4/5/6 faces (SHREE-TEL-Apoorva/Asmitha) are the same design code for
/// code: one table, shree-tel7. Shree-Tel7-001/002 draw a star at 92, where the others draw ₹: shree-tel7w. tools/TelTruth
/// builds both table pairs from the capture.
/// </summary>
public class ShreeTeluguTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted, string? Composer, string? Why);

    private static readonly Lazy<List<Row>> Truth = new(LoadTruth);
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    public static readonly TheoryData<string> Families = new() { "shree-tel7", "shree-tel7w" };

    private static FontConverter To(string id) => Assets.Value.CreateConverter("gautami", id)!;
    private static FontConverter From(string id) => Assets.Value.CreateConverter(id, "gautami")!;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(26618, Truth.Value.Count);
        Assert.Equal(20383, Truth.Value.Count(r => r.Composer is null));
        Assert.Equal(72, Truth.Value.Count(r => r.Why == "word-final"));
        Assert.All(new[] { "shree-tel7", "shree-tel7w" }, id =>
        {
            Assert.NotNull(Assets.Value.CreateConverter("gautami", id));
            Assert.NotNull(Assets.Value.CreateConverter(id, "gautami"));
            // One byte per character: files in these fonts are Windows-1252.
            Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(id));
        });
    }

    // The fonts the language lists use the family's table: SHREE-TEL7-* and SHREE-TEL-* shree-tel7, Shree-Tel7-001/002 shree-tel7w.
    [Fact]
    public void EveryTeluguShreeFontUsesItsFamilysTable()
    {
        var telugu = Assets.Value.Languages.First(l => l.Id == "te");
        foreach (var f in telugu.Fonts.Where(f => f.Family is not null && System.Text.RegularExpressions.Regex.IsMatch(f.Family, "^(SHREE-TEL7?-|Shree-Tel7-)")))
            Assert.Equal(f.Family!.StartsWith("Shree-Tel7-", StringComparison.Ordinal) ? "shree-tel7w" : "shree-tel7", f.ConversionKey);
    }

    // (a) Every truth string alone comes out as the preferred spelling (P1): the composer's own bytes, or the documented exception.
    [Theory]
    [MemberData(nameof(Families))]
    public void TableWritesThePreferredSpelling(string id)
    {
        var conv = To(id);
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Hex(c.Text);
            if (got != r.Pref || c.HasUnmapped) wrong.Add($"{r.U} [{Cps(r.U)}]: want {r.Pref}, got {got} (reported {string.Join("", c.UnmappedCharacters)})");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // The glyph exceptions are exactly the documented substitutions on the composer's own bytes, nothing else.
    [Fact]
    public void TheGlyphExceptionsAreOnlyTheDocumentedSubstitutions()
    {
        int n = 0;
        foreach (var r in Truth.Value.Where(r => r.Composer is { Length: > 0 } && r.Why is not null && !r.Why.Contains("jha-dropped") && r.Why != "word-final"))
        {
            n++;
            string fixedUp = string.Join(" ", r.Composer!.Split(' ').Select(b =>
                b == "40" ? "5D" : b == "B6" ? "97" : b == "5D" && r.U.Contains("్ఙ") ? "94" : b == "CA" && r.U.Contains("్ఱ") ? "3C" : b));
            Assert.True(fixedUp == r.Pref, $"{r.U}: composer {r.Composer} -> {fixedUp}, preferred {r.Pref} ({r.Why})");
            Assert.Contains(r.Composer!, r.Accepted);
        }
        Assert.Equal(5586, n);
    }

    // (b) Strings joined into words: no pair or rule reaches across a syllable.
    [Theory]
    [InlineData("shree-tel7", 2026)]
    [InlineData("shree-tel7", 7)]
    [InlineData("shree-tel7w", 2026)]
    [InlineData("shree-tel7w", 7)]
    public void JoinedSyllablesConvertExactly(string id, int seed)
    {
        var conv = To(id);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, seed))
        {
            string want = string.Join(" ", parts.Select(p => p.Pref));
            string got = Hex(conv.ConvertText(u));
            if (got != want) wrong.Add($"{u} [{Cps(u)}]: want {want}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every spelling the composer writes - preferred and every other one it accepts, its own glyph-error bytes (40 B6
    // 5D CA) too - reads back as its text, or as text the table writes with those very bytes; never Latin, a control code
    // or a noncharacter (P5). (Where the composer dropped ఝ after the link its bytes hold no ఝ: they read as the text they show.)
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryComposerSpellingReadsBack(string id)
    {
        var to = To(id);
        var from = From(id);
        var wrong = new List<string>();
        int n = 0, exact = 0;
        foreach (var r in Truth.Value)
            foreach (var spelling in r.Accepted.Distinct())
            {
                n++;
                string got = from.ConvertText(FromHex(spelling));
                if (Malformed(got).Any(b => b.StartsWith("U+"))) { wrong.Add($"{spelling}: read {got} [{Cps(got)}] for {r.U}"); continue; }
                if (got == r.U) { exact++; continue; }
                if (r.Why?.Contains("jha-dropped") == true && spelling != r.Pref) continue;
                var again = to.Convert(got);
                if (Hex(again.Text) != spelling && !(r.Composer == spelling && Hex(again.Text) == r.Pref))
                    wrong.Add($"{spelling}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}] (written back {Hex(again.Text)})");
                else if (Malformed(got).Count > 0 && Malformed(r.U).Count == 0)
                    wrong.Add($"{spelling}: read {got} for {r.U}: {string.Join("; ", Malformed(got))}");
            }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.True(exact > 30000, $"{id}: only {exact} of {n} exact");
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void JoinedSpellingsReadBack(string id)
    {
        var to = To(id);
        var from = From(id);
        var pool = Truth.Value.Where(r => !r.U.Any(c => c is '\u200C' or '\u200D')).ToList();
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

    // (d) A virama that ends a word is the visible halant (P1, tools/Shared/ShreeWordFinal.cs): the composer's own spelling
    // of C + ్ + ZWNJ - its pending link writes the bare letter and loses the virama. Every letter, క్ష and the ra-vattu too,
    // at the end, before a space, punctuation or a digit; and read back as the virama alone.
    [Theory]
    [InlineData("ఫోన్", "B8 F9 AF 8C FE")]
    [InlineData("కంప్యూటర్", "4D E6 FC 85 E7 B3 4E C5 72 C6 8A FF")]
    [InlineData("హైదరాబాద్", "F2 DF FD 4F A7 E6 FE C6 3E BB EA A7 8A FE")]
    [InlineData("క్ క", "4D 8A FC 20 4D E6 FC")]
    [InlineData("క్।", "4D 8A FC 91")]
    [InlineData("క్,", "4D 8A FC 2C")]
    [InlineData("క్1", "4D 8A FC 31")]
    [InlineData("క్ష్", "84 8A FC")]
    [InlineData("క్ర్", "7B 4D 8A FC")]
    [InlineData("ఫిల్మ్", "ED 9C CC 8C FD C3")]          // a cluster: the virama on its first letter, as a sign is
    public void AViramaThatEndsAWordIsTheVisibleHalant(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            var c = To(id).Convert(unicode);
            Assert.True(bytes == Hex(c.Text), $"{id}: {unicode} want {bytes}, got {Hex(c.Text)}");
            Assert.Empty(c.UnmappedCharacters);
            Assert.Equal(unicode, From(id).ConvertText(FromHex(bytes)));
        }
        var halants = Truth.Value.Where(r => r.U.Length > 2 && r.U[^1] == '\u200C' && r.U[^2] == '్').ToList();
        Assert.Equal(72, halants.Count);
        Assert.All(halants, r => Assert.Equal(r.Pref, Hex(To("shree-tel7").ConvertText(r.U[..^1]))));
    }

    // (e) Spellings worth naming, in both families, and back.
    [Theory]
    [InlineData("క", "4D E6 FC")]
    [InlineData("కై", "4D F0 FC 4F")]                 // ై = the e-shape + 4F, after the letter
    [InlineData("క్కై", "4D F0 FC 4F 50")]            // ... before the vattu
    [InlineData("ఖ్ఖై", "52 F1 FD 95 55")]            // ... 95 before a wide vattu
    [InlineData("క్కి", "4D EC FC 50")]               // a sign on a cluster shapes its first letter
    [InlineData("క్కృ", "4D E6 FC 50 A0 96")]         // ృ after the vattu, behind the spacer A0
    [InlineData("క్ర", "7B 4D E6 FC")]                // the ra-vattu hook before its letter
    [InlineData("క్క్ర", "7B 4D E6 FC 50")]
    [InlineData("క్రై", "5B 4D F0 FC 4F")]            // the deeper hook with ై
    [InlineData("ప్రభుత్వం", "7B E7 B3 BF E6 FD 24 99 E8 FE D3 85")]
    [InlineData("స్త్ర", "E7 DC 88")]                // combined ్త్ర
    [InlineData("రాష్ట్రం", "C6 3E E7 D9 89 85")]     // combined ్ట్ర
    [InlineData("స్త్రీ", "EF DC 88")]
    [InlineData("శ్రీ", "7D")]
    [InlineData("క్ష", "84 E6 FC")]
    [InlineData("క్ష్క", "84 E6 FC A0 50")]           // క్ష's vattu behind a spacer
    [InlineData("క్ష్ఖ", "84 E6 FC 9B 55")]
    [InlineData("క్ష్ఖి", "84 EC FC 9B 55")]
    [InlineData("లక్ష్మి", "CB 4D EC FC 87")]         // క్ష్మ = క + 87
    [InlineData("ప్పు", "E7 B3 9A")]
    [InlineData("కః", "4D E6 FC 5D")]                // the visarga glyph (the composer's 40 is a tick)
    [InlineData("కఁ", "4D E6 FC 97")]                // the candrabindu glyph (the composer's B6 is a tick)
    [InlineData("క్ఙ", "4D E6 FC 94")]               // the subjoined ఙ (the composer's 5D is the visarga)
    [InlineData("క్ఱ", "4D E6 FC 3C")]               // the subjoined ఱ (the composer's CA is a ూ tail)
    [InlineData("క్\u200Cఝ", "4D 8A FC 6D")]               // no subjoined ఝ in any face
    [InlineData("ఋ", "BA 24 24")]
    [InlineData("ౠ", "BA 24 2A")]
    [InlineData("క్\u200Cక", "4D 8A FC 4D E6 FC")]          // the visible halant keeps the letters apart
    [InlineData("ఽ", "AC")]
    [InlineData("।", "91")]
    public void NamedSpellings(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            var c = To(id).Convert(unicode);
            Assert.True(bytes == Hex(c.Text), $"{id}: {unicode} want {bytes}, got {Hex(c.Text)}");
            Assert.Empty(c.UnmappedCharacters);
            Assert.Equal(unicode, From(id).ConvertText(FromHex(bytes)));
        }
    }

    // A virama before ఝ, or before a ZWJ, is the visible halant: no face has a subjoined ఝ or a half form.
    [Fact]
    public void ViramaBeforeJhaOrZwj()
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            Assert.Equal("4D 8A FC 6D", Hex(To(id).ConvertText("క్ఝ")));
            Assert.Equal("4D 8A FC C6 E6 FF 6E 2A", Hex(To(id).ConvertText("క్ఝా")));
            Assert.Equal("4D 8A FC", Hex(To(id).ConvertText("క్\u200D")));
            Assert.Equal("4D 8A FC 4D E6 FC", Hex(To(id).ConvertText("క్\u200Dక")));
        }
    }

    // Shree's own glyph errors read back as the text typed where the bytes say so: 40 / B6 (ః ఁ), CA (్ఱ), 5D before
    // its own ః ఁ or behind a spacer (్ఙ); the earlier TextSoft table's spellings too (ై's mark before the letter, the
    // talakattu 40 / B6 inside a letter, the double u-hook AC).
    [Theory]
    [InlineData("4D E6 FC 40", "కః")]
    [InlineData("4D E6 FC B6", "కఁ")]
    [InlineData("4D E6 FC CA", "క్ఱ")]
    [InlineData("4D E6 FC 5D 40", "క్ఙః")]
    [InlineData("51 5D A0 96", "ఖ్ఙృ")]
    [InlineData("84 E6 FC 9B 5D", "క్ష్ఙ")]
    [InlineData("4D E6 FC 7B", "క్ర")]                  // the composer's pending hook, struck after the letter
    [InlineData("4F 4D F0 FC", "కై")]                   // the earlier table
    [InlineData("D0 40 FE", "వ")]
    [InlineData("C4 B6 FD 24", "య")]
    [InlineData("E7 9C AC", "ఘు")]
    [InlineData("BA AC", "ఋ")]
    public void ShreesAndTheEarlierTablesSpellingsReadBack(string bytes, string unicode)
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
            Assert.Equal(unicode, From(id).ConvertText(FromHex(bytes)));
    }

    // The ra-vattu struck first (the vendor keyboards' order: TYPEWRIT K, DOE #, then the letter) is Shree's own bytes in a
    // legacy font: a ra-vattu with no letter before it is the hook alone, 7B.
    [Fact]
    public void RaVattuWithNoLetterBeforeIt()
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            Assert.Equal("7B", Hex(To(id).ConvertText("్ర")));
            Assert.Equal("7B 4D E6 FC", Hex(To(id).ConvertText("్రక")));
            Assert.Equal("7B 4D EC FC", Hex(To(id).ConvertText("్రకి")));
            Assert.Equal("5B 4D F0 FC 4F", Hex(To(id).ConvertText("్రకై")));
            Assert.Equal("క్ర", From(id).ConvertText(FromHex("7B 4D E6 FC")));
        }
    }

    // Digits: the fonts draw Latin digits at 30-39, where the composer writes the Telugu digits too; they read back as
    // what is drawn. The dashes: the composer's 9B is a blank glyph (Word shows a gap) - the fonts' hyphen 26 (P4).
    [Fact]
    public void DigitsAndDashes()
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            Assert.Equal("31 32 2E 35", Hex(To(id).ConvertText("౧౨.౫")));
            Assert.Equal("31 32 2E 35", Hex(To(id).ConvertText("12.5")));
            Assert.Equal("12.5", From(id).ConvertText(FromHex("31 32 2E 35")));
            Assert.Equal("4D E6 FC 26 4D E6 FC 26 26 4D E6 FC 26 4D E6 FC", Hex(To(id).ConvertText("క–క—క-క")));
            Assert.Empty(To(id).Convert("–—").UnmappedCharacters);
            Assert.Equal("—", From(id).ConvertText(FromHex("9B 9B")));
            Assert.Equal("–", From(id).ConvertText(FromHex("9B")));
        }
    }

    // ₹ and *: 92 draws ₹ in SHREE-TEL7/SHREE-TEL and a star in Shree-Tel7-001/002 (where no code draws ₹: it passes
    // through, reported, P3b); * (2A draws ా) is left out and reported where no code draws a star.
    [Fact]
    public void RupeeAndStar()
    {
        var a = To("shree-tel7").Convert("₹౧౦౦");
        Assert.Equal("92 31 30 30", Hex(a.Text));
        Assert.Empty(a.UnmappedCharacters);
        var b = To("shree-tel7w").Convert("₹౧౦౦");
        Assert.Equal("U+20B9 31 30 30", Hex(b.Text));
        Assert.Contains("₹", b.UnmappedCharacters);
        var s7 = To("shree-tel7").Convert("2*3");
        Assert.Equal("32 33", Hex(s7.Text));
        Assert.Contains("*", s7.UnmappedCharacters);
        var sw = To("shree-tel7w").Convert("2*3");
        Assert.Equal("32 92 33", Hex(sw.Text));
        Assert.Empty(sw.UnmappedCharacters);
        Assert.Equal("₹", From("shree-tel7").ConvertText(FromHex("92")));
        Assert.Equal("*", From("shree-tel7w").ConvertText(FromHex("92")));
        // P5: ₹ passed through in a Shree-Tel7-00x document reads back as itself, unreported
        var back = From("shree-tel7w").Convert("₹" + FromHex("31"));
        Assert.True(back.Text == "₹1" && back.UnmappedCharacters.Count == 0, Cps(back.Text));
    }

    // (f) ASCII (P2-P4): what every face draws as itself is written as itself, unreported (the digits draw Latin digits);
    // '-' is the hyphen 26 (2D is a blank); any other ASCII - the Latin letters too, whose codes draw Telugu (A 41 = అ) - is
    // left out and reported, never written as a code that draws something else.
    [Theory]
    [InlineData("shree-tel7", "!%()+,./:;=?0123456789", "")]
    [InlineData("shree-tel7", "#$&*<>@[\\]^_`{|}~", "left out")]
    [InlineData("shree-tel7", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "left out")]
    [InlineData("shree-tel7w", "!%()+,./:;=?0123456789", "")]
    [InlineData("shree-tel7w", "#$&<>@[\\]^_`{|}~", "left out")]
    [InlineData("shree-tel7w", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "left out")]
    public void AsciiInTheseFonts(string id, string chars, string how)
    {
        foreach (char ch in chars)
        {
            var c = To(id).Convert(ch.ToString());
            string s = ch.ToString();
            if (how == "") Assert.True(c.Text == s && c.UnmappedCharacters.Count == 0, $"{id} '{ch}': {Hex(c.Text)}");
            else Assert.True(c.Text == "" && c.UnmappedCharacters.Contains(s), $"{id} '{ch}': {Hex(c.Text)}");
            Assert.Equal("4D E6 FC " + (c.Text.Length > 0 ? Hex(c.Text) + " " : "") + "4D E6 FC", Hex(To(id).ConvertText("క" + ch + "క")));
        }
        Assert.Equal("26", Hex(To(id).ConvertText("-")));
        var mixed = To(id).Convert("రాము Ram");
        Assert.Equal("C6 3E D0 E8 FE 24 24 20", Hex(mixed.Text));
        Assert.Contains("R", mixed.UnmappedCharacters);
    }

    // Beyond ASCII, a decision each: the Unicode spaces are the fonts' space; · and ∙ are written B7, the fonts' large
    // bullet, a stand-in, reported; the soft hyphen and the controls are left out, reported; a noncharacter is never taken for
    // one of the tables' marks (U+FFFD, reported). The Telugu no face draws (ౄ ఌ ౡ ౢ ౣ ఀ ఄ ఴ ౘ ౙ ౚ ౝ, the nukta, the
    // length mark ౕ, the fraction digits) passes through as itself, reported (P3b) - and reads back as itself, unreported (P5).
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
        }
        foreach (int sp in new[] { 0x2000, 0x2003, 0x2009, 0x202F, 0x3000 }) Is(sp, "20", false);
        Is(0x2219, "B7", true);
        Is(0x00B7, "B7", true);
        Is(0x00AD, "", true);
        Is(0x0001, "", true);
        Is(0x0092, "", true);
        Is(0x00D7, "", true);
        Is(0x00F7, "", true);
        Is(0x00B0, "", true);
        foreach (int cp in new[] { 0x0C44, 0x0C0C, 0x0C61, 0x0C62, 0x0C63, 0x0C00, 0x0C04, 0x0C34, 0x0C58, 0x0C5D, 0x0C3C, 0x0C55, 0x0C78 })
        {
            Is(cp, $"U+{cp:X4}", true);
            var back = From(id).Convert(FromHex("4D E6 FC") + (char)cp);
            Assert.True(back.Text == "క" + (char)cp && back.UnmappedCharacters.Count == 0, $"{id} U+{cp:X4} reads back as {Cps(back.Text)}");
        }
        foreach (int nc in new[] { 0xFDD1, 0xFDD3, 0xFDD9 })
        {
            var c = to.Convert("క" + (char)nc + "క");
            Assert.Equal("4D E6 FC U+FFFD 4D E6 FC", Hex(c.Text));
            Assert.Contains("\uFFFD", c.UnmappedCharacters);
        }
    }

    [Theory]
    [InlineData("“తెలుగు”", "22 22 99 F0 FE CB 24 56 E6 FC 24 27 27")]   // the fonts' ‘ ’ (22 27), twice
    [InlineData("\"తెలుగు\"", "22 22 99 F0 FE CB 24 56 E6 FC 24 27 27")]
    [InlineData("'తెలుగు'", "22 99 F0 FE CB 24 56 E6 FC 24 27")]
    [InlineData("తెలుగు…", "99 F0 FE CB 24 56 E6 FC 24 2E 2E 2E")]
    [InlineData("• క", "B7 20 4D E6 FC")]
    [InlineData("కా॥", "4D 3E 91 91")]
    public void PunctuationIsWrittenAsTheLookAlikeTheFontsDraw(string unicode, string bytes)
    {
        foreach (var id in new[] { "shree-tel7", "shree-tel7w" })
        {
            var c = To(id).Convert(unicode);
            Assert.Equal(bytes, Hex(c.Text));
            Assert.Empty(c.UnmappedCharacters);
        }
    }

    // (g) Every code point a table writes is in the cmap of every face of its family (Data/shree-tel-cmaps.json, read from
    // the installed faces), and none is one Word or RichEdit draws as nothing (U+00AD, a C1 code - U+0090, the fonts' blank
    // 90, included) or in another font (U+2219): r3\word\final-invisible.txt for SHREE-TEL / SHREE-TEL7 / Shree-Tel7-00x.
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodePointWrittenIsInTheFontsCmapAndDrawnByWord(string id)
    {
        var cmap = Cmaps.Value[id].Codes;
        var to = To(id);
        var table = Assets.Value.FindMapping("gautami", id)!;
        var written = new HashSet<char>();
        void Add(string s) { foreach (char c in s) if (c < '\uFDD0' || c > '\uFDEF') written.Add(c); }
        // (a pair marked unmapped that writes its own character passes it through, reported - P3b)
        foreach (var p in table.Pairs) Add(p.Unmapped ? new string(p.To.Where(c => !p.From.Contains(c)).ToArray()) : p.To);
        foreach (var r in table.PostRules) Add(System.Text.RegularExpressions.Regex.Replace(r.Replace, @"\$\d|\$\$", ""));
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Truth.Value, 5000, 99).Select(w => w.U)))
        {
            var c = to.Convert(u);
            Add(new string(c.Text.Where(ch => !c.UnmappedCharacters.Contains(ch.ToString())).ToArray()));
        }
        var missing = written.Where(c => !cmap.Contains(c)).Select(c => $"U+{(int)c:X4}").ToList();
        Assert.True(missing.Count == 0, $"{id} writes code points no face of its family has: {string.Join(" ", missing)}");
        var invisible = written.Where(c => c is '\u00AD' or '∙' || (c >= '\u0080' && c <= '\u009F')).ToList();
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
            }
    }

    // (h) Every output path carries the same code points (P6): what the tables write is one Windows-1252 byte each, so a .txt
    // file in these fonts holds the very bytes the RTF, .docx and typing paths do.
    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodeWrittenIsOneWindows1252Byte(string id)
    {
        var table = Assets.Value.FindMapping("gautami", id)!;
        var bad = new List<string>();
        foreach (var p in table.Pairs)
            foreach (char c in p.To.Where(c => (c < '\uFDD0' || c > '\uFDEF') && !(p.Unmapped && p.From.Contains(c))))
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
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-tel7-truth.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var rows = new List<Row>();
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            string u = row[0].GetString()!, pref = row[1].GetString()!;
            var acc = new List<string> { pref };
            if (row.GetArrayLength() > 2) acc.AddRange(row[2].EnumerateArray().Select(a => a.GetString()!));
            string? composer = row.GetArrayLength() > 3 ? row[3].GetString() : null;
            string? why = row.GetArrayLength() > 4 ? row[4].GetString() : null;
            rows.Add(new Row(u, pref, acc.Where(a => a.Length > 0).Distinct().ToArray(), composer, why));
        }
        return rows;
    }

    private static readonly Lazy<Dictionary<string, (string[] Faces, HashSet<char> Codes)>> Cmaps = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-tel-cmaps.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var d = new Dictionary<string, (string[], HashSet<char>)>();
        foreach (var fam in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
            d[fam.Name] = (fam.Value.GetProperty("faces").EnumerateArray().Select(f => f.GetString()!).ToArray(),
                fam.Value.GetProperty("codePoints").GetString()!.Split(' ').Select(h => (char)Convert.ToInt32(h, 16)).ToHashSet());
        return d;
    });

    private static bool IsLetter(char c) => c >= 'క' && c <= 'హ';
    private static bool IsVowelSign(char c) => (c >= 'ా' && c <= 'ౌ' && c != '౅' && c != '౉') || c is 'ౕ' or '\u0C56' or 'ౢ' or 'ౣ';
    private static bool IsMark(char c) => IsVowelSign(c) || (c >= 'ఀ' && c <= 'ఄ') || c is '఼' or '్';
    private static bool IsVowel(char c) => (c >= 'అ' && c <= 'ఔ') || c is 'ౠ' or 'ౡ';

    /// <summary>Ways a string is not well formed Telugu (empty when it is): a sign off its letter, Latin, controls or the tables' marks leaking through.</summary>
    private static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            if ((IsVowelSign(c) || c == '్') && !IsLetter(p) && !(c == '\u0C56' && p == 'ె')) bad.Add($"sign {c} at {i}");
            if (c >= 'ఀ' && c <= 'ఄ' && (i == 0 || p == '్' || !(IsLetter(p) || IsVowel(p) || IsMark(p)))) bad.Add($"{c} at {i}");
            if (c is '\u200C' or '\u200D' && p != '్') bad.Add($"joiner at {i}");
            if ((c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') || (c >= '\u0080' && c <= '\u009F') || (c >= '\uFDD0' && c <= '\uFDEF') || (c < ' ' && c is not ('\t' or '\n' or '\r')))
                bad.Add($"U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>Truth strings that can stand next to each other in a word: they start on a base and do not end on a virama or joiner.</summary>
    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] is not ('\u200C' or '\u200D') && r.U[^1] is not ('్' or '\u200C' or '\u200D') && r.Why != "word-final" &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '్' && IsMark(r.U[i]) && r.U[i] != '్'))
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
            if (c == '\u0090') { parts.Add("90"); continue; }
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    internal static string FromHex(string hex) =>
        hex.Length == 0 ? "" : string.Concat(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h => h == "90" ? "\u0090" : Cp1252.GetString(new[] { Convert.ToByte(h, 16) })));

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}

/// <summary>
/// Typing in a Shree-Lipi Telugu font (P6): the real TypingEngine on the vendor keyboards, in a SHREE-TEL7 and a
/// Shree-Tel7-001 profile, puts down the bytes the converter writes for the word - Shree's own spelling - and the keys the
/// per-key audit fixed type what Shree's keyboards type (round1/audit against cap-tel/tel-vendor-keys.json).
/// </summary>
[Collection("typing engine")]
public class ShreeTeluguTypingTests
{
    private const int VkShift = 0x10, VkControl = 0x11;
    private readonly AssetBundle _config = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
    private readonly HashSet<int> _down = new();
    private readonly Dictionary<string, FontConverter> _conv = new();
    private FontConverter Conv(string enc) => _conv.TryGetValue(enc, out var c) ? c : _conv[enc] = _config.CreateConverterToLegacy(enc)!;

    /// <summary>The document after typing <paramref name="keys"/> (Ø = Ctrl+Space) on a layout, in a Shree font (<paramref name="enc"/>) or in Unicode (null).</summary>
    private string Document(string layoutId, string keys, string? enc)
    {
        TypingEngine.KeyDownProbe = vk => _down.Contains(vk);
        var telugu = _config.Languages.First(l => l.Id == "te");
        var layout = _config.GetLayout(telugu.Layouts.First(l => l.Id == layoutId));
        var injector = new RecordingInjector();
        using var hook = new LowLevelKeyboardHook();
        using var engine = new TypingEngine(hook, injector);
        engine.SetProfile(layout, telugu.Name, "Unicode");
        engine.SetTransliterator(layout.Phonetic ? _config.GetTransliterator(telugu) : null);
        engine.SetLegacyConverter(enc is null ? null : Conv(enc), enc is null ? null : "SHREE-TEL7-Apoorva-0900");
        engine.SetEnabled(true);
        var field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<HookKeyEventArgs>?)field.GetValue(hook);
        try
        {
            foreach (char c in keys)
            {
                if (c == 'Ø')
                {
                    _down.Add(VkControl);
                    handler!(hook, new HookKeyEventArgs { VirtualKey = 0x20, IsKeyDown = true });
                    handler!(hook, new HookKeyEventArgs { VirtualKey = 0x20, IsKeyDown = false });
                    _down.Remove(VkControl);
                    continue;
                }
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
                if (!down.Handled) injector.SendText(c.ToString());
            }
        }
        finally { TypingEngine.KeyDownProbe = vk => false; _down.Clear(); }
        return injector.Text;
    }

    // Words typed key by key (the key plans the audit found on the real engine): the Unicode, and in both Shree families the
    // converter's bytes for it - Shree's own spelling.
    [Theory]
    [InlineData("tel-doe", "l*ngig", "తెలుగు", "99 F0 FE CB 24 56 E6 FC 24")]
    [InlineData("tel-doe", "og_Kx", "దుఃఖం", "A7 E6 FE 24 5D 51 85")]
    [InlineData("tel-typewrit", "lpohyjk", "పుస్తకం", "E7 B3 23 E7 DC A2 4D E6 FC 85")]
    [InlineData("tel-typewrit", "okohjzyk", "సంస్కృతం", "E7 DC 85 E7 DC 50 A0 96 99 E8 FE 85")]
    [InlineData("te-inscript", "kwnemx", "కైలాసం", "4D F0 FC 4F CC EA E7 DC 85")]
    // the ై key on every letter (it typed Cఐ్ on all but క: the key's text was the composer's placeholder ఐ్)
    [InlineData("tel-doe", "jw", "రై", "C6 F0 FF 4F")]
    [InlineData("tel-doe", "jwng", "రైలు", "C6 F0 FF 4F CB 24")]
    // the halant key (Shree's 44): the visible virama, no conjunct (it typed the bare virama: గ్క as a conjunct)
    [InlineData("tel-godrej", "j@j", "క్\u200Cక", "4D 8A FC 4D E6 FC")]
    // TYPEWRIT @ is Shree's slot link + ఞ: the subjoined ఞ (it typed the full ఞ)
    [InlineData("tel-typewrit", "j@", "క్ఞ", "4D E6 FC 71")]
    // INDICA's conjunct keys: త్ర and శ్ర (they typed ్రత, ్రశ)
    [InlineData("tel-indica", "^", "త్ర", "7B 99 E8 FE")]
    [InlineData("tel-indica", "*", "శ్ర", "7B D4 E8 FD")]
    public void TypingWritesTheConverterBytes(string layoutId, string keys, string unicode, string bytes)
    {
        Assert.Equal(unicode, Document(layoutId, keys, null));
        foreach (var enc in new[] { "shree-tel7", "shree-tel7w" })
        {
            Assert.Equal(bytes, ShreeTeluguTruthTests.Hex(Conv(enc).ConvertText(unicode)));
            Assert.True(bytes == ShreeTeluguTruthTests.Hex(Document(layoutId, keys, enc)), $"{layoutId} {enc} {keys}: want {bytes}, got {ShreeTeluguTruthTests.Hex(Document(layoutId, keys, enc))}");
        }
    }

    // The ra-vattu struck first, as on Shree's own keyboards: in a Shree font the bytes are Shree's (7B before the letter).
    [Theory]
    [InlineData("tel-doe", "#k", "7B 4D E6 FC")]
    [InlineData("tel-typewrit", "Kj", "7B 4D E6 FC")]
    public void RaVattuStruckFirstInAShreeFont(string layoutId, string keys, string bytes)
    {
        foreach (var enc in new[] { "shree-tel7", "shree-tel7w" })
            Assert.Equal(bytes, ShreeTeluguTruthTests.Hex(Document(layoutId, keys, enc)));
    }

    // Keys Shree's keyboard leaves empty (FF) type nothing (they typed the unshifted key's letter, or the raw key into the
    // Shree font); ఎ is typed without a ZWNJ.
    [Theory]
    [InlineData("tel-crtronic", "A", "")]
    [InlineData("tel-crtronic", "W", "")]
    [InlineData("tel-doe", "`", "")]
    [InlineData("tel-vision", "B", "")]
    [InlineData("tel-doe", "S", "ఎ")]
    [InlineData("tel-doe", "w", "ఐ")]
    [InlineData("tel-itr", "a", "")]
    [InlineData("tel-itr", "Øa", "గ")]
    // Shree's code E1 (a ra-vattu hook only the 7 composer writes, after the syllable; the Unicode composer types
    // nothing): no text to type, where the unshifted key's letter came out (APPLE Shift+Q typed అ, VISION > a full stop).
    [InlineData("tel-apple", "Q", "")]
    [InlineData("te-inscript", "B", "")]
    [InlineData("tel-roma", "X", "")]
    [InlineData("tel-vision", ">", "")]
    [InlineData("tel-apple", "jQ", "క")]
    public void KeysTypeWhatShreesKeyboardsType(string layoutId, string keys, string unicode)
    {
        Assert.Equal(unicode, Document(layoutId, keys, null));
    }
}
