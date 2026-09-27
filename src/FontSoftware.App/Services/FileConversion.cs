using System.IO;
using System.Text;
using FontFamily = System.Windows.Media.FontFamily;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FontSoftware.App.Services;

/// <summary>
/// Converter nxt's "Files" mode: .txt, .rtf and .docx in, the same kind of file
/// out, with the target layout's font applied so the document opens correctly.
/// Legacy text files are Windows-1252 bytes (the convention every legacy font and
/// our mapping tables follow); Unicode text files are UTF-8 with a BOM.
/// </summary>
public static class FileConversion
{
    public static readonly string[] Extensions = { ".txt", ".rtf", ".docx", ".doc" };

    private static readonly Encoding Cp1252 = GetCp1252();

    private static Encoding GetCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Convert one file. <paramref name="convert"/> is the text transform; the fonts
    /// name the family to stamp on the output (rtf/docx only).
    /// </summary>
    public static void ConvertFile(
        string inputPath,
        string outputPath,
        Func<string, string> convert,
        bool sourceIsUnicode,
        bool targetIsUnicode,
        FontFamily targetFont,
        string targetFontName,
        bool plainText)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        switch (Path.GetExtension(inputPath).ToLowerInvariant())
        {
            case ".txt":
                ConvertTxt(inputPath, outputPath, convert, sourceIsUnicode, targetIsUnicode);
                break;
            case ".rtf":
                if (plainText)
                {
                    var doc = RichTextConversion.FromRtf(File.ReadAllText(inputPath, Cp1252));
                    string text = RichTextConversion.PlainText(doc);
                    WriteTxt(Path.ChangeExtension(outputPath, ".txt"), convert(text), targetIsUnicode);
                }
                else
                {
                    ConvertRtf(inputPath, outputPath, convert, targetFont);
                }
                break;
            case ".docx":
                ConvertDocx(inputPath, outputPath, convert, targetFontName);
                break;
            case ".doc":
                ConvertDoc(inputPath, outputPath, convert, targetFontName);
                break;
            default:
                throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(inputPath)}");
        }
    }

    private static void ConvertTxt(string inputPath, string outputPath, Func<string, string> convert, bool sourceIsUnicode, bool targetIsUnicode)
    {
        string text = ReadTxt(inputPath, sourceIsUnicode);
        // Line by line so a huge file never builds one giant match buffer; the line
        // structure (including a trailing newline or its absence) is kept as is.
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        WriteTxt(outputPath, string.Join("\r\n", lines.Select(convert)), targetIsUnicode);
    }

    private static string ReadTxt(string path, bool unicode)
    {
        byte[] bytes = File.ReadAllBytes(path);
        // A BOM always wins, whatever the layout says.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        return unicode ? Encoding.UTF8.GetString(bytes) : Cp1252.GetString(bytes);
    }

    private static void WriteTxt(string path, string text, bool unicode)
    {
        if (unicode) File.WriteAllText(path, text, new UTF8Encoding(true));
        else File.WriteAllBytes(path, Cp1252.GetBytes(text));
    }

    private static void ConvertRtf(string inputPath, string outputPath, Func<string, string> convert, FontFamily targetFont)
    {
        var doc = RichTextConversion.FromRtf(File.ReadAllText(inputPath, Cp1252));
        var outDoc = RichTextConversion.Convert(doc, convert, targetFont, null, plainText: false);
        File.WriteAllText(outputPath, RichTextConversion.ToRtf(outDoc), Cp1252);
    }

    /// <summary>
    /// The old binary Word format, through Word itself: .doc to .docx, convert that,
    /// and back to .doc. Word is the only thing on the machine that can read a .doc,
    /// and anyone with .doc files has it.
    /// </summary>
    private static void ConvertDoc(string inputPath, string outputPath, Func<string, string> convert, string targetFontName)
    {
        string work = Path.Combine(Path.GetTempPath(), "TextSoft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string asDocx = Path.Combine(work, "in.docx");
        string converted = Path.Combine(work, "out.docx");
        try
        {
            WordDocFormat.SaveAs(inputPath, asDocx, asDocx: true);
            ConvertDocx(asDocx, converted, convert, targetFontName);
            WordDocFormat.SaveAs(converted, outputPath, asDocx: false);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* a temp folder left behind is not worth an error */ }
        }
    }

    private static void ConvertDocx(string inputPath, string outputPath, Func<string, string> convert, string targetFontName)
    {
        File.Copy(inputPath, outputPath, overwrite: true);
        using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
        var main = doc.MainDocumentPart ?? throw new InvalidDataException("Not a Word document.");
        var parts = new List<OpenXmlPart> { main };
        parts.AddRange(main.HeaderParts);
        parts.AddRange(main.FooterParts);
        if (main.FootnotesPart is not null) parts.Add(main.FootnotesPart);
        if (main.EndnotesPart is not null) parts.Add(main.EndnotesPart);

        foreach (var part in parts)
        {
            OpenXmlElement? root = part switch
            {
                MainDocumentPart m => m.Document.Body,
                HeaderPart h => h.Header,
                FooterPart f => f.Footer,
                FootnotesPart fn => fn.Footnotes,
                EndnotesPart en => en.Endnotes,
                _ => null
            };
            if (root is null) continue;

            foreach (var run in root.Descendants<Run>().ToList())
            {
                var texts = run.Elements<Text>().ToList();
                if (texts.Count == 0) continue;
                string joined = string.Concat(texts.Select(t => t.Text));
                string converted = convert(joined);
                texts[0].Text = converted;
                texts[0].Space = SpaceProcessingModeValues.Preserve;
                for (int i = 1; i < texts.Count; i++) texts[i].Remove();

                var props = run.RunProperties ?? run.PrependChild(new RunProperties());
                var fonts = props.RunFonts ?? props.PrependChild(new RunFonts());
                fonts.Ascii = targetFontName;
                fonts.HighAnsi = targetFontName;
                fonts.ComplexScript = targetFontName;
                fonts.EastAsia = targetFontName;
                fonts.AsciiTheme = null;
                fonts.HighAnsiTheme = null;
                fonts.ComplexScriptTheme = null;
                fonts.EastAsiaTheme = null;
            }
            part.RootElement?.Save();
        }
    }
}
