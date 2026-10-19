using System.Text;
using System.Text.Json.Nodes;

/// <summary>Independent helpers for the Telugu skeptic check (no code shared with tools/TelTruth).</summary>
static class U
{
    public static readonly Encoding Cp;
    static U()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    /// <summary>Legacy text as hex bytes (Windows-1252, U+0090 = 90); anything else as U+XXXX.</summary>
    public static string Hex(string s)
    {
        var parts = new List<string>();
        foreach (char c in s)
        {
            if (c == '\u0090') { parts.Add("90"); continue; }
            if (c < 0x80) { parts.Add(((int)c).ToString("X2")); continue; }
            try
            {
                var b = Cp.GetBytes(c.ToString());
                parts.Add(b.Length == 1 ? b[0].ToString("X2") : "U+" + ((int)c).ToString("X4"));
            }
            catch (EncoderFallbackException) { parts.Add("U+" + ((int)c).ToString("X4")); }
        }
        return string.Join(" ", parts);
    }

    public static string FromHex(string hex)
    {
        var sb = new StringBuilder();
        foreach (var h in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (h.StartsWith("U+")) { sb.Append((char)Convert.ToInt32(h[2..], 16)); continue; }
            int v = Convert.ToInt32(h, 16);
            if (v == 0x90) sb.Append('\u0090');
            else if (v < 0x80) sb.Append((char)v);
            else
            {
                try { sb.Append(Cp.GetString(new[] { (byte)v })); }
                catch (DecoderFallbackException) { sb.Append((char)v); }
            }
        }
        return sb.ToString();
    }

    public static string Norm(string? hex) => hex is null ? "" : string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant().Replace("U+", "U+");
    public static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    // ---- Telugu ----
    public static bool IsDrawnCons(char c) => c >= '\u0C15' && c <= '\u0C39' && c != '\u0C29' && c != '\u0C34';
    public static bool IsLetter(char c) => (c >= '\u0C15' && c <= '\u0C39' && c != '\u0C29') || c is '\u0C58' or '\u0C59' or '\u0C5A' or '\u0C5D';
    public static bool IsVowel(char c) => (c >= '\u0C05' && c <= '\u0C14' && c != '\u0C0D' && c != '\u0C11') || c is '\u0C60' or '\u0C61';
    public static bool IsSign(char c) => (c >= '\u0C3E' && c <= '\u0C4C' && c != '\u0C45' && c != '\u0C49') || c is '\u0C55' or '\u0C56' or '\u0C62' or '\u0C63';
    public static bool IsVirama(char c) => c == '\u0C4D';
    public static bool IsTail(char c) => c >= '\u0C00' && c <= '\u0C04';
    public static bool IsJoiner(char c) => c is '\u200C' or '\u200D';
    public static bool IsDigit(char c) => c >= '\u0C66' && c <= '\u0C6F';

    /// <summary>Characters that may never appear in text read back from a legacy font unless they were typed.</summary>
    public static string? Forbidden(char c)
    {
        if (c < ' ' && c is not ('\t' or '\n' or '\r')) return "C0";
        if (c == '\u007F') return "DEL";
        if (c >= '\u0080' && c <= '\u009F') return "C1";
        if (c >= '\u00A1' && c <= '\u024F' && c != '\u00D7' && c != '\u00F7') return "Latin-1/Ext";
        if (c is '\u02C6' or '\u02DC' or '\u2020' or '\u2021' or '\u2030' or '\u2039' or '\u203A' or '\u2122') return "cp1252-symbol";
        if (c >= '\uE000' && c <= '\uF8FF') return "PUA";
        if (c >= '\uFDD0' && c <= '\uFDEF') return "noncharacter";
        if (c == '\uFFFD') return "U+FFFD";
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return "ASCII letter";
        if ("#$&*<>@[\\]^_`{|}~".Contains(c)) return "ASCII glyph-code";
        return null;
    }

    /// <summary>Ways a Telugu string is not well formed (mine, independent of TelTruth's).</summary>
    public static List<string> Malformed(string u)
    {
        var bad = new List<string>();
        for (int i = 0; i < u.Length; i++)
        {
            char c = u[i], p = i > 0 ? u[i - 1] : '\0';
            bool afterLetter = IsLetter(p) || p == '\u0C3C';
            if (IsSign(c) && !afterLetter && !(c == '\u0C56' && p == '\u0C46')) bad.Add($"sign U+{(int)c:X4}@{i}");
            if (IsVirama(c) && !afterLetter) bad.Add($"virama@{i}");
            if (IsTail(c) && !(IsLetter(p) || IsVowel(p) || IsSign(p) || IsTail(p) || p == '\u0C3C')) bad.Add($"tail U+{(int)c:X4}@{i}");
            if (IsJoiner(c) && !IsVirama(p)) bad.Add($"joiner@{i}");
        }
        return bad;
    }

    public static string Esc(string s) => s.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
}

/// <summary>The raw composer capture, read without tools/TelTruth.</summary>
sealed class Capture
{
    public sealed record T(string Pref, string[] Acc, string[] Alts);
    public sealed record P(string U, string Bytes, string Kind, string Source);
    public readonly Dictionary<string, T> Truth = new(StringComparer.Ordinal);
    public readonly List<P> Pairs = new();
    public readonly List<(string U, string? Derived)> Gaps = new();

    public Capture(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        foreach (var (u, n) in doc["truth"]!.AsObject())
        {
            var alts = new List<string>();
            if (n!["alternatives"] is JsonArray aa)
                foreach (var a in aa) alts.Add(U.Norm(a is JsonObject o ? (string?)o["bytes"] : (string?)a));
            Truth[u] = new T(U.Norm((string?)n["preferred"]), n["accepted"]!.AsArray().Select(x => U.Norm((string?)x)).ToArray(), alts.ToArray());
        }
        foreach (var p in doc["pairs"]!.AsArray())
            Pairs.Add(new P((string)p!["unicode"]!, U.Norm((string?)p["bytes"]), (string)p["kind"]!, (string?)p["source"] ?? ""));
        string gp = Path.Combine(Path.GetDirectoryName(path)!, "tel7-composer-gaps.json");
        if (File.Exists(gp))
        {
            var g = JsonNode.Parse(File.ReadAllText(gp, Encoding.UTF8))!;
            foreach (var n in g["gaps"]!.AsArray()) Gaps.Add(((string)n!["unicode"]!, n["derived"] is JsonValue d ? U.Norm((string?)d) : null));
        }
    }
}
