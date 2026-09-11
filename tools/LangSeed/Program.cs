using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// LangSeed — add the three Shree languages missing from our catalog: Assamese,
// Manipuri, Diacritical. Assamese uses its own .ASS schemes (filled by KbdGen
// afterwards). Manipuri uses the Bengali script, so it reuses Bengali's keyboard
// layouts. Diacritical (IAST) has no Shree scheme file; it gets a placeholder
// InScript until its own layout data is provided.
//
//   dotnet run --project tools/LangSeed     (run BEFORE KbdGen)

string config = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "config"));
if (!Directory.Exists(config)) config = @"C:\Users\harde\Desktop\Windows Font Software\assets\config";

var loader = new ConfigLoader(config);
var catalog = loader.LoadCatalog();

FontAsset Unicode(string family) => new() { Id = "unicode", Name = "Unicode", Family = family, Type = FontType.Unicode };
FontAsset Legacy(string id, string name, string family, string? file, string enc) =>
    new() { Id = id, Name = name, Family = family, File = file, Type = FontType.Legacy, Encoding = enc };

List<FontLayout> FL(string langId, string shreeExFontId, params string[] names)
{
    static string Slug(string s) => new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-').Replace("--", "-");
    var list = new List<FontLayout>();
    foreach (var n in names)
    {
        var e = new FontLayout { Id = $"{langId}-fl-{Slug(n)}", Name = n, Type = FontType.Legacy };
        if (n == "Unicode") { e.Type = FontType.Unicode; e.FontId = "unicode"; }
        else if (n == "ShreeLipi-Ex") e.FontId = shreeExFontId;
        list.Add(e);
    }
    return list;
}

LanguageProfile Ensure(string id, string name, string sample)
{
    var l = catalog.Languages.FirstOrDefault(x => x.Id == id);
    if (l is null) { l = new LanguageProfile { Id = id, Name = name, Sample = sample }; catalog.Languages.Add(l); }
    else { l.Name = name; l.Sample = sample; }
    return l;
}

// ---- Assamese (Bengali script; own .ASS schemes filled by KbdGen) ----
var asm = Ensure("as", "Assamese", "অ");
asm.Fonts = new()
{
    Legacy("shree-banx", "Shree-BanX (Legacy)", "Shree-BanX-0001", "fonts/shree/shree-banx.ttf", "shree-banx"),
    Unicode("Nirmala UI"),
};
asm.FontLayouts = FL("as", "shree-banx",
    "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "STM-BNT", "STM", "Ramdhenu 3.1", "Indica 2004");
// Give Assamese an InScript (Bengali InScript works for the Assamese script);
// KbdGen keeps this hand-built entry and appends the generated ass-* schemes.
if (!asm.Layouts.Any(x => x.Name == "InScript"))
    asm.Layouts.Insert(0, new LayoutRef { Id = "as-inscript", Name = "InScript", File = "layouts/bengali-inscript.json" });

// ---- Manipuri (Bengali script; reuse Bengali keyboard layouts) ----
var mni = Ensure("mni", "Manipuri", "অ");
mni.Fonts = new()
{
    Legacy("shree-banx", "Shree-BanX (Legacy)", "Shree-BanX-0001", "fonts/shree/shree-banx.ttf", "shree-banx"),
    Unicode("Nirmala UI"),
};
mni.FontLayouts = FL("mni", "shree-banx",
    "Unicode", "ShreeLipi-Ex", "Shree-Lipi 7.0", "Shree-Lipi 4.0/5.0/6.0", "STM-BNT", "STM", "Ramdhenu 3.1", "Indica 2004");
var bengali = catalog.Languages.First(x => x.Id == "bn");
mni.Layouts = bengali.Layouts.Select(r => new LayoutRef { Id = "mni-" + r.Id, Name = r.Name, File = r.File }).ToList();

// ---- Diacritical (IAST; placeholder keyboard until its own data is provided) ----
var dia = Ensure("dia", "Diacritical", "ā");
dia.Fonts = new() { Unicode("Nirmala UI") };
dia.FontLayouts = FL("dia", "shree-devx", "Unicode", "ShreeLipi-Ex", "Indic Diacritical");
if (dia.Layouts.Count == 0)
    dia.Layouts.Add(new LayoutRef { Id = "dia-inscript", Name = "InScript", File = "layouts/hindi-inscript.json" });

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
File.WriteAllText(Path.Combine(config, "languages.json"), JsonSerializer.Serialize(catalog, opts));
Console.WriteLine($"Seeded languages. Total = {catalog.Languages.Count}:");
foreach (var l in catalog.Languages)
    Console.WriteLine($"  {l.Name}: fontLayouts={l.FontLayouts.Count} keyboardLayouts={l.Layouts.Count}");
