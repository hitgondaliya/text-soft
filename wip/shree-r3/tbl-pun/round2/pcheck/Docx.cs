using System.IO.Compression;
using System.Security;
using System.Text;
using FontSoftware.Core.Conversion;

/// <summary>A minimal .docx (no Word involved in making it): one paragraph per line, every run in one font.</summary>
static class Docx
{
    public static void Write(string path, string font, IEnumerable<string> lines)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Put(string name, string text)
        {
            var e = zip.CreateEntry(name);
            using var s = new StreamWriter(e.Open(), new UTF8Encoding(false));
            s.Write(text);
        }
        Put("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Put("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
        string f = SecurityElement.Escape(font);
        foreach (var l in lines)
            sb.Append($"<w:p><w:r><w:rPr><w:rFonts w:ascii=\"{f}\" w:hAnsi=\"{f}\" w:cs=\"{f}\" w:eastAsia=\"{f}\"/><w:sz w:val=\"28\"/></w:rPr><w:t xml:space=\"preserve\">{SecurityElement.Escape(l)}</w:t></w:r></w:p>");
        sb.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"720\" w:right=\"720\" w:bottom=\"720\" w:left=\"720\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>");
        Put("word/document.xml", sb.ToString());
    }
}
