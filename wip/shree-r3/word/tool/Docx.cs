using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json.Nodes;

namespace WordVis;

public sealed class Line
{
    public int Cp;
    public int Gid = -1;          // -1: not in the font's cmap
    public string Kind = "";      // "mapped" | "unmapped-written" | "unmapped-punct"
}

public static class Lines
{
    public static readonly int[] Punct = { 0x2013, 0x2014, 0x2018, 0x2019, 0x201A, 0x201B, 0x201C, 0x201D, 0x201E, 0x2022, 0x2026, 0x00B7, 0x00A0, 0x00AD };
    public static int PerPage = int.Parse(Environment.GetEnvironmentVariable("WV_PERPAGE") ?? "8");

    public static JsonObject Base(string family) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "fam", family + ".base.json")))!.AsObject();

    public static IEnumerable<string> Families() =>
        Directory.GetFiles(Path.Combine(Program.Root, "fam"), "*.base.json").Select(f => Path.GetFileName(f)[..^".base.json".Length]).OrderBy(x => x, StringComparer.Ordinal);

    public static List<Line> Build(string family)
    {
        var b = Base(family);
        var cmap = new SortedDictionary<int, int>();
        foreach (var c in b["cps"]!.AsArray()) cmap[Convert.ToInt32(((string)c!["cp"])[2..], 16)] = (int)c["gid"]!;
        var lines = new List<Line>();
        foreach (var kv in cmap) if (kv.Key >= 0x20) lines.Add(new Line { Cp = kv.Key, Gid = kv.Value, Kind = "mapped" });
        var extra = new SortedSet<int>();
        foreach (var t in Tables.Forward(family)) foreach (var cp in t.Written()) if (cp >= 0x20 && !cmap.ContainsKey(cp) && cp < 0xFDD0) extra.Add(cp);
        foreach (var cp in extra) lines.Add(new Line { Cp = cp, Kind = "unmapped-written" });
        foreach (var cp in Punct) if (!cmap.ContainsKey(cp) && !extra.Contains(cp)) lines.Add(new Line { Cp = cp, Kind = "unmapped-punct" });
        return lines;
    }

    public static List<Line> Load(string family)
    {
        var arr = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "fam", family + ".lines.json")))!.AsArray();
        return arr.Select(n => new Line { Cp = Convert.ToInt32(((string)n!["cp"])[2..], 16), Gid = (int)n["gid"]!, Kind = (string)n["kind"]! }).ToList();
    }
}

public static class DocxWriter
{
    public static void RunAll(string[] args)
    {
        if (args.Length > 0 && args[0] == "--slice")
        {
            // --slice <fam> <start> <count> <outName>
            string sf = args[1];
            var sl = Lines.Load(sf).Skip(int.Parse(args[2])).Take(int.Parse(args[3])).Select(l => l.Cp).ToList();
            Write(Path.Combine(Program.Root, "fam", args[4] + ".docx"), (string)Lines.Base(sf)["repFace"]!, sl);
            Console.WriteLine($"slice {args[4]}: {string.Join(" ", sl.Select(Program.Hex))}");
            return;
        }
        var fams = args.Length > 0 ? args.ToList() : Lines.Families().ToList();
        foreach (var fam in fams)
        {
            var b = Lines.Base(fam);
            string face = (string)b["repFace"]!;
            var lines = Lines.Build(fam);
            var arr = new JsonArray(lines.Select(l => (JsonNode)new JsonObject { ["cp"] = Program.Hex(l.Cp), ["gid"] = l.Gid, ["kind"] = l.Kind }).ToArray());
            File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".lines.json"), arr.ToJsonString(Program.JsonOpt));
            string path = Path.Combine(Program.Root, "fam", fam + ".docx");
            Write(path, face, lines.Select(l => l.Cp).ToList());
            Console.WriteLine($"{fam}: {lines.Count} lines ({lines.Count(l => l.Kind == "mapped")} mapped, {lines.Count(l => l.Kind != "mapped")} unmapped) face={face} -> {path}");
        }
    }

    const string Arial = "Arial";

    static string Run(string font, string color, int halfPts, string text)
    {
        string f = SecurityElement.Escape(font);
        return $"<w:r><w:rPr><w:rFonts w:ascii=\"{f}\" w:hAnsi=\"{f}\" w:cs=\"{f}\" w:eastAsia=\"{f}\"/><w:color w:val=\"{color}\"/><w:sz w:val=\"{halfPts}\"/><w:szCs w:val=\"{halfPts}\"/></w:rPr><w:t xml:space=\"preserve\">{Esc(text)}</w:t></w:r>";
    }

    static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c == '&') sb.Append("&amp;");
            else if (c == '<') sb.Append("&lt;");
            else if (c == '>') sb.Append("&gt;");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public static void Write(string path, string face, List<int> cps)
    {
        var body = new StringBuilder();
        for (int i = 0; i < cps.Count; i++)
        {
            bool pageStart = i % Lines.PerPage == 0 && i > 0;
            body.Append("<w:p><w:pPr>");
            if (pageStart) body.Append("<w:pageBreakBefore/>");
            body.Append("<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"1440\" w:lineRule=\"atLeast\"/></w:pPr>");
            body.Append(Run(Arial, "0000FF", 16, Program.Hex(cps[i]) + "  "));
            body.Append(Run(Arial, "FF0000", 48, "H    "));
            body.Append(Run(face, "000000", 48, char.ConvertFromUtf32(cps[i])));
            body.Append(Run(Arial, "FF0000", 48, "    H"));
            body.Append("</w:p>");
        }
        string doc = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body +
            "<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"" + (Environment.GetEnvironmentVariable("WV_PAGEH") ?? "15840") + "\"/><w:pgMar w:top=\"720\" w:right=\"720\" w:bottom=\"720\" w:left=\"720\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
        const string ct = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>";
        const string rels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";
        if (File.Exists(path)) File.Delete(path);
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(content);
        }
        Add("[Content_Types].xml", ct);
        Add("_rels/.rels", rels);
        Add("word/document.xml", doc);
    }
}

public static class DocxCustom
{
    // docxcustom <name> <face> <U+XXXX>...
    public static void Run(string[] a)
    {
        var cps = a.Skip(2).Select(x => Convert.ToInt32(x.Replace("U+", ""), 16)).ToList();
        DocxWriter.Write(Path.Combine(Program.Root, "fam", a[0] + ".docx"), a[1], cps);
        Console.WriteLine($"{a[0]}.docx: {string.Join(" ", cps.Select(Program.Hex))} in {a[1]}");
    }
}
