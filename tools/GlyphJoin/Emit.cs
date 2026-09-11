using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GlyphJoin;

// Emit — turn a raw GlyphJoin table (legacy code -> Unicode sequence) into the
// two MappingTable files our converter loads, complete with the matra reorder
// rules. Indic text stores a pre-base matra AFTER its consonant but draws it
// BEFORE; a legacy font has no shaping engine, so the legacy byte stream must
// carry the matra in visual order. That swap is the pre/post rule.
public static class Emit
{
    // per script block: the matras that are drawn to the LEFT of the cluster
    static readonly Dictionary<int, int[]> PreBaseMatras = new()
    {
        [0x0900] = new[] { 0x093F },                     // Devanagari
        [0x0980] = new[] { 0x09BF, 0x09C7, 0x09C8 },     // Bengali / Assamese
        [0x0A00] = new[] { 0x0A3F },                     // Gurmukhi
        [0x0A80] = new[] { 0x0ABF },                     // Gujarati
        [0x0B00] = new[] { 0x0B47, 0x0B48 },             // Oriya — i-matra is post-base here
        [0x0B80] = new[] { 0x0BC6, 0x0BC7, 0x0BC8 },     // Tamil
        [0x0C00] = Array.Empty<int>(),                   // Telugu — all above/post base
        [0x0C80] = Array.Empty<int>(),                   // Kannada
        [0x0D00] = new[] { 0x0D46, 0x0D47, 0x0D48 },     // Malayalam
    };

