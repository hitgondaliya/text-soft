using System.IO.Compression;
using System.Security;
using System.Text;

/// <summary>Writes small .docx files (no Word involved) holding what the product writes, for a read-only check in Word.</summary>
static class Docx
{
    public static readonly string[] Words =
    {
        "ଓଡ଼ିଆ", "ଭାରତ", "ଭୁବନେଶ୍ୱର", "ଜଗନ୍ନାଥ", "ସ୍ୱାସ୍ଥ୍ୟ", "ମନ୍ତ୍ରୀ", "ସ୍ତ୍ରୀ", "ସନ୍ଧ୍ୟା", "ଅକ୍ଟୋବର", "କର୍ମ", "ଧର୍ମ", "ପୂର୍ବ",
        "କାର୍ଯ୍ୟ", "ବିଦ୍ୟାଳୟ", "ପ୍ରଧାନ", "ଶ୍ରୀ", "ରାଷ୍ଟ୍ର", "କ୍ଷମା", "ଲକ୍ଷ୍ମୀ", "ଜ୍ଞାନ", "ଚିତ୍ର", "ପୌଷ", "ଗୌରବ", "କୈଳାସ", "ଦେଶ",
        "ଏବଂ", "ସିଂହ", "ଗାଁ", "ଦୁଃଖ", "ନଦୀ", "ନୂଆ", "ଅନ୍ନ", "ଚନ୍ଦ୍ର", "ଅଙ୍କ", "ଘଣ୍ଟା", "ମଣ୍ଡଳ", "ଖ୍ରୀଷ୍ଟାବ୍ଦ", "ବର୍ଷ", "ଯୁଦ୍ଧ",
        "ରତ୍ନ", "ଉତ୍ପଳ", "ଆତ୍ମା", "ଧ୍ୟାନ", "ଦଣ୍ଡ", "ଖିଅ", "ଥିଲା", "ଧିର", "ଠିକ୍।", "“ଓଡ଼ିଆ”", "୧୨/୦୮/୨୦୨୪", "ଦୁଇ – ତିନି",
    };
    public static readonly string[] PassWords = { "₹୧୦୦", "୰", "ସୋଽହମ୍", "ବାକ୍‌ଶକ୍ତି" };

    static string Run(string text, string font) =>
        $"<w:r><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:cs=\"{font}\" w:eastAsia=\"{font}\"/><w:sz w:val=\"32\"/><w:szCs w:val=\"32\"/></w:rPr><w:t xml:space=\"preserve\">{SecurityElement.Escape(text)}</w:t></w:r>";

    static string Para(params string[] runs) => "<w:p>" + string.Concat(runs) + "</w:p>";

    public static void Write(string path, string font, IEnumerable<(string Label, string Text)> lines)
    {
        var body = new StringBuilder();
        foreach (var (label, text) in lines)
            body.Append(Para(Run(label + "\t", "Arial"), Run(text, font), Run("\t|", "Arial")));
        string doc = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                     "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body +
                     "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"720\" w:right=\"720\" w:bottom=\"720\" w:left=\"720\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
        const string ct = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>";
        const string rels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";
        if (File.Exists(path)) File.Delete(path);
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content) { using var s = z.CreateEntry(name).Open(); var b = new UTF8Encoding(false).GetBytes(content); s.Write(b, 0, b.Length); }
        Add("[Content_Types].xml", ct); Add("_rels/.rels", rels); Add("word/document.xml", doc);
    }

    public static void Make(string dir)
    {
        Directory.CreateDirectory(dir);
        // every code point the forward table writes for the truth and the words, one per line
        var written = new SortedSet<char>();
        foreach (var r in Truth.Load()) foreach (char c in K.Fwd.ConvertText(r.U)) written.Add(c);
        foreach (var w in Words) foreach (char c in K.Fwd.ConvertText(w)) written.Add(c);
        written.Remove(' ');
        var codeLines = written.Select(c => ($"U+{(int)c:X4} {K.Hex(c.ToString())}", c.ToString())).ToList();
        var wordLines = Words.Select(w => ($"{K.Cps(w).Replace(" ", "")[..Math.Min(16, K.Cps(w).Replace(" ", "").Length)]}", K.Fwd.ConvertText(w))).ToList();
        var passLines = PassWords.Select(w => ($"pass {K.Cps(w).Replace(" ", "")[..Math.Min(12, K.Cps(w).Replace(" ", "").Length)]}", K.Fwd.ConvertText(w))).ToList();
        foreach (var font in new[] { "SHREE-ORI7-0600", "SHREE-ORI-0600", "SHREE-ORI7-0603" })
        {
            Write(Path.Combine(dir, $"codes-{font}.docx"), font, codeLines);
            Write(Path.Combine(dir, $"words-{font}.docx"), font, wordLines);
            Write(Path.Combine(dir, $"pass-{font}.docx"), font, passLines);
        }
        File.WriteAllLines(Path.Combine(dir, "codes.tsv"), codeLines.Select(l => $"{l.Item1}\t{(int)l.Item2[0]}"), new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(dir, "words.tsv"), Words.Select(w => $"{w}\t{K.Hex(K.Fwd.ConvertText(w))}").Concat(PassWords.Select(w => $"{w}\t{K.Hex(K.Fwd.ConvertText(w))}")), new UTF8Encoding(false));
        Console.WriteLine($"DOCX: {codeLines.Count} code points written (one per line), {Words.Length} words, {PassWords.Length} pass-through words, fonts SHREE-ORI7-0600 SHREE-ORI-0600 SHREE-ORI7-0603 -> {dir}");
    }
}
