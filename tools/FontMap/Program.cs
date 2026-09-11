using System.IO;
using System.Text;

// FontMap — read a TTF and dump byte -> glyph-name using the cmap (byte->GID) and
// post (GID->name) tables. This gauges whether a legacy font can be auto-mapped:
// if the glyph names are meaningful, we can derive byte -> Unicode automatically.
//
//   dotnet run --project tools/FontMap -- "<font.ttf>"

if (args.Length < 1) { Console.Error.WriteLine("usage: fontmap <font.ttf>"); return 1; }
byte[] d = File.ReadAllBytes(args[0]);

// ---- big-endian readers ----
int P = 0;
ushort U16(int o) => (ushort)((d[o] << 8) | d[o + 1]);
uint U32(int o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
short S16(int o) => (short)U16(o);

// ---- table directory ----
ushort numTables = U16(4);
var tables = new Dictionary<string, (uint off, uint len)>();
for (int i = 0; i < numTables; i++)
{
    int rec = 12 + i * 16;
    string tag = Encoding.ASCII.GetString(d, rec, 4);
    tables[tag] = (U32(rec + 8), U32(rec + 12));
}
// ---- cmap via WPF GlyphTypeface (handles every subtable format reliably) ----
var gt = new System.Windows.Media.GlyphTypeface(new Uri(Path.GetFullPath(args[0])));
var code2gid = new Dictionary<int, ushort>();
foreach (var kv in gt.CharacterToGlyphMap) code2gid[kv.Key] = kv.Value;
ushort format = 0; // (unused now; cmap resolved by WPF)

// ---- post: GID -> name (format 2.0) ----
var gid2name = new Dictionary<int, string>();
if (tables.TryGetValue("post", out var post))
{
    uint po = post.off; uint ver = U32((int)po);
    if (ver == 0x00020000)
    {
        int o = (int)po + 32; ushort num = U16(o); o += 2;
        var idx = new ushort[num];
        for (int i = 0; i < num; i++) { idx[i] = U16(o); o += 2; }
        var names = new List<string>();
        int end = (int)(po + post.len);
        while (o < end) { int len = d[o++]; if (o + len > d.Length) break; names.Add(Encoding.ASCII.GetString(d, o, len)); o += len; }
        for (int g = 0; g < num; g++)
        {
            int ix = idx[g];
            gid2name[g] = ix >= 258 ? (ix - 258 < names.Count ? names[ix - 258] : $"g{g}") : MacGlyphs.Name(ix);
        }
    }
}

// ---- report ----
int printable = 0, named = 0, junk = 0;
var sb = new StringBuilder();
for (int c = 0x21; c <= 0xFE; c++)
{
    if (!code2gid.TryGetValue(c, out var gid) || gid == 0) continue;
    printable++;
    string nm = gid2name.TryGetValue(gid, out var n) ? n : $"gid{gid}";
    bool junky = nm.Length == 0 || nm.StartsWith("g") && nm.Length > 1 && char.IsDigit(nm[1]) || nm.StartsWith("glyph") || nm == $"gid{gid}" || nm == ".notdef" || nm.StartsWith("uni") == false && nm.All(ch => char.IsDigit(ch));
    if (!junky) named++; else junk++;
    if (c < 0x21 + 90)
        sb.AppendLine($"  0x{c:X2} '{(char)c}' -> gid {gid,-4} name '{nm}'");
}
Console.WriteLine($"cmap via WPF ({code2gid.Count} codes)  post={(gid2name.Count>0?"yes":"no")}");
Console.WriteLine(sb.ToString());
Console.WriteLine($"printable bytes mapped: {printable}   meaningful names: {named}   junk/none: {junk}");
Console.WriteLine(named * 100 / Math.Max(1, printable) + "% have usable glyph names => auto-map " + (named * 100 / Math.Max(1, printable) >= 60 ? "FEASIBLE" : "NOT reliable"));
return 0;

static class MacGlyphs
{
    // Minimal standard Mac glyph order (first entries); enough to flag named vs junk.
    static readonly string[] N = { ".notdef", ".null", "nonmarkingreturn", "space", "exclam", "quotedbl", "numbersign", "dollar", "percent", "ampersand", "quotesingle", "parenleft", "parenright", "asterisk", "plus", "comma", "hyphen", "period", "slash", "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "colon", "semicolon", "less", "equal", "greater", "question", "at", "A", "B", "C" };
    public static string Name(int i) => i < N.Length ? N[i] : $"mac{i}";
}
