using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

// ChmMap — extract the KEYSTROKE -> UNICODE tables baked into Shree-Lipi's
// per-keyboard CHM help files. Each help page is a Word-exported HTML table of
// (character, keystroke) pairs where the character is written as Unicode numeric
// entities in the Mangal/Unicode font (e.g. &#2309; = अ). That is authoritative,
// machine-readable ground truth — no guessing, no cracking the encrypted maps.
//
// Output: one JSON per layout {script, layout, pairs:[{key, uni}]}, plus a
// combined index. Feeds (a) verification of the keyboard layouts and (b) the
// converter Rosetta (join key->uni here with key->glyphByte from the .DEV scheme).
//
//   dotnet run --project tools/ChmMap -- --shree "C:\Shree-Lipi nxt Plus" --out <dir> [--7z "C:\Program Files\7-Zip\7z.exe"]

string shree = @"C:\Shree-Lipi nxt Plus";
string outDir = Path.Combine(AppContext.BaseDirectory, "chmmaps");
string sevenZip = @"C:\Program Files\7-Zip\7z.exe";
string? layoutsDir = null;   // if set, also emit InScript keyboard-layout JSON for new scripts
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--shree": shree = args[++i]; break;
        case "--out": outDir = args[++i]; break;
        case "--7z": sevenZip = args[++i]; break;
        case "--layouts": layoutsDir = args[++i]; break;
    }
}

// script code -> (layout file basename, unicode fontId) for scripts whose InScript
// keyboard layout we still need to generate (dev/guj/… are already hand-curated).
var layoutTargets = new Dictionary<string, (string File, string Font)>(StringComparer.OrdinalIgnoreCase)
{
    ["kan"] = ("kannada-inscript", "tunga"),
    ["mal"] = ("malayalam-inscript", "kartika"),
    ["ori"] = ("odia-inscript", "kalinga"),
    ["tam"] = ("tamil-inscript", "latha"),
};

string dataDir = Path.Combine(shree, "data");
if (!Directory.Exists(dataDir)) { Console.Error.WriteLine($"no data dir: {dataDir}"); return 1; }
if (!File.Exists(sevenZip)) { Console.Error.WriteLine($"7-Zip not found: {sevenZip} (pass --7z)"); return 1; }
Directory.CreateDirectory(outDir);

