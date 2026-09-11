using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

// MapGen — build legacy-font -> Unicode converter tables (FR-4) by JOINING two
// authoritative Shree-Lipi sources, with no guessing:
//   * a 406-byte scheme file  (INSCRIPT.DEV): keyboard char -> legacy glyph byte
//   * the matching CHM keymap  (from ChmMap): keyboard char -> Unicode
// Joining on the keyboard char yields  legacy byte -> Unicode. Matras/halant are
// decomposed from syllable entries; i-matra reorder is added as a post-rule.
//
// Single:  dotnet run --project tools/MapGen -- --shree <dir> --chm <chmmaps> \
//            --scheme INSCRIPT --ext DEV --script dev --source shree-devx --target mangal --out <file> [--test "<legacy>"]
// Batch:   dotnet run --project tools/MapGen -- --batch --shree <dir> --chm <chmmaps> --out-dir assets/config/mappings

var opt = ParseArgs(args);
string shree = opt.GetValueOrDefault("shree", @"C:\Shree-Lipi nxt Plus") ?? @"C:\Shree-Lipi nxt Plus";
string chmRoot = opt.GetValueOrDefault("chm", "") ?? "";
if (string.IsNullOrEmpty(chmRoot)) { Console.Error.WriteLine("--chm <chmmaps dir> required"); return 1; }

var writeOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};

if (opt.ContainsKey("batch"))
{
    string outDir = opt.GetValueOrDefault("out-dir", "assets/config/mappings") ?? "assets/config/mappings";
    // script (chm folder) | scheme ext | source encoding | target (unicode font id) | reorder token
    var jobs = new (string Script, string Ext, string Source, string Target, string Reorder)[]
    {
        ("dev", "DEV", "shree-devx", "mangal",  "dev"),
        ("guj", "GUJ", "shree-gujx", "shruti",  "guj"),
        ("ban", "BAN", "shree-banx", "vrinda",  "ban"),
        ("tel", "TEL", "shree-telx", "gautami", ""),
        ("pun", "PUN", "shree-punx", "raavi",   ""),
        ("kan", "KAN", "shree-kanx", "tunga",   ""),
        ("mal", "MAL", "shree-malx", "kartika", ""),
        ("ori", "ORI", "shree-orix", "kalinga", ""),
        ("tam", "TAM", "shree-tamx", "latha",   ""),
    };
    int made = 0;
    var generated = new List<string>();
    foreach (var j in jobs)
    {
        string fwd = Path.Combine(outDir, $"{j.Source}-to-unicode.json");
        string rev = Path.Combine(outDir, $"unicode-to-{j.Source}.json");
        var r = Generate(shree, chmRoot, "INSCRIPT", j.Ext, j.Script, j.Source, j.Target, j.Reorder, fwd, null, writeOpts);
        if (r is null) { Console.WriteLine($"  skip {j.Script}: missing scheme/keymap"); continue; }
        var rr = Generate(shree, chmRoot, "INSCRIPT", j.Ext, j.Script, j.Source, j.Target, j.Reorder, rev, null, writeOpts, reverse: true);
        Console.WriteLine($"  {j.Script,-4} fwd={r.Value.Pairs,-3} rev={rr!.Value.Pairs,-3} conflicts={r.Value.Conflicts}");
        generated.Add($"mappings/{j.Source}-to-unicode.json");
        generated.Add($"mappings/unicode-to-{j.Source}.json");
        made++;
    }
    Console.WriteLine($"\nGenerated {made} tables into {outDir}");
    Console.WriteLine("mappingFiles entries:");
    foreach (var g in generated) Console.WriteLine($"    \"{g}\",");
    return 0;
}

