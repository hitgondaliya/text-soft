using System.IO;
using System.Linq;
using System.Text.Json;

namespace GlyphJoin;

// Batch — run the glyph join over the whole installed Shree font set.
//
// Shree names every font by a 4-digit DESIGN number and a series suffix that
// encodes the byte layout:  S0750810.TTF = design 0750, series "810" (X-series),
// S0750892.TTF = same design, series "892" (7-series). The Unicode counterpart of
// the same design is SHREE0750 (installed under its own family name).
//
// Step 1: pair each legacy font with the Unicode font of the same design ->
//         byte -> Unicode table for that (design, series).
// Step 2: every legacy font that shares a codepoint set with a solved font gets
//         the same table, so one solved design covers its whole encoding family.
public static class Batch
{
    public sealed record FontRef(string Path, string Family, string Design, string Series, string LayoutHash, int CodeCount);

    public static int Run(string legacyDir, string uniDir, string outDir, double tol, int limit)
    {
        Directory.CreateDirectory(outDir);

        var legacy = Index(legacyDir, legacyOnly: true);
        var unicode = Index(uniDir, legacyOnly: false).Where(f => f.CodeCount > 200).ToList();

        Console.WriteLine("legacy Shree fonts: " + legacy.Count + " in " + legacy.Select(f => f.LayoutHash).Distinct().Count() + " distinct layouts");
        Console.WriteLine("unicode candidates: " + unicode.Count);

        // Shree ships more than one Unicode face per design number, and they are not
        // equally close to the legacy drawing: the *_OTF_* cuts match far worse than
        // the same-numbered SHREE#### ones (GUJX-0750 maps 133 through the OTF twin
        // against 317 through SHREE0750). Keep every candidate and pick by result.
        var uniByDesign = new Dictionary<string, List<FontRef>>(StringComparer.Ordinal);
        foreach (var u in unicode)
        {
            if (!uniByDesign.TryGetValue(u.Design, out var l)) uniByDesign[u.Design] = l = new List<FontRef>();
            l.Add(u);
        }

        // one representative legacy font per (layout hash) that has a Unicode partner
        var solved = new Dictionary<string, (FontRef leg, FontRef uni, SortedDictionary<string, string> table)>(StringComparer.Ordinal);
        int done = 0;
        foreach (var group in legacy.GroupBy(f => f.LayoutHash))
        {
            if (solved.ContainsKey(group.Key)) continue;

            FontRef? bestLeg = null, bestUni = null;
            SortedDictionary<string, string>? bestTable = null;
            string bestStats = "";
            foreach (var leg in group)
            {
                if (!uniByDesign.TryGetValue(leg.Design, out var cands)) continue;
                foreach (var uni in cands)
                {
                    var t = Join(leg, uni, tol, out string s);
                    if (bestTable is null || t.Count > bestTable.Count)
                    { bestLeg = leg; bestUni = uni; bestTable = t; bestStats = s; }
                }
                if (bestTable is not null) break;   // one legacy font per group is enough
            }
            if (bestTable is null) continue;

            Console.WriteLine(group.Key + "  " + bestLeg!.Family + " x " + bestUni!.Family + "  " + bestStats);
            solved[group.Key] = (bestLeg, bestUni, bestTable);
            if (++done >= limit && limit > 0) break;
        }

        // write one table per layout, and an index saying which families use it
        var index = new List<object>();
        foreach (var kv in solved)
        {
            string name = "shree-" + kv.Value.leg.Series + "-" + kv.Key.ToLowerInvariant();
            File.WriteAllText(Path.Combine(outDir, name + ".json"),
                JsonSerializer.Serialize(kv.Value.table, new JsonSerializerOptions { WriteIndented = true }));
            index.Add(new
            {
                table = name,
                layoutHash = kv.Key,
                derivedFrom = new { legacy = kv.Value.leg.Family, unicode = kv.Value.uni.Family },
                entries = kv.Value.table.Count,
                fonts = legacy.Where(f => f.LayoutHash == kv.Key).Select(f => f.Family).OrderBy(x => x).ToArray(),
            });
        }
        File.WriteAllText(Path.Combine(outDir, "index.json"),
            JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));

        int covered = legacy.Count(f => solved.ContainsKey(f.LayoutHash));
        Console.WriteLine();
        Console.WriteLine("solved layouts: " + solved.Count + "/" + legacy.Select(f => f.LayoutHash).Distinct().Count());
        Console.WriteLine("fonts covered:  " + covered + "/" + legacy.Count);
        Console.WriteLine("wrote " + outDir);
        return 0;
    }

    static SortedDictionary<string, string> Join(FontRef leg, FontRef uni, double tol, out string stats)
    {
        var a = Sfnt.Load(leg.Path);
        var b = Sfnt.Load(uni.Path);
        var mapA = a.BestCmap();
        var mapB = b.BestCmap();
        var gidToCode = new Dictionary<int, int>();
        foreach (var kv in mapB.OrderBy(k => k.Key)) if (!gidToCode.ContainsKey(kv.Value)) gidToCode[kv.Value] = kv.Key;

        var gidMap = Raster.Match(new Raster(leg.Path), new Raster(uni.Path), tol, out int amb);
        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int miss = 0;
        foreach (var kv in mapA.OrderBy(k => k.Key))
        {
            if (gidMap.TryGetValue(kv.Value, out int gb) && gidToCode.TryGetValue(gb, out int cp))
                table[kv.Key.ToString("X4")] = cp.ToString("X4");
            else miss++;
        }
        stats = "mapped=" + table.Count + " miss=" + miss + " ambiguous=" + amb;
        return table;
    }

    static List<FontRef> Index(string dir, bool legacyOnly)
    {
        var res = new List<FontRef>();
        foreach (var f in Directory.EnumerateFiles(dir).Where(p =>
                     p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) == false && p.EndsWith("._tf", StringComparison.OrdinalIgnoreCase)))
        {
            Sfnt s;
            try { s = Sfnt.Load(f); } catch { continue; }
            string fam = s.FamilyName;
            string file = Path.GetFileNameWithoutExtension(f);
            bool isShree = fam.Contains("SHREE", StringComparison.OrdinalIgnoreCase)
                        || file.StartsWith("SHREE", StringComparison.OrdinalIgnoreCase)
                        || (file.Length >= 5 && file[0] is 'S' or 's' && file.Skip(1).Take(4).All(char.IsDigit));
            if (legacyOnly && !isShree) continue;

            var map = s.BestCmap();
            if (map.Count == 0) continue;
            bool isLegacy = map.Keys.Count(c => c is >= 0x0900 and <= 0x0DFF) < 10;
            if (legacyOnly && !isLegacy) continue;
            if (!legacyOnly && (isLegacy || !isShree)) continue;

            string digits = new string(file.Where(char.IsDigit).ToArray());
            string design = digits.Length >= 4 ? digits.Substring(0, 4) : digits.PadLeft(4, '0');
            string series = digits.Length >= 7 ? digits.Substring(4, 3) : "uni";
            res.Add(new FontRef(f, fam, design, series, Sfnt.LayoutHash(map), map.Count));
        }
        return res;
    }
}
