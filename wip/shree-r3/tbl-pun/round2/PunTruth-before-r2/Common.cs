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
    public static string Fixture => Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-pun7-truth.json");

    /// <summary>The full capture (composer rows + truth). Only needed to rebuild the fixture and to read every spelling back.</summary>
    public static string GroundTruth =>
        Environment.GetEnvironmentVariable("PUN7_GROUNDTRUTH") is { Length: > 0 } p ? p
        : Path.Combine(Path.GetTempPath(), "claude", "C--Users-harde-Desktop-Windows-Font-Software",
            "7673923b-96b4-4287-87dd-a8e7c6533eca", "scratchpad", "r3", "cap-pun", "pun7-groundtruth.json");

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
    /// Legacy text as hex bytes. Every code a SHREE-PUN7 / SHREE-PUN face maps is in 20-7E or A0-BF (and U+2219), all
    /// Windows-1252 bytes; a char outside the code page shows as U+XXXX - in a spelling, a character the table passes
    /// through and reports (₹ in a 4/5/6 font).
    /// </summary>
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

    /// <summary>Hex bytes as the code points the fonts' cmaps hold (Windows-1252; a U+XXXX token is that character).</summary>
    public static string FromHex(string hex)
    {
        if (hex.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(h.StartsWith("U+") ? ((char)Convert.ToInt32(h[2..], 16)).ToString()
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

    // ---------- Gurmukhi characters ----------

    /// <summary>A consonant: ਕ-ਹ, the nukta letters \u0A59 \u0A5A \u0A5B \u0A5C \u0A5E (and \u0A33 \u0A36 inside the range).</summary>
    public static bool IsCons(char c) =>
        (c >= 'ਕ' && c <= 'ਹ' && c is not ('਩' or '਱' or '਴' or '਷'))
        || c is '\u0A59' or '\u0A5A' or '\u0A5B' or '\u0A5C' or '\u0A5E';
    public static bool IsVirama(char c) => c == '੍';
    public static bool IsJoiner(char c) => c == '‌' || c == '‍';
    /// <summary>A vowel sign (ਾ-ੌ, not the virama).</summary>
    public static bool IsVowelSign(char c) => (c >= '\u0A3E' && c <= '\u0A42') || c is '\u0A47' or '\u0A48' or '\u0A4B' or '\u0A4C';
    /// <summary>A sign that follows its letter: the bearing marks (adak bindi, bindi, visarga, tippi, addak) and udaat, yakash.</summary>
    public static bool IsTail(char c) => c is 'ਁ' or 'ਂ' or 'ਃ' or 'ੰ' or 'ੱ' or 'ੑ' or 'ੵ';
    /// <summary>A dependent sign: vowel sign, tail, nukta, virama.</summary>
    public static bool IsMark(char c) => IsVowelSign(c) || IsTail(c) || c == '਼' || IsVirama(c);
    /// <summary>An independent vowel, or the carriers ੲ ੳ.</summary>
    public static bool IsVowel(char c) => (c >= '\u0A05' && c <= '\u0A0A') || c is '\u0A0F' or '\u0A10' or '\u0A13' or '\u0A14' or '\u0A72' or '\u0A73';

    /// <summary>
    /// Ways a Unicode string is not well formed Gurmukhi (empty when it is): a vowel sign or virama that does not
    /// follow a letter, a nukta not on a letter, a tail after a virama or a joiner, a joiner away from a virama,
    /// Latin-1 letters, C1 controls or the tables' own marks leaking through. "Reads back to the same bytes" is
    /// no proof of text.
    /// </summary>
    public static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool letter = IsCons(p) || p == '਼';
            if (IsVowelSign(c) && !letter) bad.Add($"vowel sign {c} at {i} after '{p}'");
            if (IsVirama(c) && !letter) bad.Add($"virama at {i} after '{p}'");
            if (c == '਼' && !IsCons(p)) bad.Add($"nukta at {i} after '{p}'");
            if (IsTail(c) && (i == 0 || IsVirama(p) || IsJoiner(p) || !(IsCons(p) || IsVowel(p) || IsMark(p))))
                bad.Add($"{c} at {i} after '{p}'");
            if (IsJoiner(c) && !IsVirama(p)) bad.Add($"joiner U+{(int)c:X4} at {i} after '{p}'");
            if (c >= '¡' && c <= 'ɏ' && c != '×' && c != '÷') bad.Add($"Latin {c} at {i}");
            if (c >= '\u0080' && c <= '\u009F') bad.Add($"C1 U+{(int)c:X4} at {i}");
            if (c < ' ' && c is not ('\t' or '\n' or '\r')) bad.Add($"control U+{(int)c:X4} at {i}");
            if (c >= '﷐' && c <= '﷯') bad.Add($"noncharacter U+{(int)c:X4} at {i}");
        }
        return bad;
    }

    /// <summary>C = consonant, H = virama, s = vowel sign, t = tail, n = nukta, V = vowel, J/N = joiners, o = other.</summary>
    public static string Shape(string u) => new(u.Select(c =>
        IsCons(c) ? 'C' : IsVirama(c) ? 'H' : c == '‍' ? 'J' : c == '‌' ? 'N' : IsVowelSign(c) ? 's' : IsTail(c) ? 't' :
        c == '਼' ? 'n' : IsVowel(c) ? 'V' : 'o').ToArray());
}
