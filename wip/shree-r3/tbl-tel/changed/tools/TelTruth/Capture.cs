using System.Text;
using System.Text.Json.Nodes;
using static Common;

/// <summary>
/// Reads the composer capture (tel7-groundtruth.json, scratchpad r3\cap-tel) into the truth the tables are built
/// against. The capture's "truth" object holds, per Unicode string, the 7 composer's preferred bytes and every spelling
/// it accepts (canonical typing order only: the consonant, then the vowel sign, then ం ః ఁ; conjuncts through the link
/// code; the ra-vattu struck before its letter, as the vendor charts type it; the alt:* and quirk:* rows are kept apart,
/// as evidence).
///
/// Where the composer's bytes draw something other than what was typed, the table writes what the fonts draw for it and
/// the composer's own spelling stays accepted (it is read back): the documented exceptions below, each checked against
/// the capture (an exception that no longer holds throws). Everything else is the composer's spelling, byte for byte.
/// </summary>
public static class Capture
{
    /// <summary>The documented exceptions: id, what the composer does, why the table does otherwise (the evidence).</summary>
    public static readonly (string Id, string What)[] Exceptions =
    {
        ("nga-vattu", "్ఙ: the composer writes 5D, which every installed face draws as the visarga (two circles, advance 198/1000); " +
                      "the subjoined ఙ is 94 (the code the 7 composer types for its own code DB). The table writes 94."),
        ("rra-vattu", "్ఱ: the composer writes CA, a spacing glyph (advance 752/1000) with the outline of the ు+ూ tail: క్ఱ = 4D E6 FC CA " +
                      "is drawn as కూ, BA CA as ౠ (the earlier table spelled ౠ so). The subjoined ఱ is 3C (zero width like every vattu; " +
                      "4D E6 FC 3C draws what Nirmala UI draws for క్ఱ - r3\\tbl-tel\\round1\\gdi-rra.png, eye-v.png; the earlier table wrote 3C). " +
                      "The table writes 3C. (The 7 composer types 3C for its code 5E after the link - క్ష subjoined, 7-only evidence - which " +
                      "draws ్ఱ: the table writes C్క్ష as the vattus of క and ష, 50 DB.)"),
        ("visarga", "ః: the composer writes 40, which every installed face draws as a talakattu tick (zero width, advance 5/1000, the " +
                    "same ink as E6); the visarga glyph is 5D. The table writes 5D (cap-tel fonts\\check-marks3.png, gdi-marks.png)."),
        ("candrabindu", "ఁ: the composer writes B6, which every installed face draws as a talakattu tick (in SHREE-TEL7-Apoorva B6 is the " +
                        "very outline of E6); the candrabindu glyph is 97. The table writes 97."),
        ("jha-dropped", "C్ఝ: the composer writes nothing for ఝ after the link (no face has a subjoined ఝ): the letter is lost. The table " +
                        "writes the visible halant of the letter before it and ఝ in full - its own spellings of C్\u200C and ఝ..., both truth rows."),
        ("removed", "Rows left out of the table's truth: ౢ (the composer writes AB A7 E6, pieces of ధ - no face has a vocalic-l glyph: " +
                    "passed through, reported, P3b); * (92, which draws ₹ in SHREE-TEL7/SHREE-TEL and a star in Shree-Tel7-001/002: " +
                    "per family, Family.cs); — and – (9B 9B / 9B: 9B is a blank glyph, advance 269/1000, in every face - Word shows " +
                    "a gap; written as the fonts' hyphen 26, P4); శ్రీ్\u200Cక (a virama after a vowel sign: not well-formed text)."),
    };

    public sealed record Result(List<Truth> Rows, List<(string U, string Composer, string Why)> Removed, Dictionary<string, int> Counts);

    public static Result Read(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var raw = new List<Truth>();
        foreach (var (u, node) in doc["truth"]!.AsObject())
        {
            string pref = Norm((string)node!["preferred"]!);
            var acc = node["accepted"]!.AsArray().Select(x => Norm((string)x!)).ToList();
            acc.Remove(pref);
            acc.Insert(0, pref);
            raw.Add(new Truth(u, pref, acc.Distinct().ToArray()));
        }
        return Adjust(raw, PendingVirama(doc), Gaps(path).Select(g => g.U).ToList());
    }