string tmp = Path.Combine(Path.GetTempPath(), "chmmap_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

int totalLayouts = 0, totalPairs = 0, failed = 0;
var perScript = new SortedDictionary<string, int>();

// script folders: data/dev, data/guj, ...
foreach (var scriptDir in Directory.GetDirectories(dataDir).OrderBy(d => d))
{
    string script = Path.GetFileName(scriptDir);
    var chms = Directory.GetFiles(scriptDir, "*.chm");
    if (chms.Length == 0) continue;

    string scriptOut = Path.Combine(outDir, script);
    Directory.CreateDirectory(scriptOut);

    foreach (var chm in chms.OrderBy(c => c))
    {
        string layout = LayoutName(Path.GetFileNameWithoutExtension(chm), script);
        string extractDir = Path.Combine(tmp, script + "_" + layout);
        try
        {
            Extract(sevenZip, chm, extractDir);
            string? html = FindIndexHtml(extractDir);
            if (html is null) { Console.Error.WriteLine($"  no index.htm: {Path.GetFileName(chm)}"); failed++; continue; }

            var pairs = ParsePairs(File.ReadAllText(html));
            if (pairs.Count == 0) { Console.Error.WriteLine($"  0 pairs: {Path.GetFileName(chm)}"); failed++; continue; }

            var map = new LayoutMap
            {
                Source = Path.GetFileName(chm),
                Script = script,
                Layout = layout,
                Pairs = pairs
            };
            File.WriteAllText(Path.Combine(scriptOut, Slug(layout) + ".json"),
                              JsonSerializer.Serialize(map, jsonOpts));
            totalLayouts++; totalPairs += pairs.Count;
            perScript[script] = perScript.GetValueOrDefault(script) + 1;

            if (layoutsDir is not null
                && layout.Equals("INSCRIPT", StringComparison.OrdinalIgnoreCase)
                && layoutTargets.TryGetValue(script, out var lt))
            {
                Directory.CreateDirectory(layoutsDir);
                string layoutJson = BuildLayout(lt.File, lt.Font, pairs);
                File.WriteAllText(Path.Combine(layoutsDir, lt.File + ".json"), layoutJson);
                Console.WriteLine($"  layout: {lt.File}.json");
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"  FAIL {Path.GetFileName(chm)}: {ex.Message}"); failed++; }
    }
}

try { Directory.Delete(tmp, true); } catch { }

Console.WriteLine($"\nLayouts extracted: {totalLayouts}   pairs: {totalPairs}   failed: {failed}");
foreach (var kv in perScript) Console.WriteLine($"  {kv.Key}: {kv.Value} layouts");
Console.WriteLine($"Output: {outDir}");
return 0;

// ---- helpers ----

static string LayoutName(string file, string script)
{
    // filenames like "INSCRIPTDEV", "AksharGUJ", "RAMING2DEV" -> strip trailing script code
    foreach (var suffix in new[] { script.ToUpperInvariant(), script.ToLowerInvariant() })
        if (file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && file.Length > suffix.Length)
            return file[..^suffix.Length];
    return file;
}

static string Slug(string s) =>
    new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

static void Extract(string sevenZip, string chm, string dest)
{
    Directory.CreateDirectory(dest);
    var psi = new ProcessStartInfo(sevenZip)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi.ArgumentList.Add("x");
    psi.ArgumentList.Add(chm);
    psi.ArgumentList.Add("-o" + dest);
    psi.ArgumentList.Add("-y");
    using var p = Process.Start(psi)!;
    p.StandardOutput.ReadToEnd();
    p.StandardError.ReadToEnd();
    p.WaitForExit();
}

static string? FindIndexHtml(string dir)
{
    var candidates = Directory.GetFiles(dir, "*.htm*", SearchOption.AllDirectories);
    // prefer index.htm, else the largest html (the content page)
    return candidates.FirstOrDefault(f => Path.GetFileName(f).Equals("index.htm", StringComparison.OrdinalIgnoreCase))
        ?? candidates.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
}

static List<Pair> ParsePairs(string html)
{
    // Column labels that are never a keystroke.
    var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "keystroke", "vowel", "vowels", "consonant", "consonants", "matra", "matras",
        "numeral", "numerals", "special", "specials", "conjunct", "conjuncts",
        "character", "characters", "symbol", "symbols", "sign", "signs"
    };
    html = html.Replace('\r', ' ').Replace('\n', ' ');
    // split into table cells
    var cells = Regex.Split(html, "</td>", RegexOptions.IgnoreCase);
    var pairs = new List<Pair>();
    string? pendingUni = null;

    foreach (var raw in cells)
    {
        string stripped = Regex.Replace(raw, "<[^>]*>", " ");        // drop tags
        // unicode numeric entities -> the actual characters
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(stripped, "&#x?([0-9A-Fa-f]+);"))
        {
            string digits = m.Groups[1].Value;
            bool hex = m.Value.StartsWith("&#x", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int cp) && cp > 0 && cp <= 0x10FFFF)
                sb.Append(char.ConvertFromUtf32(cp));
        }
        string uni = sb.ToString();

        // residual text with entities removed -> potential keystroke
        string text = Regex.Replace(stripped, "&#x?[0-9A-Fa-f]+;", " ");
        text = DecodeBasicEntities(text);
        bool isLabel = labels.Contains(Regex.Replace(text, "\\s+", " ").Trim());
        // Keystrokes never contain spaces; the CHM sometimes formats them with one
        // (e.g. Bengali i-matra key "k f"). Strip all whitespace so they decompose.
        text = Regex.Replace(text, "\\s+", "");

        bool isChar = uni.Any(ch => ch >= '\u0900' && ch <= '\u0DFF');   // any Indic scalar
        if (isChar)
        {
            pendingUni = uni;
            continue;
        }

        // keystroke cell?
        if (pendingUni is not null && text.Length is > 0 and <= 10 && !isLabel && text.All(c => c <= '\u007E'))
        {
            pairs.Add(new Pair { Key = text, Uni = pendingUni });
            pendingUni = null;
        }
    }
    return pairs;
}

