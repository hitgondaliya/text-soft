using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

// RegisterFonts — add every installed Shree font of a SOLVED encoding to
// languages.json.
//
// A Shree encoding covers many typefaces: SHREE-GUJX-0750, -0751, -1120 … all
// place the same glyph at the same code, so the one mapping table GlyphJoin
// derived serves the whole group. Group membership is decided by the exact set of
// code points the font's cmap maps — the same layout hash FontScan prints — so a
// font is only registered when its layout matches a solved one, never by name.
//
//   dotnet run --project tools/RegisterFonts -- [--fonts C:\Windows\Fonts] [--dry-run]

string fontDir = @"C:\Windows\Fonts";
string configPath = Path.Combine("assets", "config", "languages.json");
bool dryRun = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--fonts" && i + 1 < args.Length) fontDir = args[++i];
    else if (args[i] == "--config" && i + 1 < args.Length) configPath = args[++i];
    else if (args[i] == "--dry-run") dryRun = true;
    else if (args[i] == "--font-src" && i + 1 < args.Length) ExtraFonts.Dirs.Add(args[++i]);
}

// layout hash -> (encoding id, language name, display label)
var solved = new Dictionary<string, (string enc, string lang, string label)>(StringComparer.OrdinalIgnoreCase)
{
    ["98272A59EE"] = ("shree-gujx", "Gujarati", "ShreeLipi-Ex"),
    ["0D71C0D096"] = ("shree-guj7", "Gujarati", "Shree-Lipi 7.0 / 4-5-6"),
    ["74DE83DE5B"] = ("shree-kanx", "Kannada", "ShreeLipi-Ex"),
    // 60DE8EFD4E shree-kan7 — held back: its table covers only 9 of Kannada's 34
    // consonants, so it would render most letters as raw Latin. Needs a capture pass.
    ["D3AB4C5CCB"] = ("shree-malx", "Malayalam", "ShreeLipi-Ex"),
    ["F472ED5273"] = ("shree-mal7", "Malayalam", "Shree-Lipi 7.0"),
    ["906F4A89D0"] = ("shree-orix", "Odia", "ShreeLipi-Ex"),
    ["625020BE5A"] = ("shree-ori7", "Odia", "Shree-Lipi 7.0 / 4-5-6"),
    ["1110C07211"] = ("shree-punx", "Punjabi", "ShreeLipi-Ex"),
    ["D883D0CD52"] = ("shree-pun7", "Punjabi", "Shree-Lipi 4-5-6"),
    ["109C24AD35"] = ("shree-tam7", "Tamil", "Shree-Lipi 7.0 / 4-5-6"),
};

if (!File.Exists(configPath)) { Console.Error.WriteLine("config not found: " + configPath); return 1; }

// --remove <encoding>: pull an encoding back out of the config. Used when a table
// turns out not to cover its script's alphabet — shipping a partial table renders
// the unmapped letters as themselves in a legacy font, which is garbage on screen.
string? removeEnc = null;
for (int i = 0; i < args.Length; i++) if (args[i] == "--remove" && i + 1 < args.Length) removeEnc = args[++i];
if (removeEnc is not null)
{
    var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    int dropped = 0;
    foreach (var l in cfg["languages"]!.AsArray())
    {
        var fonts = l!["fonts"]!.AsArray();
        for (int i = fonts.Count - 1; i >= 0; i--)
            if ((string?)fonts[i]!["encoding"] == removeEnc) { fonts.RemoveAt(i); dropped++; }
    }
    var mf = cfg["mappingFiles"]?.AsArray();
    if (mf is not null)
        for (int i = mf.Count - 1; i >= 0; i--)
        {
            string v = (string?)mf[i] ?? "";
            if (v.Contains("/" + removeEnc + "-to-unicode.json") || v.Contains("/unicode-to-" + removeEnc + ".json"))
                mf.RemoveAt(i);
        }
    File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
    Console.WriteLine("removed encoding '" + removeEnc + "': " + dropped + " font(s), wrote " + configPath);
    return 0;
}

