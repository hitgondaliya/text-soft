using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Core.Fonts;

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

// --layouts-only rewrites the layout files and leaves languages.json alone. The
// catalog now carries 1,500 hand-verified font entries; re-serialising it from the
// model to change keyboards would risk all of that for nothing.
bool layoutsOnly = args.Contains("--layouts-only");

// --chm <dir>: Shree publishes a help file per keyboard per script, and ChmMap
// turns each into key -> Unicode pairs. That is Shree's own account of its
// keyboards, so where it names a key the scheme file could not explain, take it.
// Only ever ADDS keys — the hand-verified InScript mappings are never overwritten
// by it — and every added key still has to pass the renderability check.
string? chmDir = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--chm" && i + 1 < args.Length) chmDir = args[++i];

// script ext -> (rosetta InScript layout id-language, unicode fontId, id prefix,
//                target language ids, Unicode block base)
var scripts = new (string Ext, string RosettaLang, string FontId, string Prefix, string[] Targets, int Block)[]
{
    ("DEV", "hi", "mangal",  "dev", new[]{"hi","mr"}, 0x0900),
    ("SAN", "hi", "mangal",  "san", new[]{"sa"},      0x0900),
    ("GUJ", "gu", "shruti",  "guj", new[]{"gu"},      0x0A80),
    ("PUN", "pa", "raavi",   "pun", new[]{"pa"},      0x0A00),
    ("BAN", "bn", "vrinda",  "ban", new[]{"bn"},      0x0980),
    ("TEL", "te", "gautami", "tel", new[]{"te"},      0x0C00),
    ("KAN", "kn", "tunga",   "kan", new[]{"kn"},      0x0C80),
    ("MAL", "ml", "kartika", "mal", new[]{"ml"},      0x0D00),
    ("ORI", "or", "kalinga", "ori", new[]{"or"},      0x0B00),
    ("TAM", "ta", "latha",   "tam", new[]{"ta"},      0x0B80),
    ("ASS", "bn", "vrinda",  "ass", new[]{"as"},      0x0980),
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

// ---------------------------------------------------------------------------
// Shree's scheme files map a key to an internal GLYPH BYTE, and the only way we
// can read those bytes is by joining INSCRIPT.<ext> against our verified InScript
// layout. That explains about 80 bytes per script — every other byte was simply
// dropped, taking its key with it, which is why the Eng layout had no શ and 216
// layouts were short of keys.
//
// Two things rescue most of the rest.
//
// First, the byte code is POSITIONAL and shared between related scripts: byte 0x53
// is "kha" in Devanagari, Gujarati and Bengali alike, each at offset 0x16 in its
// own Unicode block. So a byte one script cannot explain can be borrowed from a
// script that can. Which scripts may lend to which is measured here, not assumed —
// Devanagari and Gujarati agree on 68 of 68 shared bytes, Telugu and Kannada form
// their own pair, and Tamil and Malayalam agree with nobody and so borrow nothing.
// A donor needs >=90% agreement over >=20 shared bytes, every donor that knows the
// byte has to give the same answer, and the borrowed code point has to be assigned
// in the target block.
//
// Second, a key whose byte equals its own ASCII code is punctuation passing
// straight through, and can be taken literally.
// ---------------------------------------------------------------------------
var offsets = new Dictionary<string, Dictionary<byte, int>>(StringComparer.Ordinal);
var rosettas = new Dictionary<string, Dictionary<byte, string>>(StringComparer.Ordinal);

foreach (var sc in scripts)
{
    // Shree ships no INSCRIPT.SAN — Sanskrit is Devanagari and shares its scheme,
    // so read the byte meanings from INSCRIPT.DEV. Without this the 9 Sanskrit
    // schemes decode against nothing and every key of them is dropped.
    string insPath = Path.Combine(shree, "INSCRIPT." + sc.Ext);
    if (!File.Exists(insPath) && sc.Ext == "SAN") insPath = Path.Combine(shree, "INSCRIPT.DEV");
    if (!File.Exists(insPath)) continue;
    byte[] insBytes = File.ReadAllBytes(insPath);
    var rl = catalog.FindLanguage(sc.RosettaLang);
    if (rl is null) continue;
    var rIns = bundle.GetLayout(rl.Layouts.First(l => l.Name == "InScript"));

    var map = new Dictionary<byte, string>();
    foreach (var (vk, n, s) in keys)
    {
        var m = rIns.Find(vk); if (m is null) continue;
        byte bn = Prim(insBytes, n), bs = Prim(insBytes, s);
        if (bn != 0xFF && !string.IsNullOrEmpty(m.Base) && !map.ContainsKey(bn)) map[bn] = m.Base!;
        if (bs != 0xFF && !string.IsNullOrEmpty(m.Shift) && !map.ContainsKey(bs)) map[bs] = m.Shift!;
    }
    rosettas[sc.Ext] = map;

    var off = new Dictionary<byte, int>();
    foreach (var (b, u) in map)
        if (u.Length == 1 && u[0] >= sc.Block && u[0] < sc.Block + 0x80) off[b] = u[0] - sc.Block;
    offsets[sc.Ext] = off;
}

// donors[ext] = the scripts allowed to lend it a byte meaning
var donors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
foreach (var a in scripts)
{
    var list = new List<string>();
    if (offsets.TryGetValue(a.Ext, out var oa))
        foreach (var b in scripts)
        {
            if (a.Ext == b.Ext || !offsets.TryGetValue(b.Ext, out var ob)) continue;
            int shared = 0, agree = 0;
            foreach (var (k, v) in oa) if (ob.TryGetValue(k, out int w)) { shared++; if (v == w) agree++; }
            // Perfect agreement only. At 90% the Telugu/Kannada pair (43 of 47)
            // lent Kannada three matras Kannada does not use, and the keys it
            // produced converted to nothing.
            if (shared >= 20 && agree == shared) list.Add(b.Ext);
        }
    donors[a.Ext] = list;
}
foreach (var sc in scripts)
    Console.WriteLine($"  {sc.Ext}: {(rosettas.TryGetValue(sc.Ext, out var r) ? r.Count : 0)} bytes known, donors: "
                    + (donors[sc.Ext].Count > 0 ? string.Join(",", donors[sc.Ext]) : "(none)"));
Console.WriteLine();

// A key is only worth having if the app can draw what it types. This is the same
// test the full-matrix run applies: push the character through every legacy
// encoding the language offers and check the resulting code exists in the fonts
// that would render it. It matters most for the literal-punctuation rule — Tamil
// legacy fonts put Tamil glyphs on the bytes for f, h and t, and the Bengali ones
// have nothing at all on '%' or ':', so taking those keys at face value produced
// keys that typed blanks.
var fontCodes = FontIndex.Build(
    @"C:\Windows\Fonts",
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"));
Console.WriteLine($"indexed {fontCodes.Count} installed font families for the renderability check\n");

bool Renderable(string[] targets, string s)
{
    foreach (string langId in targets)
    {
        var lang = catalog.FindLanguage(langId);
        if (lang is null) continue;
        foreach (var enc in lang.Fonts.Where(f => f.Type == FontType.Legacy)
                                      .Select(f => f.ConversionKey).Distinct(StringComparer.Ordinal))
        {
            var conv = bundle.CreateConverterToLegacy(enc);
            if (conv is null) continue;
            var r = conv.Convert(s);
            if (r.UnmappedCharacters.Any(c => c.Any(ch => ch >= 0x80))) return false;
            foreach (var font in lang.Fonts.Where(f => f.Type == FontType.Legacy && f.ConversionKey == enc))
            {
                if (!fontCodes.TryGetValue(font.Family, out var codes)) continue;   // not installed here
                foreach (char ch in r.Text) if (ch >= 0x20 && !codes.Contains(ch)) return false;
            }
        }
    }
    return true;
}

int fromRosetta = 0, fromBorrow = 0, fromLiteral = 0, unknownKeys = 0, rejected = 0, fromHelp = 0;

/// Single-character keys from Shree's help file for this script and scheme.
/// A multi-character key there is a typing recipe ("kha" -> ખ), not a key, and
/// belongs in a transliteration scheme rather than a keyboard layout.
Dictionary<char, string> LoadChm(string? dir, string ext, string scheme)
{
    var result = new Dictionary<char, string>();
    if (dir is null) return result;
    string path = Path.Combine(dir, ext.ToLowerInvariant(),
        new string(scheme.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()) + ".json");
    if (!File.Exists(path)) return result;
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
    {
        string k = p.GetProperty("Key").GetString() ?? "", u = p.GetProperty("Uni").GetString() ?? "";
        if (k.Length != 1 || u.Length == 0) continue;
        if (!result.ContainsKey(k[0])) result[k[0]] = u;
    }
    return result;
}

/// The Unicode a key produces, or null if nothing can be said about it honestly.
string? Decode(string ext, int block, byte b, char keyChar, string[] targets)
{
    if (b == 0xFF) return null;
    if (rosettas.TryGetValue(ext, out var own) && own.TryGetValue(b, out string? direct)) { fromRosetta++; return direct; }

    int? consensus = null;
    foreach (string donor in donors[ext])
    {
        if (!offsets[donor].TryGetValue(b, out int o)) continue;
        if (consensus is null) consensus = o;
        else if (consensus != o) { consensus = null; break; }   // donors disagree: say nothing
    }
    if (consensus is int off2)
    {
        char cp = (char)(block + off2);
        if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(cp) != System.Globalization.UnicodeCategory.OtherNotAssigned)
        {
            if (Renderable(targets, cp.ToString())) { fromBorrow++; return cp.ToString(); }
            rejected++;
            return null;
        }
    }

    if (b == keyChar && b >= 0x20 && b < 0x7F)
    {
        if (Renderable(targets, keyChar.ToString())) { fromLiteral++; return keyChar.ToString(); }
        rejected++;
        return null;
    }
    unknownKeys++;
    return null;
}

foreach (var sc in scripts)
{
    if (!rosettas.TryGetValue(sc.Ext, out var b2u)) { Console.WriteLine($"skip {sc.Ext}: no scheme bytes"); continue; }

    int made = 0;
    foreach (var file in Directory.GetFiles(shree, "*." + sc.Ext))
    {
        string scheme = Path.GetFileNameWithoutExtension(file);
        if (scheme.Equals("INSCRIPT", StringComparison.OrdinalIgnoreCase)) continue; // keep hand-built
        byte[] d = File.ReadAllBytes(file);
        if (d.Length < 128) continue;

        var fromChm = LoadChm(chmDir, sc.Ext, scheme);
        var rows = new List<string>();
        foreach (var (vk, n, s) in keys.OrderBy(k => k.vk))
        {
            string? un = Decode(sc.Ext, sc.Block, Prim(d, n), n, sc.Targets);
            string? us = Decode(sc.Ext, sc.Block, Prim(d, s), s, sc.Targets);
            // Shree's own help file fills the gaps the scheme file left.
            if (un is null && fromChm.TryGetValue(n, out string? cn) && Renderable(sc.Targets, cn)) { un = cn; fromHelp++; }
            if (us is null && fromChm.TryGetValue(s, out string? cs) && Renderable(sc.Targets, cs)) { us = cs; fromHelp++; }
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

Console.WriteLine($"\nkeys decoded: {fromRosetta} from this script's InScript, {fromBorrow} borrowed from an"
                + $" agreeing script, {fromLiteral} literal punctuation; {fromHelp} from Shree's help files, {rejected} rejected as unrenderable, {unknownKeys} still unexplained.");

if (layoutsOnly)
{
    Console.WriteLine("--layouts-only: languages.json left untouched.");
    return;
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
