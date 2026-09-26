using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// KbdFromCapture — put the vendor's own answer into our keyboard layouts.
//
// tools/KbdCapture asks the vendor's composer, key by key, what each keyboard
// types (see its header). This takes those captures and makes our layouts say
// the same thing: every printed key, and the third layer reached with
// Ctrl+Space. The published charts stay the source for multi-key runs, which
// the composer's one-key-at-a-time API cannot give.
//
// Ranking, highest first: the composer, then the charts, then whatever decode a
// layout already carried.
//
//   dotnet run --project tools/KbdFromCapture -- --capture <dir> [--apply]

string captureDir = "";
string chmDir = "";
bool apply = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--capture") captureDir = args[++i];
    else if (args[i] == "--chm") chmDir = args[++i];
    else if (args[i] == "--apply") apply = true;
}
if (captureDir.Length == 0) { Console.Error.WriteLine("usage: kbdfromcapture --capture <dir> --chm <dir> [--apply]"); return 1; }

string repo = FindRoot();
string layoutsDir = Path.Combine(repo, "assets", "config", "layouts");
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();

var scriptOf = new Dictionary<string, string>
{
    ["hi"] = "dev", ["mr"] = "dev", ["sa"] = "dev", ["gu"] = "guj", ["pa"] = "pun",
    ["bn"] = "ban", ["as"] = "ban", ["mni"] = "ban", ["te"] = "tel", ["kn"] = "kan",
    ["ml"] = "mal", ["or"] = "ori", ["ta"] = "tam",
};
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