// single mode
string res = "";
var single = Generate(shree, chmRoot,
    opt.GetValueOrDefault("scheme", "INSCRIPT") ?? "INSCRIPT",
    opt.GetValueOrDefault("ext", "DEV") ?? "DEV",
    opt.GetValueOrDefault("script", "dev") ?? "dev",
    opt.GetValueOrDefault("source", "shree-legacy") ?? "shree-legacy",
    opt.GetValueOrDefault("target", "mangal") ?? "mangal",
    opt.GetValueOrDefault("reorder", opt.GetValueOrDefault("script", "dev")) ?? "dev",
    opt.GetValueOrDefault("out", "out.json") ?? "out.json",
    opt.GetValueOrDefault("test", null),
    writeOpts) is { } g2 ? (res = $"keys joined={g2.Keys} byte->unicode pairs={g2.Pairs} conflicts={g2.Conflicts}") : null;
Console.WriteLine(res.Length > 0 ? res : "FAILED (missing scheme or keymap)");
return res.Length > 0 ? 0 : 1;

// ---- generation ----

static (int Keys, int Pairs, int Conflicts)? Generate(
    string shree, string chmRoot, string scheme, string ext, string script,
    string sourceEnc, string targetEnc, string reorder, string outFile, string? test,
    JsonSerializerOptions writeOpts, bool reverse = false)
{
    string schemeFile = Path.Combine(shree, scheme + "." + ext);
    string chmJson = Path.Combine(chmRoot, script, Slug(scheme) + ".json");
    if (!File.Exists(schemeFile) || !File.Exists(chmJson)) return null;

    byte[] schemeBytes = File.ReadAllBytes(schemeFile);
    var readOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var keymap = JsonSerializer.Deserialize<ChmLayout>(File.ReadAllText(chmJson), readOpts)!;

    // key(char) -> unicode. Base letters are single-key entries. Matras/halant/nukta
    // only appear inside syllables in the help (e.g. "ke"->"का", "kdk"->"क्क"), so we
    // DECOMPOSE: if leading keys already decode to a prefix of the Unicode, the
    // trailing key must produce the remainder. Iterated to a fixpoint.
    // A physical key's BASE output is its consonant; the CHM sometimes lists the
    // same key letter twice (e.g. Gujarati 'v' -> ૠ vocalic AND 'v' -> ન na). Prefer
    // the consonant: keep the first mapping, but upgrade to a consonant if one
    // appears. Consonants are ISCII-aligned at block-offset 0x15..0x39 in every
    // Indic block, so one test covers all scripts.
    static bool IsConsonant(string u) =>
        u.Length == 1 && u[0] >= 'ऀ' && u[0] <= '෿' && (u[0] & 0x7F) is >= 0x15 and <= 0x39;

    var key2uni = new Dictionary<char, string>();
    foreach (var p in keymap.Pairs)
    {
        if (p.Key.Length != 1) continue;
        char k = p.Key[0];
        if (!key2uni.TryGetValue(k, out var existing))
            key2uni[k] = p.Uni;
        else if (IsConsonant(p.Uni) && !IsConsonant(existing))
            key2uni[k] = p.Uni;   // upgrade vowel/other -> consonant for this base key
    }

    string? Decode(string keys)
    {
        var sb = new StringBuilder();
        foreach (char k in keys) { if (!key2uni.TryGetValue(k, out var u)) return null; sb.Append(u); }
        return sb.ToString();
    }

    // Generalised: if an entry has exactly ONE unknown key, and the known keys
    // before/after it decode to a prefix/suffix of the Unicode, the unknown key
    // produces the middle. This recovers matras (last position), halant/virama
    // (e.g. "kdk"->"क्क": k=क, k=क, so middle d=्) and nukta. Iterated to fixpoint.
    bool learned = true;
    while (learned)
    {
        learned = false;
        foreach (var p in keymap.Pairs)
        {
            if (p.Key.Length < 2) continue;
            // find the single unknown key
            int unknownIdx = -1; bool multiple = false;
            for (int i = 0; i < p.Key.Length; i++)
                if (!key2uni.ContainsKey(p.Key[i]))
                {
                    if (unknownIdx >= 0) { multiple = true; break; }
                    unknownIdx = i;
                }
            if (multiple || unknownIdx < 0) continue;

            string? prefix = Decode(p.Key[..unknownIdx]);
            string? suffix = Decode(p.Key[(unknownIdx + 1)..]);
            if (prefix is null || suffix is null) continue;
            if (!p.Uni.StartsWith(prefix, StringComparison.Ordinal) ||
                !p.Uni.EndsWith(suffix, StringComparison.Ordinal)) continue;
            int midLen = p.Uni.Length - prefix.Length - suffix.Length;
            if (midLen <= 0) continue;
            key2uni[p.Key[unknownIdx]] = p.Uni.Substring(prefix.Length, midLen);
            learned = true;
        }
    }

    // Matra/sign override: a key that adds a COMBINING mark after a consonant
    // (e.g. Kannada "ke" -> "ಕಾ", so 'e' adds ಾ) is really a matra key, even if the
    // CHM also lists it standalone as a rare consonant. Prefer the combining mark.
    static bool IsCombining(string u)
    {
        if (u.Length != 1) return false;
        int o = u[0] & 0x7F;
        return u[0] >= 'ऀ' && u[0] <= '෿' &&
               (o is 0x01 or 0x02 or 0x03 or 0x3C or 0x4D or 0x62 or 0x63 || (o >= 0x3E && o <= 0x4C) || (o >= 0x51 && o <= 0x57));
    }
    bool over = true;
    while (over)
    {
        over = false;
        foreach (var p in keymap.Pairs)
        {
            if (p.Key.Length < 2) continue;
            for (int i = 0; i < p.Key.Length; i++)
            {
                string? prefix = Decode(p.Key[..i]);
                string? suffix = Decode(p.Key[(i + 1)..]);
                if (prefix is null || suffix is null) continue;
                if (!p.Uni.StartsWith(prefix, StringComparison.Ordinal) ||
                    !p.Uni.EndsWith(suffix, StringComparison.Ordinal)) continue;
                int midLen = p.Uni.Length - prefix.Length - suffix.Length;
                if (midLen <= 0) continue;
                string mid = p.Uni.Substring(prefix.Length, midLen);
                char mk = p.Key[i];
                // Only override to a COMBINING mark (matra/virama/nukta/sign): a key
                // used between/after consonants to add such a mark is really that mark,
                // even if the CHM also lists it standalone as a rare consonant/vowel.
                if (IsCombining(mid) && (!key2uni.TryGetValue(mk, out var cur) || (!IsCombining(cur) && cur != mid)))
                {
                    key2uni[mk] = mid;
                    over = true;
                }
            }
        }
    }

    // legacy byte -> unicode
    var byte2uni = new SortedDictionary<int, string>();
    int conflicts = 0;
    foreach (var (key, uni) in key2uni)
    {
        int idx = key;
        if (idx >= schemeBytes.Length) continue;
        byte gb = schemeBytes[idx];
        if (gb == 0xFF) continue;
        if (byte2uni.TryGetValue(gb, out var existing)) { if (existing != uni) conflicts++; }
        else byte2uni[gb] = uni;
    }

    MappingTable table;
    if (!reverse)
    {
        // legacy byte -> Unicode
        table = new MappingTable
        {
            Id = Path.GetFileNameWithoutExtension(outFile),
            Name = $"{sourceEnc} -> Unicode ({scheme})",
            SourceFontId = sourceEnc,
            TargetFontId = targetEnc,
            SourceType = FontType.Legacy,
            TargetType = FontType.Unicode,
            Pairs = byte2uni.Select(kv => new MappingPair { From = ((char)kv.Key).ToString(), To = kv.Value })
                            .OrderByDescending(p => p.From.Length).ToList(),
            PostRules = ReorderRules(reorder)
        };
    }
    else
    {
        // Unicode -> legacy byte. Invert (first byte wins per Unicode string), map
        // longest Unicode first so conjuncts (क्ष) beat their pieces. i-matra is put
        // BACK before its cluster as a PRE-rule, so substitution emits visual order.
        var uni2byte = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in byte2uni)
            if (!uni2byte.ContainsKey(kv.Value)) uni2byte[kv.Value] = kv.Key;

        table = new MappingTable
        {
            Id = Path.GetFileNameWithoutExtension(outFile),
            Name = $"Unicode -> {sourceEnc} ({scheme})",
            SourceFontId = targetEnc,
            TargetFontId = sourceEnc,
            SourceType = FontType.Unicode,
            TargetType = FontType.Legacy,
            PreRules = ReverseReorderRules(reorder),
            Pairs = uni2byte.Select(kv => new MappingPair { From = kv.Key, To = ((char)kv.Value).ToString() })
                            .OrderByDescending(p => p.From.Length).ToList()
        };
    }

    string outPath = Path.GetFullPath(outFile);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, JsonSerializer.Serialize(table, writeOpts));

    if (test is not null)
    {
        var r = new FontConverter(table).Convert(test);
        Console.WriteLine($"TEST in : {test}\nTEST out: {r.Text}");
    }
    return (key2uni.Count, table.Pairs.Count, conflicts);
}

