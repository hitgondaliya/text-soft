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
    /// name the family to stamp on the output (rtf/docx only). <paramref name="readWhole"/>,
    /// when given, sees a .doc's whole text before any of it is converted - a .doc can
    /// only be read through Word, and opening it twice would double the wait.
    /// </summary>
    public static void ConvertFile(
        string inputPath,
        string outputPath,
        Func<string, string> convert,
        bool sourceIsUnicode,
        bool targetIsUnicode,
        FontFamily targetFont,
        string targetFontName,
        bool plainText,
        Action<string>? readWhole = null,
        Func<string, bool>? isSourceFont = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        // Only the text in the source's script is converted and set in the target font:
        // English in a Unicode document stays as it is, in its own font (Shree has no codes
        // for Latin letters - set in a Shree font, "Email" drew ઊળફશલ). From Unicode, the
        // script is told by its characters; from a legacy font, by the run's font when the
        // caller says which fonts are the source's (isSourceFont).
        // (Unicode to Unicode - the digit style, the dictionary - still takes every run.)
        var scope = sourceIsUnicode && targetIsUnicode ? RunScope.All : new RunScope(sourceIsUnicode, isSourceFont);
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
                    ConvertRtf(inputPath, outputPath, convert, targetFont, scope);
                }
                break;
            case ".docx":
                ConvertDocx(inputPath, outputPath, convert, targetFontName, scope);
                break;
            case ".doc":
                ConvertDoc(inputPath, outputPath, convert, targetFontName, readWhole, scope);
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
        else File.WriteAllBytes(path, LegacyBytes(text));
    }

    /// <summary>
    /// Legacy text as the bytes of a .txt file. An 8-bit font holds each of its codes at the
    /// code point of the same number, U+0000-U+00FF, the C1 controls U+0080-U+009F included
    /// (every SHREE-BAN font draws ৱ at U+009E; earlier tables wrote ব্ঢ as U+0081), and
    /// Windows-1252's own characters (– is 96) at theirs. Encoding.GetEncoding(1252) alone
    /// turns the C1 code points into '?', which loses the letter without a word: each is
    /// written as its own byte. A character with no byte at all - one the table passed
    /// through because no code of the fonts draws it (₹ in a Shree font, P3b), which the
    /// converter reports - is left out: never '?' (which the font draws as itself, a wrong
    /// character) and never a "best fit" letter (ā as a), which the legacy font would draw
    /// as a letter of its script.
    /// </summary>
    public static byte[] LegacyBytes(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c <= 'ÿ') { bytes.Add((byte)c); continue; }
            // The line and paragraph separators are line breaks (the tables pass them through, as they do
            // \n): a Windows-1252 file has no byte for them, so they are its line break, CR LF - never '?'.
            if (c is (char)0x2028 or (char)0x2029) { bytes.Add(0x0D); bytes.Add(0x0A); continue; }
            // a character beyond the BMP (two UTF-16 units) has no Windows-1252 byte: left out
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
            bytes.AddRange(Cp1252Strict.GetBytes(c.ToString()));
        }
        return bytes.ToArray();
    }

    /// <summary>Windows-1252 that writes nothing for a character it has no byte for (no '?', no best-fit mapping).</summary>
    private static readonly Encoding Cp1252Strict =
        Encoding.GetEncoding(1252, new EncoderReplacementFallback(""), new DecoderReplacementFallback("?"));

    private static void ConvertRtf(string inputPath, string outputPath, Func<string, string> convert, FontFamily targetFont, RunScope scope)
    {
        var doc = RichTextConversion.FromRtf(File.ReadAllText(inputPath, Cp1252));
        var outDoc = RichTextConversion.Convert(doc, convert, targetFont, null, plainText: false, scope);
        File.WriteAllText(outputPath, RichTextConversion.ToRtf(outDoc), Cp1252);
    }

    /// <summary>
    /// The old binary Word format. Word is the only thing on the machine that can read
    /// a .doc, and anyone with .doc files has it - but Word is asked only to open the
    /// file and to save it again where it already is. It is never asked to change a
    /// file's format, because that is a "Save as", and a Save as can stop on a save
    /// dialog that nobody can see: with Word hidden, the conversion would simply hang.
    /// The document's own XML comes out of Word, goes through the same conversion every
    /// .docx gets, and goes back in.
    /// </summary>
    private static void ConvertDoc(string inputPath, string outputPath, Func<string, string> convert, string targetFontName, Action<string>? readWhole, RunScope scope)
    {
        File.Copy(inputPath, outputPath, overwrite: true);
        WordDocFormat.ConvertInPlace(outputPath, xml =>
        {
            // Word hands the whole document over before a single run is converted.
            if (readWhole is not null)
            {
                var text = new StringBuilder();
                ConvertFlatOpc(xml, s => { text.Append(s).Append(' '); return s; }, targetFontName, scope);
                readWhole(text.ToString());
            }
            return ConvertFlatOpc(xml, convert, targetFontName, scope);
        });
    }

    /// <summary>A whole Word document as one XML string - the shape Word hands its own
    /// content over in - converted and handed back.</summary>
    public static string ConvertFlatOpc(string flatOpc, Func<string, string> convert, string targetFontName) =>
        ConvertFlatOpc(flatOpc, convert, targetFontName, RunScope.All);

    internal static string ConvertFlatOpc(string flatOpc, Func<string, string> convert, string targetFontName, RunScope scope)
    {
        using var doc = WordprocessingDocument.FromFlatOpcString(flatOpc);
        ConvertParts(doc, convert, targetFontName, scope);
        return doc.ToFlatOpcString();
    }

    /// <summary>
    /// Runs that sit next to each other, hold nothing but text and are formatted the
    /// same way, grouped so they can be converted as one piece. Word splits a word
    /// into several runs for reasons of its own - an editing session, a spell-check
    /// pass - and the split falls anywhere, including inside a conjunct.
    /// </summary>
    private static IEnumerable<List<Run>> RunGroups(OpenXmlElement root, bool byFont = false)
    {
        foreach (var parent in root.Descendants<Run>().Select(r => r.Parent!).Distinct().ToList())
        {
            List<Run>? group = null;
            string? groupKey = null;
            foreach (var child in parent.ChildElements.ToList())
            {
                bool plain = child is Run r0 && r0.ChildElements.All(c => c is Text or RunProperties);
                if (!plain)
                {
                    if (group is not null) { yield return group; group = null; groupKey = null; }
                    continue;
                }
                var run = (Run)child;
                string key = FormattingKey(run, byFont);
                if (group is not null && key == groupKey) group.Add(run);
                else
                {
                    if (group is not null) yield return group;
                    group = new List<Run> { run };
                    groupKey = key;
                }
            }
            if (group is not null) yield return group;
        }
    }

    /// <summary>
    /// What makes two runs look the same on the page. The font is left out because the
    /// conversion restamps it anyway, and so are the spell-check and language hints
    /// Word sprinkles through a document without changing how it looks.
    /// </summary>
    private static string FormattingKey(Run run, bool byFont = false)
    {
        var props = run.RunProperties;
        if (props is null) return "";
        var copy = (RunProperties)props.CloneNode(true);
        foreach (var ignorable in copy.ChildElements.Where(c => (c is RunFonts && !byFont) || c is Languages or NoProof).ToList())
            ignorable.Remove();
        return copy.OuterXml;
    }

    private static void ConvertDocx(string inputPath, string outputPath, Func<string, string> convert, string targetFontName, RunScope scope)
    {
        File.Copy(inputPath, outputPath, overwrite: true);
        using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
        ConvertParts(doc, convert, targetFontName, scope);
    }

    /// <summary>Convert the text of every part of a Word document that holds any: the
    /// body, the headers and footers, the footnotes and the endnotes.</summary>
    private static void ConvertParts(WordprocessingDocument doc, Func<string, string> convert, string targetFontName, RunScope scope)
    {
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

            foreach (var group in RunGroups(root, byFont: !scope.SourceIsUnicode && scope.IsSourceFont is not null))
            {
                var texts = group.SelectMany(r => r.Elements<Text>()).ToList();
                if (texts.Count == 0) continue;
                // A word Word split across runs is still one word: convert the whole
                // seam at once, or a vowel sign that is drawn before its consonant
                // ends up on the wrong side of it.
                string joined = string.Concat(texts.Select(t => t.Text));

                // Which of it is the source's text: from a legacy font, the runs in that
                // font; from Unicode, the stretches in the script (English keeps its font).
                var pieces = scope.Pieces(joined, scope.SourceIsUnicode || scope.IsSourceFont is null ? null : RunFontName(doc, group[0]));
                if (!pieces.Any(p => p.Convert)) continue;            // nothing of the source's here: left as it is

                var run = group[0];
                if (pieces.Count == 1)
                {
                    texts[0].Text = convert(joined);
                    texts[0].Space = SpaceProcessingModeValues.Preserve;
                    for (int i = 1; i < texts.Count; i++) texts[i].Remove();
                    foreach (var extra in group.Skip(1)) extra.Remove();
                    Restamp(run, targetFontName);
                    continue;
                }

                // A run that mixes the two is split: the script's stretches converted and
                // in the target font, the rest as it was, each part in the run it came from
                // (the runs of a group can differ in font).
                var starts = new List<int>();
                int at = 0;
                foreach (var r in group) { starts.Add(at); at += string.Concat(r.Elements<Text>().Select(t => t.Text)).Length; }
                Run Piece(Run from, string text, bool conv)
                {
                    var piece = (Run)from.CloneNode(true);
                    foreach (var t in piece.Elements<Text>().ToList()) t.Remove();
                    piece.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    if (conv) Restamp(piece, targetFontName);
                    return piece;
                }
                int pos = 0;
                foreach (var (text, conv) in pieces)
                {
                    if (conv)
                        run.InsertBeforeSelf(Piece(run, convert(text), true));
                    else
                        for (int i = 0; i < group.Count; i++)
                        {
                            int s = starts[i], e = i + 1 < group.Count ? starts[i + 1] : at;
                            int a = Math.Max(s, pos), b = Math.Min(e, pos + text.Length);
                            if (a < b) run.InsertBeforeSelf(Piece(group[i], joined[a..b], false));
                        }
                    pos += text.Length;
                }
                foreach (var r in group) r.Remove();
            }
            part.RootElement?.Save();
        }
    }

    /// <summary>Set a run in the target font, for every script slot (no theme font).</summary>
    private static void Restamp(Run run, string targetFontName)
    {
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

    /// <summary>
    /// The font a run's Latin-range text is drawn in: its own, else its character style's, else
    /// its paragraph style's (and the styles they are based on), else the document default.
    /// Null when it is a theme font or cannot be told.
    /// </summary>
    private static string? RunFontName(WordprocessingDocument doc, Run run)
    {
        static string? Own(RunFonts? f) => f?.Ascii?.Value ?? f?.HighAnsi?.Value;
        if (Own(run.RunProperties?.RunFonts) is { } own) return own;
        var styles = doc.MainDocumentPart?.StyleDefinitionsPart?.Styles;
        string? FromStyle(string? id)
        {
            for (int depth = 0; id is not null && styles is not null && depth < 10; depth++)
            {
                var s = styles.Elements<Style>().FirstOrDefault(x => x.StyleId?.Value == id);
                if (s is null) return null;
                if (Own(s.StyleRunProperties?.RunFonts) is { } f) return f;
                id = s.BasedOn?.Val?.Value;
            }
            return null;
        }
        if (FromStyle(run.RunProperties?.RunStyle?.Val?.Value) is { } rs) return rs;
        var para = run.Ancestors<Paragraph>().FirstOrDefault();
        if (FromStyle(para?.ParagraphProperties?.ParagraphStyleId?.Val?.Value) is { } ps) return ps;
        return Own(styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.RunFonts);
    }
}

