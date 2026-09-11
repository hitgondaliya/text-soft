using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GlyphJoin;

// Aligned — build a raw mapping from two line-aligned files: the Unicode units we
// fed to Shree-Lipi's own converter, and what it gave back in a legacy layout.
//
// This is the general oracle. The shape join needs a Unicode twin of the same
// typeface, which only exists for Shree's own faces; the converter will convert
// into ANY layout it lists — Bhasha Bharati, ISFOC, Akruti, Anu, Terafont — so one
// run of it produces a whole table's worth of ground truth.
public static class Aligned
{
    public static int Run(string unitsFile, string convertedFile, string outJson, bool reverse)
    {
        var units = ReadLines(unitsFile);
        var conv = ReadLines(convertedFile);
        int n = Math.Min(units.Count, conv.Count);

        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int mapped = 0, empty = 0, conflict = 0;
        for (int i = 0; i < n; i++)
        {
            string uni = units[i], leg = conv[i];
            if (uni.Length == 0 || leg.Length == 0) { empty++; continue; }
            // the converter writes the legacy side; `reverse` means the file pair is
            // the other way round (legacy in, Unicode out)
            string key = reverse ? Hex(uni) : Hex(leg);
            string val = reverse ? Hex(leg) : Hex(uni);
            if (table.TryGetValue(key, out var prev) && prev != val) { conflict++; continue; }
            table[key] = val;
            mapped++;
        }

        Console.WriteLine("lines=" + n + " mapped=" + mapped + " blank=" + empty + " conflicts=" + conflict);
        File.WriteAllText(outJson, JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("wrote " + outJson + " (" + table.Count + " entries)");
        return 0;
    }

    static List<string> ReadLines(string path) =>
        File.ReadAllText(path, Encoding.UTF8)
            .Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(s => s.Trim('﻿', ' ', '\t'))
            .ToList();

    static string Hex(string s) =>
        string.Join(" ", s.EnumerateRunes().Select(r => r.Value.ToString("X4")));
}
