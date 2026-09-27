using System.Text;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Tools;

// KbdAudit — one table that says, keyboard by keyboard, whether we type what
// Shree-Lipi types.
//
// Two independent sources of truth, both the vendor's own:
//   * the published key charts out of their help files (tools/ChmMap) — what a
//     typist is taught: single keys and multi-key runs;
//   * the composer's own answers (tools/KbdCapture) — every key of the keyboard,
//     including the ones no chart prints, and the Ctrl+Space layer.
//
// A keyboard is "zero to zero" when nothing in either source disagrees with us,
// and nothing in either source is missing from us.
//
//   dotnet run --project tools/KbdAudit -- --chm <dir> --capture <dir>
//                                          [--report <file.txt>] [--all]

string chmDir = "", captureDir = "", report = "";
bool all = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--chm": chmDir = args[++i]; break;
        case "--capture": captureDir = args[++i]; break;
        case "--report": report = args[++i]; break;
        case "--all": all = true; break;
    }
}

string repo = Vendor.FindRoot();
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();
var scriptOf = Vendor.ScriptOf;

// vk -> the ASCII characters a US keyboard puts on it
var chars = new Dictionary<int, (char Base, char Shift)>();
for (int vk = 'A'; vk <= 'Z'; vk++) chars[vk] = ((char)(vk + 32), (char)vk);
for (int vk = '0'; vk <= '9'; vk++) chars[vk] = ((char)vk, ")!@#$%^&*("[vk - '0']);
chars[186] = (';', ':'); chars[187] = ('=', '+'); chars[188] = (',', '<');
chars[189] = ('-', '_'); chars[190] = ('.', '>'); chars[191] = ('/', '?');
chars[192] = ('`', '~'); chars[219] = ('[', '{'); chars[220] = ((char)92, '|');
chars[221] = (']', '}'); chars[222] = ((char)39, '"');

var rows = new List<Row>();
var detail = new List<string>();
var forms = new Dictionary<Vendor.Difference, int>();
void Form(Vendor.Difference how) => forms[how] = forms.GetValueOrDefault(how) + 1;
int repeated = 0;
void Repeated() => repeated++;