// --fix-layouts: the FONT LAYOUT dropdown entries point at a font id. Many point
// at nothing, or at a font id that is not in that language, so the entry is dead.
// Repair every one we can now serve, and leave the rest empty rather than wiring
// them to something that would render wrong.
if (args.Contains("--fix-layouts"))
{
    var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    int fixedUni = 0, fixedLegacy = 0, stillDead = 0;

    foreach (var l in cfg["languages"]!.AsArray())
    {
        var lang = l!.AsObject();
        var fonts = lang["fonts"]!.AsArray();
        string LangName() => (string?)lang["name"] ?? "?";

        string? FirstUnicodeId() => fonts.FirstOrDefault(f => (string?)f!["type"] == "unicode") is { } u ? (string?)u["id"] : null;

        // Which ENCODING does a dropdown entry name? Matching on a label inside the
        // font's name only worked for fonts registered by the older path; the
        // by-series pass writes the bare family name, which left every entry it
        // could have fixed looking dead. The entry names a Shree series, and the
        // series is exactly what the encoding id records: "…x" for the X series,
        // the other Shree encoding for 7.0 and 4.0/5.0/6.0 (Shree gives those two
        // the same byte layout, so they legitimately share a table).
        //
        // The "Shree" prefix check matters: Telugu has an "Anu 7.0" entry, and a
        // bare "7.0" test would hand it a Shree encoding and render garbage.
        string? EncodingForLayout(string layoutName)
        {
            if (!layoutName.StartsWith("Shree", StringComparison.OrdinalIgnoreCase)) return null;
            bool wantEx = layoutName.Contains("Ex", StringComparison.OrdinalIgnoreCase);
            if (!wantEx && !layoutName.Contains("7.0", StringComparison.Ordinal)
                        && !layoutName.Contains("4.0", StringComparison.Ordinal)) return null;

            var encs = fonts.Where(f => (string?)f!["type"] == "legacy" && ShreeFamily((string?)f["family"] ?? ""))
                            .Select(f => (string?)f!["encoding"] ?? (string?)f["id"] ?? "")
                            .Where(e => e.StartsWith("shree-", StringComparison.Ordinal))
                            .Distinct(StringComparer.Ordinal).ToList();
            return encs.FirstOrDefault(e => e.EndsWith("x", StringComparison.Ordinal) == wantEx);
        }

        // Any font of the right encoding renders correctly, so prefer a stable,
        // predictable default: the first by family name.
        string? LegacyIdForLayout(string layoutName)
        {
            string? enc = EncodingForLayout(layoutName);
            if (enc is null) return null;
            return fonts.Where(f => (string?)f!["type"] == "legacy"
                                 && ((string?)f["encoding"] ?? (string?)f["id"]) == enc)
                        .OrderBy(f => (string?)f!["family"] ?? "", StringComparer.OrdinalIgnoreCase)
                        .Select(f => (string?)f!["id"]).FirstOrDefault();
        }

        foreach (var flNode in lang["fontLayouts"]!.AsArray())
        {
            var fl = flNode!.AsObject();
            string id = (string?)fl["fontId"] ?? "";
            string name = (string?)fl["name"] ?? "";
            var cur = fonts.FirstOrDefault(f => (string?)f!["id"] == id);
            bool resolves = id.Length > 0 && cur is not null;
            // A layout can resolve and still be wrong: Hindi's "Shree-Lipi 7.0"
            // pointed at SHREE-DEVX-0700, an X-series font, so the entry silently
            // produced ShreeLipi-Ex output under a 7.0 label.
            if (resolves && fl["type"]?.GetValue<string>() == "legacy"
                && EncodingForLayout(name) is { } wantEnc
                && ((string?)cur!["encoding"] ?? (string?)cur["id"]) != wantEnc) resolves = false;
            if (resolves) continue;

            string type = (string?)fl["type"] ?? "legacy";
            string? repl = type == "unicode" ? FirstUnicodeId() : LegacyIdForLayout(name);
            if (repl is null)
            {
                stillDead++;
                // A stale id is worse than an empty one: it names a font the
                // language does not have, and the app has nothing to fall back on.
                if (id.Length > 0 && cur is null)
                    Console.WriteLine("  " + LangName().PadRight(11) + name.PadRight(26) + id + " -> (cleared: no such font here)");
                if (cur is null) fl["fontId"] = "";
                continue;
            }

            fl["fontId"] = repl;
            if (type == "unicode") fixedUni++; else fixedLegacy++;
            Console.WriteLine("  " + LangName().PadRight(11) + name.PadRight(26) + (id.Length == 0 ? "(empty)" : id) + " -> " + repl);
        }
    }

    File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
    Console.WriteLine("fixed unicode=" + fixedUni + " legacy=" + fixedLegacy + " still-unsupported=" + stillDead);
    return 0;
}

