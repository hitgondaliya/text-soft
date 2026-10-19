using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

/// <summary>One Unicode string and the bytes Shree's composer writes for it (preferred first among the accepted).</summary>
public sealed record Truth(string U, string Pref, string[] Acc)
{
    /// <summary>The composer's own preferred answer, when the table writes another (Pref); else null (tools/Shared/ShreeWordFinal.cs).</summary>
    public string? Composer { get; init; }
}

public static class Common
{
    public static readonly string Root = FindRoot();
    public static string MapDir => Path.Combine(Root, "assets", "config", "mappings");
    public static string Fixture => Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-ban7-truth.json");

    /// <summary>The full capture (composer rows + truth). Only needed to rebuild the fixture.</summary>
    public static string GroundTruth =>
        Environment.GetEnvironmentVariable("BAN7_GROUNDTRUTH") is { Length: > 0 } p ? p
        : Path.Combine(Path.GetTempPath(), "claude", "C--Users-harde-Desktop-Windows-Font-Software",
            "624671f5-ee6d-402b-b810-05b52f236677", "scratchpad", "shree7", "ban7-groundtruth.json");

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

    /// <summary>The truth rows of the test fixture: { "rows": [ [unicode, preferred, [other accepted...]], ... ] }.</summary>
    public static List<Truth> LoadTruth(string? from = null)
    {
        from ??= Fixture;
        var doc = JsonNode.Parse(File.ReadAllText(from, Encoding.UTF8))!;
        var list = new List<Truth>();
        foreach (var row in doc["rows"]!.AsArray())
        {
            var a = row!.AsArray();
            var acc = a.Count > 2 ? a[2]!.AsArray().Select(x => (string)x!).ToList() : new List<string>();
            string pref = (string)a[1]!;
            if (!acc.Contains(pref)) acc.Insert(0, pref);
            list.Add(new Truth((string)a[0]!, pref, acc.ToArray()));
        }
        return list;
    }

    /// <summary>
    /// The code point the fonts' cmaps hold for their code 9E: every SHREE-BAN face has ৱ at
    /// U+009E (and no U+017E, Windows-1252's 9E). But U+0080-U+009F are C1 control codes, and
    /// Word and RichEdit draw NOTHING for them, cmap or not (checked on this PC through Word's
    /// EMF output); only the app's preview, which draws by glyph index, shows them. So no table
    /// writes any of them: ৱ is not written in a Bangla font at all (it passes through as U+09F1,
    /// reported, and Word's font fallback draws it), and ব্ঢ ব্ড চ্ঞ (81 8D 90, C1 as well) are
    /// written letter + visible hasanta + letter (Decisions.cs 4). The reverse tables still read
    /// 9E (as U+009E, or U+017E as a .txt file decodes it) and 81 8D 90: a document typed with
    /// Shree's own composer has them.
    /// </summary>
    public const char Wa = '\u009E';

    /// <summary>A code point Word and RichEdit draw nothing for: U+0080-U+009F (see <see cref="Wa"/>).</summary>
    public static bool IsC1(char c) => c >= '\u0080' && c <= '\u009F';

