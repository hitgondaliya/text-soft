using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;
using FontSoftware.Core.Conversion;

// FullTest — walk the WHOLE product surface the user actually touches:
// every language x font layout x keyboard layout, and for legacy layouts every
// font registered under that encoding.
//
// For each combination it presses every key of the layout, pushes the result
// through the same converter the typing engine uses, and then asks the question
// that actually matters: does the code it produced exist in the font that will
// draw it? A table can round-trip perfectly and still render nothing, because the
// font never had that code. That check needs the installed font file, so the fonts
// directory is scanned once and indexed by family name.
//
//   dotnet run --project tools/FullTest [-- --fonts C:\Windows\Fonts] [--verbose]

// Fonts installed for the current user only live in a second folder — a font
// registered in the config would otherwise be reported as "not installed".
string[] fontDirs =
{
    @"C:\Windows\Fonts",
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
};
bool verbose = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--fonts" && i + 1 < args.Length) fontDirs = new[] { args[++i] };
    else if (args[i] == "--verbose") verbose = true;
}

var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
Console.WriteLine("Loaded " + bundle.Languages.Count + " languages, " + bundle.MappingTables.Count + " mapping tables.");

var cmaps = FontIndex.Build(fontDirs);
Console.WriteLine("Indexed " + cmaps.Count + " installed font families from " + string.Join(" + ", fontDirs));
Console.WriteLine();

var problems = new List<string>();
int comboOk = 0, comboTotal = 0;

foreach (var lang in bundle.Languages)
{
    var layouts = bundle.LayoutsFor(lang.Id);
    Console.WriteLine("=== " + lang.Name + "  (" + lang.FontLayouts.Count + " font layouts, " + layouts.Count + " keyboard layouts)");

    foreach (var fl in lang.FontLayouts)
    {
        // 1. is a font wired to this dropdown entry at all?
        if (string.IsNullOrWhiteSpace(fl.FontId))
        {
            Console.WriteLine("  [ -- ] " + fl.Name.PadRight(26) + "no font wired (dropdown entry does nothing)");
            problems.Add(lang.Name + " / " + fl.Name + ": fontLayout has no fontId");
            continue;
        }

        var font = lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId);
        if (font is null)
        {
            Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "fontId '" + fl.FontId + "' is not a font of this language");
            problems.Add(lang.Name + " / " + fl.Name + ": fontId '" + fl.FontId + "' not found");
            continue;
        }

        // 2. the font layout claims a type — does the font agree?
        bool wantLegacy = fl.Type == FontType.Legacy;
        if (wantLegacy != (font.Type == FontType.Legacy))
        {
            Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "type mismatch: layout says " + fl.Type.ToString() + ", font '" + font.Id + "' is " + font.Type);
            problems.Add(lang.Name + " / " + fl.Name + ": type mismatch with font " + font.Id);
            continue;
        }

        FontConverter? conv = null;
        if (wantLegacy)
        {
            conv = bundle.CreateConverterToLegacy(font.ConversionKey);
            if (conv is null)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + "no Unicode -> " + font.ConversionKey + " table");
                problems.Add(lang.Name + " / " + fl.Name + ": no mapping table for " + font.ConversionKey);
                continue;
            }
        }

        // every font that shares this encoding is offered in the same dropdown
        var sameEncoding = wantLegacy
            ? lang.Fonts.Where(f => f.Type == FontType.Legacy && f.ConversionKey == font.ConversionKey).ToList()
            : new List<FontAsset> { font };

        foreach (var kb in layouts)
        {
            comboTotal++;
            KeyboardLayout kl;
            try { kl = bundle.GetLayout(kb); }
            catch (Exception ex)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + "layout will not load: " + ex.Message);
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": layout load failed");
                continue;
            }

            // press every key, base and shift
            var produced = new List<string>();
            foreach (var k in kl.Keys)
            {
                if (!string.IsNullOrEmpty(k.Base)) produced.Add(k.Base!);
                if (!string.IsNullOrEmpty(k.Shift)) produced.Add(k.Shift!);
                if (!string.IsNullOrEmpty(k.AltGr)) produced.Add(k.AltGr!);
            }
            if (produced.Count == 0)
            {
                Console.WriteLine("  [FAIL] " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + "layout has no keys");
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": empty layout");
                continue;
            }

            var unmapped = new SortedSet<string>(StringComparer.Ordinal);
            var outChars = new SortedSet<char>();
            foreach (string u in produced)
            {
                string outText = u;
                if (conv is not null)
                {
                    var r = conv.Convert(u);
                    outText = r.Text;
                    // plain ASCII that maps to itself is not a gap — a comma stays a
                    // comma in a legacy font, so only flag real script characters
                    foreach (string c in r.UnmappedCharacters) if (c.Any(ch => ch >= 0x80)) unmapped.Add(c);
                }
                foreach (char c in outText) if (c >= 0x20) outChars.Add(c);
            }

            // does every produced code exist in the fonts that would draw it?
            var missingByFont = new List<string>();
            foreach (var f in sameEncoding)
            {
                if (!cmaps.TryGetValue(f.Family, out var codes))
                {
                    missingByFont.Add(f.Family + " (not installed)");
                    continue;
                }
                int missing = outChars.Count(c => !codes.Contains(c));
                if (missing > 0) missingByFont.Add(f.Family + " missing " + missing + "/" + outChars.Count);
            }

            bool ok = unmapped.Count == 0 && missingByFont.Count == 0;
            if (ok) comboOk++;

            if (!ok || verbose)
            {
                string flag = ok ? "  ok  " : "[FAIL]";
                string detail = ok ? produced.Count + " keys -> " + outChars.Count + " codes, all present"
                    : (unmapped.Count > 0 ? "unmapped[" + string.Join("", unmapped) + "] " : "")
                      + (missingByFont.Count > 0 ? string.Join("; ", missingByFont.Take(3)) : "");
                Console.WriteLine("  " + flag + " " + fl.Name.PadRight(26) + kb.Name.PadRight(18) + detail);
            }
            if (!ok)
                problems.Add(lang.Name + " / " + fl.Name + " / " + kb.Name + ": "
                    + (unmapped.Count > 0 ? unmapped.Count + " unmapped " : "")
                    + (missingByFont.Count > 0 ? string.Join("; ", missingByFont.Take(2)) : ""));
        }
    }
    Console.WriteLine();
}

