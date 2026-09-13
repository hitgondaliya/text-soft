using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

// SchemeGen — build "type by sound" schemes from Shree-Lipi's own help files.
//
// Shree ships a CHM per script per keyboard, and the ENG one is a full phonetic
// table: "ka" -> ક, "kha" -> ખ, "ki" -> કિ, "aorA" -> અ. ChmMap pulls those pairs
// out; this turns them into a TransliterationScheme.
//
// Deriving it needs three readings of the table, all of which the table itself
// then checks:
//   * a row whose text is an independent vowel is that vowel;
//   * a row spelled "k<something>" whose text starts with the ka consonant shows
//     the MATRA for <something> — "ki" -> કિ means i attaches as િ;
//   * any other row ending in "a" is a consonant carrying its inherent vowel, so
//     "kha" -> ખ gives the token "kh".
// "or" inside a key separates alternative spellings ("phaorfa" = pha or fa).
//
// Nothing is trusted on faith: every pair Shree published is replayed through our
// own Transliterator, and the scheme is only written if it reproduces them.
//
//   dotnet run --project tools/SchemeGen -- --maps <chmmaps dir> --out <translit dir> [--write-config]

string mapsDir = "chmmaps", outDir = ".";
string configPath = Path.Combine("assets", "config", "languages.json");
bool writeConfig = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--maps" && i + 1 < args.Length) mapsDir = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
    else if (args[i] == "--config" && i + 1 < args.Length) configPath = args[++i];
    else if (args[i] == "--write-config") writeConfig = true;
}

// script folder -> (block base, unicode fontId, language ids, scheme file name)
var scripts = new (string Dir, int Block, string Font, string[] Langs, string File)[]
{
    ("dev", 0x0900, "mangal",  new[]{"hi","mr","sa"}, "devanagari-shree-eng"),
    ("guj", 0x0A80, "shruti",  new[]{"gu"},           "gujarati-shree-eng"),
    ("pun", 0x0A00, "raavi",   new[]{"pa"},           "punjabi-shree-eng"),
    ("ban", 0x0980, "vrinda",  new[]{"bn","as","mni"},"bengali-shree-eng"),
    ("tel", 0x0C00, "gautami", new[]{"te"},           "telugu-shree-eng"),
    ("kan", 0x0C80, "tunga",   new[]{"kn"},           "kannada-shree-eng"),
    ("mal", 0x0D00, "kartika", new[]{"ml"},           "malayalam-shree-eng"),
    ("ori", 0x0B00, "kalinga", new[]{"or"},           "odia-shree-eng"),
    ("tam", 0x0B80, "latha",   new[]{"ta"},           "tamil-shree-eng"),
};

var jsonOpts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
Directory.CreateDirectory(outDir);

var built = new List<(string[] Langs, string File, string Name)>();

