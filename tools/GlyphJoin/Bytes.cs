using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlyphJoin;

// Bytes — render an exact sequence of code points in a font and report what the
// text engine actually did with it.
//
// A legacy 8-bit encoding puts letters on bytes that Unicode has already spoken
// for. ISFOC Gujarati puts ર on 0xAD, which is U+00AD SOFT HYPHEN: the font has
// the glyph, but every Unicode-aware text engine treats the character as a
// conditional hyphen and draws nothing. This measures that directly — width and
// glyph count for a code-point sequence — so the question is settled by the
// renderer rather than by reasoning about it.
//
//   glyphjoin bytes <font> 0041,00AD,0042 [--out png]
public static class Bytes
{
    public static int Run(string fontPath, string codes, string? png)
    {
        string text = new string(codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => (char)Convert.ToInt32(c.Trim(), 16)).ToArray());

        var fam = Fonts.GetFontFamilies(new Uri(Path.GetDirectoryName(Path.GetFullPath(fontPath))! + Path.DirectorySeparatorChar))
                       .FirstOrDefault();
        if (fam is null) { Console.Error.WriteLine("no font family in " + fontPath); return 1; }
        var tf = new Typeface(fam, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Console.WriteLine("font: " + fam.Source);

        // If the glyph is reachable from a second code point, the table can simply
        // use that one and the problem disappears without touching the renderer.
        var sf = Sfnt.Load(fontPath);
        var cmap = sf.BestCmap();
        foreach (char c in text.Distinct())
        {
            if (!cmap.TryGetValue(c, out int gid)) { Console.WriteLine("  U+" + ((int)c).ToString("X4") + " -> not in cmap"); continue; }
            var aliases = cmap.Where(kv => kv.Value == gid && kv.Key != c).Select(kv => "U+" + kv.Key.ToString("X4")).ToList();
            Console.WriteLine("  U+" + ((int)c).ToString("X4") + " -> gid " + gid
                + (aliases.Count > 0 ? "  also at " + string.Join(",", aliases) : "  (no other code point)"));
        }

        foreach (var mode in new[] { TextFormattingMode.Ideal, TextFormattingMode.Display })
            foreach (var (label, s) in new[] { ("full", text), ("without", new string(text.Where(c => c != '­').ToArray())) })
            {
                var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    tf, 40, Brushes.Black, null, mode, 96);
                Console.WriteLine("  " + (mode + " " + label).PadRight(16) + " chars=" + s.Length + " width=" + ft.Width.ToString("F2"));
            }

        // FormattedText and TextBox are different text paths in WPF; the app shows
        // legacy output in a TextBox, so measure that one too before concluding
        // anything about what the customer sees.
        foreach (var (label, s) in new[] { ("box full", text), ("box without", new string(text.Where(c => c != '­').ToArray())) })
        {
            // A TextBox measures to nothing until its template is applied, which
            // only happens inside a window — so give it one rather than trust a
            // zero that means "not laid out yet".
            var tb = new System.Windows.Controls.TextBox
            {
                Text = s, FontFamily = fam, FontSize = 40,
                BorderThickness = new Thickness(0), TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            };
            var win = new Window { Content = tb, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, Left = -4000, Top = -4000 };
            win.Show();
            tb.UpdateLayout();
            Console.WriteLine("  " + label.PadRight(12) + " chars=" + s.Length + " width=" + tb.ActualWidth.ToString("F2"));
            win.Close();
        }

        if (png is not null)
        {
            // Two renderings of the same bytes: as Unicode text (what a TextBox
            // does) and by glyph index (what LegacyTextView does). Any character
            // missing from the top line and present on the bottom one is a letter
            // the Unicode text path threw away.
            var full = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                tf, 40, Brushes.Black, 96);
            tf.TryGetGlyphTypeface(out var gtf);
            var glyphs = new List<ushort>();
            var advances = new List<double>();
            double runWidth = 0;
            foreach (char ch in text)
            {
                if (gtf is null || !gtf.CharacterToGlyphMap.TryGetValue(ch, out ushort gid)) continue;
                double adv = gtf.AdvanceWidths.TryGetValue(gid, out double a) ? a * 40 : 0;
                glyphs.Add(gid); advances.Add(adv); runWidth += adv;
            }
            double w = Math.Max(full.Width, runWidth) + 260;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, 140));
                var label = new Typeface("Segoe UI");
                dc.DrawText(new FormattedText("as Unicode text (TextBox):", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 12, Brushes.Gray, 96), new Point(10, 8));
                dc.DrawText(full, new Point(250, 2));
                dc.DrawText(new FormattedText("by glyph index (LegacyTextView):", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, label, 12, Brushes.Gray, 96), new Point(10, 78));
                if (glyphs.Count > 0 && gtf is not null)
                    dc.DrawGlyphRun(Brushes.Black, new GlyphRun(gtf, 0, false, 40, 1.0f, glyphs,
                        new Point(250, 72 + 40 * gtf.Baseline), advances, null, null, null, null, null, null));
            }
            var rtb = new RenderTargetBitmap((int)w, 140, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(png);
            enc.Save(fs);
            Console.WriteLine("wrote " + png);
        }
        return 0;
    }
}
