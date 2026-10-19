using System.Text;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The Shree-Lipi Devanagari tables against what Shree's own composers write. The
/// fixture (Data/shree-dev7-truth.json) holds 27,175 Unicode strings - every letter
/// with every sign, every two-letter and many three-letter conjuncts, reph, ra- and
/// ya-signs, nukta letters, joiners - with the bytes the Shree-Lipi 7 composer types
/// for each and the other spellings it also accepts. Nothing here needs Shree installed.
///
/// The 7 fonts (SHREE-DEV7-*) and the 4/5/6 fonts (SHREE-DEV-*) share every code but
/// four: 91 92 B3 B6 are ङ्क ङ्ख, a subscript ्न and ङ्म in 4/5/6, and copies of म फ क्
/// य in 7; 4/5/6 also draws ligatures at U+0200-0218. The 7 composer writes none of
/// those, so the two families get the same Unicode -> legacy table and their own reverse.
///
/// A row that ends in a bare virama holds what the composer shows while it waits for the
/// next letter - a half form, which drawn alone is a broken letter. At the end of a word
/// the tables write the visible halant instead, as every Unicode font draws it; the
/// fixture's "exceptions" say what they write for each such row ("the table writes ...").
/// </summary>
public class ShreeDevanagariTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    private static readonly Lazy<List<Row>> Truth = new(() => LoadTruth("shree-dev7-truth.json"));
    private static readonly Lazy<Dictionary<string, string>> Exceptions = new(() => ShreeFixture.LoadExceptions("shree-dev7-truth.json"));

    /// <summary>The bytes the tables write for a row: the composer's, or the fixture's exception.</summary>
    private static string Target(Row r) => Exceptions.Value.TryGetValue(r.U, out var why) ? ShreeFixture.TableWrites(why) : r.Pref;
    private static readonly Lazy<Dictionary<string, Row>> ByUnicode = new(() => Truth.Value.ToDictionary(r => r.U, StringComparer.Ordinal));
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    private static readonly string[] FamilyCodes = { "91", "92", "B3", "B6" };

    /// <summary>
    /// True when the table reports the row: a character the fonts draw nothing for (ऌ ॡ ॢ ॣ ॑),
    /// or one the output policy leaves out (× ÷) - reported, never dropped silently.
    /// </summary>
    private static bool Reported(Row r) =>
        r.U.IndexOfAny("ऌॡॢॣ॑".ToCharArray()) >= 0 || (Exceptions.Value.TryGetValue(r.U, out var why) && ShreeFixture.LeavesOut(why));

    // Composer spellings that drop a mark: what the bytes really draw.
    private static readonly Dictionary<string, string> DrawnAs = new()
    {
        ["4C EC"] = "ङव्", ["52 EC"] = "ठव्", ["53 EC"] = "डव्", ["54 EC"] = "ढव्",
    };

    private static FontConverter To7 => Assets.Value.CreateConverter("mangal", "shree-dev7")!;
    private static FontConverter To456 => Assets.Value.CreateConverter("mangal", "shree-dev456")!;
    private static FontConverter From7 => Assets.Value.CreateConverter("shree-dev7", "mangal")!;
    private static FontConverter From456 => Assets.Value.CreateConverter("shree-dev456", "mangal")!;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(27175, Truth.Value.Count);
        // The rows that end in a bare virama and are drawn with the halant at the end of a word
        // (the letters with no half form - ङ छ ट ठ ड ढ द - the composer already writes so).
        // And (2026-10-18) the output policy's decisions (P4): – — × ÷, whose composer codes Word draws as nothing.
        Assert.Equal(911, Exceptions.Value.Count);
        Assert.Equal(907, Exceptions.Value.Count(e => ShreeFixture.IsWordFinal(e.Value)));
        Assert.All(Exceptions.Value, e =>
        {
            if (ShreeFixture.IsWordFinal(e.Value)) Assert.EndsWith("्", e.Key);
            else Assert.True(ShreeFixture.IsPolicy(e.Value), e.Value);
            Assert.Contains(Truth.Value, r => r.U == e.Key);
        });
        Assert.All(new[] { "shree-dev7", "shree-dev456" }, enc =>
        {
            Assert.NotNull(Assets.Value.CreateConverter("mangal", enc));
            Assert.NotNull(Assets.Value.CreateConverter(enc, "mangal"));
            // One byte per character: files in these fonts are Windows-1252.
            Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(enc));
        });
        // The 7 composer never writes a code the 4/5/6 fonts draw differently.
        Assert.DoesNotContain(Truth.Value, r => FamilyCodes.Any(c => (" " + r.Pref + " ").Contains(" " + c + " ")));
    }

    // (a) Every truth string alone comes out as the spelling the composer prefers.
    [Fact]
    public void SevenTableWritesTheComposerSpelling()
    {
        var conv = To7;
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Hex(c.Text), want = Target(r);
            if (got != want || c.HasUnmapped != Reported(r)) wrong.Add($"{r.U} [{Cps(r.U)}]: want {want}, got {got}{(c.HasUnmapped ? " (reported)" : "")}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    [Fact]
    public void FourFiveSixTableWritesTheSameSpelling()
    {
        var to7 = To7;
        var to456 = To456;
        var wrong = Truth.Value.Where(r => to456.ConvertText(r.U) != to7.ConvertText(r.U)).Select(r => r.U).ToList();
        Assert.True(wrong.Count == 0, $"{wrong.Count} differ:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (b) Strings joined into words: no pair or rule may reach across a syllable.
    [Fact]
    public void JoinedSyllablesConvertExactly()
    {
        var to7 = To7;
        var to456 = To456;
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, 2026))
        {
            string want = string.Join(" ", parts.Select(Target).Where(p => p.Length > 0));
            string got = Hex(to7.ConvertText(u));
            if (got != want) wrong.Add($"7: {u} [{Cps(u)}]: want {want}, got {got}");
            if (Hex(to456.ConvertText(u)) != want) wrong.Add($"4/5/6: {u} [{Cps(u)}]: want {want}, got {Hex(to456.ConvertText(u))}");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every preferred spelling reads back to its Unicode, or to text the composer writes the same way.
    [Fact]
    public void PreferredSpellingsReadBack()
    {
        var to7 = To7;
        foreach (var (from, name) in new[] { (From7, "7"), (From456, "4/5/6") })
        {
            var wrong = new List<string>();
            foreach (var r in Truth.Value.Where(r => r.Pref.Length > 0))
            {
                // The composer's spelling - for a word-final row, what it shows while it waits - and the table's.
                foreach (string bytes in new[] { r.Pref, Target(r) }.Distinct().Where(b => b.Length > 0))
                {
                    string got = from.ConvertText(FromHex(bytes));
                    if (!SameText(got, r.U, bytes, to7)) wrong.Add($"{bytes}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
                }
            }
            Assert.True(wrong.Count == 0, $"{name}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
        }
    }

    [Fact]
    public void JoinedSpellingsReadBack()
    {
        var to7 = To7;
        var from7 = From7;
        var alone = new Dictionary<string, string>(StringComparer.Ordinal);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Truth.Value, 20000, 7))
        {
            string bytes = string.Join(" ", parts.Select(p => p.Pref).Where(p => p.Length > 0));
            string got = from7.ConvertText(FromHex(bytes));
            // As its syllables read alone, or the same text.
            string want = string.Concat(parts.Select(p => alone.TryGetValue(p.Pref, out var a) ? a : alone[p.Pref] = from7.ConvertText(FromHex(p.Pref))));
            if (got != want && !SameText(got, u, bytes, to7)) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // In 7 the four codes are copies; in 4/5/6 they are ligatures of their own.
    [Theory]
    [InlineData("91", "म", "ङ्क")]
    [InlineData("92", "फ", "ङ्ख")]
    [InlineData("B3", "क्", "्न")]
    [InlineData("B6", "य", "ङ्म")]
    public void FamilyCodesReadAsTheirFamilyDrawsThem(string code, string seven, string fourFiveSix)
    {
        Assert.Equal(seven, From7.ConvertText(FromHex(code)));
        Assert.Equal(fourFiveSix, From456.ConvertText(FromHex(code)));
    }

    [Theory]
    [InlineData(0x200, "क्क")]
    [InlineData(0x203, "ङ्क")]
    [InlineData(0x208, "ङ्क्ष")]
    [InlineData(0x20E, "ल्ल")]
    [InlineData(0x218, "प्त")]
    public void FourFiveSixLigaturesRead(int code, string unicode) =>
        Assert.Equal(unicode, From456.ConvertText(((char)code).ToString()));

    // (d) Neither table writes a code that draws something else in the other family.
    [Fact]
    public void NoTableWritesACodeThatDrawsSomethingElse()
    {
        var family = FamilyCodes.Select(h => FromHex(h)[0]).ToArray();
        foreach (var enc in new[] { "shree-dev7", "shree-dev456" })
        {
            var table = Assets.Value.FindMapping("mangal", enc)!;
            Assert.DoesNotContain(table.Pairs, p => p.To.IndexOfAny(family) >= 0);
            Assert.DoesNotContain(table.PostRules, r => r.Replace.IndexOfAny(family) >= 0);
        }
        var to7 = To7;
        var to456 = To456;
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Truth.Value, 5000, 99).Select(w => w.U)))
        {
            Assert.True(to7.ConvertText(u).IndexOfAny(family) < 0, $"7 wrote a 4/5/6 code for {u}: {Hex(to7.ConvertText(u))}");
            Assert.True(to456.ConvertText(u).IndexOfAny(family) < 0, $"4/5/6 wrote a family code for {u}: {Hex(to456.ConvertText(u))}");
        }
    }

    // (e) Spellings a Hindi typist knows, as Shree's composer writes them.
    [Theory]
    [InlineData("क्र", "48 AB 24")]                // ra-sign: after the letter, before its spacer 24
    [InlineData("ट्र", "51 B4 3E")]                // ra-sign under a round-bottomed letter: B4
    [InlineData("कर्म", "48 24 5F A9")]            // reph: written after the letter it sits on
    [InlineData("र्कें", "48 5D 24")]              // reph + e + anusvara: one glyph 5D, before the spacer
    [InlineData("कि", "7B 48 24")]                 // i-sign: first, the short 7B over one letter
    [InlineData("स्थिर", "70 F1 57 61")]           // i-sign over a conjunct: the long 70 (and the half स् F1)
    [InlineData("हिंदी", "71 68 58 72")]           // i-sign + anusvara: one glyph 71
    [InlineData("क्ष", "6A")]                      // conjunct: one glyph
    [InlineData("स्त्र", "F3")]                    // conjunct of three letters: one glyph
    [InlineData("विद्यार्थी", "7B 64 DA 6D 57 75")] // द्य ligature, ii + reph glyph 75
    [InlineData("क्ख", "8A 49")]                   // half form: क् before a letter is the half क 8A
    [InlineData("ङ्क", "4C B2 3E 48 24")]          // no half form: the halant B2, before the spacer
    [InlineData("क्या", "8A 60 6D")]                // half क + य (not the ya-sign) for क्य
    [InlineData("ज़्य", "B5 4F E7")]               // nukta before its letter, then the ya-sign
    [InlineData("राष्ट्र", "61 6D EF B4 3E")]       // ष्ट + ra-sign B4
    public void NamedSpellings(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
        Assert.Equal(Nukta.Spell(unicode), Nukta.Spell(From7.ConvertText(FromHex(bytes))));
        Assert.Equal(Nukta.Spell(unicode), Nukta.Spell(From456.ConvertText(FromHex(bytes))));
    }

    // (f) A virama that ends a word - before a space, punctuation, the danda, a digit, or at
    // the end of the text - is the visible halant, as every Unicode font draws it (the
    // letter as the composer writes it with ZWNJ: त्‌ = 56 B2). The table used to write the
    // half form the composer shows while it waits for the next letter (अर्थात् ended in the
    // half त CB), which drawn alone is a broken letter. It reads back as the plain
    // virama it was typed with - the table used to add a ZWNJ (अर्थात्+ZWNJ); one stays only
    // where a letter follows, where it is what asks for the visible halant.
    [Theory]
    [InlineData("अर्थात्", "41 57 6D A9 56 B2")]                  // was 41 57 6D A9 CB
    [InlineData("संस्कृतम्", "67 A7 F1 48 A5 24 56 5F B2")]       // was ... 56 E5 (half म)
    [InlineData("वाक्", "64 6D 48 B2 24")]                        // was 64 6D 8A: the halant before the spacer, as क्‌
    [InlineData("अहम् इदम्", "41 68 5F B2 20 42 58 5F B2")]       // before a space and at the end
    [InlineData("म्।", "5F B2 26")]                               // before the danda
    [InlineData("म्॥", "5F B2 26 26")]
    [InlineData("अर्थात्, इति", "41 57 6D A9 56 B2 2C 20 42 7B 56")] // before punctuation
    [InlineData("सत्.", "67 56 B2 2E")]
    [InlineData("जगत्१", "4F 4A 56 B2 31")]                        // before a digit
    [InlineData("भगवान्", "5E 4A 64 6D 5A B2")]
    [InlineData("स्त्", "F1 56 B2")]                              // a cluster: the half स् as before त, then त्‌
    [InlineData("न्त्", "DD 56 B2")]
    [InlineData("क्ष्", "6A B2")]                                 // a ligature keeps it, the halant after (the composer's क्ष्‌)
    [InlineData("र्", "61 B2")]                                   // not a reph with nothing under it: र with the halant
    public void AViramaThatEndsAWordIsTheHalant(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));
    }

    // An explicit halant inside a word (्+ZWNJ before a letter) reads back with its ZWNJ.
    [Theory]
    [InlineData("त्‌क", "56 B2 48 24")]
    [InlineData("सत्‌कार", "67 56 B2 48 24 6D 61")]
    public void AHalantInsideAWordReadsBackWithItsZwnj(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(unicode, From7.ConvertText(FromHex(bytes)));
        Assert.Equal(unicode, From456.ConvertText(FromHex(bytes)));
    }

    // Inside a word the virama still joins the next letter, as the composer writes it.
    [Theory]
    [InlineData("त्र", "CC")]
    [InlineData("क्ष", "6A")]
    [InlineData("स्त", "F1 56")]
    [InlineData("स्तुति", "F1 56 77 7B 56")]
    [InlineData("कर्म", "48 24 5F A9")]                           // the reph
    [InlineData("क्‍", "8A")]                                // an explicit half form (ZWJ) stays the half form
    [InlineData("क्‌", "48 B2 24")]                          // and an explicit halant (ZWNJ) the halant
    public void InsideAWordTheViramaStillJoins(string unicode, string bytes)
    {
        Assert.Equal(bytes, Hex(To7.ConvertText(unicode)));
        Assert.Equal(bytes, Hex(To456.ConvertText(unicode)));
    }

    // ---------- helpers ----------

    private static List<Row> LoadTruth(string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", file);
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

    /// <summary>
    /// The same text: equal (nukta letters composed or not); what the bytes draw where
    /// the composer dropped a mark (a joiner more or less draws the same there); text the
    /// composer also writes with these bytes; or well-formed text the table writes so.
    /// </summary>
    private static bool SameText(string got, string want, string bytes, FontConverter forward)
    {
        if (got == want || Nukta.Spell(got) == Nukta.Spell(want)) return true;
        string drawn = DrawnAs.TryGetValue(bytes, out var d) ? d
            : bytes.Split(' ').Any(h => h is "B5" or "8B" or "98" or "99") ? want : Nukta.Spell(want).Replace("़", "");
        if (drawn != want && NoJoiners(Nukta.Spell(got)) == NoJoiners(Nukta.Spell(drawn))) return true;
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

    private static string NoJoiners(string s) => new(s.Where(c => c != '‌' && c != '‍').ToArray());

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

    /// <summary>Truth strings that can stand next to each other in a word: they start on a
    /// base, do not end on a virama or joiner, and have no sign straight after a virama.</summary>
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

    private static string FromHex(string hex) =>
        hex.Length == 0 ? "" : Cp1252.GetString(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h => Convert.ToByte(h, 16)).ToArray());

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}
