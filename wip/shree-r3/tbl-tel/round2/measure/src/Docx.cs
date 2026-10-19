using System.IO.Compression;
using System.Text;
using static U;

static partial class Program
{
    /// <summary>A small .docx: each sample in Nirmala UI, then our bytes in each Telugu Shree face, and Shree's composer bytes for comparison.</summary>
    static void Docx(string path)
    {
        string[] words =
        {
            "కః కఁ దుఃఖం అంతఃపురం ఆఁ", "క్ఙ ఙ్ఙ క్ఱ", "క్ర ప్రభుత్వం గ్రామం చక్రం", "రాష్ట్రం స్త్రీ శ్రీ శాస్త్రం", "కై క్కై ఖ్ఖై క్రై రైలు",
            "హైదరాబాద్ కంప్యూటర్ ఫోన్ ఇంటర్నెట్", "లక్ష్మి క్ష్క క్షృ క్ష్ఖ", "క్కృ కృష్ణ వృక్షం", "క్ఝ నిర్ఝరం", "ఋషి ౠ ఽ",
            "\u2018తెలుగు\u2019 \u201Cతెలుగు\u201D \"తెలుగు\"", "క–క—క-క", "₹100 2*3 । ॥ ౧౨౩ 12:30", "తెలుగు, భాష।",
        };
        var faces = new (string Font, string Enc)[] { ("SHREE-TEL7-Apoorva-0900", "shree-tel7"), ("SHREE-TEL-Asmitha-1642", "shree-tel7"), ("Shree-Tel7-001", "shree-tel7w") };
        var body = new StringBuilder();
        void Para(string font, string text, string note)
        {
            string X(string s) => System.Security.SecurityElement.Escape(s)!;
            body.Append($"<w:p><w:r><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\"/><w:sz w:val=\"16\"/></w:rPr><w:t xml:space=\"preserve\">{X(note)}  </w:t></w:r>");
            body.Append($"<w:r><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:cs=\"{font}\" w:eastAsia=\"{font}\"/><w:sz w:val=\"44\"/><w:szCs w:val=\"44\"/></w:rPr><w:t xml:space=\"preserve\">{X(text)}</w:t></w:r></w:p>");
        }
        var log = new List<string>();
        foreach (var w in words)
        {
            Para("Nirmala UI", w, "Unicode (Nirmala UI)");
            foreach (var (font, enc) in faces)
            {
                var r = Fwd[enc].Convert(w);
                Para(font, r.Text, $"ours {font}");
                log.Add($"{w}\t{font}\t{Hex(r.Text)}\treported {string.Join("", r.UnmappedCharacters)}");
            }
            // Shree's own composer bytes, where every syllable of the line is a composer string
            var parts = w.Split(' ');
            if (parts.All(p => Cap.Truth.TryGetValue(p, out var t) && t.Pref.Length > 0))
                Para("SHREE-TEL7-Apoorva-0900", string.Join(" ", parts.Select(p => FromHex(Cap.Truth[p].Pref))), "Shree composer bytes");
        }
        foreach (var u in new[] { "కః", "కఁ", "క్ఙ", "క్ఱ", "మః", "వఁ" })
            if (Cap.Truth.TryGetValue(u, out var ct))
            {
                Para("SHREE-TEL7-Apoorva-0900", FromHex(ct.Pref), "Shree composer " + u + " = " + ct.Pref);
                Para("SHREE-TEL7-Apoorva-0900", Fwd["shree-tel7"].ConvertText(u), "ours " + u + " = " + Hex(Fwd["shree-tel7"].ConvertText(u)));
            }
        string doc = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>"
                     + body + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"720\" w:right=\"720\" w:bottom=\"720\" w:left=\"720\"/></w:sectPr></w:body></w:document>";
        File.Delete(path);
        using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            void Put(string name, string content) { using var s = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); s.Write(content); }
            Put("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Put("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Put("word/document.xml", doc);
        }
        File.WriteAllLines(Path.ChangeExtension(path, ".bytes.tsv"), log, new UTF8Encoding(false));
        Say($"[docx] wrote {path} ({words.Length} samples x {faces.Length} faces)");
    }
}
