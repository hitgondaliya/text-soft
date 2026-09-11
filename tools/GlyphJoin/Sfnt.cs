using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GlyphJoin;

public sealed class Sfnt
{
    public string FilePath = "";
    public byte[] D = Array.Empty<byte>();
    public Dictionary<string, (uint off, uint len)> Tables = new(StringComparer.Ordinal);
    public string SfntTag = "";
    public int NumGlyphs;
    public bool HasGlyf, HasCff;
    public List<(int plat, int enc, int fmt, int count)> CmapSubtables = new();
    public string FamilyName = "";

    ushort U16(long o) => (ushort)((D[o] << 8) | D[o + 1]);
    uint U32(long o) => (uint)((D[o] << 24) | (D[o + 1] << 16) | (D[o + 2] << 8) | D[o + 3]);

    public static Sfnt Load(string path)
    {
        var f = new Sfnt { FilePath = path, D = File.ReadAllBytes(path) };
        uint tag = f.U32(0);
        f.SfntTag = tag == 0x4F54544F ? "OTTO" : tag == 0x00010000 ? "1.0" : "0x" + tag.ToString("X8");
        int num = f.U16(4);
        for (int i = 0; i < num; i++)
        {
            long o = 12 + i * 16;
            string t = Encoding.ASCII.GetString(f.D, (int)o, 4);
            f.Tables[t] = (f.U32(o + 8), f.U32(o + 12));
        }
        f.HasGlyf = f.Tables.ContainsKey("glyf");
        f.HasCff = f.Tables.ContainsKey("CFF ");
        if (f.Tables.TryGetValue("maxp", out var mx)) f.NumGlyphs = f.U16(mx.off + 4);
        f.ScanCmap();
        f.FamilyName = f.ReadName(1);
        return f;
    }

    void ScanCmap()
    {
        if (!Tables.TryGetValue("cmap", out var c)) return;
        int n = U16(c.off + 2);
        for (int i = 0; i < n; i++)
        {
            long rec = c.off + 4 + i * 8;
            int plat = U16(rec), enc = U16(rec + 2);
            uint sub = c.off + U32(rec + 4);
            CmapSubtables.Add((plat, enc, U16(sub), ParseSub(sub).Count));
        }
    }

    public Dictionary<int, int> BestCmap()
    {
        if (!Tables.TryGetValue("cmap", out var c)) return new();
        int n = U16(c.off + 2);
        Dictionary<int, int>? best = null; int bestScore = -1;
        for (int i = 0; i < n; i++)
        {
            long rec = c.off + 4 + i * 8;
            int plat = U16(rec), enc = U16(rec + 2);
            uint sub = c.off + U32(rec + 4);
            var m = ParseSub(sub);
            int score = m.Count + (plat == 3 && enc == 1 ? 100000 : plat == 3 && enc == 10 ? 90000 : 0);
            if (score > bestScore) { bestScore = score; best = m; }
        }
        return best ?? new();
    }

    Dictionary<int, int> ParseSub(uint sub)
    {
        var map = new Dictionary<int, int>();
        int fmt = U16(sub);
        if (fmt == 0)
        {
            for (int i = 0; i < 256; i++) { int g = D[sub + 6 + i]; if (g != 0) map[i] = g; }
        }
        else if (fmt == 4)
        {
            int segX2 = U16(sub + 6); int seg = segX2 / 2;
            long endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
            for (int s = 0; s < seg; s++)
            {
                int end = U16(endO + s * 2), start = U16(startO + s * 2);
                short delta = (short)U16(deltaO + s * 2); int ro = U16(rangeO + s * 2);
                if (start == 0xFFFF) continue;
                for (int ch = start; ch <= end && ch != 0x10000; ch++)
                {
                    int g;
                    if (ro == 0) g = (ch + delta) & 0xFFFF;
                    else
                    {
                        long gi = rangeO + s * 2 + ro + (ch - start) * 2;
                        if (gi + 1 >= D.Length) continue;
                        g = U16(gi); if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) map[ch] = g;
                }
            }
        }
        else if (fmt == 6)
        {
            int first = U16(sub + 6), cnt = U16(sub + 8);
            for (int i = 0; i < cnt; i++) { int g = U16(sub + 10 + i * 2); if (g != 0) map[first + i] = g; }
        }
        else if (fmt == 12)
        {
            uint groups = U32(sub + 12);
            for (uint i = 0; i < groups; i++)
            {
                long o = sub + 16 + i * 12;
                uint s = U32(o), e = U32(o + 4), gs = U32(o + 8);
                for (uint ch = s; ch <= e && ch - s < 0x10000; ch++) map[(int)ch] = (int)(gs + (ch - s));
            }
        }
        return map;
    }

    public int Advance(int gid)
    {
        if (!Tables.TryGetValue("hhea", out var hh) || !Tables.TryGetValue("hmtx", out var hm)) return -1;
        int numH = U16(hh.off + 34);
        if (numH == 0) return -1;
        int i = Math.Min(gid, numH - 1);
        return U16(hm.off + i * 4);
    }

    public Dictionary<string, int> OutlineIndex()
    {
        var res = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!HasGlyf || !Tables.TryGetValue("loca", out var lo) || !Tables.TryGetValue("head", out var hd)) return res;
        bool longLoca = U16(hd.off + 50) == 1;
        var gl = Tables["glyf"];
        using var sha = SHA256.Create();
        for (int g = 0; g < NumGlyphs; g++)
        {
            uint s, e;
            if (longLoca) { s = U32(lo.off + g * 4); e = U32(lo.off + g * 4 + 4); }
            else { s = (uint)U16(lo.off + g * 2) * 2; e = (uint)U16(lo.off + g * 2 + 2) * 2; }
            if (e <= s) continue;
            var arr = new byte[e - s];
            Array.Copy(D, gl.off + s, arr, 0, arr.Length);
            string h = Convert.ToHexString(sha.ComputeHash(arr));
            if (!res.ContainsKey(h)) res[h] = g;
        }
        return res;
    }

    public string ReadName(int nameId)
    {
        if (!Tables.TryGetValue("name", out var nm)) return "";
        int cnt = U16(nm.off + 2); uint strOff = nm.off + U16(nm.off + 4);
        for (int i = 0; i < cnt; i++)
        {
            long r = nm.off + 6 + i * 12;
            int plat = U16(r), id = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
            if (id != nameId) continue;
            var bytes = new byte[len];
            Array.Copy(D, strOff + off, bytes, 0, len);
            return plat == 3 ? Encoding.BigEndianUnicode.GetString(bytes) : Encoding.ASCII.GetString(bytes);
        }
        return "";
    }

    /// Stable id for a byte layout: the exact set of code points the cmap maps.
    /// Two fonts with the same hash share an encoding, so they share a table.
    public static string LayoutHash(Dictionary<int, int> cmap)
    {
        var codes = cmap.Keys.OrderBy(c => c).ToArray();
        var buf = new byte[codes.Length * 4];
        for (int i = 0; i < codes.Length; i++) BitConverter.TryWriteBytes(buf.AsSpan(i * 4), codes[i]);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(buf)).Substring(0, 10);
    }
}
