using System.Text;
using System.Text.Json;
using System.Windows.Documents;
using FontSoftware.Core.Models;

static partial class Program
{
    // ---------------- L6: every code point the Gujarati tables can write, down every output path ----------------
    static int L6(string[] args)
    {
        var chars = new SortedSet<char>();
        foreach (var t in Bundle.MappingTables.Where(m => m.TargetFontId is "shree-guj7" or "shree-guj456" && m.TargetType == FontType.Legacy))
        {
            foreach (var p in t.Pairs) foreach (char c in p.To) chars.Add(c);
            foreach (var r in t.PostRules) foreach (char c in System.Text.RegularExpressions.Regex.Unescape(r.Replace)) if (c != '$' && !char.IsDigit(c)) chars.Add(c);
        }
        // ASCII passes through the tables unchanged (no pair keys on it): it reaches the outputs too
        for (char c = ' '; c <= '~'; c++) chars.Add(c);
        // what the pre-rules mark and must never leak
        foreach (char c in "﷐﷑﷒﷓") chars.Add(c);
        // characters the tables pass through unmapped (no pair): they reach the outputs as themselves
        foreach (char c in "₹॥૱−‐‑―′″→​﻿°") chars.Add(c);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var cp = Encoding.GetEncoding(1252);
        var rows = new List<object>();
        string BuildRtf(string text) => (string)typeof(FontSoftware.Engine.InputInjector).Assembly.GetType("FontSoftware.Engine.RichTextClipboard")!
            .GetMethod("BuildRtf")!.Invoke(null, new object[] { text, "SHREE-GUJ7-0750" })!;
        var font = new System.Windows.Media.FontFamily("SHREE-GUJ7-0750");
        foreach (char c in chars)
        {
            string s = "A" + c + "A";
            // .txt (FileConversion.WriteTxt -> Windows-1252), read back the way FileConversion.ReadTxt reads a legacy file
            byte[] txt = cp.GetBytes(s);
            string txtBack = cp.GetString(txt);
            // .rtf: RichTextConversion.ToRtf (WPF TextRange.Save) then FromRtf
            var doc = FontSoftware.App.Services.RichTextConversion.FromText(s, font);
            string rtf = FontSoftware.App.Services.RichTextConversion.ToRtf(doc);
            var back = FontSoftware.App.Services.RichTextConversion.FromRtf(rtf);
            string rtfBack = FontSoftware.App.Services.RichTextConversion.PlainText(back);
            int at = rtf.IndexOf("A", rtf.IndexOf("\\f0", StringComparison.Ordinal) + 1, StringComparison.Ordinal);
            string rtfBody = Between(rtf, s);
            // typing injector: RTF paste payload (RichTextClipboard.BuildRtf) and the plain text that goes with it
            string pasteRtf = BuildRtf(s);
            string pasteBody = pasteRtf[(pasteRtf.LastIndexOf(@"\f0 ", StringComparison.Ordinal) + 4)..^1];
            // .docx: FileConversion writes the converted string into w:t; OpenXML serialises it as XML text
            string docxBack = DocxRoundTrip(s, out string docxErr);
            rows.Add(new
            {
                ch = ((int)c).ToString("X4"),
                asByte = Cp1252Back(c),
                txt = Convert.ToHexString(txt[1..^1]),
                txtOk = txtBack == s,
                rtfBody,
                rtfOk = rtfBack == s,
                rtfBack = UHex(rtfBack),
                pasteBody,
                docxOk = docxBack == s,
                docxBack = docxErr.Length > 0 ? docxErr : UHex(docxBack),
                injector = c < 0x20 ? "control char sent as a KEYEVENTF_UNICODE key" : "one KEYEVENTF_UNICODE key down/up of U+" + ((int)c).ToString("X4"),
            });
        }
        File.WriteAllText(Path.Combine(A, "results", "l6-output-paths.json"), JsonSerializer.Serialize(rows, Json));
        Console.WriteLine($"{rows.Count} code points");
        return 0;

        static string Between(string rtf, string s)
        {
            // the run text is the part after the last control word before our first 'A'
            int a = rtf.IndexOf(" A", StringComparison.Ordinal);
            if (a < 0) a = rtf.IndexOf("A", StringComparison.Ordinal);
            int e = rtf.IndexOf("A", a + 2, StringComparison.Ordinal);
            for (int k = a + 2; k < rtf.Length; k++) { if (rtf[k] == 'A') { e = k; break; } }
            return e > a ? rtf.Substring(a + 1, Math.Min(e - a, 60)) : rtf;
        }
    }

    static string DocxRoundTrip(string s, out string err)
    {
        err = "";
        try
        {
            using var ms = new MemoryStream();
            using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.Text(s) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }))));
                main.Document.Save();
            }
            ms.Position = 0;
            using var rd = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
            return string.Concat(rd.MainDocumentPart!.Document.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
        }
        catch (Exception ex) { err = "ERROR " + ex.GetType().Name + ": " + ex.Message; return ""; }
    }

    // ---------------- L5: which fonts each Gujarati Shree layout lists, against what is installed ----------------
    static int L5(string[] args)
    {
        var rows = new List<object>();
        foreach (var fl in Gu.FontLayouts)
        {
            string? ownKey = fl.FontId is { } id ? Gu.Fonts.FirstOrDefault(f => f.Id == id)?.ConversionKey : null;
            var listed = Gu.Fonts.Where(f => fl.IsUnicode ? f.Type == FontType.Unicode : f.Type != FontType.Unicode && fl.Includes(f, ownKey))
                .Select(f => new { f.Id, f.Name, f.Family, enc = f.ConversionKey, table = Bundle.CreateConverterToLegacy(f.ConversionKey)?.Table.Id }).ToList();
            rows.Add(new { fl.Id, fl.Name, fl.FontId, fl.Families, listed });
        }
        File.WriteAllText(Path.Combine(A, "results", "l5-fontlayouts.json"), JsonSerializer.Serialize(rows, Json));
        Console.WriteLine(JsonSerializer.Serialize(rows.Take(4), Json));
        return 0;
    }
}
