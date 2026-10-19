using System.Text;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The Shree-Lipi Gujarati tables against what Shree's own composers write. The
/// fixture (Data/shree-guj7-truth.json) holds 17,335 Unicode strings - every letter
/// with every sign, every two- and most three-letter clusters, reph, ra- and ya-signs
/// - with the bytes the Shree-Lipi 7 composer types for each and the other spellings
/// it also accepts. Nothing here needs Shree installed.
///
/// The 7 fonts (SHREE-GUJ7-*) and the 4/5/6 fonts (SHREE-GUJ-*) share every code but
/// five: 91 92 93 94 B6 are the ઙ conjuncts ઙ્ક ઙ્ખ ઙ્ગ ઙ્ઘ ઙ્ક્ષ in 4/5/6, and copies of
/// પ થ ન સ્ત્ર and the ્રૂ mark in 7. Each family has its own pair of tables.
///
/// A row that ends in a bare virama holds what the composer shows while it waits for the
/// next letter - a half form, which drawn alone is a broken letter. At the end of a word
/// the tables write the visible halant instead, as every Unicode font draws it; the
/// fixture's "exceptions" say what they write for each such row ("the table writes ...").
/// </summary>
public class ShreeComposerTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    private static readonly Lazy<List<Row>> Truth = new(LoadTruth);
    private static readonly Lazy<Dictionary<string, string>> Exceptions = new(() => ShreeFixture.LoadExceptions("shree-guj7-truth.json"));

    /// <summary>The bytes the tables write for a row: the composer's, or the fixture's exception.</summary>
    private static string Target(Row r) => Exceptions.Value.TryGetValue(r.U, out var why) ? ShreeFixture.TableWrites(why) : r.Pref;
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    // The 7 composer's spelling of each ઙ conjunct, and the 4/5/6 code for it.
    private static readonly (string Seven, string Code, string Unicode)[] Nga =
    {
        ("50 B9 24 4C 24", "91", "ઙ્ક"),
        ("50 B9 24 6E", "B6", "ઙ્ક્ષ"),
        ("50 B9 24 4D", "92", "ઙ્ખ"),
        ("50 B9 24 4E", "93", "ઙ્ગ"),
        ("50 B9 24 4F", "94", "ઙ્ઘ"),
    };

    private static readonly string[] FamilyCodes = { "91", "92", "93", "94", "B6" };

    // The vocalic-l letters and signs and the joiners have no glyph: the bytes carry no trace of them.
    private static readonly string NoGlyph = "ઌૡૢૣ‌‍";

    private static FontConverter To7 => Assets.Value.CreateConverter("shruti", "shree-guj7")!;
    private static FontConverter To456 => Assets.Value.CreateConverter("shruti", "shree-guj456")!;
    private static FontConverter From7 => Assets.Value.CreateConverter("shree-guj7", "shruti")!;
    private static FontConverter From456 => Assets.Value.CreateConverter("shree-guj456", "shruti")!;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(17335, Truth.Value.Count);
        // The rows that end in a bare virama and are drawn with the halant at the end of a word
        // (the letters with no half form - ઙ છ ટ ઠ ડ ઢ દ - the composer already writes so).
        // And (2026-10-18) the ru/ruu ligatures Shree's own ru and ruu keys type (8 rows), and ૰,
        // whose code 8F Word draws as nothing: passed through as itself and reported (the output policy, P3b).
        Assert.Equal(927, Exceptions.Value.Count);
        Assert.Equal(918, Exceptions.Value.Count(e => ShreeFixture.IsWordFinal(e.Value)));
        Assert.Equal(8, Exceptions.Value.Count(e => ShreeFixture.IsRuLigature(e.Value)));
        Assert.All(Exceptions.Value, e =>
        {
            if (ShreeFixture.IsWordFinal(e.Value)) Assert.EndsWith("્", e.Key);
            else Assert.True(ShreeFixture.IsRuLigature(e.Value) || ShreeFixture.IsPolicy(e.Value), e.Value);
            Assert.Contains(Truth.Value, r => r.U == e.Key);
        });
        Assert.All(new[] { "shree-guj7", "shree-guj456" }, enc =>
        {
            Assert.NotNull(Assets.Value.CreateConverter("shruti", enc));
            Assert.NotNull(Assets.Value.CreateConverter(enc, "shruti"));
            // One byte per character: files in these fonts are Windows-1252.
            Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(enc));
        });
    }

    // (a) Every truth string alone comes out as a spelling the composer writes.
    [Fact]
    public void SevenTableWritesTheComposerSpelling()
    {
        var conv = To7;
        var wrong = new List<string>();
        int preferred = 0;
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Composer(c.Text, r.U), want = Target(r);
            if (got == want) preferred++;
            bool ok = want == r.Pref ? r.Accepted.Contains(got) : got == want;
            if (!ok || c.HasUnmapped != Reported(r)) wrong.Add($"{r.U} [{Cps(r.U)}]: want {want}, got {got}{(c.HasUnmapped ? " (reported)" : "")}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.Equal(Truth.Value.Count, preferred);
    }

    [Fact]
    public void FourFiveSixTableWritesTheComposerSpelling()
    {
        var conv = To456;
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Composer(c.Text, r.U), want = Target(r);
            bool ok = want == r.Pref
                ? r.Accepted.Contains(got) || r.Accepted.Any(a => With456Codes(a) == got)
                : got == want || got == With456Codes(want);
            if (!ok || c.HasUnmapped != Reported(r)) wrong.Add($"{r.U} [{Cps(r.U)}]: want {want} (or {With456Codes(want)}), got {got}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (b) Strings joined into words: no pair or rule may reach across a syllable.
    [Fact]
    public void JoinedSyllablesConvertExactly()
    {
        var to7 = To7;
        var to456 = To456;
        var alone456 = Syllables(Truth.Value).ToDictionary(r => r.U, r => to456.ConvertText(r.U), StringComparer.Ordinal);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, 2026))
        {
            string want = string.Join(" ", parts.Select(Target).Where(p => p.Length > 0));
            string got = Composer(to7.ConvertText(u), u);
            if (got != want) wrong.Add($"7: {u} [{Cps(u)}]: want {want}, got {got}");
            string want456 = string.Concat(parts.Select(p => alone456[p.U]));
            if (to456.ConvertText(u) != want456) wrong.Add($"4/5/6: {u} [{Cps(u)}]: want {Hex(want456)}, got {Hex(to456.ConvertText(u))}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every preferred spelling reads back to its Unicode, or to text the composer writes the same way.
    [Fact]
    public void PreferredSpellingsReadBack()
    {
        var to7 = To7;
        var from7 = From7;
        var wrong = new List<string>();
        foreach (var r in Truth.Value.Where(r => r.Pref.Length > 0))
        {
            // The composer's spelling - for a word-final row, what it shows while it waits - and the table's.
            foreach (string bytes in new[] { r.Pref, Target(r) }.Distinct().Where(b => b.Length > 0))
            {
                string got = from7.ConvertText(FromHex(bytes));
                if (!SameText(got, r.U, bytes, to7)) wrong.Add($"{bytes}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
            }
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    [Fact]
    public void FourFiveSixSpellingsReadBack()
    {
        var to456 = To456;
        var from456 = From456;
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            string legacy = to456.ConvertText(r.U);
            if (legacy.Length == 0) continue;
            string got = from456.ConvertText(legacy);
            if (!SameText(got, r.U, Hex(legacy), to456)) wrong.Add($"{Hex(legacy)}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    [Fact]
    public void JoinedSpellingsReadBack()
    {
        var to7 = To7;
        var from7 = From7;
        // Syllables with a character the font cannot draw are left out: their bytes are
        // the same as without it (a reph on an invisible ૡ reads as a reph on the letter before).
        var visible = Truth.Value.Where(r => r.U.IndexOfAny(NoGlyph.ToCharArray()) < 0).ToList();
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(visible, 20000, 7))
        {
            string bytes = string.Join(" ", parts.Select(p => p.Pref).Where(p => p.Length > 0));
            string got = from7.ConvertText(FromHex(bytes));
            if (!SameText(got, u, bytes, to7)) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // In 7 the five codes are copies; in 4/5/6 they are the ઙ conjuncts.
    [Theory]
    [InlineData("91", "પ", "ઙ્ક")]                       // પ | ઙ્ક
    [InlineData("92", "થ", "ઙ્ખ")]                       // થ | ઙ્ખ
    [InlineData("93", "ન", "ઙ્ગ")]                       // ન | ઙ્ગ
    [InlineData("94", "સ્ત્ર", "ઙ્ઘ")] // સ્ત્ર | ઙ્ઘ
    [InlineData("B6", "્રૂ", "ઙ્ક્ષ")] // ્રૂ | ઙ્ક્ષ
    public void FamilyCodesReadAsTheirFamilyDrawsThem(string code, string seven, string fourFiveSix)
    {
        Assert.Equal(seven, From7.ConvertText(FromHex(code)));
        Assert.Equal(fourFiveSix, From456.ConvertText(FromHex(code)));
    }

    // (d) Neither table writes a code that draws something else in its family.
    [Fact]
    public void NoTableWritesACodeThatDrawsSomethingElse()
    {
        var family = FamilyCodes.Select(FromHex).ToArray();
        var seven = Assets.Value.FindMapping("shruti", "shree-guj7")!;
        var four = Assets.Value.FindMapping("shruti", "shree-guj456")!;

        // 7: never, from any pair or rule.
        Assert.DoesNotContain(seven.Pairs, p => p.To.IndexOfAny(family.Select(f => f[0]).ToArray()) >= 0);
        Assert.DoesNotContain(seven.PostRules, r => r.Replace.IndexOfAny(family.Select(f => f[0]).ToArray()) >= 0);
        // 4/5/6: only for its own ઙ conjunct - a pair from it, or a rule from its 7 spelling.
        foreach (var (seven7, code, uni) in Nga)
        {
            foreach (var p in four.Pairs.Where(p => p.To.Contains(FromHex(code))))
                Assert.Contains(uni, p.From);
            foreach (var r in four.PostRules.Where(r => r.Replace.Contains(FromHex(code))))
                Assert.Contains(FromHex(seven7), System.Text.RegularExpressions.Regex.Unescape(r.Pattern));
        }

        // And over all the truth, alone and in words.
        var to7 = To7;
        var to456 = To456;
        var texts = Truth.Value.Select(r => r.U).Concat(Words(Truth.Value, 5000, 99).Select(w => w.U));
        foreach (string u in texts)
        {
            string s7 = Hex(to7.ConvertText(u));
            Assert.False(FamilyCodes.Any(c => (" " + s7 + " ").Contains(" " + c + " ")), $"7 wrote a 4/5/6 code for {u}: {s7}");
            string s456 = Hex(to456.ConvertText(u));
            foreach (var (_, code, uni) in Nga)
                if ((" " + s456 + " ").Contains(" " + code + " "))
                    Assert.True(u.Contains(uni), $"4/5/6 wrote {code} for {u} [{Cps(u)}] without {uni}: {s456}");
        }
    }

    // (e) Spellings worth naming.
    [Theory]
    [InlineData("ગ્ર", "4E B0", "4E B0")]                         // ગ્ર: ga + ra-sign, not half-ga + ra
    [InlineData("ર્કિ", "71 4C AE 24", "71 4C AE 24")]       // ર્કિ: i-sign first, reph after the letter
    [InlineData("ર્કી", "4C 24 7D", "4C 24 7D")]             // ર્કી: ii and reph are one glyph
    [InlineData("ક્કિ", "5B BC 4C 24", "5B BC 4C 24")]       // ક્કિ: the long i-sign over a cluster
    [InlineData("ખિં", "74 4D", "74 4D")]                         // ખિં: i-sign and anusvara are one glyph
    [InlineData("કુ", "4C 79 24", "4C 79 24")]                         // કુ: the sign goes before the spacer
    [InlineData("ઙ્ક", "50 B9 24 4C 24", "91")]                   // ઙ્ક: half-ઙ + ક in 7, its own glyph in 4/5/6
    [InlineData("ઙ્ક્ષ", "50 B9 24 6E", "B6")]          // ઙ્ક્ષ
    [InlineData("પો", "60 70 A1", "60 70 A1")]                         // પો: પ is 60 in both
    [InlineData("ગુજરાત", "4E 79 53 3E 66 70 73", "4E 79 53 3E 66 70 73")] // ગુજરાત
    public void NamedSpellings(string unicode, string seven, string fourFiveSix)
    {
        Assert.Equal(seven, Hex(To7.ConvertText(unicode)));
        Assert.Equal(fourFiveSix, Hex(To456.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(seven)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(fourFiveSix)));
    }

    // (f) A virama that ends a word - before a space, punctuation, the danda, or at the end
    // of the text - is the visible halant, as every Unicode font draws it. The tables used to
    // write the half form the composer shows while it waits for the next letter (વાક્ was
    // 68 70 BC), which drawn alone is a broken letter.
    [Theory]
    [InlineData("વાક્", "68 70 4C B9 24")]             // the end of the text: ક્ as the composer writes ક્+ZWNJ
    [InlineData("સત્", "6B 73 B9")]
    [InlineData("અર્થાત્", "41 5C 70 AE 73 B9")]
    [InlineData("ભગવાન્", "63 4E 68 70 5F B9")]
    [InlineData("સત્ છે", "6B 73 B9 20 52 3E A1")]     // before a space
    [InlineData("વાક્,", "68 70 4C B9 24 2C")]          // before punctuation
    [InlineData("ભગવાન્.", "63 4E 68 70 5F B9 2E")]
    [InlineData("સત્।", "6B 73 B9 97 26")]              // before the danda
    [InlineData("સ્ત્", "F5 73 B9")]                    // a cluster: the half સ્ as before ત, then ત્ with the halant
    [InlineData("ક્ષ્", "6E B9")]                       // a ligature keeps it, the halant after (the composer's ક્ષ્+ZWNJ)
    [InlineData("ર્", "66 B9")]                         // not a reph with nothing under it: ર with the halant
    public void AViramaThatEndsAWordIsTheHalant(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));
    }

    // Inside a word the virama still joins the next letter, as the composer writes it.
    [Theory]
    [InlineData("ત્ર", "D3")]
    [InlineData("ક્ષ", "6E")]
    [InlineData("સ્ત", "F5 73")]
    [InlineData("સ્તુતિ", "F5 73 79 72 73")]
    [InlineData("ક્‍", "BC")]                      // an explicit half form (ZWJ) stays the half form
    [InlineData("ક્‌", "4C B9 24")]                // and an explicit halant (ZWNJ) the halant
    public void InsideAWordTheViramaStillJoins(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
    }

    // (g) A reph on a ઙ conjunct. In 4/5/6 the conjunct is one code - 91 ઙ્ક, 92 ઙ્ખ, 93 ઙ્ગ,
    // 94 ઙ્ઘ, B6 ઙ્ક્ષ - and the reph after it must move back before the whole conjunct. The
    // reverse table used to take these codes for what they are in 7 (copies of પ થ ન સ્ત્ર,
    // and B6 a sign), so ર્ઙ્ગા (93 70 AE) read back as ઙ્ગર્ા.
    [Theory]
    [InlineData("93 70 AE", "ર્ઙ્ગા")]
    [InlineData("93 7D", "ર્ઙ્ગી")]                 // ii + reph: one glyph
    [InlineData("91 70 AE 4C 24", "ર્ઙ્કાક")]
    [InlineData("92 AE", "ર્ઙ્ખ")]
    [InlineData("93 AE", "ર્ઙ્ગ")]
    [InlineData("94 AE", "ર્ઙ્ઘ")]
    [InlineData("B6 AE", "ર્ઙ્ક્ષ")]
    [InlineData("B6 70 AE", "ર્ઙ્ક્ષા")]
    [InlineData("4C 24 92 A3", "કર્ઙ્ખે")]           // e + reph: one glyph, after a letter
    public void FourFiveSixRephOnANgaConjunctReadsBack(string bytes, string unicode) =>
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));

    [Fact]
    public void EveryRephOnANgaConjunctReadsBack()
    {
        string[] nga = { "ઙ્ક", "ઙ્ખ", "ઙ્ગ", "ઙ્ઘ", "ઙ્ક્ષ" };
        string[] signs = { "", "ા", "િ", "ી", "ુ", "ૂ", "ૃ", "ૅ", "ે", "ૈ", "ૉ", "ો", "ૌ", "ં", "ઁ", "ઃ", "ેં", "ૈં", "ીં", "િં", "ોં" };
        var wrong = new List<string>();
        foreach (var (to, from, name) in new[] { (To7, From7, "7"), (To456, From456, "4/5/6") })
            foreach (var g in nga) foreach (var s in signs) foreach (var after in new[] { "", "ક", "કા", "મ" })
            {
                string u = "ર્" + g + s + after;
                string bytes = to.ConvertText(u), back = from.ConvertText(bytes);
                if (back != u) wrong.Add($"{name}: {u} -> {Hex(bytes)} -> {back} [{Cps(back)}]");
            }
        Assert.True(wrong.Count == 0, $"{wrong.Count} of 840 wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (h) A word that ends in a reph cluster and a virama: the reph sits on the last letter,
    // before its halant. The tables used to put it on the letter before - આર્ત્ was
    // 41 70 AE 73 B9, the reph over ા - because the reph rule did not see a letter followed by
    // the word-final halant as a letter.
    [Theory]
    [InlineData("આર્ત્", "41 70 73 AE B9")]
    [InlineData("ધર્મ્ છે", "5E 64 AE B9 20 52 3E A1")]
    [InlineData("ઊર્ક્", "45 4C AE B9 24")]        // the halant before the letter's spacer, as in ક્‌ = 4C B9 24
    [InlineData("ર્છ્", "52 B9 3E AE")]            // a tall letter keeps its sign above after the spacer
    public void ARephBeforeAWordFinalViramaSitsOnItsLetter(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));
    }

    [Fact]
    public void EveryConsonantTakesTheRephBeforeAWordFinalVirama()
    {
        var wrong = new List<string>();
        foreach (var (to, from, name) in new[] { (To7, From7, "7"), (To456, From456, "4/5/6") })
            foreach (var c in Consonants)
                foreach (var before in new[] { "આ", "ધ", "ઊ", "કા" })
                    foreach (var tail in new[] { "", " છે", "," })
                    {
                        string u = before + "ર્" + c + "્" + tail;
                        string bytes = to.ConvertText(u), back = from.ConvertText(bytes);
                        if (bytes.StartsWith(to.ConvertText(before) + FromHex("AE"), StringComparison.Ordinal) || back != u)
                            wrong.Add($"{name}: {u} -> {Hex(bytes)} -> {back}");
                    }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (i) The nukta reads back right after its letter. The bytes have it before the glyph it
    // sits under, which can be a letter with its vowel sign (જા C5) or with the ra-, ya- or
    // va-sign (ક્ર BE): જ઼ા used to read back as જા઼, ક઼્ર as ક્ર઼.
    [Theory]
    [InlineData("જ઼ા", "9C C5")]
    [InlineData("જ઼ી", "9C C6")]
    [InlineData("જ઼ો", "9C C5 A1")]
    [InlineData("ક઼્ર", "9C BE 24")]
    [InlineData("ખ઼્ય", "9C BF 65")]
    [InlineData("ફ઼િ", "71 9C 61")]                // the i-sign first, the nukta before its letter
    public void TheNuktaReadsBackAfterItsLetter(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));
    }

    [Fact]
    public void EveryLetterWithANuktaAndASignReadsBack()
    {
        string[] signs = { "", "ા", "િ", "ી", "ુ", "ૂ", "ૃ", "ૄ", "ૅ", "ે", "ૈ", "ૉ", "ો", "ૌ", "ં", "ઁ", "ઃ", "્ર", "્ય", "્વ", "િં", "ીં", "ેં", "ૈં", "ોં", "્રા", "્યા" };
        var wrong = new List<string>();
        int n = 0;
        foreach (var (to, from, name) in new[] { (To7, From7, "7"), (To456, From456, "4/5/6") })
            foreach (var c in Consonants)
                foreach (var s in signs)
                {
                    // ર઼ is no Gujarati letter: ર઼ with the ya- or va-sign is written with the reph glyph.
                    if (c == "ર" && (s.StartsWith("્ય") || s.StartsWith("્વ"))) continue;
                    n++;
                    string u = c + "઼" + s;
                    string bytes = to.ConvertText(u), back = from.ConvertText(bytes);
                    if (back != u) wrong.Add($"{name}: {u} -> {Hex(bytes)} -> {back} [{Cps(back)}]");
                }
        Assert.True(n > 1800, n.ToString());
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (j) A virama before ન is the letter's own. ્ન alone is ન 5F (the composer drops a virama
    // with nothing before it), and the tables used to apply that after any cluster with no
    // half form: જ્ઞ્ન was 6F 5F, which draws જ્ઞન.
    [Theory]
    [InlineData("જ્ઞ્ન", "6F B9 5F")]
    [InlineData("જ્ઞ્મ", "6F B9 64")]
    [InlineData("જ્ઞ્નિ", "5B 6F B9 5F")]
    [InlineData("ક્ષ્ન", "FF 5F")]                  // a cluster with a half form keeps taking it
    [InlineData("્ન", "5F")]
    public void AViramaBeforeNaIsKept(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
    }

    [Fact]
    public void JnaWithNaAndEverySignKeepsItsVirama()
    {
        string[] signs = { "", "ા", "િ", "ી", "ુ", "ૂ", "ૃ", "ૅ", "ે", "ૈ", "ૉ", "ો", "ૌ", "ં", "ઁ", "ઃ", "્ય", "્વ", "ીં", "ેં", "ોં", "્યા" };
        var wrong = new List<string>();
        foreach (var s in signs)
        {
            string u = "જ્ઞ્ન" + s, bytes = To7.ConvertText(u), back = From7.ConvertText(bytes);
            // જ્ઞ 6F and the halant B9 before the ન (or half ન), whatever sign it takes.
            if (!(" " + Hex(bytes) + " ").Contains(" 6F B9 ") || back != u)
                wrong.Add($"{u} -> {Hex(bytes)} -> {back} [{Cps(back)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // ---------- helpers ----------

    /// <summary>The Gujarati consonants ક-હ (U+0AA9 U+0AB1 U+0AB4 are not assigned).</summary>
    private static readonly string[] Consonants = Enumerable.Range(0x0A95, 0x0AB9 - 0x0A95 + 1)
        .Where(c => c is not (0x0AA9 or 0x0AB1 or 0x0AB4)).Select(c => ((char)c).ToString()).ToArray();

    private static List<Row> LoadTruth()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-guj7-truth.json");
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

    /// <summary>A 7 spelling with the ઙ conjuncts on their 4/5/6 codes.</summary>
    private static string With456Codes(string hex)
    {
        string s = " " + hex + " ";
        foreach (var (seven, code, _) in Nga) s = s.Replace(" " + seven + " ", " " + code + " ");
        return s.Trim();
    }

    /// <summary>
    /// True when the table reports the row (the output policy: a character the fonts cannot
    /// show - the vocalic-l signs and letters, which they draw nothing for, and ૰, both passed through (P3b) -
    /// is reported, never dropped silently).
    /// </summary>
    private static bool Reported(Row r) =>
        r.U.IndexOfAny(PassedThrough.ToCharArray()) >= 0
        || (Exceptions.Value.TryGetValue(r.U, out var why) && (ShreeFixture.LeavesOut(why) || ShreeFixture.PassesThrough(why)));

    // P3b: the vocalic-l letters and signs, which the fonts have no glyph for, are passed through as themselves.
    private const string PassedThrough = "ઌૡૢૣ";

    /// <summary>
    /// P3b: what a table wrote for <paramref name="u"/>, as hex, with the vocalic-l letters and signs it passes
    /// through taken out - the composer's spelling, which leaves them out - when it passed each one of them
    /// through exactly once; else a line that matches no spelling.
    /// </summary>
    private static string Composer(string legacy, string u)
    {
        int typed = u.Count(c => PassedThrough.IndexOf(c) >= 0), written = legacy.Count(c => PassedThrough.IndexOf(c) >= 0);
        if (typed != written) return $"(passed {written} of {typed} vocalic-l characters through: {Hex(legacy)})";
        return Hex(new string(legacy.Where(c => PassedThrough.IndexOf(c) < 0).ToArray()));
    }

    private static bool SameText(string got, string want, string bytes, FontConverter forward)
    {
        if (got == want) return true;
        if (got == new string(want.Where(c => NoGlyph.IndexOf(c) < 0).ToArray())) return true;
        var r = forward.Convert(got);
        if (!r.HasUnmapped && Hex(r.Text) == bytes) return true;
        // Text that ends in a virama, read from what the composer shows while it waits for the
        // next letter: the table writes the halant at the end of a word, but before a letter
        // these very codes.
        if (got.Length == 0 || got[^1] != '્') return false;
        var next = forward.Convert(got + "ક");
        return !next.HasUnmapped && Hex(next.Text) == bytes + " " + Hex(forward.ConvertText("ક"));
    }

    private static bool IsMark(char c) =>
        (c >= 'ઁ' && c <= 'ઃ') || c == '઼' || (c >= 'ા' && c <= '્') || c == 'ૢ' || c == 'ૣ';

    /// <summary>Truth strings that can stand next to each other in a word: they start on a
    /// base, do not end on a virama, and have no sign straight after a virama.</summary>
    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] != '‌' && r.U[0] != '‍' && r.U[^1] != '્' &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '્' && IsMark(r.U[i]) && r.U[i] != '્'))
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

    /// <summary>Legacy text as hex bytes; a character outside Windows-1252 shows as U+XXXX.</summary>
    private static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    /// <summary>Hex bytes as Windows-1252 text; a U+XXXX token is that character (passed through, P3b).</summary>
    private static string FromHex(string hex) =>
        string.Concat(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.StartsWith("U+") ? ((char)Convert.ToInt32(h[2..], 16)).ToString() : Cp1252.GetString(new[] { Convert.ToByte(h, 16) })));

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}
