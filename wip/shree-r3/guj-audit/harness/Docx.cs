using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FontSoftware.Core.Conversion;

static partial class Program
{
    /// <summary>
    /// A Unicode .docx with a Gujarati run and an English run, and a Unicode .txt with ₹ and ॥,
    /// converted by the app's own FileConversion.ConvertFile to Shree-Lipi 7 (the Converter's
    /// Files mode): what each run holds afterwards and which font it is stamped with.
    /// </summary>
    static int DocxMode(string[] args)
    {
        string dir = Path.Combine(A, "results", "files");
        Directory.CreateDirectory(dir);
        string src = Path.Combine(dir, "mixed-unicode.docx"), dst = Path.Combine(dir, "mixed-to-guj7.docx");
        using (var doc = WordprocessingDocument.Create(src, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            Run R(string text, string font) => new Run(new RunProperties(new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font }), new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
            main.Document = new Document(new Body(
                new Paragraph(R("ગુજરાત સરકાર ", "Nirmala UI"), new Run(new RunProperties(new Bold()), new Text("Email: info@gujarat.gov.in [1] & \"Notice\" #5 ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve })),
                new Paragraph(R("કિંમત ₹૫૦૦ – સત્યમેવ જયતે॥ ગુરુ રૂપિયા", "Nirmala UI"))));
            main.Document.Save();
        }
        var pipeline = new ConversionPipeline(Bundle);
        Func<string, string> conv = s => pipeline.Convert(s, LayoutKey.Unicode, LayoutKey.Legacy("shree-guj7")).Text;
        FontSoftware.App.Services.FileConversion.ConvertFile(src, dst, conv, true, false, new System.Windows.Media.FontFamily("SHREE-GUJ7-0750"), "SHREE-GUJ7-0750", false);
        var runs = new List<object>();
        using (var rd = WordprocessingDocument.Open(dst, false))
            foreach (var r in rd.MainDocumentPart!.Document.Body!.Descendants<Run>())
            {
                string t = string.Concat(r.Elements<Text>().Select(x => x.Text));
                runs.Add(new { font = r.RunProperties?.RunFonts?.Ascii?.Value, text = t, hex = Hex(t) });
            }
        // .txt: Unicode in, Shree-Lipi 7 out (Windows-1252 bytes)
        string tsrc = Path.Combine(dir, "unicode.txt"), tdst = Path.Combine(dir, "unicode-to-guj7.txt");
        File.WriteAllText(tsrc, "કિંમત ₹૫૦૦ – સત્યમેવ જયતે॥ ૐ ૩૦° ગુરુ\r\n", new System.Text.UTF8Encoding(true));
        FontSoftware.App.Services.FileConversion.ConvertFile(tsrc, tdst, conv, true, false, new System.Windows.Media.FontFamily("SHREE-GUJ7-0750"), "SHREE-GUJ7-0750", false);
        string txtHex = Convert.ToHexString(File.ReadAllBytes(tdst));
        File.WriteAllText(Path.Combine(A, "results", "l6-files.json"), JsonSerializer.Serialize(new { docxRuns = runs, txtOut = txtHex, txtIn = File.ReadAllText(tsrc) }, Json));
        foreach (var r in runs) Console.WriteLine(JsonSerializer.Serialize(r, Json).Replace("\n", " "));
        Console.WriteLine("txt: " + txtHex);
        return 0;
    }
}
