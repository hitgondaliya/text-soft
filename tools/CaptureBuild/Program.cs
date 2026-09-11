using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

// CaptureBuild — build a VERIFIED legacy<->Unicode converter table from a ground-
// truth capture. You convert tools/capture/<script>.unicode.txt in Shree-Lipi to a
// legacy encoding (Unicode -> legacy), save the output line-for-line, and this
// aligns the two files to derive the byte<->Unicode map. Because the sample lists
// one unit per line (letters, matras, half-forms, conjuncts, reph, digits, words),
// alignment is 1:1 and the storage order (pre-base i-matra, reph) is auto-detected.
//
//   dotnet run --project tools/CaptureBuild -- --unicode tools/capture/dev.unicode.txt \
//       --legacy <converted.txt> --source <enc-id> --target mangal --script dev --out assets/config/mappings/<enc>-to-unicode.json

var o = Args(args);
string uniFile = o.GetValueOrDefault("unicode", "");
string legFile = o.GetValueOrDefault("legacy", "");
string source = o.GetValueOrDefault("source", "legacy");
string target = o.GetValueOrDefault("target", "mangal");
string script = o.GetValueOrDefault("script", "dev");
string outFile = o.GetValueOrDefault("out", $"{source}-to-unicode.json");
if (uniFile == "" || legFile == "") { Console.Error.WriteLine("need --unicode and --legacy"); return 1; }

string[] uni = File.ReadAllLines(uniFile);
string[] leg = File.ReadAllLines(legFile);
// pair up non-empty lines
var U = new List<string>(); var L = new List<string>();
for (int i = 0; i < Math.Max(uni.Length, leg.Length); i++)
{
    string a = i < uni.Length ? uni[i].Trim() : "";
    string b = i < leg.Length ? leg[i].TrimEnd() : "";
    if (a.Length == 0 && b.Length == 0) continue;
    U.Add(a); L.Add(b);
}
bool misaligned = U.Count != L.Count;
for (int i = 0; !misaligned && i < U.Count; i++)
    if ((U[i].Length == 0) != (L[i].Length == 0)) misaligned = true;
if (misaligned)
{
    Console.Error.WriteLine($"alignment mismatch: {U.Count} unicode vs {L.Count} legacy non-empty lines. " +
                            "Make sure the converted file preserved every line (no lines added/removed).");
    return 1;
}

// unit(unicode) -> legacy string
var map = new Dictionary<string, string>(StringComparer.Ordinal);
for (int i = 0; i < U.Count; i++) if (!map.ContainsKey(U[i])) map[U[i]] = L[i];

string Leg(string u) => map.TryGetValue(u, out var v) ? v : "";

// Build ATOMIC legacy->unicode pairs only (like MapGen): base letters, matras
// decomposed from क+matra rows, and conjunct/half-form clusters. Whole words and
// i-matra syllables are NOT added as pairs (they would fight the reorder rule);
// words are used for validation only.
const char Virama = '्', Nukta = '़', IMatra = 'ि', Ka = 'क';
static bool IsCombining(char c) { int o = c & 0x7F; return c >= 'ऀ' && c <= '෿' && (o is 0x01 or 0x02 or 0x03 or 0x3C or 0x4D or 0x62 or 0x63 || (o >= 0x3E && o <= 0x4C) || (o >= 0x51 && o <= 0x57)); }

var b2u = new Dictionary<string, string>(StringComparer.Ordinal);
void Add(string legacy, string unicode)
{ if (legacy.Length > 0 && unicode.Length > 0 && !b2u.ContainsKey(legacy)) b2u[legacy] = unicode; }

string kaLeg = Leg("क");

foreach (var (u, l) in map)
{
    if (u.Length == 0) continue;
    // base letter: single codepoint, or consonant+nukta
    if (u.Length == 1 || (u.Length == 2 && u[1] == Nukta)) { Add(l, u); continue; }
    // matra decomposition: क + combining mark(s). The matra bytes may follow क
    // (post-base) OR precede it (pre-base, e.g. the i-matra) — handle both.
    if (u[0] == Ka && u.Skip(1).All(IsCombining) && kaLeg.Length > 0 && l.Length > kaLeg.Length)
    {
        string matra = u[1..];
        string? matraLeg = l.StartsWith(kaLeg, StringComparison.Ordinal) ? l[kaLeg.Length..]
                         : l.EndsWith(kaLeg, StringComparison.Ordinal) ? l[..^kaLeg.Length]
                         : null;
        if (matraLeg is { Length: >= 1 }) Add(matraLeg, matra);
        continue;
    }
    // conjunct / half-form: contains virama, no i-matra, not a reph cluster
    if (u.Contains(Virama) && !u.Contains(IMatra) && !u.StartsWith("र्", StringComparison.Ordinal))
        Add(l, u);
    // everything else (whole words, reph rows, i-matra syllables) -> validation/analysis only
}

