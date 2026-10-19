using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Common;

/// <summary>
/// The test fixture Data/shree-tel-keys.json: which key of each of Shree's 14 fixed Telugu keyboards is which, read off
/// the keyboards' own files as the composers answered them (cap-tel\tel-vendor-keys.json, every key of layer 0 and
/// Ctrl+Space with the code it holds) - the consonant keys, the vowel-sign keys (the sign each types after a letter: the
/// vowel-sign codes, from the capture's vendor rows where such a key alone types its vowel), the link (64), the halant
/// (44), the tails ం ః ఁ (3C 3E E0) and the ra-vattu (D7). A test types every class on every consonant key through the
/// real TypingEngine (a chart shows a sign on క only; Shree's composer puts it on every letter).
/// </summary>
public static class KeyFixture
{
    public static string Path_ => Path.Combine(Root, "tests", "FontSoftware.Tests", "Data", "shree-tel-keys.json");

    static readonly Dictionary<string, string> LayoutOf = new()
    {
        ["APPLE"] = "tel-apple", ["CRTRONIC"] = "tel-crtronic", ["DOE"] = "tel-doe", ["GODREJ"] = "tel-godrej", ["INDICA"] = "tel-indica",
        ["INSCRIPT"] = "te-inscript", ["ITR"] = "tel-itr", ["MICRO"] = "tel-micro", ["MONO"] = "tel-mono", ["NEWROMA"] = "tel-newroma",
        ["PRAKASH"] = "tel-prakash", ["ROMA"] = "tel-roma", ["TYPEWRIT"] = "tel-typewrit", ["VISION"] = "tel-vision",
    };

    static readonly Dictionary<char, char> SignOfVowel = new()
    {
        ['ఆ'] = 'ా', ['ఇ'] = 'ి', ['ఈ'] = 'ీ', ['ఉ'] = 'ు', ['ఊ'] = 'ూ', ['ఋ'] = 'ృ', ['ౠ'] = 'ౄ',
        ['ఎ'] = 'ె', ['ఏ'] = 'ే', ['ఐ'] = 'ై', ['ఒ'] = 'ొ', ['ఓ'] = 'ో', ['ఔ'] = 'ౌ',
    };

    public static void Write()
    {
        string capDir = Path.GetDirectoryName(GroundTruth)!;
        var gt = JsonNode.Parse(File.ReadAllText(GroundTruth, Encoding.UTF8))!;
        var signCodes = new SortedDictionary<string, char>(StringComparer.Ordinal);
        foreach (var p in gt["pairs"]!.AsArray())
        {
            string kind = (string)p!["kind"]!, src = (string?)p["source"] ?? "", u = (string)p["unicode"]!;
            if (!kind.StartsWith("vendor-key (a vowel-sign key alone", StringComparison.Ordinal)) continue;
            int at = src.IndexOf("codes ", StringComparison.Ordinal);
            if (at >= 0 && u.Length == 1 && SignOfVowel.TryGetValue(u[0], out var sign)) signCodes[src[(at + 6)..].Trim()] = sign;
        }
        signCodes["6D"] = 'ై';   // the ై key: the composer answers it alone with a placeholder, not ఐ (round 1: it types ఐ alone)
        var keys = JsonNode.Parse(File.ReadAllText(Path.Combine(capDir, "tel-vendor-keys.json"), Encoding.UTF8))!["keyboards"]!.AsObject();
        var o = new JsonObject
        {
            ["about"] = "Each of Shree's 14 fixed Telugu keyboards (C:\\Shree-Lipi nxt\\<KB>.TEL, captured key by key through Shree's " +
                        "composers: r3\\cap-tel\\tel-vendor-keys.json): its consonant keys (a key, or a Ctrl+Space key where no plain key has that letter, whose code the Unicode composer answers " +
                        "with one consonant; not ర), its vowel-sign keys (the vowel-sign codes " + string.Join(" ", signCodes.Select(k => k.Key + "=" + k.Value)) +
                        ": alone the vowel, after a letter the sign), link keys (64), halant keys (44, the visible virama), tails (3C ం, " +
                        "3E ః, E0 ఁ) and ra-vattu keys (D7, which Shree puts on the NEXT consonant). Keys as the charts write them: a " +
                        "character of a US keyboard, Ø + key for Ctrl+Space. Written by tools/TelTruth (teltruth keys).",
        };
        var layouts = new JsonObject();
        foreach (var (kb, node) in keys.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!LayoutOf.TryGetValue(kb, out var id)) continue;
            var cons = new JsonObject(); var signs = new JsonObject(); var tails = new JsonObject();
            var links = new JsonArray(); var halants = new JsonArray(); var ras = new JsonArray();
            foreach (var layer in new[] { "0", "1" })
                foreach (var (k, v) in node!["layers"]![layer]!.AsObject().OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    string stroke = (layer == "1" ? "Ø" : "") + k, codes = ((string?)v!["codes"]) ?? "", u = (string?)v["uRead"] ?? "";
                    if (codes.Split(' ').Length == 1 && u.Length == 1 && IsCons(u[0]) && u != "ర" && u != "ఴ"
                        && !cons.Any(c => (string?)c.Value == u))
                        cons[stroke] = u;
                    if (signCodes.TryGetValue(codes, out var sign)) signs[stroke] = sign.ToString();
                    if (codes == "64") links.Add(stroke);
                    if (codes == "44") halants.Add(stroke);
                    if (codes == "D7") ras.Add(stroke);
                    if (codes is "3C" or "3E" or "E0") tails[stroke] = codes == "3C" ? "ం" : codes == "3E" ? "ః" : "ఁ";
                }
            layouts[id] = new JsonObject { ["keyboard"] = kb, ["consonants"] = cons, ["signs"] = signs, ["links"] = links, ["halants"] = halants, ["tails"] = tails, ["ravattu"] = ras };
        }
        o["layouts"] = layouts;
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        File.WriteAllText(Path_, o.ToJsonString(opts).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
        Console.WriteLine($"wrote {layouts.Count} keyboards to {Path_}");
    }
}
