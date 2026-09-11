using System.IO;
using System.Linq;
using System.Text.Json;

namespace GlyphJoin;

// Chain — solve a second legacy encoding through one that is already solved.
//
// Shree ships the same typeface in several byte layouts (S0850810 X-series,
// S0850890 7-series, S08500F0 4/5/6-series). Matching an unsolved font's glyphs
// against a SOLVED font of the same design is far more reliable than matching it
// against the Unicode font, because the two legacy files are the same drawing:
// the shapes are identical rather than a generation apart.
public static class Chain
{
    public static int Run(string fontFrom, string fontTo, string solvedTable, string outJson, double tol)
    {
        var a = Sfnt.Load(fontFrom);
        var b = Sfnt.Load(fontTo);
        var solved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(solvedTable))!;

        var mapA = a.BestCmap();
        var mapB = b.BestCmap();
        var gidToCodeB = new Dictionary<int, int>();
        foreach (var kv in mapB.OrderBy(k => k.Key)) if (!gidToCodeB.ContainsKey(kv.Value)) gidToCodeB[kv.Value] = kv.Key;

        var gidMap = Raster.Match(new Raster(fontFrom), new Raster(fontTo), tol, out int ambiguous);

        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int mapped = 0, noShape = 0, noCode = 0, notInSolved = 0;
        foreach (var kv in mapA.OrderBy(k => k.Key))
        {
            if (!gidMap.TryGetValue(kv.Value, out int gb)) { noShape++; continue; }
            if (!gidToCodeB.TryGetValue(gb, out int codeB)) { noCode++; continue; }
            if (!solved.TryGetValue(codeB.ToString("X4"), out var seq)) { notInSolved++; continue; }
            table[kv.Key.ToString("X4")] = seq;
            mapped++;
        }

        Console.WriteLine("source codes=" + mapA.Count + " mapped=" + mapped
            + " no-shape-match=" + noShape + " target-code-unknown=" + noCode
            + " not-in-solved-table=" + notInSolved + " ambiguous=" + ambiguous);
        File.WriteAllText(outJson, JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("wrote " + outJson + " (" + table.Count + " entries)");
        return 0;
    }
}