    public static int Run(string rawJson, string encodingId, string unicodeFontId, string outDir, string? name, bool noReorder = false)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(rawJson))!;
        Directory.CreateDirectory(outDir);

        var pairs = new List<(string legacy, string uni)>();
        foreach (var kv in raw)
        {
            var lsb = new StringBuilder();
            foreach (var lc in kv.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                lsb.Append(char.ConvertFromUtf32(Convert.ToInt32(lc, 16)));
            string legacy = lsb.ToString();
            var sb = new StringBuilder();
            foreach (var cp in kv.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                sb.Append(char.ConvertFromUtf32(Convert.ToInt32(cp, 16)));
            string uni = sb.ToString();
            if (uni.Length == 0) continue;
            // an identity ASCII pair carries no information
            if (legacy == uni && legacy.Length == 1 && legacy[0] < 0x80) continue;
            pairs.Add((legacy, uni));
        }

        int block = DetectBlock(pairs.Select(p => p.uni));

        // Malayalam has no half forms — a conjunct is one glyph and the word ends
        // in a chillu, not a bare virama. The font still builds those glyphs with a
        // half-form lookup, which leaves a trailing virama the spelling never has,
        // so strip it here rather than round-tripping ല്ല back as ല്ല്.
        if (block == 0x0D00)
        {
            char v0 = (char)(block + 0x4D);
            for (int i = 0; i < pairs.Count; i++)
                if (pairs[i].uni.Length >= 3 && pairs[i].uni[^1] == v0)
                    pairs[i] = (pairs[i].legacy, pairs[i].uni[..^1]);
        }

        int filled = FillOrderedGaps(pairs, block);
        // A table captured from Shree's own converter already lists whole clusters
        // (consonant + matra) as units, so reordering the input would break the match.
        var (fwdRules, revRules) = noReorder ? (new JsonArray(), new JsonArray()) : Rules(block);
        string label = name ?? encodingId;

        Write(Path.Combine(outDir, encodingId + "-to-unicode.json"), new JsonObject
        {
            ["id"] = encodingId + "-to-unicode",
            ["name"] = label + " -> Unicode",
            ["sourceFontId"] = encodingId,
            ["targetFontId"] = unicodeFontId,
            ["sourceType"] = "legacy",
            ["targetType"] = "unicode",
            ["preRules"] = new JsonArray(),
            ["pairs"] = PairArray(pairs.Select(p => (p.legacy, p.uni))),
            ["postRules"] = fwdRules,
        });

        int combos = AddSubjoinedCombos(pairs, block);

        // reverse: longest Unicode sequence wins, so a conjunct beats its parts
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rev = new List<(string, string)>();
        var ordered = pairs.OrderByDescending(p => p.uni.Length).ToList();

        // When the pre-base reorder rule is on, the input reaches the pair matcher
        // with the matra already moved in front, so a "consonant + matra" pair can
        // never match — and if it is left in the list it wins on the UNREORDERED
        // text and puts the matra on the wrong side. Drop those, keep the pieces.
        var preBase = !noReorder && PreBaseMatras.TryGetValue(block, out var pb)
            ? new HashSet<char>(pb.Select(c => (char)c))
            : new HashSet<char>();
        bool ClusterWithPreBaseMatra(string s) =>
            s.Length >= 2 && preBase.Contains(s[^1]) && !preBase.Contains(s[0]);

        foreach (var p in ordered)
            if (!ClusterWithPreBaseMatra(p.uni) && seen.Add(p.uni)) rev.Add((p.uni, p.legacy));

        // The same letter can be spelled more than one way. Real text uses whichever
        // the keyboard produced, so accept the alternates too: a nukta letter typed
        // decomposed (ਸ + ਼ for ਸ਼), a Malayalam chillu typed as consonant + virama
        // (ണ + ് for ൺ), and a conjunct written without the trailing virama that the
        // font's half-form lookup left on it. Alternates run in a SECOND pass so they
        // only fill gaps and never shadow a spelling the font stated directly.
        foreach (var p in ordered)
            foreach (string alt in Alternates(p.uni, block))
                if (alt.Length > 0 && seen.Add(alt)) rev.Add((alt, p.legacy));

        foreach (var (uni, legacy) in ComposedLetters(pairs))
            if (seen.Add(uni)) rev.Add((uni, legacy));

        Write(Path.Combine(outDir, "unicode-to-" + encodingId + ".json"), new JsonObject
        {
            ["id"] = "unicode-to-" + encodingId,
            ["name"] = "Unicode -> " + label,
            ["sourceFontId"] = unicodeFontId,
            ["targetFontId"] = encodingId,
            ["sourceType"] = "unicode",
            ["targetType"] = "legacy",
            ["preRules"] = revRules,
            ["pairs"] = PairArray(rev),
            ["postRules"] = new JsonArray(),
        });

        Console.WriteLine(encodingId + ": " + pairs.Count + " pairs, block U+" + block.ToString("X4")
            + ", reorder matras=" + (PreBaseMatras.TryGetValue(block, out var m) ? m.Length : 0) + ", gap-filled=" + filled + ", subjoined-combos=" + combos);
        return 0;
    }

    /// The X-series lays its base letters out in Unicode block order, so a code
    /// the shape match could not decide sits between two codes that did. When the
    /// gap on both sides is the same width, the missing pair is forced.
    static int FillOrderedGaps(List<(string legacy, string uni)> pairs, int block)
    {
        var single = new SortedDictionary<int, int>();
        foreach (var p in pairs)
        {
            if (p.legacy.Length != 1 || p.uni.Length != 1) continue;
            int cp = p.uni[0];
            if (cp < block || cp >= block + 0x80) continue;
            int code = p.legacy[0];
            if (!single.ContainsKey(code)) single[code] = cp;
        }
        var known = single.Keys.ToArray();
        var taken = new HashSet<int>(single.Values);
        var have = new HashSet<int>(pairs.Select(p => p.legacy.Length == 1 ? p.legacy[0] : -1));
        int added = 0;
        for (int i = 0; i + 1 < known.Length; i++)
        {
            int c1 = known[i], c2 = known[i + 1];
            int u1 = single[c1], u2 = single[c2];
            int span = c2 - c1;
            if (span < 2 || span > 16 || u2 - u1 != span) continue;
            for (int k = 1; k < span; k++)
            {
                int code = c1 + k, cp = u1 + k;
                if (have.Contains(code) || taken.Contains(cp)) continue;
                pairs.Add((((char)code).ToString(), ((char)cp).ToString()));
                taken.Add(cp); have.Add(code); added++;
            }
        }
        return added;
    }

    /// Kannada-style scripts write a conjunct as base letter + a subjoined form
    /// hanging under it, and the subjoined form is spelled "virama + consonant".
    /// The converter takes the longest match at each position, so on "ನ ್ ನ" it
    /// would grab "ನ ್" — the explicit-halant form — and the conjunct would never
    /// form. Spelling the whole cluster out as one three-character pair makes the
    /// right reading the longest one.
    static int AddSubjoinedCombos(List<(string legacy, string uni)> pairs, int block)
    {
        string virama = char.ConvertFromUtf32(block + 0x4D);
        var baseLeg = new Dictionary<string, string>(StringComparer.Ordinal);
        var subjoinedLeg = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pairs)
        {
            if (p.uni.Length == 1 && p.uni[0] >= block + 0x15 && p.uni[0] <= block + 0x39)
            { if (!baseLeg.ContainsKey(p.uni)) baseLeg[p.uni] = p.legacy; }
            else if (p.uni.Length == 2 && p.uni.StartsWith(virama, StringComparison.Ordinal)
                     && p.uni[1] >= block + 0x15 && p.uni[1] <= block + 0x39)
            { if (!subjoinedLeg.ContainsKey(p.uni)) subjoinedLeg[p.uni] = p.legacy; }
        }
        if (subjoinedLeg.Count == 0) return 0;

        var have = new HashSet<string>(pairs.Select(p => p.uni), StringComparer.Ordinal);
        var added = new List<(string, string)>();
        foreach (var b in baseLeg)
            foreach (var s in subjoinedLeg)
            {
                string uni = b.Key + s.Key;
                if (!have.Add(uni)) continue;
                added.Add((b.Value + s.Value, uni));
            }
        pairs.AddRange(added);
        return added.Count;
    }

    /// Gurmukhi writes its independent vowels as a bearer letter plus a matra —
    /// ਆ is ਅ + ਾ on the page — so a legacy font stores two codes and has none for
    /// the precomposed letter. Spell them out so ਆ still converts.
    static readonly Dictionary<char, string> WrittenAsTwo = new()
    {
        ['ਆ'] = "ਅਾ", ['ਇ'] = "ੲਿ", ['ਈ'] = "ੲੀ",
        ['ਉ'] = "ੳੁ", ['ਊ'] = "ੳੂ", ['ਏ'] = "ੲੇ",
        ['ਐ'] = "ਅੈ", ['ਓ'] = "ੳੋ", ['ਔ'] = "ਅੌ",
    };

    /// Reverse pairs for letters the font spells with two codes, built from the
    /// pieces it does have. Only fills what is genuinely missing.
    static List<(string uni, string legacy)> ComposedLetters(List<(string legacy, string uni)> pairs)
    {
        var byUni = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pairs) if (!byUni.ContainsKey(p.uni)) byUni[p.uni] = p.legacy;

        var res = new List<(string, string)>();
        foreach (var kv in WrittenAsTwo)
        {
            string whole = kv.Key.ToString();
            if (byUni.ContainsKey(whole)) continue;
            if (!byUni.TryGetValue(kv.Value[0].ToString(), out var l1)) continue;
            if (!byUni.TryGetValue(kv.Value[1].ToString(), out var l2)) continue;
            res.Add((whole, l1 + l2));
        }
        return res;
    }

    // Malayalam chillu letters and the consonant + virama they stand for.
    static readonly Dictionary<char, string> Chillu = new()
    {
        ['ൺ'] = "ണ്", ['ൻ'] = "ന്", ['ർ'] = "ര്",
        ['ൽ'] = "ല്", ['ൾ'] = "ള്", ['ൿ'] = "ക്",
    };

    /// Other spellings of the same text that should map to the same legacy code.
    static IEnumerable<string> Alternates(string uni, int block)
    {
        string decomposed = uni.Normalize(NormalizationForm.FormD);
        if (decomposed != uni) yield return decomposed;

        if (uni.Any(c => Chillu.ContainsKey(c)))
        {
            var sb = new StringBuilder();
            foreach (char c in uni) sb.Append(Chillu.TryGetValue(c, out var e) ? e : c.ToString());
            yield return sb.ToString();
        }

        // A conjunct glyph often comes out of the font's half-form lookup, which
        // leaves a trailing virama the written word does not have (ല ് ല ് for ല്ല).
        char virama = (char)(block + 0x4D);
        if (uni.Length >= 3 && uni[^1] == virama) yield return uni[..^1];
    }

    static JsonArray PairArray(IEnumerable<(string from, string to)> src)
    {
        var arr = new JsonArray();
        foreach (var p in src) arr.Add(new JsonObject { ["from"] = p.from, ["to"] = p.to });
        return arr;
    }

    static int DetectBlock(IEnumerable<string> unicodes)
    {
        var counts = new Dictionary<int, int>();
        foreach (string s in unicodes)
            foreach (var r in s.EnumerateRunes())
                if (r.Value is >= 0x0900 and <= 0x0D7F)
                {
                    int b = (r.Value - 0x0900) / 0x80 * 0x80 + 0x0900;
                    counts[b] = counts.GetValueOrDefault(b) + 1;
                }
        return counts.Count == 0 ? 0x0900 : counts.OrderByDescending(k => k.Value).First().Key;
    }

    static (JsonArray fwd, JsonArray rev) Rules(int block)
    {
        var fwd = new JsonArray();
        var rev = new JsonArray();
        if (!PreBaseMatras.TryGetValue(block, out var matras) || matras.Length == 0) return (fwd, rev);

        string ka = char.ConvertFromUtf32(block + 0x15);
        string ha = char.ConvertFromUtf32(block + 0x39);
        string nukta = char.ConvertFromUtf32(block + 0x3C);
        string virama = char.ConvertFromUtf32(block + 0x4D);
        string cluster = "((?:[" + ka + "-" + ha + "]" + nukta + "?" + virama + ")*[" + ka + "-" + ha + "]" + nukta + "?)";

        // ...and put the two-code spellings back together on the way out, so ਅ + ਾ
        // reads as ਆ rather than as the two pieces the legacy font stores.
        foreach (var kv in WrittenAsTwo)
        {
            if (kv.Value[0] < block || kv.Value[0] >= block + 0x80) continue;
            fwd.Add(new JsonObject
            {
                ["pattern"] = kv.Value,
                ["replace"] = kv.Key.ToString(),
                ["note"] = "Recompose U+" + ((int)kv.Key).ToString("X4") + ", which the legacy font stores as two codes.",
            });
        }

        foreach (int m in matras)
        {
            string mc = char.ConvertFromUtf32(m);
            fwd.Add(new JsonObject
            {
                ["pattern"] = mc + cluster,
                ["replace"] = "$1" + mc,
                ["note"] = "Move pre-base matra U+" + m.ToString("X4") + " after the consonant cluster (legacy visual order -> Unicode logical order).",
            });
            rev.Add(new JsonObject
            {
                ["pattern"] = cluster + mc,
                ["replace"] = mc + "$1",
                ["note"] = "Move pre-base matra U+" + m.ToString("X4") + " before the consonant cluster (Unicode logical order -> legacy visual order).",
            });
        }
        return (fwd, rev);
    }

    static void Write(string path, JsonObject obj)
    {
        File.WriteAllText(path, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("  wrote " + Path.GetFileName(path));
    }
}