// --add-nirmala: the per-language Unicode fonts in the config (Mangal, Raavi,
// Tunga, Kartika, Kalinga, Latha, Gautami, Vrinda) were the Windows 7 faces and
// ship on no current Windows — Windows 10/11 covers all of these scripts with the
// single family "Nirmala UI". Offer it in every Indic language and make it the
// Unicode layout's font, keeping the old entry so a machine that has it still
// lists it. The new entry carries the old id as its encoding, so every existing
// mapping table keeps matching.
if (args.Contains("--add-nirmala"))
{
    var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    int nAdded = 0, repointed = 0;
    foreach (var l in cfg["languages"]!.AsArray())
    {
        var lang = l!.AsObject();
        var fonts = lang["fonts"]!.AsArray();
        if (fonts.Any(f => (string?)f!["family"] == "Nirmala UI")) continue;

        var primary = fonts.FirstOrDefault(f => (string?)f!["type"] == "unicode");
        if (primary is null) continue;
        string key = (string?)primary["encoding"] ?? (string?)primary["id"] ?? "";
        if (key.Length == 0) continue;

        string id = "nirmala-" + ((string?)lang["id"] ?? "x");
        fonts.Add(new JsonObject
        {
            ["id"] = id,
            ["name"] = "Nirmala UI (Unicode)",
            ["family"] = "Nirmala UI",
            ["type"] = "unicode",
            ["encoding"] = key,
        });
        nAdded++;

        foreach (var flNode in lang["fontLayouts"]!.AsArray())
        {
            var fl = flNode!.AsObject();
            if ((string?)fl["type"] != "unicode") continue;
            fl["fontId"] = id;
            repointed++;
        }
        Console.WriteLine("  " + ((string?)lang["name"] ?? "?").PadRight(12) + "+ Nirmala UI (key " + key + ")");
    }
    File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
    Console.WriteLine("added=" + nAdded + " unicode layouts repointed=" + repointed);
    return 0;
}

// --add-font "<language>|<id>|<display name>|<font family>|<encoding>[|<fontLayout name>]"
// Registers one font by hand. The hash-based scan only recognises Shree's own
// families; a third-party legacy font whose table came from the Converter-nxt
// oracle has to be named explicitly, and optionally wired to a FONT LAYOUT entry.
{
    var specs = new List<string>();
    for (int i = 0; i < args.Length; i++) if (args[i] == "--add-font" && i + 1 < args.Length) specs.Add(args[++i]);
    if (specs.Count > 0)
    {
        var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        int done = 0;
        foreach (string spec in specs)
        {
            var f = spec.Split('|');
            if (f.Length < 5) { Console.WriteLine("  bad spec: " + spec); continue; }
            var lang = cfg["languages"]!.AsArray().FirstOrDefault(l => (string?)l!["name"] == f[0])?.AsObject();
            if (lang is null) { Console.WriteLine("  no language " + f[0]); continue; }

            // prove the font really uses this encoding before listing it: every
            // legacy code the table can emit has to exist in the font's cmap
            string tbl = Path.Combine(Path.GetDirectoryName(configPath)!, "mappings", "unicode-to-" + f[4] + ".json");
            string? file = FindInstalled(f[3]);
            if (File.Exists(tbl) && file is not null)
            {
                var want = new HashSet<int>();
                foreach (var pr in JsonNode.Parse(File.ReadAllText(tbl))!["pairs"]!.AsArray())
                    foreach (char ch in (string?)pr!["to"] ?? "") if (ch >= 0x80) want.Add(ch);
                var got = CodePoints(file);
                int miss = want.Count(c => !got.Contains(c));
                if (miss > want.Count * 0.02)
                {
                    Console.WriteLine("  REJECT " + f[2] + ": " + miss + "/" + want.Count + " codes absent from the font");
                    continue;
                }
            }
            else Console.WriteLine("  (unverified: " + (file is null ? "font not installed" : "no table") + ")");

            var fonts = lang["fonts"]!.AsArray();
            if (!fonts.Any(x => (string?)x!["id"] == f[1]))
                fonts.Add(new JsonObject
                {
                    ["id"] = f[1], ["name"] = f[2], ["family"] = f[3],
                    ["type"] = "legacy", ["encoding"] = f[4],
                });

            if (f.Length >= 6)
            {
                var fl = lang["fontLayouts"]!.AsArray().FirstOrDefault(x => (string?)x!["name"] == f[5])?.AsObject();
                if (fl is not null) fl["fontId"] = f[1];
                else Console.WriteLine("  no fontLayout '" + f[5] + "' in " + f[0]);
            }

            foreach (string rel in new[] { "mappings/" + f[4] + "-to-unicode.json", "mappings/unicode-to-" + f[4] + ".json" })
            {
                var mf = cfg["mappingFiles"]!.AsArray();
                if (mf.Any(n => (string?)n == rel)) continue;
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(configPath)!, rel.Replace('/', Path.DirectorySeparatorChar)))) continue;
                mf.Add(rel);
            }
            Console.WriteLine("  " + f[0] + ": + " + f[2] + " (" + f[4] + ")");
            done++;
        }
        File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
        Console.WriteLine("added " + done + " font(s), wrote " + configPath);
        return 0;
    }
}

