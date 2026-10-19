using System.Text;
using System.Text.Json.Nodes;

namespace WordVis;

/// Reads the EMF pictures Word made of each test paragraph (Range.EnhMetaFileBits): which font Word
/// selected for the black test character and which code (or glyph index) it wrote.
public static class EmfAnalyze
{
    public sealed class TextOut { public string Face = ""; public int Height; public int Charset; public uint Color; public bool GlyphIdx; public int X, Y; public List<int> Codes = new(); }

    public static List<TextOut> Parse(byte[] b)
    {
        var res = new List<TextOut>();
        var objs = new Dictionary<int, (string face, int h, int cs)>();
        (string face, int h, int cs) cur = ("", 0, 0);
        uint color = 0;
        int o = 0;
        int I32(int p) => BitConverter.ToInt32(b, p);
        uint U32(int p) => BitConverter.ToUInt32(b, p);
        while (o + 8 <= b.Length)
        {
            int type = I32(o), size = I32(o + 4);
            if (size < 8 || o + size > b.Length) break;
            switch (type)
            {
                case 82: // EXTCREATEFONTINDIRECTW
                    {
                        int ih = I32(o + 8);
                        int h = I32(o + 12);
                        int cs = b[o + 12 + 23];
                        string face = Encoding.Unicode.GetString(b, o + 12 + 28, 64);
                        int z = face.IndexOf('\0'); if (z >= 0) face = face[..z];
                        objs[ih] = (face, h, cs);
                        break;
                    }
                case 37: // SELECTOBJECT
                    {
                        uint ih = U32(o + 8);
                        if ((ih & 0x80000000) == 0 && objs.TryGetValue((int)ih, out var f)) cur = f;
                        break;
                    }
                case 24: color = U32(o + 8) & 0x00FFFFFF; break; // SETTEXTCOLOR
                case 84: // EXTTEXTOUTW
                case 83: // EXTTEXTOUTA
                    {
                        int x = I32(o + 36), y = I32(o + 40), n = I32(o + 44), off = I32(o + 48), opt = I32(o + 52);
                        var t = new TextOut { Face = cur.face, Height = cur.h, Charset = cur.cs, Color = color, GlyphIdx = (opt & 0x10) != 0, X = x, Y = y };
                        for (int i = 0; i < n; i++) t.Codes.Add(type == 84 ? BitConverter.ToUInt16(b, o + off + 2 * i) : b[o + off + i]);
                        res.Add(t);
                        break;
                    }
                case 108: // SMALLTEXTOUT
                    {
                        int x = I32(o + 8), y = I32(o + 12), n = I32(o + 16), opt = I32(o + 20);
                        int p = o + 36 + ((opt & 0x100) != 0 ? 0 : 16);
                        var t = new TextOut { Face = cur.face, Height = cur.h, Charset = cur.cs, Color = color, GlyphIdx = (opt & 0x10) != 0, X = x, Y = y };
                        for (int i = 0; i < n; i++) t.Codes.Add((opt & 0x200) != 0 ? b[p + i] : BitConverter.ToUInt16(b, p + 2 * i));
                        res.Add(t);
                        break;
                    }
            }
            o += size;
        }
        return res;
    }

    public static void Run(string[] args)
    {
        var fams = args.Length > 0 ? args.ToList() : Lines.Families().ToList();
        foreach (var famArg in fams)
        {
            string tag = famArg.Contains('=') ? famArg.Split('=')[0] : famArg;
            string fam = famArg.Contains('=') ? famArg.Split('=')[1] : famArg;
            string dir = Path.Combine(Program.Root, "emf", tag);
            if (!Directory.Exists(dir)) { Console.WriteLine($"{fam}: no emf dir"); continue; }
            var b = Lines.Base(fam);
            string face = (string)b["repFace"]!;
            var orig = new Ttf((string)b["repFile"]!);
            var lines = Lines.Load(fam);
            var outArr = new JsonArray();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                string f = Path.Combine(dir, $"{i + 1:D4}.emf");
                var o = new JsonObject { ["cp"] = Program.Hex(line.Cp), ["kind"] = line.Kind, ["gid"] = line.Gid };
                if (!File.Exists(f)) { o["emf"] = "missing"; outArr.Add(o); continue; }
                var outs = Parse(File.ReadAllBytes(f));
                // label check: the blue run must spell this line's code point
                var label = string.Concat(outs.Where(t => t.Color == 0x00FF0000).SelectMany(t => t.Codes).Select(c => (char)c)).Trim();
                var reds = outs.Where(t => t.Color == 0x000000FF).OrderBy(t => t.X).ToList();
                int xL = reds.Count > 0 ? reds.First().X : int.MinValue, xR = reds.Count > 0 ? reds.Last().X : int.MaxValue;
                var blacks = outs.Where(t => t.Color == 0 && t.X > xL && t.X < xR).ToList();
                var arr = new JsonArray();
                string verdict;
                foreach (var t in blacks)
                    arr.Add(new JsonObject { ["face"] = t.Face, ["height"] = t.Height, ["charset"] = t.Charset, ["glyphIndex"] = t.GlyphIdx, ["codes"] = string.Join(" ", t.Codes.Select(c => c.ToString("X4"))) });
                if (blacks.Count == 0) verdict = "nothing-emitted";
                else if (blacks.All(t => t.Face.Equals(face, StringComparison.OrdinalIgnoreCase)))
                {
                    var codes = blacks.SelectMany(t => t.Codes.Select(c => (c, t.GlyphIdx))).ToList();
                    if (codes.Count == 1 && ((!codes[0].GlyphIdx && codes[0].c == line.Cp) || (codes[0].GlyphIdx && codes[0].c == line.Gid))) verdict = "shree-font, same code";
                    else verdict = "shree-font, codes " + string.Join(" ", codes.Select(c => (c.GlyphIdx ? "gid" : "U+") + c.c.ToString("X4")));
                }
                else verdict = "fallback:" + string.Join("+", blacks.Select(t => t.Face).Where(x => !x.Equals(face, StringComparison.OrdinalIgnoreCase)).Distinct());
                double advEm = double.NaN;
                if (blacks.Count > 0 && reds.Count >= 2 && blacks[0].Height != 0)
                    advEm = (reds.Last().X - blacks[0].X) / (double)Math.Abs(blacks[0].Height) - 4 * 0.27783203125;
                o["label"] = label;
                o["labelOk"] = label == Program.Hex(line.Cp);
                o["emf"] = verdict;
                o["outs"] = arr;
                o["advanceUnits"] = double.IsNaN(advEm) ? null : Math.Round(advEm * 1000);
                o["fontAdvance"] = line.Gid >= 0 ? orig.Adv[line.Gid] * 1000.0 / orig.UnitsPerEm : null;
                outArr.Add(o);
            }
            File.WriteAllText(Path.Combine(Program.Root, "fam", tag + ".emf.json"), outArr.ToJsonString(Program.JsonOpt));
            var counts = outArr.GroupBy(x => ((string)x!["emf"]!).Split(':')[0].Split(',')[0] + (((string)x["emf"]!).Contains("same code") ? "/same" : "")).Select(g => g.Key + "=" + g.Count());
            Console.WriteLine($"{tag}: {outArr.Count} lines; labelOk={outArr.Count(x => (bool?)x!["labelOk"] == true)}; {string.Join(" ", counts)}");
        }
    }
}

