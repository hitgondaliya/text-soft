using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>Shared helpers for the independent Odia measurements (skeptic).</summary>
static class K
{
    public const string Scratch = @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3";
    public static string Cap => Path.Combine(Scratch, "cap-ori");
    public static string GT => Path.Combine(Cap, "ori7-groundtruth.json");
    public static string VendorKeys => Path.Combine(Cap, "ori-vendor-keys.json");
    public static string Evidence => Path.Combine(Cap, "ori7-only7-evidence.json");
    public static string PResults => Path.Combine(Cap, "capp", "P-results.json");

    public static readonly Encoding Cp;
    static K()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    public static AssetBundle B;
    public static LanguageProfile Or;
    public static FontConverter Fwd, Rev;
    public static ConversionPipeline Pipe;
    public static string Repo;

    public static void Load(string repo)
    {
        Repo = repo;
        B = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();
        Or = B.Languages.First(l => l.Id == "or");
        Fwd = B.CreateConverterToLegacy("shree-ori7");
        var fwdTables = B.MappingTables.Where(m => m.TargetFontId == "shree-ori7" && m.TargetType == FontType.Legacy && m.SourceType == FontType.Unicode).ToList();
        var revTables = B.MappingTables.Where(m => m.SourceFontId == "shree-ori7" && m.SourceType == FontType.Legacy && m.TargetType == FontType.Unicode).ToList();
        Console.WriteLine($"repo {repo}: tables to shree-ori7 {fwdTables.Count} ({string.Join(",", fwdTables.Select(t => t.Pairs.Count + " pairs"))}), from shree-ori7 {revTables.Count} ({string.Join(",", revTables.Select(t => t.Pairs.Count + " pairs"))})");
        Rev = new FontConverter(revTables[0]);
        Pipe = new ConversionPipeline(B);
    }

    /// <summary>Document text as hex bytes. C1 code points other than U+008F/U+0090 are marked (no face maps them).</summary>
    public static string Hex(string s)
    {
        var p = new List<string>(s.Length);
        foreach (char c in s)
        {
            if (c < 0x80 || (c >= 0xA0 && c <= 0xFF) || c == '\u008F' || c == '\u0090') p.Add(((int)c).ToString("X2"));
            else if (c >= 0x80 && c <= 0x9F) p.Add("C1:" + ((int)c).ToString("X2"));
            else
            {
                try { p.Add(Cp.GetBytes(c.ToString())[0].ToString("X2")); }
                catch (EncoderFallbackException) { p.Add("U+" + ((int)c).ToString("X4")); }
            }
        }
        return string.Join(" ", p);
    }

    /// <summary>Composer bytes as a document holds them (Windows-1252 for 80-9F, but 81 8D 8F 90 9D stay C1).</summary>
    public static string Doc(string hex)
    {
        var sb = new StringBuilder();
        foreach (var t in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (t.StartsWith("U+")) { sb.Append((char)Convert.ToInt32(t[2..], 16)); continue; }
            int b = Convert.ToInt32(t, 16);
            if (b >= 0x80 && b <= 0x9F && b is not (0x81 or 0x8D or 0x8F or 0x90 or 0x9D)) sb.Append(Cp.GetString(new[] { (byte)b }));
            else sb.Append((char)b);
        }
        return sb.ToString();
    }

    public static string Norm(string h) => string.Join(" ", (h ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The two documented exceptions: ନ raw 9C written 5B; 3C 8F 90 passed through as ₹ ୰ ଽ.</summary>
    public static string Ex(string hex, bool na, bool pass) => Norm(string.Join(" ", Norm(hex).Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => na && t == "9C" ? "5B" : t)
        .Select(t => !pass ? t : t switch { "3C" => "U+20B9", "8F" => "U+0B70", "90" => "U+0B3D", _ => t })));

    public static string Cps(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    public static bool IsCons(char c) => (c >= '\u0B15' && c <= '\u0B39') || c is '\u0B5C' or '\u0B5D' or '\u0B5F' or '\u0B71';
    public static bool IsIndVowel(char c) => (c >= '\u0B05' && c <= '\u0B14') || c is '\u0B60' or '\u0B61';
    public static bool IsDigit(char c) => c >= '\u0B66' && c <= '\u0B6F';
    public static bool IsMark(char c) => (c >= '\u0B01' && c <= '\u0B03') || c == '\u0B3C' || (c >= '\u0B3E' && c <= '\u0B57') || c is '\u0B62' or '\u0B63';
    public const char Virama = '\u0B4D';

    /// <summary>Characters that must never come out of reading Odia legacy text.</summary>
    public static string BadChars(string s)
    {
        var bad = new List<string>();
        foreach (char c in s)
        {
            var cat = char.GetUnicodeCategory(c);
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) bad.Add("Latin " + c);
            else if (c < 0x20 && c is not ('\t' or '\n' or '\r')) bad.Add($"C0 U+{(int)c:X4}");
            else if (c >= 0x7F && c <= 0x9F) bad.Add($"C1 U+{(int)c:X4}");
            else if (c >= 0xC0 && c <= 0x24F) bad.Add($"Latin-ext U+{(int)c:X4}");
            else if (c is '\uFFFD' or '\uFFFE' or '\uFFFF' || (c >= '\uFDD0' && c <= '\uFDEF')) bad.Add($"nonchar U+{(int)c:X4}");
            else if (cat == UnicodeCategory.PrivateUse) bad.Add($"PUA U+{(int)c:X4}");
            else if ("ƒˆ˜‰‹›™€Œœ".Contains(c)) bad.Add($"cp1252 U+{(int)c:X4}");
        }
        return string.Join("; ", bad.Distinct());
    }

    /// <summary>Odia text that cannot be right: a sign with no base, a sign after a virama, two viramas.</summary>
    public static string Malformed(string s)
    {
        var bad = new List<string>();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!IsMark(c) && c != Virama) continue;
            char prev = i > 0 ? s[i - 1] : ' ';
            if (i == 0 || prev == ' ' || prev == '\n') bad.Add($"{c} at {i} has no base");
            else if (prev == Virama && c != '\u0B3C') bad.Add($"{c} after virama at {i}");
            else if (!(IsCons(prev) || IsIndVowel(prev) || IsMark(prev) || prev == '\u0B3C' || prev == '\u200D' || prev == '\u200C' || prev == Virama || prev == '\u0B70' || prev == '\u25CC'))
                bad.Add($"{c} after U+{(int)prev:X4} at {i}");
        }
        return string.Join("; ", bad);
    }

    public static JsonNode ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;

    public static StreamWriter Out(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        return new StreamWriter(Path.Combine(dir, name), false, new UTF8Encoding(false));
    }
}

/// <summary>One truth row of the raw capture.</summary>
sealed record Row(string U, string Pref, List<string> Acc, List<string> Alt);

static class Truth
{
    public static List<Row> Load()
    {
        var gt = K.ReadJson(K.GT);
        var rows = new List<Row>();
        foreach (var (u, node) in gt["truth"]!.AsObject())
        {
            var acc = node!["accepted"]!.AsArray().Select(x => K.Norm((string)x!)).ToList();
            var alt = node["alternatives"] is JsonArray a ? a.Select(x => K.Norm(x is JsonObject o ? (string)o["bytes"]! : (string)x!)).ToList() : new List<string>();
            rows.Add(new Row(u, K.Norm((string)node["preferred"]!), acc, alt));
        }
        return rows;
    }
}
