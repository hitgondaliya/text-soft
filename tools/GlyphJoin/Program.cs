using System.IO;
using System.Linq;
using System.Text.Json;
using GlyphJoin;

// GlyphJoin — derive a legacy-byte -> Unicode mapping for a Shree font by joining
// the LEGACY font (byte -> GID) with another font of the SAME typeface through the
// glyph shapes themselves. Shree ships each design number twice: an X/7/0-series
// legacy TTF (S0750810.TTF = SHREE-GUJX-0750) and a Unicode OTF of the same
// typeface (SHREE0750._TF). Matching glyphs by rendered shape gives byte -> Unicode
// ground truth without touching any Shree data file.
//
//   glyphjoin probe <fontA> <fontB>              diagnostics + shape-match rate
//   glyphjoin map   <legacy> <unicode> [--out f] byte -> Unicode table
//   glyphjoin xmap  <fontA> <fontB>  [--out f]   byteA -> byteB table (two legacy fonts)

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: glyphjoin probe|map|xmap <fontA> <fontB> [--out file] [--tol 0.06]");
    Console.Error.WriteLine("       glyphjoin batch [--legacy <dir>] [--uni <dir>] --out-dir <dir> [--tol 0.06] [--limit N]");
    return 1;
}

if (args[0] == "cover")
{
    // cover <raw.json>... — how much of each script's base alphabet a table carries
    foreach (string rp in args.Skip(1))
    {
        var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(rp))!;
        var singles = new HashSet<int>(raw.Values.Where(v => !v.Contains(' ')).Select(v => Convert.ToInt32(v, 16)));
        var blk = singles.Where(c => c is >= 0x0900 and <= 0x0D7F && c is not (0x0964 or 0x0965))
                         .GroupBy(c => (c - 0x0900) / 0x80 * 0x80 + 0x0900)
                         .OrderByDescending(g => g.Count()).Select(g => (int?)g.Key).FirstOrDefault();
        if (blk is null) { Console.WriteLine(Path.GetFileNameWithoutExtension(rp) + ": no Indic block"); continue; }
        int have = Enumerable.Range(blk.Value + 0x15, 0x25).Count(singles.Contains);
        int vowels = Enumerable.Range(blk.Value + 0x05, 0x10).Count(singles.Contains);
        Console.WriteLine(Path.GetFileNameWithoutExtension(rp).PadRight(12) + " block=U+" + blk.Value.ToString("X4")
            + "  consonants " + have + "/37  vowels " + vowels + "/16  entries " + raw.Count);
    }
    return 0;
}

if (args[0] == "chain")
{
    // chain <fontFrom> <fontToSolved> <solvedRaw.json> --out raw.json [--tol 0.06]
    string co = "chain.json"; double ct = 0.06;
    for (int i = 4; i < args.Length; i++)
    {
        if (args[i] == "--out" && i + 1 < args.Length) co = args[++i];
        else if (args[i] == "--tol" && i + 1 < args.Length) ct = double.Parse(args[++i]);
    }
    return Chain.Run(args[1], args[2], args[3], co, ct);
}

if (args[0] == "aligned")
{
    // aligned <units.txt> <converted.txt> --out raw.json [--reverse]
    string ao = "aligned.json"; bool arev = args.Contains("--reverse");
    for (int i = 3; i < args.Length; i++) if (args[i] == "--out" && i + 1 < args.Length) ao = args[++i];
    return Aligned.Run(args[1], args[2], ao, arev);
}

if (args[0] == "keys")
{
    // keys <unicode-capture.json> --out keys.txt [--max N]
    string kf = "keys.txt"; int mx = 0; string? hal = null;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--out" && i + 1 < args.Length) kf = args[++i];
        else if (args[i] == "--max" && i + 1 < args.Length) mx = int.Parse(args[++i]);
        else if (args[i] == "--halant" && i + 1 < args.Length) hal = args[++i];
    }
    return Keys.Run(args[1], kf, mx, hal);
}

if (args[0] == "keystroke")
{
    // keystroke <legacy-capture.json> <unicode-capture.json> --out raw.json
    string ko = "keystroke-map.json";
    for (int i = 3; i < args.Length; i++) if (args[i] == "--out" && i + 1 < args.Length) ko = args[++i];
    return Keystroke.Run(args[1], args[2], ko);
}

if (args[0] == "guess")
{
    // guess <font> --pua E004 --block 0900 [--verify raw.json] [--out raw.json]
    int pua = 0, blk = 0; string? verify = null, o = null;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--pua" && i + 1 < args.Length) pua = Convert.ToInt32(args[++i], 16);
        else if (args[i] == "--block" && i + 1 < args.Length) blk = Convert.ToInt32(args[++i], 16);
        else if (args[i] == "--verify" && i + 1 < args.Length) verify = args[++i];
        else if (args[i] == "--out" && i + 1 < args.Length) o = args[++i];
    }
    return Guess.Run(args[1], pua, blk, verify, o);
}

if (args[0] == "codes")
{
    // codes <font> — print the contiguous runs of code points the cmap maps
    var f0 = Sfnt.Load(args[1]);
    var codes = f0.BestCmap().Keys.OrderBy(c => c).ToArray();
    Console.WriteLine(Path.GetFileName(args[1]) + "  " + codes.Length + " codes");
    int runStart = -1, prev = -2;
    foreach (int c in codes)
    {
        if (c != prev + 1)
        {
            if (runStart >= 0) Console.WriteLine("  " + runStart.ToString("X4") + "-" + prev.ToString("X4") + "  (" + (prev - runStart + 1) + ")");
            runStart = c;
        }
        prev = c;
    }
    if (runStart >= 0) Console.WriteLine("  " + runStart.ToString("X4") + "-" + prev.ToString("X4") + "  (" + (prev - runStart + 1) + ")");
    return 0;
}