    /// <summary>
    /// Legacy text as hex bytes (U+009E is 9E); a char outside the fonts' code page shows as U+XXXX
    /// - in a spelling, a character the table passes through and reports (ৱ in a Bangla font).
    /// </summary>
    public static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            if (c == Wa) { parts.Add("9E"); continue; }
            if (c == 'ž') { parts.Add("U+017E"); continue; }   // never written: not in any face's cmap
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    /// <summary>Hex bytes as the code points the fonts' cmaps hold (Windows-1252, but 9E = U+009E; a U+XXXX token is that character).</summary>
    public static string FromHex(string hex)
    {
        if (hex.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(h.StartsWith("U+") ? ((char)Convert.ToInt32(h[2..], 16)).ToString()
                                         : Cp1252.GetString(new[] { Convert.ToByte(h, 16) }).Replace('ž', Wa));
        return sb.ToString();
    }

    /// <summary>The characters a spelling passes through unconverted (its U+XXXX tokens): the converter must report exactly these.</summary>
    public static HashSet<string> Reported(string hex) =>
        hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(h => h.StartsWith("U+"))
           .Select(h => ((char)Convert.ToInt32(h[2..], 16)).ToString()).ToHashSet();

    /// <summary>True when a conversion reported exactly the characters the spelling passes through.</summary>
    public static bool ReportsAsSpelled(FontSoftware.Core.Conversion.ConversionResult r, string hex) =>
        Reported(hex).SetEquals(r.UnmappedCharacters);

    /// <summary>Hex tokens joined by single spaces, empty tokens dropped.</summary>
    public static string Norm(string hex) => string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries));

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

    /// <summary>A pair; one the target cannot show (tools/Shared/ShreeNonIndic.cs) is marked "unmapped".</summary>
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

    // ---------- Bengali characters ----------

    public static bool IsCons(char c) =>
        (c >= 'ক' && c <= 'হ' && c != '঩' && c != '঱' && !(c >= '঳' && c <= '঵'))
        || c is 'ড়' or 'ঢ়' or 'য়' or 'ৰ' or 'ৱ';
    public static bool IsVirama(char c) => c == '্';
    public static bool IsJoiner(char c) => c == '\u200C' || c == '\u200D';
    /// <summary>A dependent sign: matra, anusvara, candrabindu, visarga, nukta, virama, length mark, vocalic-l signs.</summary>
    public static bool IsMark(char c) =>
        (c >= 'ঁ' && c <= 'ঃ') || c == '়' || (c >= 'া' && c <= '্') || c == 'ৗ' || c == 'ৢ' || c == 'ৣ';
    public static bool IsVowel(char c) => (c >= 'অ' && c <= 'ঔ') || c == 'ৠ' || c == 'ৡ';

    /// <summary>
    /// Ways a Unicode string is not well formed Bengali (empty when it is): a vowel sign or
    /// hasanta that does not follow a letter (ি্ in রি্‌ক, a sign on a sign, on a vowel, on
    /// a space), a nukta not on a letter, ঁ ং ঃ after a hasanta, a joiner away from a hasanta.
    /// "Reads back to the same bytes" is no proof of text: রি্‌ক and র্‌কি are written alike.
    /// </summary>
    public static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        static bool Letter(char c) => IsCons(c) || c == '়';
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool vowelSign = (c >= 'া' && c <= 'ৌ') || c == 'ৗ' || c == 'ৢ' || c == 'ৣ';
            if (vowelSign && !Letter(p)) bad.Add($"vowel sign {c} at {i} after '{p}'");
            // A hasanta follows a letter, or the joiner of র‌্য / র‍্য (ra with the ya-phala).
            if (IsVirama(c) && !(Letter(p) || (IsJoiner(p) && i > 1 && u[i - 2] is 'র' or 'ৰ')))
                bad.Add($"hasanta at {i} after '{p}'");
            if (c == '়' && !(p >= 'ক' && p <= 'হ')) bad.Add($"nukta at {i} after '{p}'");
            if (c is 'ঁ' or 'ং' or 'ঃ' && (i == 0 || IsVirama(p) || IsJoiner(p) || !(Letter(p) || IsVowel(p) || IsMark(p) || p == 'ৎ')))
                bad.Add($"{c} at {i} after '{p}'");
            if (IsJoiner(c) && !(IsVirama(p) || (p is 'র' or 'ৰ' && i + 1 < u.Length && IsVirama(u[i + 1]))))
                bad.Add($"joiner U+{(int)c:X4} at {i} after '{p}'");
            // Text the fonts cannot be read as: Latin-1 letters, signs and marks leaking through, C1
            // control codes, the tables' own marks (noncharacters).
            if (c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') bad.Add($"Latin {c} at {i}");
            // ASCII letters too: every one of their codes is a Bengali glyph in these fonts (A 41 = ক), so
            // a letter read back is a code no pair took ('V' for the half ঙ 56), never English.
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') bad.Add($"Latin {c} at {i}");
            if (IsC1(c)) bad.Add($"C1 U+{(int)c:X4} at {i}");
            if (c >= '﷐' && c <= '﷯') bad.Add($"noncharacter U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>C = consonant, R = ra, H = virama, m = sign, V = vowel, J/N = joiners, o = other.</summary>
    public static string Shape(string u) => new(u.Select(c =>
        c == 'র' ? 'R' : IsCons(c) ? 'C' : IsVirama(c) ? 'H' : c == '\u200D' ? 'J' : c == '\u200C' ? 'N' : IsMark(c) ? 'm' :
        IsVowel(c) ? 'V' : 'o').ToArray());
}