static string Norm(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

int matched = 0, noCapture = 0;
int keysSame = 0, keysFixed = 0, keysAdded = 0, keysCleared = 0, thirdWritten = 0, keysFromChart = 0;
var examples = new List<string>();

foreach (string path in Directory.EnumerateFiles(layoutsDir, "*.json").OrderBy(p => p))
{
    if (!byFile.TryGetValue(Path.GetFileName(path), out var meta)) { noCapture++; continue; }
    (string script, string name) = meta;

    string dir = Path.Combine(captureDir, script);
    if (!Directory.Exists(dir)) { noCapture++; continue; }
    string? file = Directory.EnumerateFiles(dir, "*.json")
        .FirstOrDefault(f => Norm(Path.GetFileNameWithoutExtension(f)) == Norm(name));
    if (file is null) { noCapture++; continue; }

    using var doc = JsonDocument.Parse(File.ReadAllText(file));
    var typed = Read(doc.RootElement, "keys");
    var third = Read(doc.RootElement, "alter");
    if (typed.Count == 0) { noCapture++; continue; }

    // Where the vendor's published chart speaks, it wins. The two differ only on
    // the vowel keys of the phonetic keyboards: asked in isolation the composer
    // answers with the matra, while the chart prints the independent vowel - what
    // a typist is taught, and what our multi-key runs were built from. So the
    // composer fills the keys the chart never printed, and touches nothing else.
    var printed = new HashSet<string>(StringComparer.Ordinal);
    var printedThird = new HashSet<string>(StringComparer.Ordinal);
    if (chmDir.Length > 0)
    {
        string chartDir = Path.Combine(chmDir, script);
        string? chart = Directory.Exists(chartDir)
            ? Directory.EnumerateFiles(chartDir, "*.json")
                       .FirstOrDefault(f => Norm(Path.GetFileNameWithoutExtension(f)) == Norm(name))
            : null;
        if (chart is not null)
        {
            using var chartDoc = JsonDocument.Parse(File.ReadAllText(chart));
            foreach (var p in chartDoc.RootElement.GetProperty("Pairs").EnumerateArray())
            {
                string k = p.GetProperty("Key").GetString() ?? "";
                if (k.Length == 1) printed.Add(k);
                else if (k.Length == 2 && k[0] == EscapeLayer.Mark) printedThird.Add(k[1..]);
            }
        }
    }

    var layout = JsonSerializer.Deserialize<KeyboardLayout>(File.ReadAllText(path), opts)!;
    var byVk = layout.Keys.ToDictionary(k => k.VirtualKey);

    // Every key the chart never printed, from the composer.
    foreach (var (ch, pos) in toKey)
    {
        if (printed.Contains(ch.ToString())) { keysFromChart++; continue; }
        typed.TryGetValue(ch.ToString(), out string? want);
        if (!byVk.TryGetValue(pos.Vk, out var m))
        {
            if (want is null) continue;
            m = new KeyMapping { VirtualKey = pos.Vk };
            byVk[pos.Vk] = m;
            layout.Keys.Add(m);
        }

        string? have = pos.Shift ? m.Shift : m.Base;
        if (have == want) { if (want is not null) keysSame++; continue; }

        // Silence from the composer is not evidence that a key types nothing: it
        // answers for a few characters - the virama among them - with the tutor's
        // own placeholder, which we discard. So the composer only adds and
        // corrects; clearing a key would throw away what the chart knew.
        if (want is null) { keysCleared++; continue; }
        if (string.IsNullOrEmpty(have)) keysAdded++;
        else
        {
            keysFixed++;
            if (examples.Count < 14) examples.Add($"{name,-12} '{ch}'  ours {have}  composer {want}");
        }
        if (pos.Shift) m.Shift = want; else m.Base = want;
    }

    // The third layer, on the same terms: what the chart prints stays, and the
    // composer fills the rest of it. Only its one-key entries are ours to touch;
    // the charts also carry longer runs that pass through the layer ("kØqk").
    var fromChart = layout.Escape
        .Where(e => e.Keys.Length == 1 && printedThird.Contains(e.Keys))
        .ToList();
    var fromComposer = third
        .Where(t => t.Key.Length == 1 && !printedThird.Contains(t.Key))
        .Select(t => new KeySequence { Keys = t.Key, To = t.Value });
    layout.Escape = fromChart.Concat(fromComposer)
                             .GroupBy(e => e.Keys)
                             .Select(g => g.First())
                             .OrderBy(e => e.Keys, StringComparer.Ordinal)
                             .ToList();
    layout.Sequences.RemoveAll(s => s.Keys.Length == 2 && s.Keys[0] == EscapeLayer.Mark);
    layout.Sequences.AddRange(layout.Escape.Select(e => new KeySequence
    {
        Keys = EscapeLayer.Mark + e.Keys,
        To = e.To,
    }));
    layout.Sequences = layout.Sequences
        .GroupBy(s => s.Keys)
        .Select(g => g.First())
        .OrderBy(s => s.Keys.Length).ThenBy(s => s.Keys, StringComparer.Ordinal)
        .ToList();
    thirdWritten += layout.Escape.Count;

    layout.Keys.RemoveAll(m => string.IsNullOrEmpty(m.Base) && string.IsNullOrEmpty(m.Shift)
                               && string.IsNullOrEmpty(m.AltGr) && !m.DeadKey);
    layout.Keys.Sort((a, b) => a.VirtualKey.CompareTo(b.VirtualKey));

    if (apply)
        File.WriteAllText(path, JsonSerializer.Serialize(layout, opts), new UTF8Encoding(false));
    matched++;
}

Console.WriteLine($"layouts the composer answered for : {matched}");
Console.WriteLine($"  keys the chart already printed  : {keysFromChart}");
Console.WriteLine($"  keys we already had right       : {keysSame}");
Console.WriteLine($"  keys corrected                  : {keysFixed}");
Console.WriteLine($"  keys added                      : {keysAdded}");
Console.WriteLine($"  keys the composer had no word for: {keysCleared} (left as the chart had them)");
Console.WriteLine($"  Ctrl+Space entries written      : {thirdWritten}");
Console.WriteLine($"layouts with no capture           : {noCapture}");
if (examples.Count > 0)
{
    Console.WriteLine("\nwhat the composer disagreed with:");
    foreach (string e in examples) Console.WriteLine("  " + e);
}
if (!apply) Console.WriteLine("\ndry run - pass --apply to write the files");
return 0;

static Dictionary<string, string> Read(JsonElement root, string property)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    if (!root.TryGetProperty(property, out var node)) return map;
    foreach (var p in node.EnumerateObject())
        if (p.Value.GetString() is { Length: > 0 } text) map[p.Name] = text;
    return map;
}

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}