public static class EmfPlay
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr GetEnhMetaFileW(string path);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool PlayEnhMetaFile(IntPtr hdc, IntPtr hemf, ref Native.RECT rc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteEnhMetaFile(IntPtr hemf);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern uint GetEnhMetaFileHeader(IntPtr hemf, uint size, byte[] buf);

    /// Play Word's EMF picture of a test line with GDI and save it; report black ink pixels.
    public static void Run(string[] args)
    {
        if (args.Length == 0) { foreach (var f in Lines.Families()) Run(new[] { f }); return; }
        string tag = args[0].Contains('=') ? args[0].Split('=')[0] : args[0];
        string fam = args[0].Contains('=') ? args[0].Split('=')[1] : args[0];
        var lines = Lines.Load(fam);
        string outDir = Path.Combine(Program.Root, "emfplay", tag);
        Directory.CreateDirectory(outDir);
        var cps = args.Skip(1).Select(a => Convert.ToInt32(a.Replace("U+", ""), 16)).ToList();
        if (cps.Count == 0) cps = lines.Select(l => l.Cp).ToList();
        var res = new System.Text.Json.Nodes.JsonArray();
        foreach (int cp in cps)
        {
            int i = lines.FindIndex(l => l.Cp == cp);
            string f = Path.Combine(Program.Root, "emf", tag, $"{i + 1:D4}.emf");
            IntPtr h = GetEnhMetaFileW(f);
            var hdr = new byte[108];
            GetEnhMetaFileHeader(h, 108, hdr);
            int bl = BitConverter.ToInt32(hdr, 8), bt = BitConverter.ToInt32(hdr, 12), br = BitConverter.ToInt32(hdr, 16), bb = BitConverter.ToInt32(hdr, 20);
            double scale = cps.Count > 12 ? 1.5 : 3.0;
            int w = (int)((br - bl + 1) * scale), hh = (int)((bb - bt + 1) * scale);
            using var s = new Surface(Math.Max(1, w), Math.Max(1, hh));
            var rc = new Native.RECT { left = 0, top = 0, right = w, bottom = hh };
            PlayEnhMetaFile(s.Dc, h, ref rc);
            DeleteEnhMetaFile(h);
            var img = s.ToImg();
            int ink = 0;
            for (int k = 0; k < img.P.Length; k++) if (Img.IsInk(img.P[k])) ink++;
            if (cps.Count <= 12 || ink == 0) img.Save(Path.Combine(outDir, $"{cp:X4}.png"));
            res.Add(new System.Text.Json.Nodes.JsonObject { ["cp"] = Program.Hex(cp), ["blackInkPx"] = ink, ["w"] = w, ["h"] = hh });
            if (cps.Count <= 12) Console.WriteLine($"{fam} {Program.Hex(cp)} black ink px {ink} ({w}x{hh})");
        }
        File.WriteAllText(Path.Combine(outDir, "emfplay.json"), res.ToJsonString(Program.JsonOpt));
        Console.WriteLine($"{fam}: {res.Count} lines played, {res.Count(x => (int)x!["blackInkPx"]! == 0)} without black ink");
    }
}

public static class GdiChar
{
    /// GDI ExtTextOutW of a character (no glyph index) vs the same glyph by index: ink pixel counts.
    public static void Run(string[] args)
    {
        string face = args[0];
        var t = new Ttf(args[1]);
        foreach (var a in args.Skip(2))
        {
            int cp = Convert.ToInt32(a.Replace("U+", ""), 16);
            var byChar = Gdi.Render(face, new[] { (ushort)cp }, false, out _);
            int gid = t.Cmap31.TryGetValue(cp, out var g) ? g : 0;
            var byGid = Gdi.Render(face, new[] { (ushort)gid }, true, out _);
            Console.WriteLine($"{face} {Program.Hex(cp)} gid {gid}: ExtTextOutW char ink {byChar.Count}, ETO_GLYPH_INDEX ink {byGid.Count}");
        }
    }
}
