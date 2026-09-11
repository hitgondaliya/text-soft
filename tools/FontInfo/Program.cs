using System.IO;
using System.Windows.Media;

// FontInfo — identify each font in a folder: family name, script coverage, and
// whether it is Unicode (real code points) or legacy (glyphs at ASCII bytes).
//
//   dotnet run --project tools/FontInfo -- "D:\fonts"

string dir = args.Length > 0 ? args[0] : @"D:\fonts";
var en = System.Globalization.CultureInfo.GetCultureInfo("en-US");

(string name, int lo, int hi)[] blocks =
{
    ("Devanagari", 0x0900, 0x097F), ("Bengali/Assamese", 0x0980, 0x09FF),
    ("Gurmukhi/Punjabi", 0x0A00, 0x0A7F), ("Gujarati", 0x0A80, 0x0AFF),
    ("Oriya", 0x0B00, 0x0B7F), ("Tamil", 0x0B80, 0x0BFF),
    ("Telugu", 0x0C00, 0x0C7F), ("Kannada", 0x0C80, 0x0CFF),
    ("Malayalam", 0x0D00, 0x0D7F),
};

Console.WriteLine($"{"file",-22} {"family",-26} {"glyphs",6} {"asciiGlyphs",11}  script / verdict");
Console.WriteLine(new string('-', 110));

foreach (var f in Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories)
                           .Concat(Directory.GetFiles(dir, "*.otf", SearchOption.AllDirectories))
                           .OrderBy(x => x))
{
    string file = Path.GetFileName(f);
    try
    {
        var gt = new GlyphTypeface(new Uri(Path.GetFullPath(f)));
        string fam = gt.Win32FamilyNames.TryGetValue(en, out var w) && !string.IsNullOrEmpty(w) ? w
                   : gt.FamilyNames.Values.FirstOrDefault() ?? "?";
        var codes = gt.CharacterToGlyphMap.Keys;
        int total = codes.Count;
        int ascii = codes.Count(c => c >= 0x21 && c <= 0xFE);
        // Best Indic block by count of mapped Unicode code points.
        var best = blocks.Select(b => (b.name, n: codes.Count(c => c >= b.lo && c <= b.hi)))
                         .OrderByDescending(x => x.n).First();
        string verdict = best.n >= 20 ? $"UNICODE {best.name} ({best.n})"
                       : ascii >= 60 ? "LEGACY (ascii-mapped)"
                       : "unknown";
        Console.WriteLine($"{file,-22} {Trunc(fam,26),-26} {total,6} {ascii,11}  {verdict}");
    }
    catch (Exception ex) { Console.WriteLine($"{file,-22} <read failed: {ex.Message}>"); }
}

static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";