foreach (var lang in bundle.Languages)
{
    if (!scriptOf.TryGetValue(lang.Id, out string? script)) continue;
    foreach (var lref in lang.Layouts)
    {
        KeyboardLayout ours;
        try { ours = bundle.GetLayout(lref); } catch { continue; }

        var row = new Row
        {
            Lang = lang.Name, LangId = lang.Id, Script = script, Layout = lref.Name,
            OwnDesign = Vendor.NotShree(lref.Name),
        };

        var byChar = new Dictionary<char, string>();
        foreach (var k in ours.Keys)
        {
            if (!chars.TryGetValue(k.VirtualKey, out var c)) continue;
            if (!string.IsNullOrEmpty(k.Base)) byChar[c.Base] = k.Base!;
            if (!string.IsNullOrEmpty(k.Shift)) byChar[c.Shift] = k.Shift!;
        }
        var ourSeq = ours.Sequences.GroupBy(s => s.Keys)
                         .ToDictionary(g => g.Key, g => g.First().To, StringComparer.Ordinal);
        var ourEsc = ours.Escape.GroupBy(e => e.Keys)
                         .ToDictionary(g => g.Key, g => g.First().To, StringComparer.Ordinal);

        // The composer first, so the chart pass can tell a real difference from a
        // key we deliberately took from the composer instead.
        var typed = new Dictionary<string, string>(StringComparer.Ordinal);
        var alter = new Dictionary<string, string>(StringComparer.Ordinal);
        string? cap = Vendor.Find(captureDir, Vendor.CaptureScript(lang.Id, script), lref.Name, lref.File)
                   ?? Vendor.Find(captureDir, script, lref.Name, lref.File);
        if (cap is not null)
        {
            using var capDoc = JsonDocument.Parse(File.ReadAllText(cap));
            typed = Vendor.CaptureMap(capDoc.RootElement, "keys");
            alter = Vendor.CaptureMap(capDoc.RootElement, "alter");
        }

        // Where this language has a runtime file of its own for the keyboard, the
        // letters it changes change inside the other language's runs as well: the
        // Assamese Indica "yi" types ৱি where the Bengali chart writes বি.
        var ownKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        string? twinCap = Vendor.CaptureScript(lang.Id, script) == script
            ? null : Vendor.Find(captureDir, script, lref.Name, lref.File);
        if (twinCap is not null && twinCap != cap)
        {
            using var twin = JsonDocument.Parse(File.ReadAllText(twinCap));
            var twinTyped = Vendor.CaptureMap(twin.RootElement, "keys");
            foreach (var (key, mine) in typed)
                if (twinTyped.TryGetValue(key, out string? theirs) && theirs != mine) ownKeys[key] = theirs;
        }
        string Retuned(string charted, string stroke)
        {
            foreach (var (key, theirs) in ownKeys)
                if (stroke.Contains(key) && theirs.Length > 0 && typed.TryGetValue(key, out string? mine))
                    charted = charted.Replace(theirs, mine);
            return charted;
        }

        // An answer the composer gives to three keys at once is the composer having
        // nothing to say about any of them, not three keys that type the same thing.
        var answerCount = typed.Concat(alter)
            .GroupBy(p => p.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        bool NothingToSay(string answer) => answerCount.GetValueOrDefault(answer) >= 3;

        // What the composer says a keystroke types, carrier stripped, for the
        // keystrokes the chart writes: a bare key, or the third layer's "Øk".
        string? Composer(string stroke, string charted)
        {
            string? want = stroke.Length == 1 ? typed.GetValueOrDefault(stroke)
                         : stroke.Length == 2 && stroke[0] == EscapeLayer.Mark ? alter.GetValueOrDefault(stroke[1..])
                         : null;
            return want is null ? null : Vendor.Payload(Vendor.Compare(charted, want), want);
        }

        // ---- the published chart ----
        string? chart = Vendor.Find(chmDir, script, lref.Name, lref.File);
        if (chart is not null)
        {
            row.HasChart = true;
            using var doc = JsonDocument.Parse(File.ReadAllText(chart));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in Pairs(doc.RootElement, "Pairs").Concat(Pairs(doc.RootElement, "Specials")))
            {
                if (p.Uni.Length == 0 || Formatting(p.Uni)) continue;
                if (!seen.Add(p.Key)) continue;   // a chart may list the same keystroke twice
                if (p.Key.Length == 1)
                {
                    row.Chart++;
                    if (!byChar.TryGetValue(p.Key[0], out string? mine))
                    { row.ChartMiss++; Note(row, $"chart key '{p.Key}' missing (wants {p.Uni})"); }
                    else if (mine == p.Uni) row.ChartOk++;
                    else if (mine == Composer(p.Key, p.Uni) || mine == Retuned(p.Uni, p.Key)) row.ChartTaken++;
                    else { row.ChartWrong++; Note(row, $"chart key '{p.Key}' ours {mine} chart {p.Uni}"); }
                }
                else
                {
                    row.Runs++;
                    if (!ourSeq.TryGetValue(p.Key, out string? mine))
                    { row.RunsMiss++; Note(row, $"chart run '{Show(p.Key)}' missing (wants {p.Uni})"); }
                    else if (mine == p.Uni) row.RunsOk++;
                    else if (mine == Composer(p.Key, p.Uni) || mine == Retuned(p.Uni, p.Key)) row.ChartTaken++;
                    else { row.RunsWrong++; Note(row, $"chart run '{Show(p.Key)}' ours {mine} chart {p.Uni}"); }
                }
            }
        }

        // ---- the composer's own answers ----
        if (cap is not null)
        {
            row.HasCapture = true;
            using var doc = JsonDocument.Parse(File.ReadAllText(cap));
            foreach (var (key, want) in Map(doc.RootElement, "keys"))
            {
                if (key.Length != 1 || Formatting(want)) continue;
                row.Comp++;
                if (!byChar.TryGetValue(key[0], out string? mine))
                { row.CompMiss++; Note(row, $"composer key '{key}' missing (types {want})"); }
                else if (mine == want) row.CompOk++;
                else if (Vendor.Compare(mine, want) is var how and not Vendor.Difference.DifferentCharacters)
                { row.CompForm++; Form(how); }
                else if (NothingToSay(want)) { row.CompForm++; Repeated(); }
                else { row.CompWrong++; Note(row, $"composer key '{key}' ours {mine} composer {want}"); }
            }
            foreach (var (key, want) in Map(doc.RootElement, "alter"))
            {
                if (key.Length != 1 || Formatting(want)) continue;
                row.Third++;
                if (!ourEsc.TryGetValue(key, out string? mine))
                { row.ThirdMiss++; Note(row, $"Ctrl+Space '{key}' missing (types {want})"); }
                else if (mine == want) row.ThirdOk++;
                else if (Vendor.Compare(mine, want) is var how and not Vendor.Difference.DifferentCharacters)
                { row.ThirdForm++; Form(how); }
                else if (NothingToSay(want)) { row.ThirdForm++; Repeated(); }
                else { row.ThirdWrong++; Note(row, $"Ctrl+Space '{key}' ours {mine} composer {want}"); }
            }
        }

        rows.Add(row);
    }
}

