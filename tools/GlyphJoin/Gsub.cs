using System.IO;
using System.Linq;
using System.Text;

namespace GlyphJoin;

// Gsub — recover the Unicode SEQUENCE behind every glyph of a Unicode Indic font.
//
// A Unicode font's cmap only covers base letters and marks. Conjuncts, half forms
// and reph live in GSUB: a ligature lookup says "these input glyphs become that
// one glyph". Walking those lookups outwards from the cmap gives, for most glyphs,
// the exact Unicode string that produces it — which is what turns a legacy PUA
// conjunct code into real text.
public static class Gsub
{
    // a half form is "consonant + virama"; a below/post/pre-base form is
    // "virama + consonant". Both are single substitutions, so the feature tag is
    // the only thing that says which way the virama goes.
    static readonly HashSet<string> HalfFeatures = new(StringComparer.Ordinal) { "half", "rphf" };
    static readonly HashSet<string> SubjoinedFeatures = new(StringComparer.Ordinal) { "blwf", "vatu", "pstf", "pref" };

    public static Dictionary<int, string> GlyphSequences(Sfnt font, Dictionary<int, int> cmap, out string diag)
    {
        var seq = new Dictionary<int, string>();
        foreach (var kv in cmap.OrderBy(k => k.Key))
            if (!seq.ContainsKey(kv.Value)) seq[kv.Value] = char.ConvertFromUtf32(kv.Key);

        int virama = Virama(cmap.Keys);
        var lookups = ReadLookups(font, out var featureOfLookup);
        if (lookups.Count == 0) { diag = "no GSUB"; return seq; }

        // Form-defining features (what a glyph IS) must claim a glyph before the
        // positional variant features (where it SITS) do, or a subjoined consonant
        // gets recorded as its plain base letter and the conjunct is lost.
        var priority = new HashSet<string>(StringComparer.Ordinal)
            { "akhn", "nukt", "rphf", "rkrf", "pref", "blwf", "half", "pstf", "vatu", "cjct" };
        var order = Enumerable.Range(0, lookups.Count)
            .OrderBy(li => priority.Contains(featureOfLookup.TryGetValue(li, out var ft) ? ft : "") ? 0 : 1)
            .ToArray();

        int added = 0;
        for (int pass = 0; pass < 8; pass++)
        {
            int before = seq.Count;
            foreach (int li in order)
            {
                string feat = featureOfLookup.TryGetValue(li, out var f) ? f : "";
                foreach (var (outGid, inGids) in lookups[li])
                {
                    if (seq.ContainsKey(outGid)) continue;
                    var sb = new StringBuilder();
                    bool ok = true;
                    foreach (int g in inGids)
                    {
                        if (!seq.TryGetValue(g, out var s) || s.Length == 0) { ok = false; break; }
                        // a conjunct joins its consonants with a virama; the ligature
                        // lookup runs after that virama glyph was already consumed
                        if (sb.Length > 0 && virama > 0 && IsConsonant(sb[sb.Length - 1], virama) && IsConsonant(s[0], virama))
                            sb.Append(char.ConvertFromUtf32(virama));
                        sb.Append(s);
                    }
                    if (!ok) continue;
                    string s2 = sb.ToString();
                    if (virama > 0)
                    {
                        char v = (char)virama;
                        if (inGids.Length == 1)
                        {
                            if (HalfFeatures.Contains(feat)) s2 += v;
                            else if (SubjoinedFeatures.Contains(feat)) s2 = v + s2;
                        }
                        // A below/post-base form is usually a LIGATURE of (consonant,
                        // virama) — but the glyph it makes is the one that hangs under
                        // the PREVIOUS letter, so in text the virama comes first.
                        // Leaving it as "consonant + virama" makes it collide with the
                        // explicit-halant form and the conjunct never forms.
                        else if (SubjoinedFeatures.Contains(feat) && s2.Length >= 2 && s2[^1] == v)
                            s2 = v + s2[..^1];
                    }
                    seq[outGid] = s2;
                    added++;
                }
            }
            if (seq.Count == before) break;
        }
        if (Environment.GetEnvironmentVariable("GSUB_DEBUG") == "1")
        {
            for (int li = 0; li < lookups.Count; li++)
            {
                string ft = featureOfLookup.TryGetValue(li, out var t2) ? t2 : "-";
                int withSeq = lookups[li].Count(s => seq.ContainsKey(s.outGid));
                int inputsKnown = lookups[li].Count(s => s.inGids.All(g => seq.ContainsKey(g)));
                Console.Error.WriteLine("  lookup " + li + " feat=" + ft + " subs=" + lookups[li].Count
                    + " out-with-seq=" + withSeq + " all-inputs-known=" + inputsKnown
                    + " sample=" + string.Join(" ; ", lookups[li].Take(3).Select(s => "in" + s.inGids.Length + ":" + (seq.TryGetValue(s.outGid, out var sv) ? string.Join("+", sv.Select(ch => ((int)ch).ToString("X4"))) : "-"))));
            }
        }

        diag = "GSUB lookups=" + lookups.Count + " glyphs-with-sequence=" + seq.Count + " (+" + added + " beyond cmap) features=" + string.Join(",", featureOfLookup.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value)) + " virama-initial=" + seq.Values.Count(v => v.Length > 1 && v[0] == (char)virama);
        return seq;
    }

    static bool IsConsonant(char c, int virama)
    {
        int block = virama - 0x4D;
        int off = c - block;
        return off is (>= 0x15 and <= 0x39) or (>= 0x58 and <= 0x5F) or 0x79 or 0x7A;
    }

    /// The script's own virama. Take the block the font mostly covers, not the
    /// first Indic code point seen: every Indic font also maps danda U+0964, which
    /// lives in the Devanagari block and would hand back the wrong virama.
    static int Virama(IEnumerable<int> codes)
    {
        var counts = new Dictionary<int, int>();
        foreach (int c in codes)
        {
            if (c is < 0x0900 or > 0x0D7F) continue;
            if (c is 0x0964 or 0x0965 or 0x0970) continue;   // shared punctuation
            int b = (c - 0x0900) / 0x80 * 0x80 + 0x0900;
            counts[b] = counts.GetValueOrDefault(b) + 1;
        }
        if (counts.Count == 0) return 0;
        return counts.OrderByDescending(k => k.Value).First().Key + 0x4D;
    }

    // lookupIndex -> list of (outputGid, inputGids)
    static List<List<(int outGid, int[] inGids)>> ReadLookups(Sfnt f, out Dictionary<int, string> featureOfLookup)
    {
        featureOfLookup = new Dictionary<int, string>();
        var result = new List<List<(int, int[])>>();
        if (!f.Tables.TryGetValue("GSUB", out var t)) return result;
        var d = f.D;
        uint g = t.off;

        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);

        uint featureListOff = g + U16(g + 6);
        uint lookupListOff = g + U16(g + 8);

        int lookupCount = U16(lookupListOff);
        var lookupOffs = new uint[lookupCount];
        for (int i = 0; i < lookupCount; i++) lookupOffs[i] = lookupListOff + U16(lookupListOff + 2 + i * 2);

        // feature tag per lookup index
        int featCount = U16(featureListOff);
        for (int i = 0; i < featCount; i++)
        {
            long rec = featureListOff + 2 + i * 6;
            string tag = Encoding.ASCII.GetString(d, (int)rec, 4);
            uint featOff = featureListOff + U16(rec + 4);
            int n = U16(featOff + 2);
            for (int j = 0; j < n; j++)
            {
                int li = U16(featOff + 4 + j * 2);
                if (!featureOfLookup.ContainsKey(li)) featureOfLookup[li] = tag;
            }
        }

        for (int i = 0; i < lookupCount; i++)
        {
            var subs = new List<(int, int[])>();
            uint lo = lookupOffs[i];
            int type = U16(lo);
            int subCount = U16(lo + 4);
            for (int s = 0; s < subCount; s++)
            {
                uint st = lo + U16(lo + 6 + s * 2);
                int realType = type;
                if (type == 7) { realType = U16(st + 2); st = st + (uint)((d[st + 4] << 24) | (d[st + 5] << 16) | (d[st + 6] << 8) | d[st + 7]); }
                if (realType == 1) ReadSingle(d, st, subs);
                else if (realType == 4) ReadLigature(d, st, subs);
            }
            result.Add(subs);
        }
        return result;
    }

    static List<int> Coverage(byte[] d, uint off)
    {
        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
        var list = new List<int>();
        int fmt = U16(off);
        if (fmt == 1)
        {
            int n = U16(off + 2);
            for (int i = 0; i < n; i++) list.Add(U16(off + 4 + i * 2));
        }
        else if (fmt == 2)
        {
            int n = U16(off + 2);
            for (int i = 0; i < n; i++)
            {
                long r = off + 4 + i * 6;
                int start = U16(r), end = U16(r + 2);
                for (int gid = start; gid <= end; gid++) list.Add(gid);
            }
        }
        return list;
    }

    static void ReadSingle(byte[] d, uint st, List<(int, int[])> subs)
    {
        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
        int fmt = U16(st);
        var cov = Coverage(d, st + U16(st + 2));
        if (fmt == 1)
        {
            short delta = (short)U16(st + 4);
            foreach (int gid in cov) subs.Add(((gid + delta) & 0xFFFF, new[] { gid }));
        }
        else if (fmt == 2)
        {
            int n = U16(st + 4);
            for (int i = 0; i < n && i < cov.Count; i++) subs.Add((U16(st + 6 + i * 2), new[] { cov[i] }));
        }
    }

    static void ReadLigature(byte[] d, uint st, List<(int, int[])> subs)
    {
        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
        var cov = Coverage(d, st + U16(st + 2));
        int setCount = U16(st + 4);
        for (int i = 0; i < setCount && i < cov.Count; i++)
        {
            uint setOff = st + U16(st + 6 + i * 2);
            int ligCount = U16(setOff);
            for (int j = 0; j < ligCount; j++)
            {
                uint lig = setOff + U16(setOff + 2 + j * 2);
                int ligGid = U16(lig);
                int compCount = U16(lig + 2);
                var ins = new int[compCount];
                ins[0] = cov[i];
                for (int c = 1; c < compCount; c++) ins[c] = U16(lig + 2 + c * 2);
                subs.Add((ligGid, ins));
            }
        }
    }
}