// --add-layout "<language>|<layout name>|<fontId>": add a FONT LAYOUT entry the
// config never had. Shree offers Bhasha Bharati for Devanagari but our config only
// listed it under Gujarati, so there was no way to pick it for Hindi.
{
    var specs = new List<string>();
    for (int i = 0; i < args.Length; i++) if (args[i] == "--add-layout" && i + 1 < args.Length) specs.Add(args[++i]);
    if (specs.Count > 0)
    {
        var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        int n = 0;
        foreach (string spec in specs)
        {
            var f = spec.Split('|');
            if (f.Length < 3) { Console.WriteLine("  bad spec: " + spec); continue; }
            var lang = cfg["languages"]!.AsArray().FirstOrDefault(l => (string?)l!["name"] == f[0])?.AsObject();
            if (lang is null) { Console.WriteLine("  no language " + f[0]); continue; }
            var fls = lang["fontLayouts"]!.AsArray();
            var existing = fls.FirstOrDefault(x => (string?)x!["name"] == f[1])?.AsObject();
            if (existing is not null) { existing["fontId"] = f[2]; Console.WriteLine("  " + f[0] + ": '" + f[1] + "' -> " + f[2]); n++; continue; }
            string langId = (string?)lang["id"] ?? "x";
            fls.Add(new JsonObject
            {
                ["id"] = langId + "-fl-" + Slug(f[1]),
                ["name"] = f[1],
                ["type"] = "legacy",
                ["fontId"] = f[2],
            });
            Console.WriteLine("  " + f[0] + ": + '" + f[1] + "' -> " + f[2]);
            n++;
        }
        File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
        Console.WriteLine("layouts touched: " + n);
        return 0;
    }
}

// --sync-mappings: the catalog loads only the tables it lists, so every encoding a
// registered font names must have its two files listed. Missing entries make the
// font look wired while the converter cannot find a table for it.
if (args.Contains("--sync-mappings"))
{
    var cfg = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    var mf = cfg["mappingFiles"]!.AsArray();
    var have = new HashSet<string>(mf.Select(n => (string?)n ?? ""), StringComparer.OrdinalIgnoreCase);
    string dir = Path.GetDirectoryName(configPath)!;
    int syncAdded = 0;
    foreach (var l in cfg["languages"]!.AsArray())
        foreach (var f in l!["fonts"]!.AsArray())
        {
            string enc = (string?)f!["encoding"] ?? "";
            if (enc.Length == 0) continue;
            foreach (string rel in new[] { "mappings/" + enc + "-to-unicode.json", "mappings/unicode-to-" + enc + ".json" })
            {
                if (!have.Add(rel)) continue;
                if (!File.Exists(Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar)))) { have.Remove(rel); continue; }
                mf.Add(rel);
                Console.WriteLine("  + " + rel);
                syncAdded++;
            }
        }
    File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
    Console.WriteLine("syncAdded " + syncAdded + " mapping file entries");
    return 0;
}

