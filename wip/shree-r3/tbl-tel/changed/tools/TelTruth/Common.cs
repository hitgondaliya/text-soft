using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

/// <summary>One Unicode string and the bytes the tables must write for it (preferred first among the accepted).</summary>
public sealed record Truth(string U, string Pref, string[] Acc)
{
    /// <summary>
    /// Shree's composer's own preferred answer, when the table writes another (Pref) - a documented exception
    /// (Capture.Exceptions, tools/Shared/ShreeWordFinal.cs); else null.
    /// </summary>
    public string? Composer { get; init; }

    /// <summary>Which documented exception changed Pref (null when Pref is the composer's own).</summary>
    public string? Why { get; init; }
}

public static class Common
{
    public static readonly string Root = FindRoot();
    public static string MapDir => Path.Combine(Root, "assets", "config", "mappings");
    public static string Fixture => Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-tel7-truth.json");

    /// <summary>The full capture (composer rows + truth). Only needed to rebuild the fixture and to read every spelling back.</summary>
    public static string GroundTruth =>
        Environment.GetEnvironmentVariable("TEL7_GROUNDTRUTH") is { Length: > 0 } p ? p
        : Path.Combine(Path.GetTempPath(), "claude", "C--Users-harde-Desktop-Windows-Font-Software",
            "7673923b-96b4-4287-87dd-a8e7c6533eca", "scratchpad", "r3", "cap-tel", "tel7-groundtruth.json");

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
    /// The truth rows of the test fixture: { "rows": [ [unicode, preferred, [other accepted...], composer?, why?], ... ] }.
    /// A row whose preferred spelling is a documented exception carries the composer's own spelling (accepted too) and why.
    /// </summary>
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
            list.Add(new Truth((string)a[0]!, pref, acc.ToArray())
            {
                Composer = a.Count > 3 ? (string?)a[3] : null,
                Why = a.Count > 4 ? (string?)a[4] : null,
            });
        }
        return list;
    }

    /// <summary>
    /// Legacy text as hex bytes. Every code a SHREE-TEL7 / SHREE-TEL / Shree-Tel7 face maps is a Windows-1252 byte (the
    /// fonts map 90 at U+0090); a char outside the code page shows as U+XXXX - in a spelling, a character the table passes
    /// through and reports.
    /// </summary>
    public static string Hex(string legacy)
    {
        var parts = new List<string>(legacy.Length);
        foreach (char c in legacy)
        {
            if (c == '\u0090') { parts.Add("90"); continue; }
            try { parts.Add(Cp1252.GetBytes(c.ToString())[0].ToString("X2")); }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    /// <summary>Hex bytes as the code points the fonts' cmaps hold (Windows-1252; 90 = U+0090; a U+XXXX token is that character).</summary>
    public static string FromHex(string hex)
    {
        if (hex.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(h.StartsWith("U+") ? ((char)Convert.ToInt32(h[2..], 16)).ToString()
                      : h == "90" ? "\u0090"
                      : Cp1252.GetString(new[] { Convert.ToByte(h, 16) }));
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

    // ---------- Telugu characters ----------

    /// <summary>
    /// A consonant the fonts draw: క-హ (U+0C15-U+0C39) but U+0C29 (unassigned) and ఴ U+0C34 (no glyph in any face).
    /// ౘ ౙ ౚ have no glyph either: they pass through (Learner.NoGlyph) and are letters only for <see cref="Malformed"/>.
    /// </summary>
    public static bool IsCons(char c) => c >= 'క' && c <= 'హ' && c != '఩' && c != 'ఴ';
    /// <summary>Any Telugu consonant letter, the ones with no glyph included.</summary>
    public static bool IsLetter(char c) => IsCons(c) || c is 'ఴ' or 'ౘ' or 'ౙ' or 'ౚ' or 'ౝ';
    public static bool IsVirama(char c) => c == '్';
    public static bool IsJoiner(char c) => c == '\u200C' || c == '\u200D';
    /// <summary>A dependent vowel sign (ా-ౌ, the length marks ౕ \u0C56, the vocalic l signs ౢ ౣ), not the virama.</summary>
    public static bool IsVowelSign(char c) => (c >= 'ా' && c <= 'ౌ' && c != '౅' && c != '౉') || c is 'ౕ' or '\u0C56' or 'ౢ' or 'ౣ';
    /// <summary>A sign that follows its letter or vowel: ఀ ఁ ం ః ఄ.</summary>
    public static bool IsTail(char c) => c >= 'ఀ' && c <= 'ఄ';
    /// <summary>A dependent sign: vowel sign, tail, nukta, virama.</summary>
    public static bool IsMark(char c) => IsVowelSign(c) || IsTail(c) || c == '఼' || IsVirama(c);
    /// <summary>An independent vowel.</summary>
    public static bool IsVowel(char c) => (c >= 'అ' && c <= 'ఔ' && c != '఍' && c != '఑') || c is 'ౠ' or 'ౡ';

    /// <summary>
    /// Ways a Unicode string is not well formed Telugu (empty when it is): a vowel sign or virama that does not follow a
    /// letter, a tail after a virama or a joiner, a joiner away from a virama, Latin-1 letters, C1 controls or the tables'
    /// own marks leaking through. "Reads back to the same bytes" is no proof of text.
    /// </summary>
    public static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool letter = IsLetter(p) || p == '఼';
            if (IsVowelSign(c) && !letter && !(c == '\u0C56' && p == 'ె')) bad.Add($"vowel sign {c} at {i} after '{p}'");
            if (IsVirama(c) && !letter) bad.Add($"virama at {i} after '{p}'");
            if (IsTail(c) && (i == 0 || IsVirama(p) || IsJoiner(p) || !(IsLetter(p) || IsVowel(p) || IsMark(p))))
                bad.Add($"{c} at {i} after '{p}'");
            if (IsJoiner(c) && !IsVirama(p)) bad.Add($"joiner U+{(int)c:X4} at {i} after '{p}'");
            if (c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') bad.Add($"Latin {c} at {i}");
            if (c >= '\u0080' && c <= '\u009F') bad.Add($"C1 U+{(int)c:X4} at {i}");
            if (c < ' ' && c is not ('\t' or '\n' or '\r')) bad.Add($"control U+{(int)c:X4} at {i}");
            if (c >= '\uFDD0' && c <= '\uFDEF') bad.Add($"noncharacter U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>C = consonant, H = virama, s = vowel sign, t = tail, V = vowel, J/N = joiners, o = other.</summary>
    public static string Shape(string u) => new(u.Select(c =>
        IsLetter(c) ? 'C' : IsVirama(c) ? 'H' : c == '\u200D' ? 'J' : c == '\u200C' ? 'N' : IsVowelSign(c) ? 's' : IsTail(c) ? 't' :
        IsVowel(c) ? 'V' : 'o').ToArray());
}
