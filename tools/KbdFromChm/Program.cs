using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// KbdFromChm — rebuild the keyboard layouts from the vendor's own published key
// charts (extracted by tools/ChmMap) instead of the decoded byte schemes.
//
// Two things come out of the charts that the byte files cannot give:
//   * the right character for keys the byte decode got wrong or missed;
//   * the multi-key runs - about two thirds of every chart - that make the
//     difference between typing અ ા and typing આ.
//
//   dotnet run --project tools/KbdFromChm -- --chm <dir> [--apply]

string chmDir = "";
string schemeDir = "";
bool apply = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--chm") chmDir = args[++i];
    else if (args[i] == "--scheme") schemeDir = args[++i];
    else if (args[i] == "--apply") apply = true;
}
if (chmDir.Length == 0) { Console.Error.WriteLine("usage: kbdfromchm --chm <dir> [--scheme <dir>] [--apply]"); return 1; }

string repo = FindRoot();
string layoutsDir = Path.Combine(repo, "assets", "config", "layouts");
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();

// which script each layout belongs to, from the language that lists it
var scriptOf = new Dictionary<string, string>
{
    ["hi"] = "dev", ["mr"] = "dev", ["sa"] = "dev", ["gu"] = "guj", ["pa"] = "pun",
    ["bn"] = "ban", ["as"] = "ban", ["mni"] = "ban", ["te"] = "tel", ["kn"] = "kan",
    ["ml"] = "mal", ["or"] = "ori", ["ta"] = "tam",
};
// the file a layout lives in is named independently of its id, so index by file
var byFile = new Dictionary<string, (string Script, string Name)>(StringComparer.OrdinalIgnoreCase);
foreach (var lang in bundle.Languages)
{
    if (!scriptOf.TryGetValue(lang.Id, out string? script)) continue;
    foreach (var l in lang.Layouts)
    {
        string file = Path.GetFileName(l.File);
        if (file.Length > 0 && !byFile.ContainsKey(file)) byFile[file] = (script, l.Name);
    }
}

// character -> virtual key + shift, on a US keyboard
var toKey = new Dictionary<char, (int Vk, bool Shift)>();
void Add(int vk, char bas, char shifted) { toKey[bas] = (vk, false); toKey[shifted] = (vk, true); }
for (int vk = 'A'; vk <= 'Z'; vk++) Add(vk, (char)(vk + 32), (char)vk);
string digitsShift = ")!@#$%^&*(";
for (int vk = '0'; vk <= '9'; vk++) Add(vk, (char)vk, digitsShift[vk - '0']);
Add(186, ';', ':'); Add(187, '=', '+'); Add(188, ',', '<'); Add(189, '-', '_');
Add(190, '.', '>'); Add(191, '/', '?'); Add(192, '`', '~'); Add(219, '[', '{');
Add(220, (char)92, '|'); Add(221, ']', '}'); Add(222, (char)39, '"');

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

// A chart cell can hold an invisible formatting character (a soft hyphen, a
// joiner). Those are not letters, no legacy font draws them, and a key that
// types one is a typist typing nothing - so they are left out of the layouts.
static bool Formatting(string s) =>
    s.All(c => c is (char)0x00AD or (char)0x200B or (char)0x200C or (char)0x200D
                 or (char)0x200E or (char)0x200F or (char)0xFEFF);

