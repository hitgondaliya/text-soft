using System.Text;
using static Common;

public static class Fixtures
{
    /// <summary>Write the compact test fixture: [unicode, preferred, [other accepted]] per row.</summary>
    public static void Write(List<Truth> rows)
    {
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners are invisible: write them as escapes.
        string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc })
            .Replace("\u200C", "\\u200C").Replace("\u200D", "\\u200D");
        var sb = new StringBuilder();
        sb.Append("{\n  \"about\": ").Append(J(
            "What Shree-Lipi's own composers write for Unicode Punjabi (Gurmukhi): S8PunU64.dll (Unicode) and S8Pun764.dll " +
            "(Shree-Lipi 7 raw bytes), Shree-Lipi nxt, typed the same keys into both (capture r3\\cap-pun, 2026-10-18: 12 vendor " +
            "keyboards and 1,242 probe keyboards, every capture run twice, byte-identical). Each row: the Unicode string, the " +
            "preferred bytes (hex, one byte per glyph code) and, when there are any, the other spellings the composer also accepts. " +
            "Canonical typing order only (the i-sign struck first, every other sign after its letter, bindi/tippi/visarga/addak " +
            "after the sign, subjoined ੍ਰ ੍ਹ ੍ਵ ੍ਯ with their own codes, nukta letters as letter + nukta); the other orders and the " +
            "composer's quirks are kept in the capture, not here. One choice: a virama before a letter with no subjoined form is " +
            "preferred as the visible halant B5 (10 of the 12 vendor keyboards put the halant code on their virama key); the link " +
            "spelling, with no glyph for the virama, stays accepted. Unicode text spells a tippi after ਾ ੀ ੇ ੈ ੋ ੌ ਉ ਊ ੳ as the " +
            "bindi and ੱ + ਂ as ਁ, as the Unicode composer writes them. The bytes are SHREE-PUN7-0951/0952 codes; the 4/5/6 fonts " +
            "SHREE-PUN-0950/0951/0952/0955/0956 draw the same codes, except ₹ 3C, which they lack (the shree-pun456 tables pass " +
            "₹ through, reported). tools/PunTruth builds and checks the shree-pun7 / shree-pun456 tables against it."));
        sb.Append(",\n  \"rows\": [\n");
        for (int i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var other = t.Acc.Where(a => a != t.Pref).ToList();
            sb.Append("    [").Append(J(t.U)).Append(", ").Append(J(t.Pref));
            if (other.Count > 0) sb.Append(", [").Append(string.Join(", ", other.Select(J))).Append(']');
            sb.Append(']').Append(i < rows.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Fixture)!);
        File.WriteAllText(Fixture, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {rows.Count} rows to {Fixture} ({new FileInfo(Fixture).Length / 1024} KB)");
    }
}