    /// <summary>What the 7 composer wrote for C + link at a word end (it drops the virama): unicode -> bytes.</summary>
    static Dictionary<string, string> PendingVirama(JsonNode doc)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in doc["pairs"]!.AsArray())
            if (((string)p!["kind"]!).StartsWith("quirk:7-composer-drops-the-pending-virama", StringComparison.Ordinal))
                d.TryAdd((string)p["unicode"]!, Norm((string)p["bytes"]!));
        return d;
    }

    static string Swap(string hex, string from, string to) =>
        string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(b => b == from ? to : b));

    public static Result Adjust(List<Truth> raw, Dictionary<string, string> pending, List<string> gaps)
    {
        var counts = new Dictionary<string, int>();
        void Count(string k) => counts[k] = counts.GetValueOrDefault(k) + 1;
        var removed = new List<(string, string, string)>();
        var byU = raw.ToDictionary(t => t.U, StringComparer.Ordinal);
        var rows = new List<Truth>();
        foreach (var t0 in raw)
        {
            var t = t0;
            string u = t.U;
            // (A sign or a vattu alone, two characters at most, is a piece the table needs, not text: kept.)
            if (u is "ౢ" or "*" or "—" or "–" || (u.Length > 2 && Malformed(u).Count > 0))
            {
                removed.Add((u, t.Pref, u == "ౢ" ? "no vocalic-l glyph: the composer writes pieces of ధ" : u == "*" ? "per family (92 draws ₹ or a star)"
                    : u is "—" or "–" ? "9B is a blank glyph" : "not well-formed: " + string.Join("; ", Malformed(u))));
                Count("removed");
                continue;
            }
            var why = new List<string>();
            string Fix(string hex)
            {
                // 5D in a composer spelling is only ever the subjoined ఙ (its visarga is 40): checked below.
                if (u.Contains("్ఙ")) hex = Swap(hex, "5D", "94");
                if (u.Contains("్ఱ")) hex = Swap(hex, "CA", "3C");
                if (u.Contains('ః')) hex = Swap(hex, "40", "5D");
                if (u.Contains('ఁ')) hex = Swap(hex, "B6", "97");
                return hex;
            }
            foreach (var a in t.Acc)
            {
                var codes = a.Split(' ');
                if (codes.Contains("CA") && !u.Contains("్ఱ")) throw new InvalidOperationException($"CA in {u} ({a}) is not the subjoined ఱ");
                if (codes.Contains("5D") && !u.Contains("్ఙ")) throw new InvalidOperationException($"5D in {u} ({a}) is not the subjoined ఙ");
                if (codes.Contains("40") && !u.Contains('ః')) throw new InvalidOperationException($"40 in {u} ({a}) is not the visarga");
                if (codes.Contains("B6") && !u.Contains('ఁ')) throw new InvalidOperationException($"B6 in {u} ({a}) is not the candrabindu");
            }
            string pref = Fix(t.Pref);
            if (pref != t.Pref)
            {
                if (u.Contains("్ఙ") && t.Pref.Split(' ').Contains("5D")) { why.Add("nga-vattu"); Count("nga-vattu"); }
                if (u.Contains("్ఱ") && t.Pref.Split(' ').Contains("CA")) { why.Add("rra-vattu"); Count("rra-vattu"); }
                if (u.Contains('ః') && t.Pref.Split(' ').Contains("40")) { why.Add("visarga"); Count("visarga"); }
                if (u.Contains('ఁ') && t.Pref.Split(' ').Contains("B6")) { why.Add("candrabindu"); Count("candrabindu"); }
            }
            if (u.Contains("్ఝ"))
            {
                // The composer drops ఝ after the link: the halant of what comes before it, then ఝ with what follows.
                int at = u.IndexOf("్ఝ", StringComparison.Ordinal);
                string head = u[..at] + "్\u200C", tail = u[(at + 1)..];
                if (!byU.TryGetValue(head, out var h) || !byU.TryGetValue(tail, out var tl))
                {
                    removed.Add((u, t.Pref, "ఝ dropped by the composer, and no truth for " + (byU.ContainsKey(head) ? tail : head)));
                    Count("removed");
                    continue;
                }
                pref = Norm(Fix(h.Pref) + " " + Fix(tl.Pref));
                why.Add("jha-dropped");
                Count("jha-dropped");
            }
            if (pref != t.Pref)
            {
                // (where the composer dropped ఝ its spellings hold no ఝ: no glyph-fixed copies of them are made up)
                var acc = (why.Contains("jha-dropped") ? Array.Empty<string>() : t.Acc.Select(Fix)).Prepend(pref).Concat(t.Acc).Distinct().ToArray();
                rows.Add(t with { Pref = pref, Acc = acc, Composer = t.Pref, Why = string.Join(",", why) });
            }
            else rows.Add(t);
        }

        // A virama that ends a word (tools/Shared/ShreeWordFinal.cs): the 7 composer answers C + link at a word end with the
        // letter alone (the virama is lost); every Unicode font draws the visible halant there, as the composer writes C + ్ + ZWNJ.
        // The ra-vattu + virama at a word end (C్ర్, tel7-composer-gaps.json) has no composer answer at all: no bytes.
        var byRow = rows.ToDictionary(r => r.U, StringComparer.Ordinal);
        var finals = pending.Where(p => p.Key.Length > 1 && p.Key[^1] == '్' && Malformed(p.Key).Count == 0)
            .Select(p => (U: p.Key, Bytes: p.Value))
            .Concat(gaps.Where(g => !pending.ContainsKey(g) && g.Length > 1 && g[^1] == '్' && Malformed(g).Count == 0).Select(g => (U: g, Bytes: "")))
            .OrderBy(p => p.U, StringComparer.Ordinal).ToList();
        foreach (var (u, bytes) in finals)
        {
            if (byRow.ContainsKey(u)) continue;
            var t = new Truth(u, bytes, bytes.Length > 0 ? new[] { bytes } : Array.Empty<string>());
            if (WordFinal.Spell(t, byRow, '్', '఼', "8A", new[] { "FC", "FD", "FE", "FF" }) is not { } a) continue;
            var row = t with { Pref = a.Pref, Acc = t.Acc.Prepend(a.Pref).Distinct().ToArray(), Composer = bytes, Why = "word-final" };
            rows.Add(row);
            byRow[u] = row;
            Count("word-final");
        }
        rows = rows.OrderBy(r => r.U, StringComparer.Ordinal).ToList();
        counts["rows"] = rows.Count;
        counts["composer's own"] = rows.Count(r => r.Composer is null);
        return new Result(rows, removed, counts);
    }

    /// <summary>
    /// Every spelling Shree's composer wrote in the capture, of every kind: the truth's preferred, accepted and
    /// alternative spellings and every probe in "pairs" - the vendor keys, canonical, alt:* and quirk:* rows - as it wrote
    /// them. Distinct (typed text, bytes); what a document typed with the composer can hold. The 7 composer's carrier for a
    /// lone sign (E6 61 E6 61) is a probe artefact, kept: it must still read as text.
    /// </summary>
    public static List<(string U, string Hex, string Kind)> Spellings(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var seen = new HashSet<(string, string)>();
        var list = new List<(string, string, string)>();
        void Add(string u, string hex, string kind)
        {
            hex = Norm(hex);
            if (hex.Length == 0) return;
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
    /// The composer gaps (tel7-composer-gaps.json next to the capture): Unicode strings the composer cannot spell
    /// directly, with the spelling derived from its own answers. Not truth: what the table writes is reported against them.
    /// </summary>
    public static List<(string U, string? Derived, string? From)> Gaps(string groundTruth)
    {
        string path = Path.Combine(Path.GetDirectoryName(groundTruth)!, "tel7-composer-gaps.json");
        var list = new List<(string, string?, string?)>();
        if (!File.Exists(path)) return list;
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        foreach (var node in doc["gaps"]!.AsArray())
            list.Add(((string)node!["unicode"]!, node["derived"] is JsonValue d ? Norm((string)d!) : null, node["from"] is JsonValue f ? (string)f! : null));
        return list;
    }
}