// --- detect pre-base i-matra: compare "कि" to "क" ---
bool iMatraPreBase = false;
string kiLeg = Leg("कि");
if (kaLeg.Length > 0 && kiLeg.Length > kaLeg.Length && kiLeg.EndsWith(kaLeg, StringComparison.Ordinal))
    iMatraPreBase = true;   // i-matra bytes come BEFORE the consonant in storage

// --- detect reph position: compare "र्क" to "क" ---
string rkaLeg = Leg("र्क");
string rephHint = rkaLeg.Length > 0 && kaLeg.Length > 0
    ? (rkaLeg.StartsWith(kaLeg, StringComparison.Ordinal) ? "reph-after" :
       rkaLeg.EndsWith(kaLeg, StringComparison.Ordinal) ? "reph-before" : "reph-other")
    : "n/a";

var pre = new List<RewriteRule>();
var post = new List<RewriteRule>();
if (iMatraPreBase)
    post.Add(new RewriteRule
    {
        Pattern = "ि((?:[क-ह]़?्)*[क-ह]़?)",
        Replace = "$1ि",
        Note = "Auto-detected: i-matra stored pre-base in this font; move it after the cluster."
    });

var fwd = new MappingTable
{
    Id = System.IO.Path.GetFileNameWithoutExtension(outFile),
    Name = $"{source} -> Unicode (captured)",
    SourceFontId = source, TargetFontId = target,
    SourceType = FontType.Legacy, TargetType = FontType.Unicode,
    Pairs = b2u.Select(kv => new MappingPair { From = kv.Key, To = kv.Value })
               .OrderByDescending(p => p.From.Length).ToList(),
    PostRules = post
};

WriteTable(fwd, outFile);

// reverse
var u2b = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var kv in b2u) if (!u2b.ContainsKey(kv.Value)) u2b[kv.Value] = kv.Key;
var revPre = new List<RewriteRule>();
if (iMatraPreBase)
    revPre.Add(new RewriteRule
    {
        Pattern = "((?:[क-ह]़?्)*[क-ह]़?)ि",
        Replace = "ि$1",
        Note = "Reverse: put i-matra back before the cluster (Unicode post-base -> legacy pre-base)."
    });
string revFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outFile)!, $"unicode-to-{source}.json");
var rev = new MappingTable
{
    Id = System.IO.Path.GetFileNameWithoutExtension(revFile),
    Name = $"Unicode -> {source} (captured)",
    SourceFontId = target, TargetFontId = source,
    SourceType = FontType.Unicode, TargetType = FontType.Legacy,
    PreRules = revPre,
    Pairs = u2b.Select(kv => new MappingPair { From = kv.Key, To = kv.Value })
               .OrderByDescending(p => p.From.Length).ToList()
};
WriteTable(rev, revFile);

// validate on the whole-word lines (multi-syllable entries)
var conv = new FontConverter(fwd);
int ok = 0, bad = 0;
Console.WriteLine($"\ni-matra pre-base: {iMatraPreBase}   reph: {rephHint}   pairs: {b2u.Count}");
Console.WriteLine("word check (legacy -> unicode):");
foreach (var (u, l) in map)
{
    if (u.Length < 4) continue;   // words only
    string got = conv.ConvertText(l);
    bool good = got == u;
    if (good) ok++; else { bad++; Console.WriteLine($"  MISMATCH  {u}  (legacy {l}) -> {got}"); }
}
Console.WriteLine($"words: {ok} ok, {bad} mismatch");
Console.WriteLine($"wrote {outFile} and {revFile}");
return 0;

static void WriteTable(MappingTable t, string path)
{
    var opts = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
    File.WriteAllText(path, JsonSerializer.Serialize(t, opts));
}

static Dictionary<string, string> Args(string[] a)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < a.Length; i++)
        if (a[i].StartsWith("--") && i + 1 < a.Length) d[a[i][2..]] = a[++i];
    return d;
}
