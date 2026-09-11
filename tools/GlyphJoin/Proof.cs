using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlyphJoin;

// Proof — render the same text twice: once as real Unicode in the Unicode font,
// once as legacy codes in the legacy font after running it through a table that
// GlyphJoin derived. If the two lines look identical, the table is right.
public static class Proof
{
    public static int Run(string legacyFont, string uniFont, string mapJson, string text, string outPng)
    {
        // accepts either a raw GlyphJoin table (hex -> hex) or an emitted
        // unicode-to-<enc> MappingTable, whose preRules we then actually apply
        var rev = new Dictionary<string, string>(StringComparer.Ordinal);
        var preRules = new List<(string pattern, string replace)>();
        string text0 = File.ReadAllText(mapJson);
        using (var doc = JsonDocument.Parse(text0))
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("pairs", out var pairsEl))
            {
                foreach (var p in pairsEl.EnumerateArray())
                {
                    string from = p.GetProperty("from").GetString()!;
                    string to = p.GetProperty("to").GetString()!;
                    if (!rev.ContainsKey(from)) rev[from] = to;
                }
                if (root.TryGetProperty("preRules", out var pr))
                    foreach (var r in pr.EnumerateArray())
                        preRules.Add((r.GetProperty("pattern").GetString()!, r.GetProperty("replace").GetString()!));
            }
            else
            {
                foreach (var kv in root.EnumerateObject())
                {
                    var sb = new StringBuilder();
                    foreach (var cp in kv.Value.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        sb.Append(char.ConvertFromUtf32(Convert.ToInt32(cp, 16)));
                    string u = sb.ToString();
                    if (!rev.ContainsKey(u)) rev[u] = char.ConvertFromUtf32(Convert.ToInt32(kv.Name, 16));
                }
            }
        }
        string reordered = text;
        foreach (var (pattern, replace) in preRules)
            reordered = System.Text.RegularExpressions.Regex.Replace(reordered, pattern, replace);
        int maxLen = rev.Keys.Max(k => k.Length);

        var outSb = new StringBuilder();
        int i = 0, hit = 0, miss = 0;
        while (i < reordered.Length)
        {
            bool matched = false;
            for (int len = Math.Min(maxLen, reordered.Length - i); len >= 1; len--)
            {
                string probe = reordered.Substring(i, len);
                if (rev.TryGetValue(probe, out string? code))
                {
                    outSb.Append(code);
                    i += len; matched = true; hit++;
                    break;
                }
            }
            if (!matched) { outSb.Append(reordered[i]); i++; miss++; }
        }
        string legacy = outSb.ToString();
        Console.WriteLine("units mapped=" + hit + " passthrough=" + miss);

        var uniFam = LoadFamily(uniFont);
        var legFam = LoadFamily(legacyFont);

        var dv = new DrawingVisual();
        const int W = 1100, H = 260;
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
            Draw(dc, "Unicode text in " + Path.GetFileName(uniFont), 20, 30, 14, new FontFamily("Segoe UI"), Brushes.Gray);
            Draw(dc, text, 20, 90, 44, uniFam, Brushes.Black);
            Draw(dc, "same text via GlyphJoin table in " + Path.GetFileName(legacyFont), 20, 150, 14, new FontFamily("Segoe UI"), Brushes.Gray);
            Draw(dc, legacy, 20, 210, 44, legFam, Brushes.Black);
        }
        var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(outPng);
        enc.Save(fs);
        Console.WriteLine("wrote " + outPng);
        return 0;
    }

    static FontFamily LoadFamily(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var uri = new Uri(dir.EndsWith("\\") ? dir : dir + "\\");
        var gt = new GlyphTypeface(new Uri(Path.GetFullPath(path)));
        string name = gt.FamilyNames.Values.FirstOrDefault() ?? "";
        return new FontFamily(uri, "./#" + name);
    }

    static void Draw(DrawingContext dc, string s, double x, double y, double size, FontFamily fam, Brush brush)
    {
        var ft = new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(fam, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size, brush, 96);
        dc.DrawText(ft, new Point(x, y - size));
    }
}
