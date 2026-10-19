using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;

// measure <groundtruth.json> <config root> <out dir> [forwardEncoding=shree-pun7] [unicodeId=raavi]
// The REAL FontConverter (FontSoftware.Core built from the snapshot copy) with the shipped tables
// unicode-to-shree-pun7.json / shree-pun7-to-unicode.json, against Shree's composer truth.
string gtPath = args[0], configRoot = args[1], outDir = args[2];
string enc = args.Length > 3 ? args[3] : "shree-pun7";
string uni = args.Length > 4 ? args[4] : "raavi";
Directory.CreateDirectory(outDir);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

var assets = new ConfigLoader(configRoot).Load();
var fwdTable = assets.FindMapping(uni, enc) ?? throw new Exception("no forward table " + uni + "->" + enc);
var revTable = assets.FindMapping(enc, uni) ?? throw new Exception("no reverse table");
var fwd = new FontConverter(fwdTable);
var rev = new FontConverter(revTable);
var toLegacy = assets.CreateConverterToLegacy(enc);

using var doc = JsonDocument.Parse(File.ReadAllText(gtPath, Encoding.UTF8));
var truth = new List<(string U, string Pref, string[] Acc, string[] Alt)>();
foreach (var p in doc.RootElement.GetProperty("truth").EnumerateObject())
    truth.Add((p.Name, p.Value.GetProperty("preferred").GetString()!, p.Value.GetProperty("accepted").EnumerateArray().Select(x => x.GetString()!).ToArray(), p.Value.GetProperty("alternatives").EnumerateArray().Select(x => x.GetString()!).ToArray()));
var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
// all spellings Shree writes for a byte string -> the Unicode strings it writes them for
var bytesToU = new Dictionary<string, List<string>>(StringComparer.Ordinal);
foreach (var t in truth) foreach (var a in t.Acc) { if (!bytesToU.TryGetValue(a, out var l)) bytesToU[a] = l = new(); l.Add(t.U); }

string Hex(string s)
{
    var sb = new StringBuilder();
    foreach (char c in s)
    {
        string h;
        try { h = string.Join(" ", cp1252.GetBytes(c.ToString()).Select(b => b.ToString("X2"))); }
        catch { h = "U+" + ((int)c).ToString("X4"); }
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(h);
    }
    return sb.ToString();
}
bool Is1252(char c) { try { cp1252.GetBytes(c.ToString()); return true; } catch { return false; } }
string FromHex(string h) => h.Length == 0 ? "" : cp1252.GetString(h.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray());
string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));
string[] Joins = { "ਅਾ>ਆ", "ਅੈ>ਐ", "ਅੌ>ਔ", "ੲਿ>ਇ", "ੲੀ>ਈ", "ੲੇ>ਏ", "ੳੁ>ਉ", "ੳੂ>ਊ" };
string Norm(string s) { foreach (var j in Joins) { var p = j.Split('>'); s = s.Replace(p[0], p[1]); } return s.Normalize(NormalizationForm.FormD); }

string Category(string u)
{
    if (Regex.IsMatch(u, "੍[ਰਹਵਯ]")) return "subjoined (pairin) ੍ਰ ੍ਹ ੍ਵ ੍ਯ";
    if (Regex.IsMatch(u, "੍[‌‍]?$")) return "word-final virama (C੍)";
    if (u.Contains('੍')) return "C੍C cluster (no subjoined form)";
    if (Regex.IsMatch(u, "[ਲ਼ਸ਼ਖ਼ਗ਼ਜ਼ਫ਼਼]")) return "nukta letter";
    if (u.Contains('ੴ')) return "ੴ";
    if (u.Contains('ਁ')) return "adak bindi ਁ";
    if (u.Contains('ੱ')) return "addak ੱ";
    if (Regex.IsMatch(u, "[ਂੰ]")) return "bindi/tippi";
    if (u.Contains('ਃ')) return "visarga ਃ";
    if (Regex.IsMatch(u, "^[ਕ-ਹੜ][ਾ-ੌ]$")) return "consonant + vowel sign";
    if (Regex.IsMatch(u, "^[ਕ-ਹੜ]$")) return "consonant";
    if (Regex.IsMatch(u, "[ਅ-ਔੲੳ]")) return "independent vowel / vowel carrier";
    if (Regex.IsMatch(u, "^[ਾ-ੌ]+$")) return "vowel sign alone";
    if (Regex.IsMatch(u, "[੦-੯]")) return "digit";
    if (Regex.IsMatch(u, "[ਕ-ਹ]")) return "other letter string";
    return "punctuation / symbol";
}
string FailType(string got, string want, string gotText)
{
    if (gotText.Any(c => c >= '਀' && c <= '੿')) return "Gurmukhi passed through unmapped";
    if (gotText.Any(c => c > 'ÿ' && !Is1252(c))) return "non-1252 char passed through";
    var g = got.Split(' ', StringSplitOptions.RemoveEmptyEntries); var w = want.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (g.OrderBy(x => x).SequenceEqual(w.OrderBy(x => x))) return "same bytes, other order";
    if (g.Length < w.Length && w.Except(g).Any()) return "bytes missing (" + string.Join(" ", w.Except(g)) + ")";
    if (g.Length > w.Length && g.Except(w).Any()) return "extra bytes (" + string.Join(" ", g.Except(w)) + ")";
    return "other bytes (ours has " + string.Join(" ", g.Except(w)) + " / Shree has " + string.Join(" ", w.Except(g)) + ")";
}

