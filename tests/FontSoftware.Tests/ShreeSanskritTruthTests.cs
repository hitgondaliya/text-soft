using System.Text;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The Shree-Lipi 7 Sanskrit tables (SHREE-SAN7-*) against what Shree's own Sanskrit
/// composers write. These fonts are NOT the Devanagari 7 encoding: the same text has
/// other bytes on almost every probe (क = 40 A1 here, 48 24 in SHREE-DEV7), and DEV7
/// bytes draw garbage in a SAN7 font. The fixture (Data/shree-san7-truth.json) holds
/// 25,468 Unicode strings with the bytes the Sanskrit 7 composer types for each and the
/// other spellings it also accepts; its "exceptions" say where the table writes another
/// spelling (ऐ: the composer's byte 00 cannot live in a document; ॒: the anudatta's own
/// byte 85, not इ; a virama that ends a word: the visible halant, not the half form the
/// composer shows while it waits for the next letter) or which rows are not checked (the
/// composer writes a code that draws something else). Nothing here needs Shree installed.
/// </summary>
public class ShreeSanskritTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    private static readonly Lazy<(List<Row> Rows, Dictionary<string, string> Exceptions)> Fixture = new(Load);
    private static List<Row> Truth => Fixture.Value.Rows;
    private static readonly Lazy<Dictionary<string, Row>> ByUnicode = new(() => Truth.ToDictionary(r => r.U, StringComparer.Ordinal));
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    // Composer spellings that do not draw their text: what the bytes really draw.
    private static readonly Dictionary<string, string> DrawnAs = new() { ["40 40 A1 A7"] = "ककॢ", ["54 54 22 A7"] = "पपॢ" };

    private static FontConverter To => Assets.Value.CreateConverter("mangal", "shree-san7")!;
    private static FontConverter From => Assets.Value.CreateConverter("shree-san7", "mangal")!;

    /// <summary>The rows the table is checked against, with the bytes it is to write.</summary>
    private static IEnumerable<Row> Checked => Truth.Where(r => !Excluded(r)).Select(r => r with { Pref = Target(r) });

    private static bool Excluded(Row r) => Fixture.Value.Exceptions.TryGetValue(r.U, out var why) && why.StartsWith("not checked");

    /// <summary>The composer's preferred bytes - or what the fixture's exception says the table writes.</summary>
    private static string Target(Row r) =>
        Fixture.Value.Exceptions.TryGetValue(r.U, out var why) && !why.StartsWith("not checked") ? ShreeFixture.TableWrites(why) : r.Pref;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(25468, Truth.Count);
        // 930 word-final viramas, the 6 spellings of ऐ, the anudatta, and ” (not checked).
        Assert.Equal(938, Fixture.Value.Exceptions.Count);
        Assert.Equal(930, Fixture.Value.Exceptions.Count(e => ShreeFixture.IsWordFinal(e.Value)));
        // The table writes no NUL for ऐ; only ऐ ऐं ऐँ र्ऐ have an accepted spelling without it.
        foreach (var u in new[] { "ऐ", "ऐं", "ऐँ", "र्ऐ" })
            Assert.Contains(ShreeFixture.TableWrites(Fixture.Value.Exceptions[u]), ByUnicode.Value[u].Accepted);
        foreach (var u in new[] { "ऐः", "र्ऐं" })
        {
            Assert.DoesNotContain(ShreeFixture.TableWrites(Fixture.Value.Exceptions[u]), ByUnicode.Value[u].Accepted);
            Assert.DoesNotContain("also accepts", Fixture.Value.Exceptions[u]);
        }
        Assert.NotNull(Assets.Value.CreateConverter("mangal", "shree-san7"));
        Assert.NotNull(Assets.Value.CreateConverter("shree-san7", "mangal"));
        Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding("shree-san7"));
    }

    // (a) Every truth string alone comes out as the spelling the composer prefers.
    [Fact]
    public void TableWritesTheComposerSpelling()
    {
        var conv = To;
        var wrong = new List<string>();
        foreach (var r in Checked)
        {
            var c = conv.Convert(r.U);
            string got = Hex(c.Text);
            if (got != r.Pref || c.HasUnmapped) wrong.Add($"{r.U} [{Cps(r.U)}]: want {r.Pref}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (b) Strings joined into words: no pair or rule may reach across a syllable.
    [Fact]
    public void JoinedSyllablesConvertExactly()
    {
        var conv = To;
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Checked, 20000, 2026))
        {
            string want = string.Join(" ", parts.Select(p => p.Pref).Where(p => p.Length > 0));
            string got = Hex(conv.ConvertText(u));
            if (got != want) wrong.Add($"{u} [{Cps(u)}]: want {want}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every spelling the table writes reads back to its Unicode, or to text the composer writes the same way.
    [Fact]
    public void SpellingsReadBack()
    {
        var to = To;
        var from = From;
        var wrong = new List<string>();
        foreach (var r in Checked.Where(r => r.Pref.Length > 0))
        {
            string got = from.ConvertText(FromHex(r.Pref));
            if (!SameText(got, r.U, r.Pref, to)) wrong.Add($"{r.Pref}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
        }
        // And what the composer shows for a word-final row while it waits for the next letter.
        foreach (var r in Truth.Where(r => r.Pref.Length > 0 && Fixture.Value.Exceptions.TryGetValue(r.U, out var why) && ShreeFixture.IsWordFinal(why)))
        {
            string got = from.ConvertText(FromHex(r.Pref));
            if (!SameText(got, r.U, r.Pref, to)) wrong.Add($"{r.Pref} (waiting): want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    [Fact]
    public void JoinedSpellingsReadBack()
    {
        var to = To;
        var from = From;
        var alone = new Dictionary<string, string>(StringComparer.Ordinal);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Checked, 20000, 7))
        {
            string bytes = string.Join(" ", parts.Select(p => p.Pref).Where(p => p.Length > 0));
            string got = from.ConvertText(FromHex(bytes));
            string want = string.Concat(parts.Select(p => alone.TryGetValue(p.Pref, out var a) ? a : alone[p.Pref] = from.ConvertText(FromHex(p.Pref))));
            if (got != want && !SameText(got, u, bytes, to)) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (d) The composer's byte 00 never reaches a document, and DEV7 bytes are not SAN7 bytes.
    [Fact]
    public void NeverWritesNul()
    {
        var table = Assets.Value.FindMapping("mangal", "shree-san7")!;
        Assert.DoesNotContain(table.Pairs, p => p.To.Contains('\0'));
        var conv = To;
        foreach (string u in Truth.Select(r => r.U).Concat(Words(Checked, 5000, 99).Select(w => w.U)))
            Assert.False(conv.ConvertText(u).Contains('\0'), $"byte 00 written for {u}");
        Assert.NotEqual(Hex(Assets.Value.CreateConverter("mangal", "shree-dev7")!.ConvertText("क")), Hex(conv.ConvertText("क")));
    }

    // (e) Spellings a Sanskrit typist knows, as Shree's Sanskrit composer writes them.
    [Theory]
    [InlineData("क्र", "40 F8 A1")]                 // ra-sign: after the letter, before its spacer A1
    [InlineData("ट्र", "4A 7E 3E")]                 // ra-sign under a round-bottomed letter: 7E
    [InlineData("कर्म", "40 A1 58 22 EA")]          // reph: written after the letter it sits on
    [InlineData("र्कें", "40 75 7C A1")]            // reph + anusvara: one glyph 7C, before the spacer
    [InlineData("कि", "7B 40 A1")]                  // i-sign: first, the short 7B over one letter
    [InlineData("क्खि", "5B D7 41 22")]             // i-sign over a conjunct: the long 5B
    [InlineData("स्थिर", "5B 5F 50 22 5A")]         // the half स् is the body of स without its stem 22
    [InlineData("क्ष", "62 22")]                    // conjunct: one glyph and its stem
    [InlineData("स्त्र", "AE")]                     // conjunct of three letters: one glyph
    [InlineData("क्न", "40 6E A1")]                 // the na-sign 6E under क
    [InlineData("क्ख", "D7 41 22")]                 // half form: क् before a letter is the half क D7
    [InlineData("ङ्क", "8A A1")]                    // ङ्क: a ligature of its own
    [InlineData("ट्या", "4A F7 70")]                // ya-sign after ट: in place of the spacer 3E
    [InlineData("ऐ", "EF 75")]                      // ऐ: ए + e-sign (the composer's own 00 75 cannot live in a document)
    [InlineData("धर्मक्षेत्रे", "52 22 58 22 EA 62 22 75 65 22 75")]
    public void NamedSpellings(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To.ConvertText(unicode)));
        Assert.Equal(unicode, From.ConvertText(FromHex(bytes)));
    }

    // (f) A virama that ends a word - before a space, punctuation, the danda, a digit, or at
    // the end of the text - is the visible halant, as every Unicode font draws it (the letter
    // as the composer writes it with ZWNJ: म्‌ = 58 22 6F, क्‌ = 40 6F A1). The table used to
    // write the half form the composer shows while it waits for the next letter (संस्कृतम्
    // ended in the half म 58), which drawn alone is a broken letter. It reads back as the
    // plain virama it was typed with - the table used to add a ZWNJ; one stays only where a
    // letter follows, where it is what asks for the visible halant.
    [Theory]
    [InlineData("संस्कृतम्", "5F 22 7A 5F 40 77 A1 4F 22 58 22 6F")]    // was ... 4F 22 58
    [InlineData("वाक्", "5C 22 70 40 6F A1")]                             // was 5C 22 70 D7
    [InlineData("अर्थात्", "EC 50 22 70 EA 4F 22 6F")]                    // was ... EA 4F
    [InlineData("अहम् इदम्", "EC 60 58 22 6F 20 F2 51 58 22 6F")]         // before a space and at the end
    [InlineData("म्।", "58 22 6F 24")]                                    // before the danda
    [InlineData("अर्थात्, इति", "EC 50 22 70 EA 4F 22 6F 2C 20 F2 7B 4F 22")] // before punctuation
    [InlineData("जगत्१", "47 22 42 70 4F 22 6F 31")]                      // before a digit
    [InlineData("भगवान्", "57 22 42 70 5C 22 70 53 22 6F")]
    [InlineData("स्त्", "5F 4F 22 6F")]                                   // a cluster: the half स् as before त, then त्‌
    [InlineData("क्ष्", "62 22 6F")]                                      // a ligature keeps it, the halant after (the composer's क्ष्‌)
    [InlineData("र्", "5A 6F")]                                           // not a reph with nothing under it: र with the halant
    public void AViramaThatEndsAWordIsTheHalant(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To.ConvertText(unicode)));
        Assert.Equal(unicode, From.ConvertText(FromHex(bytes)));
    }

    // Inside a word the virama still joins the next letter, as the composer writes it.
    [Theory]
    [InlineData("त्र", "65 22")]
    [InlineData("क्ष", "62 22")]
    [InlineData("स्त", "5F 4F 22")]
    [InlineData("स्तुति", "5F 4F 22 73 7B 4F 22")]
    [InlineData("क्‍", "D7")]                                        // an explicit half form (ZWJ) stays the half form
    [InlineData("क्‌", "40 6F A1")]                                  // and an explicit halant (ZWNJ) the halant
    public void InsideAWordTheViramaStillJoins(string unicode, string bytes) =>
        Assert.Equal(bytes, Hex(To.ConvertText(unicode)));

    // The anudatta (U+0952) is byte 85 in the SAN7 fonts, a zero-advance bar under the letter
    // before it. The table used to drop it, and byte 85 read back as the ellipsis U+2026 (what
    // Windows-1252 puts there).
    [Theory]
    [InlineData("॒", "85")]
    [InlineData("क॒", "40 A1 85")]
    [InlineData("अ॒ग्निमी॒ळे", "EC 85 7B D0 70 58 22 72 85 61 75")]  // was EC 7B D0 70 58 22 72 61 75: both dropped
    public void TheAnudattaIsByte85(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To.ConvertText(unicode)));
        Assert.Equal(unicode, From.ConvertText(FromHex(bytes)));
        Assert.False(To.Convert(unicode).HasUnmapped);
    }

    // ---------- helpers ----------

    private static (List<Row>, Dictionary<string, string>) Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-san7-truth.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var rows = new List<Row>();
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            string u = row[0].GetString()!, pref = row[1].GetString()!;
            var acc = new List<string> { pref };
            if (row.GetArrayLength() > 2) acc.AddRange(row[2].EnumerateArray().Select(a => a.GetString()!));
            rows.Add(new Row(u, pref, acc.ToArray()));
        }
        var exceptions = doc.RootElement.GetProperty("exceptions").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        return (rows, exceptions);
    }

    /// <summary>
    /// The same text: equal; what the bytes draw where the composer's spelling draws
    /// something else; text the composer also writes with these bytes; or well-formed
    /// text the table writes so.
    /// </summary>
    private static bool SameText(string got, string want, string bytes, FontConverter forward)
    {
        if (got == want || Nukta.Spell(got) == Nukta.Spell(want)) return true;
        if (DrawnAs.TryGetValue(bytes, out var d) && got == d) return true;
        if (ByUnicode.Value.TryGetValue(got, out var r) && r.Accepted.Contains(bytes)) return true;
        if (!WellFormed(got)) return false;
        var c = forward.Convert(got);
        if (!c.HasUnmapped && Hex(c.Text) == bytes) return true;
        // Text that ends in a virama, read from what the composer shows while it waits for the
        // next letter: the table writes the halant at the end of a word, but before a letter
        // these very codes.
        if (got.Length == 0 || got[^1] != '्') return false;
        var next = forward.Convert(got + "क");
        return !next.HasUnmapped && Hex(next.Text) == bytes + " " + Hex(forward.ConvertText("क"));
    }

    private static bool IsMark(char c) =>
        (c >= 'ऀ' && c <= 'ः') || (c >= 'ऺ' && c <= 'ॏ' && c != 'ऽ') || (c >= '॑' && c <= 'ॗ') || c == 'ॢ' || c == 'ॣ';

    /// <summary>No sign without a letter, no sign straight after a virama, no two vowel signs in a row.</summary>
    private static bool WellFormed(string u)
    {
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i];
            if (!IsMark(c) || c == '्' || c == '़') continue;
            if (i == 0 || u[i - 1] == '्') return false;
            char p = u[i - 1];
            bool matra = c >= 'ा' && c <= 'ौ', pMatra = p >= 'ा' && p <= 'ौ';
            if (matra && (pMatra || p is 'ँ' or 'ं' or 'ः')) return false;
        }
        return true;
    }

    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] != '‌' && r.U[0] != '‍' && r.U[^1] != '्' && r.U[^1] != '‌' && r.U[^1] != '‍' &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '्' && IsMark(r.U[i]) && r.U[i] != '्'))
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

    private static string FromHex(string hex) =>
        hex.Length == 0 ? "" : Cp1252.GetString(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h => Convert.ToByte(h, 16)).ToArray());

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}