static string Norm(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

// ---- the keys the charts do not print ----
//
// A published chart documents the keystrokes a typist is taught, not every key
// of the keyboard: the Gujarati InScript chart lists 50 of them. The vendor's own
// runtime file for that keyboard (INSCRIPT.GUJ) does have an entry for every key,
// but it holds an internal code, not Unicode and not the font's byte - every one
// of our six Gujarati font tables scores 0 against the vendor's chart on those
// codes, so they cannot simply be looked up.
//
// They can be learned instead. Each script has ~20 keyboards, all of them
// arrangements of the same internal codes, so joining a chart (key -> character)
// with its own runtime file (key -> code) on every key a chart does print gives
// code -> character; a code a chart never prints on one keyboard is usually
// printed on another. Where the keyboards disagree the reading with the most
// keyboards behind it wins.
static Dictionary<int, string> LearnCodes(string schemeDir, string chmScriptDir, string extension)
{
    var votes = new Dictionary<int, Dictionary<string, int>>();
    if (!Directory.Exists(schemeDir) || !Directory.Exists(chmScriptDir)) return new();

    foreach (string scheme in Directory.EnumerateFiles(schemeDir, "*" + extension))
    {
        string chart = Path.Combine(chmScriptDir, Path.GetFileNameWithoutExtension(scheme).ToLowerInvariant() + ".json");
        if (!File.Exists(chart)) continue;

        byte[] codes = File.ReadAllBytes(scheme);
        if (codes.Length < 128) continue;
        using var doc = JsonDocument.Parse(File.ReadAllText(chart));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
        {
            string key = p.GetProperty("Key").GetString() ?? "";
            string uni = p.GetProperty("Uni").GetString() ?? "";
            if (key.Length != 1 || uni.Length == 0 || !seen.Add(key)) continue;
            if (key[0] is < '!' or > '~') continue;

            int code = codes[key[0]];
            if (code is 0 or 0x20) continue;   // the vendor leaves this key empty
            var reading = votes.TryGetValue(code, out var r) ? r : votes[code] = new(StringComparer.Ordinal);
            reading[uni] = reading.GetValueOrDefault(uni) + 1;
        }
    }

    // Only readings several keyboards agree on. A code seen once, or one the
    // keyboards are split over, is a guess - and a guess on a key is worse than
    // a key that types nothing.
    var settled = new Dictionary<int, string>();
    foreach (var (code, reading) in votes)
    {
        var ranked = reading.OrderByDescending(r => r.Value).ToList();
        int total = ranked.Sum(r => r.Value);
        if (ranked[0].Value >= 2 && ranked[0].Value * 2 > total) settled[code] = ranked[0].Key;
    }
    return settled;
}

// the vendor names its runtime keyboard files by script
var schemeExt = new Dictionary<string, string>
{
    ["dev"] = ".DEV", ["guj"] = ".GUJ", ["ban"] = ".BAN", ["pun"] = ".PUN", ["tel"] = ".TEL",
    ["kan"] = ".KAN", ["mal"] = ".MAL", ["ori"] = ".ORI", ["tam"] = ".TAM",
};
var learnedCache = new Dictionary<string, Dictionary<int, string>>();

Dictionary<int, string> Learned(string script)
{
    if (learnedCache.TryGetValue(script, out var known)) return known;
    known = schemeExt.TryGetValue(script, out string? ext)
        ? LearnCodes(schemeDir, Path.Combine(chmDir, script), ext)
        : new Dictionary<int, string>();
    learnedCache[script] = known;
    return known;
}

string? SchemeFor(string script, string layoutName)
{
    if (schemeDir.Length == 0 || !schemeExt.TryGetValue(script, out string? ext)) return null;
    return Directory.EnumerateFiles(schemeDir, "*" + ext)
        .FirstOrDefault(f => Norm(Path.GetFileNameWithoutExtension(f)) == Norm(layoutName));
}

int rebuilt = 0, keysFixed = 0, keysAdded = 0, seqAdded = 0, escAdded = 0, symAdded = 0, skipped = 0;
int filled = 0, blankInShree = 0, stillUnknown = 0;
foreach (string path in Directory.EnumerateFiles(layoutsDir, "*.json").OrderBy(p => p))
{
    if (!byFile.TryGetValue(Path.GetFileName(path), out var meta)) { skipped++; continue; }
    (string script, string name) = meta;

    string dir = Path.Combine(chmDir, script);
    if (!Directory.Exists(dir)) { skipped++; continue; }
    string? chart = Directory.EnumerateFiles(dir, "*.json")
        .FirstOrDefault(f => Norm(Path.GetFileNameWithoutExtension(f)) == Norm(name));
    if (chart is null) { skipped++; continue; }

    using var chartDoc = JsonDocument.Parse(File.ReadAllText(chart));
    var pairs = chartDoc.RootElement.GetProperty("Pairs").EnumerateArray()
        .Select(p => (Key: p.GetProperty("Key").GetString() ?? "", Uni: p.GetProperty("Uni").GetString() ?? ""))
        .Where(p => p.Key.Length > 0 && p.Uni.Length > 0 && !Formatting(p.Uni))
        .ToList();
    if (pairs.Count == 0) { skipped++; continue; }

    var specials = chartDoc.RootElement.TryGetProperty("Specials", out var sp)
        ? sp.EnumerateArray()
            .Select(p => (Key: p.GetProperty("Key").GetString() ?? "", Uni: p.GetProperty("Uni").GetString() ?? ""))
            .Where(p => p.Key.Length > 0 && p.Uni.Length > 0 && !Formatting(p.Uni))
            .ToList()
        : new List<(string Key, string Uni)>();

    var layout = JsonSerializer.Deserialize<KeyboardLayout>(File.ReadAllText(path), opts)!;
    var byVk = layout.Keys.ToDictionary(k => k.VirtualKey);

    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var (key, uni) in pairs.Where(p => p.Key.Length == 1))
    {
        if (!seen.Add(key)) continue;   // a chart can list a key twice; the first table is the keyboard
        if (!toKey.TryGetValue(key[0], out var pos)) continue;
        if (!byVk.TryGetValue(pos.Vk, out var m))
        {
            m = new KeyMapping { VirtualKey = pos.Vk };
            byVk[pos.Vk] = m;
            layout.Keys.Add(m);
            keysAdded++;
        }
        string? before = pos.Shift ? m.Shift : m.Base;
        if (before != uni)
        {
            if (!string.IsNullOrEmpty(before)) keysFixed++; else keysAdded++;
            if (pos.Shift) m.Shift = uni; else m.Base = uni;
        }
    }

    // Multi-key runs. Each of the two reach-past-the-keyboard combinations counts
    // as a keystroke of its own here, exactly as the charts write it, so "Øj" (the
    // keyboard's own third layer) and "kØqk" are runs like any other.
    var seqs = pairs.Where(p => p.Key.Length > 1).Concat(specials)
                    .GroupBy(p => p.Key)
                    .Select(g => new KeySequence { Keys = g.Key, To = g.First().Uni })
                    .OrderBy(s => s.Keys.Length).ThenBy(s => s.Keys, StringComparer.Ordinal)
                    .ToList();
    seqAdded += seqs.Count;
    layout.Sequences = seqs;

    // and each layer's one-key entries again, without the mark, for the viewer
    layout.Escape = OneKeyLayer(seqs, EscapeLayer.Mark);
    layout.Symbols = OneKeyLayer(seqs, EscapeLayer.SymbolMark);
    escAdded += layout.Escape.Count;
    symAdded += layout.Symbols.Count;

    // The chart has had its say; the keys it never printed come from the vendor's
    // own runtime file for this keyboard. Only keys that are still empty are
    // filled: a chart, and the decode this layout already carried, both outrank a
    // reading pieced together from the other keyboards.
    if (SchemeFor(script, name) is { } schemeFile)
    {
        byte[] codes = File.ReadAllBytes(schemeFile);
        var known = Learned(script);
        foreach (var (ch, pos) in toKey)
        {
            if (ch is < '!' or > '~') continue;
            byVk.TryGetValue(pos.Vk, out var m);
            if (!string.IsNullOrEmpty(pos.Shift ? m?.Shift : m?.Base)) continue;

            int code = codes[ch];
            if (code is 0 or 0x20) { blankInShree++; continue; }  // the vendor types nothing here either
            if (!known.TryGetValue(code, out string? uni) || Formatting(uni)) { stillUnknown++; continue; }

            if (m is null)
            {
                m = new KeyMapping { VirtualKey = pos.Vk };
                byVk[pos.Vk] = m;
                layout.Keys.Add(m);
            }
            if (pos.Shift) m.Shift = uni; else m.Base = uni;
            filled++;
        }
    }

    // clear anything an earlier pass left that only types an invisible character
    foreach (var m in layout.Keys)
    {
        if (!string.IsNullOrEmpty(m.Base) && Formatting(m.Base!)) m.Base = null;
        if (!string.IsNullOrEmpty(m.Shift) && Formatting(m.Shift!)) m.Shift = null;
        if (!string.IsNullOrEmpty(m.AltGr) && Formatting(m.AltGr!)) m.AltGr = null;
    }
    layout.Keys.RemoveAll(m => string.IsNullOrEmpty(m.Base) && string.IsNullOrEmpty(m.Shift)
                               && string.IsNullOrEmpty(m.AltGr) && !m.DeadKey);
    layout.Keys.Sort((a, b) => a.VirtualKey.CompareTo(b.VirtualKey));

    if (apply)
        File.WriteAllText(path, JsonSerializer.Serialize(layout, opts), new UTF8Encoding(false));
    rebuilt++;
}

Console.WriteLine($"layouts rebuilt from the vendor charts : {rebuilt}");
Console.WriteLine($"  single keys corrected                : {keysFixed}");
Console.WriteLine($"  single keys added                    : {keysAdded}");
Console.WriteLine($"  multi-key runs written               : {seqAdded}");
Console.WriteLine($"  Ctrl+Space layer keys written        : {escAdded}");
Console.WriteLine($"  Ctrl+Shift+1 layer keys written      : {symAdded}");
Console.WriteLine($"  keys the charts never printed        : filled {filled}, empty in Shree too {blankInShree}, still unnamed {stillUnknown}");
Console.WriteLine($"layouts left alone (no chart)          : {skipped}");
if (!apply) Console.WriteLine("\ndry run - pass --apply to write the files");
return 0;

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}

/// <summary>The entries a layer puts on a single key, with the layer's mark taken off.</summary>
static List<KeySequence> OneKeyLayer(List<KeySequence> seqs, char mark) =>
    seqs.Where(s => s.Keys.Length == 2 && s.Keys[0] == mark && s.Keys[1] != mark)
        .Select(s => new KeySequence { Keys = s.Keys[1..], To = s.To })
        .OrderBy(e => e.Keys, StringComparer.Ordinal)
        .ToList();
