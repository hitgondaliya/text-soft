using System.Text;

/// <summary>Reads the (3,1) cmap and the family name of an installed TrueType face, independently of the generator.</summary>
static class Ttf
{
    public static readonly string[] Files =
    {
        "S0600892.TTF", "S0601892.TTF", "S0602892.TTF", "S0603892.TTF", "S0604892.TTF", "WORI7001.TTF", "WORI7002.TTF",
        "S06000F2.TTF", "S06010F2.TTF", "S06020F2.TTF", "S06030F2.TTF", "S06040F2.TTF",
    };

    public sealed record Face(string File, string Family, HashSet<int> Cmap, Dictionary<int, int> Glyph);

    static List<Face> _faces;
    public static List<Face> Faces => _faces ??= Files.Select(f => Read(Path.Combine(@"C:\Windows\Fonts", f))).ToList();

    public static HashSet<int> Union => Faces.SelectMany(f => f.Cmap).ToHashSet();
    public static HashSet<int> Intersection
    {
        get { var s = new HashSet<int>(Faces[0].Cmap); foreach (var f in Faces.Skip(1)) s.IntersectWith(f.Cmap); return s; }
    }

    static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
    static short S16(byte[] b, int o) => (short)((b[o] << 8) | b[o + 1]);
    static int U32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    public static Face Read(string path)
    {
        var b = File.ReadAllBytes(path);
        int n = U16(b, 4);
        var tables = new Dictionary<string, (int Off, int Len)>();
        for (int i = 0; i < n; i++)
        {
            int o = 12 + 16 * i;
            tables[Encoding.ASCII.GetString(b, o, 4)] = (U32(b, o + 8), U32(b, o + 12));
        }
        var glyph = new Dictionary<int, int>();
        int c = tables["cmap"].Off, nt = U16(b, c + 2);
        for (int i = 0; i < nt; i++)
        {
            int pid = U16(b, c + 4 + 8 * i), eid = U16(b, c + 6 + 8 * i), so = c + U32(b, c + 8 + 8 * i);
            if (pid != 3 || eid != 1 || U16(b, so) != 4) continue;
            int segX2 = U16(b, so + 6), seg = segX2 / 2;
            int ends = so + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ros = deltas + segX2;
            for (int s = 0; s < seg; s++)
            {
                int e = U16(b, ends + 2 * s), st = U16(b, starts + 2 * s), d = S16(b, deltas + 2 * s), ro = U16(b, ros + 2 * s);
                for (int ch = st; ch <= e && ch != 0xFFFF; ch++)
                {
                    int g;
                    if (ro == 0) g = (ch + d) & 0xFFFF;
                    else { g = U16(b, ros + 2 * s + ro + 2 * (ch - st)); if (g != 0) g = (g + d) & 0xFFFF; }
                    if (g != 0) glyph[ch] = g;
                }
            }
        }
        // family name (name id 1, Windows platform)
        string family = "?";
        var (no, _) = tables["name"];
        int count = U16(b, no + 2), strOff = no + U16(b, no + 4);
        for (int i = 0; i < count; i++)
        {
            int r = no + 6 + 12 * i;
            int pid = U16(b, r), nid = U16(b, r + 6), len = U16(b, r + 8), off = U16(b, r + 10);
            if (pid == 3 && nid == 1) { family = Encoding.BigEndianUnicode.GetString(b, strOff + off, len); break; }
        }
        return new Face(Path.GetFileName(path), family, glyph.Keys.ToHashSet(), glyph);
    }
}