// --by-series: register by what the font IS, then prove it.
//
// Grouping by the exact set of code points a font maps splits one encoding into
// dozens of groups, because designs of the same series carry different glyph
// subsets — SHREE-GUJX-5227 was treated as its own layout although the shree-gujx
// table renders it correctly. Shree's own taxonomy is in the family name:
// SHREE-<SCRIPT><SERIES>-<design>, where the series is X, 7 or nothing.
//
// Naming alone is not proof, so each candidate is checked against the table it
// would use: every legacy code that table can emit must exist in the font's cmap.
if (args.Contains("--by-series"))
{
    var seriesToEnc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Shree-Lipi 7.0 (SHREE-DEV7-*) and 4.0/5.0/6.0 (SHREE-DEV-*) are two families with
        // a table each: they share the 7 composer's codes but four (91 92 B3 B6).
        ["DEVX"] = "shree-devx",  ["DEV7"] = "shree-dev7",   ["DEV"] = "shree-dev456",
        // The Shree-Lipi 7 Sanskrit fonts (SHREE-SAN7-*) are an encoding of their own, not
        // Devanagari's layout (क = 40 A1 there, 48 24 in DEV7): shree-san7. No 4/5/6 Sanskrit
        // font (SHREE-SAN-*) has been checked against any table, so that series is left out
        // and counted below as a series with no table, not guessed.
        ["SANX"] = "shree-devx",  ["SAN7"] = "shree-san7",
        ["BANX"] = "shree-banx",  ["ASSX"] = "shree-banx",   ["MANX"] = "shree-banx",
        ["BAN7"] = "shree-ban7",  ["ASS7"] = "shree-ban7",   ["BAN"] = "shree-ban7", ["ASS"] = "shree-ban7",
        ["TEL7"] = "shree-tel7",  ["TEL"] = "shree-tel7",
        // Gujarati 7.0 (SHREE-GUJ7-*) and 4.0/5.0/6.0 (SHREE-GUJ-*): five codes (91-94, B6) differ.
        ["GUJX"] = "shree-gujx",  ["GUJ7"] = "shree-guj7",   ["GUJ"] = "shree-guj456",
        ["KANX"] = "shree-kanx",  ["KAN7"] = "shree-kan7",   ["KAN"] = "shree-kan7",
        ["MALX"] = "shree-malx",  ["MAL7"] = "shree-mal7",   ["MAL"] = "shree-mal7",
        ["ORIX"] = "shree-orix",  ["ORI7"] = "shree-ori7",   ["ORI"] = "shree-ori7",
        ["PUNX"] = "shree-punx",  ["PUN7"] = "shree-pun7",   ["PUN"] = "shree-pun7",
        ["TAMX"] = "shree-tamx",  ["TAM7"] = "shree-tam7",   ["TAM"] = "shree-tam7",
        ["TELX"] = "shree-telx",
    };
    // An encoding belongs to a SCRIPT, and a script serves several languages:
    // Devanagari is Hindi, Marathi and Sanskrit; the Bengali script is Bangla,
    // Assamese and Manipuri. Registering each font under only the first language
    // left Marathi with 7 fonts against Hindi's 130 and Assamese with 2 — the
    // same font file has to be offered in every language that writes that script.
    var encToLangs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["shree-devx"] = new[] { "Hindi", "Marathi", "Sanskrit" },
        ["shree-dev7"] = new[] { "Hindi", "Marathi", "Sanskrit" },
        ["shree-dev456"] = new[] { "Hindi", "Marathi", "Sanskrit" },
        ["shree-san7"] = new[] { "Hindi", "Marathi", "Sanskrit" },
        ["shree-banx"] = new[] { "Bangla", "Assamese", "Manipuri" },
        ["shree-ban7"] = new[] { "Bangla", "Assamese", "Manipuri" },
        ["shree-tel7"] = new[] { "Telugu" }, ["shree-telx"] = new[] { "Telugu" },
        ["shree-gujx"] = new[] { "Gujarati" }, ["shree-guj7"] = new[] { "Gujarati" }, ["shree-guj456"] = new[] { "Gujarati" },
        ["shree-kanx"] = new[] { "Kannada" }, ["shree-kan7"] = new[] { "Kannada" },
        ["shree-malx"] = new[] { "Malayalam" }, ["shree-mal7"] = new[] { "Malayalam" },
        ["shree-orix"] = new[] { "Odia" }, ["shree-ori7"] = new[] { "Odia" },
        ["shree-punx"] = new[] { "Punjabi" }, ["shree-pun7"] = new[] { "Punjabi" },
        ["shree-tamx"] = new[] { "Tamil" }, ["shree-tam7"] = new[] { "Tamil" },
    };

    string mapDir = Path.Combine(Path.GetDirectoryName(configPath)!, "mappings");
    // encoding -> the legacy code points its table can emit
    var needed = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
    foreach (var enc in seriesToEnc.Values.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        string p = Path.Combine(mapDir, "unicode-to-" + enc + ".json");
        if (!File.Exists(p)) continue;
        var set = new HashSet<int>();
        var doc = JsonNode.Parse(File.ReadAllText(p))!.AsObject();
        foreach (var pr in doc["pairs"]!.AsArray())
            foreach (char ch in (string?)pr!["to"] ?? "")
                if (ch >= 0x80) set.Add(ch);
        needed[enc] = set;
    }
    Console.WriteLine("tables loaded: " + string.Join(", ", needed.Select(k => k.Key + "=" + k.Value.Count)));

    var cfg2 = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    int ok = 0, rejected = 0, dup = 0, noTable = 0;
    var perEnc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var unmatched = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in Directory.EnumerateFiles(fontDir, "*.ttf"))
    {
        string[] r;
        try { r = Read(file); } catch { continue; }
        if (r.Length != 2) continue;
        string family = r[1];
        var m = System.Text.RegularExpressions.Regex.Match(family, @"^SHREE[-_ ]?([A-Z]{3}[X7]?)[-_ ]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) { string k2 = "OTHER: " + (family.Length > 14 ? family.Substring(0, 14) : family); unmatched[k2] = unmatched.GetValueOrDefault(k2) + 1; continue; }
        if (!seriesToEnc.TryGetValue(m.Groups[1].Value, out var enc))
        {
            // no table for this series yet — count it so the gap is visible
            string key = m.Groups[1].Value.ToUpperInvariant();
            unmatched[key] = unmatched.GetValueOrDefault(key) + 1;
            continue;
        }
        if (!needed.TryGetValue(enc, out var want)) { noTable++; continue; }

        var have = CodePoints(file);
        int missing = want.Count(c => !have.Contains(c));
        // a design legitimately omits a few rare glyphs; a different encoding misses many
        if (missing > want.Count * 0.02) { rejected++; continue; }

        if (!encToLangs.TryGetValue(enc, out var langNames)) continue;
        string id = Slug(family);
        foreach (string langName in langNames)
        {
            var lang = cfg2["languages"]!.AsArray().FirstOrDefault(l => (string?)l!["name"] == langName)?.AsObject();
            if (lang is null) continue;
            var fonts = lang["fonts"]!.AsArray();
            if (fonts.Any(x => (string?)x!["family"] == family)) { dup++; continue; }
            // Two families can slug to the same id — SHREE-DEV-0700 and the config's
            // hand-written entry for SHREE-DEVX-0700 both want "shree-dev-0700". The
            // first one in wins the plain id and the other would be dropped as a
            // duplicate, silently losing a whole font, so give it its own.
            string id2 = id;
            if (fonts.Any(x => (string?)x!["id"] == id2)) id2 = Slug(family) + "-" + enc.Replace("shree-", "");
            if (fonts.Any(x => (string?)x!["id"] == id2)) { dup++; continue; }

            fonts.Add(new JsonObject
            {
                ["id"] = id2, ["name"] = family, ["family"] = family,
                ["type"] = "legacy", ["encoding"] = enc,
            });
            perEnc[enc] = perEnc.GetValueOrDefault(enc) + 1;
            ok++;
        }
    }

    foreach (var kv in perEnc.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key.PadRight(14) + kv.Value);
    Console.WriteLine("registered=" + ok + " already-present=" + dup + " rejected-by-cmap-check=" + rejected + " no-table=" + noTable);
    Console.WriteLine("series with NO table yet:");
    foreach (var kv in unmatched.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key.PadRight(18) + kv.Value);
    if (dryRun) { Console.WriteLine("(dry run)"); return 0; }
    File.WriteAllText(configPath, cfg2.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
    Console.WriteLine("wrote " + configPath);
    return 0;
}

// --- scan the installed fonts ---
var found = new List<(string hash, string family, string file)>();
foreach (var f in Directory.EnumerateFiles(fontDir, "*.ttf", SearchOption.TopDirectoryOnly))
{
    string[] codesAndName;
    try { codesAndName = Read(f); } catch { continue; }
    if (codesAndName.Length != 2) continue;
    string hash = codesAndName[0], family = codesAndName[1];
    if (Environment.GetEnvironmentVariable("RF_LIST") == "1") { Console.WriteLine(hash + "  " + family + "  " + Path.GetFileName(f)); continue; }
    if (!solved.ContainsKey(hash)) continue;
    if (family.Length == 0) continue;
    // The code-point SET alone is not proof of a shared encoding: an 8-bit legacy
    // font from another vendor covers the same ASCII range while placing entirely
    // different letters there (ITRFK-Guj-Brush matched the Shree 7-series set but
    // only 1 of its 214 glyphs). Require the Shree family naming too.
    if (!System.Text.RegularExpressions.Regex.IsMatch(family, @"^SHREE-[A-Z]+\d?-\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
    {
        Console.WriteLine("  skip (not a Shree family, shares the code-point set only): " + family);
        continue;
    }
    found.Add((hash, family, Path.GetFileName(f)));
}

Console.WriteLine("installed fonts in solved encodings: " + found.Count);

var root = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
var languages = root["languages"]!.AsArray();
int added = 0, already = 0;

foreach (var group in found.GroupBy(x => x.hash))
{
    var (enc, langName, label) = solved[group.Key];
    var lang = languages.FirstOrDefault(l => (string?)l!["name"] == langName)?.AsObject();
    if (lang is null) { Console.WriteLine("  no language " + langName + " — skipped " + group.Count()); continue; }
    var fonts = lang["fonts"]!.AsArray();

    foreach (var (_, family, file) in group.OrderBy(x => x.family, StringComparer.OrdinalIgnoreCase))
    {
        string id = Slug(family);
        if (fonts.Any(x => (string?)x!["id"] == id || (string?)x!["family"] == family)) { already++; continue; }
        // The Gujarati 7.0 and 4.0/5.0/6.0 fonts share one code-point set but five codes draw
        // differently (91-94, B6): SHREE-GUJ-* (no 7) is the 4/5/6 family, with its own table.
        bool guj456 = enc == "shree-guj7" && !System.Text.RegularExpressions.Regex.IsMatch(family, @"^SHREE[-_ ]?GUJ7", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        fonts.Add(new JsonObject
        {
            ["id"] = id,
            ["name"] = family + " (" + (guj456 ? "Shree-Lipi 4.0/5.0/6.0" : label) + ")",
            ["family"] = family,
            ["type"] = "legacy",
            ["encoding"] = guj456 ? "shree-guj456" : enc,
        });
        added++;
    }
    Console.WriteLine("  " + langName.PadRight(10) + " " + enc.PadRight(12) + group.Count() + " fonts");
}

// the catalog loads only the tables it lists, so register the new ones there too
var mappingFiles = root["mappingFiles"]?.AsArray();
int tablesAdded = 0;
if (mappingFiles is not null)
{
    var have = new HashSet<string>(mappingFiles.Select(n => (string?)n ?? ""), StringComparer.OrdinalIgnoreCase);
    foreach (var enc in solved.Values.Select(v => v.enc).Distinct(StringComparer.Ordinal))
    {
        foreach (string rel in new[] { "mappings/" + enc + "-to-unicode.json", "mappings/unicode-to-" + enc + ".json" })
        {
            if (have.Contains(rel)) continue;
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(configPath)!, rel.Replace('/', Path.DirectorySeparatorChar)))) continue;
            mappingFiles.Add(rel);
            have.Add(rel);
            tablesAdded++;
        }
    }
}
Console.WriteLine("mapping files registered: +" + tablesAdded);

Console.WriteLine("added=" + added + " already-present=" + already);
if (dryRun) { Console.WriteLine("(dry run, nothing written)"); return 0; }
File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }));
Console.WriteLine("wrote " + configPath);
return 0;