// ---------------- words mode: common words, expected = Shree's syllables concatenated ----------------
string? wordsFile = Environment.GetEnvironmentVariable("WORDS");
if (wordsFile != null)
{
    var keys = truth.Select(t => t.U).OrderByDescending(k => k.Length).ToList();
    int maxK = keys.Max(k => k.Length);
    string Pre(string s) => s.Replace("ਸ਼", "ਸ਼").Replace("ਖ਼", "ਖ਼").Replace("ਗ਼", "ਗ਼").Replace("ਜ਼", "ਜ਼").Replace("ਫ਼", "ਫ਼").Replace("ਲ਼", "ਲ਼");
    var sbw = new StringBuilder("word\tcodepoints\tours\tshree_by_syllables\tsyllables\tok\n");
    int nW = 0, okW = 0, unc = 0; var bad = new List<string>();
    foreach (var raw in File.ReadAllLines(wordsFile, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0))
    {
        string w = Pre(raw);
        var parts = new List<string>(); var bytes = new List<string>(); int i = 0; bool ok = true;
        while (i < w.Length)
        {
            if (w[i] == ' ') { parts.Add(" "); bytes.Add("20"); i++; continue; }
            string? hit = null;
            for (int L = Math.Min(maxK, w.Length - i); L > 0 && hit == null; L--) { var s = w.Substring(i, L); if (byU.ContainsKey(s)) hit = s; }
            if (hit == null) { ok = false; break; }
            parts.Add(hit); bytes.Add(byU[hit].Pref); i += hit.Length;
        }
        if (!ok) { unc++; sbw.Append($"{raw}\t{Cps(raw)}\t\t(not coverable by truth syllables)\t\t\n"); continue; }
        nW++;
        string want = string.Join(" ", bytes.Where(b => b.Length > 0));
        string got = Hex(fwd.ConvertText(raw));
        bool good = got == want;
        if (good) okW++; else bad.Add($"{raw} [{Cps(raw)}]: ours {got} | Shree {want} | syllables {string.Join("+", parts)}");
        sbw.Append($"{raw}\t{Cps(raw)}\t{got}\t{want}\t{string.Join("+", parts)}\t{(good ? 1 : 0)}\n");
    }
    File.WriteAllText(Path.Combine(outDir, "common-words.tsv"), sbw.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"common words: {okW}/{nW} exact (Shree's syllable spellings concatenated), {unc} not coverable");
    foreach (var b in bad) Console.WriteLine("  " + b);
    return;
}

