using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Fonts;

// FullTest — walk the WHOLE product surface the user actually touches:
// every language x font layout x keyboard layout, and for legacy layouts every
// font registered under that encoding.
//
// For each combination it presses every key of the layout, pushes the result
// through the same converter the typing engine uses, and then asks the question
// that actually matters: does the code it produced exist in the font that will
// draw it? A table can round-trip perfectly and still render nothing, because the
// font never had that code. That check needs the installed font file, so the fonts
// directory is scanned once and indexed by family name.
//
//   dotnet run --project tools/FullTest [-- --fonts C:\Windows\Fonts] [--verbose]

// Fonts installed for the current user only live in a second folder — a font
// registered in the config would otherwise be reported as "not installed".
string[] fontDirs =
{
    @"C:\Windows\Fonts",
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
};
bool verbose = false;
for (int i = 0; i < args.Length; i++)
{
    // --fonts may be repeated: fonts a customer owns often sit in their own folder
    // before being installed, and a run has to be able to see those too.
    if (args[i] == "--fonts" && i + 1 < args.Length) fontDirs = fontDirs.Append(args[++i]).ToArray();
    else if (args[i] == "--only-fonts" && i + 1 < args.Length) fontDirs = new[] { args[++i] };
    else if (args[i] == "--verbose") verbose = true;
}

var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
Console.WriteLine("Loaded " + bundle.Languages.Count + " languages, " + bundle.MappingTables.Count + " mapping tables.");

var cmaps = FontIndex.Build(fontDirs);
Console.WriteLine("Indexed " + cmaps.Count + " installed font families from " + string.Join(" + ", fontDirs));
Console.WriteLine();

var problems = new List<string>();
int comboOk = 0, comboTotal = 0;

foreach (var lang in bundle.Languages)
{
    var layouts = bundle.LayoutsFor(lang.Id);
    Console.WriteLine("=== " + lang.Name + "  (" + lang.FontLayouts.Count + " font layouts, " + layouts.Count + " keyboard layouts)");

    foreach (var fl in lang.FontLayouts)
    {
        // 1. is a font wired to this dropdown entry at all?
        if (string.IsNullOrWhiteSpace(fl.FontId))
        {
            Console.WriteLine("  [ -- ] " + fl.Name.PadRight(26) + "no font wired (dropdown entry does nothing)");
            problems.Add(lang.Name + " / " + fl.Name + ": fontLayout has no fontId");
            continue;
        }

        var font = lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId);
        if (font is null)
        {
            Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "fontId '" + fl.FontId + "' is not a font of this language");
            problems.Add(lang.Name + " / " + fl.Name + ": fontId '" + fl.FontId + "' not found");
            continue;
        }

        // 2. the font layout claims a type — does the font agree?
        bool wantLegacy = fl.Type == FontType.Legacy;
        if (wantLegacy != (font.Type == FontType.Legacy))
        {
            Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "type mismatch: layout says " + fl.Type.ToString() + ", font '" + font.Id + "' is " + font.Type);
            problems.Add(lang.Name + " / " + fl.Name + ": type mismatch with font " + font.Id);
            continue;
        }

        FontConverter? conv = null;
        if (wantLegacy)
        {
            conv = bundle.CreateConverterToLegacy(font.ConversionKey);
            if (conv is null)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "no Unicode -> " + font.ConversionKey + " table");
                problems.Add(lang.Name + " / " + fl.Name + ": no mapping table for " + font.ConversionKey);
                continue;
            }
        }

        // every font that shares this encoding is offered in the same dropdown
        var sameEncoding = wantLegacy
            ? lang.Fonts.Where(f => f.Type == FontType.Legacy && f.ConversionKey == font.ConversionKey).ToList()
            : new List<FontAsset> { font };

        foreach (var kb in layouts)
        {
            comboTotal++;
            KeyboardLayout kl;
            try { kl = bundle.GetLayout(kb); }
            catch (Exception ex)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + "layout will not load: " + ex.Message);
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": layout load failed");
                continue;
            }

            // press every key, base and shift
            var produced = new List<string>();
            foreach (var k in kl.Keys)
            {
                if (!string.IsNullOrEmpty(k.Base)) produced.Add(k.Base!);
                if (!string.IsNullOrEmpty(k.Shift)) produced.Add(k.Shift!);
                if (!string.IsNullOrEmpty(k.AltGr)) produced.Add(k.AltGr!);
            }
            if (produced.Count == 0)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + "layout has no keys");
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": empty layout");
                continue;
            }

            var unmapped = new SortedSet<string>(StringComparer.Ordinal);
            var outChars = new SortedSet<char>();
            foreach (string u in produced)
            {
                string outText = u;
                if (conv is not null)
                {
                    var r = conv.Convert(u);
                    outText = r.Text;
                    // plain ASCII that maps to itself is not a gap — a comma stays a
                    // comma in a legacy font, so only flag real script characters
                    foreach (string c in r.UnmappedCharacters)
                        if (c.Any(ch => ch >= 0x80) && !c.All(ch => EncodingLimits.Covers(font.ConversionKey, ch))) unmapped.Add(c);
                }
                // A character the encoding cannot express comes back unchanged, so
                // it would then fail the cmap check as well — count it once, as a
                // known limit of the encoding, not twice as a missing glyph.
                foreach (char c in outText) if (c >= 0x20 && !EncodingLimits.Covers(font.ConversionKey, c)) outChars.Add(c);
            }

            // does every produced code exist in the fonts that would draw it?
            var missingByFont = new List<string>();
            foreach (var f in sameEncoding)
            {
                if (!cmaps.TryGetValue(f.Family, out var codes))
                {
                    missingByFont.Add(f.Family + " (not installed)");
                    continue;
                }
                // Name the codes, not just the count: "missing 1/70" gives nothing
                // to act on, while the code points say which glyph the table is
                // asking a font for that the font never had.
                var gone = outChars.Where(c => !codes.Contains(c)).ToList();
                if (gone.Count > 0)
                    missingByFont.Add(f.Family + " missing " + string.Join(",", gone.Select(c => "U+" + ((int)c).ToString("X4"))) + " of " + outChars.Count);
            }

            bool ok = unmapped.Count == 0 && missingByFont.Count == 0;
            if (ok) comboOk++;

            if (!ok || verbose)
            {
                string flag = ok ? "  ok  " : "[FAIL]";
                string detail = ok ? produced.Count + " keys -> " + outChars.Count + " codes, all present"
                    : (unmapped.Count > 0 ? "unmapped[" + string.Join("", unmapped) + "] " : "")
                      + (missingByFont.Count > 0 ? string.Join("; ", missingByFont.Take(3)) : "");
                Console.WriteLine("  " + flag + " " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + detail);
            }
            if (!ok)
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": "
                    + (unmapped.Count > 0 ? unmapped.Count + " unmapped " : "")
                    + (missingByFont.Count > 0 ? string.Join("; ", missingByFont.Take(2)) : ""));
        }
    }
    Console.WriteLine();
}

