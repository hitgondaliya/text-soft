using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WordVis;

public static class Report
{
    static readonly Encoding Cp1252;
    static Report() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); Cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }

    const string TruthDir = @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\624671f5-ee6d-402b-b810-05b52f236677\scratchpad\shree7";

    static string TruthKey(string fam)
    {
        string u = fam.ToUpperInvariant();
        if (u.Contains("GUJ")) return "guj7";
        if (u.Contains("SAN")) return "san7";
        if (u.Contains("DEV")) return "dev7";
        if (u.Contains("BAN") || u.Contains("ASS")) return "ban7";
        return null;
    }

    static bool Is456(string fam) => fam.StartsWith("SHREE-") && !fam.Contains('7');

    // 4/5/6 codes whose glyph differs from the 7 font (family-diff-456-vs-7.json): composer 7 truth does not name them.
    static readonly Dictionary<string, HashSet<int>> Differ456 = new()
    {
        ["GUJ"] = new() { 0x91, 0x92, 0x93, 0x94, 0xB6 },
    };

    static readonly Dictionary<string, Dictionary<string, List<string>>> truthCache = new();

    /// byte (hex "XX") -> Unicode strings the composer writes as exactly that one byte (preferred first, then accepted).
    static Dictionary<string, List<string>> Truth(string key)
    {
        if (key == null) return new();
        if (truthCache.TryGetValue(key, out var c)) return c;
        var res = new Dictionary<string, List<string>>();
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TruthDir, key + "-groundtruth.json")));
        var pref = new List<(string b, string u)>(); var acc = new List<(string b, string u)>();
        foreach (var p in doc.RootElement.GetProperty("truth").EnumerateObject())
        {
            string pb = p.Value.GetProperty("preferred").GetString() ?? "";
            if (pb.Length == 2) pref.Add((pb, p.Name));
            foreach (var a in p.Value.GetProperty("accepted").EnumerateArray()) { string ab = a.GetString() ?? ""; if (ab.Length == 2 && ab != pb) acc.Add((ab, p.Name)); }
        }
        foreach (var (b, u) in pref.Concat(acc)) { if (!res.TryGetValue(b, out var l)) res[b] = l = new(); if (!l.Contains(u)) l.Add(u); }
        truthCache[key] = res;
        return res;
    }

    static int ByteOf(int cp)
    {
        if (cp < 0x100) return cp;
        try { var b = Cp1252.GetBytes(char.ConvertFromUtf32(cp)); return b.Length == 1 ? b[0] : -1; } catch { return -1; }
    }

    static bool IsIndic(int c) => c >= 0x0900 && c <= 0x0DFF;
    static bool IsIndicDigit(int c) => IsIndic(c) && (c & 0x7F) >= 0x66 && (c & 0x7F) <= 0x6F;
    static string Cps(string s) => string.Join(" ", FwdTable.Cps(s).Select(Program.Hex));

    static string Classify(string meaning, int cp)
    {
        if (meaning == null) return null;
        var cs = FwdTable.Cps(meaning).Where(c => c != 0x200C && c != 0x200D && c != 0x25CC).ToList();
        if (cs.Count == 0) return "nothing (" + (meaning.Length == 0 ? "empty" : Cps(meaning)) + ")";
        if (cs.Count == 1 && cs[0] == cp) return "itself";
        if (cs.Count == 1 && IsIndicDigit(cs[0])) return $"script digit {meaning} ({Cps(meaning)})";
        if (cs.Any(IsIndic)) return $"Indic {meaning} ({Cps(meaning)})";
        return $"look-alike {meaning} ({Cps(meaning)})";
    }

    public static void Run(string[] args)
    {
        var fams = args.Length > 0 ? args.ToList() : Lines.Families().ToList();
        var summary = new JsonArray();
        foreach (var fam in fams) summary.Add(One(fam));
        File.WriteAllText(Path.Combine(Program.Root, "summary.json"), new JsonObject
        {
            ["about"] = "Word/RichEdit visibility of every cmap code point of every installed non-Ex Shree font family (one representative face each). Word = Microsoft Word 16.0.18429 (free edition) via COM: the test .docx opened read-only, exported to PDF (ExportAsFixedFormat) and each line read from the PDF content stream (font, code, embedded glyph outline) and from the PDF rendered by Windows.Data.Pdf at 225 dpi (ink compared, 1px-tolerant F1, with the glyph drawn by GDI ExtTextOut ETO_GLYPH_INDEX at 75 px/em); also Word's own picture of each line (Range.EnhMetaFileBits, EMR_EXTTEXTOUTW records). RichEdit = msftedit.dll RICHEDIT50W in a hidden window: typed (WM_CHAR after EM_SETCHARFORMAT to the face) and pasted RTF (the product's RichTextClipboard.BuildRtf bytes via EM_SETTEXTEX ST_SELECTION), then EM_GETCHARFORMAT per character and EM_FORMATRANGE ink.",
            ["families"] = summary,
        }.ToJsonString(Program.JsonOpt));
    }

    static readonly HashSet<string> Visual = new() { "SHREE-PUN U+005B", "SHREE-PUN U+00AB", "Shree-Ban7-00xA U+005D" };

    static JsonNode Get(JsonArray arr, string cp) => arr?.FirstOrDefault(x => (string)x!["cp"]! == cp);

    static JsonObject One(string fam)
    {
        var b = Lines.Base(fam);
        string face = (string)b["repFace"]!;
        var orig = new Ttf((string)b["repFile"]!);
        string F(string kind) => Path.Combine(Program.Root, "fam", fam + "." + kind + ".json");
        JsonArray Load(string kind) => File.Exists(F(kind)) ? JsonNode.Parse(File.ReadAllText(F(kind)))!.AsArray() : null;
        var word = Load("word"); var emf = Load("emf"); var re = Load("richedit");
        string playPath = Path.Combine(Program.Root, "emfplay", fam, "emfplay.json");
        var play = File.Exists(playPath) ? JsonNode.Parse(File.ReadAllText(playPath))!.AsArray() : null;
        var baseCps = b["cps"]!.AsArray().ToDictionary(x => Convert.ToInt32(((string)x!["cp"]!)[2..], 16), x => x!.AsObject());
        var lines = Lines.Load(fam);
        var fwd = Tables.Forward(fam);
        var rev = Tables.Reverse(fam);
        var (wMain, wWt) = Tables.Wiring(fam);
        string tkey = TruthKey(fam);
        var truth = Truth(tkey);
        string scr = fam.ToUpperInvariant().Contains("GUJ") ? "GUJ" : "";
        var revSingle = rev.Select(t => (t.Where + ":" + t.Name, t.Pairs.Where(p => p.from.Length >= 1 && FwdTable.Cps(p.from).Count() == 1).GroupBy(p => FwdTable.Cps(p.from).First()).ToDictionary(g => g.Key, g => g.First().to))).ToList();
        string expFace = null;
        var aLine = Get(re, "U+0041");
        if (aLine != null) expFace = ((string)aLine["typed"]!["faces"]![0]!).Split(':')[1];
        else expFace = re!.Select(x => x!["typed"]!["faces"]!.AsArray()).Where(a => a.Count == 1).Select(a => ((string)a[0]!).Split(':')[1]).GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;

        var cpsOut = new JsonArray();
        var failing = new List<string>();
        var asciiDesc = new List<(int cp, string d)>();
        foreach (var line in lines)
        {
            string hex = Program.Hex(line.Cp);
            bool inCmap = line.Gid >= 0;
            var bc = inCmap ? baseCps[line.Cp] : null;
            var w = Get(word, hex); var e = Get(emf, hex); var r = Get(re, hex);
            bool malformed = inCmap && ((string)bc!["sig"]!).StartsWith("raw") && (int?)w?["refInkPx"] == 0;
            bool empty = inCmap && ((bool)bc!["empty"]! || malformed);

            // ---- Word
            string pdf = (string)w?["pdf"] ?? "no-data";
            double? f1 = (double?)w?["iouExpected"];
            int inkPx = (int?)w?["inkPx"] ?? -1;
            string pdfInk;
            bool pdfOk;
            if (!inCmap)
            {
                pdfInk = inkPx == 0 ? "no ink" : (int?)w?["bestGid"] == 0 && (double?)w?["bestScore"] >= 0.9 ? "ink = the font's .notdef box (gid 0)" : (double?)w?["iouFallback"] >= 0.9 ? $"ink = the real character in {((string)w!["pdf"]!).Split(':').Last()}" : (double?)w?["bestScore"] >= 0.9 ? $"ink = Shree glyph of {(string)w!["bestGidCps"]}" : "ink unidentified";
                pdfOk = false;
            }
            else if (empty)
            {
                pdfOk = (pdf == "expected-glyph" || pdf == "no-glyph-emitted") && inkPx == 0;
                pdfInk = inkPx == 0 ? "no ink (glyph is blank in the font too)" : (double?)w?["bestScore"] >= 0.9 ? $"ink = glyph of {(string)w!["bestGidCps"]} although the font's own glyph is blank" : "ink although the font's glyph is blank";
            }
            else
            {
                pdfOk = pdf == "expected-glyph" && f1 >= 0.9;
                pdfInk = f1 >= 0.9 ? "ink = expected glyph" : inkPx == 0 ? "no ink" : (int?)w?["bestGid"] == 0 && (double?)w?["bestScore"] >= 0.9 ? "ink = the font's .notdef box (gid 0)" : (double?)w?["bestScore"] >= 0.9 ? $"ink = glyph of {(string)w!["bestGidCps"]} (F1 {(double)w!["bestScore"]!:0.00}), not this code point's glyph (F1 {f1:0.00})" : (double?)w?["iouFallback"] >= 0.9 ? "ink = fallback font" : $"ink unidentified (F1 {f1:0.00})";
            }
            string emfV = (string)e?["emf"] ?? "no-data";
            int emfInk = (int?)Get(play, hex)?["blackInkPx"] ?? -1;
            string emfInkV = emfInk < 0 ? "not played" : emfInk == 0 ? "GDI playback draws no ink" : $"GDI playback ink {emfInk} px";
            if (emfV == "shree-font, same code" && !empty && emfInk == 0) emfV = "shree-font, same code, but GDI draws NO ink for it";
            // a blank glyph that Word writes as another blank glyph of the same font (U+00A0 -> U+0020) still shows nothing
            bool emfOk = inCmap && ((emfV == "shree-font, same code" && (empty || emfInk > 0)) || (empty && (emfV == "nothing-emitted" || (emfV == "shree-font, codes U+0020" && baseCps.TryGetValue(0x20, out var sp) && (bool)sp["empty"]!))));
            var codes = w?["runs"]?.AsArray().SelectMany(rr => rr!["codes"]!.AsArray().Select(c => $"{(string)rr["font"]!}/{(string)rr["subtype"]!} code {(string)c!["code"]!} -> {(string)c["match"]!}")).ToList() ?? new();
            bool wordOk = pdfOk && emfOk;
            string wordV = wordOk ? (empty ? "blank as designed (font glyph is empty)" : "draws") :
                !inCmap ? $"not in font cmap; PDF: {pdf}; {pdfInk}; EMF: {emfV}" :
                $"PDF: {pdf}; {pdfInk}; EMF: {emfV}";

            // ---- RichEdit
            JsonObject ReV(string mode)
            {
                var m = r?[mode];
                if (m == null) return new JsonObject { ["verdict"] = "no-data" };
                string landed = (string)m["landed"]!;
                var faces = m["faces"]!.AsArray().Select(x => ((string)x!).Split(':')[1]).ToList();
                bool landedOk = landed == hex[2..];
                bool faceOk = faces.Count > 0 && faces.All(fc => fc.Split('/')[0].Equals(face, StringComparison.OrdinalIgnoreCase));
                bool csSwitched = faces.Any(fc => fc != expFace);
                int ink = (int)m["inkPx"]!;
                double? sc = (double?)m["iouExpected"];
                bool inkOk = !inCmap ? false : empty ? ink == 0 : sc >= 0.9;
                // thin glyphs where the 1px-tolerant F1 fell below 0.9 although the crop shows the right glyph (checked by eye)
                bool eyeChecked = !inkOk && ink > 0 && Visual.Contains(fam + " " + hex);
                if (eyeChecked) inkOk = true;
                string v;
                if (landedOk && faceOk && inkOk) v = empty ? "blank as designed (font glyph is empty)" : eyeChecked ? $"draws (thin glyph: F1 {sc:0.00}, crop checked by eye)" : "draws";
                else
                {
                    var parts = new List<string>();
                    if (!landedOk) parts.Add($"text stored as [{landed}]");
                    if (!faceOk || csSwitched) parts.Add($"font set to {string.Join(",", faces.Distinct())}" + ((string)m["gdiBindsOtherTo"] is string gb ? $" (GDI binds it to {gb})" : ""));
                    if (ink == 0) parts.Add(empty ? "no ink" : inCmap ? $"no ink, advance {(double?)m["advancePx"]:0.#} px kept (font advance {orig.Adv[line.Gid] * 75.0 / orig.UnitsPerEm:0.#} px)" : $"no ink, advance {(double?)m["advancePx"]:0.#} px");
                    else if ((int?)m["bestGid"] == 0 && (double?)m["bestScore"] >= 0.9) parts.Add("ink = the font's .notdef box (gid 0)");
                    else if ((double?)m["iouOtherFace"] >= 0.9) parts.Add($"ink = the real character drawn by {(string)m["gdiBindsOtherTo"] ?? (string)m["otherFace"]}");
                    else if ((double?)m["bestScore"] >= 0.9) parts.Add($"ink = Shree glyph of {(string)m["bestGidCps"]}");
                    else if (inCmap) parts.Add($"ink unidentified (F1 {sc:0.00})");
                    if (!inCmap) parts.Insert(0, "not in font cmap");
                    v = string.Join("; ", parts);
                }
                return new JsonObject { ["verdict"] = v, ["ok"] = landedOk && faceOk && inkOk, ["landed"] = landed, ["faces"] = string.Join(",", faces), ["inkPx"] = ink, ["f1Expected"] = sc, ["advancePx"] = (double?)m["advancePx"] };
            }
            var typed = ReV("typed"); var rtf = ReV("rtf");
            bool reOk = (bool?)typed["ok"] == true && (bool?)rtf["ok"] == true;

            // ---- what the glyph draws
            int by = ByteOf(line.Cp);
            string bhex = by >= 0 ? by.ToString("X2") : null;
            var tr = bhex != null && truth.TryGetValue(bhex, out var tl) ? tl : new List<string>();
            bool truthApplies = tr.Count > 0 && !(Is456(fam) && Differ456.TryGetValue(scr, out var dset) && dset.Contains(by));
            var revs = revSingle.Select(x => (x.Item1, x.Item2.TryGetValue(line.Cp, out var t) ? t : null)).Where(x => x.Item2 != null).ToList();
            string draws;
            if (!inCmap) draws = "not in the font's cmap (another font draws the real character, or nothing)";
            else if (empty) draws = (malformed ? $"nothing: the glyph has no drawable outline (malformed glyf entry, bbox {(string)bc!["bbox"]!}; GDI draws no ink), advance {orig.Adv[line.Gid]}/{orig.UnitsPerEm} em" : $"nothing: blank glyph, advance {orig.Adv[line.Gid]}/{orig.UnitsPerEm} em") + (revs.Count > 0 ? $" (reverse table: {string.Join("; ", revs.Select(x => x.Item1 + " -> " + (x.Item2.Length == 0 ? "\"\"" : Cps(x.Item2))))})" : "");
            else
            {
                var cands = new List<string>();
                foreach (var (src, t) in revs) cands.Add($"{Classify(t, line.Cp)} [reverse {src}]");
                if (truthApplies) cands.Add($"{Classify(tr[0], line.Cp)} [composer {tkey}: byte {bhex} = {string.Join(" | ", tr.Take(4))}]");
                if (cands.Count == 0)
                {
                    // no table pair, no composer truth: compare the glyph with the character itself in common Latin fonts
                    var g = Gdi.Render(face, new[] { (ushort)line.Gid }, true, out _);
                    double bestV = 0; string bestF = "";
                    foreach (var lf in new[] { "Arial", "Times New Roman", "Segoe UI", "Courier New" })
                    {
                        var m = Gdi.Render(lf, char.ConvertFromUtf32(line.Cp).Select(c => (ushort)c).ToArray(), false, out _);
                        double v = Mask.Best(m, g, 6).score;
                        if (v > bestV) { bestV = v; bestF = lf; }
                    }
                    cands.Add(bestV >= 0.75 ? $"itself (visual: matches {bestF} at F1 {bestV:0.00}; no table pair, no composer truth)" : $"unidentified glyph (no table pair, no composer truth; best Latin look-alike {bestF} F1 {bestV:0.00})");
                }
                draws = string.Join("; ", cands.Distinct());
            }
            bool fails = !(inCmap && !empty && wordOk && reOk) && !(inCmap && empty);
            // a blank glyph that PDF/RichEdit leave blank is not a failure; a blank glyph that something inks is
            if (inCmap && empty && (!wordOk || (bool?)typed["ok"] != true || (bool?)rtf["ok"] != true)) fails = true;

            var o = new JsonObject
            {
                ["cp"] = hex,
                ["gid"] = inCmap ? line.Gid : null,
                ["kind"] = line.Kind,
                ["byte"] = bhex,
                ["glyphEmpty"] = inCmap ? empty : null,
                ["glyphMalformed"] = malformed ? true : null,
                ["advance"] = inCmap ? orig.Adv[line.Gid] : null,
                ["sameGlyphAs"] = bc?["sameGlyphAs"]?.DeepClone(),
                ["sameOutlineAs"] = bc?["sameOutlineAs"]?.DeepClone(),
                ["word"] = new JsonObject
                {
                    ["ok"] = wordOk,
                    ["verdict"] = wordV,
                    ["pdf"] = pdf,
                    ["pdfCodes"] = string.Join("; ", codes),
                    ["pdfInk"] = pdfInk,
                    ["pdfInkF1"] = f1,
                    ["emf"] = emfV,
                    ["emfPlaybackInk"] = emfInk,
                    ["emfOuts"] = e?["outs"]?.DeepClone(),
                    ["advanceUnitsPdf"] = w?["advanceUnits"]?.DeepClone(),
                    ["advanceUnitsEmf"] = e?["advanceUnits"]?.DeepClone(),
                },
                ["richedit"] = new JsonObject { ["typed"] = typed, ["rtf"] = rtf },
                ["draws"] = draws,
                ["fails"] = fails,
            };
            if (fails)
            {
                var tabs = new JsonArray();
                string ch = char.ConvertFromUtf32(line.Cp);
                foreach (var t in fwd)
                {
                    var hits = t.Pairs.Where(p => p.to.Contains(ch)).ToList();
                    var post = t.Post.Where(p => p.replace.Contains(ch)).ToList();
                    tabs.Add(new JsonObject
                    {
                        ["table"] = (t.Where == "main" ? "main:" : "worktree:") + t.Name,
                        ["pairsWritingIt"] = hits.Count,
                        ["examples"] = new JsonArray(hits.OrderBy(p => p.from.Length).Take(8).Select(p => (JsonNode)$"{p.from} ({Cps(p.from)}) -> {Cps(p.to)}").ToArray()),
                        ["postRulesWritingIt"] = new JsonArray(post.Select(p => (JsonNode)$"{p.pattern} => {p.replace}  // {p.note}").ToArray()),
                    });
                }
                o["tables"] = tabs;
                var alts = new JsonArray();
                foreach (var a in (bc?["sameGlyphAs"]?.AsArray() ?? new JsonArray()).Concat(bc?["sameOutlineAs"]?.AsArray() ?? new JsonArray()))
                {
                    string ac = (string)a!;
                    var aw = Get(word, ac); var ar = Get(re, ac); var ae = Get(emf, ac);
                    alts.Add(new JsonObject { ["cp"] = ac, ["wordPdf"] = (string)aw?["pdf"], ["wordInkF1"] = (double?)aw?["iouExpected"], ["emf"] = (string)ae?["emf"], ["richeditTyped"] = (string)ar?["typed"]?["faces"]?[0], ["richeditTypedF1"] = (double?)ar?["typed"]?["iouExpected"] });
                }
                o["alternatives"] = alts.Count > 0 ? alts : "none";
                failing.Add(hex);
            }
            if ((line.Cp >= 0x20 && line.Cp <= 0x7E) || Lines.Punct.Contains(line.Cp)) asciiDesc.Add((line.Cp, draws));
            cpsOut.Add(o);
        }
        // the cp1252 punctuation the font maps: also part of step 3
        var famObj = new JsonObject
        {
            ["family"] = fam,
            ["repFace"] = face,
            ["repFile"] = (string)b["repFile"]!,
            ["faces"] = b["faces"]!.DeepClone(),
            ["allFacesSameCmap"] = b["faces"]!.AsArray().All(x => (bool)x!["sameCmap"]!),
            ["wiring"] = new JsonObject { ["main"] = wMain, ["worktree"] = wWt },
            ["truth"] = tkey,
            ["richeditFontFace"] = expFace,
            ["codepoints"] = cpsOut,
        };
        File.WriteAllText(Path.Combine(Program.Root, "fam", fam + ".json"), famObj.ToJsonString(Program.JsonOpt));
        var failList = cpsOut.Where(x => (bool)x!["fails"]!).ToList();
        Console.WriteLine($"{fam}: {cpsOut.Count} cps, failing {failList.Count}: {string.Join(" ", failList.Select(x => (string)x!["cp"]!))}");
        return new JsonObject
        {
            ["family"] = fam,
            ["repFace"] = face,
            ["faces"] = b["faces"]!.AsArray().Count,
            ["allFacesSameCmap"] = famObj["allFacesSameCmap"]!.DeepClone(),
            ["mapped"] = lines.Count(l => l.Gid >= 0),
            ["testedUnmapped"] = lines.Count(l => l.Gid < 0),
            ["wordOk"] = cpsOut.Count(x => (bool)x!["word"]!["ok"]!),
            ["richeditTypedOk"] = cpsOut.Count(x => (bool?)x!["richedit"]!["typed"]!["ok"] == true),
            ["richeditRtfOk"] = cpsOut.Count(x => (bool?)x!["richedit"]!["rtf"]!["ok"] == true),
            ["failing"] = new JsonArray(failList.Select(x => (JsonNode)new JsonObject
            {
                ["cp"] = (string)x!["cp"]!,
                ["word"] = (bool)x["word"]!["ok"]! ? "ok" : (string)x["word"]!["verdict"]!,
                ["richeditTyped"] = (string)x["richedit"]!["typed"]!["verdict"]!,
                ["richeditRtf"] = (string)x["richedit"]!["rtf"]!["verdict"]!,
                ["draws"] = (string)x["draws"]!,
            }).ToArray()),
            ["asciiAndPunct"] = new JsonArray(asciiDesc.Select(a => (JsonNode)$"{Program.Hex(a.cp)} {(a.cp >= 0x20 && a.cp < 0x7F ? "'" + (char)a.cp + "' " : "")}: {a.d}").ToArray()),
        };
    }
}