// i-matra is stored before its consonant cluster in visual-order legacy text; move
// it after the cluster for Unicode. Ranges differ per script.
static List<RewriteRule> ReorderRules(string token) => token switch
{
    "dev" or "san" => IMatra('\u093F', '\u0915', '\u0939', '\u093C', '\u094D'),
    "guj"          => IMatra('\u0ABF', '\u0A95', '\u0AB9', '\u0ABC', '\u0ACD'),
    "ban"          => IMatra('\u09BF', '\u0995', '\u09B9', '\u09BC', '\u09CD'),
    _              => new()
};

static List<RewriteRule> IMatra(char iMatra, char consLo, char consHi, char nukta, char virama) => new()
{
    new RewriteRule
    {
        Pattern = $"{iMatra}((?:[{consLo}-{consHi}]{nukta}?{virama})*[{consLo}-{consHi}]{nukta}?)",
        Replace = "$1" + iMatra,
        Note = "Move i-matra after the consonant cluster (legacy pre-base -> Unicode post-base)."
    }
};

// Reverse (Unicode -> legacy): put the i-matra BACK before its cluster (visual order).
static List<RewriteRule> ReverseReorderRules(string token) => token switch
{
    "dev" or "san" => IMatraReverse('ि', 'क', 'ह', '़', '्'),
    "guj"          => IMatraReverse('િ', 'ક', 'હ', '઼', '્'),
    "ban"          => IMatraReverse('ি', 'ক', 'হ', '়', '্'),
    _              => new()
};

static List<RewriteRule> IMatraReverse(char iMatra, char consLo, char consHi, char nukta, char virama) => new()
{
    new RewriteRule
    {
        Pattern = $"((?:[{consLo}-{consHi}]{nukta}?{virama})*[{consLo}-{consHi}]{nukta}?){iMatra}",
        Replace = iMatra + "$1",
        Note = "Move i-matra before the consonant cluster (Unicode post-base -> legacy pre-base)."
    }
};

static string Slug(string s) =>
    new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

static Dictionary<string, string?> ParseArgs(string[] a)
{
    var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < a.Length; i++)
        if (a[i].StartsWith("--")) d[a[i][2..]] = (i + 1 < a.Length && !a[i + 1].StartsWith("--")) ? a[++i] : "true";
    return d;
}

sealed class ChmLayout
{
    public string Script { get; set; } = "";
    public string Layout { get; set; } = "";
    public List<ChmPair> Pairs { get; set; } = new();
}
sealed class ChmPair
{
    public string Key { get; set; } = "";
    public string Uni { get; set; } = "";
}