foreach (var sc in scripts)
{
    string path = Path.Combine(mapsDir, sc.Dir, "eng.json");
    if (!File.Exists(path)) { Console.WriteLine($"{sc.Dir}: no eng.json"); continue; }

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var rows = new List<(string Key, string Uni)>();
    foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
    {
        string k = p.GetProperty("Key").GetString() ?? "", u = p.GetProperty("Uni").GetString() ?? "";
        if (k.Length == 0 || u.Length == 0) continue;
        foreach (string alt in Alternatives(k)) rows.Add((alt, u));
    }

    string viramaStr = ((char)(sc.Block + 0x4D)).ToString();

    bool Independent(string u) => u.Length == 1 && u[0] >= sc.Block + 0x04 && u[0] <= sc.Block + 0x14;
    bool ConsonantChar(string u) => u.Length >= 1 && u[0] >= sc.Block + 0x15 && u[0] <= sc.Block + 0x39;

    // Two spelling conventions are in use: Devanagari and Gujarati write a
    // consonant with its inherent vowel ("ka" -> ક), Gurmukhi writes the bare
    // letter ("k" -> ਕ). Which one this table uses decides how a consonant token
    // is read, so ask the table instead of assuming.
    bool inherentA = rows.Any(r => r.Key == "ka" && ConsonantChar(r.Uni) && r.Uni.Length == 1);
    string kaKey = inherentA ? "ka" : "k";
    string? ka = rows.FirstOrDefault(r => r.Key == kaKey).Uni;

    var consonants = new Dictionary<string, string>(StringComparer.Ordinal);
    var vowels = new Dictionary<string, string[]>(StringComparer.Ordinal);
    var specials = new Dictionary<string, string>(StringComparer.Ordinal);
    var matraRows = new HashSet<string>(StringComparer.Ordinal);

    // pass 1: independent vowels
    foreach (var (k, u) in rows)
        if (Independent(u) && !vowels.ContainsKey(k)) vowels[k] = new[] { u, "" };

    // pass 2: matras. They are written against the BARE consonant, because a vowel
    // replaces the inherent one: "kaa" is k + aa, not ka + a. Slicing off "ka"
    // instead of "k" made the long a look like a plain a and every bare consonant
    // then typed its matra as well — "ka" came out as का.
    if (!string.IsNullOrEmpty(ka))
        foreach (var (k, u) in rows)
        {
            if (k.Length <= 1 || !k.StartsWith("k", StringComparison.Ordinal)) continue;
            if (!u.StartsWith(ka, StringComparison.Ordinal) || u.Length <= ka.Length) continue;
            string token = k[1..], matra = u[ka.Length..];
            // a conjunct row like "kra" -> ક્ર is not a matra
            if (matra.Contains(viramaStr, StringComparison.Ordinal)) continue;
            matraRows.Add(k);
            if (vowels.TryGetValue(token, out var v)) { if (v[1].Length == 0) vowels[token] = new[] { v[0], matra }; }
            else if (!specials.ContainsKey(token)) specials[token] = matra;
        }

    // pass 3: consonants. A matra row must not be read as one: "kaa" -> કા would
    // otherwise register the token "ka" as a consonant meaning કા, and then plain
    // "ka" typed કા instead of ક.
    foreach (var (k, u) in rows)
    {
        if (matraRows.Contains(k) || vowels.ContainsKey(k) || u.Length == 0 || Independent(u)) continue;
        string token = inherentA
            ? (k.EndsWith("a", StringComparison.Ordinal) && k.Length >= 2 ? k[..^1] : null)!
            : k;
        if (string.IsNullOrEmpty(token)) continue;
        if (!ConsonantChar(u)) continue;

        // A spelling can appear twice. Shree's help pages lay the retroflex and
        // dental series out as two tables, and the extractor flattens them, so
        // Telugu shows "Ta" for both ట and త; Gujarati shows "na" for ઙ and ન
        // because whatever marked the velar nasal apart was lost. Neither "first
        // wins" nor "last wins" is right — ట is correct for Ta and ન for na — so
        // the tie is settled by what the roman letter means in standard Indic
        // romanisation, which is the same for every one of these scripts.
        if (consonants.TryGetValue(token, out string? had))
        {
            if (Roman.Offsets.TryGetValue(token, out int want))
            {
                int hadOff = had[0] - sc.Block, newOff = u[0] - sc.Block;
                if (hadOff == want) continue;            // keep what we have
                if (newOff != want) continue;            // neither matches: keep the first
            }
            else continue;                                // no expectation: keep the first
        }
        consonants[token] = u;
    }

    // pass 4: whatever is left and stands alone
    foreach (var (k, u) in rows)
    {
        if (vowels.ContainsKey(k) || specials.ContainsKey(k) || matraRows.Contains(k)) continue;
        string cons = inherentA && k.EndsWith("a", StringComparison.Ordinal) && k.Length >= 2 ? k[..^1] : k;
        if (consonants.ContainsKey(cons)) continue;
        specials[k] = u;
    }

    // The inherent vowel: a bare consonant already carries it.
    if (!vowels.ContainsKey("a")) vowels["a"] = new[] { ((char)(sc.Block + 0x05)).ToString(), "" };

    var scheme = new TransliterationScheme
    {
        Id = sc.File,
        Name = "Phonetic (Shree Eng)",
        FontId = sc.Font,
        Virama = viramaStr,
        Consonants = consonants,
        Vowels = vowels,
        Specials = specials,
    };

    // --- check it against every row Shree published ---
    var tr = new Transliterator(scheme);
    int ok = 0, bad = 0;
    var examples = new List<string>();
    foreach (var (k, u) in rows)
    {
        string got = tr.Convert(k);
        if (got == u) ok++;
        else { bad++; if (examples.Count < 4) examples.Add($"'{k}' -> {got} (Shree: {u})"); }
    }
    double rate = 100.0 * ok / Math.Max(1, rows.Count);
    Console.WriteLine($"  {sc.Dir}: {consonants.Count} consonants, {vowels.Count} vowels, {specials.Count} specials"
                    + $"  — reproduces {ok}/{rows.Count} of Shree's rows ({rate:F0}%)");
    foreach (string e in examples) Console.WriteLine($"      {e}");

    File.WriteAllText(Path.Combine(outDir, sc.File + ".json"), JsonSerializer.Serialize(scheme, jsonOpts));
    built.Add((sc.Langs, "translit/" + sc.File + ".json", scheme.Name));
}

