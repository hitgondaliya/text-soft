using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WordVis;

public static class Program
{
    public static readonly string Root = @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word";
    public static readonly JsonSerializerOptions JsonOpt = new() { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(), WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [STAThread]
    public static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0] : "";
        switch (cmd)
        {
            case "scan": Scan.Run(); return 0;
            case "docx": DocxWriter.RunAll(args.Skip(1).ToArray()); return 0;
            case "refs": Gdi.RunRefs(args.Skip(1).ToArray()); return 0;
            case "pdf": PdfAnalyze.Run(args.Skip(1).ToArray()); return 0;
            case "emf": EmfAnalyze.Run(args.Skip(1).ToArray()); return 0;
            case "emfplay": EmfPlay.Run(args.Skip(1).ToArray()); return 0;
            case "gdichar": GdiChar.Run(args.Skip(1).ToArray()); return 0;
            case "docxcustom": DocxCustom.Run(args.Skip(1).ToArray()); return 0;
            case "rtfdoc": RtfDoc.Run(args.Skip(1).ToArray()); return 0;
            case "richedit": RichEditTest.Run(args.Skip(1).ToArray()); return 0;
            case "report": Report.Run(args.Skip(1).ToArray()); return 0;
            default: Console.Error.WriteLine("usage: wordvis scan|docx|refs|pdf|richedit|report"); return 1;
        }
    }

    public static string Hex(int cp) => "U+" + cp.ToString("X4");
}

public static class Scan
{
    public static readonly string[] Dirs =
    {
        @"C:\Windows\Fonts",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts"),
    };

    static readonly Regex Indic = new(@"^SHREE-(GUJ|DEV|SAN|BAN|ASS|ORI|PUN|TAM|TEL|KAN|MAL)(7?)-(.*)$", RegexOptions.IgnoreCase);

    public static string GroupOf(Ttf f)
    {
        var m = Indic.Match(f.Family.Trim());
        if (!m.Success) return null;
        string scr = m.Groups[1].Value.ToUpperInvariant();
        string seven = m.Groups[2].Value;
        string rest = m.Groups[3].Value;
        string fn = Path.GetFileName(f.Path).ToUpperInvariant();
        // W<SCR>7nnn.TTF: the 'Shree-<Scr>7-00x' families
        if (Regex.IsMatch(fn, @"^W[A-Z]{3}7\d\d[0-9T]\.TTF$"))
        {
            if (scr == "PUN") return "SHREE-PUN7";   // WPUN700x.TTF are the only SHREE-PUN7-* faces
            return "Shree-" + scr[0] + scr.Substring(1).ToLowerInvariant() + "7-00x" + (rest.EndsWith("A") ? "A" : "");
        }
        return "SHREE-" + scr + seven;
    }

