using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Every code point a Unicode -> Shree table can write must be in the (3,1) cmap of every
/// font of its family: a code the font does not map is drawn in another font, or not at
/// all. (A Bangla table once wrote ৱ as U+017E, Windows-1252's ž for byte 9E, while every
/// Bangla font maps U+009E.) Reads the fonts installed in C:\Windows\Fonts, matched by the
/// family name in their name table; a family with no font installed is not checked here.
/// </summary>
public class ShreeFontCmapTests
{
    private static readonly Lazy<AssetBundle> Assets = new(() => new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
    private static readonly Lazy<List<Font>> Installed = new(ScanFonts);

    public static readonly TheoryData<string, string, string, string> Families = new()
    {
        { "shruti", "shree-guj7", "^(SHREE-GUJ7-|Shree-Guj7-)", "shree-guj7-truth.json" },
        { "shruti", "shree-guj456", "^SHREE-GUJ-", "shree-guj7-truth.json" },
        { "mangal", "shree-dev7", "^(SHREE-DEV7-|Shree-Dev7-)", "shree-dev7-truth.json" },
        { "mangal", "shree-dev456", "^SHREE-DEV-", "shree-dev7-truth.json" },
        { "mangal", "shree-san7", "^SHREE-SAN7-", "shree-san7-truth.json" },
        { "kalinga", "shree-ori7", "^SHREE-ORI7-", "shree-ori7-truth.json" },
        { "kalinga", "shree-ori7", "^SHREE-ORI-", "shree-ori7-truth.json" },
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodeATableWritesIsInEveryFontOfItsFamily(string unicodeFont, string encoding, string family, string fixture)
    {
        var fonts = Installed.Value.Where(f => Regex.IsMatch(f.Family, family)).ToList();
        if (fonts.Count == 0) return;   // none of this family installed on this PC

        var table = Assets.Value.FindMapping(unicodeFont, encoding)!;
        var passed = PassedThrough(table);
        var written = Written(table, Assets.Value.CreateConverter(unicodeFont, encoding)!, fixture, passed);
        var missing = new List<string>();
        foreach (var f in fonts)
        {
            foreach (var (c, why) in written)
                if (!f.Codes.Contains(c)) missing.Add($"{Path.GetFileName(f.File)} ({f.Family}) has no U+{c:X4}, which the table writes for {why}");
            // P3b: a character the table passes through as itself must be no code of the fonts, or it would print their glyph.
            foreach (char c in passed)
                if (f.Codes.Contains(c)) missing.Add($"{Path.GetFileName(f.File)} ({f.Family}) maps U+{(int)c:X4}, which the table passes through as itself");
        }
        Assert.True(missing.Count == 0, $"{encoding}: {missing.Count} missing:\n" + string.Join("\n", missing.Take(25)));
    }

    // What the non-Indic policy (ShreeNonIndicTests) knows the fonts map must be all they map:
    // a code beyond ASCII it did not know of could print a letter for text that is not Indic.
    [Theory]
    [MemberData(nameof(Families))]
    public void TheFontsMapNoCodeTheTablesDoNotKnow(string unicodeFont, string encoding, string family, string fixture)
    {
        _ = unicodeFont; _ = fixture;
        var known = ShreeNonIndicTests.FontCodes(encoding);
        var unknown = new List<string>();
        foreach (var f in Installed.Value.Where(f => Regex.IsMatch(f.Family, family)))
            foreach (int c in f.Codes.Where(c => c > 0x7E && !known.Contains((char)c)))
                unknown.Add($"{Path.GetFileName(f.File)} ({f.Family}) maps U+{c:X4}");
        Assert.True(unknown.Count == 0, $"{encoding}: {unknown.Count}:\n" + string.Join("\n", unknown.Take(25)));
    }

    /// <summary>
    /// The characters the table passes through as themselves and reports (P3b: a pair marked unmapped that writes
    /// its own character - ₹, ૱, the vocalic-l letters and signs): no code of the fonts draws them, so Word draws
    /// them in a fallback font. Not a code of the fonts themselves (U+00FF and below): · written as B7, its own code, is a
    /// stand-in the fonts draw (a large bullet), reported (round 2), as FontConverter tells kept from left out.
    /// </summary>
    private static HashSet<char> PassedThrough(MappingTable table) =>
        table.Pairs.Where(p => p.Unmapped).SelectMany(p => p.From.Where(c => c > (char)0xFF && p.To.Contains(c))).ToHashSet();

    /// <summary>Every code point the table can write: in its pairs, in its post rules' replacements, and over its truth fixture - but the ones it passes through.</summary>
    private static Dictionary<int, string> Written(MappingTable table, FontConverter conv, string fixture, HashSet<char> passed)
    {
        var codes = new Dictionary<int, string>();
        // U+FDD0-FDEF mark places for the rules, which take them out: never written.
        void Note(char c, string why) { if ((c < '\uFDD0' || c > '\uFDEF') && !passed.Contains(c)) codes.TryAdd(c, why); }
        foreach (var p in table.Pairs) foreach (char c in p.To) Note(c, "the pair " + p.From);
        foreach (var r in table.PostRules)
            for (int i = 0; i < r.Replace.Length; i++)
            {
                if (r.Replace[i] == '$' && i + 1 < r.Replace.Length && (char.IsDigit(r.Replace[i + 1]) || r.Replace[i + 1] == '$'))
                {
                    if (r.Replace[++i] == '$') Note('$', "a post rule");
                    while (i + 1 < r.Replace.Length && char.IsDigit(r.Replace[i + 1])) i++;
                    continue;
                }
                Note(r.Replace[i], "a post rule");
            }
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", fixture), Encoding.UTF8));
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            string u = row[0].GetString()!;
            foreach (char c in conv.ConvertText(u)) Note(c, u);
        }
        // Whitespace passes through every table; a space is in every font.
        codes.Remove(' ');
        return codes;
    }

    private sealed record Font(string File, string Family, HashSet<int> Codes);

    /// <summary>The Shree fonts of C:\Windows\Fonts (S*.TTF, W*.TTF), each with its family name and (3,1) cmap.</summary>
    private static List<Font> ScanFonts()
    {
        var list = new List<Font>();
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        if (!Directory.Exists(dir)) return list;
        foreach (var path in Directory.EnumerateFiles(dir, "*.ttf").Where(p => Path.GetFileName(p).ToUpperInvariant() is var n && (n.StartsWith('S') || n.StartsWith('W'))))
        {
            try
            {
                using var fs = File.OpenRead(path);
                var tables = TableDirectory(fs);
                if (!tables.TryGetValue("name", out var name) || !tables.TryGetValue("cmap", out var cmap)) continue;
                string family = FamilyName(Read(fs, name.Off, name.Len));
                if (!family.StartsWith("SHREE-", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new Font(path, family, Unicode(Read(fs, cmap.Off, cmap.Len))));
            }
            catch (IOException) { }
            catch (ArgumentException) { }
            catch (IndexOutOfRangeException) { }
        }
        return list;
    }

    private static byte[] Read(FileStream fs, long off, long len)
    {
        var b = new byte[len];
        fs.Position = off;
        fs.ReadExactly(b);
        return b;
    }

    private static int U16(byte[] d, long o) => (d[o] << 8) | d[o + 1];
    private static long U32(byte[] d, long o) => ((long)d[o] << 24) | ((long)d[o + 1] << 16) | ((long)d[o + 2] << 8) | d[o + 3];

    private static Dictionary<string, (long Off, long Len)> TableDirectory(FileStream fs)
    {
        var head = Read(fs, 0, 12);
        int n = U16(head, 4);
        var recs = Read(fs, 12, n * 16);
        var t = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        for (int i = 0; i < n; i++) t[Encoding.ASCII.GetString(recs, i * 16, 4)] = (U32(recs, i * 16 + 8), U32(recs, i * 16 + 12));
        return t;
    }

    /// <summary>Name id 1 (family), Windows platform.</summary>
    private static string FamilyName(byte[] d)
    {
        int count = U16(d, 2), strings = U16(d, 4);
        for (int i = 0; i < count; i++)
        {
            int r = 6 + i * 12;
            if (U16(d, r) != 3 || U16(d, r + 6) != 1) continue;
            return Encoding.BigEndianUnicode.GetString(d, strings + U16(d, r + 10), U16(d, r + 8)).Trim();
        }
        return "";
    }

    /// <summary>The code points the (3,1) format 4 subtable maps to a glyph.</summary>
    private static HashSet<int> Unicode(byte[] d)
    {
        var codes = new HashSet<int>();
        int n = U16(d, 2);
        for (int i = 0; i < n; i++)
        {
            int rec = 4 + i * 8;
            if (U16(d, rec) != 3 || U16(d, rec + 2) != 1) continue;
            long sub = U32(d, rec + 4);
            if (U16(d, sub) != 4) continue;
            int segX2 = U16(d, sub + 6), seg = segX2 / 2;
            long ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < seg; s++)
            {
                int end = U16(d, ends + s * 2), start = U16(d, starts + s * 2), delta = (short)U16(d, deltas + s * 2), ro = U16(d, ranges + s * 2);
                if (start == 0xFFFF) continue;
                for (int c = start; c <= end; c++)
                {
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        long at = ranges + s * 2 + ro + (c - start) * 2;
                        if (at + 1 >= d.Length) continue;
                        g = U16(d, at);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) codes.Add(c);
                }
            }
            break;
        }
        return codes;
    }
}
