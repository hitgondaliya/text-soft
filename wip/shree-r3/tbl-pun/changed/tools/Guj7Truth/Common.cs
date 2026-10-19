using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

/// <summary>One Unicode string and what Shree's 7 composer writes for it.</summary>
public sealed record Truth(string U, string Pref, string[] Acc)
{
    /// <summary>The composer's own preferred answer, when the table writes another (Pref); else null.</summary>
    public string? Composer { get; init; }
}

public static class Common
{
    public static readonly string Root = FindRoot();
    public static string MapDir => Path.Combine(Root, "assets", "config", "mappings");
    public static string Fixture => Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-guj7-truth.json");

    /// <summary>The full capture (composer rows + truth). Only needed to rebuild the fixture.</summary>
    public static string GroundTruth =>
        Environment.GetEnvironmentVariable("GUJ7_GROUNDTRUTH") is { Length: > 0 } p ? p
        : Path.Combine(Path.GetTempPath(), "claude", "C--Users-harde-Desktop-Windows-Font-Software",
            "624671f5-ee6d-402b-b810-05b52f236677", "scratchpad", "shree7", "guj7-groundtruth.json");

    public static readonly Encoding Cp1252;
    static Common()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) return d.FullName;
        throw new InvalidOperationException("Run from inside the repository.");
    }

    public static readonly JsonSerializerOptions Writer = new() { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    /// <summary>
    /// A row the table writes otherwise than the composer: the ru/ruu ligatures and the output
    /// policy's decisions (see Policy), and a word-final virama, which the table writes with the
    /// halant (see WordFinal); null for every other row.
    /// </summary>
    public static (string Pref, string Why)? Adjust(Truth t, IReadOnlyDictionary<string, Truth> raw) =>
        Policy.Adjust(t) ?? WordFinal.Spell(t, raw, '્', '઼', "B9", new[] { "24", "3E" });

    /// <summary>
    /// The truth the tables are built and checked against: the composer's rows (from the full
    /// capture when given, else from the test fixture), with the rows the table writes
    /// otherwise as it writes them - <paramref name="raw"/> leaves them as the composer wrote them.
    /// </summary>
    public static List<Truth> LoadTruth(string? from = null, bool raw = false)
    {
        var list = LoadRows(from);
        return raw ? list : WordFinal.Adjust(list, Adjust);
    }

    static List<Truth> LoadRows(string? from)
    {
        from ??= File.Exists(Fixture) ? Fixture : GroundTruth;
        var doc = JsonNode.Parse(File.ReadAllText(from, Encoding.UTF8))!;
        var list = new List<Truth>();
        if (doc is JsonObject o && o["truth"] is JsonObject truth)
        {
            foreach (var (u, node) in truth)
                list.Add(new Truth(u, (string)node!["preferred"]!, node["accepted"]!.AsArray().Select(x => (string)x!).ToArray()));
        }
        else
        {
            // Fixture: { "rows": [ [unicode, preferred, [accepted...]], ... ] }
            foreach (var row in doc["rows"]!.AsArray())
            {
                var a = row!.AsArray();
                var acc = a.Count > 2 ? a[2]!.AsArray().Select(x => (string)x!).ToList() : new List<string>();
                string pref = (string)a[1]!;
                if (!acc.Contains(pref)) acc.Insert(0, pref);
                list.Add(new Truth((string)a[0]!, pref, acc.ToArray()));
            }
        }
        return list;
    }

    /// <summary>Legacy text (Windows-1252 chars) as hex bytes; a char outside 1252 shows as U+XXXX.</summary>
    public static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    /// <summary>Hex bytes as Windows-1252 text; a U+XXXX token (as <see cref="Hex"/> writes a character outside 1252) is that character.</summary>
    public static string FromHex(string hex)
    {
        if (hex.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(h.StartsWith("U+") ? ((char)Convert.ToInt32(h[2..], 16)).ToString() : Cp1252.GetString(new[] { Convert.ToByte(h, 16) }));
        return sb.ToString();
    }

    public static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    public static MappingTable LoadTable(string fileOrName)
    {
        string path = File.Exists(fileOrName) ? fileOrName : Path.Combine(MapDir, fileOrName);
        return JsonSerializer.Deserialize<MappingTable>(File.ReadAllText(path), ConfigLoader.JsonOptions)!;
    }

    public static MappingTable Table(string id, string name, string src, string tgt, FontType st, FontType tt,
        IEnumerable<RewriteRule> pre, IEnumerable<(string From, string To)> pairs, IEnumerable<RewriteRule>? post = null) => new()
    {
        Id = id, Name = name, SourceFontId = src, TargetFontId = tgt, SourceType = st, TargetType = tt,
        PreRules = pre.ToList(),
        Pairs = pairs.Select(p => new MappingPair { From = p.From, To = p.To }).ToList(),
        PostRules = (post ?? Array.Empty<RewriteRule>()).ToList(),
    };

    /// <summary>Write a table in the house JSON style (the writer GapFill uses).</summary>
    public static void Save(MappingTable t, string path)
    {
        var o = new JsonObject
        {
            ["id"] = t.Id,
            ["name"] = t.Name,
            ["sourceFontId"] = t.SourceFontId,
            ["targetFontId"] = t.TargetFontId,
            ["sourceType"] = t.SourceType == FontType.Unicode ? "unicode" : "legacy",
            ["targetType"] = t.TargetType == FontType.Unicode ? "unicode" : "legacy",
            ["preRules"] = Rules(t.PreRules),
            ["pairs"] = new JsonArray(t.Pairs.Select(Pair).ToArray()),
            ["postRules"] = Rules(t.PostRules),
        };
        File.WriteAllText(path, o.ToJsonString(Writer));
    }

    /// <summary>A pair; one the target cannot show (see NonIndic) is marked "unmapped".</summary>
    static JsonNode Pair(MappingPair p)
    {
        var n = new JsonObject { ["from"] = p.From, ["to"] = p.To };
        if (p.Unmapped) n["unmapped"] = true;
        return n;
    }

    static JsonArray Rules(List<RewriteRule> rules) => new(rules.Select(r =>
    {
        var n = new JsonObject { ["pattern"] = r.Pattern, ["replace"] = r.Replace };
        if (r.Note is not null) n["note"] = r.Note;
        return (JsonNode)n;
    }).ToArray());

    public static bool IsCons(char c) => c >= 'ક' && c <= 'હ';
    public static bool IsVirama(char c) => c == '્';
    public static bool IsJoiner(char c) => c == '‌' || c == '‍';
    /// <summary>A dependent sign: matra, anusvara, candrabindu, visarga, nukta, virama, vocalic-l signs.</summary>
    public static bool IsMark(char c) => (c >= 'ઁ' && c <= 'ઃ') || c == '઼' || (c >= 'ા' && c <= '્') || c == 'ૢ' || c == 'ૣ';
}
