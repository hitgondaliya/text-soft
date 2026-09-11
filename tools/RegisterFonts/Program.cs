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

        // the label RegisterFonts writes into each font's name, per series
        string? LegacyIdForLabel(string layoutName)
        {
            string want = layoutName.Contains("Ex", StringComparison.OrdinalIgnoreCase) ? "(ShreeLipi-Ex)"
                        : layoutName.Contains("7.0", StringComparison.Ordinal) ? "(Shree-Lipi 7.0"
                        : layoutName.Contains("4.0", StringComparison.Ordinal) ? "4-5-6)"
                        : "";
            if (want.Length == 0) return null;
            foreach (var f in fonts)
            {
                if ((string?)f!["type"] != "legacy") continue;
                string name = (string?)f["name"] ?? "";
                if (!ShreeFamily((string?)f["family"] ?? "")) continue;
                if (name.Contains(want, StringComparison.Ordinal))
                    return (string?)f["id"];
            }
            return null;
        }

        foreach (var flNode in lang["fontLayouts"]!.AsArray())
        {
            var fl = flNode!.AsObject();
            string id = (string?)fl["fontId"] ?? "";
            string name = (string?)fl["name"] ?? "";
            var cur = fonts.FirstOrDefault(f => (string?)f!["id"] == id);
            bool resolves = id.Length > 0 && cur is not null;
            // A layout can resolve and still be wrong: Gujarati's "Shree-Lipi 7.0"
            // pointed at an X-series font. If the entry names a Shree series, the
            // font it picks has to carry that series' label.
            if (resolves && fl["type"]?.GetValue<string>() == "legacy")
            {
                string? shouldBe = LegacyIdForLabel(name);
                var want = fonts.FirstOrDefault(f => (string?)f!["id"] == shouldBe);
                // compare ENCODINGS, not ids — any font of the right encoding is a
                // correct choice, and the bundled one is the better default
                if (want is not null && (string?)want["encoding"] != (string?)cur!["encoding"]) resolves = false;
            }
            if (resolves) continue;

            string type = (string?)fl["type"] ?? "legacy";
            string? repl = type == "unicode" ? FirstUnicodeId() : LegacyIdForLabel(name);
            if (repl is null) { stillDead++; continue; }

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
        fonts.Add(new JsonObject
        {
            ["id"] = id,
            ["name"] = family + " (" + label + ")",
            ["family"] = family,
            ["type"] = "legacy",
            ["encoding"] = enc,
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
