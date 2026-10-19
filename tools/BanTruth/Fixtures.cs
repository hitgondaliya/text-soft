using System.Text;
using static Common;

public static class Fixtures
{
    /// <summary>Write the compact test fixture: [unicode, preferred, [other accepted]] per row.</summary>
    public static void Write(List<Truth> rows, List<string> left)
    {
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners are invisible: write them as escapes.
        string J(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc })
            .Replace("\u200C", "\\u200C").Replace("\u200D", "\\u200D");
        var sb = new StringBuilder();
        sb.Append("{\n  \"about\": ").Append(J(
            "What Shree-Lipi's own composers write for Unicode Bangla / Assamese (S8BanU64.dll and S8Ban764.dll, Shree-Lipi nxt, " +
            "typed the same keys into both). Each row: the Unicode string, the preferred bytes (hex, one byte per glyph code) and, " +
            "when there are any, the other spellings the composer also accepts. The bytes are the composer's, in the codes the " +
            "SHREE-BAN 4/5/6 fonts (SHREE-BAN-*) draw them with: the composer writes ব and ৰ as AF and ৱ as A4, which in every " +
            "SHREE-BAN font draw ব্ল and ব, so here ব is A4 (the code the composer itself uses for ব inside ব্ব and ব্ল), ৱ is 9E " +
            "and ৰ is B9 (no SHREE-BAN font has ৰ; the SHREE-ASS fonts draw it at B9). Left out: " + left.Count + " composer rows " +
            "that lose a typed letter (ক্গু written as গু, ৱ written with the র glyph) - its bugs, not spellings. Changed, where the " +
            "composer's spelling does not draw the text (tools/BanTruth/Decisions.cs): Assamese ৰ as reph or ra-phala (ৰ্ক, ক্ৰ) has " +
            "the bytes of the same text with র - the composer writes ৰ there as a letter with a visible hasanta, its Assamese keyboards " +
            "typing র for ra (and the 127 ৰ rows with no র twin in the capture: a ra-phala with a hasanta is the composer's own " +
            "letter + visible hasanta + ra, ক্ৰ্র = 41 F4 A1 B9 F8; a reph takes the composer's bytes for the text typed in its own order); " +
            "the filler FF of দ্গ দ্ঘ ধ্ব ব্ব ড়্গ ৱ্ৱ, which draws nothing (দগ, two letters), is the subscript ব AB " +
            "for ধ্ব ব্ব and a visible hasanta for the others; a lone র্ is B9 F4, not the pending reph A2; ব্ঢ ব্ড চ্ঞ, which the " +
            "composer writes 81 8D 90 - C1 control code points Word and RichEdit draw as nothing - are letter + visible hasanta + " +
            "letter (ব্ঢ = A4 F4 6E A1); ড় ঢ় with ু ূ and ঁ or a reph, where the composer's spelling draws no ু ূ (ড়ুঁ = D8 6C A1 F2 A1, " +
            "র্ড়ু = D8 6C A1 A2 A1), keep the sign the way the composer's own alternative does (র্ড়ু = D8 6C A2 A1 E6, ড়ুঁ = D8 6C F2 A1 E6). " +
            "Every row that ends in a hasanta already has the visible halant F4 in the composer's own " +
            "spelling. ৱ stays 9E here: the Bangla fonts hold it only at U+009E (drawn by nothing but the app's preview), so their " +
            "tables pass ৱ through, reported; the Assamese fonts draw it at AF. The other font " +
            "families (7: SHREE-BAN7-*, Shree-Ban7-001/002; Assamese: SHREE-ASS-*, SHREE-ASS7-*, Shree-Ass7-001/002) use the same " +
            "spellings with a few codes moved; tools/BanTruth (Family.cs) and the tests say which. tools/BanTruth builds and checks " +
            "the shree-ban7 / ban456 / ass7 / ass456 / ass7w tables against it."));
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
