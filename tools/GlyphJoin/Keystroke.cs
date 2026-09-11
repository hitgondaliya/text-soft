using System.IO;
using System.Linq;
using System.Text.Json;

namespace GlyphJoin;

// Keystroke — join two Shree keyboard captures of the SAME key sequences, one
// taken under a legacy font layout and one under the Unicode layout. Shree's own
// engine produced both, so the pair (legacy codes, Unicode codes) for a keystroke
// is ground truth. Single-code results map straight across; multi-code results
// are kept only when one side is a single code, which is where the conjuncts and
// half forms come from.
public static class Keystroke
{
    public static int Run(string legacyCapture, string unicodeCapture, string outJson)
    {
        var leg = Load(legacyCapture);
        var uni = Load(unicodeCapture);

        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int direct = 0, skippedMulti = 0, noPair = 0, empty = 0, conflict = 0;

        foreach (var kv in leg)
        {
            if (!uni.TryGetValue(kv.Key, out var u)) { noPair++; continue; }
            var lCodes = Split(kv.Value);
            var uCodes = Split(u);
            if (lCodes.Length == 0 || uCodes.Length == 0) { empty++; continue; }

            if (lCodes.Length == 1)
            {
                string value = string.Join(" ", uCodes);
                if (table.TryGetValue(lCodes[0], out var prev) && prev != value) { conflict++; continue; }
                table[lCodes[0]] = value;
                direct++;
            }
            else skippedMulti++;
        }

        Console.WriteLine("keystrokes=" + leg.Count + " mapped=" + direct
            + " multi-code-legacy=" + skippedMulti + " empty=" + empty
            + " unpaired=" + noPair + " conflicts=" + conflict);
        File.WriteAllText(outJson, JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("wrote " + outJson + " (" + table.Count + " entries)");
        return 0;
    }

    sealed class Row { public string k { get; set; } = ""; public string v { get; set; } = ""; }

    /// capture.ps1 writes an ARRAY of {k,v} rows — a PowerShell ordered dictionary
    /// resolves a digit-looking key as a positional index, which silently drops
    /// entries, so the capture never keys a dictionary by the keystroke.
    static Dictionary<string, string> Load(string path)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(File.ReadAllText(path))!;
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in rows) if (r.k.Length > 0 && !d.ContainsKey(r.k)) d[r.k] = r.v;
        return d;
    }

    static string[] Split(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Where(c => c != "000D" && c != "000A" && c != "0007")
         .ToArray();
}
