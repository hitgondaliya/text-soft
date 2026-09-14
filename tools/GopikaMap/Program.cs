using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// GopikaMap — build the Gopika (Gujarati legacy) mapping tables.
//
// Gopika is an Oasis Systems font from 1993 with no Unicode cmap: its letters sit
// on ASCII bytes. Its byte layout is the ITR Gujarati typewriter scheme — the same
// key assignment as assets/config/layouts/guj-itr.json, verified glyph by glyph
// against the font's own outlines (tools/GopikaMap/NOTES.md).
//
// Unlike the Shree tables (captured from Converter nxt, ~1 MB of enumerated
// clusters) this table is small: single-glyph atoms plus regex rules that do the
// Gujarati reordering — pre-base i-matra and reph.
//
//   dotnet run --project tools/GopikaMap            writes both mapping files

const string Ka = "ક", Ha = "હ";                 // consonant range ક..હ
const string Virama = "્", IMatra = "િ", Ra = "ર";
const string RephMark = "";                          // internal stand-in for the reph glyph

// byte (as a char in the legacy string) -> Unicode
var atoms = new List<(string Legacy, string Uni)>
{
    // ---- consonants ----
    ("f", "ક"),  // ક
    ("F", "ખ"),  // ખ
    ("d", "ગ"),  // ગ
    ("D", "ઘ"),  // ઘ
    ("a", "ચ"),  // ચ
    ("A", "છ"),  // છ
    ("s", "જ"),  // જ
    ("S", "ઝ"),  // ઝ
    ("x", "ટ"),  // ટ
    ("X", "ઠ"),  // ઠ
    ("z", "ડ"),  // ડ
    ("Z", "ઢ"),  // ઢ
    ("K", "ણ"),  // ણ
    (";", "ત"),  // ત
    (":", "થ"),  // થ
    ("^", "દ"),  // દ
    ("\"", "ધ"), // ધ
    ("l", "ન"),  // ન
    ("v", "પ"),  // પ
    ("V", "ફ"),  // ફ
    ("c", "બ"),  // બ
    ("C", "ભ"),  // ભ
    ("b", "મ"),  // મ
    ("g", "ય"),  // ય
    ("h", "ર"),  // ર
    ("j", "લ"),  // લ
    ("J", "વ"),  // વ
    ("N", "શ"),  // શ
    ("M", "ષ"),  // ષ
    ("m", "સ"),  // સ
    ("n", "હ"),  // હ

    // ---- conjuncts the font carries as one glyph ----
    // Verified by typing the byte inside a word and reading the result
    // (tools/GopikaMap/NOTES.md): ળ and the ra-conjuncts live in the high bytes,
    // not where the ITR key chart would put them.
    ("H", "ક્ષ"),
    ("@", "જ્ઞ"),
    ("¤", "ળ"),
    ("§", "શ્ર"),
    ("¡", "પ્ર"),
    ("¢", "ક્ર"),
    ("£", "ફ્ર"),

    // ---- half forms drawn after the base letter ----
    ("B", "્મ"),
    ("G", "્ય"),
    ("L", "્ન"),
    ("À", "્ત"),

    // ---- independent vowels ----
    ("y", "અ"),  // અ
    ("R", "ઇ"),  // ઇ
    ("E", "ઈ"),  // ઈ
    ("W", "ઉ"),  // ઉ
    ("Q", "ઊ"),  // ઊ
    ("}", "ઋ"),  // ઋ
    ("#", "ઇં"),
    ("$", "ઈં"),

    // ---- vowel signs and marks ----
    ("t", "ા"),  // ા
    ("r", "િ"),  // િ  (typed before its consonant; see the rules below)
    ("e", "ી"),  // ી
    ("w", "ુ"),  // ુ
    ("q", "ૂ"),  // ૂ
    ("]", "ૃ"),  // ૃ
    ("u", "ે"),  // ે
    ("i", "ૈ"),  // ૈ
    ("p", "ૅ"),  // ૅ
    ("k", "ં"),  // ં
    ("P", "ઁ"),  // ઁ
    ("&", "ઃ"),  // ઃ
    ("|", "઼"),  // nukta
    ("T", "્"),  // ્
    ("o", RephMark),  // ર્ as a mark drawn over the cluster

    // ---- shifted marks the font pairs with anusvara ----
    ("U", "ેં"),  // ેં
    ("I", "ૈં"),  // ૈં
    ("O", RephMark + "ં"),

    // ---- digits ----
    ("0", "૦"), ("1", "૧"), ("2", "૨"), ("3", "૩"), ("4", "૪"),
    ("5", "૫"), ("6", "૬"), ("7", "૭"), ("8", "૮"), ("9", "૯"),

    // ---- punctuation ----
    ("`", "ૐ"),  // ૐ
    (">", "।"),  // ।
    ("<", ";"),
    ("!", "!"), ("%", "%"), ("(", "("), (")", ")"), ("*", "*"), ("+", "+"),
    (",", ","), ("-", "-"), (".", "."), ("/", "/"), ("=", "="), ("?", "?"),
    ("_", "—"), ("'", "’"), (" ", " "),
};

