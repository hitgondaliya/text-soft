using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace WordVis;

public static class RichEditTest
{
    const int WM_USER = 0x400, WM_SETTEXT = 0x000C, WM_CHAR = 0x0102;
    const int EM_REPLACESEL = 0xC2, EM_EXSETSEL = WM_USER + 55, EM_FORMATRANGE = WM_USER + 57, EM_GETCHARFORMAT = WM_USER + 58,
        EM_SETCHARFORMAT = WM_USER + 68, EM_GETTEXTEX = WM_USER + 94, EM_SETTEXTEX = WM_USER + 97, EM_GETLANGOPTIONS = WM_USER + 121,
        EM_SETEVENTMASK = WM_USER + 69, EM_GETTEXTLENGTHEX = WM_USER + 95;
    const uint CFM_BOLD = 1, CFM_ITALIC = 2, CFM_UNDERLINE = 4, CFM_STRIKEOUT = 8, CFM_PROTECTED = 16, CFM_SIZE = 0x80000000, CFM_COLOR = 0x40000000, CFM_FACE = 0x20000000, CFM_CHARSET = 0x08000000;
    const int SCF_SELECTION = 1, SCF_ALL = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CHARFORMAT2W
    {
        public int cbSize; public uint dwMask, dwEffects; public int yHeight, yOffset; public uint crTextColor; public byte bCharSet, bPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szFaceName;
        public short wWeight, sSpacing; public uint crBackColor; public uint lcid; public uint dwReserved; public short sStyle, wKerning; public byte bUnderlineType, bAnimation, bRevAuthor, bUnderlineColor;
    }
    [StructLayout(LayoutKind.Sequential)] struct CHARRANGE { public int cpMin, cpMax; }
    [StructLayout(LayoutKind.Sequential)] struct FORMATRANGE { public IntPtr hdc, hdcTarget; public Native.RECT rc, rcPage; public CHARRANGE chrg; }
    [StructLayout(LayoutKind.Sequential)] struct SETTEXTEX { public uint flags, codepage; }
    [StructLayout(LayoutKind.Sequential)] struct GETTEXTEX { public uint cb, flags, codepage; public IntPtr lpDefaultChar, lpUsedDefChar; }
    [StructLayout(LayoutKind.Sequential)] struct GETTEXTLENGTHEX { public uint flags, codepage; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryW(string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, string l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, ref CHARFORMAT2W l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, ref CHARRANGE l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, ref FORMATRANGE l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, ref SETTEXTEX w, byte[] l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, ref GETTEXTEX w, byte[] l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, ref GETTEXTLENGTHEX w, IntPtr l);
    [DllImport("ole32.dll")] static extern int OleInitialize(IntPtr p);

    static IntPtr hwnd;

    static void SetFormat(int scf, string face, uint color, byte charset)
    {
        var cf = new CHARFORMAT2W
        {
            cbSize = Marshal.SizeOf<CHARFORMAT2W>(),
            dwMask = CFM_FACE | CFM_SIZE | CFM_COLOR | CFM_CHARSET | CFM_BOLD | CFM_ITALIC | CFM_UNDERLINE | CFM_STRIKEOUT,
            dwEffects = 0,
            yHeight = Gdi.Em * 1440 / 96,
            crTextColor = color,
            bCharSet = charset,
            szFaceName = face,
        };
        SendMessageW(hwnd, EM_SETCHARFORMAT, (IntPtr)scf, ref cf);
    }

    static (string face, byte charset) GetFormatAt(int i)
    {
        var cr = new CHARRANGE { cpMin = i, cpMax = i + 1 };
        SendMessageW(hwnd, EM_EXSETSEL, IntPtr.Zero, ref cr);
        var cf = new CHARFORMAT2W { cbSize = Marshal.SizeOf<CHARFORMAT2W>() };
        SendMessageW(hwnd, EM_GETCHARFORMAT, (IntPtr)SCF_SELECTION, ref cf);
        return (cf.szFaceName, cf.bCharSet);
    }

    static string GetText()
    {
        var gtl = new GETTEXTLENGTHEX { flags = 0, codepage = 1200 };
        int n = (int)SendMessageW(hwnd, EM_GETTEXTLENGTHEX, ref gtl, IntPtr.Zero);
        var buf = new byte[(n + 4) * 2];
        var gt = new GETTEXTEX { cb = (uint)buf.Length, flags = 0, codepage = 1200 };
        SendMessageW(hwnd, EM_GETTEXTEX, ref gt, buf);
        return Encoding.Unicode.GetString(buf).TrimEnd('\0');
    }

    static void Select(int a, int b) { var cr = new CHARRANGE { cpMin = a, cpMax = b }; SendMessageW(hwnd, EM_EXSETSEL, IntPtr.Zero, ref cr); }

    /// Product RTF (FontSoftware.Engine/RichTextClipboard.BuildRtf), byte for byte.
    public static string ProductRtf(string text, string fontName)
    {
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil ").Append(fontName.Replace("\\", "").Replace("{", "").Replace("}", "").Replace(";", "")).Append(";}}");
        sb.Append(@"\uc1\pard\plain\rtlch\fcs1\af0\ltrch\fcs0\f0 ");
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '{': sb.Append(@"\{"); break;
                case '}': sb.Append(@"\}"); break;
                case '\n': sb.Append(@"\par "); break;
                case '\r': break;
                case '\t': sb.Append(@"\tab "); break;
                default:
                    if (c < 0x80) sb.Append(c);
                    else sb.Append(@"\u").Append((short)c).Append('?');
                    break;
            }
        }
        sb.Append('}');
        return sb.ToString();
    }

    static Img Render(int w, int h)
    {
        using var s = new Surface(w, h);
        int dpi = Native.GetDeviceCaps(s.Dc, 88);
        var fr = new FORMATRANGE
        {
            hdc = s.Dc, hdcTarget = s.Dc,
            rc = new Native.RECT { left = 0, top = 0, right = w * 1440 / dpi, bottom = h * 1440 / dpi },
            rcPage = new Native.RECT { left = 0, top = 0, right = w * 1440 / dpi, bottom = h * 1440 / dpi },
            chrg = new CHARRANGE { cpMin = 0, cpMax = -1 },
        };
        SendMessageW(hwnd, EM_FORMATRANGE, (IntPtr)1, ref fr);
        SendMessageW(hwnd, EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero);
        return s.ToImg();
    }

    /// ClearType colours the edges of black text: count any dark pixel that is not the blue label.
    static bool DarkAnyHue(uint c)
    {
        Img.Rgb(c, out int r, out int g, out int b);
        int lum = (r * 299 + g * 587 + b * 114) / 1000;
        return lum < 150 && !(b > r + 80 && b > g + 80);
    }

    const string Marker = "H    ";   // left marker + 4 spaces; right marker = 4 spaces + H

    public static void Run(string[] args)
    {
        OleInitialize(IntPtr.Zero);
        LoadLibraryW("Msftedit.dll");
        hwnd = CreateWindowExW(0, "RICHEDIT50W", "", 0x80000000 /*WS_POPUP*/ | 0x4 /*ES_MULTILINE*/, 0, 0, 2400, 600, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) throw new Exception("RichEdit window failed");
        SendMessageW(hwnd, EM_SETEVENTMASK, IntPtr.Zero, IntPtr.Zero);
        int lang = (int)SendMessageW(hwnd, EM_GETLANGOPTIONS, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine($"RichEdit50W hwnd={hwnd} langOptions=0x{lang:X}");
        var fams = args.Length > 0 ? args.ToList() : Lines.Families().ToList();
        foreach (var fam in fams) One(fam);
        DestroyWindow(hwnd);
    }

    static byte FontCharset(string face)
    {
        using var s = new Surface(8, 8);
        var f = Gdi.Font(face);
        var old = Native.SelectObject(s.Dc, f);
        int cs = Native.GetTextCharsetInfo(s.Dc, IntPtr.Zero, 0);
        Native.SelectObject(s.Dc, old);
        Native.DeleteObject(f);
        return (byte)cs;
    }

    static void One(string fam)
    {
        var b = Lines.Base(fam);
        string face = (string)b["repFace"]!, file = (string)b["repFile"]!;
        var orig = new Ttf(file);
        var refs = Gdi.Refs(face, orig.NumGlyphs);
        var gidToCps = orig.Cmap31.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
        byte charset = FontCharset(face);
        var lines = Lines.Load(fam);
        var crops = Path.Combine(Program.Root, "crops", fam);
        Directory.CreateDirectory(crops);
        var outArr = new JsonArray();
        foreach (var line in lines)
        {
            var o = new JsonObject { ["cp"] = Program.Hex(line.Cp), ["kind"] = line.Kind, ["gid"] = line.Gid };
            foreach (string mode in new[] { "typed", "rtf" })
            {
                SendMessageW(hwnd, WM_SETTEXT, IntPtr.Zero, "");
                SetFormat(SCF_ALL, "Arial", 0x000000FF, 0);
                SendMessageW(hwnd, EM_REPLACESEL, IntPtr.Zero, Marker + "    H");
                Select(Marker.Length, Marker.Length);
                string s = char.ConvertFromUtf32(line.Cp);
                if (mode == "typed")
                {
                    SetFormat(SCF_SELECTION, face, 0, charset);
                    foreach (char c in s) SendMessageW(hwnd, WM_CHAR, (IntPtr)c, (IntPtr)1);
                }
                else
                {
                    var st = new SETTEXTEX { flags = 2 /*ST_SELECTION*/ | 1 /*ST_KEEPUNDO*/, codepage = 1252 };
                    string rtfText = ProductRtf(s, face);
                    string vr = Environment.GetEnvironmentVariable("WV_RTFVAR");
                    if (vr == "fcharset0") rtfText = rtfText.Replace(@"{\f0\fnil ", @"{\f0\fnil\fcharset0 ");
                    if (vr == "fcharset2") rtfText = rtfText.Replace(@"{\f0\fnil ", @"{\f0\fnil\fcharset2 ");
                    if (vr == "fcharset0fs") rtfText = rtfText.Replace(@"{\f0\fnil ", @"{\f0\fnil\fcharset0 ");
                    if (vr == "fcharset2fs") rtfText = rtfText.Replace(@"{\f0\fnil ", @"{\f0\fnil\fcharset2 ");
                    if (vr != null && vr.EndsWith("fs")) rtfText = rtfText.Replace(@"\ltrch\fcs0\f0 ", @"\ltrch\fcs0\f0\fs112 ");
                    var bytes = Encoding.ASCII.GetBytes(rtfText + "\0");
                    SendMessageW(hwnd, EM_SETTEXTEX, ref st, bytes);
                }
                string all = GetText();
                // what landed between the markers
                int a = all.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
                int z = all.LastIndexOf("    H", StringComparison.Ordinal);
                string mid = a >= Marker.Length && z >= a ? all[a..z] : "?";
                var faces = new JsonArray();
                for (int i = 0; i < mid.Length; i++) { var (fc, csx) = GetFormatAt(a + i); faces.Add($"{(int)mid[i]:X4}:{fc}/cs{csx}"); }
                var img = Render(1200, 400);
                // markers and ink
                var reds = img.Components(Img.IsRed, 50).Where(c => c.y1 - c.y0 > 25).OrderBy(c => c.x0).ToList();
                var ink = new Mask();
                double penX = double.NaN, adv = double.NaN; int baseY = -1;
                if (reds.Count >= 2)
                {
                    var L = reds.First(); var R = reds.Last();
                    baseY = Math.Max(L.y1, R.y1) + 1;
                    double lsb = 158.0 / 2048 * Gdi.Em, hAdv = Math.Round(1479.0 / 2048 * Gdi.Em), sp = Math.Round(569.0 / 2048 * Gdi.Em);
                    penX = L.x0 - lsb + hAdv + 4 * sp;
                    adv = (R.x0 - lsb - 4 * sp) - penX;
                    int bx = (int)Math.Round(penX);
                    for (int y = Math.Max(0, baseY - (int)(1.45 * Gdi.Em)); y < Math.Min(img.H, baseY + (int)(1.45 * Gdi.Em)); y++)
                        for (int x = 0; x < img.W; x++)
                            if (DarkAnyHue(img.P[y * img.W + x]) && !reds.Any(c => x >= c.x0 - 3 && x <= c.x1 + 3 && y >= c.y0 - 3 && y <= c.y1 + 3)) ink.Add(x - bx, y - baseY);
                    img.Save(Path.Combine(crops, $"{line.Cp:X4}.re-{mode}{(Environment.GetEnvironmentVariable("WV_RTFVAR") is string vq ? "-" + vq : "")}.png"), Math.Max(0, bx - 2 * Gdi.Em), Math.Max(0, baseY - (int)(1.5 * Gdi.Em)), 5 * Gdi.Em, 3 * Gdi.Em);
                }
                double iou = line.Gid >= 0 && reds.Count >= 2 ? Mask.Best(refs[line.Gid], ink).score : double.NaN;
                int bestG = -1; double bestS = -1;
                if (ink.Count > 0 && !(iou >= 0.9)) { var rk = Mask.Rank(refs, ink); bestG = rk.gid; bestS = rk.score; }
                // a font other than the Shree face: compare with GDI drawing the char in that face
                double iouOther = double.NaN; string otherFace = null; uint otherCs = 1; string gdiBound = null;
                foreach (var f in faces)
                {
                    var parts = ((string)f!).Split(':')[1].Split('/');
                    string fs = parts[0]; uint csx = uint.Parse(parts[1][2..]);
                    if (!fs.Equals(face, StringComparison.OrdinalIgnoreCase) || csx != charset) { otherFace = fs; otherCs = csx; break; }
                }
                if (otherFace != null && mid.Length > 0)
                {
                    gdiBound = Gdi.Bind(otherFace, otherCs).face;
                    var fb = Gdi.Render(otherFace, mid.Select(c => (ushort)c).ToArray(), false, out _, Gdi.Em, otherCs);
                    iouOther = Mask.Best(fb, ink).score;
                }
                o[mode] = new JsonObject
                {
                    ["landed"] = string.Join(" ", mid.Select(c => ((int)c).ToString("X4"))),
                    ["faces"] = faces,
                    ["markers"] = reds.Count,
                    ["advancePx"] = double.IsNaN(adv) ? null : Math.Round(adv, 1),
                    ["inkPx"] = ink.Count,
                    ["iouExpected"] = double.IsNaN(iou) ? null : Math.Round(iou, 3),
                    ["bestGid"] = bestG,
                    ["bestGidCps"] = bestG >= 0 && gidToCps.TryGetValue(bestG, out var bc) ? string.Join("|", bc.Select(Program.Hex)) : null,
                    ["bestScore"] = bestS < 0 ? null : Math.Round(bestS, 3),
                    ["otherFace"] = otherFace == null ? null : otherFace + "/cs" + otherCs,
                    ["gdiBindsOtherTo"] = gdiBound,
                    ["iouOtherFace"] = double.IsNaN(iouOther) ? null : Math.Round(iouOther, 3),
                };
            }
            outArr.Add(o);
        }
        string vsuf = Environment.GetEnvironmentVariable("WV_RTFVAR") is string vv ? "." + vv : "";
        File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".richedit" + vsuf + ".json"), outArr.ToJsonString(Program.JsonOpt));
        Console.WriteLine($"{fam}: {outArr.Count} lines, charset {charset}");
    }
}