    public static void Run()
    {
        var faces = new List<(string group, Ttf f)>();
        var skipped = new List<string>();
        foreach (var dir in Dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".ttf") continue;
                string fn = Path.GetFileName(file).ToUpperInvariant();
                if (!(fn.StartsWith("S") || fn.StartsWith("W") || fn.StartsWith("M") || fn.StartsWith("N"))) continue;
                Ttf nameOnly;
                try { nameOnly = new Ttf(file, full: false); } catch { continue; }
                if (!nameOnly.Family.StartsWith("SHREE", StringComparison.OrdinalIgnoreCase)) continue;
                string g = GroupOf(nameOnly);
                if (g == null) continue;
                Ttf full;
                try { full = new Ttf(file); } catch (Exception e) { skipped.Add(file + ": " + e.Message); continue; }
                faces.Add((g, full));
            }
        }
        var outArr = new JsonArray();
        foreach (var grp in faces.GroupBy(x => x.group).OrderBy(x => x.Key))
        {
            var list = grp.Select(x => x.f).OrderBy(f => f.WeightClass > 500 || f.SubFamily.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(f => Path.GetFileName(f.Path)).ToList();
            var rep = list[0];
            var facesArr = new JsonArray();
            foreach (var f in list)
            {
                var diffs = new List<string>();
                foreach (var kv in rep.Cmap31)
                    if (!f.Cmap31.TryGetValue(kv.Key, out int g2)) diffs.Add($"{Program.Hex(kv.Key)} missing");
                    else if (g2 != kv.Value) diffs.Add($"{Program.Hex(kv.Key)} gid {kv.Value}->{g2}");
                foreach (var kv in f.Cmap31) if (!rep.Cmap31.ContainsKey(kv.Key)) diffs.Add($"{Program.Hex(kv.Key)} extra");
                facesArr.Add(new JsonObject
                {
                    ["file"] = f.Path,
                    ["family"] = f.Family,
                    ["sub"] = f.SubFamily,
                    ["full"] = f.Full,
                    ["ps"] = f.PsName,
                    ["numGlyphs"] = f.NumGlyphs,
                    ["upem"] = f.UnitsPerEm,
                    ["cmap31Count"] = f.Cmap31.Count,
                    ["cmapHash"] = f.CmapHash(),
                    ["sameCmapAsRep"] = diffs.Count == 0,
                    ["cmapDiffs"] = new JsonArray(diffs.Take(40).Select(s => (JsonNode)s).ToArray()),
                    ["subtables"] = string.Join(" ", f.CmapSubtables),
                    ["fsType"] = f.FsType,
                    ["codePage1"] = "0x" + f.CodePage1.ToString("X8"),
                    ["weight"] = f.WeightClass,
                });
            }
            outArr.Add(new JsonObject
            {
                ["group"] = grp.Key,
                ["rep"] = rep.Path,
                ["repFamily"] = rep.Family,
                ["faceCount"] = list.Count,
                ["allSameCmap"] = facesArr.All(x => (bool)x["sameCmapAsRep"]),
                ["faces"] = facesArr,
            });
            WriteBase(grp.Key, rep, list);
            Console.WriteLine($"{grp.Key,-20} faces={list.Count,3} rep={rep.Family} ({Path.GetFileName(rep.Path)}) cps={rep.Cmap31.Count} glyphs={rep.NumGlyphs} upem={rep.UnitsPerEm} sub={string.Join(" ", rep.CmapSubtables)} fsType={rep.FsType} cp1=0x{rep.CodePage1:X8} allSame={facesArr.All(x => (bool)x["sameCmapAsRep"])}");
        }
        File.WriteAllText(Path.Combine(Program.Root, "families.json"), outArr.ToJsonString(Program.JsonOpt));
        foreach (var s in skipped) Console.WriteLine("SKIP " + s);
    }

    static void WriteBase(string group, Ttf rep, List<Ttf> faces)
    {
        Directory.CreateDirectory(Path.Combine(Program.Root, "fam"));
        var byGid = rep.Cmap31.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
        var sig = new Dictionary<int, string>();
        foreach (var g in rep.Cmap31.Values.Distinct()) sig[g] = rep.OutlineSig(g);
        var bySig = rep.Cmap31.Where(kv => sig[kv.Value] != "empty").GroupBy(kv => sig[kv.Value])
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
        var cps = new JsonArray();
        foreach (var kv in rep.Cmap31)
        {
            int cp = kv.Key, gid = kv.Value;
            var bb = rep.Bbox(gid);
            string s = sig[gid];
            cps.Add(new JsonObject
            {
                ["cp"] = Program.Hex(cp),
                ["gid"] = gid,
                ["adv"] = rep.Adv[gid],
                ["contours"] = rep.Contours(gid),
                ["empty"] = s == "empty",
                ["bbox"] = $"{bb.xMin},{bb.yMin},{bb.xMax},{bb.yMax}",
                ["sig"] = s,
                ["sameGlyphAs"] = new JsonArray(byGid[gid].Where(c => c != cp).Select(c => (JsonNode)Program.Hex(c)).ToArray()),
                ["sameOutlineAs"] = new JsonArray((s == "empty" ? new List<int>() : bySig[s]).Where(c => rep.Cmap31[c] != gid).Select(c => (JsonNode)Program.Hex(c)).ToArray()),
            });
        }
        var o = new JsonObject
        {
            ["family"] = group,
            ["repFace"] = rep.Family,
            ["repFile"] = rep.Path,
            ["upem"] = rep.UnitsPerEm,
            ["numGlyphs"] = rep.NumGlyphs,
            ["winAscent"] = rep.WinAscent,
            ["winDescent"] = rep.WinDescent,
            ["faces"] = new JsonArray(faces.Select(f => (JsonNode)new JsonObject { ["face"] = f.Family + (f.SubFamily != "Regular" ? " " + f.SubFamily : ""), ["file"] = f.Path, ["sameCmap"] = SameCmap(rep, f) }).ToArray()),
            ["cps"] = cps,
        };
        File.WriteAllText(Path.Combine(Program.Root, "fam", group + ".base.json"), o.ToJsonString(Program.JsonOpt));
    }

    static bool SameCmap(Ttf a, Ttf b) => a.Cmap31.Count == b.Cmap31.Count && a.Cmap31.All(kv => b.Cmap31.TryGetValue(kv.Key, out int g) && g == kv.Value);
}
