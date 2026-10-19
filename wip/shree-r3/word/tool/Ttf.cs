using System.Security.Cryptography;
using System.Text;

namespace WordVis;

/// Minimal TrueType reader: name, cmap (3,1)/(3,0)/(3,10), hmtx, loca/glyf, OS/2.
public sealed class Ttf
{
    public readonly string Path;
    readonly byte[] d;
    readonly Dictionary<string, (int off, int len)> t = new();
    public string Family = "", SubFamily = "", Full = "", PsName = "", TypoFamily = "";
    public int UnitsPerEm, NumGlyphs, IndexToLoc, NumHMetrics;
    public ushort[] Adv = Array.Empty<ushort>();
    public short[] Lsb = Array.Empty<short>();
    public SortedDictionary<int, int> Cmap31 = new();   // cp -> gid
    public SortedDictionary<int, int> Cmap30 = new();
    public bool HasCmap31, HasCmap30, HasCmap310;
    public List<string> CmapSubtables = new();
    public ushort FsType, OS2Version, WeightClass, FsSelection;
    public uint CodePage1, CodePage2;
    public short WinAscent, WinDescent, HheaAscent, HheaDescent, TypoAscent, TypoDescent;
    public string VendId = "";

    public Ttf(string path, bool full = true) : this(File.ReadAllBytes(path), path, full) { }

    public Ttf(byte[] data, string path, bool full = true)
    {
        Path = path;
        d = data;
        int n = U16(4);
        for (int i = 0; i < n; i++)
        {
            int r = 12 + 16 * i;
            string tag = Encoding.ASCII.GetString(d, r, 4);
            t[tag] = ((int)U32(r + 8), (int)U32(r + 12));
        }
        ReadName();
        if (!full) return;
        ReadHead();
        ReadMaxp();
        if (t.ContainsKey("hhea")) ReadHhea();
        if (t.ContainsKey("hmtx")) ReadHmtx();
        ReadOs2();
        if (t.ContainsKey("cmap")) ReadCmap();
    }

    public SortedDictionary<int, int> Cmap10 = new();