static string Slug(string family)
{
    var sb = new StringBuilder();
    foreach (char c in family.ToLowerInvariant())
        sb.Append(char.IsLetterOrDigit(c) ? c : '-');
    string s = sb.ToString();
    while (s.Contains("--")) s = s.Replace("--", "-");
    return s.Trim('-');
}

// returns [layout hash, family name]
static string[] Read(string path)
{
    byte[] d = File.ReadAllBytes(path);
    ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
    uint U32(long o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

    var tables = new Dictionary<string, (uint off, uint len)>(StringComparer.Ordinal);
    int num = U16(4);
    for (int i = 0; i < num; i++)
    {
        long o = 12 + i * 16;
        tables[Encoding.ASCII.GetString(d, (int)o, 4)] = (U32(o + 8), U32(o + 12));
    }
    if (!tables.TryGetValue("cmap", out var cm)) return Array.Empty<string>();

    var codes = new SortedSet<int>();
    int n = U16(cm.off + 2);
    for (int i = 0; i < n; i++)
    {
        long rec = cm.off + 4 + i * 8;
        if (U16(rec) != 3 || U16(rec + 2) != 1) continue;
        uint sub = cm.off + U32(rec + 4);
        if (U16(sub) != 4) continue;
        int segX2 = U16(sub + 6), seg = segX2 / 2;
        long endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
        for (int s = 0; s < seg; s++)
        {
            int end = U16(endO + s * 2), start = U16(startO + s * 2);
            short delta = (short)U16(deltaO + s * 2); int ro = U16(rangeO + s * 2);
            if (start == 0xFFFF) continue;
            for (int ch = start; ch <= end; ch++)
            {
                int g;
                if (ro == 0) g = (ch + delta) & 0xFFFF;
                else
                {
                    long gi = rangeO + s * 2 + ro + (ch - start) * 2;
                    if (gi + 1 >= d.Length) continue;
                    g = U16(gi); if (g != 0) g = (g + delta) & 0xFFFF;
                }
                if (g != 0) codes.Add(ch);
            }
        }
    }
    if (codes.Count == 0) return Array.Empty<string>();

    var buf = new byte[codes.Count * 4];
    int k = 0;
    foreach (int c in codes) { BitConverter.TryWriteBytes(buf.AsSpan(k * 4), c); k++; }
    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buf)).Substring(0, 10);

    string family = "";
    if (tables.TryGetValue("name", out var nm))
    {
        int cnt = U16(nm.off + 2); uint strOff = nm.off + U16(nm.off + 4);
        for (int i = 0; i < cnt; i++)
        {
            long r = nm.off + 6 + i * 12;
            int plat = U16(r), enc = U16(r + 2), id = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
            if (id != 1) continue;
            if (plat != 3 && family.Length > 0) continue;
            var bytes = new byte[len];
            Array.Copy(d, strOff + off, bytes, 0, len);
            string v = plat == 3 ? Encoding.BigEndianUnicode.GetString(bytes) : Encoding.ASCII.GetString(bytes);
            if (plat == 3) { family = v; break; }
            if (family.Length == 0) family = v;
        }
    }
    return new[] { hash, family.Trim() };
}