// ---------------- forward ----------------
var cats = new SortedDictionary<string, int[]>(StringComparer.Ordinal); // total, exact, accepted
var failTypes = new Dictionary<string, int>();
var catEx = new Dictionary<string, List<string>>();
var fails = new StringBuilder("unicode\tcodepoints\tcategory\tours\tshree_preferred\tshree_accepted\tfail_type\n");
int exact = 0, accepted = 0, unmappedRows = 0, alt = 0, nfcDiffer = 0, nfcExact = 0, nfcAccepted = 0;
var unmappedChars = new Dictionary<string, int>();
foreach (var t in truth)
{
    var r = fwd.Convert(t.U);
    string got = Hex(r.Text);
    string cat = Category(t.U);
    if (!cats.TryGetValue(cat, out var c)) cats[cat] = c = new int[3];
    c[0]++;
    bool ex = got == t.Pref, acc = t.Acc.Contains(got);
    if (ex) { exact++; c[1]++; }
    if (acc) { accepted++; c[2]++; }
    if (r.UnmappedCharacters.Count > 0) { unmappedRows++; foreach (var u in r.UnmappedCharacters) unmappedChars[u] = unmappedChars.GetValueOrDefault(u) + 1; }
    if (!acc)
    {
        if (t.Alt.Contains(got)) alt++;
        string ft = FailType(got, t.Pref, r.Text);
        string ftKey = Regex.Replace(ft, @"\(.*\)", "").Trim();
        failTypes[cat + " | " + ftKey] = failTypes.GetValueOrDefault(cat + " | " + ftKey) + 1;
        if (!catEx.TryGetValue(cat, out var l)) catEx[cat] = l = new();
        if (l.Count < 14) l.Add($"{t.U} [{Cps(t.U)}]: ours {got} | Shree {t.Pref}{(t.Acc.Length > 1 ? " (also " + string.Join(", ", t.Acc.Skip(1)) + ")" : "")} | {ft}");
        fails.Append($"{t.U}\t{Cps(t.U)}\t{cat}\t{got}\t{t.Pref}\t{string.Join(",", t.Acc)}\t{ft}\n");
    }
    // the same text in NFC (nukta letters decomposed): real documents hold either form
    string nfc = t.U.Normalize(NormalizationForm.FormC);
    if (nfc != t.U)
    {
        nfcDiffer++;
        string g2 = Hex(fwd.Convert(nfc).Text);
        if (g2 == t.Pref) nfcExact++;
        if (t.Acc.Contains(g2)) nfcAccepted++;
    }
}
File.WriteAllText(Path.Combine(outDir, "forward-failures.tsv"), fails.ToString(), new UTF8Encoding(false));

// words: syllables joined, the converter must not reach across them
var rnd = new Random(2026);
var syl = truth.Where(t => Regex.IsMatch(t.U, "^[ਅ-ਹੜੲੳ]") && !Regex.IsMatch(t.U, "੍[‌‍]?$") && !t.U.Contains('ੴ')).ToList();
int words = 0, wordsExact = 0, wordsAccepted = 0;
var wordEx = new List<string>();
for (int i = 0; i < 20000; i++)
{
    int n = 2 + rnd.Next(3);
    var parts = Enumerable.Range(0, n).Select(_ => syl[rnd.Next(syl.Count)]).ToList();
    string u = string.Concat(parts.Select(p => p.U));
    string want = string.Join(" ", parts.Select(p => p.Pref));
    string got = Hex(fwd.ConvertText(u));
    words++;
    bool alone = got == string.Join(" ", parts.Select(p => Hex(fwd.ConvertText(p.U))));
    if (got == want) wordsExact++;
    // accepted: every syllable's own output is accepted for it, and the word is just their concatenation
    if (alone && parts.All(p => p.Acc.Contains(Hex(fwd.ConvertText(p.U))))) wordsAccepted++;
    else if (wordEx.Count < 12 && !alone) wordEx.Add($"{u} [{Cps(u)}]: ours {got} | syllables alone {string.Join(" ", parts.Select(p => Hex(fwd.ConvertText(p.U))))} | Shree {want}");
}

// ---------------- reverse ----------------
int rTotal = 0, rExact = 0, rSame = 0, rUnmapped = 0, aTotal = 0, aSame = 0, rInv = 0, aInv = 0;
var revFail = new StringBuilder("bytes\tshree_unicode\tours\tours_codepoints\n");
var revEx = new List<string>();
var revUnmapped = new Dictionary<string, int>();
var revCats = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
bool SameText(string got, string u, string bytes)
{
    if (got == u || Norm(got) == Norm(u)) return true;
    // Shree writes these bytes for the text we read back too
    return bytesToU.TryGetValue(bytes, out var l) && l.Any(x => Norm(x) == Norm(got));
}
// the same text up to a virama the bytes cannot show: the link spelling (ਕ੍ਤ = 48 57) reads back as ਕਤ; ZWJ/ZWNJ have no glyph
string Strip(string s) => Regex.Replace(Norm(s), "੍(?![ਰਹਵਯ])|[‌‍]", "");
bool SameUpToInvisible(string got, string u, string bytes) => SameText(got, u, bytes) || (!bytes.Split(' ').Contains("B5") && Strip(got) == Strip(u)) || Regex.Replace(Norm(got), "[‌‍]", "") == Regex.Replace(Norm(u), "[‌‍]", "");
foreach (var t in truth)
{
    rTotal++;
    var r = rev.Convert(FromHex(t.Pref));
    string cat = Category(t.U);
    if (!revCats.TryGetValue(cat, out var c)) revCats[cat] = c = new int[3];
    c[0]++;
    if (r.Text == t.U) { rExact++; c[1]++; }
    bool same = SameText(r.Text, t.U, t.Pref);
    if (same) { rSame++; c[2]++; }
    if (r.UnmappedCharacters.Count > 0) { rUnmapped++; foreach (var u in r.UnmappedCharacters) revUnmapped[Hex(u)] = revUnmapped.GetValueOrDefault(Hex(u)) + 1; }
    if (!same)
    {
        revFail.Append($"{t.Pref}\t{t.U}\t{r.Text}\t{Cps(r.Text)}\n");
        if (revEx.Count < 40) revEx.Add($"{t.Pref}: Shree {t.U} [{Cps(t.U)}] | ours {r.Text} [{Cps(r.Text)}]");
    }
    if (SameUpToInvisible(r.Text, t.U, t.Pref)) rInv++;
    foreach (var a in t.Acc)
    {
        aTotal++;
        string back = rev.ConvertText(FromHex(a));
        if (SameText(back, t.U, a)) aSame++;
        if (SameUpToInvisible(back, t.U, a)) aInv++;
    }
}
File.WriteAllText(Path.Combine(outDir, "reverse-failures.tsv"), revFail.ToString(), new UTF8Encoding(false));

