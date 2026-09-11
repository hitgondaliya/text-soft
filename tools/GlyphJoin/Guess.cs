using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GlyphJoin;

// Guess — build the base-letter half of an X-series table without a Unicode twin.
//
// Every solved X-series font lays its base letters out the same way: starting a
// few codes into a per-script Private Use block, it walks the script's Unicode
// block in order, skipping unassigned code points, then appends danda and double
// danda. Devanagari has no Unicode twin in this licence, so its table has to be
// built from that rule — and the rule is only trustworthy because it can be
// replayed against the scripts that DO have a twin.
public static class Guess
{
    // Code points added to these blocks AFTER the Unicode version Shree's layouts
    // were cut against. .NET only knows today's assignment, so a font laid out in
    // the older order would drift by one for every later addition.
    static readonly HashSet<int> AddedAfterShree = new(new[]
    {
        // Devanagari
        0x0900, 0x0904, 0x093A, 0x093B, 0x094E, 0x094F, 0x0955, 0x0956, 0x0957,
        0x0971, 0x0972, 0x0973, 0x0974, 0x0975, 0x0976, 0x0977, 0x0978, 0x0979,
        0x097A, 0x097B, 0x097C, 0x097D, 0x097E, 0x097F,
        // Bengali / Assamese
        0x0980, 0x09FC, 0x09FD, 0x09FE,
        // Gurmukhi
        0x0A00, 0x0A11, 0x0A12, 0x0A50, 0x0A75, 0x0A76,
        // Gujarati
        0x0A80, 0x0AF0, 0x0AF9, 0x0AFA, 0x0AFB, 0x0AFC, 0x0AFD, 0x0AFE, 0x0AFF,
        // Oriya
        0x0B00, 0x0B55, 0x0B57, 0x0B5C, 0x0B5D, 0x0B77,
        // Tamil
        0x0B80, 0x0BD0, 0x0BF3, 0x0BF4, 0x0BF5, 0x0BF6, 0x0BF7, 0x0BF8, 0x0BF9,
        0x0BFA,
        // Telugu
        0x0C00, 0x0C04, 0x0C3C, 0x0C5D, 0x0C77, 0x0C7F,
        // Kannada
        0x0C80, 0x0C81, 0x0C84, 0x0C5A, 0x0CDD, 0x0CF1, 0x0CF2, 0x0CF3,
        // Malayalam
        0x0D00, 0x0D01, 0x0D04, 0x0D3B, 0x0D3C, 0x0D4E, 0x0D4F, 0x0D54, 0x0D55,
        0x0D56, 0x0D58, 0x0D59, 0x0D5A, 0x0D5B, 0x0D5C, 0x0D5D, 0x0D5E, 0x0D5F,
        0x0D7A, 0x0D7B, 0x0D7C, 0x0D7D, 0x0D7E, 0x0D7F,
    });

    public static int Run(string font, int puaBase, int block, string? verifyJson, string? outJson)
    {
        var f = Sfnt.Load(font);
        var codes = f.BestCmap().Keys.OrderBy(c => c).ToArray();

        // the run of PUA codes that starts at puaBase
        var run = new List<int>();
        for (int c = puaBase; codes.Contains(c); c++) run.Add(c);

        var assigned = new List<int>();
        for (int cp = block; cp < block + 0x80; cp++)
            if (CharUnicodeInfo.GetUnicodeCategory((char)cp) != UnicodeCategory.OtherNotAssigned
                && !AddedAfterShree.Contains(cp))
                assigned.Add(cp);
        if (block != 0x0900) { assigned.Add(0x0964); assigned.Add(0x0965); }

        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < run.Count && i < assigned.Count; i++)
            table[run[i].ToString("X4")] = assigned[i].ToString("X4");

        Console.WriteLine(Path.GetFileName(font) + ": PUA run " + puaBase.ToString("X4") + "-"
            + (run.Count > 0 ? run[^1].ToString("X4") : "-") + " = " + run.Count + " codes; block U+"
            + block.ToString("X4") + " assigned+danda = " + assigned.Count);

        if (verifyJson is not null)
        {
            var truth = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(verifyJson))!;
            int agree = 0, disagree = 0, unknown = 0;
            var firstBad = new List<string>();
            foreach (var kv in table)
            {
                if (!truth.TryGetValue(kv.Key, out var t)) { unknown++; continue; }
                if (t == kv.Value) agree++;
                else
                {
                    disagree++;
                    if (firstBad.Count < 6) firstBad.Add(kv.Key + ": guess " + kv.Value + " vs truth " + t);
                }
            }
            Console.WriteLine("  verify: agree=" + agree + " disagree=" + disagree + " not-in-truth=" + unknown);
            foreach (var s in firstBad) Console.WriteLine("    " + s);
        }

        if (outJson is not null)
        {
            File.WriteAllText(outJson, JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("  wrote " + outJson);
        }
        return 0;
    }
}