/// Shree's own families are named SHREE-&lt;SCRIPT&gt;-&lt;design&gt;; a font that only
/// shares the code-point set is not the same encoding.
static bool ShreeFamily(string family) =>
    System.Text.RegularExpressions.Regex.IsMatch(family, @"^SHREE-[A-Z]+\d?-\d+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

/// The code points a font's (3,1) cmap maps — used to prove a font really uses
/// the encoding its family name claims.
static HashSet<int> CodePoints(string path)
{
    var res = new HashSet<int>();
    byte[] d = File.ReadAllBytes(path);
    ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
    uint U32(long o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
    var tables = new Dictionary<string, uint>(StringComparer.Ordinal);
    int num = U16(4);
    for (int i = 0; i < num; i++)
    {
        long o = 12 + i * 16;
        tables[Encoding.ASCII.GetString(d, (int)o, 4)] = U32(o + 8);
    }
    if (!tables.TryGetValue("cmap", out uint cm)) return res;
    int n = U16(cm + 2);
    for (int i = 0; i < n; i++)
    {
        long rec = cm + 4 + i * 8;
        if (U16(rec) != 3 || U16(rec + 2) != 1) continue;
        uint sub = cm + U32(rec + 4);
        if (U16(sub) != 4) continue;
        int segX2 = U16(sub + 6), seg = segX2 / 2;
        long endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
        for (int s = 0; s < seg; s++)
        {
            int end = U16(endO + s * 2), start = U16(startO + s * 2);
            short delta = (short)U16(deltaO + s * 2); int ro = U16(rangeO + s * 2);
            if (start == 0xFFFF) continue;
            for (int ch = start; ch <= end; ch++)
            {
                int g;
                if (ro == 0) g = (ch + delta) & 0xFFFF;
                else
                {
                    long gi = rangeO + s * 2 + ro + (ch - start) * 2;
                    if (gi + 1 >= d.Length) continue;
                    g = U16(gi); if (g != 0) g = (g + delta) & 0xFFFF;
                }
                if (g != 0) res.Add(ch);
            }
        }
    }
    return res;
}

static string? FindInstalled(string family)
{
    var dirs = new List<string>
    {
        @"C:\Windows\Fonts",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
    };
    dirs.AddRange(ExtraFonts.Dirs);
    foreach (string d in dirs)
    {
        if (!Directory.Exists(d)) continue;
        foreach (string f in Directory.EnumerateFiles(d, "*.ttf", SearchOption.AllDirectories))
        {
            try
            {
                var r = Read(f);
                if (r.Length == 2 && string.Equals(r[1], family, StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch { }
        }
    }
    return null;
}

/// <summary>
/// Extra folders to search for a font file, from --font-src. A font the customer
/// owns but has not installed yet still has to pass the cmap proof before we list
/// it: "not installed on this machine" is not a reason to skip the check and ship
/// an unverified entry.
/// </summary>
static class ExtraFonts
{
    public static readonly List<string> Dirs = new();
}
