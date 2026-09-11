using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Atlas — render every byte's glyph of a legacy font in a labelled grid, large
// and clear, so the Gujarati letter for each byte can be read directly.
//
//   dotnet run --project tools/Atlas -- <legacy.ttf> <out.png> [loByte hi]

int rc = 0;
var th = new Thread(() => rc = Run(args));
th.SetApartmentState(ApartmentState.STA);
th.Start(); th.Join();
return rc;

static int Run(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: atlas <legacy.ttf> <out.png> [lo hi]  |  atlas ref <refFont.ttf> <out.png>"); return 1; }

    // Reference mode: render standard Gujarati letters labelled, to read against.
    if (args[0] == "ref")
    {
        RenderRef(args[1], args[2]);
        return 0;
    }

    var gt = new GlyphTypeface(new Uri(Path.GetFullPath(args[0])));
    int lo = args.Length >= 4 ? Convert.ToInt32(args[2], 16) : 0x21;
    int hi = args.Length >= 4 ? Convert.ToInt32(args[3], 16) : 0xFE;

    var cells = new List<int>();
    for (int c = lo; c <= hi; c++)
        if (gt.CharacterToGlyphMap.TryGetValue(c, out var g) && g != 0) cells.Add(c);

    int cols = 10, cell = 84, pad = 4;
    int rows = (cells.Count + cols - 1) / cols;
    int W = cols * cell + 2 * pad, H = rows * cell + 2 * pad;
    var dv = new DrawingVisual();
    var ui = new Typeface("Consolas");
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
        for (int i = 0; i < cells.Count; i++)
        {
            int c = cells[i];
            int col = i % cols, row = i / cols;
            double x = pad + col * cell, y = pad + row * cell;
            dc.DrawRectangle(null, new Pen(Brushes.Gainsboro, 1), new Rect(x, y, cell, cell));
            // byte label
            dc.DrawText(new FormattedText($"0x{c:X2}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, ui, 11, Brushes.Gray, 1.0), new Point(x + 4, y + 3));
            // glyph, centered, big
            var gid = gt.CharacterToGlyphMap[c];
            DrawGlyphFit(dc, gt, gid, new Rect(x + 6, y + 18, cell - 12, cell - 24));
        }
    }
    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
    using (var fs = File.Create(args[1])) enc.Save(fs);
    Console.WriteLine($"{cells.Count} glyphs, grid {cols}x{rows}, saved {args[1]}");
    return 0;
}

static void RenderRef(string refFont, string outPng)
{
    var gt = new GlyphTypeface(new Uri(Path.GetFullPath(refFont)));
    var items = new List<(string label, char u)>();
    void Add(string s) { foreach (char u in s) items.Add(($"U+{(int)u:X4}", u)); }
    Add("અઆઇઈઉઊઋએઐઓઔ");
    Add("કખગઘઙચછજઝઞટઠડઢણતથદધનપફબભમયરલવશષસહળ");
    Add("ાિીુૂૃેૈોૌંઃઁ્");   // matras & signs
    Add("૦૧૨૩૪૫૬૭૮૯");
    int cols = 11, cell = 84, pad = 4;
    int rows = (items.Count + cols - 1) / cols;
    int W = cols * cell + 2 * pad, H = rows * cell + 2 * pad;
    var dv = new DrawingVisual();
    var ui = new Typeface("Consolas");
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
        for (int i = 0; i < items.Count; i++)
        {
            int col = i % cols, row = i / cols;
            double x = pad + col * cell, y = pad + row * cell;
            dc.DrawRectangle(null, new Pen(Brushes.Gainsboro, 1), new Rect(x, y, cell, cell));
            dc.DrawText(new FormattedText(items[i].label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, ui, 10, Brushes.Gray, 1.0), new Point(x + 3, y + 3));
            if (gt.CharacterToGlyphMap.TryGetValue(items[i].u, out var gid))
                DrawGlyphFit(dc, gt, gid, new Rect(x + 6, y + 18, cell - 12, cell - 24));
        }
    }
    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
    using var fs = File.Create(outPng); enc.Save(fs);
    Console.WriteLine($"reference chart {items.Count} letters -> {outPng}");
}

static void DrawGlyphFit(DrawingContext dc, GlyphTypeface gt, ushort gid, Rect box)
{
    try
    {
        double em = 100;
        var run = new GlyphRun(gt, 0, false, em, 1.0f, new[] { gid }, new Point(0, 0),
            new double[] { gt.AdvanceWidths[gid] * em }, null, null, null, null, null, null);
        var gv = new DrawingVisual();
        using (var g = gv.RenderOpen()) g.DrawGlyphRun(Brushes.Black, run);
        Rect b = gv.ContentBounds;
        if (b.IsEmpty || b.Width <= 0.1 || b.Height <= 0.1) return;
        double s = Math.Min(box.Width / b.Width, box.Height / b.Height);
        var tg = new TransformGroup();
        tg.Children.Add(new TranslateTransform(-b.X, -b.Y));
        tg.Children.Add(new ScaleTransform(s, s));
        tg.Children.Add(new TranslateTransform(box.X + (box.Width - b.Width * s) / 2, box.Y + (box.Height - b.Height * s) / 2));
        dc.PushTransform(tg);
        dc.DrawGlyphRun(Brushes.Black, run);
        dc.Pop();
    }
    catch { }
}
