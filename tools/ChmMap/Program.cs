using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FontSoftware.Core.Models;

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

int totalLayouts = 0, totalPairs = 0, totalSpecials = 0, failed = 0;
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

            var (pairs, specials) = ParsePage(ReadHtml(html));
            if (pairs.Count == 0) { Console.Error.WriteLine($"  0 pairs: {Path.GetFileName(chm)}"); failed++; continue; }

            var map = new LayoutMap
            {
                Source = Path.GetFileName(chm),
                Script = script,
                Layout = layout,
                Pairs = pairs,
                Specials = specials
            };
            File.WriteAllText(Path.Combine(scriptOut, Slug(layout) + ".json"),
                              JsonSerializer.Serialize(map, jsonOpts));
            totalLayouts++; totalPairs += pairs.Count; totalSpecials += specials.Count;
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

Console.WriteLine($"\nLayouts extracted: {totalLayouts}   pairs: {totalPairs}   escape keys: {totalSpecials}   failed: {failed}");
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

/// <summary>
/// The help pages are Word exports and say so: nearly everything is a numeric
/// entity, but the dashes and the multiplication sign are literal bytes in the
/// page's own code page. Read them as declared or those rows come out as U+FFFD.
/// </summary>
static string ReadHtml(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    string probe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
    var m = Regex.Match(probe, "(?i)charset\\s*=\\s*\"?([A-Za-z0-9-]+)");
    string charset = m.Success ? m.Groups[1].Value.ToLowerInvariant() : "utf-8";
    if (charset is "utf-8" or "utf8") return Encoding.UTF8.GetString(bytes);

    // windows-1252 is Latin-1 with punctuation in the 0x80-0x9F range
    const string high = "\u20AC\u0081\u201A\u0192\u201E\u2026\u2020\u2021\u02C6\u2030"
                      + "\u0160\u2039\u0152\u008D\u017D\u008F\u0090\u2018\u2019\u201C"
                      + "\u201D\u2022\u2013\u2014\u02DC\u2122\u0161\u203A\u0153\u009D"
                      + "\u017E\u0178";
    var sb = new StringBuilder(bytes.Length);
    foreach (byte b in bytes) sb.Append(b is >= 0x80 and <= 0x9F ? high[b - 0x80] : (char)b);
    return sb.ToString();
}

