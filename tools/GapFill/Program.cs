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

// --strip-joiners [<dir>]: take the zero-width joiners out of the legacy side of
// every pair. A joiner is a Unicode instruction about shaping; a legacy encoding is
// bytes a font draws, and has no such thing. They arrived from captures of Shree's
// own converter, which writes a half letter as "क्‍" - with a joiner - and every
// half-letter pair in twelve tables ended up with one sitting on the legacy side.
// The damage: typing a half letter in Hindi put U+200D into the document, where no
// legacy font has a glyph, and reading legacy text back never matched the pair at
// all, because real legacy text has no joiner in it. Gujarati was unaffected only
// because its half letters are single glyphs that came from a different capture.
//
// U+00AD is deliberately left alone: in ISFOC and Akruti that byte draws a letter.
if (args.Length >= 1 && args[0] == "--strip-joiners")
{
    string dir = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : Path.Combine("assets", "config", "mappings");
    bool dry = args.Contains("--dry-run");
    var writer = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    char[] joiners = { '​', '‌', '‍', '‎', '‏', '﻿' };
    int touched = 0, cleaned = 0, removedPairs = 0;

    foreach (string p in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
    {
        var doc = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        if (doc["pairs"] is not JsonArray pairs) continue;
        // In a unicode-to-<enc> table the legacy text is "to"; in <enc>-to-unicode it is "from".
        string side = Path.GetFileName(p).StartsWith("unicode-to-", StringComparison.OrdinalIgnoreCase) ? "to" : "from";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int here = 0, gone = 0;

        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            string legacy = (string?)pairs[i]![side] ?? "";
            if (legacy.IndexOfAny(joiners) < 0) continue;
            string clean = new(legacy.Where(c => Array.IndexOf(joiners, c) < 0).ToArray());
            if (clean.Length == 0) { pairs.RemoveAt(i); gone++; continue; }
            pairs[i]![side] = clean;
            here++;
        }
        // Cleaning can make two pairs say the same thing; the first one wins, as everywhere.
        for (int i = 0; i < pairs.Count; i++)
        {
            string key = ((string?)pairs[i]!["from"] ?? "") + "\u0001" + ((string?)pairs[i]!["to"] ?? "");
            if (seen.Add(key)) continue;
            pairs.RemoveAt(i--); gone++;
        }
        if (here == 0 && gone == 0) continue;

        Console.WriteLine($"  {Path.GetFileName(p),-36} cleaned {here,4}   dropped {gone}");
        cleaned += here; removedPairs += gone; touched++;
        if (!dry) File.WriteAllText(p, doc.ToJsonString(writer));
    }

    Console.WriteLine($"\n{cleaned} pair(s) cleaned and {removedPairs} removedPairs across {touched} table(s)");
    if (dry) Console.WriteLine("(dry run - nothing written)");
    return 0;
}

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

