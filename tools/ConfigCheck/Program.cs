using System.Text.RegularExpressions;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// Validates a client's asset config before shipping, so faulty data is caught
// early rather than failing on a user's machine (supports FR-1/FR-4).
//
// Usage:  configcheck [path-to-config-dir]   (defaults to ./config or ./assets/config)

string root = args.Length > 0 ? args[0] : FindDefaultRoot();
Console.WriteLine($"Checking config: {root}\n");

var errors = new List<string>();
var warnings = new List<string>();

AssetBundle bundle;
try
{
    bundle = new ConfigLoader(root).Load();
}
catch (ConfigException ex)
{
    Console.Error.WriteLine($"ERROR  {ex.Message}");
    return 2;
}

// Collect the keys a mapping table can legitimately name. The converter looks a
// table up by FontAsset.ConversionKey — the font's encoding, or its id when it has
// none — so checking ids alone reported every shared-encoding table as dangling
// and buried the real findings under 22 false warnings.
var fontIds = new HashSet<string>(StringComparer.Ordinal);

foreach (var lang in bundle.Languages)
{
    string where = $"language '{lang.Id}'";
    if (string.IsNullOrWhiteSpace(lang.Id)) errors.Add($"{where}: missing id.");
    if (string.IsNullOrWhiteSpace(lang.Name)) warnings.Add($"{where}: missing name.");
    if (lang.Fonts.Count == 0) warnings.Add($"{where}: no fonts.");

    var seenFont = new HashSet<string>(StringComparer.Ordinal);
    foreach (var font in lang.Fonts)
    {
        if (string.IsNullOrWhiteSpace(font.Id)) errors.Add($"{where}: a font has no id.");
        else if (!seenFont.Add(font.Id)) errors.Add($"{where}: duplicate font id '{font.Id}'.");
        else { fontIds.Add(font.Id); fontIds.Add(font.ConversionKey); }
    }

    // Every FONT LAYOUT entry is a dropdown row the customer can pick. A row that
    // names a font the language does not have, or names one of the wrong kind,
    // silently produces nothing or the wrong script — the client who tested the
    // build hit exactly this. An empty row is honest but useless, so it is a
    // warning rather than an error.
    foreach (var fl in lang.FontLayouts)
    {
        string flWhere = $"{where} font layout '{fl.Name}'";
        if (string.IsNullOrWhiteSpace(fl.FontId)) { warnings.Add($"{flWhere}: no fontId — the dropdown entry does nothing."); continue; }

        var target = lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId);
        if (target is null) { errors.Add($"{flWhere}: fontId '{fl.FontId}' is not a font of this language."); continue; }
        if ((fl.Type == FontType.Legacy) != (target.Type == FontType.Legacy))
            errors.Add($"{flWhere}: entry is {fl.Type} but font '{target.Id}' is {target.Type}.");
        else if (fl.Type == FontType.Legacy && bundle.CreateConverterToLegacy(target.ConversionKey) is null)
            errors.Add($"{flWhere}: no Unicode -> '{target.ConversionKey}' mapping table, so typing produces nothing.");
    }

    // Load each layout referenced by the language.
    foreach (var layoutRef in lang.Layouts)
    {
        try
        {
            var layout = bundle.GetLayout(layoutRef);
            if (layout.Keys.Count == 0)
                warnings.Add($"layout '{layoutRef.Id}': no keys defined.");
            foreach (var combo in layout.DeadKeyCombinations)
            {
                if (string.IsNullOrEmpty(combo.Dead) || string.IsNullOrEmpty(combo.Next))
                    warnings.Add($"layout '{layoutRef.Id}': a dead-key combination is missing 'dead' or 'next'.");
            }
        }
        catch (ConfigException ex)
        {
            errors.Add($"layout '{layoutRef.Id}': {ex.Message}");
        }
    }
}

// Characters a legacy encoding cannot show at all (assets/config/encoding-limits.json):
// the font has no glyph for them, and its byte draws a letter, so a table may drop them
// on purpose rather than print that letter.
var limits = LoadLimits(root);

// Validate mapping tables.
foreach (var table in bundle.MappingTables)
{
    string where = $"mapping '{table.Id}'";
    if (table.Pairs.Count == 0) warnings.Add($"{where}: no substitution pairs.");
    if (!fontIds.Contains(table.SourceFontId)) warnings.Add($"{where}: sourceFontId '{table.SourceFontId}' not found in any language.");
    if (!fontIds.Contains(table.TargetFontId)) warnings.Add($"{where}: targetFontId '{table.TargetFontId}' not found in any language.");

    foreach (var rule in table.PreRules.Concat(table.PostRules))
        CheckRegex(where, rule.Pattern);

    // Empty 'from' would loop/blank the converter.
    if (table.Pairs.Any(p => string.IsNullOrEmpty(p.From)))
        errors.Add($"{where}: a pair has an empty 'from'.");

    // An empty 'to' is not "leave it alone" — it deletes the character, without
    // even reporting it unmapped. Drop the pair instead and let it fall through -
    // unless encoding-limits records that the target font cannot show the character
    // (its byte is a letter there), where deleting it beats printing that letter.
    foreach (var p in table.Pairs.Where(p => string.IsNullOrEmpty(p.To)))
    {
        bool declared = p.From.Length == 1 && limits.TryGetValue(table.TargetFontId, out var cannot)
                        && cannot.Contains(((int)p.From[0]).ToString("X4"));
        if (declared) warnings.Add($"{where}: '{p.From}' is dropped - the font cannot show it (encoding-limits).");
        else errors.Add($"{where}: '{p.From}' maps to nothing, which silently deletes it.");
    }
}

// A table that exists on disk but is not listed in languages.json is simply not
// loaded — four third-party tables sat in the folder unreferenced, looking done.
string mapDir = Path.Combine(root, "mappings");
if (Directory.Exists(mapDir))
{
    var listed = new HashSet<string>(bundle.Catalog.MappingFiles.Select(p => Path.GetFileName(p)), StringComparer.OrdinalIgnoreCase);
    foreach (string f in Directory.EnumerateFiles(mapDir, "*.json").Select(Path.GetFileName).OfType<string>())
        if (!listed.Contains(f))
            warnings.Add($"mapping file '{f}' is on disk but not listed in languages.json — it is never loaded.");
}

void CheckRegex(string where, string pattern)
{
    if (string.IsNullOrEmpty(pattern)) return;
    try { _ = new Regex(pattern); }
    catch (Exception ex) { errors.Add($"{where}: invalid regex '{pattern}' — {ex.Message}"); }
}

// Report.
foreach (var w in warnings) Console.WriteLine($"WARN   {w}");
foreach (var e in errors) Console.WriteLine($"ERROR  {e}");

Console.WriteLine($"\n{bundle.Languages.Count} language(s), {bundle.MappingTables.Count} mapping table(s). " +
                  $"{errors.Count} error(s), {warnings.Count} warning(s).");

return errors.Count > 0 ? 1 : 0;

static Dictionary<string, HashSet<string>> LoadLimits(string root)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    string path = Path.Combine(root, "encoding-limits.json");
    if (!File.Exists(path)) return result;
    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    foreach (var enc in doc.RootElement.EnumerateObject())
        if (enc.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
            result[enc.Name] = enc.Value.EnumerateArray()
                .Where(v => v.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(v => v.GetString()!.ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    return result;
}

static string FindDefaultRoot()
{
    foreach (var candidate in new[] { "config", Path.Combine("assets", "config") })
        if (Directory.Exists(candidate)) return candidate;
    return "config";
}
