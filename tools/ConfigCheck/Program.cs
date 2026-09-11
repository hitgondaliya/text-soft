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

// Collect all font ids across languages for reference checks.
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
        else fontIds.Add(font.Id);
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

static string FindDefaultRoot()
{
    foreach (var candidate in new[] { "config", Path.Combine("assets", "config") })
        if (Directory.Exists(candidate)) return candidate;
    return "config";
}