/// <summary>
/// Which text of a document a conversion takes (audit 2026-10-18, BUG-03): converting a .docx
/// to Shree-Lipi 7 used to put the Shree font on English runs too, and an e-mail address came
/// out as Gujarati letters. From Unicode, the stretches in an Indic script are converted and get
/// the target font; the rest - English, and the spaces and punctuation that go with it - stays
/// in its own font. From a legacy font, a run is converted when it is in one of the source's
/// fonts (or its font cannot be told); any other run is left as it is.
/// </summary>
public sealed class RunScope
{
    public static readonly RunScope All = new(false, null);

    public RunScope(bool sourceIsUnicode, Func<string, bool>? isSourceFont)
    {
        SourceIsUnicode = sourceIsUnicode;
        IsSourceFont = isSourceFont;
    }

    public bool SourceIsUnicode { get; }
    public Func<string, bool>? IsSourceFont { get; }

    /// <summary>A run's text in pieces, each converted or left: one piece when the whole run goes one way.</summary>
    public List<(string Text, bool Convert)> Pieces(string text, string? fontName)
    {
        if (ReferenceEquals(this, All) || text.Length == 0) return new() { (text, true) };
        if (!SourceIsUnicode)
            return new() { (text, IsSourceFont is null || fontName is null || IsSourceFont(fontName)) };
        return Split(text);
    }

