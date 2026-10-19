using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WordVis;

/// 32-bit image, pixels 0xAARRGGBB, top-down.
public sealed class Img
{
    public int W, H;
    public uint[] P;
    public Img(int w, int h) { W = w; H = h; P = new uint[w * h]; Array.Fill(P, 0xFFFFFFFFu); }

    public static Img FromBitmap(Bitmap bmp)
    {
        var img = new Img(bmp.Width, bmp.Height);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bmp.Height; y++)
        {
            var row = new int[bmp.Width];
            Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, bmp.Width);
            for (int x = 0; x < bmp.Width; x++)
            {
                uint c = (uint)row[x];
                // composite over white
                uint a = c >> 24;
                if (a < 255)
                {
                    uint r = ((c >> 16 & 255) * a + 255 * (255 - a)) / 255, g = ((c >> 8 & 255) * a + 255 * (255 - a)) / 255, b = ((c & 255) * a + 255 * (255 - a)) / 255;
                    c = 0xFF000000 | r << 16 | g << 8 | b;
                }
                img.P[y * bmp.Width + x] = c;
            }
        }
        bmp.UnlockBits(bd);
        return img;
    }

    public void Save(string path, int x0 = 0, int y0 = 0, int w = -1, int h = -1)
    {
        if (w < 0) w = W - x0;
        if (h < 0) h = H - y0;
        using var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bmp.Height; y++)
        {
            var row = new int[bmp.Width];
            for (int x = 0; x < bmp.Width; x++)
            {
                int sx = x0 + x, sy = y0 + y;
                row[x] = (int)(sx >= 0 && sy >= 0 && sx < W && sy < H ? P[sy * W + sx] : 0xFFFFFFFFu);
            }
            Marshal.Copy(row, 0, bd.Scan0 + y * bd.Stride, bmp.Width);
        }
        bmp.UnlockBits(bd);
        bmp.Save(path, ImageFormat.Png);
    }

    public static void Rgb(uint c, out int r, out int g, out int b) { r = (int)(c >> 16 & 255); g = (int)(c >> 8 & 255); b = (int)(c & 255); }

    /// Dark, colour-neutral pixel = the black test glyph.
    public static bool IsInk(uint c)
    {
        Rgb(c, out int r, out int g, out int b);
        int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        int lum = (r * 299 + g * 587 + b * 114) / 1000;
        return lum < 128 && mx - mn < 70;
    }

    public static bool IsRed(uint c)
    {
        Rgb(c, out int r, out int g, out int b);
        return r > 140 && g < 110 && b < 110 && r - g > 70;
    }

    public static bool IsBlue(uint c)
    {
        Rgb(c, out int r, out int g, out int b);
        return b > 140 && r < 110 && g < 110;
    }

    /// Connected components (8-way) of pixels matching pred; returns bboxes and pixel counts.
    public List<(int x0, int y0, int x1, int y1, int n)> Components(Func<uint, bool> pred, int minPx = 4)
    {
        var seen = new bool[W * H];
        var res = new List<(int, int, int, int, int)>();
        var stack = new Stack<int>();
        for (int i = 0; i < W * H; i++)
        {
            if (seen[i] || !pred(P[i])) continue;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, n = 0;
            stack.Push(i); seen[i] = true;
            while (stack.Count > 0)
            {
                int k = stack.Pop(); int x = k % W, y = k / W; n++;
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int j = ny * W + nx;
                        if (!seen[j] && pred(P[j])) { seen[j] = true; stack.Push(j); }
                    }
            }
            if (n >= minPx) res.Add((x0, y0, x1, y1, n));
        }
        return res;
    }
}