// Every family the config lists, checked against what is actually on this machine.
// A font listed under a name that is one character off ("B Bharati AbmarTwo Normal"
// for "B Bharati AbmarTwo", "Akruti Guj Archana" for "AkrutiGujArchana") tells a
// customer who owns the font that they do not have it, and nothing else catches it
// because the entry looks perfectly well-formed.
var listedFamilies = bundle.Languages.SelectMany(l => l.Fonts).Select(f => f.Family)
    .Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
var absent = listedFamilies.Where(f => !cmaps.ContainsKey(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
Console.WriteLine("font families listed: " + listedFamilies.Count + ", present here: " + (listedFamilies.Count - absent.Count));
foreach (string f in absent) Console.WriteLine("  not on this machine: " + f);
Console.WriteLine();

Console.WriteLine("==== combinations " + comboOk + "/" + comboTotal + " fully working ====");
if (problems.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine(problems.Count + " problem(s):");
    foreach (var group in problems.GroupBy(p => p.Split(':')[^1].Trim()).OrderByDescending(g => g.Count()))
        Console.WriteLine("  x" + group.Count() + "  " + group.Key + "   e.g. " + group.First().Split(':')[0]);
}
return problems.Count == 0 ? 0 : 1;

/// <summary>
/// Characters a given legacy encoding has no room for, so no mapping table can
/// carry them. Saying this per encoding matters: Shree's own Gujarati encodings
/// carry the nukta U+0ABC perfectly well, while Akruti's and ISFOC's do not.
///
/// The list is evidence, not opinion — GapFill writes an entry when Shree-Lipi's
/// own Converter nxt refuses a character or discards it from a cluster (asked for
/// "कॆ" it answers "क"). The few that predate that tool are marked below.
///
/// An entry here means "the encoding cannot express this", never "our table is
/// missing it", so everything FullTest still reports is a real gap.
/// </summary>
static class EncodingLimits
{
    private static readonly Dictionary<string, HashSet<char>> ByEncoding = Load();

    // InScript is one layout shared by every Indic script, so the key that types
    // the short e in Tamil or Kannada types U+0946/U+090E in Devanagari — code
    // points Unicode has only for transliterating southern vowels, which no
    // Devanagari legacy font draws. A standalone Gurmukhi nukta is the same case:
    // the fonts carry the six precomposed letters (ਖ਼ ਗ਼ ਜ਼ …) and nothing else.
    private static readonly HashSet<char> Everywhere = new() { 'ॆ', 'ऎ', '਼' };

    public static bool Covers(string encoding, char c) =>
        Everywhere.Contains(c) || (ByEncoding.TryGetValue(encoding, out var set) && set.Contains(c));

    private static Dictionary<string, HashSet<char>> Load()
    {
        var result = new Dictionary<string, HashSet<char>>(StringComparer.OrdinalIgnoreCase);
        string path = Path.Combine(AppContext.BaseDirectory, "config", "encoding-limits.json");
        if (!File.Exists(path)) return result;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var set = new HashSet<char>();
            foreach (var v in prop.Value.EnumerateArray())
                set.Add((char)Convert.ToInt32(v.GetString() ?? "0", 16));
            result[prop.Name] = set;
        }
        return result;
    }
}