// Build an InScript keyboard-layout JSON from single-key CHM pairs. Maps each
// keystroke char to a Windows virtual-key + base/shift layer.
static string BuildLayout(string id, string fontId, List<Pair> pairs)
{
    var baseByVk = new SortedDictionary<int, string>();
    var shiftByVk = new SortedDictionary<int, string>();
    foreach (var p in pairs)
    {
        if (p.Key.Length != 1) continue;              // single physical key only
        var vk = VkFor(p.Key[0]);
        if (vk is null) continue;
        var (code, shift) = vk.Value;
        var target = shift ? shiftByVk : baseByVk;
        if (!target.ContainsKey(code)) target[code] = p.Uni;
    }

    var vks = baseByVk.Keys.Union(shiftByVk.Keys).OrderBy(v => v).ToList();
    var rows = new List<string>();
    foreach (int vk in vks)
    {
        var parts = new List<string> { $"\"vk\": {vk}" };
        if (baseByVk.TryGetValue(vk, out var b)) parts.Add($"\"base\": \"{Esc(b)}\"");
        if (shiftByVk.TryGetValue(vk, out var s)) parts.Add($"\"shift\": \"{Esc(s)}\"");
        rows.Add("    { " + string.Join(", ", parts) + " }");
    }

    var sb = new StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine($"  \"$comment\": \"InScript layout generated from Shree-Lipi CHM keystroke->Unicode ground truth. Needs native-speaker QA before shipping.\",");
    sb.AppendLine($"  \"id\": \"{id}\",");
    sb.AppendLine($"  \"name\": \"InScript\",");
    sb.AppendLine($"  \"fontId\": \"{fontId}\",");
    sb.AppendLine("  \"keys\": [");
    sb.AppendLine(string.Join(",\n", rows));
    sb.AppendLine("  ],");
    sb.AppendLine("  \"deadKeyCombinations\": []");
    sb.AppendLine("}");
    return sb.ToString();
}

static (int Vk, bool Shift)? VkFor(char c)
{
    if (c >= 'a' && c <= 'z') return (c - 32, false);
    if (c >= 'A' && c <= 'Z') return (c, true);
    if (c >= '1' && c <= '9') return (c, false);
    if (c == '0') return (0x30, false);
    return c switch
    {
        ')' => (0x30, true), '!' => (0x31, true), '@' => (0x32, true), '#' => (0x33, true),
        '$' => (0x34, true), '%' => (0x35, true), '^' => (0x36, true), '&' => (0x37, true),
        '*' => (0x38, true), '(' => (0x39, true),
        ';' => (0xBA, false), ':' => (0xBA, true), '=' => (0xBB, false), '+' => (0xBB, true),
        ',' => (0xBC, false), '<' => (0xBC, true), '-' => (0xBD, false), '_' => (0xBD, true),
        '.' => (0xBE, false), '>' => (0xBE, true), '/' => (0xBF, false), '?' => (0xBF, true),
        '`' => (0xC0, false), '~' => (0xC0, true), '[' => (0xDB, false), '{' => (0xDB, true),
        ']' => (0xDD, false), '}' => (0xDD, true), '\'' => (0xDE, false), '"' => (0xDE, true),
        _ => ((int, bool)?)null
    };
}

static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

static string DecodeBasicEntities(string s) => s
    .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
    .Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&nbsp;", " ");

sealed class Pair
{
    public string Key { get; set; } = "";
    public string Uni { get; set; } = "";
}

sealed class LayoutMap
{
    public string Source { get; set; } = "";
    public string Script { get; set; } = "";
    public string Layout { get; set; } = "";
    public List<Pair> Pairs { get; set; } = new();
}