/// <summary>
/// A help page holds two kinds of table: the keyboard itself, and a "Special
/// Keys" table for the characters that live on the escape layer - the ones the
/// vendor documents as "Ctrl+Shift+1, k". They are laid out character | meaning
/// | keystroke, so the meaning column looks exactly like a keystroke to the cell
/// scanner ("Avagraha" was being shipped as a key run). Pull those tables out
/// first, read them properly, and let the scanner see only the keyboard.
/// </summary>
static (List<Pair> Pairs, List<Special> Specials) ParsePage(string html)
{
    html = html.Replace('\r', ' ').Replace('\n', ' ');
    var specials = new List<Special>();
    var keyboard = new StringBuilder();
    int pos = 0;
    foreach (Match t in Regex.Matches(html, "<table.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
    {
        var rows = ParseSpecials(t.Value);
        if (rows.Count == 0) continue;
        keyboard.Append(html, pos, t.Index - pos);
        pos = t.Index + t.Length;
        specials.AddRange(rows);
    }
    keyboard.Append(html, pos, html.Length - pos);
    return (ParsePairs(keyboard.ToString()), specials);
}

static List<Special> ParseSpecials(string table)
{
    var found = new List<Special>();
    foreach (Match row in Regex.Matches(table, "<tr.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
    {
        var cells = Regex.Matches(row.Value, "<t[dh].*?</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                         .Select(c => CellText(c.Value)).ToList();
        if (cells.Count < 2) continue;

        // the keystroke cell names the escape combination; everything else is
        // the character it types and the vendor's description of it
        int k = cells.FindIndex(c => Regex.IsMatch(c, "(?i)ctrl\\s*\\+\\s*shift\\s*\\+\\s*1"));
        if (k < 0) continue;
        string? key = EscapeKey(cells[k]);
        if (key is null) continue;

        string uni = cells.Take(k).FirstOrDefault(c => c.Any(ch => ch > '~')) ?? "";
        uni = new string(uni.Where(ch => ch > '~').ToArray());
        if (uni.Length == 0)
        {
            // the escape layer also carries plain punctuation - the brackets, whose
            // own keys type a letter while an Indic keyboard is on
            uni = cells.Take(k).FirstOrDefault(c => c.Length is > 0 and <= 2
                                                    && c.All(ch => ch is > ' ' and <= '~'
                                                                   && !char.IsLetterOrDigit(ch))) ?? "";
        }
        if (uni.Length == 0) continue;     // drawn with a private-use glyph: no Unicode to type

        string meaning = cells.Take(k).LastOrDefault(c => c.Length > 0 && c.All(ch => ch <= '~')) ?? "";
        // written the way the inline entries are, so both sources are one layer
        found.Add(new Special { Key = EscapeLayer.SymbolMark + key, Uni = uni, Meaning = meaning });
    }
    return found;
}

/// <summary>The key pressed after the escape combination, e.g. "Ctrl+shift+1, Shift -" is "_".</summary>
static string? EscapeKey(string cell)
{
    var m = Regex.Match(cell, "(?i)ctrl\\s*\\+\\s*shift\\s*\\+\\s*1\\s*,?\\s*(.*)$");
    if (!m.Success) return null;
    string key = m.Groups[1].Value.Trim();
    key = Regex.Replace(key, "^\\(\\*+\\)\\s*", "").Trim();          // footnote marker
    var shift = Regex.Match(key, "(?i)^shift\\s*\\+?\\s*(\\S)$");
    if (shift.Success) key = Shifted(shift.Groups[1].Value[0]).ToString();
    if (key.Length == 0 || key.Length > 3) return null;
    return key.All(c => c is > ' ' and <= '~') ? key : null;
}

static char Shifted(char c)
{
    if (c is >= 'a' and <= 'z') return char.ToUpperInvariant(c);
    if (c is >= '0' and <= '9') return ")!@#$%^&*("[c - '0'];
    return c switch
    {
        ';' => ':', '=' => '+', ',' => '<', '-' => '_', '.' => '>', '/' => '?',
        '`' => '~', '[' => '{', ']' => '}', '\\' => '|', '\'' => '"', _ => c,
    };
}

/// <summary>
/// Drop the HTML tags and leave the text - including a keystroke that is itself a
/// less-than sign. "&lt;d&lt;" is three keys on a Kannada keyboard, and a plain
/// "&lt;[^&gt;]*&gt;" swallows it together with the &lt;/td&gt; that follows,
/// which is how "&lt;d&lt;" became the key "d" and put a conjunct on the virama
/// key. A tag starts with a letter, a slash or a bang and holds no further "&lt;".
/// </summary>
static string DropTags(string html, string with) =>
    Regex.Replace(Regex.Replace(html, "<!--.*?-->", with, RegexOptions.Singleline),
                  "</?[A-Za-z!?][^<>]*>", with);

/// <summary>The text of one table cell: tags dropped, entities decoded, runs of space collapsed.</summary>
static string CellText(string cell)
{
    string s = DropTags(cell, "");
    s = Regex.Replace(s, "&#x?([0-9A-Fa-f]+);", m =>
    {
        bool hex = m.Value.StartsWith("&#x", StringComparison.OrdinalIgnoreCase);
        return int.TryParse(m.Groups[1].Value, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int cp) && cp > 0 && cp <= 0x10FFFF
            ? char.ConvertFromUtf32(cp) : "";
    });
    return Regex.Replace(DecodeBasicEntities(s), "\\s+", " ").Trim();
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
    // The charts print the escape inside the keystroke, as its own glyph: "Øj" is
    // the escape and then j, and "jØqj" is j, escape, q, j. Keep it where it
    // stands - dropping it silently turned every one of those into a wrong
    // single-key mapping (ঝ landed on j, which is জ).
    html = html.Replace("&#216;", EscapeLayer.Mark.ToString());
    // split into table cells
    var cells = Regex.Split(html, "</td>", RegexOptions.IgnoreCase);
    var pairs = new List<Pair>();
    string? pendingUni = null;

    foreach (var raw in cells)
    {
        string stripped = DropTags(raw, " ");
        // unicode numeric entities -> the actual characters
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(stripped, "&#x?([0-9A-Fa-f]+);"))
        {
            string digits = m.Groups[1].Value;
            bool hex = m.Value.StartsWith("&#x", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out int cp) || cp <= 0 || cp > 0x10FFFF)
                continue;
            // A control character is a glyph number in some symbol font the page
            // once used, not a character: "&#154;" left a stray U+009A sitting on
            // the end of a Tamil key. Nothing types those.
            if (cp is < 0x20 or (>= 0x7F and <= 0x9F)) continue;
            sb.Append(char.ConvertFromUtf32(cp));
        }
        string uni = sb.ToString();

        // Residual text -> potential keystroke. A keystroke that is punctuation is
        // written as an entity like the characters are (&#60; is the key "<"), so
        // decode the ASCII ones into the keystroke instead of dropping them: doing
        // otherwise turned the three keys "<d<" into "d" and put a conjunct on the
        // virama key of every Kannada keyboard.
        string text = Regex.Replace(stripped, "&#x?([0-9A-Fa-f]+);", m =>
        {
            bool hex = m.Value.StartsWith("&#x", StringComparison.OrdinalIgnoreCase);
            return int.TryParse(m.Groups[1].Value, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int cp) && cp is > 0x20 and <= 0x7E
                   ? ((char)cp).ToString() : " ";
        });
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
        if (pendingUni is not null && text.Length is > 0 and <= 10 && !isLabel
            && text.All(c => c <= (char)0x7E || c == EscapeLayer.Mark))
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

/// <summary>A character on the escape layer: press the escape combination, then <see cref="Key"/>.</summary>
sealed class Special
{
    public string Key { get; set; } = "";
    public string Uni { get; set; } = "";
    public string Meaning { get; set; } = "";
}

sealed class LayoutMap
{
    public string Source { get; set; } = "";
    public string Script { get; set; } = "";
    public string Layout { get; set; } = "";
    public List<Pair> Pairs { get; set; } = new();
    public List<Special> Specials { get; set; } = new();
}
