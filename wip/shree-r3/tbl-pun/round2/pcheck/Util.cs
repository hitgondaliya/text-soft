using System.Text;
using System.Text.Json.Nodes;

static class U
{
    public static readonly Encoding Cp1252;
    static U()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    /// <summary>Legacy text as hex bytes: Latin-1 range (not 80-9F) is its own byte, cp1252 specials their byte, else U+XXXX.</summary>
    public static string Hex(string s)
    {
        var parts = new List<string>(s.Length);
        foreach (char c in s)
        {
            if (c < 0x80 || (c >= 0xA0 && c <= 0xFF)) { parts.Add(((int)c).ToString("X2")); continue; }
            try
            {
                var b = Cp1252.GetBytes(c.ToString());
                if (b.Length == 1 && b[0] >= 0x80 && b[0] <= 0x9F) { parts.Add(b[0].ToString("X2")); continue; }
            }
            catch (EncoderFallbackException) { }
            parts.Add("U+" + ((int)c).ToString("X4"));
        }
        return string.Join(" ", parts);
    }

    public static string FromHex(string hex)
    {
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (h.StartsWith("U+")) { sb.Append((char)Convert.ToInt32(h[2..], 16)); continue; }
            int b = Convert.ToInt32(h, 16);
            if (b >= 0x80 && b <= 0x9F)
            {
                try { sb.Append(Cp1252.GetString(new[] { (byte)b })); }
                catch (DecoderFallbackException) { sb.Append((char)b); }
            }
            else sb.Append((char)b);
        }
        return sb.ToString();
    }

    public static string Norm(string hex) => string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    public static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    public static bool IsGurmukhi(char c) => c >= '਀' && c <= '੿';
    public static bool IsCons(char c) => (c >= 'ਕ' && c <= 'ਹ') || (c >= 'ਖ਼' && c <= 'ਫ਼');
    public static bool IsMarkLike(char c) => (c >= 'ਁ' && c <= 'ਃ') || (c >= '਼' && c <= 'ੑ') || c == 'ੰ' || c == 'ੱ' || c == 'ੵ' || c == '‌' || c == '‍';

    /// <summary>Characters that must never come out of a reverse table: Latin letters, controls, C1, PUA, noncharacters, U+FFFD.</summary>
    public static List<string> BadChars(string u)
    {
        var bad = new List<string>();
        foreach (char c in u)
        {
            if (c < 0x20 && c is not ('\t' or '\n' or '\r')) bad.Add($"control U+{(int)c:X4}");
            else if (c >= 0x7F && c <= 0x9F) bad.Add($"C1 U+{(int)c:X4}");
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) bad.Add($"ASCII letter {c}");
            else if (c >= 0xA1 && c <= 0x24F && c != '×' && c != '÷') bad.Add($"Latin U+{(int)c:X4} {c}");
            else if (c >= 0xE000 && c <= 0xF8FF) bad.Add($"PUA U+{(int)c:X4}");
            else if ((c >= 0xFDD0 && c <= 0xFDEF) || c == 0xFFFE || c == 0xFFFF) bad.Add($"nonchar U+{(int)c:X4}");
            else if (c == 0xFFFD) bad.Add("U+FFFD");
        }
        return bad;
    }

    public static JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8));
}

/// <summary>A minimal TrueType cmap reader (format 4 and 12 of the (3,1)/(3,10)/(3,0) subtables).</summary>
static class Cmap
{
    public static HashSet<int> Read(string path, out string which)
    {
        var d = File.ReadAllBytes(path);
        int U16(int o) => (d[o] << 8) | d[o + 1];
        uint U32(int o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
        int numTables = U16(4);
        int cmapOff = -1;
        for (int i = 0; i < numTables; i++)
        {
            int r = 12 + 16 * i;
            string tag = Encoding.ASCII.GetString(d, r, 4);
            if (tag == "cmap") cmapOff = (int)U32(r + 8);
        }
        if (cmapOff < 0) throw new InvalidDataException("no cmap in " + path);
        int n = U16(cmapOff + 2);
        int best = -1, bestRank = -1; which = "";
        for (int i = 0; i < n; i++)
        {
            int r = cmapOff + 4 + 8 * i;
            int pid = U16(r), eid = U16(r + 2); int off = (int)U32(r + 4);
            int rank = (pid, eid) switch { (3, 10) => 3, (3, 1) => 2, (3, 0) => 1, _ => 0 };
            if (rank > bestRank) { bestRank = rank; best = cmapOff + off; which = $"({pid},{eid})"; }
        }
        var set = new HashSet<int>();
        int fmt = U16(best);
        if (fmt == 4)
        {
            int segX2 = U16(best + 6);
            int ends = best + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int s = 0; s < segX2 / 2; s++)
            {
                int end = U16(ends + 2 * s), start = U16(starts + 2 * s);
                short delta = (short)U16(deltas + 2 * s); int ro = U16(ranges + 2 * s);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int gid;
                    if (ro == 0) gid = (c + delta) & 0xFFFF;
                    else
                    {
                        int gi = ranges + 2 * s + ro + 2 * (c - start);
                        gid = U16(gi); if (gid != 0) gid = (gid + delta) & 0xFFFF;
                    }
                    if (gid != 0) set.Add(c);
                }
            }
        }
        else if (fmt == 12)
        {
            uint groups = U32(best + 12);
            for (int g = 0; g < groups; g++)
            {
                int r = best + 16 + 12 * g;
                uint s = U32(r), e = U32(r + 4);
                for (uint c = s; c <= e; c++) set.Add((int)c);
            }
        }
        else throw new InvalidDataException($"cmap format {fmt} in {path}");
        return set;
    }
}
