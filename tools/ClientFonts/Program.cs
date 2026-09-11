using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// ClientFonts — register the client-supplied fonts (bundled under fonts/client)
// as selectable FontAssets. Unicode fonts work as-is; Shree-GujX legacy fonts
// reuse the existing shree-gujx map for live legacy typing.
//
//   dotnet run --project tools/ClientFonts

string config = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "config"));
if (!Directory.Exists(config)) config = @"C:\Users\harde\Desktop\Windows Font Software\assets\config";

var loader = new ConfigLoader(config);
var catalog = loader.LoadCatalog();

FontAsset Uni(string id, string name, string family, string file) =>
    new() { Id = id, Name = name, Family = family, File = "fonts/client/" + file, Type = FontType.Unicode };
FontAsset Guj(string id, string family, string file) =>
    new() { Id = id, Name = family + " (Legacy)", Family = family, File = "fonts/client/" + file, Type = FontType.Legacy, Encoding = "shree-gujx" };

var add = new Dictionary<string, List<FontAsset>>
{
    ["gu"] = new()
    {
        Uni("cf-aakar", "Aakar (Unicode)", "aakar", "Aakar.ttf"),
        Uni("cf-shruti", "Shruti (Unicode)", "Shruti", "SHRUTI.TTF"),
        Guj("cf-sgujx-0756", "SHREE-GUJX-0756", "S0756810.TTF"),
        Guj("cf-sgujx-0757", "SHREE-GUJX-0757", "S0757810.TTF"),
        Guj("cf-sgujx-0793", "SHREE-GUJX-0793", "S0793810.TTF"),
    },
    ["hi"] = new()
    {
        Uni("cf-amita", "Amita (Unicode)", "Amita", "Amita-Regular.ttf"),
        Uni("cf-biryani", "Biryani (Unicode)", "Biryani", "Biryani-Regular.ttf"),
        Uni("cf-mukta", "Mukta (Unicode)", "Mukta", "Mukta-Regular.ttf"),
    },
};
add["mr"] = add["hi"]; // Marathi shares Devanagari Unicode fonts

foreach (var (langId, fonts) in add)
{
    var lang = catalog.Languages.FirstOrDefault(l => l.Id == langId);
    if (lang is null) continue;
    foreach (var f in fonts)
        if (!lang.Fonts.Any(x => x.Id == f.Id))
            lang.Fonts.Add(f);
}

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
File.WriteAllText(Path.Combine(config, "languages.json"), JsonSerializer.Serialize(catalog, opts));
Console.WriteLine("Client fonts registered:");
foreach (var id in new[] { "gu", "hi", "mr" })
{
    var l = catalog.Languages.First(x => x.Id == id);
    Console.WriteLine($"  {l.Name}: {string.Join(", ", l.Fonts.Where(f => f.Id.StartsWith("cf-")).Select(f => f.Name))}");
}
