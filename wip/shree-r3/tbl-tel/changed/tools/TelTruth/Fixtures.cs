using System.Text;
using static Common;

public static class Fixtures
{
    /// <summary>
    /// Write the compact test fixture: [unicode, preferred, [other accepted]] per row, and for a row whose preferred
    /// spelling is a documented exception [unicode, preferred, [other accepted], composer's own, why].
    /// </summary>
    public static void Write(Capture.Result cap)
    {
        var rows = cap.Rows;
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners are invisible: write them as escapes.
        string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc })
            .Replace("\u200C", "\\u200C").Replace("\u200D", "\\u200D");
        var sb = new StringBuilder();
        sb.Append("{\n  \"about\": ").Append(J(
            "What Shree-Lipi's own composers write for Unicode Telugu: S8TelU64.dll (Unicode) and S8Tel764.dll (Shree-Lipi 7 raw " +
            "bytes), Shree-Lipi nxt, typed the same keys into both (capture r3\\cap-tel, 2026-10-18: the 16 vendor .TEL keyboards and " +
            "878 probe keyboards, every capture run twice in separate processes, byte-identical). Each row: the Unicode string, the " +
            "preferred bytes (hex, one byte per glyph code; 80-9F as the fonts map them, Windows-1252) and, when there are any, the " +
            "other spellings the composer also accepts. Canonical typing order only (the consonant, then the vowel sign, then ం ః ఁ; " +
            "conjuncts with the link code; the ra-vattu struck before its letter, as the vendor charts type it); the other orders and " +
            "the composer's quirks are kept in the capture, not here. A row with five fields is a documented exception: the table " +
            "writes the preferred bytes, the composer wrote the fourth field (accepted, read back), the fifth says why (see " +
            "\"exceptions\"). word-final rows (C + virama at the end of a word) come from tools/Shared/ShreeWordFinal.cs: the composer's " +
            "own spelling of C + virama + ZWNJ (its pending link drops the virama). The same bytes serve every face: SHREE-TEL7-* and " +
            "SHREE-TEL-* (identical designs), Shree-Tel7-001/002. tools/TelTruth builds and checks the shree-tel7 / shree-tel7w tables " +
            "against it."));
        sb.Append(",\n  \"exceptions\": {\n");
        for (int i = 0; i < Capture.Exceptions.Length; i++)
            sb.Append("    ").Append(J(Capture.Exceptions[i].Id)).Append(": ").Append(J(Capture.Exceptions[i].What)).Append(i < Capture.Exceptions.Length - 1 ? ",\n" : "\n");
        sb.Append("  },\n  \"removed\": [\n");
        for (int i = 0; i < cap.Removed.Count; i++)
        {
            var (u, c, w) = cap.Removed[i];
            sb.Append("    [").Append(J(u)).Append(", ").Append(J(c)).Append(", ").Append(J(w)).Append(']').Append(i < cap.Removed.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ],\n  \"rows\": [\n");
        for (int i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var other = t.Acc.Where(a => a != t.Pref).ToList();
            sb.Append("    [").Append(J(t.U)).Append(", ").Append(J(t.Pref));
            if (other.Count > 0 || t.Composer is not null) sb.Append(", [").Append(string.Join(", ", other.Select(J))).Append(']');
            if (t.Composer is not null) sb.Append(", ").Append(J(t.Composer)).Append(", ").Append(J(t.Why ?? ""));
            sb.Append(']').Append(i < rows.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Fixture)!);
        File.WriteAllText(Fixture, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {rows.Count} rows ({rows.Count(r => r.Composer is null)} the composer's own, {rows.Count(r => r.Composer is not null)} documented exceptions), {cap.Removed.Count} removed, to {Fixture} ({new FileInfo(Fixture).Length / 1024} KB)");
    }
}
