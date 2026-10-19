using System.Text;
using System.Text.Json;
using System.Windows.Media;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The Shree-Lipi Bangla / Assamese tables against what Shree's own composers write. The
/// fixture (Data/shree-ban7-truth.json) holds 25,741 Unicode strings - every letter with
/// every sign, every two-letter cluster and the three-letter ones a key can hold, reph,
/// ra- and ya-phala, Assamese ৰ ৱ - with the bytes Shree-Lipi's composer (S8Ban764) types
/// for each and the other spellings it also accepts. Nothing here needs Shree installed.
///
/// The composer writes the codes of the SHREE-BAN 4/5/6 fonts, and the fixture keeps them
/// (with ব ৰ ৱ moved to the codes that draw them, and - tools/BanTruth/Decisions.cs - the
/// spelling that draws the text where the composer's does not: Assamese ৰ as reph and
/// ra-phala, the filler of দ্গ ধ্ব ..., a lone র্, ব্ঢ ব্ড চ্ঞ spelled with a visible hasanta
/// instead of the C1 codes 81 8D 90; the composer's own ক্ৰ্র = 41 F4 A1 B9 F8 is kept). The
/// other families draw a few codes differently, so each has its own pair of tables and moves:
///   7 (SHREE-BAN7-*, Shree-Ban7-001/002, SHREE-ASS7-*): ঃ and the half-ন hook swap 40/96,
///     subscript ্ন and ৺ swap 97/A5, FF is the ligature ড়্গ and A8 a joining দ (দ্গ A8 4B),
///     no ধ্ম 8C or ব্ঝ A8 glyph, no % 25 or : 3A.
///   Assamese (SHREE-ASS-*, SHREE-ASS7-*, Shree-Ass7-001/002): ৱ is AF; ra reads ৰ.
///   SHREE-BAN-* (4/5/6): 3A is a mark below the letter, so : has no glyph.
/// Codes are written as the fonts' cmaps hold them (Windows-1252). No code point U+0080-U+009F
/// is ever written: Word and RichEdit draw nothing for a C1 code, whatever the cmap holds. So
/// the Bangla fonts' ৱ (9E, held at U+009E) is not written: ৱ passes through as U+09F1,
/// reported, and Word's font fallback draws it; 9E (U+009E, or U+017E from a .txt) reads ৱ.
/// </summary>
public class ShreeBanglaTruthTests
{
    private sealed record Row(string U, string Pref, string[] Accepted);

