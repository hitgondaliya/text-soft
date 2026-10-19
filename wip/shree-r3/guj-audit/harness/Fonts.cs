using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static partial class Program
{
    static readonly Encoding Win1252 = GetCp();

    /// <summary>The code point a legacy byte is stored as in our tables (Windows-1252; the 5 unassigned bytes as C1).</summary>
    public static char CharOfByte(int b)
    {
        string s = Win1252.GetString(new[] { (byte)b });
        return s.Length == 1 ? s[0] : (char)b;
    }

    // fonts scan  -> results/fonts-installed.json  (every TTF/OTF whose family has GUJ in it)
    // fonts chart <file> <out.png>
    // fonts strips <file> <out.png> "<hex hex>|<hex>|..."
    static int FontsMode(string[] args)
    {
        if (args[1] == "scan")
        {
            var dirs = new[] { @"C:\Windows\Fonts", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts") };
            var rows = new List<object>();
            foreach (var d in dirs.Where(Directory.Exists))
            foreach (var f in Directory.GetFiles(d).Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var gt = new GlyphTypeface(new Uri(f));
                    string fam = gt.Win32FamilyNames.TryGetValue(new System.Globalization.CultureInfo("en-US"), out var n) ? n : gt.Win32FamilyNames.Values.FirstOrDefault() ?? "";
                    if (fam.IndexOf("GUJ", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var map = gt.CharacterToGlyphMap;
                    var bytes = Enumerable.Range(0x20, 0xE0).Where(b => map.ContainsKey(CharOfByte(b))).Select(b => b.ToString("X2")).ToList();
                    var extra = map.Keys.Where(k => k > 0xFF && !Enumerable.Range(0x80, 0x20).Any(b => CharOfByte(b) == k)).Select(k => "U+" + k.ToString("X4")).ToList();
                    rows.Add(new { file = f, family = fam, face = gt.Win32FaceNames.Values.FirstOrDefault(), glyphs = gt.GlyphCount, codes = bytes.Count, extra, symbol = gt.Symbol, missingBytes = Enumerable.Range(0x20, 0xE0).Where(b => !map.ContainsKey(CharOfByte(b))).Select(b => b.ToString("X2")).ToList() });
                }
                catch { }
            }
            File.WriteAllText(Path.Combine(A, "results", "fonts-installed.json"), JsonSerializer.Serialize(rows, Json));
            // what WPF lists (SystemFontService.IsInstalled uses Fonts.SystemFontFamilies)
            var wpf = Fonts.SystemFontFamilies.Select(ff => ff.Source).Where(s => s.IndexOf("GUJ", StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(s => s).ToList();
            File.WriteAllText(Path.Combine(A, "results", "fonts-wpf-families.json"), JsonSerializer.Serialize(wpf, Json));
            Console.WriteLine($"{rows.Count} GUJ font files; WPF families with GUJ: {wpf.Count}");
            return 0;
        }
        if (args[1] == "chart")
        {
            var gt = new GlyphTypeface(new Uri(args[2]));
            var dv = new DrawingVisual();
            int cols = 16, rowsN = 14, cw = 70, ch = 84;
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, cols * cw + 10, rowsN * ch + 40));
                var label = new Typeface("Segoe UI");
                dc.DrawText(new FormattedText(Path.GetFileName(args[2]) + "  " + gt.Win32FamilyNames.Values.First() + "  (glyph drawn by index for each byte; '-' = no glyph)", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 14, Brushes.Black, 1), new Point(5, 5));
                for (int b = 0x20; b <= 0xFF; b++)
                {
                    int i = b - 0x20, x = (i % cols) * cw + 5, y = (i / cols) * ch + 30;
                    dc.DrawRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(x, y, cw, ch));
                    dc.DrawText(new FormattedText(b.ToString("X2"), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 11, Brushes.Blue, 1), new Point(x + 2, y + 1));
                    if (!gt.CharacterToGlyphMap.TryGetValue(CharOfByte(b), out ushort g)) { dc.DrawText(new FormattedText("-", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 20, Brushes.Red, 1), new Point(x + 30, y + 30)); continue; }
                    double size = 38, adv = gt.AdvanceWidths[g] * size;
                    var run = new GlyphRun(gt, 0, false, size, 1, new[] { g }, new Point(x + 22, y + 62), new[] { adv }, null, null, null, null, null, null);
                    dc.DrawLine(new Pen(Brushes.Orange, 0.5), new Point(x + 22, y + 16), new Point(x + 22, y + 80));
                    dc.DrawLine(new Pen(Brushes.Orange, 0.5), new Point(x + 22 + adv, y + 16), new Point(x + 22 + adv, y + 80));
                    dc.DrawGlyphRun(Brushes.Black, run);
                }
            }
            Save(dv, cols * cw + 10, rowsN * ch + 40, args[3]);
            return 0;
        }
        if (args[1] == "strips")
        {
            var files = args[2].Split(';');
            var strips = args[4].Split('|');
            var dv = new DrawingVisual();
            int w = 260, h = 70;
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 200 + files.Length * w, strips.Length * h + 30));
                var label = new Typeface("Segoe UI");
                for (int fi = 0; fi < files.Length; fi++)
                {
                    var gt = new GlyphTypeface(new Uri(files[fi]));
                    dc.DrawText(new FormattedText(gt.Win32FamilyNames.Values.First(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 12, Brushes.Black, 1), new Point(200 + fi * w, 5));
                    for (int si = 0; si < strips.Length; si++)
                    {
                        var parts = strips[si].Split('=');
                        string hex = parts[0].Trim();
                        if (fi == 0) dc.DrawText(new FormattedText(strips[si], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 11, Brushes.Blue, 1), new Point(4, 30 + si * h + 20));
                        var glyphs = new List<ushort>(); var advs = new List<double>();
                        double size = 40;
                        foreach (var hb in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            int b = Convert.ToInt32(hb, 16);
                            if (gt.CharacterToGlyphMap.TryGetValue(CharOfByte(b), out ushort g)) { glyphs.Add(g); advs.Add(gt.AdvanceWidths[g] * size); }
                        }
                        dc.DrawRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(200 + fi * w, 30 + si * h, w - 10, h - 4));
                        if (glyphs.Count > 0)
                            dc.DrawGlyphRun(Brushes.Black, new GlyphRun(gt, 0, false, size, 1, glyphs, new Point(210 + fi * w, 30 + si * h + 48), advs, null, null, null, null, null, null));
                    }
                }
            }
            Save(dv, 200 + files.Length * w, strips.Length * h + 30, args[3]);
            return 0;
        }
        return 1;
    }

    static void Save(Visual v, int w, int h, string file)
    {
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
