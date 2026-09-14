using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClientReport;

/// <summary>
/// A small PDF writer: pages, text in the base-14 Helvetica faces, rules,
/// filled boxes and an embedded PNG. Enough for a typeset report and no
/// third-party dependency to license or ship.
/// </summary>
public sealed class Pdf
{
    public const double PageWidth = 595.28;   // A4 portrait, points
    public const double PageHeight = 841.89;

    private readonly List<string> _pages = new();
    private StringBuilder _c = new();
    private readonly List<(string Name, byte[] Rgb, int W, int H)> _images = new();

    public double Y { get; set; }

    public Pdf() => NewPage();

    public int PageCount => _pages.Count;

    public void NewPage()
    {
        if (_c.Length > 0) _pages.Add(_c.ToString());
        _c = new StringBuilder();
        Y = PageHeight;
    }

    // ---- drawing ----

    public void Fill(double x, double y, double w, double h, string color)
        => _c.Append(Col(color, fill: true)).Append(F(x)).Append(' ').Append(F(y)).Append(' ')
             .Append(F(w)).Append(' ').Append(F(h)).Append(" re f\n");

    public void Line(double x1, double y1, double x2, double y2, string color, double width = 0.7)
        => _c.Append(Col(color, fill: false)).Append(F(width)).Append(" w ")
             .Append(F(x1)).Append(' ').Append(F(y1)).Append(" m ")
             .Append(F(x2)).Append(' ').Append(F(y2)).Append(" l S\n");

    public void Text(string s, double x, double y, double size, bool bold = false, string color = "111111")
    {
        _c.Append(Col(color, fill: true))
          .Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(F(size)).Append(" Tf 1 0 0 1 ")
          .Append(F(x)).Append(' ').Append(F(y)).Append(" Tm (").Append(Esc(s)).Append(") Tj ET\n");
    }

    /// <summary>Draw a PNG at the given box (the image keeps its aspect ratio).</summary>
    public void Image(string pngPath, double x, double y, double w, double h)
    {
        var src = new BitmapImage(new Uri(pngPath));
        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgr24, null, 0);
        int iw = conv.PixelWidth, ih = conv.PixelHeight;
        int stride = iw * 3;
        var bgr = new byte[stride * ih];
        conv.CopyPixels(bgr, stride, 0);
        var rgb = new byte[bgr.Length];
        for (int i = 0; i < bgr.Length; i += 3)
        {
            rgb[i] = bgr[i + 2]; rgb[i + 1] = bgr[i + 1]; rgb[i + 2] = bgr[i];
        }
        string name = "Im" + (_images.Count + 1);
        _images.Add((name, rgb, iw, ih));
        _c.Append("q ").Append(F(w)).Append(" 0 0 ").Append(F(h)).Append(' ')
          .Append(F(x)).Append(' ').Append(F(y)).Append(" cm /").Append(name).Append(" Do Q\n");
    }

    // ---- text measuring, so columns and centring line up ----

    private static readonly Typeface Regular = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static double Width(string s, double size, bool bold = false)
    {
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            bold ? Bold : Regular, size, Brushes.Black, 1.0);
        return ft.WidthIncludingTrailingWhitespace;
    }

    public static List<string> Wrap(string text, double maxWidth, double size, bool bold = false)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (string w in words)
        {
            string next = cur.Length == 0 ? w : cur + " " + w;
            if (Width(next, size, bold) > maxWidth && cur.Length > 0)
            {
                lines.Add(cur.ToString());
                cur.Clear().Append(w);
            }
            else cur.Clear().Append(next);
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        return lines;
    }

    // ---- output ----

    public void Save(string path)
    {
        if (_c.Length > 0) { _pages.Add(_c.ToString()); _c = new StringBuilder(); }

        var objects = new List<byte[]>();
        int Add(string s) { objects.Add(Encoding.ASCII.GetBytes(s)); return objects.Count; }

        // 1 catalog, 2 pages tree, 3/4 fonts, then one content stream + one page per page
        int catalog = Add("<< /Type /Catalog /Pages 2 0 R >>");
        int pagesObj = Add("PLACEHOLDER");
        int f1 = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        int f2 = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        var imageObjs = new List<(string Name, int Obj)>();
        foreach (var (name, rgb, w, h) in _images)
        {
            byte[] deflated = Deflate(rgb);
            var head = Encoding.ASCII.GetBytes(
                $"<< /Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB " +
                $"/BitsPerComponent 8 /Filter /FlateDecode /Length {deflated.Length} >>\nstream\n");
            var tail = Encoding.ASCII.GetBytes("\nendstream");
            var all = new byte[head.Length + deflated.Length + tail.Length];
            Buffer.BlockCopy(head, 0, all, 0, head.Length);
            Buffer.BlockCopy(deflated, 0, all, head.Length, deflated.Length);
            Buffer.BlockCopy(tail, 0, all, head.Length + deflated.Length, tail.Length);
            objects.Add(all);
            imageObjs.Add((name, objects.Count));
        }

        string xobjects = imageObjs.Count == 0 ? "" :
            " /XObject << " + string.Join(" ", imageObjs.Select(i => "/" + i.Name + " " + i.Obj + " 0 R")) + " >>";

        var pageObjs = new List<int>();
        foreach (string content in _pages)
        {
            byte[] raw = Encoding.ASCII.GetBytes(content);
            int stream = Add($"<< /Length {raw.Length} >>\nstream\n{content}\nendstream");
            int page = Add($"<< /Type /Page /Parent {pagesObj} 0 R /MediaBox [0 0 {F(PageWidth)} {F(PageHeight)}] " +
                           $"/Resources << /Font << /F1 {f1} 0 R /F2 {f2} 0 R >>{xobjects} >> /Contents {stream} 0 R >>");
            pageObjs.Add(page);
        }
        objects[pagesObj - 1] = Encoding.ASCII.GetBytes(
            $"<< /Type /Pages /Count {pageObjs.Count} /Kids [{string.Join(" ", pageObjs.Select(p => p + " 0 R"))}] >>");

        using var fs = File.Create(path);
        void W(string s) { var b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        W("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new long[objects.Count + 1];
        for (int i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = fs.Position;
            W($"{i + 1} 0 obj\n");
            fs.Write(objects[i], 0, objects[i].Length);
            W("\nendobj\n");
        }
        long xref = fs.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++) W($"{offsets[i]:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Col(string hex, bool fill)
    {
        double r = Convert.ToInt32(hex[..2], 16) / 255.0;
        double g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0;
        double b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0;
        return $"{F(r)} {F(g)} {F(b)} {(fill ? "rg" : "RG")} ";
    }

    /// <summary>Escape for a PDF literal string, and drop anything WinAnsi cannot show.</summary>
    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            if (c == '(' || c == ')' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '’' || c == '‘') sb.Append('\'');
            else if (c == '“' || c == '”') sb.Append('"');
            else if (c == '–' || c == '—') sb.Append('-');
            else if (c == '·') sb.Append('.');
            else if (c < 32) sb.Append(' ');
            else if (c < 127) sb.Append(c);
            else if (c < 256) sb.Append('\\').Append(Convert.ToString(c, 8).PadLeft(3, '0'));
            else sb.Append('?');
        }
        return sb.ToString();
    }
}