// round trip of the tables themselves (what a green test would show), for contrast
int rt = 0, rtOk = 0;
foreach (var t in truth) { rt++; if (Norm(rev.ConvertText(fwd.ConvertText(t.U))) == Norm(t.U)) rtOk++; }

string Pct(int a, int b) => b == 0 ? "-" : (100.0 * a / b).ToString("0.00") + "%";
var report = new Dictionary<string, object?>
{
    ["tables"] = new { forward = fwdTable.Id + " (" + fwdTable.Pairs.Count + " pairs, " + fwdTable.PreRules.Count + " preRules, " + fwdTable.PostRules.Count + " postRules)", reverse = revTable.Id + " (" + revTable.Pairs.Count + " pairs)", configRoot, converterToLegacySameTable = toLegacy?.Table.Id },
    ["truthStrings"] = truth.Count,
    ["forward"] = new
    {
        exact = $"{exact}/{truth.Count} = {Pct(exact, truth.Count)}",
        accepted = $"{accepted}/{truth.Count} = {Pct(accepted, truth.Count)}",
        failing = truth.Count - accepted,
        failingButShreeAlternative = alt,
        rowsReportingUnmapped = unmappedRows,
        unmappedCharacters = unmappedChars.OrderByDescending(k => k.Value).ToDictionary(k => $"{k.Key} U+{Cps(k.Key)}", k => k.Value),
        nfcForms = new { stringsWhoseNfcDiffers = nfcDiffer, exact = nfcExact, accepted = nfcAccepted },
        byCategory = cats.ToDictionary(k => k.Key, k => $"{k.Value[0]} strings: exact {k.Value[1]} ({Pct(k.Value[1], k.Value[0])}), accepted {k.Value[2]} ({Pct(k.Value[2], k.Value[0])})"),
        failureTypes = failTypes.OrderByDescending(k => k.Value).ToDictionary(k => k.Key, k => k.Value),
        examples = catEx,
        words = new { joined = words, exact = $"{wordsExact} ({Pct(wordsExact, words)})", everySyllableAcceptedAndNoCrossSyllableEffect = $"{wordsAccepted} ({Pct(wordsAccepted, words)})", crossSyllableExamples = wordEx },
    },
    ["reverse"] = new
    {
        preferredSpellings = rTotal,
        exact = $"{rExact}/{rTotal} = {Pct(rExact, rTotal)}",
        sameText = $"{rSame}/{rTotal} = {Pct(rSame, rTotal)}",
        sameTextUpToAViramaOrJoinerTheBytesCannotShow = $"{rInv}/{rTotal} = {Pct(rInv, rTotal)}",
        allAcceptedSpellingsSameText = $"{aSame}/{aTotal} = {Pct(aSame, aTotal)}",
        allAcceptedSpellingsSameTextUpToInvisibleVirama = $"{aInv}/{aTotal} = {Pct(aInv, aTotal)}",
        rowsReportingUnmapped = rUnmapped,
        unmappedBytes = revUnmapped.OrderByDescending(k => k.Value).ToDictionary(k => k.Key, k => k.Value),
        byCategory = revCats.ToDictionary(k => k.Key, k => $"{k.Value[0]}: exact {k.Value[1]} ({Pct(k.Value[1], k.Value[0])}), same text {k.Value[2]} ({Pct(k.Value[2], k.Value[0])})"),
        examples = revEx,
    },
    ["tablesOwnRoundTrip"] = $"Unicode -> ours -> ours back: {rtOk}/{rt} = {Pct(rtOk, rt)} (what a round-trip test would show)",
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
File.WriteAllText(Path.Combine(outDir, "measure.json"), json, new UTF8Encoding(false));
Console.WriteLine(json);

