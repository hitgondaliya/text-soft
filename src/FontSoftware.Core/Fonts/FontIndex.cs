using System.Text;

namespace FontSoftware.Core.Fonts;

/// <summary>
/// Installed fonts indexed by family name, each with the set of code points its
/// cmap maps. Both the full-matrix test and the keyboard generator need the same
/// question answered — "will this font actually draw that code?" — so they ask it
/// the same way rather than each carrying its own sfnt reader.
/// </summary>
public static class FontIndex
{
    public static Dictionary<string, HashSet<int>> Build(params string[] dirs)
    {
        var res = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in dirs) {
        if (!Directory.Exists(dir)) continue;
        // A folder a customer hands over usually has the fonts in subfolders, one
        // per download, so walk the tree rather than only the top level.
        foreach (var f in Directory.EnumerateFiles(dir, "*.tt?", SearchOption.AllDirectories))
        {
            try
            {
                // Windows ships several Indic Unicode faces inside ONE collection
                // file (Nirmala.ttc). Skipping .ttc reports them as "not installed".
                foreach (var (family, codes) in ReadAll(f))
                {
                    if (family.Length == 0 || codes.Count == 0) continue;
                    if (!res.TryGetValue(family, out var set)) res[family] = codes;
                    else set.UnionWith(codes);
                }
            }
            catch { }
        }
        }
        return res;
    }

    static IEnumerable<(string family, HashSet<int> codes)> ReadAll(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        if (d.Length > 12 && d[0] == (byte)'t' && d[1] == (byte)'t' && d[2] == (byte)'c' && d[3] == (byte)'f')
        {
            uint n = (uint)((d[8] << 24) | (d[9] << 16) | (d[10] << 8) | d[11]);
            for (uint i = 0; i < n && i < 64; i++)
            {
                long o = 12 + i * 4;
                uint off = (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
                yield return ReadAt(d, off);
            }
            yield break;
        }
        yield return ReadAt(d, 0);
    }

    static (string family, HashSet<int> codes) ReadAt(byte[] d, long b)
    {
        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
        uint U32(long o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

        var tables = new Dictionary<string, (uint off, uint len)>(StringComparer.Ordinal);
        int num = U16(b + 4);
        for (int i = 0; i < num; i++)
        {
            long o = b + 12 + i * 16;
            tables[Encoding.ASCII.GetString(d, (int)o, 4)] = (U32(o + 8), U32(o + 12));
        }

        var codes = new HashSet<int>();
        if (tables.TryGetValue("cmap", out var cm))
        {
            int n = U16(cm.off + 2);
            for (int i = 0; i < n; i++)
            {
                long rec = cm.off + 4 + i * 8;
                if (U16(rec) != 3 || U16(rec + 2) != 1) continue;
                uint sub = cm.off + U32(rec + 4);
                if (U16(sub) != 4) continue;
                int segX2 = U16(sub + 6), seg = segX2 / 2;
                long endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
                for (int s = 0; s < seg; s++)
                {
                    int end = U16(endO + s * 2), start = U16(startO + s * 2);
                    short delta = (short)U16(deltaO + s * 2); int ro = U16(rangeO + s * 2);
                    if (start == 0xFFFF) continue;
                    for (int ch = start; ch <= end; ch++)
                    {
                        int g;
                        if (ro == 0) g = (ch + delta) & 0xFFFF;
                        else
                        {
                            long gi = rangeO + s * 2 + ro + (ch - start) * 2;
                            if (gi + 1 >= d.Length) continue;
                            g = U16(gi); if (g != 0) g = (g + delta) & 0xFFFF;
                        }
                        if (g != 0) codes.Add(ch);
                    }
                }
            }
        }

        string family = "";
        if (tables.TryGetValue("name", out var nm))
        {
            int cnt = U16(nm.off + 2); uint strOff = nm.off + U16(nm.off + 4);
            for (int i = 0; i < cnt; i++)
            {
                long r = nm.off + 6 + i * 12;
                int plat = U16(r), id = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
                if (id != 1 || plat != 3) continue;
                var bytes = new byte[len];
                Array.Copy(d, strOff + off, bytes, 0, len);
                family = Encoding.BigEndianUnicode.GetString(bytes).Trim();
                break;
            }
        }
        return (family, codes);
    }
}
