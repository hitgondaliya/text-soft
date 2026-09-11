using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// KbdGen — batch-generate Unicode keyboard-layout JSON from Shree-Lipi's 406-byte
// scheme files. For each script it builds a Rosetta (Shree glyph byte -> Unicode)
// from INSCRIPT.<ext> joined with our verified InScript layout, then decodes every
// other scheme file for that script into a Unicode layout, and registers them all
// in languages.json.
//
//   dotnet run --project tools/KbdGen -- "C:\Shree-Lipi nxt Plus"

string shree = args.Length > 0 ? args[0] : @"C:\Shree-Lipi nxt Plus";
string config = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "config"));
if (!Directory.Exists(config)) config = @"C:\Users\harde\Desktop\Windows Font Software\assets\config";
string layoutsDir = Path.Combine(config, "layouts");
const int MinKeys = 20;

// script ext -> (rosetta InScript layout id-language, unicode fontId, id prefix, target language ids)
var scripts = new (string Ext, string RosettaLang, string FontId, string Prefix, string[] Targets)[]
{
    ("DEV", "hi", "mangal",  "dev", new[]{"hi","mr"}),
    ("SAN", "hi", "mangal",  "san", new[]{"sa"}),
    ("GUJ", "gu", "shruti",  "guj", new[]{"gu"}),
    ("PUN", "pa", "raavi",   "pun", new[]{"pa"}),
    ("BAN", "bn", "vrinda",  "ban", new[]{"bn"}),
    ("TEL", "te", "gautami", "tel", new[]{"te"}),
    ("KAN", "kn", "tunga",   "kan", new[]{"kn"}),
    ("MAL", "ml", "kartika", "mal", new[]{"ml"}),
    ("ORI", "or", "kalinga", "ori", new[]{"or"}),
    ("TAM", "ta", "latha",   "tam", new[]{"ta"}),
    ("ASS", "bn", "vrinda",  "ass", new[]{"as"}),
};

var keys = new List<(int vk, char n, char s)>();
void K(int vk, char n, char s) => keys.Add((vk, n, s));
for (char c = 'A'; c <= 'Z'; c++) K(c, char.ToLower(c), c);
K(0x31,'1','!'); K(0x32,'2','@'); K(0x33,'3','#'); K(0x34,'4','$'); K(0x35,'5','%');
K(0x36,'6','^'); K(0x37,'7','&'); K(0x38,'8','*'); K(0x39,'9','('); K(0x30,'0',')');
K(0xBA,';',':'); K(0xBB,'=','+'); K(0xBC,',','<'); K(0xBD,'-','_'); K(0xBE,'.','>');
K(0xBF,'/','?'); K(0xC0,'`','~'); K(0xDB,'[','{'); K(0xDD,']','}'); K(0xDE,'\'','"');

byte Prim(byte[] d, char c) => c < d.Length ? d[c] : (byte)0xFF;

static string Pretty(string raw) => raw.ToUpperInvariant() switch
{
    "PHONETIC" => "Phonetic", "PHONET86" => "Phonetic 86",
    "TYPEWRIT" => "Typewriter", "TYPEWRI2" => "Typewriter 2",
    "RAMING" => "Remington", "RAMING1" => "Remington 1", "RAMING2" => "Remington 2", "RAMING3" => "Remington 3",
    _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo
             .ToTitleCase(raw.Replace('_', ' ').ToLowerInvariant())
};

static string Slug(string s) => new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

var loader = new ConfigLoader(config);
var bundle = loader.Load();
var catalog = bundle.Catalog;

// generated layouts per target language id
var generated = new Dictionary<string, List<LayoutRef>>();

