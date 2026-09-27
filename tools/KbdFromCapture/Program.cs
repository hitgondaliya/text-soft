using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Tools;

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
string schemeDir = "";
bool apply = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--capture") captureDir = args[++i];
    else if (args[i] == "--chm") chmDir = args[++i];
    else if (args[i] == "--scheme") schemeDir = args[++i];
    else if (args[i] == "--apply") apply = true;
}
if (captureDir.Length == 0) { Console.Error.WriteLine("usage: kbdfromcapture --capture <dir> --chm <dir> [--scheme <dir>] [--apply]"); return 1; }

// code -> character for a script, learned from every keyboard the vendor ships:
// the third voice, for the keys the chart and the composer disagree about.
var learnedCache = new Dictionary<string, Dictionary<int, string>>();
Dictionary<int, string> Learned(string script)
{
    if (learnedCache.TryGetValue(script, out var known)) return known;
    known = schemeDir.Length > 0 && Vendor.SchemeExt.TryGetValue(script, out string? ext)
        ? Vendor.LearnCodes(schemeDir, Path.Combine(chmDir, script), ext)
        : new Dictionary<int, string>();
    return learnedCache[script] = known;
}

string repo = Vendor.FindRoot();
string layoutsDir = Path.Combine(repo, "assets", "config", "layouts");
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();

