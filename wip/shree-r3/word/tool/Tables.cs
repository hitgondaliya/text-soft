using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WordVis;

public sealed class FwdTable
{
    public string Name = "", Path = "", Where = "";
    public List<(string from, string to)> Pairs = new();
    public List<(string pattern, string replace, string note)> Post = new();

    public static FwdTable Load(string path, string where)
    {
        var j = JsonNode.Parse(File.ReadAllText(path))!;
        var t = new FwdTable { Path = path, Where = where, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
        foreach (var p in j["pairs"]!.AsArray())
        {
            if (p!["unmapped"] is JsonNode u && (bool)u) continue;
            t.Pairs.Add(((string)p["from"]!, (string)p["to"]!));
        }
        if (j["postRules"] is JsonArray pr)
            foreach (var r in pr) t.Post.Add(((string)r!["pattern"]!, (string)r["replace"]!, (string)r["note"] ?? ""));
        return t;
    }

    /// Every code point this table can write: the 'to' side of the pairs and the literal text of the post rules.
    public HashSet<int> Written()
    {
        var s = new HashSet<int>();
        foreach (var (_, to) in Pairs) foreach (var r in Cps(to)) s.Add(r);
        foreach (var (_, rep, _) in Post) foreach (var r in Cps(Regex.Replace(rep, @"\$\d|\$\{\w+\}", ""))) s.Add(r);
        return s;
    }

    public static IEnumerable<int> Cps(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
            else yield return s[i];
        }
    }
}

public static class Tables
{
    public const string Main = @"C:\Users\harde\Desktop\Windows Font Software\assets\config\mappings";
    public const string Wt = @"C:\Users\harde\Desktop\Windows Font Software\.claude\worktrees\wf_607a9ebd-c6c-2\assets\config\mappings";

    /// family -> (main encoding, worktree encoding or null) from languages.json of each checkout.
    public static (string main, string wt) Wiring(string family) => family switch
    {
        "SHREE-GUJ7" or "Shree-Guj7-00x" => ("shree-guj7", null),
        "SHREE-GUJ" => ("shree-guj456", null),
        "SHREE-DEV7" or "Shree-Dev7-00x" => ("shree-dev7", null),
        "SHREE-DEV" => ("shree-dev456", null),
        "SHREE-SAN7" => ("shree-san7", null),
        "SHREE-BAN7" or "Shree-Ban7-00x" => ("shree-ban7", "shree-ban7"),
        "SHREE-BAN" => ("shree-ban7", "shree-ban456"),
        "SHREE-ASS7" => ("shree-ban7", "shree-ass7"),
        "SHREE-ASS" => ("shree-ban7", "shree-ass456"),
        "Shree-Ass7-00x" => ("shree-ban7", "shree-ass7w"),
        "SHREE-ORI7" or "SHREE-ORI" or "Shree-Ori7-00x" => ("shree-ori7", null),
        "SHREE-PUN7" or "SHREE-PUN" => ("shree-pun7", null),
        "SHREE-TAM7" or "SHREE-TAM" or "Shree-Tam7-00x" => ("shree-tam7", null),
        "SHREE-TEL7" or "SHREE-TEL" or "Shree-Tel7-00x" => ("shree-tel7", null),
        "SHREE-KAN7" or "SHREE-KAN" or "Shree-Kan7-00x" => ("shree-kan7", null),
        "SHREE-MAL7" or "Shree-Mal7-00x" => ("shree-mal7", null),
        _ => (null, null),
    };

    public static List<FwdTable> Forward(string family)
    {
        var (m, w) = Wiring(family);
        var res = new List<FwdTable>();
        if (m != null) res.Add(FwdTable.Load(System.IO.Path.Combine(Main, $"unicode-to-{m}.json"), "main"));
        if (w != null) res.Add(FwdTable.Load(System.IO.Path.Combine(Wt, $"unicode-to-{w}.json"), "worktree"));
        return res;
    }

    public static List<FwdTable> Reverse(string family)
    {
        var (m, w) = Wiring(family);
        var res = new List<FwdTable>();
        if (m != null) res.Add(FwdTable.Load(System.IO.Path.Combine(Main, $"{m}-to-unicode.json"), "main"));
        if (w != null) res.Add(FwdTable.Load(System.IO.Path.Combine(Wt, $"{w}-to-unicode.json"), "worktree"));
        return res;
    }
}
