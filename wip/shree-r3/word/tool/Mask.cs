namespace WordVis;

/// Binary ink mask with coordinates relative to a pen origin (x right, y down; baseline y=0).
public sealed class Mask
{
    readonly List<(int x, int y)> pts = new();
    readonly HashSet<long> set = new();
    public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = int.MinValue, MaxY = int.MinValue;
    public int Count => pts.Count;
    bool[] grid, dil; int gw, gh;

    static long K(int x, int y) => ((long)x << 32) | (uint)y;

    public void Add(int x, int y)
    {
        if (!set.Add(K(x, y))) return;
        pts.Add((x, y));
        if (x < MinX) MinX = x; if (x > MaxX) MaxX = x; if (y < MinY) MinY = y; if (y > MaxY) MaxY = y;
        grid = null; dil = null;
    }

    public IEnumerable<(int x, int y)> All() => pts;

    void Freeze()
    {
        if (grid != null) return;
        gw = MaxX - MinX + 3; gh = MaxY - MinY + 3;
        grid = new bool[gw * gh]; dil = new bool[gw * gh];
        foreach (var (x, y) in pts) grid[(y - MinY + 1) * gw + (x - MinX + 1)] = true;
        foreach (var (x, y) in pts)
        {
            int cx = x - MinX + 1, cy = y - MinY + 1;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) dil[(cy + dy) * gw + (cx + dx)] = true;
        }
    }

    public bool Has(int x, int y)
    {
        if (Count == 0) return false;
        Freeze();
        int cx = x - MinX + 1, cy = y - MinY + 1;
        return cx >= 0 && cy >= 0 && cx < gw && cy < gh && grid[cy * gw + cx];
    }

    public bool Near(int x, int y)
    {
        if (Count == 0) return false;
        Freeze();
        int cx = x - MinX + 1, cy = y - MinY + 1;
        return cx >= 0 && cy >= 0 && cx < gw && cy < gh && dil[cy * gw + cx];
    }

    public static double IoU(Mask a, Mask b, int dx, int dy)
    {
        if (a.Count == 0 || b.Count == 0) return a.Count == b.Count ? 1 : 0;
        int inter = 0;
        foreach (var (x, y) in b.pts) if (a.Has(x + dx, y + dy)) inter++;
        return inter / (double)(a.Count + b.Count - inter);
    }

    /// F1 of the two masks where a pixel counts as matched if the other mask has ink within 1px.
    public static double TolF1(Mask a, Mask b, int dx, int dy)
    {
        if (a.Count == 0 || b.Count == 0) return a.Count == b.Count ? 1 : 0;
        int pb = 0; foreach (var (x, y) in b.pts) if (a.Near(x + dx, y + dy)) pb++;
        int pa = 0; foreach (var (x, y) in a.pts) if (b.Near(x - dx, y - dy)) pa++;
        double p = pb / (double)b.Count, r = pa / (double)a.Count;
        return p + r == 0 ? 0 : 2 * p * r / (p + r);
    }

    /// Best tolerant F1 over small shifts, aligning either by the pen origin or by the ink bbox.
    public static (double score, int dx, int dy, string how) Best(Mask a, Mask b, int r = 3)
    {
        if (a.Count == 0 || b.Count == 0) return (a.Count == b.Count ? 1 : 0, 0, 0, "empty");
        double best = -1; int bdx = 0, bdy = 0; string how = "";
        foreach (var (ox, oy, h) in new[] { (0, 0, "origin"), (a.MinX - b.MinX, a.MinY - b.MinY, "bbox"), (a.MaxX - b.MaxX, a.MaxY - b.MaxY, "bbox2") })
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    double s = IoU(a, b, ox + dx, oy + dy);
                    if (s > best) { best = s; bdx = ox + dx; bdy = oy + dy; how = h; }
                }
        double f = -1; int fdx = bdx, fdy = bdy;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                double s = TolF1(a, b, bdx + dx, bdy + dy);
                if (s > f) { f = s; fdx = bdx + dx; fdy = bdy + dy; }
            }
        return (f, fdx, fdy, how);
    }

    public double SizeSim(Mask o)
    {
        if (Count == 0 || o.Count == 0) return 0;
        double w1 = MaxX - MinX + 1, h1 = MaxY - MinY + 1, w2 = o.MaxX - o.MinX + 1, h2 = o.MaxY - o.MinY + 1;
        return Math.Min(w1, w2) / Math.Max(w1, w2) * Math.Min(h1, h2) / Math.Max(h1, h2);
    }

    /// Rank every reference glyph against this ink: best gid, its score, and the expected gid's rank.
    public static (int gid, double score, double second) Rank(Dictionary<int, Mask> refs, Mask ink, double sizeFloor = 0.3)
    {
        int bg = -1; double bs = -1, s2 = -1;
        foreach (var kv in refs)
        {
            if (kv.Value.Count == 0 || kv.Value.SizeSim(ink) < sizeFloor) continue;
            double s = Best(kv.Value, ink, 2).score;
            if (s > bs) { s2 = bs; bs = s; bg = kv.Key; }
            else if (s > s2) s2 = s;
        }
        return (bg, bs, s2);
    }
}
