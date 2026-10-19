using System.Runtime.InteropServices;
using System.Text;

namespace WordVis;

public static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }

    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateFontW(int h, int w, int esc, int orient, int weight, uint italic, uint underline, uint strike, uint charset, uint outPrec, uint clipPrec, uint quality, uint pitch, string face);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern bool ExtTextOutW(IntPtr hdc, int x, int y, uint options, IntPtr rect, ushort[] str, uint count, int[] dx);
    [DllImport("gdi32.dll")] public static extern uint SetTextAlign(IntPtr hdc, uint align);
    [DllImport("gdi32.dll")] public static extern uint SetTextColor(IntPtr hdc, uint c);
    [DllImport("gdi32.dll")] public static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] public static extern uint GetFontData(IntPtr hdc, uint table, uint offset, byte[] buf, uint len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int GetTextFaceW(IntPtr hdc, int n, StringBuilder sb);
    [DllImport("gdi32.dll")] public static extern bool GdiFlush();
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr hdc, int idx);
    [DllImport("gdi32.dll")] public static extern int GetTextCharsetInfo(IntPtr hdc, IntPtr sig, uint flags);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern uint GetGlyphIndicesW(IntPtr hdc, string s, int c, ushort[] g, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
}

/// A white 32-bit DIB with a memory DC.
public sealed class Surface : IDisposable
{
    public readonly int W, H;
    public readonly IntPtr Dc, Bmp, Bits;
    readonly IntPtr old;

    public Surface(int w, int h)
    {
        W = w; H = h;
        var bi = new Native.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        Dc = Native.CreateCompatibleDC(IntPtr.Zero);
        Bmp = Native.CreateDIBSection(Dc, ref bi, 0, out Bits, IntPtr.Zero, 0);
        old = Native.SelectObject(Dc, Bmp);
        Clear();
    }

    public void Clear()
    {
        Native.GdiFlush();
        var white = new int[W];
        Array.Fill(white, unchecked((int)0xFFFFFFFF));
        for (int y = 0; y < H; y++) Marshal.Copy(white, 0, Bits + y * W * 4, W);
    }

    public Img ToImg()
    {
        Native.GdiFlush();
        var img = new Img(W, H);
        var row = new int[W];
        for (int y = 0; y < H; y++)
        {
            Marshal.Copy(Bits + y * W * 4, row, 0, W);
            for (int x = 0; x < W; x++) img.P[y * W + x] = 0xFF000000 | ((uint)row[x] & 0xFFFFFF);
        }
        return img;
    }

    public void Dispose()
    {
        Native.SelectObject(Dc, old);
        Native.DeleteObject(Bmp);
        Native.DeleteDC(Dc);
    }
}

public static class Gdi
{
    public const int Em = 75;               // pixels per em in every rendering compared here
    const uint ETO_GLYPH_INDEX = 0x10;
    const uint TA_BASELINE = 24;

    public static IntPtr Font(string face, int em = Em, uint charset = 1) =>
        Native.CreateFontW(-em, 0, 0, 0, 400, 0, 0, 0, charset, charset == 1 ? 7u /*OUT_TT_ONLY_PRECIS*/ : 0u, 0, 4 /*ANTIALIASED_QUALITY*/, 0, face);

    /// Render glyph ids (glyphIndex) or UTF-16 text with GDI, return the ink mask relative to the pen origin.
    public static Mask Render(string face, ushort[] units, bool glyphIndex, out Img img, int em = Em, uint charset = 1)
    {
        int w = em * 6, h = em * 4, ox = em * 2, oy = em * 2 + em / 2;
        using var s = new Surface(w, h);
        var f = Font(face, em, charset);
        var old = Native.SelectObject(s.Dc, f);
        Native.SetTextColor(s.Dc, 0);
        Native.SetBkMode(s.Dc, 1);
        Native.SetTextAlign(s.Dc, TA_BASELINE);
        Native.ExtTextOutW(s.Dc, ox, oy, glyphIndex ? ETO_GLYPH_INDEX : 0, IntPtr.Zero, units, (uint)units.Length, null);
        Native.SelectObject(s.Dc, old);
        Native.DeleteObject(f);
        img = s.ToImg();
        var m = new Mask();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Img.IsInk(img.P[y * w + x])) m.Add(x - ox, y - oy);
        return m;
    }

    /// Which installed file GDI binds a face name to: (face reported, file size, head checksumAdjustment).
    public static (string face, uint size, uint checksum) Bind(string face, uint charset = 1)
    {
        using var s = new Surface(8, 8);
        var f = Font(face, Em, charset);
        var old = Native.SelectObject(s.Dc, f);
        var sb = new StringBuilder(256);
        Native.GetTextFaceW(s.Dc, 256, sb);
        uint size = Native.GetFontData(s.Dc, 0, 0, null, 0);
        var head = new byte[4];
        uint headTag = 0x64616568; // 'head' little-endian dword
        Native.GetFontData(s.Dc, headTag, 8, head, 4);
        Native.SelectObject(s.Dc, old);
        Native.DeleteObject(f);
        return (sb.ToString(), size, (uint)(head[0] << 24 | head[1] << 16 | head[2] << 8 | head[3]));
    }

    public static uint FileChecksum(string path)
    {
        var t = new Ttf(path, full: false);
        // read head checksumAdjustment directly
        var d = File.ReadAllBytes(path);
        int n = d[4] << 8 | d[5];
        for (int i = 0; i < n; i++)
        {
            int r = 12 + 16 * i;
            if (Encoding.ASCII.GetString(d, r, 4) == "head")
            {
                int off = d[r + 8] << 24 | d[r + 9] << 16 | d[r + 10] << 8 | d[r + 11];
                return (uint)(d[off + 8] << 24 | d[off + 9] << 16 | d[off + 10] << 8 | d[off + 11]);
            }
        }
        return 0;
    }

    static readonly Dictionary<string, Dictionary<int, Mask>> cache = new();

    /// Glyph-index reference masks for every glyph of a face.
    public static Dictionary<int, Mask> Refs(string face, int numGlyphs)
    {
        if (cache.TryGetValue(face, out var c)) return c;
        var res = new Dictionary<int, Mask>();
        for (int g = 0; g < numGlyphs; g++) res[g] = Render(face, new[] { (ushort)g }, true, out _);
        cache[face] = res;
        return res;
    }

    public static void RunRefs(string[] args)
    {
        // Report how GDI binds each family's representative face, and save a contact sheet of glyph-index renders.
        foreach (var fam in Lines.Families())
        {
            var b = Lines.Base(fam);
            string face = (string)b["repFace"]!, file = (string)b["repFile"]!;
            var bind = Bind(face);
            uint want = FileChecksum(file);
            Console.WriteLine($"{fam,-18} face={face,-26} gdi='{bind.face}' size={bind.size} fileSize={new FileInfo(file).Length} sum={bind.checksum:X8} fileSum={want:X8} {(bind.checksum == want && bind.size == new FileInfo(file).Length ? "SAME FILE" : "DIFFERENT FILE")}");
        }
    }
}
