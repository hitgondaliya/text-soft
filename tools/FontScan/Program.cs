using System.Security.Cryptography;
using System.Text;

// FontScan — enumerate the DISTINCT legacy encodings ("layouts") in a folder of
// TrueType fonts. Design/italic/weight variants of one Shree font share the same
// byte->glyph layout, so the converter needs only ONE mapping table per layout,
// not per font. This tool groups every TTF by the exact set of codepoints its
// (3,1) Unicode cmap maps, and prints which fonts belong to each group.
//
//   dotnet run --project tools/FontScan -- "D:\font software\FONTS\Default" [--recurse] [--csv out.csv]

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: fontscan <font-folder> [more-folders...] [--recurse] [--csv <file>]");
    return 1;
}

bool recurse = false;
string? csv = null;
var roots = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--recurse": recurse = true; break;
        case "--csv": csv = i + 1 < args.Length ? args[++i] : null; break;
        default: roots.Add(args[i]); break;
    }
}

var files = new List<string>();
foreach (var root in roots)
{
    if (File.Exists(root)) { files.Add(root); continue; }
    if (!Directory.Exists(root)) { Console.Error.WriteLine($"skip (not found): {root}"); continue; }
    var opt = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    foreach (var pat in new[] { "*.ttf", "*.otf", "*._tf" })
        files.AddRange(Directory.GetFiles(root, pat, opt));
}
files = files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToList();

// hash of codepoint set -> group
var groups = new Dictionary<string, LayoutGroup>(StringComparer.Ordinal);
int ok = 0, skipped = 0;
foreach (var f in files)
{
    Cmap? c;
    try { c = TtfReader.ReadCmap31(f); }
    catch { c = null; }
    if (c is null) { skipped++; continue; }
    ok++;

    string hash = c.SetHash;
    if (!groups.TryGetValue(hash, out var g))
    {
        g = new LayoutGroup { Hash = hash.Substring(0, 10), Ascii = c.AsciiCount, Pua = c.PuaCount, Other = c.OtherCount };
        groups[hash] = g;
    }
    g.Fonts.Add(new FontEntry(TtfReader.FamilyName(f) ?? Path.GetFileNameWithoutExtension(f), f));
}

Console.WriteLine($"Fonts read: {ok}   skipped (no (3,1) cmap / unreadable): {skipped}");
Console.WriteLine($"Distinct legacy layouts (codepoint-set groups): {groups.Count}\n");

int idx = 0;
foreach (var g in groups.Values.OrderByDescending(x => x.Fonts.Count))
{
    idx++;
    Console.WriteLine($"Layout #{idx}  [{g.Hash}]  ASCII={g.Ascii} PUA={g.Pua} other={g.Other}  ({g.Fonts.Count} fonts)");
    foreach (var fe in g.Fonts.OrderBy(x => x.Family))
        Console.WriteLine($"    {fe.Family,-28}  {Path.GetFileName(fe.Path)}");
    Console.WriteLine();
}

if (csv is not null)
{
    var sb = new StringBuilder("layout_hash,ascii,pua,other,family,file\n");
    idx = 0;
    foreach (var g in groups.Values.OrderByDescending(x => x.Fonts.Count))
    {
        idx++;
        foreach (var fe in g.Fonts.OrderBy(x => x.Family))
            sb.AppendLine($"{g.Hash},{g.Ascii},{g.Pua},{g.Other},\"{fe.Family}\",\"{fe.Path}\"");
    }
    File.WriteAllText(csv, sb.ToString());
    Console.WriteLine($"CSV written: {csv}");
}
return 0;

sealed class FontEntry
{
    public string Family { get; }
    public string Path { get; }
    public FontEntry(string family, string path) { Family = family; Path = path; }
}

sealed class LayoutGroup
{
    public string Hash = "";
    public int Ascii, Pua, Other;
    public List<FontEntry> Fonts = new();
}

sealed class Cmap
{
    public required string SetHash;
    public int AsciiCount, PuaCount, OtherCount;
}

static class TtfReader
{
    static ushort U16(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
    static uint U32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

    static int FindTable(byte[] b, string tag)
    {
        int nt = U16(b, 4);
        for (int i = 0; i < nt; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > b.Length) break;
            if (Encoding.ASCII.GetString(b, rec, 4) == tag)
                return (int)U32(b, rec + 8);
        }
        return -1;
    }

    /// <summary>Read the (3,1) Unicode BMP cmap and summarise the codepoint set.</summary>
    public static Cmap? ReadCmap31(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (b.Length < 12) return null;
        int cmap = FindTable(b, "cmap");
        if (cmap < 0) return null;

        int nSub = U16(b, cmap + 2);
        int sub = -1;
        for (int i = 0; i < nSub; i++)
        {
            int r = cmap + 4 + i * 8;
            if (U16(b, r) == 3 && U16(b, r + 2) == 1) sub = cmap + (int)U32(b, r + 4);
        }
        if (sub < 0) return null;
        if (U16(b, sub) != 4) return null; // only format 4 expected for these fonts

        int segX2 = U16(b, sub + 6), segs = segX2 / 2;
        int endO = sub + 14, startO = endO + segX2 + 2;

        int ascii = 0, pua = 0, other = 0;
        var sb = new StringBuilder();
        for (int k = 0; k < segs; k++)
        {
            int end = U16(b, endO + k * 2), st = U16(b, startO + k * 2);
            if (end == 0xFFFF) continue;
            sb.Append(st).Append('-').Append(end).Append(';');
            for (int cp = st; cp <= end; cp++)
            {
                if (cp >= 0x20 && cp <= 0xFF) ascii++;
                else if (cp >= 0xE000 && cp <= 0xF8FF) pua++;
                else other++;
            }
        }
        byte[] hash = MD5.HashData(Encoding.ASCII.GetBytes(sb.ToString()));
        return new Cmap { SetHash = Convert.ToHexString(hash), AsciiCount = ascii, PuaCount = pua, OtherCount = other };
    }

    /// <summary>Best-effort font family name (name id 1), else full name (id 4).</summary>
    public static string? FamilyName(string path)
    {
        try
        {
            byte[] b = File.ReadAllBytes(path);
            int name = FindTable(b, "name");
            if (name < 0) return null;
            int count = U16(b, name + 2), storage = name + U16(b, name + 4);
            string? id1 = null, id4 = null;
            for (int i = 0; i < count; i++)
            {
                int r = name + 6 + i * 12;
                int nid = U16(b, r + 6), len = U16(b, r + 8), off = U16(b, r + 10);
                if (len == 0 || storage + off + len > b.Length) continue;
                if (nid != 1 && nid != 4) continue;
                string s = Encoding.BigEndianUnicode.GetString(b, storage + off, len);
                if (string.IsNullOrEmpty(s) || s[0] == '\0') s = Encoding.ASCII.GetString(b, storage + off, len);
                s = s.Trim('\0', ' ');
                if (nid == 1 && id1 is null) id1 = s;
                if (nid == 4 && id4 is null) id4 = s;
            }
            return id1 ?? id4;
        }
        catch { return null; }
    }
}
