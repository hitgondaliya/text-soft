using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// GlyphMatch (visual) — for each byte of a legacy font, render its glyph and the
// best shape-matching Unicode glyph from a reference font, side by side, into a
// PNG so we can SEE whether byte→Unicode can be recovered from the .ttf alone.
//
//   dotnet run --project tools/GlyphMatch -- <legacy.ttf> <ref.ttf> <out.png>

int rc = 0;
var th = new Thread(() => rc = Run(args));
th.SetApartmentState(ApartmentState.STA);
th.Start(); th.Join();
return rc;

static int Run(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("usage: glyphmatch <legacy.ttf> <ref.ttf> <out.png>"); return 1; }
    var legacy = new GlyphTypeface(new Uri(Path.GetFullPath(args[0])));
    var refr = new GlyphTypeface(new Uri(Path.GetFullPath(args[1])));

    string cand =
        "અઆઇઈઉઊઋએઐઓઔ" +
        "કખગઘઙચછજઝઞટઠડઢણતથદધનપફબભમયરલવશષસહળ" +
        "ાિીુૂૃેૈોૌંઃ્" +
        "૦૧૨૩૪૫૬૭૮૯";
    const int N = 48;

    var refMasks = new List<(char u, bool[] m)>();
    foreach (char u in cand)
        if (refr.CharacterToGlyphMap.TryGetValue(u, out var gid))
        { var m = Mask(refr, gid, N); if (m != null) refMasks.Add((u, m)); }

    var results = new List<(int c, ushort gid, char u, double s)>();
    for (int c = 0x21; c <= 0xFE; c++)
    {
        if (!legacy.CharacterToGlyphMap.TryGetValue(c, out var gid) || gid == 0) continue;
        var lm = Mask(legacy, gid, N); if (lm == null) continue;
        char bu = '\0'; double best = 0;
        foreach (var (u, rm) in refMasks) { double s = IoU(lm, rm); if (s > best) { best = s; bu = u; } }
        results.Add((c, gid, bu, best));
    }

    // Draw: rows of [byte] [legacy glyph] [→ matched unicode] [ref glyph] [score]
    var shown = results.Where(r => r.s > 0.0).OrderBy(r => r.c).ToList();
    int cols = 3, perCol = (shown.Count + cols - 1) / cols, rowH = 46, colW = 250;
    int W = cols * colW + 20, H = perCol * rowH + 30;
    var dv = new DrawingVisual();
    var ui = new Typeface("Segoe UI");
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
        for (int i = 0; i < shown.Count; i++)
        {
            var r = shown[i];
            int col = i / perCol, row = i % perCol;
            double x = 10 + col * colW, y = 12 + row * rowH;
            void Txt(string s, double dx, Brush b, double size, Typeface tf) =>
                dc.DrawText(new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, tf, size, b, 1.0), new Point(x + dx, y));
            Txt($"0x{r.c:X2}", 0, Brushes.Gray, 12, ui);
            DrawGlyph(dc, GTF(args[0]), r.gid, x + 40, y - 4, 34);         // legacy glyph
            Txt("→", 92, Brushes.Silver, 16, ui);
            Txt(r.u.ToString(), 120, r.s >= 0.55 ? Brushes.Black : Brushes.IndianRed, 26,
                new Typeface(new FontFamily(new Uri(Path.GetDirectoryName(Path.GetFullPath(args[1]))! + "\\"), "./#" + refr.FamilyNames.Values.First()), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
            Txt(r.s.ToString("F2"), 180, Brushes.DarkGray, 11, ui);
        }
    }
    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
    using (var fs = File.Create(args[2])) enc.Save(fs);
    int good = results.Count(r => r.s >= 0.55);
    Console.WriteLine($"matched {results.Count} bytes; {good} with score>=0.55; saved {args[2]}");
    return 0;
}

static GlyphTypeface GTF(string p) => new GlyphTypeface(new Uri(Path.GetFullPath(p)));

static void DrawGlyph(DrawingContext dc, GlyphTypeface gt, ushort gid, double x, double y, double em)
{
    try
    {
        var run = new GlyphRun(gt, 0, false, em, 1.0f, new[] { gid }, new Point(x, y + em),
            new double[] { gt.AdvanceWidths[gid] * em }, null, null, null, null, null, null);
        dc.DrawGlyphRun(Brushes.Black, run);
    }
    catch { }
}

static bool[]? Mask(GlyphTypeface gt, ushort gid, int N)
{
    try
    {
        double em = 100;
        var run = new GlyphRun(gt, 0, false, em, 1.0f, new[] { gid }, new Point(0, 0),
            new double[] { gt.AdvanceWidths[gid] * em }, null, null, null, null, null, null);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawGlyphRun(Brushes.Black, run);
        Rect b = dv.ContentBounds;
        if (b.IsEmpty || b.Width <= 0.5 || b.Height <= 0.5) return null;
        double pad = 3, s = Math.Min((N - 2 * pad) / b.Width, (N - 2 * pad) / b.Height);
        var g = new TransformGroup();
        g.Children.Add(new TranslateTransform(-b.X, -b.Y));
        g.Children.Add(new ScaleTransform(s, s));
        g.Children.Add(new TranslateTransform((N - b.Width * s) / 2, (N - b.Height * s) / 2));
        dv.Transform = g;
        var rtb = new RenderTargetBitmap(N, N, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var px = new byte[N * N * 4]; rtb.CopyPixels(px, N * 4, 0);
        var m = new bool[N * N];
        for (int i = 0; i < N * N; i++) m[i] = px[i * 4 + 3] > 60;
        return m;
    }
    catch { return null; }
}

static double IoU(bool[] a, bool[] b)
{
    int inter = 0, uni = 0;
    for (int i = 0; i < a.Length; i++) { if (a[i] || b[i]) uni++; if (a[i] && b[i]) inter++; }
    return uni == 0 ? 0 : (double)inter / uni;
}
