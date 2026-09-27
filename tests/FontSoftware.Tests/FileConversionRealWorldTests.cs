using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FontFamily = System.Windows.Media.FontFamily;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// What a real document off a customer's disk looks like, rather than one we built
/// ourselves: Word splits a word across runs for reasons of its own, puts text in
/// tables and text boxes, and keeps the picked font in a style rather than on the run.
/// </summary>
public class FileConversionRealWorldTests
{
    private const string Hindi = "\u0926\u0938\u094D\u0924\u093E\u0935\u0947\u091C \u0932\u093F\u0916\u0940";

    private static ConversionPipeline Pipeline() =>
        new(new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());

    private static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "txsf-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static Func<string, string> ToLegacy(ConversionPipeline p, string enc) =>
        t => p.Convert(t, LayoutKey.Unicode, LayoutKey.Legacy(enc)).Text;

    private static string BackToUnicode(ConversionPipeline p, string enc, string legacy) =>
        p.Convert(legacy, LayoutKey.Legacy(enc), LayoutKey.Unicode).Text;

    /// <summary>Word splits a word across runs; a conjunct must not break at the seam.</summary>
    [Fact]
    public void DocxWordSplitAcrossRunsStillConverts()
    {
        var p = Pipeline();
        string dir = TempDir(), docxIn = Path.Combine(dir, "in.docx"), docxOut = Path.Combine(dir, "out.docx");

        // "दस्तावेज लिखी" as Word would really store it after editing: one word, four runs,
        // split in the middle of a conjunct. Same formatting on every run.
        string[] pieces = { "\u0926\u0938", "\u094D\u0924\u093E", "\u0935\u0947\u091C ", "\u0932\u093F\u0916\u0940" };
        using (var d = WordprocessingDocument.Create(docxIn, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = d.AddMainDocumentPart();
            var para = new Paragraph();
            foreach (string piece in pieces)
                para.Append(new Run(new RunProperties(new RunFonts { Ascii = "Nirmala UI" }), new Text(piece) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
            main.Document = new Document(new Body(para));
            main.Document.Save();
        }

        FileConversion.ConvertFile(docxIn, docxOut, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);

        using var outDoc = WordprocessingDocument.Open(docxOut, false);
        string legacy = outDoc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Equal(Hindi, BackToUnicode(p, "shree-dev456", legacy));
    }

    /// <summary>Text in a table cell is text too.</summary>
    [Fact]
    public void DocxTableCellsAreConverted()
    {
        var p = Pipeline();
        string dir = TempDir(), docxIn = Path.Combine(dir, "in.docx"), docxOut = Path.Combine(dir, "out.docx");
        using (var d = WordprocessingDocument.Create(docxIn, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = d.AddMainDocumentPart();
            var cell = new TableCell(new Paragraph(new Run(new Text(Hindi))));
            main.Document = new Document(new Body(new Table(new TableRow(cell))));
            main.Document.Save();
        }

        FileConversion.ConvertFile(docxIn, docxOut, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);

        using var outDoc = WordprocessingDocument.Open(docxOut, false);
        string legacy = outDoc.MainDocumentPart!.Document.Body!.Descendants<TableCell>().First().InnerText;
        Assert.Equal(Hindi, BackToUnicode(p, "shree-dev456", legacy));
    }

    /// <summary>An empty file is not an error; it converts to an empty file.</summary>
    [Fact]
    public void EmptyTxtConvertsToEmptyFile()
    {
        var p = Pipeline();
        string dir = TempDir(), input = Path.Combine(dir, "empty.txt"), output = Path.Combine(dir, "out", "empty.txt");
        File.WriteAllText(input, "");
        FileConversion.ConvertFile(input, output, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);
        Assert.True(File.Exists(output));
        Assert.Empty(File.ReadAllBytes(output));
    }

    /// <summary>A .txt with no BOM, saved by Notepad as UTF-8, still reads as Unicode.</summary>
    [Fact]
    public void TxtWithoutBomReadsAsUtf8WhenTheLayoutIsUnicode()
    {
        var p = Pipeline();
        string dir = TempDir(), input = Path.Combine(dir, "in.txt"), output = Path.Combine(dir, "out.txt");
        File.WriteAllBytes(input, new UTF8Encoding(false).GetBytes(Hindi));
        FileConversion.ConvertFile(input, output, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);
        Assert.Equal(Hindi, BackToUnicode(p, "shree-dev456", File.ReadAllText(output, Encoding.Latin1)));
    }

    /// <summary>Every extension the picker offers can actually be converted.</summary>
    [Fact]
    public void EveryOfferedExtensionIsHandled()
    {
        var p = Pipeline();
        foreach (string ext in FileConversion.Extensions)
        {
            Assert.True(FileConversion.IsSupported("x" + ext));
            Assert.True(FileConversion.IsSupported("X" + ext.ToUpperInvariant()));
        }
    }

    /// <summary>
    /// The seam that really hurts: a vowel sign that a legacy font draws BEFORE its
    /// consonant. Convert the two sides of the seam on their own and the two glyphs
    /// come out the wrong way round.
    /// </summary>
    [Theory]
    [InlineData("ल", "िखी")]        // ल | िखी
    [InlineData("क्", "षत्र")]  // क् | षत्र
    [InlineData("दस्", "तावेज")] // दस् | तावेज
    public void DocxSeamInsideAWordKeepsTheWord(string head, string tail)
    {
        var p = Pipeline();
        string dir = TempDir(), docxIn = Path.Combine(dir, "in.docx"), docxOut = Path.Combine(dir, "out.docx");
        using (var d = WordprocessingDocument.Create(docxIn, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = d.AddMainDocumentPart();
            var para = new Paragraph();
            foreach (string piece in new[] { head, tail })
                para.Append(new Run(new RunProperties(new RunFonts { Ascii = "Nirmala UI" }), new Text(piece) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
            main.Document = new Document(new Body(para));
            main.Document.Save();
        }

        FileConversion.ConvertFile(docxIn, docxOut, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);

        using var outDoc = WordprocessingDocument.Open(docxOut, false);
        string legacy = outDoc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Equal(head + tail, BackToUnicode(p, "shree-dev456", legacy));
    }

    /// <summary>The same seam in an RTF file: two runs, identical formatting, one word.</summary>
    [Fact]
    public void RtfSeamInsideAWordKeepsTheWord()
    {
        Exception? err = null;
        var t = new Thread(() =>
        {
            try
            {
                var p = Pipeline();
                string dir = TempDir(), rtfIn = Path.Combine(dir, "in.rtf"), rtfOut = Path.Combine(dir, "out.rtf");
                var doc = new System.Windows.Documents.FlowDocument();
                var para = new System.Windows.Documents.Paragraph();
                para.Inlines.Add(new System.Windows.Documents.Run("ल"));
                para.Inlines.Add(new System.Windows.Documents.Run("िखी"));
                doc.Blocks.Add(para);
                File.WriteAllText(rtfIn, RichTextConversion.ToRtf(doc), Encoding.Latin1);

                FileConversion.ConvertFile(rtfIn, rtfOut, ToLegacy(p, "shree-dev456"), true, false, new FontFamily("Arial"), "SHREE-DEV-0714", false);

                var outDoc = RichTextConversion.FromRtf(File.ReadAllText(rtfOut, Encoding.Latin1));
                Assert.Equal("लिखी", BackToUnicode(p, "shree-dev456", RichTextConversion.PlainText(outDoc)));
            }
            catch (Exception e) { err = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (err is not null) throw err;
    }

    /// <summary>
    /// The .doc path never asks Word to change a file's format; it takes the whole
    /// document as one XML string, converts that, and hands it back. This is that
    /// conversion, tested without Word in the room.
    /// </summary>
    [Fact]
    public void FlatOpcXmlIsConvertedLikeAnyOtherWordDocument()
    {
        var p = Pipeline();
        string dir = TempDir(), docxIn = Path.Combine(dir, "in.docx");
        using (var d = WordprocessingDocument.Create(docxIn, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = d.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text("ल")), new Run(new Text("िखी"))),
                new Paragraph(new Run(new RunProperties(new Bold()), new Text(Hindi)))));
            main.Document.Save();
        }

        string flat;
        using (var d = WordprocessingDocument.Open(docxIn, false)) flat = d.ToFlatOpcString();

        string converted = FileConversion.ConvertFlatOpc(flat, ToLegacy(p, "shree-dev456"), "SHREE-DEV-0714");

        using var outDoc = WordprocessingDocument.FromFlatOpcString(converted);
        var paras = outDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();
        Assert.Equal(2, paras.Count);
        Assert.Equal("लिखी", BackToUnicode(p, "shree-dev456", paras[0].InnerText));
        Assert.Equal(Hindi, BackToUnicode(p, "shree-dev456", paras[1].InnerText));
        var run = paras[1].Elements<Run>().First();
        Assert.NotNull(run.RunProperties!.Bold);
        Assert.Equal("SHREE-DEV-0714", run.RunProperties!.RunFonts!.Ascii!.Value);
    }
}