    public bool Has(string tag) => t.ContainsKey(tag);
    ushort U16(int o) => (ushort)(d[o] << 8 | d[o + 1]);
    short S16(int o) => (short)(d[o] << 8 | d[o + 1]);
    uint U32(int o) => (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]);

    void ReadName()
    {
        if (!t.TryGetValue("name", out var nm)) return;
        int b = nm.off, count = U16(b + 2), so = b + U16(b + 4);
        var best = new Dictionary<int, (int score, string val)>();
        for (int i = 0; i < count; i++)
        {
            int r = b + 6 + 12 * i;
            int pid = U16(r), eid = U16(r + 2), lid = U16(r + 4), nid = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
            if (nid != 1 && nid != 2 && nid != 4 && nid != 6 && nid != 16) continue;
            string s; int score;
            if (pid == 3) { s = Encoding.BigEndianUnicode.GetString(d, so + off, len); score = lid == 0x409 ? 3 : 2; }
            else if (pid == 1) { s = Encoding.Latin1.GetString(d, so + off, len); score = 1; }
            else continue;
            if (!best.TryGetValue(nid, out var cur) || score > cur.score) best[nid] = (score, s);
        }
        string G(int id) => best.TryGetValue(id, out var v) ? v.val : "";
        Family = G(1); SubFamily = G(2); Full = G(4); PsName = G(6); TypoFamily = G(16);
    }

    void ReadHead()
    {
        var h = t["head"];
        UnitsPerEm = U16(h.off + 18);
        IndexToLoc = S16(h.off + 50);
    }

    void ReadMaxp() => NumGlyphs = U16(t["maxp"].off + 4);

    void ReadHhea()
    {
        var h = t["hhea"];
        HheaAscent = S16(h.off + 4); HheaDescent = S16(h.off + 6);
        NumHMetrics = U16(h.off + 34);
    }

    void ReadHmtx()
    {
        var h = t["hmtx"].off;
        Adv = new ushort[NumGlyphs]; Lsb = new short[NumGlyphs];
        ushort last = 0;
        for (int g = 0; g < NumGlyphs; g++)
        {
            if (g < NumHMetrics) { last = U16(h + 4 * g); Adv[g] = last; Lsb[g] = S16(h + 4 * g + 2); }
            else { Adv[g] = last; Lsb[g] = S16(h + 4 * NumHMetrics + 2 * (g - NumHMetrics)); }
        }
    }

    void ReadOs2()
    {
        if (!t.TryGetValue("OS/2", out var o)) return;
        int b = o.off;
        OS2Version = U16(b);
        WeightClass = U16(b + 4);
        FsType = U16(b + 8);
        VendId = Encoding.ASCII.GetString(d, b + 58, 4);
        FsSelection = U16(b + 62);
        TypoAscent = S16(b + 68); TypoDescent = S16(b + 70);
        WinAscent = (short)U16(b + 74); WinDescent = (short)U16(b + 76);
        if (OS2Version >= 1 && o.len >= 86) { CodePage1 = U32(b + 78); CodePage2 = U32(b + 82); }
    }

    void ReadCmap()
    {
        int b = t["cmap"].off;
        int n = U16(b + 2);
        for (int i = 0; i < n; i++)
        {
            int pid = U16(b + 4 + 8 * i), eid = U16(b + 6 + 8 * i);
            int off = b + (int)U32(b + 8 + 8 * i);
            int fmt = U16(off);
            CmapSubtables.Add($"({pid},{eid}) fmt {fmt}");
            if (pid == 3 && eid == 1) { HasCmap31 = true; ReadSub(off, Cmap31); }
            else if (pid == 3 && eid == 0) { HasCmap30 = true; ReadSub(off, Cmap30); }
            else if (pid == 3 && eid == 10) { HasCmap310 = true; ReadSub(off, Cmap31); }
            else if (pid == 1 && eid == 0) { ReadSub(off, Cmap10); }
        }
    }

    void ReadSub(int off, SortedDictionary<int, int> map)
    {
        int fmt = U16(off);
        if (fmt == 4)
        {
            int segX2 = U16(off + 6);
            int ends = off + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < segX2 / 2; s++)
            {
                int end = U16(ends + 2 * s), start = U16(starts + 2 * s);
                int delta = S16(deltas + 2 * s), ro = U16(ranges + 2 * s);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        int ga = ranges + 2 * s + ro + 2 * (c - start);
                        g = U16(ga);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) map[c] = g;
                }
            }
        }
        else if (fmt == 12)
        {
            uint ng = U32(off + 12);
            for (int i = 0; i < ng; i++)
            {
                int r = off + 16 + 12 * i;
                uint sc = U32(r), ec = U32(r + 4), sg = U32(r + 8);
                for (uint c = sc; c <= ec; c++) map[(int)c] = (int)(sg + c - sc);
            }
        }
        else if (fmt == 0)
        {
            for (int c = 0; c < 256; c++) { int g = d[off + 6 + c]; if (g != 0) map[c] = g; }
        }
        else if (fmt == 6)
        {
            int first = U16(off + 6), cnt = U16(off + 8);
            for (int i = 0; i < cnt; i++) { int g = U16(off + 10 + 2 * i); if (g != 0) map[first + i] = g; }
        }
    }

    public (int off, int len) GlyfRange(int gid)
    {
        if (!t.ContainsKey("loca") || !t.ContainsKey("glyf")) return (0, 0);
        int lo = t["loca"].off, go = t["glyf"].off;
        int a, b;
        if (IndexToLoc == 0) { a = U16(lo + 2 * gid) * 2; b = U16(lo + 2 * gid + 2) * 2; }
        else { a = (int)U32(lo + 4 * gid); b = (int)U32(lo + 4 * gid + 4); }
        return (go + a, b - a);
    }

    public int Contours(int gid)
    {
        var (o, l) = GlyfRange(gid);
        return l == 0 ? 0 : S16(o);
    }

    /// Flattened outline: list of contours, each a list of (x,y,onCurve), composites resolved.
    public List<List<(int x, int y, bool on)>> Outline(int gid, int depth = 0)
    {
        var res = new List<List<(int, int, bool)>>();
        if (depth > 8) return res;
        var (o, l) = GlyfRange(gid);
        if (l == 0) return res;
        int nc = S16(o);
        if (nc >= 0)
        {
            var endPts = new int[nc];
            for (int i = 0; i < nc; i++) endPts[i] = U16(o + 10 + 2 * i);
            int np = nc == 0 ? 0 : endPts[nc - 1] + 1;
            int p = o + 10 + 2 * nc;
            int il = U16(p); p += 2 + il;
            var flags = new byte[np];
            for (int i = 0; i < np;)
            {
                byte f = d[p++]; flags[i++] = f;
                if ((f & 8) != 0) { int rep = d[p++]; while (rep-- > 0 && i < np) flags[i++] = f; }
            }
            var xs = new int[np]; var ys = new int[np];
            int v = 0;
            for (int i = 0; i < np; i++)
            {
                byte f = flags[i];
                if ((f & 2) != 0) { int dx = d[p++]; v += (f & 16) != 0 ? dx : -dx; }
                else if ((f & 16) == 0) { v += S16(p); p += 2; }
                xs[i] = v;
            }
            v = 0;
            for (int i = 0; i < np; i++)
            {
                byte f = flags[i];
                if ((f & 4) != 0) { int dy = d[p++]; v += (f & 32) != 0 ? dy : -dy; }
                else if ((f & 32) == 0) { v += S16(p); p += 2; }
                ys[i] = v;
            }
            int s = 0;
            for (int c = 0; c < nc; c++)
            {
                var cl = new List<(int, int, bool)>();
                for (int i = s; i <= endPts[c]; i++) cl.Add((xs[i], ys[i], (flags[i] & 1) != 0));
                res.Add(cl);
                s = endPts[c] + 1;
            }
            return res;
        }
        // composite
        int q = o + 10;
        while (true)
        {
            int fl = U16(q), cg = U16(q + 2); q += 4;
            int dx, dy;
            if ((fl & 1) != 0) { dx = S16(q); dy = S16(q + 2); q += 4; }
            else { dx = (sbyte)d[q]; dy = (sbyte)d[q + 1]; q += 2; }
            if ((fl & 2) == 0) { dx = 0; dy = 0; } // point matching: ignore
            double a = 1, bb = 0, c = 0, dd = 1;
            if ((fl & 8) != 0) { a = dd = F2(q); q += 2; }
            else if ((fl & 0x40) != 0) { a = F2(q); dd = F2(q + 2); q += 4; }
            else if ((fl & 0x80) != 0) { a = F2(q); bb = F2(q + 2); c = F2(q + 4); dd = F2(q + 6); q += 8; }
            foreach (var cl in Outline(cg, depth + 1))
            {
                var nl = new List<(int, int, bool)>();
                foreach (var (x, y, on) in cl)
                    nl.Add(((int)Math.Round(a * x + c * y) + dx, (int)Math.Round(bb * x + dd * y) + dy, on));
                res.Add(nl);
            }
            if ((fl & 0x20) == 0) break;
        }
        return res;
    }

    double F2(int o) => S16(o) / 16384.0;

    public string OutlineSig(int gid)
    {
        List<List<(int x, int y, bool on)>> ol;
        try { ol = Outline(gid); }
        catch
        {
            var (o, l) = GlyfRange(gid);
            int ll = Math.Max(0, Math.Min(l, d.Length - o));
            return "raw" + Convert.ToHexString(SHA1.HashData(d.AsSpan(o, ll)))[..13];
        }
        if (ol.Count == 0 || ol.All(c => c.Count == 0)) return "empty";
        var sb = new StringBuilder();
        foreach (var c in ol)
        {
            sb.Append('[');
            foreach (var (x, y, on) in c) sb.Append(x).Append(',').Append(y).Append(on ? 'o' : 'f').Append(';');
            sb.Append(']');
        }
        return Convert.ToHexString(SHA1.HashData(Encoding.ASCII.GetBytes(sb.ToString())))[..16];
    }

    public (int xMin, int yMin, int xMax, int yMax) Bbox(int gid)
    {
        var (o, l) = GlyfRange(gid);
        if (l == 0) return (0, 0, 0, 0);
        return (S16(o + 2), S16(o + 4), S16(o + 6), S16(o + 8));
    }

    public string CmapHash()
    {
        var sb = new StringBuilder();
        foreach (var kv in Cmap31) sb.Append(kv.Key).Append(':').Append(kv.Value).Append(';');
        return Convert.ToHexString(SHA1.HashData(Encoding.ASCII.GetBytes(sb.ToString())))[..16];
    }
}
