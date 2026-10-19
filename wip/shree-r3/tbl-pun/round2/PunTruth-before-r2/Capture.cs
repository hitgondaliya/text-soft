using System.Text;
using System.Text.Json.Nodes;
using static Common;

/// <summary>
/// Reads the composer capture (pun7-groundtruth.json, tools: scratchpad r3\cap-pun) into the truth the tables are
/// built against. The capture's "truth" object already holds, per Unicode string, the 7 composer's preferred bytes
/// and every spelling it accepts (canonical typing order only; the alt:* and quirk:* rows are kept apart, as
/// evidence). Its one choice: for a virama before a letter with no subjoined form the halant spelling B5 is preferred
/// (10 of the 12 vendor keyboards put the halant code 44 on their virama key; the link code 64 writes no glyph, and
/// its spelling - the plain letters side by side - stays accepted).
/// </summary>
public static class Capture
{
    public static List<Truth> Read(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var rows = new List<Truth>();
        foreach (var (u, node) in doc["truth"]!.AsObject())
        {
            string pref = Norm((string)node!["preferred"]!);
            var acc = node["accepted"]!.AsArray().Select(x => Norm((string)x!)).ToList();
            acc.Remove(pref);
            acc.Insert(0, pref);
            rows.Add(new Truth(u, pref, acc.Distinct().ToArray()));
        }
        return rows.OrderBy(r => r.U, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Every spelling Shree's composer wrote in the capture, of every kind: the truth's preferred, accepted and
    /// alternative spellings and every probe in "pairs" - the vendor keys, canonical, alt:* and quirk:* rows - as
    /// it wrote them. Distinct (typed text, bytes); what a document typed with the composer can hold. The rows a
    /// phonetic keyboard answered on the carrier byte 07 (no face has 07) are not text and are left out.
    /// </summary>
    public static List<(string U, string Hex, string Kind)> Spellings(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var seen = new HashSet<(string, string)>();
        var list = new List<(string, string, string)>();
        void Add(string u, string hex, string kind)
        {
            hex = Norm(hex);
            if (hex.Length == 0 || hex.Split(' ').Contains("07")) return;
            if (seen.Add((u, hex))) list.Add((u, hex, kind));
        }
        foreach (var (u, node) in doc["truth"]!.AsObject())
        {
            Add(u, (string)node!["preferred"]!, "truth");
            foreach (var a in node["accepted"]!.AsArray()) Add(u, (string)a!, "truth accepted");
            if (node["alternatives"] is JsonArray alts)
                foreach (var a in alts) Add(u, a is JsonObject o ? (string)o["bytes"]! : (string)a!, "truth alternative");
        }
        foreach (var p in doc["pairs"]!.AsArray()) Add((string)p!["unicode"]!, (string)p["bytes"]!, (string)p["kind"]!);
        return list;
    }

    /// <summary>
    /// The composer gaps (pun7-composer-gaps.json next to the capture): Unicode strings typed in Shree's own order for
    /// which the 7 composer loses a glyph, with the spelling derived from its own rules where one follows (null where none
    /// does). Not truth: what the table writes for them is reported against these.
    /// </summary>
    public static List<(string U, string[] Wrote, string? Derived, string? From)> Gaps(string groundTruth)
    {
        string path = Path.Combine(Path.GetDirectoryName(groundTruth)!, "pun7-composer-gaps.json");
        var list = new List<(string, string[], string?, string?)>();
        if (!File.Exists(path)) return list;
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        foreach (var (u, node) in doc["gaps"]!.AsObject())
            list.Add((u, node!["composerWrote"]!.AsArray().Select(x => Norm((string)x!)).ToArray(),
                      node["derived"] is JsonValue d ? Norm((string)d!) : null, node["derivedFrom"] is JsonValue f ? (string)f! : null));
        return list;
    }
}