Console.WriteLine("==== combinations " + comboOk + "/" + comboTotal + " fully working ====");
if (problems.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine(problems.Count + " problem(s):");
    foreach (var group in problems.GroupBy(p => p.Split(':')[^1].Trim()).OrderByDescending(g => g.Count()))
        Console.WriteLine("  x" + group.Count() + "  " + group.Key + "   e.g. " + group.First().Split(':')[0]);
}
return problems.Count == 0 ? 0 : 1;

/// Installed fonts indexed by family name -> the set of code points its cmap maps.
static class FontIndex
{
    public static Dictionary<string, HashSet<int>> Build(params string[] dirs)
    {
        var res = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in dirs) {
        if (!Directory.Exists(dir)) continue;
        foreach (var f in Directory.EnumerateFiles(dir, "*.tt?"))
        {
            try
            {
                // Windows ships several Indic Unicode faces inside ONE collection
                // file (Nirmala.ttc). Skipping .ttc reports them as "not installed".
                foreach (var (family, codes) in ReadAll(f))
                {
                    if (family.Length == 0 || codes.Count == 0) continue;
                    if (!res.TryGetValue(family, out var set)) res[family] = codes;
                    else set.UnionWith(codes);
                }
            }
            catch { }
        }
        }
        return res;
    }

    static IEnumerable<(string family, HashSet<int> codes)> ReadAll(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        if (d.Length > 12 && d[0] == (byte)'t' && d[1] == (byte)'t' && d[2] == (byte)'c' && d[3] == (byte)'f')
        {
            uint n = (uint)((d[8] << 24) | (d[9] << 16) | (d[10] << 8) | d[11]);
            for (uint i = 0; i < n && i < 64; i++)
            {
                long o = 12 + i * 4;
                uint off = (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
                yield return ReadAt(d, off);
            }
            yield break;
        }
        yield return ReadAt(d, 0);
    }

    static (string family, HashSet<int> codes) ReadAt(byte[] d, long b)
    {
        ushort U16(long o) => (ushort)((d[o] << 8) | d[o + 1]);
        uint U32(long o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

        var tables = new Dictionary<string, (uint off, uint len)>(StringComparer.Ordinal);
        int num = U16(b + 4);
        for (int i = 0; i < num; i++)
        {
            long o = b + 12 + i * 16;
            tables[Encoding.ASCII.GetString(d, (int)o, 4)] = (U32(o + 8), U32(o + 12));
        }

        var codes = new HashSet<int>();
        if (tables.TryGetValue("cmap", out var cm))
        {
            int n = U16(cm.off + 2);
            for (int i = 0; i < n; i++)
            {
                long rec = cm.off + 4 + i * 8;
                if (U16(rec) != 3 || U16(rec + 2) != 1) continue;
                uint sub = cm.off + U32(rec + 4);
                if (U16(sub) != 4) continue;
                int segX2 = U16(sub + 6), seg = segX2 / 2;
                long endO = sub + 14, startO = endO + segX2 + 2, deltaO = startO + segX2, rangeO = deltaO + segX2;
                for (int s = 0; s < seg; s++)
                {
                    int end = U16(endO + s * 2), start = U16(startO + s * 2);
                    short delta = (short)U16(deltaO + s * 2); int ro = U16(rangeO + s * 2);
                    if (start == 0xFFFF) continue;
                    for (int ch = start; ch <= end; ch++)
                    {
                        int g;
                        if (ro == 0) g = (ch + delta) & 0xFFFF;
                        else
                        {
                            long gi = rangeO + s * 2 + ro + (ch - start) * 2;
                            if (gi + 1 >= d.Length) continue;
                            g = U16(gi); if (g != 0) g = (g + delta) & 0xFFFF;
                        }
                        if (g != 0) codes.Add(ch);
                    }
                }
            }
        }

        string family = "";
        if (tables.TryGetValue("name", out var nm))
        {
            int cnt = U16(nm.off + 2); uint strOff = nm.off + U16(nm.off + 4);
            for (int i = 0; i < cnt; i++)
            {
                long r = nm.off + 6 + i * 12;
                int plat = U16(r), id = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
                if (id != 1 || plat != 3) continue;
                var bytes = new byte[len];
                Array.Copy(d, strOff + off, bytes, 0, len);
                family = Encoding.BigEndianUnicode.GetString(bytes).Trim();
                break;
            }
        }
        return (family, codes);
    }
}