    private enum Kind { Neutral, Script, Other }

    private static Kind KindOf(char c) =>
        c is >= 'ऀ' and <= '෿' or '‌' or '‍' or '︀' ? Kind.Script
        : char.IsLetter(c) ? Kind.Other
        : Kind.Neutral;

    /// <summary>
    /// The script's stretches and the rest. A letter of the script, or of another script, decides;
    /// a space, a digit or a punctuation mark goes with the letters around it: with the script when
    /// script is on both sides of it (or on one side and the text ends on the other), else it stays.
    /// </summary>
    public static List<(string Text, bool Convert)> Split(string text)
    {
        var kinds = text.Select(KindOf).ToArray();
        var convert = new bool[text.Length];
        var left = new Kind[text.Length];
        var last = Kind.Neutral;
        for (int i = 0; i < text.Length; i++) { if (kinds[i] != Kind.Neutral) last = kinds[i]; left[i] = last; }
        last = Kind.Neutral;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            if (kinds[i] != Kind.Neutral) last = kinds[i];
            convert[i] = kinds[i] switch
            {
                Kind.Script => true,
                Kind.Other => false,
                _ => (left[i], last) is (Kind.Script, Kind.Script) or (Kind.Script, Kind.Neutral) or (Kind.Neutral, Kind.Script),
            };
        }
        var pieces = new List<(string, bool)>();
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
            if (i == text.Length || convert[i] != convert[start])
            {
                pieces.Add((text[start..i], convert[start]));
                start = i;
            }
        return pieces;
    }
}
