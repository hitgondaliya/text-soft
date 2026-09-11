using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GlyphJoin;

// Keys — build the list of key sequences to feed the capture.
//
// Single keys only reach the base letters. The conjuncts, half forms and reph
// that a legacy font stores as one code each are typed as consonant + halant +
// consonant, so the sequence list is generated from a first capture: whichever
// keys turned out to be consonants get crossed with each other through the
// halant key.
public static class Keys
{
    sealed class Row { public string k { get; set; } = ""; public string v { get; set; } = ""; }

    public static int Run(string unicodeCapture, string outFile, int maxPairs, string? halantOverride)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(File.ReadAllText(unicodeCapture))!;
        var byKey = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            var cps = r.v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(h => Convert.ToInt32(h, 16))
                         .Where(c => c != 0x0D && c != 0x0A).ToArray();
            if (cps.Length > 0) byKey[r.k] = cps;
        }

        int block = byKey.Values.SelectMany(v => v)
                         .Where(c => c is >= 0x0900 and <= 0x0D7F)
                         .GroupBy(c => (c - 0x0900) / 0x80 * 0x80 + 0x0900)
                         .OrderByDescending(g => g.Count())
                         .Select(g => g.Key).FirstOrDefault();
        if (block == 0) { Console.Error.WriteLine("no Indic block in capture"); return 1; }
        int virama = block + 0x4D;

        string? halantKey = byKey.FirstOrDefault(kv => kv.Value.Length == 1 && kv.Value[0] == virama).Key ?? halantOverride;
        var consonants = byKey.Where(kv => kv.Value.Length == 1 && kv.Value[0] >= block + 0x15 && kv.Value[0] <= block + 0x39)
                              .Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var matras = byKey.Where(kv => kv.Value.Length == 1 && kv.Value[0] >= block + 0x3E && kv.Value[0] <= block + 0x4C)
                          .Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Console.WriteLine("block U+" + block.ToString("X4") + "  halant key=" + (halantKey ?? "?")
            + "  consonants=" + consonants.Count + "  matras=" + matras.Count);
        if (halantKey is null) { Console.Error.WriteLine("halant key not found — capture the single keys first"); return 1; }

        var seqs = new List<string>();
        for (int c = 33; c <= 126; c++) seqs.Add(((char)c).ToString());          // singles again, as a control
        foreach (var a in consonants) seqs.Add(a + halantKey);                    // half forms
        foreach (var a in consonants)
            foreach (var b in consonants)
                seqs.Add(a + halantKey + b);                                      // conjuncts
        // a lone matra or halant types as nothing, so probe them after a consonant
        string probe = consonants.Count > 0 ? consonants[0] : "k";
        for (int c = 33; c <= 126; c++) seqs.Add(probe + (char)c);
        foreach (var m in matras) foreach (var a in consonants) seqs.Add(a + m);

        if (maxPairs > 0 && seqs.Count > maxPairs) seqs = seqs.Take(maxPairs).ToList();
        File.WriteAllLines(outFile, seqs, new UTF8Encoding(false));
        Console.WriteLine("wrote " + outFile + " (" + seqs.Count + " sequences)");
        return 0;
    }
}
