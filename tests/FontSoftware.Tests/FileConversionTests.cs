using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FlowDocument = System.Windows.Documents.FlowDocument;
using FontFamily = System.Windows.Media.FontFamily;
using WParagraph = System.Windows.Documents.Paragraph;
using WRun = System.Windows.Documents.Run;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>Converter nxt "Files" mode: txt / rtf / docx round trips through a legacy layout.</summary>
public class FileConversionTests
{
    private const string Hindi = "नमस्ते दुनिया, यह परीक्षण है।";

    private static ConversionPipeline Pipeline() =>
        new(new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());

    /// <summary>WPF documents need an STA thread; xunit runs on MTA.</summary>
    private static void Sta(Action body)
    {
        Exception? err = null;
        var t = new Thread(() => { try { body(); } catch (Exception e) { err = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (err is not null) throw err;
    }

    private static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "txsf-conv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void ByteVersusPuaEncodingsAreTold()
    {
        var p = Pipeline();
        Assert.True(p.IsByteEncoding("shree-dev456"));
        Assert.True(p.IsByteEncoding("krutidev"));
        Assert.False(p.IsByteEncoding("shree-devx"));
        Assert.False(p.IsByteEncoding("shree-gujx"));
    }

    [Fact]
    public void TxtUnicodeToLegacyAndBack()
    {
        var p = Pipeline();
        // An 8-bit font: the legacy file must come out as single bytes (Windows-1252).
        var devx = LayoutKey.Legacy("shree-dev456");
        string dir = TempDir();
        string uni = Path.Combine(dir, "in.txt");
        string leg = Path.Combine(dir, "out", "in.txt");
        string back = Path.Combine(dir, "back", "in.txt");
        // No Latin text: in an 8-bit font every byte is a Devanagari glyph, so "line"
        // would rightly come back as letters — exactly what Shree does with it too.
        File.WriteAllText(uni, Hindi + "\r\n123 456", new UTF8Encoding(true));

        var font = new FontFamily("Arial");
        FileConversion.ConvertFile(uni, leg, t => p.Convert(t, LayoutKey.Unicode, devx).Text, true, false, font, "Shree-Dev-0700", false);
        // Legacy bytes: no BOM, one byte per character, nothing lost to '?'.
        byte[] bytes = File.ReadAllBytes(leg);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF);
        Assert.DoesNotContain((byte)'?', bytes);

        FileConversion.ConvertFile(leg, back, t => p.Convert(t, devx, LayoutKey.Unicode, new ConversionOptions { Numerals = NumeralStyle.English }).Text, false, true, font, "Nirmala UI", false);
        string result = File.ReadAllText(back);
        Assert.Equal(Hindi + "\r\n123 456", result);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void RtfKeepsBoldAndSwapsFont()
    {
        Sta(() =>
        {
            var p = Pipeline();
            var devx = LayoutKey.Legacy("shree-devx");
            string dir = TempDir();
            string rtfIn = Path.Combine(dir, "in.rtf");
            string rtfOut = Path.Combine(dir, "out.rtf");

            var doc = new FlowDocument();
            var para = new WParagraph();
            para.Inlines.Add(new WRun("नमस्ते ") { FontWeight = System.Windows.FontWeights.Bold });
            para.Inlines.Add(new WRun("दुनिया"));
            doc.Blocks.Add(para);
            File.WriteAllText(rtfIn, RichTextConversion.ToRtf(doc), Encoding.Latin1);

            var font = new FontFamily("Shree-DevX-0001");
            FileConversion.ConvertFile(rtfIn, rtfOut, t => p.Convert(t, LayoutKey.Unicode, devx).Text, true, false, font, "Shree-DevX-0001", false);

            var outDoc = RichTextConversion.FromRtf(File.ReadAllText(rtfOut, Encoding.Latin1));
            var runs = RichTextConversion.Runs(outDoc).ToList();
            Assert.True(runs.Count >= 2);
            Assert.Equal(System.Windows.FontWeights.Bold, runs[0].FontWeight);
            Assert.Contains("Shree-DevX-0001", File.ReadAllText(rtfOut, Encoding.Latin1));
            Assert.DoesNotContain("./#", RichTextConversion.ToRtf(RichTextConversion.Convert(doc, t => t, new FontFamily(new Uri("file:///C:/x/"), "./#Shree-DevX-0001"), null, false)));
            // Converting the whole text back yields the original words.
            string backText = p.Convert(RichTextConversion.PlainText(outDoc), devx, LayoutKey.Unicode).Text;
            Assert.Equal("नमस्ते दुनिया", backText);
            Directory.Delete(dir, true);
        });
    }

    [Fact]
    public void DocxRunsAreConvertedAndFontStamped()
    {
        var p = Pipeline();
        var devx = LayoutKey.Legacy("shree-devx");
        string dir = TempDir();
        string docxIn = Path.Combine(dir, "in.docx");
        string docxOut = Path.Combine(dir, "out.docx");

        using (var d = WordprocessingDocument.Create(docxIn, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = d.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text("नमस्ते")), new Run(new RunProperties(new Bold()), new Text(" दुनिया"))),
                new Paragraph(new Run(new Text("कीमत 42")))));
            main.Document.Save();
        }

        FileConversion.ConvertFile(docxIn, docxOut, t => p.Convert(t, LayoutKey.Unicode, devx).Text, true, false, new FontFamily("Arial"), "Shree-DevX-0001", false);

        using var outDoc = WordprocessingDocument.Open(docxOut, false);
        var runs = outDoc.MainDocumentPart!.Document.Body!.Descendants<Run>().ToList();
        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.Equal("Shree-DevX-0001", r.RunProperties!.RunFonts!.Ascii!.Value));
        Assert.NotNull(runs[1].RunProperties!.Bold); // formatting kept
        string joined = string.Concat(runs.Take(2).Select(r => r.InnerText));
        Assert.Equal("नमस्ते दुनिया", p.Convert(joined, devx, LayoutKey.Unicode).Text);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void PlainTextOptionDropsFormatting()
    {
        Sta(() =>
        {
            var doc = new FlowDocument();
            var para = new WParagraph();
            para.Inlines.Add(new WRun("a") { FontWeight = System.Windows.FontWeights.Bold });
            para.Inlines.Add(new WRun("b") { FontStyle = System.Windows.FontStyles.Italic });
            doc.Blocks.Add(para);
            var flat = RichTextConversion.Convert(doc, t => t.ToUpperInvariant(), new FontFamily("Arial"), 12, plainText: true);
            var runs = RichTextConversion.Runs(flat).ToList();
            Assert.Single(runs);
            Assert.Equal("AB", runs[0].Text);
            Assert.Equal(System.Windows.FontWeights.Normal, runs[0].FontWeight);
        });
    }
}
