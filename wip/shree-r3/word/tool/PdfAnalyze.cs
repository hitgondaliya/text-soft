using System.Text;
using System.Text.Json.Nodes;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WordVis;

public sealed class PFont
{
    public int Obj;
    public string Base = "", Subtype = "", Enc = "";
    public bool Type0, Symbolic;
    public Ttf Emb;
    public int[] CidToGid;      // null = identity
    public int FirstChar;
    public List<double> Widths = new();
    public Dictionary<int, double> CidW = new();
    public double DW = 1000;
    public string Family => Base.Contains('+') ? Base[(Base.IndexOf('+') + 1)..] : Base;
}

public sealed class TRun
{
    public int Page;
    public PFont Font;
    public double Size, X, Y;
    public string Color = "";
    public List<int> Codes = new();
}

public static class PdfAnalyze
{
    static readonly Encoding Cp1252;
    static PdfAnalyze() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); Cp1252 = Encoding.GetEncoding(1252); }

    public static void Run(string[] args)
    {
        var fams = args.Length > 0 ? args.ToList() : Lines.Families().ToList();
        Exception err = null;
        var th = new Thread(() =>
        {
            try
            {
                if (fams.Count >= 4 && fams[0] == "--pdf") { One(fams[1], fams[2], fams[3]); return; }
                foreach (var f in fams)
                {
                    var parts = Directory.GetFiles(Path.Combine(Program.Root, "fam"), f + ".part*.word.pdf").OrderBy(x => x).ToList();
                    if (parts.Count > 0) One(f, string.Join("|", parts), f);
                    else One(f, Path.Combine(Program.Root, "fam", f + ".word.pdf"), f);
                }
            }
            catch (Exception e) { err = e; }
        });
        th.SetApartmentState(ApartmentState.MTA);
        th.Start();
        th.Join();
        if (err != null) throw new Exception("pdf analyze failed", err);
    }

    static string ColorName(double[] c) =>
        c.Length == 1 ? (c[0] < 0.01 ? "black" : "gray") :
        c.Length == 3 ? (c[0] > 0.9 && c[1] < 0.1 && c[2] < 0.1 ? "red" : c[0] < 0.1 && c[1] < 0.1 && c[2] > 0.9 ? "blue" : c[0] < 0.01 && c[1] < 0.01 && c[2] < 0.01 ? "black" : "other") :
        c.Length == 4 ? (c[3] > 0.99 && c[0] < 0.01 && c[1] < 0.01 && c[2] < 0.01 ? "black" : "other") : "other";

    static PFont LoadFont(PdfFile pdf, object fref, Dictionary<int, PFont> cache)
    {
        int n = fref is PRef r ? r.N : -1;
        if (n >= 0 && cache.TryGetValue(n, out var f0)) return f0;
        var d = pdf.Dict(fref);
        var f = new PFont { Obj = n };
        f.Base = d.TryGetValue("BaseFont", out var bf) && bf is PName bn ? bn.V : "?";
        f.Subtype = d.TryGetValue("Subtype", out var st) && st is PName sn ? sn.V : "?";
        f.Type0 = f.Subtype == "Type0";
        if (d.TryGetValue("Encoding", out var en)) f.Enc = pdf.Res(en) is PName ep ? ep.V : "dict";
        Dictionary<string, object> desc;
        if (f.Type0)
        {
            var dfs = pdf.Res(d["DescendantFonts"]) as List<object>;
            var cid = pdf.Dict(dfs![0]);
            if (cid.TryGetValue("CIDToGIDMap", out var cg))
            {
                var res = pdf.Res(cg);
                if (res is not PName)
                {
                    var bytes = pdf.StreamOf(cg);
                    if (bytes != null) { f.CidToGid = new int[bytes.Length / 2]; for (int i = 0; i < f.CidToGid.Length; i++) f.CidToGid[i] = bytes[2 * i] << 8 | bytes[2 * i + 1]; }
                }
            }
            if (cid.TryGetValue("DW", out var dw) && dw is double dwd) f.DW = dwd;
            if (cid.TryGetValue("W", out var w) && pdf.Res(w) is List<object> wl)
            {
                for (int i = 0; i < wl.Count;)
                {
                    int c1 = (int)(double)pdf.Res(wl[i]);
                    var nx = pdf.Res(wl[i + 1]);
                    if (nx is List<object> arr) { for (int k = 0; k < arr.Count; k++) f.CidW[c1 + k] = (double)pdf.Res(arr[k]); i += 2; }
                    else { int c2 = (int)(double)nx; double ww = (double)pdf.Res(wl[i + 2]); for (int k = c1; k <= c2; k++) f.CidW[k] = ww; i += 3; }
                }
            }
            desc = pdf.Dict(cid["FontDescriptor"]);
        }
        else
        {
            if (d.TryGetValue("FirstChar", out var fc)) f.FirstChar = (int)(double)pdf.Res(fc);
            if (d.TryGetValue("Widths", out var ws) && pdf.Res(ws) is List<object> wl2) f.Widths = wl2.Select(x => (double)pdf.Res(x)).ToList();
            desc = d.TryGetValue("FontDescriptor", out var fd) ? pdf.Dict(fd) : null;
        }
        if (desc != null)
        {
            if (desc.TryGetValue("Flags", out var fl)) f.Symbolic = (((int)(double)pdf.Res(fl)) & 4) != 0;
            if (desc.TryGetValue("FontFile2", out var ff))
            {
                var bytes = pdf.StreamOf(ff);
                if (bytes != null) { try { f.Emb = new Ttf(bytes, f.Base); } catch { } }
            }
        }
        if (n >= 0) cache[n] = f;
        return f;
    }

    /// Glyph id inside the embedded (subset) font that a code selects, as a PDF viewer resolves it.
    public static int GidOf(PFont f, int code)
    {
        if (f.Emb == null) return -1;
        if (f.Type0) return f.CidToGid == null ? code : code < f.CidToGid.Length ? f.CidToGid[code] : 0;
        if (!f.Symbolic && f.Enc == "WinAnsiEncoding")
        {
            string u = Cp1252.GetString(new[] { (byte)code });
            if (f.Emb.Cmap31.TryGetValue(u[0], out int g)) return g;
        }
        if (f.Emb.Cmap30.TryGetValue(0xF000 + code, out int g2)) return g2;
        if (f.Emb.Cmap30.TryGetValue(code, out int g3)) return g3;
        if (f.Emb.Cmap10.TryGetValue(code, out int g4)) return g4;
        if (f.Emb.Cmap31.TryGetValue(code, out int g5)) return g5;
        return 0;
    }

    static double WidthOf(PFont f, int code)
    {
        if (f.Type0) return f.CidW.TryGetValue(code, out var w) ? w : f.DW;
        int i = code - f.FirstChar;
        return i >= 0 && i < f.Widths.Count ? f.Widths[i] : double.NaN;
    }

    public static List<TRun> Runs(PdfFile pdf, Dictionary<string, object> page, int pageNo, Dictionary<int, PFont> cache)
    {
        var res = new List<TRun>();
        var resources = pdf.Dict(page["Resources"]);
        var fonts = resources != null && resources.TryGetValue("Font", out var fo) ? pdf.Dict(fo) : new Dictionary<string, object>();
        string content = Encoding.Latin1.GetString(pdf.Contents(page));
        var lx = new Lexer(content, 0);
        var ops = new List<object>();
        double[] fill = { 0 };
        var stack = new Stack<double[]>();
        PFont font = null; double size = 0;
        double tmE = 0, tmF = 0, tlE = 0, tlF = 0;
        while (true)
        {
            var t = lx.Token();
            if (t == null) break;
            if (t is POp op && op.V != "[" && op.V != "<<")
            {
                string o = op.V;
                double N(int i) => ops[i] is double dd ? dd : 0;
                switch (o)
                {
                    case "q": stack.Push(fill); break;
                    case "Q": if (stack.Count > 0) fill = stack.Pop(); break;
                    case "g": fill = new[] { N(0) }; break;
                    case "rg": fill = new[] { N(0), N(1), N(2) }; break;
                    case "k": fill = new[] { N(0), N(1), N(2), N(3) }; break;
                    case "sc": case "scn": fill = ops.OfType<double>().ToArray(); break;
                    case "BT": tmE = tmF = tlE = tlF = 0; break;
                    case "Tf": font = LoadFont(pdf, fonts[((PName)ops[0]).V], cache); size = N(1); break;
                    case "Tm": tmE = tlE = N(4); tmF = tlF = N(5); break;
                    case "Td": case "TD": tlE += N(0); tlF += N(1); tmE = tlE; tmF = tlF; break;
                    case "Tj": case "TJ": case "'": case "\"":
                        {
                            var run = new TRun { Page = pageNo, Font = font, Size = size, X = tmE, Y = tmF, Color = ColorName(fill) };
                            IEnumerable<object> items = o == "TJ" && ops.Count > 0 && ops[0] is List<object> arr ? arr : ops.OfType<PStr>();
                            foreach (var it in items)
                                if (it is PStr ps)
                                {
                                    if (font != null && font.Type0) for (int i = 0; i + 1 < ps.V.Length; i += 2) run.Codes.Add(ps.V[i] << 8 | ps.V[i + 1]);
                                    else foreach (var by in ps.V) run.Codes.Add(by);
                                }
                            res.Add(run);
                            break;
                        }
                }
                ops.Clear();
            }
            else
            {
                if (t is POp op2) { lx.Pos -= op2.V.Length; ops.Add(lx.Value()); }
                else ops.Add(t);
            }
        }
        return res;
    }

    static Img RenderPage(PdfDocument doc, uint i, uint width)
    {
        using var page = doc.GetPage(i);
        var opt = new PdfPageRenderOptions { DestinationWidth = width };
        using var ras = new InMemoryRandomAccessStream();
        page.RenderToStreamAsync(ras, opt).AsTask().GetAwaiter().GetResult();
        ras.Seek(0);
        using var st = ras.AsStreamForRead();
        using var bmp = new System.Drawing.Bitmap(st);
        return Img.FromBitmap(bmp);
    }

    public static string FaceFromPs(string ps) => ps switch
    {
        "ArialMT" => "Arial",
        "TimesNewRomanPSMT" => "Times New Roman",
        "SegoeUI" => "Segoe UI",
        "SegoeUISymbol" => "Segoe UI Symbol",
        "CourierNewPSMT" => "Courier New",
        "MS-Gothic" => "MS Gothic",
        "NirmalaUI" => "Nirmala UI",
        "MicrosoftSansSerif" => "Microsoft Sans Serif",
        "SegoeUIEmoji" => "Segoe UI Emoji",
        "SegoeUIHistoric" => "Segoe UI Historic",
        "CambriaMath" => "Cambria Math",
        "MS-Mincho" => "MS Mincho",
        "Gadugi" => "Gadugi",
        _ => ps,
    };

    static void One(string fam, string pdfPaths, string tag)
    {
        var b = Lines.Base(fam);
        string face = (string)b["repFace"]!, file = (string)b["repFile"]!;
        var orig = new Ttf(file);
        var sigToGids = new Dictionary<string, List<int>>();
        for (int g = 0; g < orig.NumGlyphs; g++) { string s = orig.OutlineSig(g); if (!sigToGids.TryGetValue(s, out var l)) sigToGids[s] = l = new(); l.Add(g); }
        var gidToCps = orig.Cmap31.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
        var lines = Lines.Load(fam);
        var byCp = lines.ToDictionary(l => l.Cp);
        var refs = Gdi.Refs(face, orig.NumGlyphs);
        var outArr = new JsonArray();
        var fontsSeen = new HashSet<string>();
        var seen = new HashSet<int>();
        foreach (var pdfPath in pdfPaths.Split("|"))
        {
        var pdf = new PdfFile(pdfPath);
        var pages = pdf.Pages();
        var cache = new Dictionary<int, PFont>();
        var doc = PdfDocument.LoadFromFileAsync(StorageFile.GetFileFromPathAsync(pdfPath).AsTask().GetAwaiter().GetResult()).AsTask().GetAwaiter().GetResult();
        var crops = Path.Combine(Program.Root, "crops", fam);
        Directory.CreateDirectory(crops);
        for (int pi = 0; pi < pages.Count; pi++)
        {
            var page = pages[pi];
            var mb = (pdf.Res(page["MediaBox"]) as List<object>)!.Select(x => (double)pdf.Res(x)).ToArray();
            double pw = mb[2] - mb[0], ph = mb[3] - mb[1];
            uint rw = (uint)Math.Round(pw / 72.0 * 225.0);
            double sc = rw / pw;
            var img = RenderPage(doc, (uint)pi, rw);
            var runs = Runs(pdf, page, pi + 1, cache);
            foreach (var grp in runs.GroupBy(r => Math.Round(r.Y, 1)))
            {
                var lineRuns = grp.OrderBy(r => r.X).ToList();
                var label = lineRuns.FirstOrDefault(r => r.Color == "blue");
                if (label == null) continue;
                string ltxt = Cp1252.GetString(label.Codes.Select(c => (byte)c).ToArray()).Trim();
                if (!ltxt.StartsWith("U+")) continue;
                int cp = Convert.ToInt32(ltxt[2..], 16);
                if (!byCp.TryGetValue(cp, out var line)) continue;
                seen.Add(cp);
                var reds = lineRuns.Where(r => r.Color == "red").ToList();
                double xL = reds.Count > 0 ? reds.First().X : 0, xR = reds.Count > 0 ? reds.Last().X : double.MaxValue;
                var blacks = lineRuns.Where(r => r.Color == "black" && r.X > xL && r.X < xR).ToList();
                var runArr = new JsonArray();
                string pdfVerdict;
                var glyphNotes = new List<string>();
                bool shreeFont = false, allExpected = true, anyCode = false;
                var fallbackFonts = new List<string>();
                foreach (var r in blacks)
                {
                    var codes = new JsonArray();
                    foreach (int code in r.Codes)
                    {
                        anyCode = true;
                        int sg = GidOf(r.Font, code);
                        string sig = r.Font.Emb != null && sg >= 0 ? r.Font.Emb.OutlineSig(sg) : "not-embedded";
                        string match;
                        bool isShree = r.Font.Family.Equals(face, StringComparison.OrdinalIgnoreCase);
                        if (isShree)
                        {
                            shreeFont = true;
                            if (sg == 0) match = "notdef";
                            else if (line.Gid >= 0 && sig == orig.OutlineSig(line.Gid)) match = "expected";
                            else if (sig == "empty") match = "empty";
                            else if (sigToGids.TryGetValue(sig, out var gl)) match = "other:" + string.Join("/", gl.Select(g => "gid" + g + (gidToCps.TryGetValue(g, out var cl) ? "=" + string.Join("|", cl.Select(Program.Hex)) : "")));
                            else match = "unknown-outline";
                            if (match != "expected") allExpected = false;
                        }
                        else
                        {
                            allExpected = false;
                            match = "fallback";
                            if (!fallbackFonts.Contains(r.Font.Family)) fallbackFonts.Add(r.Font.Family);
                        }
                        codes.Add(new JsonObject { ["code"] = code.ToString("X2"), ["subsetGid"] = sg, ["width"] = WidthOf(r.Font, code), ["match"] = match });
                        glyphNotes.Add(match);
                    }
                    runArr.Add(new JsonObject { ["font"] = r.Font.Base, ["subtype"] = r.Font.Subtype, ["enc"] = r.Font.Enc, ["x"] = Math.Round(r.X, 2), ["codes"] = codes });
                }
                // advance actually reserved: from the black run (or end of the left marker run) to the right marker
                double startX = blacks.Count > 0 ? blacks[0].X : double.NaN;
                double adv = blacks.Count > 0 && reds.Count >= 2 ? (reds.Last().X - startX) : double.NaN;
                // the right marker run is "    H" (4 Arial spaces = 4*0.2779*24 pt); subtract them
                double advUnits = (adv - 4 * 0.27783203125 * 24) / 24 * 1000;
                if (!anyCode) pdfVerdict = "no-glyph-emitted";
                else if (allExpected) pdfVerdict = "expected-glyph";
                else if (fallbackFonts.Count > 0 && !shreeFont) pdfVerdict = "fallback:" + string.Join("+", fallbackFonts);
                else pdfVerdict = "other:" + string.Join(",", glyphNotes.Distinct());

                // ---- ink on the rendered page
                double penXpt = blacks.Count > 0 ? blacks[0].X : (reds.Count > 0 ? reds[0].X + (0.72216796875 + 4 * 0.27783203125) * 24 : 0);
                int bx = (int)Math.Round(penXpt * sc), by = (int)Math.Round((ph - label.Y) * sc);
                var ink = new Mask();
                int y0 = Math.Max(0, by - (int)(1.45 * Gdi.Em)), y1 = Math.Min(img.H - 1, by + (int)(1.45 * Gdi.Em));
                for (int y = y0; y <= y1; y++)
                    for (int x = 0; x < img.W; x++)
                        if (Img.IsInk(img.P[y * img.W + x])) ink.Add(x - bx, y - by);
                double iouExp = double.NaN;
                if (line.Gid >= 0) iouExp = Mask.Best(refs[line.Gid], ink).score;
                // best glyph of the whole font for this ink
                int bestG = -1; double bestS = -1;
                if (ink.Count > 0 && !(iouExp >= 0.9)) { var rk = Mask.Rank(refs, ink); bestG = rk.gid; bestS = rk.score; }
                // fallback font: compare with the char drawn by GDI in that font
                double iouFallback = double.NaN;
                if (fallbackFonts.Count > 0)
                {
                    var fb = Gdi.Render(FaceFromPs(fallbackFonts[0]), char.ConvertFromUtf32(cp).Select(c => (ushort)c).ToArray(), false, out _);
                    iouFallback = Mask.Best(fb, ink).score;
                }
                int cx0 = Math.Max(0, bx - 2 * Gdi.Em), cy0 = Math.Max(0, by - (int)(1.5 * Gdi.Em));
                img.Save(Path.Combine(crops, $"{cp:X4}.word.png"), cx0, cy0, 5 * Gdi.Em, 3 * Gdi.Em);
                outArr.Add(new JsonObject
                {
                    ["cp"] = Program.Hex(cp),
                    ["kind"] = line.Kind,
                    ["gid"] = line.Gid,
                    ["page"] = pi + 1,
                    ["pdf"] = pdfVerdict,
                    ["runs"] = runArr,
                    ["advanceUnits"] = double.IsNaN(advUnits) ? null : Math.Round(advUnits),
                    ["fontAdvance"] = line.Gid >= 0 ? orig.Adv[line.Gid] * 1000.0 / orig.UnitsPerEm : null,
                    ["inkPx"] = ink.Count,
                    ["refInkPx"] = line.Gid >= 0 ? refs[line.Gid].Count : null,
                    ["iouExpected"] = double.IsNaN(iouExp) ? null : Math.Round(iouExp, 3),
                    ["bestGid"] = bestG,
                    ["bestGidCps"] = bestG >= 0 && gidToCps.TryGetValue(bestG, out var bc) ? string.Join("|", bc.Select(Program.Hex)) : null,
                    ["bestScore"] = bestS < 0 ? null : Math.Round(bestS, 3),
                    ["iouFallback"] = double.IsNaN(iouFallback) ? null : Math.Round(iouFallback, 3),
                });
            }
        }
        foreach (var f in cache.Values) fontsSeen.Add(f.Base + (f.Emb == null ? "(not embedded)" : ""));
        }
        foreach (var l in lines) if (!seen.Contains(l.Cp)) outArr.Add(new JsonObject { ["cp"] = Program.Hex(l.Cp), ["kind"] = l.Kind, ["gid"] = l.Gid, ["pdf"] = "line-not-found" });
        if (tag != fam) outArr = new JsonArray(outArr.Where(x => (string)x!["pdf"]! != "line-not-found").Select(x => x!.DeepClone()).ToArray());
        File.WriteAllText(Path.Combine(Program.Root, "fam", tag + ".word.json"), outArr.ToJsonString(Program.JsonOpt));
        var counts = outArr.GroupBy(x => ((string)x!["pdf"]!).Split(':')[0]).Select(g => g.Key + "=" + g.Count());
        Console.WriteLine($"{tag}: {outArr.Count} lines; {string.Join(" ", counts)}; fonts in pdf: {string.Join(", ", fontsSeen)}");
    }
}
