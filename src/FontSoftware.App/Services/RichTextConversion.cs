using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FontSoftware.App.Services;

/// <summary>
/// Converts a formatted document run by run, keeping bold/italic/size/colour and
/// swapping the font to the target layout's font — which is what makes pasted
/// Word text come out the other side still looking like the original. The text
/// transform itself is whatever the caller supplies (the conversion pipeline).
/// </summary>
public static class RichTextConversion
{
    /// <summary>Deep-copy <paramref name="source"/> as RTF, then convert every run in place.</summary>
    public static FlowDocument Convert(FlowDocument source, Func<string, string> convert, FontFamily targetFont, double? fontSize, bool plainText)
    {
        var doc = Clone(source);
        if (plainText)
        {
            // "Get Plain Text": one paragraph per line, no formatting at all.
            string text = PlainText(doc);
            var flat = new FlowDocument { FontFamily = targetFont };
            if (fontSize is { } fs) flat.FontSize = fs;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                flat.Blocks.Add(new Paragraph(new Run(convert(line))));
            return flat;
        }

        doc.FontFamily = targetFont;
        if (fontSize is { } size) doc.FontSize = size;
        foreach (var run in Runs(doc).ToList())
        {
            run.Text = convert(run.Text);
            run.FontFamily = targetFont;
            if (fontSize is { } s) run.FontSize = s;
        }
        return doc;
    }

    /// <summary>Point every run (and the document) at <paramref name="font"/>, keeping the rest of the formatting.</summary>
    public static void Restamp(FlowDocument doc, FontFamily font)
    {
        doc.FontFamily = font;
        foreach (var run in Runs(doc).ToList()) run.FontFamily = font;
    }

    /// <summary>The whole document as plain text (paragraphs joined by newlines).</summary>
    public static string PlainText(FlowDocument doc) =>
        new TextRange(doc.ContentStart, doc.ContentEnd).Text.TrimEnd('\r', '\n');

    public static string ToRtf(FlowDocument doc)
    {
        using var ms = new MemoryStream();
        new TextRange(doc.ContentStart, doc.ContentEnd).Save(ms, DataFormats.Rtf);
        string rtf = System.Text.Encoding.ASCII.GetString(ms.ToArray());
        // A bundled font is addressed as "./#Family" inside the app; Word and the
        // clipboard want the bare family name in the RTF font table.
        return rtf.Replace(@"\fcharset0 ./#", @"\fcharset0 ").Replace(@"{\f0 ./#", @"{\f0 ");
    }

    public static FlowDocument FromRtf(string rtf)
    {
        var doc = new FlowDocument();
        using var ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(rtf));
        try
        {
            new TextRange(doc.ContentStart, doc.ContentEnd).Load(ms, DataFormats.Rtf);
        }
        catch
        {
            doc = FromText(rtf);
        }
        return doc;
    }

    public static FlowDocument FromText(string text, FontFamily? font = null, double? size = null)
    {
        var doc = new FlowDocument();
        if (font is not null) doc.FontFamily = font;
        if (size is { } s) doc.FontSize = s;
        foreach (string line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            doc.Blocks.Add(new Paragraph(new Run(line)));
        return doc;
    }

    public static FlowDocument Clone(FlowDocument source)
    {
        using var ms = new MemoryStream();
        new TextRange(source.ContentStart, source.ContentEnd).Save(ms, DataFormats.Xaml);
        ms.Position = 0;
        var doc = new FlowDocument();
        new TextRange(doc.ContentStart, doc.ContentEnd).Load(ms, DataFormats.Xaml);
        return doc;
    }

    /// <summary>Every <see cref="Run"/> in reading order, including those inside lists and tables.</summary>
    public static IEnumerable<Run> Runs(FlowDocument doc)
    {
        for (var p = doc.ContentStart; p is not null && p.CompareTo(doc.ContentEnd) < 0; p = p.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart && p.GetAdjacentElement(LogicalDirection.Forward) is Run r)
                yield return r;
        }
    }
}