if (args[0] == "emit")
{
    // emit <raw.json> --id shree-gujx --unicode-font shruti --out-dir <dir> [--name "..."]
    string encId = "", uniFont = "unicode", dir = ".";
    string? label = null;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--id" && i + 1 < args.Length) encId = args[++i];
        else if (args[i] == "--unicode-font" && i + 1 < args.Length) uniFont = args[++i];
        else if (args[i] == "--out-dir" && i + 1 < args.Length) dir = args[++i];
        else if (args[i] == "--name" && i + 1 < args.Length) label = args[++i];
    }
    if (encId.Length == 0) { Console.Error.WriteLine("emit needs --id"); return 1; }
    return Emit.Run(args[1], encId, uniFont, dir, label, args.Contains("--no-reorder"));
}

if (args[0] == "proof")
{
    // proof <legacyFont> <uniFont> <map.json> <text> --out out.png
    string? png = null;
    for (int i = 5; i < args.Length; i++) if (args[i] == "--out" && i + 1 < args.Length) png = args[++i];
    int rc = 0;
    var th = new System.Threading.Thread(() => rc = Proof.Run(args[1], args[2], args[3], args[4], png ?? "proof.png"));
    th.SetApartmentState(System.Threading.ApartmentState.STA);
    th.Start(); th.Join();
    return rc;
}

if (args[0] == "batch")
{
    string legacyDir = @"C:\Windows\Fonts", uniDir = @"C:\Windows\Fonts", outDirB = "glyphjoin-out";
    double tolB = 0.06; int limit = 0;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--legacy" && i + 1 < args.Length) legacyDir = args[++i];
        else if (args[i] == "--uni" && i + 1 < args.Length) uniDir = args[++i];
        else if (args[i] == "--out-dir" && i + 1 < args.Length) outDirB = args[++i];
        else if (args[i] == "--tol" && i + 1 < args.Length) tolB = double.Parse(args[++i]);
        else if (args[i] == "--limit" && i + 1 < args.Length) limit = int.Parse(args[++i]);
    }
    return Batch.Run(legacyDir, uniDir, outDirB, tolB, limit);
}

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: glyphjoin probe|map|xmap <fontA> <fontB> [--out file] [--tol 0.06]");
    return 1;
}

string mode = args[0];
string pathA = args[1], pathB = args[2];
string? outPath = null;
double tol = 0.06;
for (int i = 3; i < args.Length; i++)
{
    if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];
    else if (args[i] == "--tol" && i + 1 < args.Length) tol = double.Parse(args[++i]);
}

var a = Sfnt.Load(pathA);
var b = Sfnt.Load(pathB);

if (mode == "probe")
{
    foreach (var (label, f) in new[] { ("A", a), ("B", b) })
    {
        Console.WriteLine(label + ": " + Path.GetFileName(f.FilePath) + "   family=" + f.FamilyName);
        Console.WriteLine("   sfnt=" + f.SfntTag + " numGlyphs=" + f.NumGlyphs
            + " outlines=" + (f.HasGlyf ? "glyf" : f.HasCff ? "CFF" : "?"));
        Console.WriteLine("   cmap: " + string.Join(", ", f.CmapSubtables.Select(s => "(" + s.plat + "," + s.enc + ") fmt" + s.fmt + " n=" + s.count)));
    }

    var ra = new Raster(pathA);
    var rb = new Raster(pathB);
    var m = Raster.Match(ra, rb, tol, out int amb);
    int inkA = Enumerable.Range(0, ra.GlyphCount).Count(g => ra[g] is not null);
    Console.WriteLine();
    Console.WriteLine("inked glyphs: A=" + inkA + "  B=" + Enumerable.Range(0, rb.GlyphCount).Count(g => rb[g] is not null));
    Console.WriteLine("shape matches: " + m.Count + "/" + inkA + " (" + (100.0 * m.Count / Math.Max(1, inkA)).ToString("F1") + "%)  ambiguous=" + amb);
    return 0;
}

var mapA = a.BestCmap();                 // code -> gid
var mapB = b.BestCmap();
// target glyph -> the Unicode STRING that produces it (cmap + GSUB ligatures)
var gidToSeqB = Gsub.GlyphSequences(b, mapB, out string gsubDiag);
Console.WriteLine(gsubDiag);

var rasA = new Raster(pathA);
var rasB = new Raster(pathB);
var gidMap = Raster.Match(rasA, rasB, tol, out int ambiguous);

var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
int mapped = 0, noGlyph = 0, noCode = 0;
foreach (var kv in mapA.OrderBy(k => k.Key))
{
    if (!gidMap.TryGetValue(kv.Value, out int gb)) { noGlyph++; continue; }
    if (!gidToSeqB.TryGetValue(gb, out string? s) || s is null) { noCode++; continue; }
    table[kv.Key.ToString("X4")] = string.Join(" ", s.EnumerateRunes().Select(r => r.Value.ToString("X4")));
    mapped++;
}

Console.WriteLine("source codes=" + mapA.Count + "  mapped=" + mapped
    + "  no-shape-match=" + noGlyph + "  shape-matched-but-target-glyph-unencoded=" + noCode
    + "  ambiguous=" + ambiguous);

string json = JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true });
if (outPath is null) Console.WriteLine(json);
else { File.WriteAllText(outPath, json); Console.WriteLine("wrote " + outPath + " (" + table.Count + " entries)"); }
return 0;
