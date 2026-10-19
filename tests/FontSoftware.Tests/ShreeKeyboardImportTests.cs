using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Keyboards made in Shree-Lipi's own Keyboard Generator come in as keyboards of
/// our own. The files here are built byte by byte the way the generator writes
/// them - the vendor's own files are not ours to ship - with codes read out of
/// Shree's composer (tools/KbdFileProbe, which also checks the importer against
/// every keyboard Shree ships: 99-100% of keys the same in every script).
/// </summary>
public class ShreeKeyboardImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "textsoft-shree-" + Guid.NewGuid().ToString("N"));
    private readonly AssetBundle _bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();

    public ShreeKeyboardImportTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>A blank keyboard file: no key types anything, every conjunct slot empty.</summary>
    private static byte[] Blank()
    {
        var f = new byte[ShreeKeyboardFile.Length];
        for (int i = 0; i < 32; i++) f[i] = (byte)i;
        for (int i = 32; i < 166; i++) f[i] = 0xFF;
        for (int r = 0; r < 48; r++)
        {
            f[166 + r * 5] = 4;
            for (int k = 1; k < 5; k++) f[166 + r * 5 + k] = 0xFF;
        }
        return f;
    }

    /// <summary>
    /// A small Gujarati keyboard: k is ક, Shift+Q is the conjunct ઔ (stored the
    /// way Shree stores it, last piece first: ૈ ા અ), and Ctrl+Space then v is ૠ.
    /// </summary>
    private static byte[] Gujarati()
    {
        var f = Blank();
        f['k'] = 0x73;                                   // ક
        f['K'] = 0x53;                                   // ખ
        f['Q'] = 0xB4;                                   // conjunct slot 4
        f[166 + 4 * 5 + 1] = 0x70;                       // ૈ
        f[166 + 4 * 5 + 2] = 0x6B;                       // ા
        f[166 + 4 * 5 + 3] = 0x24;                       // અ
        f[0x80 + ShreeKeyboardFile.EscapeKeys.IndexOf('v')] = 0xE8;   // Ctrl+Space v: ૠ
        return f;
    }

    private ShreeCodeTable Table(string script) => _bundle.GetShreeCodeTable(script)!;

    [Fact]
    public void EveryScriptShreeWritesHasItsTable()
    {
        foreach (string script in ShreeKeyboardFile.Scripts)
            Assert.True(_bundle.GetShreeCodeTable(script) is { Codes.Count: > 90 }, $"no code table for .{script}");
    }

    [Fact]
    public void TheThreeLayersComeAcross()
    {
        var read = ShreeKeyboardFile.Decode(Gujarati(), Table("GUJ"));
        Assert.Equal("ક", read.Keys['k']);
        Assert.Equal("ખ", read.Keys['K']);
        Assert.Equal("ૠ", read.Escape['v']);
        Assert.False(read.Keys.ContainsKey('j'), "0xFF is a key that types nothing");
    }

    /// <summary>A conjunct is stored last piece first, and Shree joins અ ા ૈ into the one letter ઔ.</summary>
    [Fact]
    public void AConjunctIsReadTheWayShreeJoinsIt()
    {
        var read = ShreeKeyboardFile.Decode(Gujarati(), Table("GUJ"));
        Assert.Equal("ઔ", read.Keys['Q']);
        Assert.Equal(1, read.ConjunctKeys);
    }

    [Fact]
    public void TheLayoutTypesCtrlSpaceAndCarriesTheScriptsSymbols()
    {
        var gujarati = _bundle.Languages.First(l => l.Id == "gu");
        var inscript = _bundle.GetLayout(gujarati.Layouts.First(l => l.Name == "InScript"));
        var layout = ShreeKeyboardFile.ToLayout(ShreeKeyboardFile.Decode(Gujarati(), Table("GUJ")), "Mine", inscript);

        Assert.Equal("ક", layout.Find('K')?.Base);
        Assert.Equal("ખ", layout.Find('K')?.Shift);
        Assert.Equal("ૠ", layout.MatchSequence(EscapeLayer.Mark + "v"));
        Assert.Equal("ૠ", layout.EscapeFor("v"));
        Assert.Equal(inscript.Symbols.Count, layout.Symbols.Count);
    }

    [Fact]
    public void AFileOfTheWrongSizeIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => ShreeKeyboardFile.Decode(new byte[100], Table("GUJ")));
    }

    [Fact]
    public void ImportingPutsTheKeyboardInTheListOfItsLanguage()
    {
        string file = Path.Combine(_dir, "MYKBD.GUJ");
        File.WriteAllBytes(file, Gujarati());
        var store = new UserKeyboards(Path.Combine(_dir, "store"));
        var vm = new KeyboardEditorViewModel(_bundle, store, NullLog.Instance)
        {
            SelectedLanguage = _bundle.Languages.First(l => l.Id == "hi"),   // the wrong one: the file says Gujarati
        };

        Assert.True(vm.Import(file), vm.Status);

        Assert.Equal("gu", vm.SelectedLanguage!.Id);
        var reference = vm.SelectedLanguage.Layouts.First(l => l.Name == "MYKBD");
        var saved = new ConfigLoader(_dir).LoadLayout(reference);
        Assert.Equal("ક", saved.Find('K')?.Base);
        Assert.Equal("ૠ", saved.MatchSequence(EscapeLayer.Mark + "v"));
        Assert.Contains("MYKBD", vm.Status);
    }

    /// <summary>A code no table knows, for the tests below: one this script's table has no text for.</summary>
    private static byte Unknown(ShreeCodeTable table)
    {
        for (int c = 0x20; c < 0xFF; c++)
            if (c is not (>= 0x90 and <= 0x9F) and not (>= 0xB0 and <= 0xCF) && table.Text(c) is null) return (byte)c;
        throw new InvalidOperationException("every code is known");
    }

    /// <summary>A piece this version cannot read adds nothing: Shree types ం and an unread piece as ం.</summary>
    [Fact]
    public void AnUnreadablePieceBesideALetterIsLeftOut()
    {
        var table = Table("GUJ");
        Assert.Equal("ક", ShreeKeyboardFile.Join(new byte[] { 0x73, Unknown(table) }, table));
    }

    /// <summary>...but when only punctuation is left, the conjunct is lost rather than typing "#$".</summary>
    [Fact]
    public void AnUnreadablePieceWithOnlyPunctuationLeftLosesTheConjunct()
    {
        var table = Table("GUJ");
        byte hash = (byte)Convert.ToInt32(table.Codes.First(kv => kv.Value == "(").Key, 16);
        Assert.Null(ShreeKeyboardFile.Join(new[] { hash, Unknown(table) }, table));
    }

    /// <summary>A key whose conjunct has nothing readable in it is lost - and the typist is told.</summary>
    [Fact]
    public void AConjunctWithNothingReadableIsReported()
    {
        var table = Table("GUJ");
        var f = Blank();
        byte x = Unknown(table);
        f['j'] = 0xB2;
        f[166 + 2 * 5 + 1] = x;
        f[166 + 2 * 5 + 2] = x;
        var read = ShreeKeyboardFile.Decode(f, table);
        Assert.Contains('j', read.Unreadable);
    }

    /// <summary>
    /// Conjuncts made with Link keep the link. Shree's composer shows it as "Lk" in
    /// a longer answer, and reading that as a Latin carrier made every Odia and
    /// Tamil Link conjunct lose its virama (ଜ୍ଞ came in as ଜଞ).
    /// </summary>
    [Theory]
    [InlineData("ORI", "\u0B1C", "\u0B1E", "\u0B1C\u0B4D\u0B1E")]   // ଜ୍ଞ
    [InlineData("TAM", "\u0BB7", "\u0BB0", "\u0BB7\u0BCD\u0BB0")]   // ஷ்ர
    [InlineData("GUJ", "\u0A95", "\u0AB7", "\u0A95\u0ACD\u0AB7")]   // ક્ષ
    public void ALinkConjunctKeepsItsVirama(string script, string first, string second, string expected)
    {
        var table = Table(script);
        byte Code(string text) => (byte)Convert.ToInt32(table.Codes.First(kv => kv.Value == text).Key, 16);
        string virama = expected.Substring(1, 1);
        Assert.Equal(expected, ShreeKeyboardFile.Join(new[] { Code(first), Code(virama), Code(second) }, table));
    }

    /// <summary>Shree draws the Odia wa as ଓ, the link and ବ; it is one letter, ୱ.</summary>
    [Fact]
    public void TheOdiaWaIsOneLetter()
    {
        var table = Table("ORI");
        byte Code(string text) => (byte)Convert.ToInt32(table.Codes.First(kv => kv.Value == text).Key, 16);
        Assert.Equal("\u0B71", ShreeKeyboardFile.Join(new[] { Code("\u0B13"), Code("\u0B4D"), Code("\u0B2C") }, table));
    }

    /// <summary>Importing a file twice, or over a keyboard designed here, never writes over the one already there.</summary>
    [Fact]
    public void ImportingAgainKeepsTheKeyboardAlreadyThere()
    {
        string file = Path.Combine(_dir, "MYKBD.GUJ");
        File.WriteAllBytes(file, Gujarati());
        var vm = new KeyboardEditorViewModel(_bundle, new UserKeyboards(Path.Combine(_dir, "store")), NullLog.Instance);

        Assert.True(vm.Import(file));
        Assert.True(vm.Import(file));

        var names = vm.SelectedLanguage!.Layouts.Select(l => l.Name).ToList();
        Assert.Contains("MYKBD", names);
        Assert.Contains("MYKBD (2)", names);
    }

    [Fact]
    public void AFileThatIsNotAShreeKeyboardSaysSo()
    {
        string file = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(file, "hello");
        var vm = new KeyboardEditorViewModel(_bundle, new UserKeyboards(Path.Combine(_dir, "store")), NullLog.Instance);
        Assert.False(vm.Import(file));
        Assert.Contains(".GUJ", vm.Status);
    }

    // ---- a client's own keyboard (BPMK1, from Shree's Keyboard Generator) ----
    // It put the ru and ruu ligatures on Shift+5 and 2. Shree types those codes
    // through its legacy-font composer only, so the table - read from the Unicode
    // composer - had no text for them: Shift+5 came in empty and typed the 5 key's
    // letter, and 2 typed an English 2.

    /// <summary>E9 and EA are Shree's ru and ruu ligatures, 7E its other ruu (its legacy glyphs E3C5, E3C6, E3CA).</summary>
    [Fact]
    public void TheRuLigaturesComeAcross()
    {
        var f = Blank();
        f['%'] = 0xE9;
        f['2'] = 0xEA;
        f[0x80 + ShreeKeyboardFile.EscapeKeys.IndexOf('v')] = 0x7E;
        var read = ShreeKeyboardFile.Decode(f, Table("GUJ"));
        Assert.Equal("રુ", read.Keys['%']);
        Assert.Equal("રૂ\uFE00", read.Keys['2']);   // the new shape: a glyph of its own in Shree fonts
        Assert.Equal("રૂ", read.Escape['v']);
        Assert.Empty(read.Unknown);
    }

    /// <summary>A code the table cannot read is named, not dropped without a word.</summary>
    [Fact]
    public void ACodeTheTableCannotReadIsReported()
    {
        var table = Table("GUJ");
        var f = Blank();
        f['%'] = Unknown(table);
        f[0x80 + ShreeKeyboardFile.EscapeKeys.IndexOf('3')] = Unknown(table);
        var read = ShreeKeyboardFile.Decode(f, table);
        Assert.Contains('%', read.Unknown);
        Assert.Contains('3', read.UnknownOnEscape);
        Assert.False(read.Keys.ContainsKey('%'));
    }

    /// <summary>A layer the file leaves empty stays empty: Shift+5 must not borrow the letter on 5.</summary>
    [Fact]
    public void ALayerTheFileLeavesEmptyTypesNothing()
    {
        var f = Blank();
        f['5'] = 0x77;                                   // પ, and nothing on Shift+5
        var layout = ShreeKeyboardFile.ToLayout(ShreeKeyboardFile.Decode(f, Table("GUJ")), "Mine");
        Assert.Equal("પ", layout.Find('5')?.Base);
        Assert.Equal("", layout.Find('5')?.Shift);
    }

    [Fact]
    public void TheImportNamesTheKeysItCouldNotRead()
    {
        var table = Table("GUJ");
        var f = Gujarati();
        f['%'] = Unknown(table);
        string file = Path.Combine(_dir, "MINE.GUJ");
        File.WriteAllBytes(file, f);
        var vm = new KeyboardEditorViewModel(_bundle, new UserKeyboards(Path.Combine(_dir, "store")), NullLog.Instance)
        {
            SelectedLanguage = _bundle.Languages.First(l => l.Id == "gu"),
        };
        Assert.True(vm.Import(file), vm.Status);
        Assert.Contains("Shift+5", vm.Status);
    }

    /// <summary>Shree's own files write 0x00 where their charts show nothing; it is not a character we failed to read.</summary>
    [Fact]
    public void CodeZeroIsNothingNotAnUnreadCharacter()
    {
        var f = Blank();
        f['$'] = 0x00;
        var read = ShreeKeyboardFile.Decode(f, Table("GUJ"));
        Assert.Empty(read.Unknown);
        Assert.False(read.Keys.ContainsKey('$'));
    }

    /// <summary>
    /// A key the file gives only a character this version cannot read types nothing,
    /// as the summary says - it used to be left out and type its English character.
    /// </summary>
    [Fact]
    public void AKeyWithOnlyAnUnreadCharacterTypesNothing()
    {
        var table = Table("GUJ");
        var f = Blank();
        f['`'] = Unknown(table);
        var layout = ShreeKeyboardFile.ToLayout(ShreeKeyboardFile.Decode(f, table), "Mine");
        var key = layout.Find(192);
        Assert.NotNull(key);
        Assert.Equal("", key!.Base);
        Assert.Equal("", key.Shift);
    }
}