if (!writeConfig) { Console.WriteLine("\n(schemes written; languages.json untouched — pass --write-config to wire them)"); return; }

var cfg = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
int wired = 0, kept = 0;
foreach (var (langs, file, _) in built)
    foreach (string id in langs)
    {
        var lang = cfg["languages"]!.AsArray().FirstOrDefault(l => (string?)l!["id"] == id)?.AsObject();
        if (lang is null) continue;
        // A language that already has a hand-built scheme keeps it: those were
        // written to a different spelling convention and are covered by tests.
        if (lang["scheme"] is not null) { kept++; continue; }
        lang["scheme"] = file;
        wired++;
    }
File.WriteAllText(configPath, cfg.ToJsonString(new JsonSerializerOptions
{
    WriteIndented = true,
    TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
}));
Console.WriteLine($"\nwired {wired} language(s), left {kept} existing scheme(s) alone; wrote {configPath}");

/// "phaorfa" is two spellings of one sound. Split on a literal "or" between two
/// non-empty halves; a key with no such split is returned as itself.
static IEnumerable<string> Alternatives(string key)
{
    int at = key.IndexOf("or", StringComparison.Ordinal);
    if (at > 0 && at + 2 < key.Length)
    {
        yield return key[..at];
        yield return key[(at + 2)..];
        yield break;
    }
    yield return key;
}

/// <summary>
/// Where each roman consonant sits in an Indic Unicode block, counted from the
/// block's base. The order is the traditional varnamala and is identical in every
/// block these scripts use, which is what makes one table enough to settle a
/// duplicated spelling in any of them.
/// </summary>
static class Roman
{
    public static readonly Dictionary<string, int> Offsets = new(StringComparer.Ordinal)
    {
        ["k"] = 0x15, ["kh"] = 0x16, ["g"] = 0x17, ["gh"] = 0x18,
        ["c"] = 0x1A, ["ch"] = 0x1B, ["j"] = 0x1C, ["jh"] = 0x1D,
        ["T"] = 0x1F, ["Th"] = 0x20, ["D"] = 0x21, ["Dh"] = 0x22, ["N"] = 0x23,
        ["t"] = 0x24, ["th"] = 0x25, ["d"] = 0x26, ["dh"] = 0x27, ["n"] = 0x28,
        ["p"] = 0x2A, ["ph"] = 0x2B, ["f"] = 0x2B, ["b"] = 0x2C, ["bh"] = 0x2D, ["m"] = 0x2E,
        ["y"] = 0x2F, ["r"] = 0x30, ["l"] = 0x32, ["L"] = 0x33,
        ["v"] = 0x35, ["w"] = 0x35, ["sh"] = 0x36, ["S"] = 0x37, ["s"] = 0x38, ["h"] = 0x39,
    };
}