// --plan <file.json>: add pairs worked out from evidence that is already on disk
// rather than from a fresh capture. The plan says, per encoding, which pairs to add
// and which characters the encoding has no room for:
//
//   { "shree-tam7": { "pairs": [ { "from": "ொ", "to": "…", "why": "…" } ],
//                     "limits": [ "0BE6", "0BE7" ] } }
//
// Everything it writes has to be derivable from something the repository can show:
// the same table read the other way, the two pieces a two-part matra is written
// from, or the font's own cmap. See the tool that builds the plan.
if (args.Length >= 2 && args[0] == "--plan")
{
    string planPath = args[1];
    string dir = Path.Combine("assets", "config", "mappings");
    bool dry = args.Contains("--dry-run");
    for (int i = 2; i < args.Length; i++) if (args[i] == "--map-dir" && i + 1 < args.Length) dir = args[++i];

    var writer = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    var plan = JsonNode.Parse(File.ReadAllText(planPath))!.AsObject();
    string limitsFile = Path.Combine(Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar)) ?? ".", "encoding-limits.json");
    var limitsDoc = File.Exists(limitsFile) ? JsonNode.Parse(File.ReadAllText(limitsFile))!.AsObject() : new JsonObject();
    int totalPairs = 0, totalLimits = 0, tables = 0;

    foreach (var (encoding, node) in plan)
    {
        string toLegacyFile = Path.Combine(dir, "unicode-to-" + encoding + ".json");
        string toUnicodeFile = Path.Combine(dir, encoding + "-to-unicode.json");
        if (!File.Exists(toLegacyFile) || !File.Exists(toUnicodeFile))
        { Console.Error.WriteLine("  no table pair for " + encoding); continue; }

        var toLegacyDoc = JsonNode.Parse(File.ReadAllText(toLegacyFile))!.AsObject();
        var toUnicodeDoc = JsonNode.Parse(File.ReadAllText(toUnicodeFile))!.AsObject();
        var toLegacyPairs = toLegacyDoc["pairs"]!.AsArray();
        var toUnicodePairs = toUnicodeDoc["pairs"]!.AsArray();
        var haveUnicode = new HashSet<string>(toLegacyPairs.Select(p => (string?)p!["from"] ?? ""), StringComparer.Ordinal);
        var haveLegacy = new HashSet<string>(toUnicodePairs.Select(p => (string?)p!["from"] ?? ""), StringComparer.Ordinal);

        int here = 0;
        foreach (var p in node?["pairs"]?.AsArray() ?? new JsonArray())
        {
            string from = (string?)p!["from"] ?? "", to = (string?)p["to"] ?? "";
            if (from.Length == 0 || to.Length == 0 || haveUnicode.Contains(from)) continue;
            toLegacyPairs.Add(new JsonObject { ["from"] = from, ["to"] = to });
            haveUnicode.Add(from);
            here++;
            Console.WriteLine($"  + {encoding,-22} {Describe(from)} -> {Describe(to)}   {(string?)p["why"]}");
            if (!haveLegacy.Contains(to))
            { toUnicodePairs.Add(new JsonObject { ["from"] = to, ["to"] = from }); haveLegacy.Add(to); }
        }

        var noRoom = new SortedSet<string>(StringComparer.Ordinal);
        if (limitsDoc[encoding] is JsonArray existing) foreach (var v in existing) noRoom.Add((string?)v! ?? "");
        int before = noRoom.Count;
        foreach (var v in node?["limits"]?.AsArray() ?? new JsonArray()) noRoom.Add((string?)v! ?? "");
        if (noRoom.Count != before)
        {
            var arr = new JsonArray();
            foreach (string h in noRoom) arr.Add(h);
            limitsDoc[encoding] = arr;
            totalLimits += noRoom.Count - before;
        }

        if (here > 0 && !dry)
        {
            File.WriteAllText(toLegacyFile, toLegacyDoc.ToJsonString(writer));
            File.WriteAllText(toUnicodeFile, toUnicodeDoc.ToJsonString(writer));
            tables++;
        }
        totalPairs += here;
    }

    if (!dry && totalLimits > 0) File.WriteAllText(limitsFile, limitsDoc.ToJsonString(writer));
    Console.WriteLine($"\n{totalPairs} pair(s) added across {tables} table(s), {totalLimits} encoding limit(s) recorded");
    if (dry) Console.WriteLine("(dry run - nothing written)");
    return 0;
}

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: gapfill <units.txt> <converted.txt> <encoding> [--map-dir <dir>] [--dry-run]");
    Console.Error.WriteLine("       gapfill --plan <plan.json> [--map-dir <dir>] [--dry-run]");
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

var probe = Lines(unitsPath);
string[] units, conv;

