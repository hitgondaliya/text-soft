using System.Text.Json;
using FontSoftware.Core.Conversion;

static partial class Program
{
    /// <summary>
    /// Every string of up to three characters over a Gujarati alphabet (letters, signs, virama,
    /// nukta, joiners, symbols, space, punctuation) through the guj7 and guj456 forward tables:
    /// anything in the output that is not a font code (a byte as Windows-1252 maps it) is a leak -
    /// a private marker (U+FDDx) or Gujarati Unicode left in legacy text. Then each output is read
    /// back and written again: the bytes must come back the same (legacy -> Unicode -> legacy).
    /// </summary>
    static int Fuzz(string[] args)
    {
        string alpha = "કખગઙજટડતદનપબમયરલવશષસહળઞ" + "અઆઇઉએઓઋઍઑ" + "ાિીુૂૃૄૅેૈૉોૌંઁઃ઼્" + "‌‍" + "ૐઽ૰।" + " .૧";
        var font = new HashSet<char>();
        for (int b = 0x20; b <= 0xFF; b++) font.Add(CharOfByte(b));
        var leaks = new Dictionary<string, List<object>>();
        var roundTrip = new Dictionary<string, List<object>>();
        int n = 0;
        foreach (var (name, fwd, rev) in new[] { ("guj7", Fwd7, Rev7), ("guj456", Fwd456, Rev456) })
        {
            var lk = leaks[name] = new List<object>();
            var rt = roundTrip[name] = new List<object>();
            var seen = new HashSet<string>();
            foreach (char a in alpha) foreach (char b in alpha) foreach (char c in alpha)
            {
                string s = new string(new[] { a, b, c });
                n++;
                var r = fwd.Convert(s);
                var bad = r.Text.Where(ch => !font.Contains(ch)).Distinct().ToList();
                if (bad.Count > 0)
                {
                    string kind = string.Join(",", bad.Select(ch => "U+" + ((int)ch).ToString("X4")));
                    if (lk.Count < 4000) lk.Add(new { input = s, inputHex = UHex(s), output = Hex(r.Text), leaked = kind });
                    continue;
                }
                // read back and write again
                if (!seen.Add(r.Text)) continue;
                string back = rev.ConvertText(r.Text);
                string again = fwd.ConvertText(back);
                if (again != r.Text && rt.Count < 4000)
                    rt.Add(new { input = s, inputHex = UHex(s), legacy = Hex(r.Text), readBack = back, readBackHex = UHex(back), writtenAgain = Hex(again) });
            }
        }
        // summarise leaks by leaked code point and by the shape of the input
        var summary = leaks.ToDictionary(kv => kv.Key, kv => kv.Value.Cast<dynamic>().GroupBy(x => (string)x.leaked).Select(g => new { leaked = g.Key, count = g.Count(), examples = g.Take(12).Select(x => (string)x.input + " = " + (string)x.output).ToArray() }).OrderByDescending(g => g.count).ToList());
        File.WriteAllText(Path.Combine(A, "results", "fuzz-forward-leaks.json"), JsonSerializer.Serialize(new { strings = n, alphabet = alpha, summary, roundTripSample = roundTrip.ToDictionary(kv => kv.Key, kv => new { count = kv.Value.Count, examples = kv.Value }) }, Json));
        foreach (var (k, v) in summary) { Console.WriteLine($"{k}: {leaks[k].Count} leaking strings; round trip changed {roundTrip[k].Count}"); foreach (var g in v.Take(15)) Console.WriteLine($"   {g.leaked}: {g.count}  e.g. {string.Join(" | ", g.examples.Take(4))}"); }
        return 0;
    }
}