    /// <summary>
    /// A font family: the codes it moves (a 4/5/6 code to its own, "" = none), the runs of codes
    /// it has a conjunct for, the codes it must never be written, and whether it is Assamese.
    /// </summary>
    private sealed record Family(string Id, Dictionary<string, string> Moves, (string From, string To)[] Seq, string[] Never, bool Assamese)
    {
        public string Apply(string hex)
        {
            if (hex.Length == 0) return hex;
            string s = string.Join(" ", hex.Split(' ').Select(t => Moves.TryGetValue(t, out var m) ? m : t).Where(t => t.Length > 0));
            foreach (var (from, to) in Seq)
                s = string.Join(" ", (" " + s + " ").Replace(" " + from + " ", " " + to + " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return s;
        }

        public char Ra => Assamese ? 'ৰ' : 'র';
        public bool Seven => Moves.ContainsKey("8C");
    }

    private static readonly string[] Controls = "08 09 0A 0B 0C 0D 0E 0F 1A 1B 1C 1D 1E 1F".Split(' ');

    private static Dictionary<string, string> Moves(params (string From, string To)[] m) => m.ToDictionary(x => x.From, x => x.To);

    private static readonly (string, string)[] Seven =
    {
        ("40", "96"), ("96", "40"), ("97", "A5"), ("A5", "97"), ("FF", ""), ("8C", "8B F4 B3"), ("A8", "A4 F4 63"),
    };
    private static readonly (string, string)[] SevenGlyphs = { ("FF", ""), ("8C", "8B F4 B3"), ("A8", "A4 F4 63") };
    private static readonly (string, string)[] Assam = { ("9E", "AF") };
    /// <summary>The Bangla fonts hold ৱ only at U+009E, a C1 code nothing draws: ৱ passes through as U+09F1.</summary>
    private static readonly (string, string)[] BanglaWa = { ("9E", "U+09F1") };
    /// <summary>The C1 control codes (and 9E, ৱ at U+009E): Word and RichEdit draw nothing there.</summary>
    private static readonly string[] C1 = "81 8D 8F 90 9D 9E".Split(' ');
    /// <summary>The 7 fonts' ড়্গ FF and joining দ A8, where the 4/5/6 fonts take a visible hasanta.</summary>
    private static readonly (string, string)[] SevenConjuncts = { ("D8 6C F4 A1 4B", "FF"), ("83 F4 4B", "A8 4B"), ("83 F4 51", "A8 51") };

    private static readonly Family[] Families =
    {
        new("shree-ban7", Moves(Seven.Concat(new[] { ("25", ""), ("3A", "") }).Concat(BanglaWa).ToArray()), SevenConjuncts,
            "8C 25 3A".Split(' ').Concat(C1).Concat(Controls).ToArray(), false),
        new("shree-ban456", Moves(new[] { ("3A", "") }.Concat(BanglaWa).ToArray()), Array.Empty<(string, string)>(),
            new[] { "3A" }.Concat(C1).Concat(Controls).ToArray(), false),
        new("shree-ass7", Moves(Seven.Concat(Assam).Concat(new[] { ("25", ""), ("3A", "") }).ToArray()), SevenConjuncts,
            "8C 25 3A".Split(' ').Concat(C1).Concat(Controls).ToArray(), true),
        new("shree-ass456", Moves(Assam), Array.Empty<(string, string)>(), C1.Concat(Controls).ToArray(), true),
        new("shree-ass7w", Moves(SevenGlyphs.Concat(Assam).ToArray()), SevenConjuncts, new[] { "8C" }.Concat(C1).Concat(Controls).ToArray(), true),
    };

    public static IEnumerable<object[]> FamilyIds => Families.Select(f => new object[] { f.Id });

    private static Family Fam(string id) => Families.Single(f => f.Id == id);

    private static readonly Lazy<List<Row>> Truth = new(LoadTruth);
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Encoding Cp1252 = GetCp1252();

    private static readonly string Zwnj = ((char)0x200C).ToString();
    private const char Wa = (char)0x9E;          // ৱ in the Bangla fonts' cmaps
    private const char Zcaron = (char)0x17E;     // Windows-1252's 9E: in no face's cmap
    private const char Keep = (char)0xFDD1;      // the tables' "leave this spelling alone" mark, never text

    private static FontConverter To(string enc) => Assets.Value.CreateConverter("vrinda", enc)!;
    private static FontConverter From(string enc) => Assets.Value.CreateConverter(enc, "vrinda")!;

    [Fact]
    public void FixtureIsThere()
    {
        Assert.Equal(25741, Truth.Value.Count);
        Assert.All(Families, f =>
        {
            Assert.NotNull(Assets.Value.CreateConverter("vrinda", f.Id));
            Assert.NotNull(Assets.Value.CreateConverter(f.Id, "vrinda"));
            // One byte per character: files in these fonts are Windows-1252.
            Assert.True(new ConversionPipeline(Assets.Value).IsByteEncoding(f.Id));
        });
    }

    // (a) Every truth string alone comes out as the composer's spelling, in the family's codes.
    // A character the family has no code for (an empty spelling: % or : in some) is left out where its
    // code draws something else and passes through where the fonts have no such code - reported either way.
    // In a Bangla family ৱ passes through as U+09F1, reported; nothing else may be.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void TableWritesTheComposerSpelling(string id)
    {
        var fam = Fam(id);
        var conv = To(id);
        var wrong = new List<string>();
        int preferred = 0;
        foreach (var r in Truth.Value)
        {
            var c = conv.Convert(r.U);
            string got = Hex(c.Text);
            if (fam.Apply(r.Pref).Length == 0)
            {
                if ((c.Text == r.U || c.Text.Length == 0) && c.UnmappedCharacters.Contains(r.U)) preferred++;
                else wrong.Add($"{r.U}: the family has no glyph, want it left out or passed through, and reported, got {got}");
                continue;
            }
            bool reportedWa = !fam.Assamese && c.UnmappedCharacters.All(u => u == "ৱ") && r.U.Contains('ৱ');
            if (got == fam.Apply(r.Pref)) preferred++;
            if (!r.Accepted.Select(fam.Apply).Contains(got) || (c.HasUnmapped && !reportedWa)) wrong.Add($"{r.U} [{Cps(r.U)}]: want {fam.Apply(r.Pref)}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.Equal(Truth.Value.Count, preferred);
    }

    // (b) Strings joined into words: no pair or rule may reach across a syllable.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void JoinedSyllablesConvertExactly(string id)
    {
        var fam = Fam(id);
        var conv = To(id);
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(Drawn(fam), 20000, 2026))
        {
            string want = string.Join(" ", parts.Select(p => fam.Apply(p.Pref)).Where(p => p.Length > 0));
            string got = Hex(conv.ConvertText(u));
            if (got != want) wrong.Add($"{u} [{Cps(u)}]: want {want}, got {got}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c) Every preferred spelling reads back to its Unicode, or to text the composer writes the same way.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void PreferredSpellingsReadBack(string id)
    {
        var fam = Fam(id);
        var to = To(id);
        var from = From(id);
        var wrong = new List<string>();
        foreach (var r in Truth.Value)
        {
            string bytes = fam.Apply(r.Pref);
            if (bytes.Length == 0) continue;
            string got = from.ConvertText(FromHex(bytes));
            if (!SameText(got, r.U, bytes, to)) wrong.Add($"{bytes}: want {r.U} [{Cps(r.U)}], got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void JoinedSpellingsReadBack(string id)
    {
        var fam = Fam(id);
        var to = To(id);
        var from = From(id);
        // Syllables with a character the font cannot draw are left out (their bytes are the same
        // as without it), and so are those with a joiner: ক্‌ + খ is written as ক্খ is.
        var visible = Drawn(fam).Where(r => r.U.All(c => to.ConvertText(c.ToString()).Length > 0)).ToList();
        var wrong = new List<string>();
        foreach (var (u, parts) in Words(visible, 20000, 7))
        {
            string bytes = string.Join(" ", parts.Select(p => fam.Apply(p.Pref)));
            string got = from.ConvertText(FromHex(bytes));
            if (!SameText(got, u, bytes, to)) wrong.Add($"{bytes}: want {u}, got {got} [{Cps(got)}]");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (c2) "The same bytes" is no proof of text: রি্‌ক and র্‌কি are written alike. Every read back of
    // well-formed text is well formed - no sign or hasanta off its letter, no Latin leaking through.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void EveryReadBackIsWellFormedText(string id)
    {
        var fam = Fam(id);
        var from = From(id);
        var wrong = new List<string>();
        var items = Drawn(fam).Select(r => (r.U, fam.Apply(r.Pref)))
            .Concat(Words(Drawn(fam), 20000, 11).Select(w => (w.U, string.Join(" ", w.Parts.Select(p => fam.Apply(p.Pref))))));
        foreach (var (u, bytes) in items)
        {
            if (Malformed(u).Count > 0) continue;
            string got = from.ConvertText(FromHex(bytes));
            var bad = Malformed(got);
            if (bad.Count > 0) wrong.Add($"{bytes}: read {got} [{Cps(got)}] for {u}: {string.Join("; ", bad)}");
        }
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} malformed:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (d) No table writes a code that its family draws as something else (or not at all), nor U+017E.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void NoTableWritesACodeThatDrawsSomethingElse(string id)
    {
        var never = Fam(id).Never.Select(h => FromHex(h)[0]).Append(Zcaron).ToArray();
        var table = Assets.Value.FindMapping("vrinda", id)!;
        Assert.DoesNotContain(table.Pairs, p => p.To.IndexOfAny(never) >= 0);
        Assert.DoesNotContain(table.PostRules, r => r.Replace.IndexOfAny(never) >= 0);

        var to = To(id);
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Drawn(Fam(id)), 5000, 99).Select(w => w.U)))
        {
            var c = to.Convert(u);
            string legacy = new(c.Text.Where(ch => !c.UnmappedCharacters.Contains(ch.ToString())).ToArray());
            Assert.True(legacy.IndexOfAny(never) < 0, $"{id} wrote a code it draws as something else for {u} [{Cps(u)}]: {Hex(c.Text)}");
        }
    }

    // (d2) Every code point a table writes is in the cmap of every face of its family (Data/shree-ban-cmaps.json,
    // read from the installed fonts): Word and the app's preview draw a code point through the cmap and
    // nothing else - a byte the font holds at U+009E, written U+017E, is ž in Word and nothing in the preview.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void EveryCodePointWrittenIsInTheFontsCmap(string id)
    {
        var cmap = Cmaps.Value[id].Codes;
        var to = To(id);
        var table = Assets.Value.FindMapping("vrinda", id)!;
        var written = new HashSet<char>();
        void Add(string s) { foreach (char c in s) if (c != Keep) written.Add(c); }
        foreach (var p in table.Pairs) Add(p.To);
        foreach (var r in table.PostRules) Add(System.Text.RegularExpressions.Regex.Replace(r.Replace, @"\$\d|\$\$", ""));
        foreach (string u in Truth.Value.Select(r => r.U).Concat(Words(Drawn(Fam(id)), 5000, 99).Select(w => w.U)))
        {
            var c = to.Convert(u);
            Add(new string(c.Text.Where(ch => !c.UnmappedCharacters.Contains(ch.ToString())).ToArray()));
        }
        var missing = written.Where(c => !cmap.Contains(c)).Select(c => $"U+{(int)c:X4}").ToList();
        Assert.True(missing.Count == 0, $"{id} writes code points no face of its family has: {string.Join(" ", missing)}");
        // ...and none is a C1 code (Word and RichEdit draw nothing there): not ৱ's U+009E either.
        Assert.DoesNotContain(written, c => c >= (char)0x80 && c <= (char)0x9F);
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
                if (!Fam(id).Assamese) Assert.Contains(Wa, keys);
                Assert.DoesNotContain(Zcaron, keys);
            }
    }

    // The codes the families draw differently read as each family draws them.
    [Theory]
    [InlineData("shree-ban7", "41 A1 96", "কঃ")]
    [InlineData("shree-ban456", "41 A1 40", "কঃ")]
    [InlineData("shree-ass7w", "41 A1 40", "কঃ")]
    [InlineData("shree-ban7", "40 83", "ন্দ")]
    [InlineData("shree-ban456", "96 83", "ন্দ")]
    [InlineData("shree-ban7", "41 A5 A1", "ক্ন")]
    [InlineData("shree-ban456", "41 97 A1", "ক্ন")]
    [InlineData("shree-ban7", "97", "৺")]
    [InlineData("shree-ban456", "A5", "৺")]
    [InlineData("shree-ban456", "8C", "ধ্ম")]
    [InlineData("shree-ban7", "8C", "ব")]           // a ব in the 7 fonts
    [InlineData("shree-ban7", "8B 8C", "ধ্ব")]      // how the earlier table wrote ধ্ব there
    [InlineData("shree-ban7", "A4 8C", "ব্ব")]
    [InlineData("shree-ban7", "A8 4B", "দ্গ")]      // the 7 fonts' joining দ
    [InlineData("shree-ban456", "83 FF 4B", "দ্গ")] // the composer's filler: drawn দগ, meant দ্গ
    [InlineData("shree-ban456", "8B FF A4", "ধ্ব")]
    [InlineData("shree-ban456", "2D FF 2D", "–")]   // between hyphens the filler is a dash
    [InlineData("shree-ban7", "FF", "ড়্গ")]         // the ligature at FF in the 7 fonts, not ÿ
    [InlineData("shree-ass7", "FF", "ড়্গ")]
    [InlineData("shree-ass7w", "FF E5", "ড়্গু")]
    [InlineData("shree-ban456", "9E", "ৱ")]
    [InlineData("shree-ass456", "AF", "ৱ")]
    [InlineData("shree-ban456", "AF", "ব্ল")]
    [InlineData("shree-ass456", "B9", "ৰ")]
    [InlineData("shree-ban456", "B9", "র")]
    // P5: every spelling Shree's composer or an earlier table wrote reads as its text
    [InlineData("shree-ass456", "9E", "ৱ")]               // 9E is ৱ in the Bangla fonts; a document may carry it
    [InlineData("shree-ban7", "U+017E E0", "ৱা")]         // 9E as a .txt decodes it
    [InlineData("shree-ass7", "U+017E", "ৱ")]
    [InlineData("shree-ban456", "U+09F1 E0", "ৱা")]       // what the Bangla tables write now
    [InlineData("shree-ban7", "81", "ব্ঢ")]               // the composer's C1 conjuncts
    [InlineData("shree-ban456", "8D A1", "ব্ড")]
    [InlineData("shree-ass7", "90 A1 E0", "চ্ঞা")]
    [InlineData("shree-ass456", "5B 81", "ব্ঢি")]
    [InlineData("shree-ban7", "41 F4 A1 B9 F8", "ক্র্র")] // the composer's ra + ra-phala (was ক্রø)
    [InlineData("shree-ban456", "B9 F8", "র্র")]          // (was রø)
    [InlineData("shree-ban456", "B9 E7 A1 F8", "র্রু")]   // (was রুø)
    [InlineData("shree-ass456", "41 F4 A1 B9 F8", "ক্ৰ্ৰ")]
    [InlineData("shree-ass7w", "B9 F8", "ৰ্ৰ")]
    [InlineData("shree-ass7", "B9 EA A1 F8", "ৰ্ৰূ")]
    // ি + ঁ (7B) in front of any cluster, and a half letter under a reph: never '{' or 'V' (the composer's spellings)
    [InlineData("shree-ban7", "7B 9E", "ৱিঁ")]
    [InlineData("shree-ban456", "7B 9E A2", "র্ৱিঁ")]
    [InlineData("shree-ass456", "7B AF", "ৱিঁ")]
    [InlineData("shree-ban7", "7B DB A2 A1", "র্ক্ষিঁ")]
    [InlineData("shree-ban456", "7B D8 5C A2", "র্জ়িঁ")]
    [InlineData("shree-ban7", "56 DB A2 A1", "র্ঙ্ক্ষ")]
    [InlineData("shree-ass7", "56 DB A2 A1", "ৰ্ঙ্ক্ষ")]
    [InlineData("shree-ban456", "7B D1 7A", "স্তিঁ")]
    [InlineData("shree-ban7", "7B 94 7A", "ন্তিঁ")]
    [InlineData("shree-ass7w", "7B 42 A1", "ক্কিঁ")]
    // the composer writes ₹ at 3C (no face has a glyph there)
    [InlineData("shree-ban7", "31 3C 32", "১₹২")]
    [InlineData("shree-ass456", "3C", "₹")]
    // the composer's own spellings that lose a sign still read as the text typed (its kept spacer is the ু)
    [InlineData("shree-ban456", "D8 6C A1 F2 A1", "ড়ুঁ")]
    [InlineData("shree-ban7", "D8 6C A1 A2 A1", "র্ড়ু")]
    [InlineData("shree-ban7", "D8 6E F6 A1 A2 A1", "র্ঢ়্রু")]
    [InlineData("shree-ban456", "D2 EA A1 A2 A1", "র্হ্রৃ")]
    [InlineData("shree-ass456", "D2 EA A1 A2 A1", "ৰ্হ্ৰৃ")]
    [InlineData("shree-ban7", "B9 E7 A1 A2 A1", "র্র্রু")]
    // the subscripts ত 7A and ন 95 alone: the table shipped before the split wrote ম্ত as B4 7A, ম্ন as B4 95 (read ম্z, ম্•)
    [InlineData("shree-ban7", "B4 7A", "ম্ত")]
    [InlineData("shree-ass456", "B4 95 E0", "ম্না")]
    public void FamilyCodesReadAsTheirFamilyDrawsThem(string id, string bytes, string unicode)
    {
        Assert.Equal(unicode, From(id).ConvertText(FromHex(bytes)));
    }

    // (e) Spellings worth naming: 7 and 4/5/6 bytes, and back.
    [Theory]
    [InlineData("কঃ", "41 A1 96", "41 A1 40")]             // visarga: 96 in 7, 40 in 4/5/6
    [InlineData("ন্দ", "40 83", "96 83")]                   // the half-ন hook: 40 in 7, 96 in 4/5/6
    [InlineData("ক্ন", "41 A5 A1", "41 97 A1")]             // subscript ্ন: A5 in 7, 97 in 4/5/6
    [InlineData("ধ্ম", "8B F4 B3", "8C")]                    // no ধ্ম glyph in 7: ধ্ + ম
    [InlineData("ব্ঝ", "A4 F4 63 A1", "A8 A1")]             // no ব্ঝ glyph in 7: ব্ + ঝ
    [InlineData("র্কি", "5B 41 A2 A1", "5B 41 A2 A1")]       // i-sign first, the reph before ক's spacer
    [InlineData("র্কা", "41 A1 E0 A2", "41 A1 E0 A2")]       // after া the reph comes last
    [InlineData("কো", "EC 41 A1 E0", "EC 41 A1 E0")]         // ো = ে ... া
    [InlineData("কুঁ", "41 E5 F2 A1", "41 E5 F2 A1")]         // signs below and ঁ before the spacer
    [InlineData("কিঁ", "7B 41 A1", "7B 41 A1")]               // ি + ঁ: one glyph
    [InlineData("গ্যু", "50 A1 B8", "50 A1 B8")]             // গু has its own glyph, the ya-phala after it
    [InlineData("প্র", "9A F8", "9A F8")]                    // ra-phala F8 on প
    [InlineData("ধ্ব", "8B AB", "8B AB")]                    // the subscript ব, never ধ + filler + ব (drawn ধব)
    [InlineData("ব্ব", "A4 AB", "A4 AB")]
    [InlineData("ধ্বু", "8B AB E6", "8B AB E6")]             // ু under the subscript: E6
    [InlineData("দ্গ", "A8 4B", "83 F4 4B")]                 // 7: the joining দ; 4/5/6: a visible hasanta
    [InlineData("দ্ঘ", "A8 51", "83 F4 51")]
    [InlineData("দ্গি", "5B A8 4B", "5B 83 F4 4B")]
    [InlineData("র্দ্গ", "A8 4B A2", "83 F4 4B A2")]
    [InlineData("ড়্গ", "FF", "D8 6C F4 A1 4B")]              // 7: the ligature FF; 4/5/6: ড়্ + গ
    [InlineData("ড়্গু", "FF E5", "D8 6C F4 A1 4B E5")]
    [InlineData("ৱ্ৱ", "U+09F1 F4 U+09F1", "U+09F1 F4 U+09F1")]   // ৱ passes through (its 9E is a C1 code)
    // ব্ঢ ব্ড চ্ঞ: the composer's 81 8D 90 are C1 codes nothing draws - letter + visible hasanta + letter
    [InlineData("ব্ঢ", "A4 F4 6E A1", "A4 F4 6E A1")]
    [InlineData("ব্ঢু", "A4 F4 6E E5 A1", "A4 F4 6E E5 A1")]
    [InlineData("ব্ড", "A4 F4 6C A1", "A4 F4 6C A1")]
    [InlineData("চ্ঞা", "57 F4 A1 64 A1 E0", "57 F4 A1 64 A1 E0")]
    [InlineData("চ্ঞি", "5B 57 F4 A1 64 A1", "5B 57 F4 A1 64 A1")]
    // a ra-phala with a ra-phala or a hasanta of its own: the composer's spelling (two F6 at one spot draw one)
    [InlineData("ক্র্র", "41 F4 A1 B9 F8", "41 F4 A1 B9 F8")]
    [InlineData("ক্র্", "41 F4 A1 B9 F4", "41 F4 A1 B9 F4")]
    // ড় ঢ় with ু ূ and ঁ or a reph: the composer's own spelling draws no ু ূ (ড়ুঁ = D8 6C A1 F2 A1, র্ড়ু = D8 6C A1 A2 A1);
    // its alternative keeps the sign after the spacer (র্ড়ু = D8 6C A2 A1 E6), as ড়ু = D8 6C A1 E6 does
    [InlineData("ড়ুঁ", "D8 6C F2 A1 E6", "D8 6C F2 A1 E6")]
    [InlineData("ড়ূঁ", "D8 6C F2 A1 E9", "D8 6C F2 A1 E9")]
    [InlineData("ঢ়ুঁ", "D8 6E F2 A1 E6", "D8 6E F2 A1 E6")]
    [InlineData("র্ড়ু", "D8 6C A2 A1 E6", "D8 6C A2 A1 E6")]
    [InlineData("র্ড়ূ", "D8 6C A2 A1 E9", "D8 6C A2 A1 E9")]
    [InlineData("র্ঢ়ুং", "D8 6E A2 A1 E6 7D", "D8 6E A2 A1 E6 7D")]
    [InlineData("র্ড়ূঁ", "D8 6C A2 F2 A1 E9", "D8 6C A2 F2 A1 E9")]
    [InlineData("র্ড়্রূ", "D8 6C F6 A2 A1 E9", "D8 6C F6 A2 A1 E9")]
    // and র্হ্রৃ, whose composer spelling (D2 EA A1 A2 A1) loses the ra-phala F9 that হ্রৃ = D2 EA A1 F9 has
    [InlineData("র্হ্রৃ", "D2 EA A2 A1 F9", "D2 EA A2 A1 F9")]
    public void NamedSpellings(string unicode, string seven, string fourFiveSix)
    {
        Assert.Equal(seven, Hex(To("shree-ban7").ConvertText(unicode)));
        Assert.Equal(fourFiveSix, Hex(To("shree-ban456").ConvertText(unicode)));
        Assert.Equal(unicode, From("shree-ban7").ConvertText(FromHex(seven)));
        Assert.Equal(unicode, From("shree-ban456").ConvertText(FromHex(fourFiveSix)));
    }

    // (f) Assamese Unicode spells ra ৰ: ৰ্ + a letter is the reph and ্ৰ the ra-phala, in every family
    // (never ৰ + a visible hasanta, which is what Shree's composer writes for them). Read back, an
    // Assamese family writes every ra ৰ - a word never mixes ৰ and র - and a Bangla one র.
    [Theory]
    [InlineData("কৰ্ম", "41 A1 B3 A2")]
    [InlineData("প্ৰকাশ", "9A F8 41 A1 E0 C5")]
    [InlineData("শ্ৰী", "C5 F8 E3")]
    [InlineData("ৰাষ্ট্ৰ", "B9 E0 CA A1 F6")]
    [InlineData("প্ৰধানমন্ত্ৰী", "9A F8 8B E0 3E B3 94 7C E3")]
    [InlineData("কাৰ্য", "41 A1 E0 99 A2")]
    [InlineData("ৰ্কি", "5B 41 A2 A1")]
    [InlineData("ক্ৰু", "79 FB E6 A1")]
    public void AssameseRaIsTheRephAndTheRaPhala(string assamese, string bytes)
    {
        string bangla = assamese.Replace('ৰ', 'র');
        foreach (var fam in Families)
        {
            var c = To(fam.Id).Convert(assamese);
            Assert.True(bytes == Hex(c.Text), $"{fam.Id}: {assamese} want {bytes}, got {Hex(c.Text)}");
            Assert.Empty(c.UnmappedCharacters);
            Assert.Equal(Hex(To(fam.Id).ConvertText(bangla)), Hex(c.Text));
            string back = From(fam.Id).ConvertText(c.Text);
            Assert.Equal(fam.Assamese ? assamese : bangla, back);
            Assert.DoesNotContain(fam.Assamese ? 'র' : 'ৰ', back);
        }
    }

    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void AnAssameseReadBackNeverMixesTheTwoRas(string id)
    {
        var fam = Fam(id);
        var from = From(id);
        foreach (var r in Drawn(fam))
        {
            string got = from.ConvertText(FromHex(fam.Apply(r.Pref)));
            Assert.DoesNotContain(fam.Assamese ? 'র' : 'ৰ', got);
        }
    }

    // ra with a visible hasanta, as the composer writes ৰ্কি (5B B9 F4 41 A1): the i-sign is the
    // whole cluster's, never রি্‌ক; ZWNJ marks the hasanta, and a hasanta that ends a word needs none.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void ARaWithAVisibleHasantaReadsAsOne(string id)
    {
        var fam = Fam(id);
        var from = From(id);
        string ra = fam.Ra.ToString();
        Assert.Equal(ra + "্" + Zwnj + "কি", from.ConvertText(FromHex("5B B9 F4 41 A1")));
        Assert.Equal(ra + "্" + Zwnj + "কে", from.ConvertText(FromHex("EC B9 F4 41 A1")));
        Assert.Equal(ra + "্" + Zwnj + "ক্ষৈ", from.ConvertText(FromHex("EE B9 F4 DB A1")));
        Assert.Equal(ra + "্" + Zwnj + "ক", from.ConvertText(FromHex("B9 F4 41 A1")));
        Assert.Equal("ক" + ra + "্", from.ConvertText(FromHex("41 A1 B9 F4")));
        // and they go back to the same bytes
        Assert.Equal("B9 F4 41 A1", Hex(To(id).ConvertText(ra + "্" + Zwnj + "ক")));
    }

    // (g) ৱ. The Bangla fonts draw it at 9E, which their cmaps hold at U+009E - a C1 code Word and
    // RichEdit draw as nothing, and no other code reaches that glyph. So it is not written: ৱ passes
    // through as itself, reported, and Word's font fallback draws it. 9E reads ৱ in every family - as
    // U+009E, as U+017E (Windows-1252's 9E, as a .txt file decodes it) and as U+09F1. The Assamese
    // fonts draw ৱ at AF.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void WaIsNeverWrittenAsACodeWordDrawsAsNothing(string id)
    {
        var fam = Fam(id);
        var c = To(id).Convert("ৱা");
        if (fam.Assamese)
        {
            Assert.Equal("AF E0", Hex(c.Text));
            Assert.Empty(c.UnmappedCharacters);
        }
        else
        {
            Assert.Equal("ৱ" + (char)0xE0, c.Text);
            Assert.Equal(new[] { "ৱ" }, c.UnmappedCharacters);
        }
        Assert.Equal("ৱা", From(id).ConvertText(c.Text));
        Assert.Equal("ৱা", From(id).ConvertText(new string(new[] { Wa, (char)0xE0 })));
        Assert.Equal("ৱা", From(id).ConvertText(new string(new[] { Zcaron, (char)0xE0 })));
        Assert.Equal(fam.Ra + "াৱণ", From(id).ConvertText(To(id).ConvertText("ৰাৱণ")));
        // ž itself is left out, reported: its code is the one a .txt file gives the byte 9E, read as ৱ.
        var z = To(id).Convert(Zcaron.ToString());
        Assert.Equal("", z.Text);
        Assert.Contains(Zcaron.ToString(), z.UnmappedCharacters);
    }

    // (h) Every two-letter cluster with the ra-phala and ু or ূ (স্ট্রু, ক্ক্রূ) reads back - the ু ূ under a
    // conjunct's ra-phala (E6 E9) are signs, never æ é.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void ClusterWithRaPhalaAndUReadsBack(string id)
    {
        var fam = Fam(id);
        var to = To(id);
        var from = From(id);
        var wrong = new List<string>();
        int n = 0;
        var clusters = Truth.Value.Select(r => r.U).Where(u => u.Length == 3 && u[1] == '্' && IsLetter(u[0]) && IsLetter(u[2]) && u[0] is not ('র' or 'ৰ')).Distinct();
        foreach (var c in clusters)
            foreach (var sign in new[] { "ু", "ূ" })
            {
                string u = c + "্" + fam.Ra + sign;
                var w = to.Convert(u);
                if (w.HasUnmapped) continue;
                n++;
                string got = from.ConvertText(w.Text);
                if (!SameText(got, u, Hex(w.Text), to) || Malformed(got).Count > 0) wrong.Add($"{u} = {Hex(w.Text)}: read {got} [{Cps(got)}]");
            }
        Assert.True(n > 2000, $"{id}: only {n} clusters");
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
        Assert.Equal("D0 A1 F6 E6", Hex(to.ConvertText("স্ট্রু")));
        Assert.Equal("স্ট্" + fam.Ra + "ু", from.ConvertText(FromHex("D0 A1 F6 E6")));
        Assert.Equal("ক্ক্" + fam.Ra + "ূ", from.ConvertText(FromHex("42 F6 E9 A1")));
    }

    // (i) Nothing is dropped unseen, and nothing is written as a letter it is not. ASCII the fonts draw
    // as itself is written as itself, unreported (! ( ) , - . ? and the digits, which draw the Bengali
    // digits as Shree's composer writes them); ASCII whose code draws a letter (/ the base of ব্ভ, = থ,
    // ; ৎ, + ঔ, * ও, [ the i-sign ...) is left out and reported - and so are the Latin letters,
    // whose codes all draw Bengali glyphs, and '<', which no face draws (3C is where Shree's composer writes ₹); a
    // character the fonts have no code for passes through, reported.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void ACharacterTheFontCannotDrawIsReportedNeverDropped(string id)
    {
        var to = To(id);
        foreach (string s in new[] { "!", "(", ")", ",", "-", ".", "?", "0", "5", "9" })
        {
            var c = to.Convert(s);
            Assert.Equal(s, c.Text);
            Assert.Empty(c.UnmappedCharacters);
        }
        string kakha = Hex(to.ConvertText("কখ"));
        foreach (string s in new[] { "/", "=", ";", "+", "*", "[", "]", "#", "$", "&", "<", ">", "@", "\\", "^", "_", "`", "{", "|", "}", "~", "×", "÷", "°", "A", "T", "a", "z" })
        {
            var c = to.Convert(s);
            Assert.True(c.Text.Length == 0, $"{id}: {s} written {Hex(c.Text)}");
            Assert.Contains(s, c.UnmappedCharacters);
            Assert.Equal(kakha, Hex(to.ConvertText("ক" + s + "খ")));
        }
        foreach (string s in new[] { "₹", "ৄ", "ৠ", "ৡ", "ৢ", "ৣ" })
        {
            var c = to.Convert(s);
            Assert.Equal(s, c.Text);
            Assert.Contains(s, c.UnmappedCharacters);
        }
        var date = to.Convert("১২/০৮/২০২৪");
        Assert.Equal("31 32 30 38 32 30 32 34", Hex(date.Text));
        Assert.Equal(new[] { "/" }, date.UnmappedCharacters);
        Assert.Equal(new[] { "+", "=" }, to.Convert("২+২=৪").UnmappedCharacters.OrderBy(x => x, StringComparer.Ordinal));
        // A pair that writes nothing is marked unmapped (the joiners aside: the spelling around them carries them).
        Assert.DoesNotContain(Assets.Value.FindMapping("vrinda", id)!.Pairs,
            p => p.To.Replace(Keep.ToString(), "").Length == 0 && !p.Unmapped && !p.From.All(ch => ch == (char)0x200C || ch == (char)0x200D));
        // Every character of the blocks a reader types, alone: written, or reported.
        foreach (int cp in Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA0, 0x60)).Concat(Enumerable.Range(0x0980, 0x80))
                     .Concat(Enumerable.Range(0x2000, 0x70)).Concat(Enumerable.Range(0x20A0, 0x20)))
        {
            string s = ((char)cp).ToString();
            if (char.GetUnicodeCategory(s[0]) == System.Globalization.UnicodeCategory.Format) continue;   // joiners, marks of direction
            var c = to.Convert(s);
            Assert.True(c.Text.Length > 0 || c.UnmappedCharacters.Contains(s), $"{id}: U+{cp:X4} dropped without a word");
        }
    }

    // (i2) ASCII quotes: 22 draws অ and 27 ঐ in these fonts. They are the fonts' quotes ‘ 91 and ’ 92.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void AsciiQuotesAreTheFontsQuotes(string id)
    {
        var to = To(id);
        Assert.Equal("91 91 41 A1 92 92", Hex(to.ConvertText("\"ক\"")));
        Assert.Equal("91 41 A1 92", Hex(to.ConvertText("'ক'")));
        Assert.Equal("41 A1 20 91 91 4A 92 92", Hex(to.ConvertText("ক \"খ\"")));
        Assert.Empty(to.Convert("\"ক\" 'খ'").UnmappedCharacters);
        Assert.Equal("“ক”", From(id).ConvertText(to.ConvertText("\"ক\"")));
    }

    // (i3) P3: every Latin letter's code is a Bengali glyph in these fonts (T 54 = ঙ্খ, e 65, x 78 = ত্থ ...), so English
    // in a Bangla Shree run is left out and reported - never written as those glyphs.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void EnglishLettersAreLeftOutAndReported(string id)
    {
        var to = To(id);
        var c = to.Convert("TextSoft বাংলা");
        Assert.Equal("20 A4 E0 7D BA E0", Hex(c.Text));
        Assert.Equal("STefotx", string.Concat(c.UnmappedCharacters.OrderBy(x => x, StringComparer.Ordinal)));
        for (char l = 'A'; l <= 'z'; l++)
        {
            if (!char.IsAsciiLetter(l)) continue;
            var r = to.Convert(l.ToString());
            Assert.True(r.Text.Length == 0 && r.UnmappedCharacters.Contains(l.ToString()), $"{id}: {l} written {Hex(r.Text)}");
        }
        Assert.Equal("", to.ConvertText("a:b").Replace(":", ""));
    }

    // (i4) Beyond ASCII: the Unicode spaces are the fonts' space 20 (no face maps them; a .txt made them '?'), the
    // line and paragraph separators stay line breaks, · and ∙ are the fonts' dot B7 (∙ is the same glyph; RichEdit
    // moves U+2219 to another font and a .txt has no byte for it), the soft hyphen is left out and reported (blank in
    // most faces, a visible hyphen in the -00x faces and in Word's PDF), and the tables' own marks U+FDD0-U+FDDF
    // in the text are never taken for marks: U+FFFD, reported.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void SpacesDotsSoftHyphenAndMarksInTheText(string id)
    {
        var to = To(id);
        foreach (int cp in new[] { 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A, 0x202F, 0x205F, 0x3000 })
        {
            var r = to.Convert("ক" + (char)cp + "খ");
            Assert.True(Hex(r.Text) == "41 A1 20 4A" && !r.HasUnmapped, $"{id}: U+{cp:X4} written {Hex(r.Text)}");
        }
        string ls = ((char)0x2028).ToString(), ps = ((char)0x2029).ToString();
        var lines = to.Convert("ক" + ls + "খ" + ps);
        Assert.Equal(FromHex("41 A1") + ls + FromHex("4A") + ps, lines.Text);
        Assert.False(lines.HasUnmapped);
        foreach (int cp in new[] { 0x00B7, 0x2219 })
        {
            var r = to.Convert("ক" + (char)cp + "খ");
            Assert.True(Hex(r.Text) == "41 A1 B7 4A" && !r.HasUnmapped, $"{id}: U+{cp:X4} written {Hex(r.Text)}");
        }
        string shy = ((char)0x00AD).ToString();
        var soft = to.Convert("ক" + shy + "খ");
        Assert.Equal("41 A1 4A", Hex(soft.Text));
        Assert.Equal(new[] { shy }, soft.UnmappedCharacters);
        string fffd = ((char)0xFFFD).ToString();
        for (int cp = 0xFDD0; cp <= 0xFDDF; cp++)
        {
            var r = to.Convert("ক" + (char)cp + "খ");
            Assert.True(r.Text == FromHex("41 A1") + fffd + FromHex("4A") && r.UnmappedCharacters.Contains(fffd), $"{id}: U+{cp:X4} written {Hex(r.Text)}");
            Assert.Equal("ক" + fffd + "খ", From(id).ConvertText(FromHex("41 A1") + (char)cp + FromHex("4A")));
        }
    }

    // (i5) The reverse never answers with ASCII the text did not have: ি + ঁ (7B) on any cluster of the truth, with and
    // without a reph, and a half letter under a reph (র্ঙ্ক্ষ = 56 DB A2 A1), read back as the text.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void ISignCandrabinduOnEveryClusterReadsBack(string id)
    {
        var fam = Fam(id);
        var to = To(id);
        var from = From(id);
        var clusters = Truth.Value.Select(r => r.U).Where(u => u.Length > 0 && IsLetter(u[0]) && u.All(ch => IsLetter(ch) || ch == '্' || ch == '়')
            && u[^1] != '্' && !(u.Length > 2 && u[0] is 'র' or 'ৰ' && u[1] == '্')).Distinct().ToList();
        var wrong = new List<string>();
        int n = 0;
        foreach (var c in clusters)
            foreach (var u in new[] { c + "িঁ", fam.Ra + "্" + c + "িঁ", fam.Ra + "্" + c, c })
            {
                var w = to.Convert(u);
                if (w.UnmappedCharacters.Any(x => x != "ৱ")) continue;
                n++;
                string got = from.ConvertText(w.Text);
                bool ascii = got.Any(ch => ch is >= '!' and <= '~' && !char.IsDigit(ch));
                if (ascii || !SameText(got, u, Hex(w.Text), to) || Malformed(got).Count > 0) wrong.Add($"{u} = {Hex(w.Text)}: read {got} [{Cps(got)}]");
            }
        Assert.True(n > 5000, $"{id}: only {n}");
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} of {n} wrong:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (i6) Decision 6: ড় ঢ় with ু or ূ always draw the sign (E6 E9), u and uu apart - in the converter and in typing, which
    // use the same table - and read back as the text.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void TheUSignOfRraAndRhaIsAlwaysDrawn(string id)
    {
        var fam = Fam(id);
        var to = To(id);
        var from = From(id);
        var typing = Assets.Value.CreateConverterToLegacy(id)!;
        // the sign on ড় ঢ় itself (or on its ra-phala), not on a letter after it (ড়্কু)
        var sign = new System.Text.RegularExpressions.Regex("[" + (char)0x09DC + (char)0x09DD + "](" + (char)0x09CD + "[" + (char)0x09B0 + (char)0x09F0 + "])?[" + (char)0x09C1 + (char)0x09C2 + "]");
        var rows = Truth.Value.Where(r => sign.IsMatch(r.U)).ToList();
        Assert.True(rows.Count >= 100, $"only {rows.Count} rows");
        foreach (var r in rows)
        {
            string got = Hex(to.ConvertText(r.U));
            Assert.Equal(got, Hex(typing.ConvertText(r.U)));
            string glyph = r.U.Contains('ু') ? "E6" : "E9";
            Assert.True(got.Split(' ').Contains(glyph), $"{id}: {r.U} written {got}, no {glyph}");
            string other = r.U.Contains('ু') ? r.U.Replace('ু', 'ূ') : r.U.Replace('ূ', 'ু');
            Assert.NotEqual(got, Hex(to.ConvertText(other)));
            Assert.Equal(fam.Assamese ? r.U.Replace('র', 'ৰ') : r.U.Replace('ৰ', 'র'), from.ConvertText(to.ConvertText(r.U)));
        }
    }

    [Theory]
    [InlineData("shree-ban7", "35", "31 30 33 30", true, true)]            // % and : are marks below the letter (blank in Shree-Ban7-00x): left out
    [InlineData("shree-ban456", "35 25", "31 30 33 30", false, true)]      // % is itself, : a mark: left out
    [InlineData("shree-ass7", "35 25", "31 30 3A 33 30", true, true)]      // no % or : in the fonts: passed through, reported
    [InlineData("shree-ass456", "35 25", "31 30 3A 33 30", false, false)]
    [InlineData("shree-ass7w", "35 25", "31 30 3A 33 30", false, false)]
    public void PercentAndColonWhereTheFontHasThem(string id, string percent, string colon, bool percentReported, bool colonReported)
    {
        var to = To(id);
        Assert.Equal(percent, Hex(to.ConvertText("৫%")));
        Assert.Equal(colon, Hex(to.ConvertText("১০:৩০")));
        Assert.Equal(percentReported, to.Convert("৫%").UnmappedCharacters.Contains("%"));
        Assert.Equal(colonReported, to.Convert("১০:৩০").UnmappedCharacters.Contains(":"));
    }

    // (c3) Every other spelling the composer accepts reads well formed too, not only the preferred ones.
    [Theory]
    [MemberData(nameof(FamilyIds))]
    public void EveryAcceptedSpellingReadsWellFormed(string id)
    {
        var fam = Fam(id);
        var from = From(id);
        var wrong = new List<string>();
        int n = 0;
        foreach (var r in Truth.Value)
            foreach (var a in r.Accepted.Skip(1).Distinct())
            {
                string bytes = fam.Apply(a);
                if (bytes.Length == 0 || Malformed(r.U).Count > 0) continue;
                n++;
                string got = from.ConvertText(FromHex(bytes));
                var bad = Malformed(got);
                if (bad.Count > 0) wrong.Add($"{bytes}: read {got} [{Cps(got)}] for {r.U}: {string.Join("; ", bad)}");
            }
        Assert.True(n > 1000, $"{id}: only {n} accepted spellings");
        Assert.True(wrong.Count == 0, $"{id}: {wrong.Count} of {n} malformed:\n" + string.Join("\n", wrong.Take(25)));
    }

    // (j) A hasanta that ends a word is the letter with a visible hasanta - never a pending form
    // waiting for a letter that does not come (the reph A2 for র্).
    [Theory]
    [InlineData("বাক্", "A4 E0 41 F4 A1")]
    [InlineData("সৎ", "CE 3B")]                       // khanda ta is a letter of its own
    [InlineData("দিক্ ", "5B 83 41 F4 A1 20")]
    [InlineData("ক্।", "41 F4 A1 A1 FA")]
    [InlineData("ক্", "41 F4 A1")]
    [InlineData("কর্", "41 A1 B9 F4")]
    [InlineData("কর্ ক", "41 A1 B9 F4 20 41 A1")]
    [InlineData("কর্।", "41 A1 B9 F4 A1 FA")]
    [InlineData("কৰ্", "41 A1 B9 F4")]
    [InlineData("র্", "B9 F4")]
    [InlineData("কর্১", "41 A1 B9 F4 31")]
    public void AHasantaThatEndsAWordIsVisible(string unicode, string bytes)
    {
        foreach (var fam in Families)
        {
            var c = To(fam.Id).Convert(unicode);
            Assert.True(bytes == Hex(c.Text), $"{fam.Id}: {unicode} want {bytes}, got {Hex(c.Text)}");
            string back = From(fam.Id).ConvertText(c.Text);
            Assert.Equal(fam.Assamese ? unicode.Replace('র', 'ৰ') : unicode.Replace('ৰ', 'র'), back);
        }
    }

    // (k) The 4/5/6 fonts hold glyphs at control codes; a document that carries one reads as the font
    // draws it - but tab, line feed, vertical tab, form feed and carriage return stay what they are.
    [Theory]
    [InlineData("08", "ব", "ব")]
    [InlineData("0E", "+", "+")]
    [InlineData("0F", "*", "র")]                     // a Bengali র in the Assamese fonts
    [InlineData("1A", "—", "—")]
    [InlineData("1B", "×", "×")]
    [InlineData("1C", "÷", "÷")]
    [InlineData("1D", "[", "[")]
    [InlineData("1E", "]", "]")]
    [InlineData("1F", "॥", "॥")]
    [InlineData("09", "\t", "\t")]
    [InlineData("0A", "\n", "\n")]
    [InlineData("0D", "\r", "\r")]
    public void ControlCodeGlyphsReadAsDrawn(string code, string bangla, string assamese)
    {
        Assert.Equal(bangla, From("shree-ban456").ConvertText(FromHex(code)));
        Assert.Equal(assamese, From("shree-ass456").ConvertText(FromHex(code)));
    }

    // (l) Whole words as a typist types them (nukta letters as letter + dot), there and back
    // through the app's own pipeline.
    [Theory]
    [InlineData("shree-ban7", "কর্ম", "41 A1 B3 A2")]
    [InlineData("shree-ban7", "প্রকাশ", "9A F8 41 A1 E0 C5")]
    [InlineData("shree-ban7", "বিদ্যার্থী", "5B A4 83 B8 E0 3D E3 A2")]
    [InlineData("shree-ban456", "রাষ্ট্র", "B9 E0 CA A1 F6")]
    [InlineData("shree-ban456", "হৃদয়", "D2 EA A1 83 DA")]      // হৃদয়
    [InlineData("shree-ban456", "উদ্ঘাটন", "6C A1 FC 83 F4 51 E0 69 A1 3E")]   // দ্ঘ: দ্ + ঘ (the filler drew দঘ)
    [InlineData("shree-ban7", "উদ্ঘাটন", "6C A1 FC A8 51 E0 69 A1 3E")]        // the 7 fonts' joining দ
    [InlineData("shree-ban7", "ধ্বংস", "8B AB 7D CE")]
    [InlineData("shree-ban456", "ঊর্ধ্ব", "24 8B AB A2")]
    [InlineData("shree-ass7", "ৰাজ্য", "B9 E0 5C B8")]                                    // ৰ is B9 in the Assamese fonts
    [InlineData("shree-ass7", "কৰ্ম", "41 A1 B3 A2")]
    [InlineData("shree-ass456", "প্ৰকাশ", "9A F8 41 A1 E0 C5")]
    [InlineData("shree-ass456", "অসমীয়া", "22 CE B3 E3 DA E0")] // অসমীয়া
    [InlineData("shree-ass7w", "ৰাষ্ট্ৰ", "B9 E0 CA A1 F6")]
    [InlineData("shree-ban456", "ইনস্ট্রুমেন্ট", "D2 FC 3E D0 A1 F6 E6 EC B3 96 69 A1")]
    public void TypedWords(string id, string word, string bytes)
    {
        var p = new ConversionPipeline(Assets.Value);
        var legacy = LayoutKey.Legacy(id);
        var there = p.Convert(word, LayoutKey.Unicode, legacy);
        Assert.Equal(bytes, Hex(there.Text));
        Assert.Empty(there.UnmappedCharacters);
        Assert.Equal(word, p.Convert(there.Text, legacy, LayoutKey.Unicode).Text);
    }

    // ---------- helpers ----------

    private static List<Row> LoadTruth()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-ban7-truth.json");
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
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "shree-ban-cmaps.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var d = new Dictionary<string, (string[], HashSet<char>)>();
        foreach (var fam in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
            d[fam.Name] = (fam.Value.GetProperty("faces").EnumerateArray().Select(f => f.GetString()!).ToArray(),
                fam.Value.GetProperty("codePoints").GetString()!.Split(' ').Select(h => (char)Convert.ToInt32(h, 16)).ToHashSet());
        return d;
    });

    /// <summary>The truth rows the family draws (a character it has no code for has an empty spelling).</summary>
    private static List<Row> Drawn(Family fam) => Truth.Value.Where(r => fam.Apply(r.Pref).Length > 0).ToList();

    /// <summary>Same text: equal, or text the table writes with the very same bytes (a character the font cannot draw leaves no trace).</summary>
    private static bool SameText(string got, string want, string bytes, FontConverter forward)
    {
        if (got == want) return true;
        var r = forward.Convert(got);
        // (ৱ in a Bangla family passes through, reported: the bytes then carry it as U+09F1)
        return r.UnmappedCharacters.All(u => u == "ৱ" && bytes.Contains("U+09F1")) && Hex(r.Text) == bytes;
    }

    private static bool IsMark(char c) =>
        (c >= 'ঁ' && c <= 'ঃ') || c == '়' || (c >= 'া' && c <= '্') || c == 'ৗ' || c == 'ৢ' || c == 'ৣ';

    private static bool IsLetter(char c) =>
        (c >= 'ক' && c <= 'হ' && c != (char)0x09A9 && c != (char)0x09B1 && !(c >= (char)0x09B3 && c <= (char)0x09B5))
        || c is 'ড়' or 'ঢ়' or 'য়' or 'ৰ' or 'ৱ';

    private static bool IsVowel(char c) => (c >= 'অ' && c <= 'ঔ') || c == 'ৠ' || c == 'ৡ';

    /// <summary>
    /// Ways a string is not well formed Bengali (empty when it is): a vowel sign or hasanta not on a
    /// letter (ি্ in রি্‌ক), a nukta not on a letter, ঁ ং ঃ on a hasanta or on nothing, a joiner away
    /// from a hasanta, a Latin letter or sign leaking through (স্ট্রæ).
    /// </summary>
    private static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        static bool OnLetter(char c) => IsLetter(c) || c == '়';
        static bool Joiner(char c) => c == (char)0x200C || c == (char)0x200D;
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool vowelSign = (c >= 'া' && c <= 'ৌ') || c == 'ৗ' || c == 'ৢ' || c == 'ৣ';
            if (vowelSign && !OnLetter(p)) bad.Add($"vowel sign at {i}");
            if (c == '্' && !(OnLetter(p) || (Joiner(p) && i > 1 && u[i - 2] is 'র' or 'ৰ'))) bad.Add($"hasanta at {i}");
            if (c == '়' && !(p >= 'ক' && p <= 'হ')) bad.Add($"nukta at {i}");
            if (c is 'ঁ' or 'ং' or 'ঃ' && (i == 0 || p == '্' || Joiner(p) || !(OnLetter(p) || IsVowel(p) || IsMark(p) || p == 'ৎ')))
                bad.Add($"{c} at {i}");
            if (Joiner(c) && !(p == '্' || (p is 'র' or 'ৰ' && i + 1 < u.Length && u[i + 1] == '্'))) bad.Add($"joiner at {i}");
            if (c >= (char)0xC0 && c <= (char)0x24F && c != '×' && c != '÷') bad.Add($"Latin {c} at {i}");
        }
        return bad;
    }

    /// <summary>Truth strings that can stand next to each other in a word: they start on a
    /// base, do not end on a virama, and have no sign straight after a virama.</summary>
    private static List<Row> Syllables(IEnumerable<Row> rows) => rows.Where(r =>
            r.U.Length > 0 && !IsMark(r.U[0]) && r.U[0] != (char)0x200C && r.U[0] != (char)0x200D && r.U[^1] != '্' &&
            !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == '্' && IsMark(r.U[i]) && r.U[i] != '্'))
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

    /// <summary>Legacy text as hex bytes (U+009E is 9E); a character outside the fonts' code page shows as U+XXXX.</summary>
    private static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            if (c == Wa) { parts.Add("9E"); continue; }
            if (c == Zcaron) { parts.Add("U+017E"); continue; }
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Hex bytes as the code points the fonts' cmaps hold: Windows-1252, the C1 bytes 81 8D 8F 90 9D and 9E
    /// as U+0081 ... U+009E; a token U+XXXX is that character (one the tables pass through).
    /// </summary>
    private static string FromHex(string hex) =>
        hex.Length == 0 ? "" : string.Concat(hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h =>
        {
            if (h.StartsWith("U+")) return ((char)Convert.ToInt32(h[2..], 16)).ToString();
            byte b = Convert.ToByte(h, 16);
            return b is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D or 0x9E ? ((char)b).ToString() : Cp1252.GetString(new[] { b });
        }));

    private static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
}
