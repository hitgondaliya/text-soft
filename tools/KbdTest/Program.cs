using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

// KbdTest — check the keyboard side of the product, which the font matrix never
// touches: is every key of every layout actually reachable, and does phonetic
// typing produce the right word?
//
// The layouts were decoded from Shree-Lipi's 406-byte scheme files by joining them
// against our verified InScript layout (tools/KbdGen). That join can only explain a
// glyph byte InScript itself uses, and KbdGen drops any key it cannot explain — so
// a layout can look fine and still be missing letters. This counts what was lost,
// per layout, against the scheme file it came from.
//
//   dotnet run --project tools/KbdTest -- ["C:\Shree-Lipi nxt"]

string shree = args.Length > 0 ? args[0] : @"C:\Shree-Lipi nxt";
var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();

// US keyboard: virtual key -> the unshifted and shifted ASCII character
var keys = new List<(int Vk, char Plain, char Shift)>();
for (char c = 'A'; c <= 'Z'; c++) keys.Add((c, char.ToLower(c), c));
void K(int vk, char n, char s) => keys.Add((vk, n, s));
K(0x31, '1', '!'); K(0x32, '2', '@'); K(0x33, '3', '#'); K(0x34, '4', '$'); K(0x35, '5', '%');
K(0x36, '6', '^'); K(0x37, '7', '&'); K(0x38, '8', '*'); K(0x39, '9', '('); K(0x30, '0', ')');
K(0xBA, ';', ':'); K(0xBB, '=', '+'); K(0xBC, ',', '<'); K(0xBD, '-', '_'); K(0xBE, '.', '>');
K(0xBF, '/', '?'); K(0xC0, '`', '~'); K(0xDB, '[', '{'); K(0xDD, ']', '}'); K(0xDE, '\'', '"');

Console.WriteLine("=== keyboard layout completeness ===");
Console.WriteLine("(keys the Shree scheme file defines, against keys our layout carries)\n");

int totalDefined = 0, totalCarried = 0, badLayouts = 0;
var worst = new List<(string Lang, string Layout, int Defined, int Carried)>();

foreach (var lang in bundle.Languages)
{
    foreach (var layoutRef in bundle.LayoutsFor(lang.Id))
    {
        KeyboardLayout layout;
        try { layout = bundle.GetLayout(layoutRef); }
        catch { continue; }

        // Which scheme file did this layout come from? The generator records it.
        string? scheme = SchemeFileFor(layoutRef.Id, shree);
        if (scheme is null) continue;
        byte[] d = File.ReadAllBytes(scheme);

        int defined = 0, carried = 0;
        foreach (var (vk, plain, shift) in keys)
        {
            var m = layout.Find(vk);
            if (plain < d.Length && d[plain] != 0xFF) { defined++; if (!string.IsNullOrEmpty(m?.Base)) carried++; }
            if (shift < d.Length && d[shift] != 0xFF) { defined++; if (!string.IsNullOrEmpty(m?.Shift)) carried++; }
        }
        totalDefined += defined;
        totalCarried += carried;
        if (carried < defined)
        {
            badLayouts++;
            worst.Add((lang.Name, layoutRef.Name, defined, carried));
        }
    }
}

foreach (var w in worst.OrderBy(w => (double)w.Carried / w.Defined).Take(15))
    Console.WriteLine($"  {w.Lang,-11} {w.Layout,-16} {w.Carried}/{w.Defined} keys  ({w.Defined - w.Carried} missing)");
Console.WriteLine($"\n  layouts with missing keys: {badLayouts}");
Console.WriteLine($"  keys carried overall:      {totalCarried}/{totalDefined}"
                + $"  ({100.0 * totalCarried / Math.Max(1, totalDefined):F1}%)");

// --- phonetic typing -------------------------------------------------------
Console.WriteLine("\n=== phonetic 'type by sound' ===");
var words = new (string Lang, string Roman, string Expect)[]
{
    ("Gujarati", "nishaant", "નિશાન્ત"),
    ("Gujarati", "nishaaMt", "નિશાંત"),
    ("Gujarati", "gujaraatii", "ગુજરાતી"),
    ("Gujarati", "kamal", "કમલ"),
    ("Gujarati", "bhaarat", "ભારત"),
    ("Hindi", "namaste", "नमस्ते"),
    ("Hindi", "bhaarat", "भारत"),
};
int pOk = 0, pTotal = 0;
foreach (var lang in bundle.Languages)
{
    var tr = bundle.GetTransliterator(lang);
    if (tr is null) { Console.WriteLine($"  {lang.Name,-11} no phonetic scheme"); continue; }
    foreach (var w in words.Where(w => w.Lang == lang.Name))
    {
        pTotal++;
        string got = tr.Convert(w.Roman);
        bool ok = got == w.Expect;
        if (ok) pOk++;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {lang.Name,-11} \"{w.Roman}\" -> {got}" + (ok ? "" : $"   expected {w.Expect}"));
    }
}
Console.WriteLine($"\n  phonetic {pOk}/{pTotal}");

return badLayouts == 0 && pOk == pTotal ? 0 : 1;

/// The scheme file a generated layout was decoded from: our ids are
/// "&lt;prefix&gt;-&lt;scheme&gt;" and the files are "&lt;SCHEME&gt;.&lt;EXT&gt;".
static string? SchemeFileFor(string layoutId, string shreeDir)
{
    var ext = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dev"] = "DEV", ["san"] = "SAN", ["guj"] = "GUJ", ["pun"] = "PUN", ["ban"] = "BAN",
        ["tel"] = "TEL", ["kan"] = "KAN", ["mal"] = "MAL", ["ori"] = "ORI", ["tam"] = "TAM",
        ["ass"] = "ASS",
    };
    int dash = layoutId.IndexOf('-');
    if (dash <= 0) return null;
    if (!ext.TryGetValue(layoutId[..dash], out string? e)) return null;
    string scheme = layoutId[(dash + 1)..].Replace("-", "");
    foreach (string f in Directory.EnumerateFiles(shreeDir, "*." + e))
        if (string.Equals(Path.GetFileNameWithoutExtension(f), scheme, StringComparison.OrdinalIgnoreCase))
            return f;
    return null;
}
