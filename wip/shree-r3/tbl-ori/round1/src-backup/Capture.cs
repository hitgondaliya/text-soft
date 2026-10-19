using System.Text;
using System.Text.Json.Nodes;
using static Common;

/// <summary>
/// Reads the composer capture (ori7-groundtruth.json, scratchpad r3\cap-ori, 2026-10-18) into the truth the tables are
/// built against. The capture's "truth" object holds, per Unicode string, the 7 composer's preferred bytes and every
/// spelling it accepts (canonical typing order: the e-sign struck first, the reph after its syllable, ra-/ya-phala
/// with their own codes, conjuncts through the link code; the alt:* and quirk:* rows are kept apart, as evidence).
/// The bytes are the composer's raw output; <see cref="Fam.Apply"/> turns them into the codes the installed fonts
/// draw (the one difference: ନ, raw 9C, is 5B in every installed face).
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
        // P1 (tools/Shared/ShreeWordFinal.cs): a virama that ends a word is the visible virama. The Odia composer already
        // answers every such row with FA (kind canonical:pending-link), so the shared rule changes no row here (oritruth wordfinal).
        return WordFinal.Adjust(rows.OrderBy(r => r.U, StringComparer.Ordinal).ToList(), WordFinalSpelling);
    }

    /// <summary>The shared word-final rule for Odia: the virama ୍, the nukta ଼, the visible virama FA, no spacer it goes before.</summary>
    public static (string Pref, string Why)? WordFinalSpelling(Truth t, IReadOnlyDictionary<string, Truth> byU)
    {
        try { return WordFinal.Spell(t, byU, '୍', '଼', "FA", Array.Empty<string>()); }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>
    /// Every spelling Shree's composer wrote in the capture, of every kind: the truth's preferred, accepted and
    /// alternative spellings and every probe in "pairs" - the vendor keys, canonical, alt:* and quirk:* rows - as it
    /// wrote them (raw bytes). Distinct (typed text, bytes). The rows a phonetic keyboard answered on the carrier
    /// byte 07 (no face has 07) are not text and are left out.
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
    /// The probes with a code only the 7 composer types (ori7-only7-evidence.json, 3,850 rows): E1 (the ba-phala FD), 51
    /// (the hook 23), E0 (E3). The Unicode composer writes nothing for them, so the text is the charts' (E1 = ୍ବ) or
    /// unknown; these spellings must read back as Odia text, never as Latin or control characters.
    /// </summary>
    public static List<(string? U, string Hex, string Tag)> EvidenceRows()
    {
        var list = new List<(string?, string, string)>();
        if (!File.Exists(Evidence)) return list;
        var doc = JsonNode.Parse(File.ReadAllText(Evidence, Encoding.UTF8))!;
        foreach (var r in doc["rows"]!.AsArray())
            list.Add(((string?)r!["unicodeIfE1IsBaPhala"], Norm((string)r["bytes"]!), (string)r["tag"]!));
        return list;
    }
}
