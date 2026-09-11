using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Raster — render every glyph of a font to a small fixed bitmap so glyphs can be
// compared ACROSS two fonts that share a typeface but not an outline format
// (legacy TrueType 'glyf' vs Unicode OpenType 'CFF '). A glyph pair that renders
// to the same bitmap is the same glyph, which is what lets GlyphJoin line up a
// legacy byte with the Unicode code point that draws the same shape.
namespace GlyphJoin;

public sealed class Raster
{
    public const int Size = 32;          // bitmap edge, px
    const double Em = 24.0;              // em size used when rendering
    const double OriginX = 3.0, OriginY = 26.0;

    public int GlyphCount { get; }
    readonly byte[]?[] _sigs;

    public Raster(string fontPath)
    {
        var gt = new GlyphTypeface(new Uri(fontPath));
        GlyphCount = gt.GlyphCount;
        _sigs = new byte[GlyphCount][];
        for (int g = 0; g < GlyphCount; g++)
        {
            Geometry geo;
            try { geo = gt.GetGlyphOutline((ushort)g, Em, Em); }
            catch { continue; }
            if (geo is null || geo.IsEmpty()) continue;
            _sigs[g] = Rasterize(geo);
        }
    }

    static byte[]? Rasterize(Geometry geo)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(OriginX, OriginY));
            dc.DrawGeometry(Brushes.Black, null, geo);
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var px = new byte[Size * Size * 4];
        rtb.CopyPixels(px, Size * 4, 0);
        var sig = new byte[Size * Size];
        bool any = false;
        for (int i = 0; i < Size * Size; i++)
        {
            byte a = px[i * 4 + 3];
            sig[i] = a;
            if (a > 8) any = true;
        }
        return any ? sig : null;
    }

    public byte[]? this[int gid] => (uint)gid < (uint)_sigs.Length ? _sigs[gid] : null;

    /// Ink pixels, used to skip whitespace and to size the distance threshold.
    public static int Ink(byte[] sig)
    {
        int n = 0;
        foreach (byte b in sig) if (b > 8) n++;
        return n;
    }

    public static int Distance(byte[] a, byte[] b, int cutoff)
    {
        int d = 0;
        for (int i = 0; i < a.Length; i++)
        {
            d += Math.Abs(a[i] - b[i]);
            if (d > cutoff) return int.MaxValue;
        }
        return d;
    }

    /// Match every inked glyph of `from` to the closest glyph of `to`.
    /// Returns fromGid -> toGid for confident matches only.
    public static Dictionary<int, int> Match(Raster from, Raster to, double tolerance, out int ambiguous)
    {
        var res = new Dictionary<int, int>();
        ambiguous = 0;

        // bucket target glyphs by ink count so we compare only plausible pairs
        var byInk = new Dictionary<int, List<int>>();
        var toInk = new int[to.GlyphCount];
        for (int g = 0; g < to.GlyphCount; g++)
        {
            var s = to[g];
            if (s is null) { toInk[g] = -1; continue; }
            int ink = Ink(s);
            toInk[g] = ink;
            int bucket = ink / 8;
            for (int b = bucket - 1; b <= bucket + 1; b++)
            {
                if (!byInk.TryGetValue(b, out var l)) byInk[b] = l = new List<int>();
                l.Add(g);
            }
        }

        for (int g = 0; g < from.GlyphCount; g++)
        {
            var s = from[g];
            if (s is null) continue;
            int ink = Ink(s);
            if (!byInk.TryGetValue(ink / 8, out var cands)) continue;

            int best = -1, bestD = int.MaxValue, secondD = int.MaxValue;
            int cutoff = (int)(tolerance * 255 * Math.Max(16, ink));
            foreach (int t in cands)
            {
                var ts = to[t];
                if (ts is null) continue;
                int d = Distance(s, ts, cutoff);
                if (d < bestD) { secondD = bestD; bestD = d; best = t; }
                else if (d < secondD) secondD = d;
            }
            if (best < 0 || bestD == int.MaxValue) continue;
            // require the winner to be clearly better than the runner-up
            if (secondD != int.MaxValue && bestD > 0 && secondD < bestD * 2) { ambiguous++; continue; }
            res[g] = best;
        }
        return res;
    }
}
