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
bool apply = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--chm") chmDir = args[++i];
    else if (args[i] == "--apply") apply = true;
}
if (chmDir.Length == 0) { Console.Error.WriteLine("usage: kbdfromchm --chm <dir> [--apply]"); return 1; }

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

int rebuilt = 0, keysFixed = 0, keysAdded = 0, seqAdded = 0, escAdded = 0, symAdded = 0, skipped = 0;
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
