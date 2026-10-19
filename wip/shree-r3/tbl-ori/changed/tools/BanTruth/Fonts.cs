using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The installed faces of each family and what their cmaps hold. A code a table writes is
/// only a glyph if the code point is in the cmap of every face of the family: Word and the
/// app's preview look it up there (the preview draws by glyph index and drops what is not
/// there; Word falls back to another font). Every SHREE-BAN face holds ৱ at U+009E and has
/// no U+017E, which is why 9E is written U+009E (Common.Wa).
/// </summary>
public static class Fonts
{
    public static string FontDir => Environment.GetEnvironmentVariable("BAN_FONTDIR") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public static List<string> Faces(Fam f) => Directory.Exists(FontDir)
        ? Directory.EnumerateFiles(FontDir).Where(p => Regex.IsMatch(Path.GetFileName(p), f.FilePattern, RegexOptions.IgnoreCase)).OrderBy(p => p).ToList()
        : new List<string>();

    /// <summary>The (3,1) cmap of a TrueType file: code point -> glyph id (format 4).</summary>
    public static Dictionary<int, int> Cmap(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int U16(int o) => (b[o] << 8) | b[o + 1];
        int S16(int o) => (short)((b[o] << 8) | b[o + 1]);
        int U32(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        int cmap = -1;
        for (int i = 0, n = U16(4); i < n; i++)
            if (Encoding.ASCII.GetString(b, 12 + 16 * i, 4) == "cmap") cmap = U32(12 + 16 * i + 8);
        var map = new Dictionary<int, int>();
        if (cmap < 0) return map;
        for (int i = 0, n = U16(cmap + 2); i < n; i++)
        {
            int r = cmap + 4 + 8 * i;
            if (U16(r) != 3 || U16(r + 2) != 1) continue;
            int st = cmap + U32(r + 4);
            if (U16(st) != 4) continue;
            int segX2 = U16(st + 6), ends = st + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < segX2 / 2; s++)
            {
                int end = U16(ends + 2 * s), start = U16(starts + 2 * s), delta = S16(deltas + 2 * s), ro = U16(ranges + 2 * s);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int g = ro == 0 ? (c + delta) & 0xFFFF : U16(ranges + 2 * s + ro + 2 * (c - start));
                    if (ro != 0 && g != 0) g = (g + delta) & 0xFFFF;
                    if (g != 0) map[c] = g;
                }
            }
        }
        return map;
    }

    static readonly Dictionary<string, SortedSet<char>> _codes = new();

    /// <summary>
    /// The code points every face of the family maps (its installed faces; the test fixture
    /// Data/shree-ban-cmaps.json when none is installed here): what the non-Indic policy
    /// (tools/Shared/ShreeNonIndic.cs) must keep the tables from writing a letter for.
    /// </summary>
    public static SortedSet<char> Codes(Fam f)
    {
        if (_codes.TryGetValue(f.Id, out var have)) return have;
        HashSet<int>? common = null;
        foreach (var face in Faces(f))
        {
            var keys = Cmap(face).Keys.ToHashSet();
            if (common is null) common = keys; else common.IntersectWith(keys);
        }
        if (common is null)
        {
            string fixture = Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-ban-cmaps.json");
            var doc = JsonNode.Parse(File.ReadAllText(fixture))!;
            common = ((string)doc[f.Id]!["codePoints"]!).Split(' ').Select(h => Convert.ToInt32(h, 16)).ToHashSet();
        }
        return _codes[f.Id] = new SortedSet<char>(common.Where(c => c <= 0xFFFF).Select(c => (char)c));
    }

    /// <summary>Every character a forward table can write: its pairs, its post rules, and what it writes for the probe text.</summary>
    public static SortedSet<int> Written(MappingTable table, FontConverter fwd, IEnumerable<string> probe)
    {
        var set = new SortedSet<int>();
        void Add(string s) { foreach (char c in s) if (c != Rules.Keep[0]) set.Add(c); }
        foreach (var p in table.Pairs) Add(p.To);
        foreach (var r in table.PostRules) Add(Regex.Replace(r.Replace, @"\$\d|\$\$", ""));
        foreach (var u in probe)
        {
            var r = fwd.Convert(u);
            // what passes through unmapped is reported to the user, not written by the table
            Add(new string(r.Text.Where(c => !r.UnmappedCharacters.Contains(c.ToString())).ToArray()));
        }
        return set;
    }

    /// <summary>Codes written that a face of the family has no glyph for, per face (empty when every face has them all).</summary>
    public static List<string> Check(Fam f, SortedSet<int> written)
    {
        var bad = new List<string>();
        var faces = Faces(f);
        if (faces.Count == 0) { bad.Add($"{f.Id}: no installed face matches {f.FilePattern} in {FontDir}"); return bad; }
        foreach (var face in faces)
        {
            var cmap = Cmap(face);
            var missing = written.Where(c => !cmap.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                bad.Add($"{Path.GetFileName(face)}: no glyph for {string.Join(" ", missing.Select(c => $"U+{c:X4} ({Hex(((char)c).ToString())})"))}");
        }
        return bad;
    }

    /// <summary>The test fixture: each family's faces and the code points every one of them maps.</summary>
    public static void WriteFixture(string path)
    {
        var root = new JsonObject
        {
            ["about"] = "The (3,1) cmap of every installed Shree-Lipi Bangla / Assamese face, per font family: the code points " +
                        "every face of the family maps (they are the same in each). A code a table writes is a glyph only if it is " +
                        "here: Word and the app's preview look it up in the cmap. Written by tools/BanTruth (bantruth cmaps) from " +
                        "C:\\Windows\\Fonts. Note U+009E (ৱ in the Bangla faces) and no U+017E anywhere.",
        };
        foreach (var f in Fam.All)
        {
            var faces = Faces(f);
            if (faces.Count == 0) throw new InvalidOperationException($"{f.Id}: no installed face");
            HashSet<int>? common = null;
            foreach (var face in faces)
            {
                var keys = Cmap(face).Keys.ToHashSet();
                if (common is null) common = keys; else common.IntersectWith(keys);
            }
            root[f.Id] = new JsonObject
            {
                ["faces"] = new JsonArray(faces.Select(p => (JsonNode)Path.GetFileName(p)).ToArray()),
                ["codePoints"] = string.Join(" ", common!.OrderBy(c => c).Select(c => c.ToString("X4"))),
            };
        }
        File.WriteAllText(path, root.ToJsonString(Writer) + "\n", new UTF8Encoding(false));
        Console.WriteLine($"wrote {path}");
    }

    /// <summary>
    /// Characters a reader could type that the table leaves unmapped (it passes them through and
    /// the converter reports them): punctuation and symbols of ASCII and beyond, and the Bengali block.
    /// This is the family's line in assets/config/encoding-limits.json.
    /// </summary>
    public static List<string> Limits(FontConverter fwd)
    {
        var probe = Enumerable.Range(0x21, 0x5E).Select(c => (char)c).Where(c => !char.IsLetterOrDigit(c))
            .Concat("“”‘’–—…•×÷₹°").Concat(Enumerable.Range(0x0980, 0x80).Select(c => (char)c)
                .Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.OtherNotAssigned));
        return probe.Where(c => fwd.Convert(c.ToString()).HasUnmapped).Select(c => ((int)c).ToString("X4")).OrderBy(x => x).ToList();
    }

    public static void WriteLimits(string path, Dictionary<string, List<string>> byFamily)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        foreach (var (id, list) in byFamily) doc[id] = new JsonArray(list.Select(x => (JsonNode)x).ToArray());
        File.WriteAllText(path, doc.ToJsonString(Writer));
        Console.WriteLine($"wrote {string.Join(", ", byFamily.Keys)} into {path}");
    }
}