foreach (var sc in scripts)
{
    string insFile = Path.Combine(shree, "INSCRIPT." + sc.Ext);
    if (!File.Exists(insFile)) { Console.WriteLine($"skip {sc.Ext}: no INSCRIPT file"); continue; }
    byte[] ins = File.ReadAllBytes(insFile);

    // Rosetta from this script's verified InScript.
    var rlang = catalog.FindLanguage(sc.RosettaLang)!;
    var rInscript = bundle.GetLayout(rlang.Layouts.First(l => l.Name == "InScript"));
    var b2u = new Dictionary<byte, string>();
    foreach (var (vk, n, s) in keys)
    {
        var m = rInscript.Find(vk); if (m is null) continue;
        byte bn = Prim(ins, n), bs = Prim(ins, s);
        if (bn != 0xFF && !string.IsNullOrEmpty(m.Base) && !b2u.ContainsKey(bn)) b2u[bn] = m.Base!;
        if (bs != 0xFF && !string.IsNullOrEmpty(m.Shift) && !b2u.ContainsKey(bs)) b2u[bs] = m.Shift!;
    }

    int made = 0;
    foreach (var file in Directory.GetFiles(shree, "*." + sc.Ext))
    {
        string scheme = Path.GetFileNameWithoutExtension(file);
        if (scheme.Equals("INSCRIPT", StringComparison.OrdinalIgnoreCase)) continue; // keep hand-built
        byte[] d = File.ReadAllBytes(file);
        if (d.Length < 128) continue;

        var rows = new List<string>();
        foreach (var (vk, n, s) in keys.OrderBy(k => k.vk))
        {
            byte bn = Prim(d, n), bs = Prim(d, s);
            string? un = bn != 0xFF && b2u.TryGetValue(bn, out var u1) ? u1 : null;
            string? us = bs != 0xFF && b2u.TryGetValue(bs, out var u2) ? u2 : null;
            if (un is null && us is null) continue;
            var parts = new List<string> { $"\"vk\": {vk}" };
            if (un is not null) parts.Add($"\"base\": \"{Esc(un)}\"");
            if (us is not null) parts.Add($"\"shift\": \"{Esc(us)}\"");
            rows.Add("    { " + string.Join(", ", parts) + " }");
        }
        if (rows.Count < MinKeys) continue;

        string id = $"{sc.Prefix}-{Slug(scheme)}";
        string outFile = $"layouts/{id}.json";
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"$comment\": \"Auto-generated from Shree-Lipi {Path.GetFileName(file)} via InScript Rosetta decode.\",");
        sb.AppendLine($"  \"id\": \"{id}\",");
        sb.AppendLine($"  \"name\": \"{Esc(Pretty(scheme))}\",");
        sb.AppendLine($"  \"fontId\": \"{sc.FontId}\",");
        sb.AppendLine("  \"keys\": [");
        sb.AppendLine(string.Join(",\n", rows));
        sb.AppendLine("  ],");
        sb.AppendLine("  \"deadKeyCombinations\": []");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(config, outFile), sb.ToString());

        var lref = new LayoutRef { Id = id, Name = Pretty(scheme), File = outFile };
        foreach (var t in sc.Targets)
        {
            if (!generated.TryGetValue(t, out var list)) generated[t] = list = new();
            list.Add(lref);
        }
        made++;
    }
    Console.WriteLine($"{sc.Ext}: generated {made} schemes (rosetta {b2u.Count} bytes) -> {string.Join(",", sc.Targets)}");
}

// Register: only touch the languages this run generated for. Others (e.g.
// Manipuri, which reuses Bengali's layouts) keep whatever was seeded for them.
var targetIds = scripts.SelectMany(s => s.Targets).ToHashSet();
foreach (var lang in catalog.Languages)
{
    if (!targetIds.Contains(lang.Id)) continue;
    var genPrefixes = new[] { "dev-", "san-", "guj-", "pun-", "ban-", "tel-", "kan-", "mal-", "ori-", "tam-", "ass-" };
    var inscript = lang.Layouts.Where(l => l.Name == "InScript" && !genPrefixes.Any(p => l.Id.StartsWith(p))).ToList();
    var gen = generated.TryGetValue(lang.Id, out var g) ? g.OrderBy(x => x.Name).ToList() : new();
    lang.Layouts = inscript.Concat(gen).ToList();
}

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
File.WriteAllText(Path.Combine(config, "languages.json"), JsonSerializer.Serialize(catalog, opts));
Console.WriteLine($"\nUpdated languages.json. Total layouts per language:");
foreach (var lang in catalog.Languages)
    Console.WriteLine($"  {lang.Name}: {lang.Layouts.Count}");
