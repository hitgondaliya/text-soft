using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

// GapFill — add the characters a mapping table is missing, using Shree's own
// converter as the source of truth.
//
// FullTest reports the exact characters a table cannot produce (Gujarati's
// independent vowels, the Bangla danda, Tamil ள …). gapunits.ps1 writes those as
// a probe, convdrive.ps1 pushes it through Converter nxt, and this merges the
// answer into both directions of the table. Nothing is guessed: a unit is only
// added when Shree gave back a legacy string that is actually different from what
// went in — an echoed line means the converter had no answer either.
//
//   gapfill <units.txt> <converted.txt> <encoding-id> [--map-dir assets/config/mappings] [--dry-run]

// --prune <dir>: drop pairs that map a character to nothing. A capture that came
// back blank for a unit was once stored as from="(" to="", which does not leave
// the character alone — it deletes it, so a Bangla document lost its brackets on
// the way back to Unicode. With no pair at all the character falls through as
// itself and is reported unmapped, which is visible and recoverable.
if (args.Length >= 1 && args[0] == "--prune")
{
    string dir = args.Length > 1 ? args[1] : Path.Combine("assets", "config", "mappings");
    int files = 0, removed = 0;
    foreach (string p in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
    {
        var doc = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        if (doc["pairs"] is not JsonArray pairs) continue;
        var drop = new List<int>();
        for (int i = pairs.Count - 1; i >= 0; i--)
            if (((string?)pairs[i]!["to"] ?? "").Length == 0) drop.Add(i);
        if (drop.Count == 0) continue;
        foreach (int i in drop)
        {
            Console.WriteLine("  - " + Path.GetFileName(p) + ": '" + (string?)pairs[i]!["from"] + "' mapped to nothing");
            pairs.RemoveAt(i);
        }
        File.WriteAllText(p, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
        files++; removed += drop.Count;
    }
    Console.WriteLine("pruned " + removed + " pair(s) from " + files + " table(s)");
    return 0;
}

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: gapfill <units.txt> <converted.txt> <encoding> [--map-dir <dir>] [--dry-run]");
    Console.Error.WriteLine("       gapfill --prune [<mappings dir>]");
    return 1;
}

string unitsPath = args[0], convPath = args[1], enc = args[2];
string mapDir = Path.Combine("assets", "config", "mappings");
bool dryRun = false;
for (int i = 3; i < args.Length; i++)
{
    if (args[i] == "--map-dir" && i + 1 < args.Length) mapDir = args[++i];
    else if (args[i] == "--dry-run") dryRun = true;
}

static string[] Lines(string p) =>
    File.ReadAllText(p, Encoding.UTF8).Replace("\r\n", "\n").Replace('\r', '\n')
        .Split('\n').Where(l => l.Length > 0).ToArray();

var units = Lines(unitsPath);
var conv = Lines(convPath);
if (units.Length != conv.Length)
{
    Console.Error.WriteLine($"line mismatch: {units.Length} units vs {conv.Length} converted — refusing to guess the alignment");
    return 2;
}

string toLegacyPath = Path.Combine(mapDir, "unicode-to-" + enc + ".json");
string toUnicodePath = Path.Combine(mapDir, enc + "-to-unicode.json");
foreach (string p in new[] { toLegacyPath, toUnicodePath })
    if (!File.Exists(p)) { Console.Error.WriteLine("no such table: " + p); return 2; }

var toLegacy = JsonNode.Parse(File.ReadAllText(toLegacyPath))!.AsObject();
var toUnicode = JsonNode.Parse(File.ReadAllText(toUnicodePath))!.AsObject();
var lPairs = toLegacy["pairs"]!.AsArray();
var uPairs = toUnicode["pairs"]!.AsArray();

var haveUni = new HashSet<string>(lPairs.Select(p => (string?)p!["from"] ?? ""), StringComparer.Ordinal);
var haveLeg = new HashSet<string>(uPairs.Select(p => (string?)p!["from"] ?? ""), StringComparer.Ordinal);

int added = 0, echoed = 0, dupUni = 0, dupLeg = 0;
for (int i = 0; i < units.Length; i++)
{
    string u = units[i], l = conv[i];
    if (l.Length == 0) { echoed++; continue; }
    // The converter leaves a unit it does not know exactly as it found it. Storing
    // that would map the character to itself and render as a stray Latin letter.
    if (string.Equals(u, l, StringComparison.Ordinal)) { echoed++; continue; }
    if (haveUni.Contains(u)) { dupUni++; }
    else
    {
        lPairs.Add(new JsonObject { ["from"] = u, ["to"] = l });
        haveUni.Add(u);
        added++;
        Console.WriteLine("  + " + Describe(u) + "  ->  " + Describe(l));
    }
    // The reverse direction is a separate table and can already own this legacy
    // string through a longer cluster; first writer wins, as everywhere else.
    if (haveLeg.Contains(l)) { dupLeg++; }
    else { uPairs.Add(new JsonObject { ["from"] = l, ["to"] = u }); haveLeg.Add(l); }
}

Console.WriteLine($"{enc}: added={added} already-present={dupUni} reverse-already-present={dupLeg} no-answer-from-converter={echoed}");
if (dryRun) { Console.WriteLine("(dry run — nothing written)"); return 0; }
if (added == 0) { Console.WriteLine("nothing to write"); return 0; }

var opts = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
File.WriteAllText(toLegacyPath, toLegacy.ToJsonString(opts));
File.WriteAllText(toUnicodePath, toUnicode.ToJsonString(opts));
Console.WriteLine("wrote " + toLegacyPath + " and " + toUnicodePath);
return 0;

static string Describe(string s) =>
    s + " [" + string.Join(" ", s.Select(c => ((int)c).ToString("X4"))) + "]";