// ---- report ----
var sb = new StringBuilder();
void W(string s = "") { Console.WriteLine(s); sb.AppendLine(s); }

int clean = rows.Count(r => r.Clean), dirty = rows.Count - clean;
W("=== every keyboard against Shree-Lipi's own two sources ===");
W();
W($"keyboards audited                  : {rows.Count}");
W($"  zero to zero                     : {clean}");
W($"  something still disagrees        : {dirty}");
W($"  no published chart               : {rows.Count(r => !r.HasChart && !r.OwnDesign)}");
W($"  no composer capture              : {rows.Count(r => !r.HasCapture && !r.OwnDesign)}   (the vendor ships no runtime file for these)");
W($"  our own design, not the vendor's : {rows.Count(r => r.OwnDesign)}");
W();
W($"chart single keys {Sum(rows, r => r.Chart),6}   same {Sum(rows, r => r.ChartOk),6}   different {Sum(rows, r => r.ChartWrong),5}   missing {Sum(rows, r => r.ChartMiss),5}");
W($"chart runs       {Sum(rows, r => r.Runs),6}   same {Sum(rows, r => r.RunsOk),6}   different {Sum(rows, r => r.RunsWrong),5}   missing {Sum(rows, r => r.RunsMiss),5}");
W($"composer keys    {Sum(rows, r => r.Comp),6}   same {Sum(rows, r => r.CompOk),6}   different {Sum(rows, r => r.CompWrong),5}   missing {Sum(rows, r => r.CompMiss),5}   the chart's own form {Sum(rows, r => r.CompForm)}");
W($"chart rows the composer overruled (the chart repeated itself): {Sum(rows, r => r.ChartTaken)}");
W($"Ctrl+Space keys  {Sum(rows, r => r.Third),6}   same {Sum(rows, r => r.ThirdOk),6}   different {Sum(rows, r => r.ThirdWrong),5}   missing {Sum(rows, r => r.ThirdMiss),5}   the chart's own form {Sum(rows, r => r.ThirdForm)}");
W();
W("where the composer answers in another form than the chart prints - the chart stands:");
foreach (var (how, n) in forms.OrderByDescending(f => f.Value))
    W($"  {n,6}  {Describe(how)}");
if (repeated > 0) W($"  {repeated,6}  the composer gives one answer to three or more keys at once");
W();
W("by script:");
W("  script  keyboards   0-0    chart w/m       runs w/m        composer w/m    Ctrl+Space w/m");
foreach (var g in rows.GroupBy(r => r.Script).OrderBy(g => g.Key))
    W($"  {g.Key,-7} {g.Count(),9} {g.Count(r => r.Clean),5}   {Sum(g, r => r.ChartWrong),6}/{Sum(g, r => r.ChartMiss),-8}{Sum(g, r => r.RunsWrong),7}/{Sum(g, r => r.RunsMiss),-8}{Sum(g, r => r.CompWrong),8}/{Sum(g, r => r.CompMiss),-9}{Sum(g, r => r.ThirdWrong),7}/{Sum(g, r => r.ThirdMiss)}");
W();

var show = all ? rows.OrderBy(r => r.Script).ThenBy(r => r.Layout).ToList()
               : rows.Where(r => !r.Clean).OrderByDescending(r => r.Problems).ToList();