// A fenced probe alternates fence / unit / fence / unit … / fence. Shree drops a
// line it cannot convert, so straight position matching falls apart the moment one
// unit has no answer — which is exactly the kind of unit a gap probe is made of.
// Re-syncing on the fence recovers the alignment and, better, records the empty
// answer as an empty answer rather than shifting every later unit by one.
bool fenced = probe.Length >= 3 && probe.Length % 2 == 1 && probe[0] == probe[2];
if (fenced)
{
    string fence = probe[0];
    var rawConv = Lines(convPath);
    var wanted = new List<string>();
    for (int i = 1; i < probe.Length; i += 2) wanted.Add(probe[i]);

    string? fenceOut = rawConv.Length > 0 ? rawConv[0] : null;
    if (fenceOut is null) { Console.Error.WriteLine("converted file is empty"); return 2; }

    var answers = new List<string>();
    for (int i = 0; i < rawConv.Length; i++)
    {
        if (rawConv[i] != fenceOut) continue;
        // between this fence and the next lies this unit's answer, or nothing
        answers.Add(i + 1 < rawConv.Length && rawConv[i + 1] != fenceOut ? rawConv[i + 1] : "");
    }
    if (answers.Count > 0) answers.RemoveAt(answers.Count - 1);   // closing fence
    if (answers.Count != wanted.Count)
    {
        Console.Error.WriteLine($"fence mismatch: {wanted.Count} units but {answers.Count} fenced answers — refusing to guess");
        return 2;
    }
    units = wanted.ToArray();
    conv = answers.ToArray();
    Console.WriteLine($"fenced probe: {units.Length} units, {conv.Count(c => c.Length == 0)} with no answer from the converter");
}
else
{
    units = probe;
    conv = Lines(convPath);
    if (units.Length != conv.Length)
    {
        Console.Error.WriteLine($"line mismatch: {units.Length} units vs {conv.Length} converted — refusing to guess the alignment");
        return 2;
    }
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

// What the table already produces for a string, so an answer that adds nothing can
// be told apart from one that does.
var known = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var p in lPairs) known[(string?)p!["from"] ?? ""] = (string?)p["to"] ?? "";

// Characters Shree's own converter will not put into this encoding. Recorded as
// evidence so the full-matrix run can tell "the encoding cannot express this" from
// "our table is missing it", instead of carrying a hand-written list of exceptions.
var limits = new SortedSet<char>();

int added = 0, echoed = 0, dupUni = 0, dupLeg = 0, dropped = 0;
for (int i = 0; i < units.Length; i++)
{
    string u = units[i], l = conv[i];
    if (l.Length == 0)
    {
        echoed++;
        // A single character the converter answers with nothing is one this
        // encoding has no room for at all.
        if (u.Length == 1) limits.Add(u[0]);
        continue;
    }
    // The converter leaves a unit it does not know exactly as it found it. Storing
    // that would map the character to itself and render as a stray Latin letter.
    if (string.Equals(u, l, StringComparison.Ordinal)) { echoed++; continue; }

    // "ક઼" answered with exactly what "ક" gives means the font has no nukta glyph
    // and Shree simply discarded the mark. Storing that as a pair would make our
    // converter discard it too — silently, with nothing reported to the user. Far
    // better to leave it unmapped: the character then comes back as itself and is
    // listed in UnmappedCharacters, which is visible and recoverable.
    if (u.Length > 1 && known.TryGetValue(u[..^1], out string? shorter) && shorter == l)
    {
        dropped++;
        foreach (char c in u[(u.Length - 1)..]) limits.Add(c);
        continue;
    }
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

Console.WriteLine($"{enc}: added={added} already-present={dupUni} reverse-already-present={dupLeg} "
                + $"no-answer-from-converter={echoed} mark-discarded-by-converter={dropped}");
if (dryRun) { Console.WriteLine("(dry run — nothing written)"); return 0; }

var opts = new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

if (limits.Count > 0)
{
    string limitsPath = Path.Combine(Path.GetDirectoryName(mapDir.TrimEnd(Path.DirectorySeparatorChar)) ?? ".", "encoding-limits.json");
    var doc = File.Exists(limitsPath)
        ? JsonNode.Parse(File.ReadAllText(limitsPath))!.AsObject()
        : new JsonObject();
    var have = new SortedSet<char>();
    if (doc[enc] is JsonArray old)
        foreach (var n in old) have.Add((char)Convert.ToInt32((string?)n! ?? "0", 16));
    foreach (char c in limits) have.Add(c);
    var arr = new JsonArray();
    foreach (char c in have) arr.Add(((int)c).ToString("X4"));
    doc[enc] = arr;
    File.WriteAllText(limitsPath, doc.ToJsonString(opts));
    Console.WriteLine("recorded " + limits.Count + " encoding limit(s) in " + limitsPath);
}

if (added == 0) { Console.WriteLine("no new pairs to write"); return 0; }
File.WriteAllText(toLegacyPath, toLegacy.ToJsonString(opts));
File.WriteAllText(toUnicodePath, toUnicode.ToJsonString(opts));
Console.WriteLine("wrote " + toLegacyPath + " and " + toUnicodePath);
return 0;

static string Describe(string s) =>
    s + " [" + string.Join(" ", s.Select(c => ((int)c).ToString("X4"))) + "]";
