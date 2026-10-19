using System.Text;
using static Common;

public static class Fixtures
{
    /// <summary>
    /// Write the compact test fixture: [unicode, preferred, [other accepted]] per row, the spellings the shree-ori7 table
    /// writes (the composer's raw bytes through <see cref="Fam.Apply"/>), and the decisions that make them differ from the
    /// raw bytes ("byteMap", "exceptions").
    /// </summary>
    public static void Write(List<Truth> raw)
    {
        var fam = Fam.Ori7;
        var rows = fam.Truth(raw);
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners are invisible: write them as escapes.
        string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc })
            .Replace("\u200C", "\\u200C").Replace("\u200D", "\\u200D");
        int mapped = rows.Count(t => t.Composer is not null && Policy.Rows.All(p => p.U != t.U));
        var sb = new StringBuilder();
        sb.Append("{\n  \"about\": ").Append(J(
            "What Shree-Lipi's own composers write for Unicode Odia: S8OriU64.dll (Unicode) and S8Ori764.dll (Shree-Lipi 7 raw " +
            "bytes), Shree-Lipi nxt, typed the same keys into both (capture r3\\cap-ori, 2026-10-18: the 8 vendor .ORI keyboards " +
            "and 1,203 probe keyboards, every capture run twice, identical). Each row: the Unicode string, the preferred bytes " +
            "(hex, one byte per glyph code) and, when there are any, the other spellings the composer also accepts. Canonical " +
            "typing order only (the e-sign struck first, ୈ ୋ ୌ as େ + letter + ୖ / ା / ୗ, the reph struck after its syllable and " +
            "before ଂ ଁ ଃ, ra-/ya-phala with their own codes, conjuncts through the link code); the other orders and the composer's " +
            "quirks are kept in the capture, not here. The bytes are what the shree-ori7 table writes for SHREE-ORI7-0600..0604 and " +
            "SHREE-ORI-0600..0604 (the same codes in both families): the composer's raw bytes with the corrections in byteMap " +
            "and exceptions. tools/OriTruth builds and checks the shree-ori7 tables against it."));
        sb.Append(",\n  \"byteMap\": {\n    \"9C\": ").Append(J(
            $"5B - the composer's raw byte for ନ is 9C; every installed face (S0600892-S0604892, S06000F2-S06040F2, WORI7001/2) " +
            $"draws the ligature ଙ୍କ୍ତ at 9C (U+0153, 566/1000 wide) and ନ at 5B (341 wide; 5B FF is as wide as କ 4C FF). Rendered: " +
            $"r3\\tbl-ori\\round1\\glyphs3.png, sheet-00..02.png. {mapped} rows carry ନ and are written with 5B; the reverse tables " +
            $"read a bare 9C as ଙ୍କ୍ତ, the glyph a document shows there, and 9C FF - ନ as the composer writes it, which is the only " +
            $"place it writes 9C - as ନ.")).Append("\n  }");
        sb.Append(",\n  \"exceptions\": [\n");
        for (int i = 0; i < Policy.Rows.Length; i++)
        {
            var (u, composer, why) = Policy.Rows[i];
            var row = rows.First(r => r.U == u);
            sb.Append("    [").Append(J(u)).Append(", ").Append(J(row.Pref)).Append(", ").Append(J(composer)).Append(", ").Append(J(why))
              .Append(']').Append(i < Policy.Rows.Length - 1 ? ",\n" : "\n");
        }
        sb.Append("  ],\n  \"rows\": [\n");
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
        Console.WriteLine($"wrote {rows.Count} rows to {Fixture} ({new FileInfo(Fixture).Length / 1024} KB); rows with ନ 9C -> 5B: {mapped}");
    }
}