W(all ? "every keyboard:" : "the keyboards that are not zero to zero:");
W("  script lang  layout           chart w/m       runs w/m        comp w/m        Ctrl+Space w/m");
foreach (var r in show)
    W($"  {r.Script,-6} {r.LangId,-5} {r.Layout,-16} {r.Chart,4} {r.ChartWrong,3}/{r.ChartMiss,-4} {r.Runs,5} {r.RunsWrong,3}/{r.RunsMiss,-4} {r.Comp,5} {r.CompWrong,3}/{r.CompMiss,-4} {r.Third,5} {r.ThirdWrong,3}/{r.ThirdMiss,-4}{(r.HasChart ? "" : " no-chart")}{(r.HasCapture ? "" : " no-capture")}");

if (detail.Count > 0)
{
    W();
    W($"what disagrees ({detail.Count} lines, first 150 shown):");
    foreach (string d in detail.Take(150)) W("  " + d);
}

if (report.Length > 0) File.WriteAllText(report, sb.ToString(), new UTF8Encoding(false));
return dirty == 0 ? 0 : 1;

void Note(Row r, string text) { if (r.Notes++ < 8) detail.Add($"{r.Script}/{r.Layout,-14} {text}"); }

static int Sum(IEnumerable<Row> rows, Func<Row, int> pick) => rows.Sum(pick);

static string Show(string keys) => keys;

static string Describe(Vendor.Difference how) => how switch
{
    Vendor.Difference.JoinerOnly => "the same text, the composer with a joiner in it",
    Vendor.Difference.HalfForm => "the composer answers with the half form (a virama after the letter)",
    Vendor.Difference.SubjoinedForm => "the composer answers with the subjoined form (a virama before it)",
    Vendor.Difference.MatraForVowel => "the chart prints the vowel, the composer answers with its matra",
    Vendor.Difference.VowelBearer => "the composer answers with the bearer the vowel is written on",
    Vendor.Difference.MarkOnCarrier => "a mark that cannot stand alone, answered on a carrier letter",
    Vendor.Difference.ChartOnCarrier => "the chart prints the mark on a carrier letter, the composer alone",
    Vendor.Difference.TwoPartVowel => "the composer spells the vowel in the two pieces it is drawn from",
    Vendor.Difference.OtherOrder => "the same characters, in the order the composer stores them",
    Vendor.Difference.PlainKey => "the composer hands the key back unchanged",
    _ => how.ToString(),
};

static bool Formatting(string s) => Vendor.Formatting(s);

static IEnumerable<(string Key, string Uni)> Pairs(JsonElement root, string property)
{
    if (!root.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.Array) yield break;
    foreach (var p in node.EnumerateArray())
        yield return (p.GetProperty("Key").GetString() ?? "", p.GetProperty("Uni").GetString() ?? "");
}

static IEnumerable<(string Key, string Value)> Map(JsonElement root, string property) =>
    Vendor.CaptureMap(root, property).Select(kv => (kv.Key, kv.Value));

sealed class Row
{
    public string Lang = "", LangId = "", Script = "", Layout = "";
    public bool HasChart, HasCapture, OwnDesign;
    public int Chart, ChartOk, ChartWrong, ChartMiss, ChartTaken;
    public int Runs, RunsOk, RunsWrong, RunsMiss;
    public int Comp, CompOk, CompWrong, CompMiss, CompForm;
    public int Third, ThirdOk, ThirdWrong, ThirdMiss, ThirdForm;
    public int Notes;
    public int Problems => ChartWrong + ChartMiss + RunsWrong + RunsMiss
                         + CompWrong + CompMiss + ThirdWrong + ThirdMiss;

    /// <summary>
    /// Nothing the vendor says disagrees with us. A keyboard the vendor does not
    /// have - one of our own designs - has nothing to be checked against, so it
    /// counts as clean once it has no chart and no capture to contradict.
    /// A keyboard with no runtime file of its own (the vendor never shipped one)
    /// is clean on its chart alone; the composer cannot be asked about it.
    /// </summary>
    public bool Clean => Problems == 0 && (OwnDesign || HasChart);
}