// Unicode clusters the font types as two or three glyphs.
var composites = new List<(string Uni, string Legacy)>
{
    ("આ", "yt"),      // આ = અ + ા
    ("એ", "yu"),      // એ = અ + ે
    ("ઐ", "yi"),      // ઐ = અ + ૈ
    ("ઓ", "yut"),     // ઓ = અ + ો
    ("ઔ", "yit"),     // ઔ = અ + ૌ
    ("ઍ", "yp"),      // ઍ = અ + ૅ
    ("ૉ", "pt"),      // ૉ = ૅ + ા
    ("ો", "ut"),      // ો = ે + ા
    ("ૌ", "it"),      // ૌ = ૈ + ા
    ("ૄ", "]t"),      // ૄ
};

string consonant = "[" + Ka + "-" + Ha + "]";
string cluster = "((?:" + consonant + Virama + ")*" + consonant + ")";

var toUnicode = new Table
{
    Id = "gopika-guj-to-unicode",
    Name = "Gopika Gujarati -> Unicode",
    SourceFontId = "gopika-guj",
    TargetFontId = "shruti",
    SourceType = "legacy",
    TargetType = "unicode",
    PreRules = new List<Rule>(),
    PostRules = new List<Rule>
    {
        new() { Pattern = IMatra + cluster, Replace = "$1" + IMatra,
                Note = "i-matra is typed before its cluster in the font; Unicode puts it after" },
        new() { Pattern = cluster + RephMark, Replace = Ra + Virama + "$1",
                Note = "reph is drawn over the cluster; Unicode writes ra+virama before it" },
    },
    Pairs = new List<Pair>(),
};

var toLegacy = new Table
{
    Id = "unicode-to-gopika-guj",
    Name = "Unicode -> Gopika Gujarati",
    SourceFontId = "shruti",
    TargetFontId = "gopika-guj",
    SourceType = "unicode",
    TargetType = "legacy",
    PreRules = new List<Rule>
    {
        new() { Pattern = Ra + Virama + cluster, Replace = "$1" + RephMark,
                Note = "ra+virama before a cluster becomes the reph mark after it" },
        new() { Pattern = cluster + IMatra, Replace = IMatra + "$1",
                Note = "the i-matra glyph is typed before its cluster" },
    },
    PostRules = new List<Rule>(),
    Pairs = new List<Pair>(),
};

foreach (var (legacy, uni) in atoms)
{
    toUnicode.Pairs.Add(new Pair { From = legacy, To = uni });
    if (uni != " ")
        toLegacy.Pairs.Add(new Pair { From = uni, To = legacy });
}
foreach (var (uni, legacy) in composites)
{
    toLegacy.Pairs.Add(new Pair { From = uni, To = legacy });
    toUnicode.Pairs.Add(new Pair { From = legacy, To = uni });
}

// Clusters the font has no glyph for are written consonant + virama + consonant,
// exactly as they are typed on the machine; that is legible and is what the
// typewriter layout does.

string dir = Path.Combine(FindRoot(), "assets", "config", "mappings");
var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(Path.Combine(dir, "gopika-guj-to-unicode.json"), JsonSerializer.Serialize(toUnicode, opts), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(dir, "unicode-to-gopika-guj.json"), JsonSerializer.Serialize(toLegacy, opts), new UTF8Encoding(false));
Console.WriteLine($"wrote gopika-guj-to-unicode.json ({toUnicode.Pairs.Count} pairs)");
Console.WriteLine($"wrote unicode-to-gopika-guj.json ({toLegacy.Pairs.Count} pairs)");

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln")))
        d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}

sealed class Pair
{
    [JsonPropertyName("from")] public string From { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
}

sealed class Rule
{
    [JsonPropertyName("pattern")] public string Pattern { get; set; } = "";
    [JsonPropertyName("replace")] public string Replace { get; set; } = "";
    [JsonPropertyName("note")] public string? Note { get; set; }
}

sealed class Table
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("sourceFontId")] public string SourceFontId { get; set; } = "";
    [JsonPropertyName("targetFontId")] public string TargetFontId { get; set; } = "";
    [JsonPropertyName("sourceType")] public string SourceType { get; set; } = "";
    [JsonPropertyName("targetType")] public string TargetType { get; set; } = "";
    [JsonPropertyName("preRules")] public List<Rule> PreRules { get; set; } = new();
    [JsonPropertyName("pairs")] public List<Pair> Pairs { get; set; } = new();
    [JsonPropertyName("postRules")] public List<Rule> PostRules { get; set; } = new();
}