var scriptOf = Vendor.ScriptOf;
var byFile = new Dictionary<string, (string Script, string Name, string Captures)>(StringComparer.OrdinalIgnoreCase);
foreach (var lang in bundle.Languages)
{
    if (!scriptOf.TryGetValue(lang.Id, out string? script)) continue;
    foreach (var l in lang.Layouts)
    {
        string file = Path.GetFileName(l.File);
        if (file.Length > 0 && !byFile.ContainsKey(file))
            byFile[file] = (script, l.Name, Vendor.CaptureScript(lang.Id, script));
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
int keysSame = 0, keysFixed = 0, keysAdded = 0, keysCleared = 0, thirdWritten = 0, keysFromChart = 0, thirdFixed = 0, runsRetuned = 0;
var examples = new List<string>();
var missing = new List<string>();

foreach (string path in Directory.EnumerateFiles(layoutsDir, "*.json").OrderBy(p => p))
{
    if (!byFile.TryGetValue(Path.GetFileName(path), out var meta)) { noCapture++; continue; }
    (string script, string name, string captures) = meta;

    // Match on the layout's own file name as well as its display name: our files
    // are named after the vendor's keyboard files, so guj-typewrit.json finds
    // TYPEWRIT.GUJ where the display name "Typewriter" does not.
    string? file = Vendor.Find(captureDir, captures, name, path)
                ?? Vendor.Find(captureDir, script, name, path);
    if (file is null) { noCapture++; if (!Vendor.NotShree(name)) missing.Add($"{script}/{name}"); continue; }

    using var doc = JsonDocument.Parse(File.ReadAllText(file));
    var typed = Read(doc.RootElement, "keys");
    var third = Read(doc.RootElement, "alter");
    if (typed.Count == 0) { noCapture++; continue; }

    // A keyboard the vendor ships twice - INSCRIPT.ASS beside INSCRIPT.BAN - is one
    // keyboard with a few letters changed for the language, and the one help file
    // they wrote for it describes the other language's. So on the keys where the two
    // runtime files answer differently, this language's own file has the last word,
    // chart or no chart.
    var ownKeys = new Dictionary<string, string>(StringComparer.Ordinal);
    if (captures != script && Vendor.Find(captureDir, script, name, path) is { } twinFile && twinFile != file)
    {
        using var twin = JsonDocument.Parse(File.ReadAllText(twinFile));
        var twinTyped = Read(twin.RootElement, "keys");
        foreach (var (key, mine) in typed)
            if (twinTyped.TryGetValue(key, out string? theirs) && theirs != mine)
                ownKeys[key] = theirs;
    }

    // Where the vendor's published chart speaks, it wins. The two differ only on
    // the vowel keys of the phonetic keyboards: asked in isolation the composer
    // answers with the matra, while the chart prints the independent vowel - what
    // a typist is taught, and what our multi-key runs were built from. So the
    // composer fills the keys the chart never printed, and touches nothing else.
    var printed = new HashSet<string>(StringComparer.Ordinal);
    var printedThird = new HashSet<string>(StringComparer.Ordinal);
    // How many keystrokes of this keyboard the chart gives each character to. A
    // character on two keystrokes is the tell that one of the two rows lost its
    // own character, and that is the one case where the composer overrules the
    // chart - see Vendor.ComposerWins.
    var strokesFor = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var valuesFor = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    if (chmDir.Length > 0)
    {
        string? chart = Vendor.Find(chmDir, script, name, path);
        if (chart is not null)
        {
            using var chartDoc = JsonDocument.Parse(File.ReadAllText(chart));
            foreach (var p in chartDoc.RootElement.GetProperty("Pairs").EnumerateArray())
            {
                string k = p.GetProperty("Key").GetString() ?? "";
                string u = p.GetProperty("Uni").GetString() ?? "";
                if (k.Length == 1) printed.Add(k);
                else if (k.Length == 2 && k[0] == EscapeLayer.Mark) printedThird.Add(k[1..]);
                if (k.Length > 0 && u.Length > 0)
                {
                    if (!strokesFor.TryGetValue(u, out var set)) strokesFor[u] = set = new(StringComparer.Ordinal);
                    set.Add(k);
                    if (!valuesFor.TryGetValue(k, out var vals)) valuesFor[k] = vals = new(StringComparer.Ordinal);
                    vals.Add(u);
                }
            }
        }
    }

    // The vendor's runtime file for this keyboard: key -> internal code, which the
    // other keyboards of the script say what to read as.
    byte[]? codes = null;
    if (Vendor.SchemeFile(schemeDir, script, name, path) is { } schemeFile)
        try { codes = File.ReadAllBytes(schemeFile); } catch { }
    var learned = Learned(script);

    // Where the composer puts each character, the keystrokes written the way the
    // charts write them: a bare key, or "Øk" for the third layer.
    var composerStrokes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    foreach (var (k, v) in typed.Concat(third.Select(t => new KeyValuePair<string, string>(EscapeLayer.Mark + t.Key, t.Value))))
    {
        if (!composerStrokes.TryGetValue(v, out var set)) composerStrokes[v] = set = new(StringComparer.Ordinal);
        set.Add(k);
    }

    // Is the chart's row for this keystroke the only one of its kind - one
    // character, one keystroke; is the character it gives this key not already
    // accounted for on another key; and does the keyboard's own runtime file read
    // the way the chart does? Where it is not, the composer breaks the tie.
    bool Outvoted(string value, string stroke, string composer, Vendor.Difference how)
    {
        if (strokesFor.TryGetValue(value, out var set) && set.Any(s => s != stroke)) return true;
        if (valuesFor.TryGetValue(stroke, out var vals) && vals.Count > 1 && vals.Contains(composer)) return true;
        if (stroke.Length == 1 && stroke[0] is >= '!' and <= '~'
            && codes is { Length: > 127 } && learned.TryGetValue(codes[stroke[0]], out string? reading)
            && reading == composer) return true;

        // The character the chart gives this key is one the composer gives to a
        // different key - so this is the row that lost its own character. Only when
        // the composer's answer is its own, though: an answer it hands to five keys
        // at once (Bengali Swarna) is the composer having nothing to say - a letter
        // and its shifted twin are two keys, five are not - and a
        // longer answer it alone names is how the Tamil keyboards spell a vowel in
        // two pieces, which is not what a key should type.
        return composerStrokes.TryGetValue(value, out var also) && also.Any(s => s != stroke)
               && composerStrokes.GetValueOrDefault(composer, new()).Count <= 2
               && (composer.Length == 1 || how is not Vendor.Difference.DifferentCharacters);
    }

    var layout = JsonSerializer.Deserialize<KeyboardLayout>(File.ReadAllText(path), opts)!;
    var byVk = layout.Keys.ToDictionary(k => k.VirtualKey);

    // Every key the chart never printed, from the composer - and the few it did
    // print but repeated, where the composer is the one that still knows.
    foreach (var (ch, pos) in toKey)
    {
        string stroke = ch.ToString();
        typed.TryGetValue(stroke, out string? want);
        if (printed.Contains(stroke))
        {
            string? charted = pos.Shift ? byVk.GetValueOrDefault(pos.Vk)?.Shift : byVk.GetValueOrDefault(pos.Vk)?.Base;
            var how = charted is null || want is null
                ? Vendor.Difference.Same
                : Vendor.Compare(charted, want);
            if (!ownKeys.ContainsKey(stroke)
                && !Vendor.ComposerWins(how, Outvoted(charted ?? "", stroke, want ?? "", how)))
            { keysFromChart++; continue; }

            string fixedTo = Vendor.Payload(how, want!);
            if (pos.Shift) byVk[pos.Vk].Shift = fixedTo; else byVk[pos.Vk].Base = fixedTo;
            keysFixed++;
            if (examples.Count < 14)
            {
                var also = strokesFor.GetValueOrDefault(charted!, new()).Concat(composerStrokes.GetValueOrDefault(charted!, new()))
                                     .Where(s => s != stroke).Distinct();
                examples.Add($"{name,-12} '{ch}'  chart {charted} (also on {string.Join(" ", also)})  composer {fixedTo}");
            }
            continue;
        }
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
        .Select(e =>
        {
            if (!third.TryGetValue(e.Keys, out string? want)) return e;
            var how = Vendor.Compare(e.To, want);
            if (!Vendor.ComposerWins(how, Outvoted(e.To, EscapeLayer.Mark + e.Keys, want, how))) return e;
            thirdFixed++;
            return new KeySequence { Keys = e.Keys, To = Vendor.Payload(how, want) };
        })
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

    // The runs came from the other language's chart, so a key whose letter this
    // language changes changes it inside the runs that press that key too: on the
    // Assamese Indica keyboard "y" types ৱ, so its "yi" run types ৱি, not বি.
    foreach (var (key, theirs) in ownKeys)
    {
        if (theirs.Length == 0 || !typed.TryGetValue(key, out string? mine) || mine.Length == 0) continue;
        foreach (var s in layout.Sequences.Where(s => s.Keys.Contains(key) && s.To.Contains(theirs)))
        {
            s.To = s.To.Replace(theirs, mine);
            runsRetuned++;
        }
        foreach (var e in layout.Escape.Where(e => e.Keys.Contains(key) && e.To.Contains(theirs)))
            e.To = e.To.Replace(theirs, mine);
    }

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
Console.WriteLine($"  Ctrl+Space entries the chart repeated: {thirdFixed} (taken from the composer)");
Console.WriteLine($"  runs retuned to this language's own letters: {runsRetuned}");
Console.WriteLine($"layouts with no capture           : {noCapture}");
if (missing.Count > 0) Console.WriteLine("  " + string.Join(", ", missing.Take(20)));
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
