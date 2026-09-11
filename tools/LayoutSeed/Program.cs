using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// LayoutSeed — write the per-language Font Layout list (copied from Shree-Lipi's
// Font-Layout dropdown, per the client's screenshots) into languages.json.
//
//   dotnet run --project tools/LayoutSeed

string config = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "config"));
if (!Directory.Exists(config)) config = @"C:\Users\harde\Desktop\Windows Font Software\assets\config";

var loader = new ConfigLoader(config);
var catalog = loader.LoadCatalog();

// Legacy Shree-*X font id per language (the "ShreeLipi-Ex" family we already ship).
var shreeEx = new Dictionary<string, string>
{
    ["hi"] = "shree-devx", ["mr"] = "shree-devx", ["sa"] = "shree-devx",
    ["pa"] = "shree-punx", ["bn"] = "shree-banx", ["te"] = "shree-telx",
    ["gu"] = "shree-gujx", ["kn"] = "shree-kanx", ["ml"] = "shree-malx",
    ["or"] = "shree-orix", ["ta"] = "shree-tamx",
};
// Bundled "Shree-Lipi 7.0" font id where we have one.
var shree7 = new Dictionary<string, string?>
{
    ["hi"] = "shree-dev-0700", ["mr"] = "shree-dev-0700", ["sa"] = "shree-dev-0700",
    ["gu"] = "shree-guj-1120",
};

// Font-layout NAMES per language, in Shree's order (from screenshots).
var devanagari = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "Shivaji", "ISFOC Monolingual", "Indica", "Anu", "APS Monolingual", "Akruti Monolingual" };
var lists = new Dictionary<string, string[]>
{
    ["hi"] = devanagari,
    ["mr"] = devanagari,
    ["sa"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0" },
    ["pa"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0" },
    ["bn"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "STM-BNT", "STM-BAT", "STM", "Ramdhenu 3.1", "Indica 2004", "BIJOY", "Bangla Akademi 2004" },
    ["te"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "ISFOC Monolingual", "Anu6", "Anu 7.0", "Anu 4.0" },
    ["gu"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "Terafont", "ISFOC Monolingual", "Bhasha Bharti-2", "Bhasha Bharti-1", "Bhasha Bharati" },
    ["kn"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "KGP/Nudi Monolingual", "Anu" },
    ["ml"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0" },
    ["or"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "Akruti 98" },
    ["ta"] = new[] { "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "Vanavil", "Tamil99 Monolingual", "Tamil99 Bilingual", "Bamini", "Anu" },
};

static string Slug(string s) => new string(s.ToLowerInvariant()
    .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
    .Trim('-').Replace("--", "-");

foreach (var lang in catalog.Languages)
{
    if (!lists.TryGetValue(lang.Id, out var names)) continue;
    var fl = new List<FontLayout>();
    foreach (var name in names)
    {
        var e = new FontLayout { Id = $"{lang.Id}-fl-{Slug(name)}", Name = name };
        if (name == "Unicode")
        {
            e.Type = FontType.Unicode;
            e.FontId = "unicode";
        }
        else if (name == "ShreeLipi-Ex")
        {
            e.Type = FontType.Legacy;
            e.FontId = shreeEx.TryGetValue(lang.Id, out var f) ? f : null;
        }
        else if (name == "Shree-Lipi 7.0")
        {
            e.Type = FontType.Legacy;
            e.FontId = shree7.TryGetValue(lang.Id, out var f) ? f : null;
        }
        else
        {
            e.Type = FontType.Legacy;   // third-party family; font/map provided later
            e.FontId = null;
        }
        fl.Add(e);
    }
    lang.FontLayouts = fl;
}

// The "Shree-Lipi 7.0" bundled fonts share their script's Shree-*X byte encoding,
// so tag them to reuse the existing Unicode↔legacy maps for live legacy typing.
void SetEnc(string langId, string fontId, string enc)
{
    var f = catalog.Languages.FirstOrDefault(l => l.Id == langId)?.Fonts.FirstOrDefault(x => x.Id == fontId);
    if (f is not null) f.Encoding = enc;
}
SetEnc("hi", "shree-dev-0700", "shree-devx");
SetEnc("mr", "shree-dev-0700", "shree-devx");
SetEnc("sa", "shree-dev-0700", "shree-devx");
SetEnc("gu", "shree-guj-1120", "shree-gujx");

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
File.WriteAllText(Path.Combine(config, "languages.json"), JsonSerializer.Serialize(catalog, opts));
Console.WriteLine("Seeded Font Layouts:");
foreach (var lang in catalog.Languages)
    Console.WriteLine($"  {lang.Name}: {lang.FontLayouts.Count} font layout(s)");
