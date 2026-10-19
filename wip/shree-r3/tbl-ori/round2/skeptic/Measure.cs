using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Conversion;

static class Measure
{
    // ---------------------------------------------------------------- forward against the raw capture
    public static void Fwd(string outDir)
    {
        var rows = Truth.Load();
        var cls = new Dictionary<string, int>();
        using var w = K.Out(outDir, "fwd-detail.tsv");
        int nineCNoNa = 0, repBad = 0, naRows = 0, writes9C = 0, ownZw = 0;
        var naRowsNotOnlyNa = 0;
        foreach (var r in rows)
        {
            if (r.Pref.Split(' ').Contains("9C") && !r.U.Contains('ନ')) nineCNoNa++;
            var res = K.Fwd.Convert(r.U);
            string got = K.Hex(res.Text);
            if (got.Split(' ').Contains("9C")) writes9C++;
            var passChars = r.U.Where(c => c is '₹' or '୰' or 'ଽ').Select(c => c.ToString()).ToHashSet();
            string c;
            if (got == r.Pref) c = "1 composer-preferred";
            else if (r.Acc.Contains(got)) c = "2 composer-accepted";
            else if (got == K.Ex(r.Pref, true, false) || r.Acc.Any(a => got == K.Ex(a, true, false))) c = "3 exception: na 9C->5B";
            else if (got == K.Ex(r.Pref, true, true) || r.Acc.Any(a => got == K.Ex(a, true, true))) c = "4 exception: P3b pass-through";
            else if (r.Alt.Contains(got)) c = "5 FAIL: matches only a composer alt (other typing order)";
            else c = "6 FAIL";
            if (c.StartsWith("3")) { naRows++; if (!r.U.Contains('ନ')) naRowsNotOnlyNa++; }
            var rep = res.UnmappedCharacters.ToHashSet();
            bool repOk = c.StartsWith("4") ? rep.SetEquals(passChars) : rep.Count == 0;
            if (!repOk) { repBad++; c += " / REPORT-WRONG"; }
            cls[c] = cls.GetValueOrDefault(c) + 1;
            if (!c.StartsWith("1") && !c.StartsWith("2"))
                w.WriteLine($"{c}\t{r.U}\t[{K.Cps(r.U)}]\tpref {r.Pref}\tacc {string.Join(" | ", r.Acc)}\tgot {got}\treported {string.Join("", rep)}");
        }
        Console.WriteLine($"FORWARD raw capture truth rows {rows.Count}");
        foreach (var (k, v) in cls.OrderBy(k => k.Key)) Console.WriteLine($"  {k}: {v}");
        Console.WriteLine($"  rows whose composer bytes hold 9C but whose text has no ନ: {nineCNoNa}");
        Console.WriteLine($"  rows explained only by 9C->5B: {naRows} (of them without ନ in the text: {naRowsNotOnlyNa}); rows whose output holds 9C: {writes9C}; unmapped-report mismatches: {repBad}");
        int nineCRows = rows.Count(r => r.Pref.Split(' ').Contains("9C")), naTextRows = rows.Count(r => r.U.Contains('ନ'));
        Console.WriteLine($"  rows with 9C in the composer's preferred bytes: {nineCRows}; rows whose text holds ନ: {naTextRows}");
        // Where does ନ come out NOT as 9C in the composer? (e.g. inside a ligature)
        var naNo9C = rows.Where(r => r.U.Contains('ନ') && !r.Pref.Split(' ').Contains("9C")).Take(15).Select(r => $"{r.U}={r.Pref}");
        Console.WriteLine($"  ନ rows without 9C (ligatures), first: {string.Join("  ", naNo9C)}");
        // Does the composer ever write 5B?
        Console.WriteLine($"  composer spellings (pref/acc/alt) holding 5B: {rows.Count(r => r.Acc.Concat(r.Alt).Append(r.Pref).Any(a => a.Split(' ').Contains("5B")))}");
    }

    // ---------------------------------------------------------------- the capture's own syllable-join probes
    public static void Joins()
    {
        var rows = Truth.Load().ToDictionary(r => r.U, StringComparer.Ordinal);
        var res = K.ReadJson(K.PResults).AsArray();
        string[] kinds = { "କି ", "କା ", "କେ ", "କ reph ", "କୁ ", "କଂ ", "କ after-େ ", "କ link " };
        int n = 0, ok = 0, nosplit = 0, ours = 0;
        var bad = new List<string>();
        foreach (var e in res)
        {
            string tag = (string)e!["tag"]!;
            var k0 = kinds.FirstOrDefault(k => tag.StartsWith(k, StringComparison.Ordinal));
            if (k0 is null) continue;
            string rest = tag[k0.Length..];
            // only the syllable-join probes: "<first syllable> <letter>" or "କ link <letter> ା"
            if (!(rest.Length > 0 && K.IsCons(rest[0]) && (rest.Length == 1 || rest[1..] is "଼" or " ା" || (rest.Length == 2 && rest[1] == '଼')))) continue;
            string u = (string)e["u"]!, bytes = K.Norm((string)e["bytes"]!);
            if (string.IsNullOrEmpty(u) || bytes.Length == 0) continue;
            n++;
            bool found = false, any = false;
            for (int i = 1; i < u.Length; i++)
            {
                if (!rows.TryGetValue(u[..i], out var a) || !rows.TryGetValue(u[i..], out var b)) continue;
                any = true;
                if (K.Norm(a.Pref + " " + b.Pref) == bytes || a.Acc.Any(x => b.Acc.Any(y => K.Norm(x + " " + y) == bytes))) { found = true; break; }
            }
            if (!any) nosplit++;
            else if (found) ok++;
            else bad.Add($"{tag}\t{u}\t{bytes}");
            // and the product's converter on the whole join
            if (K.Hex(K.Fwd.ConvertText(u)) == K.Ex(bytes, true, false)) ours++;
            else if (bad.Count < 400) bad.Add($"  ours differs: {tag}\t{u}\tcomposer {bytes}\tours {K.Hex(K.Fwd.ConvertText(u))}");
        }
        Console.WriteLine($"JOIN probes (two syllables typed in one go) {n}: composer bytes = the two syllables' truth bytes joined {ok}, no split into truth rows {nosplit}, not a join {n - ok - nosplit}; product converter on the whole string = composer (na 9C->5B) {ours}/{n}");
        foreach (var b in bad.Where(b => b.Contains("ours differs")).Concat(bad.Where(b => !b.Contains("ours differs")).Take(10))) Console.WriteLine("   " + b);
    }

    // ---------------------------------------------------------------- random words, new seeds
    static List<(string U, string Want)> Pool(List<Row> rows) => rows
        .Where(r => r.U.Length > 0 && r.Pref.Length > 0 && (K.IsCons(r.U[0]) || K.IsIndVowel(r.U[0]) || K.IsDigit(r.U[0]))
                    && r.U[^1] is not (K.Virama or '\u200C' or '\u200D')
                    && !Enumerable.Range(1, r.U.Length - 1).Any(i => r.U[i - 1] == K.Virama && K.IsMark(r.U[i]))
                    && !r.Pref.Split(' ').Any(t => t is "3C" or "8F" or "90"))
        .OrderBy(r => r.U, StringComparer.Ordinal)
        .Select(r => (r.U, K.Ex(r.Pref, true, false))).ToList();

    public static void Words(string outDir, int[] seeds, int count)
    {
        var pool = Pool(Truth.Load());
        Console.WriteLine($"WORDS: syllable pool {pool.Count} truth rows (start on a letter/vowel/digit, no final virama or joiner)");
        using var w = K.Out(outDir, "words-fail.tsv");
        foreach (int seed in seeds)
        {
            var rng = new Random(seed);
            int ok = 0, rep = 0;
            for (int i = 0; i < count; i++)
            {
                int n = 2 + rng.Next(4);
                var parts = Enumerable.Range(0, n).Select(_ => pool[rng.Next(pool.Count)]).ToList();
                string u = string.Concat(parts.Select(p => p.U)), want = string.Join(" ", parts.Select(p => p.Want));
                var r = K.Fwd.Convert(u);
                string got = K.Hex(r.Text);
                if (r.HasUnmapped) rep++;
                if (got == want && !r.HasUnmapped) ok++;
                else w.WriteLine($"seed {seed}\t{u}\t[{K.Cps(u)}]\twant {want}\tgot {got}\t{string.Join("", r.UnmappedCharacters)}");
            }
            // sentences: words separated by a space, a comma, a danda or a line break
            string[] seps = { " ", ", ", "। ", "\n", "? " };
            string[] sepHex = { "20", "2C 20", "3E 20", "0A", "3F 20" };
            int sok = 0, sn = count / 5;
            for (int i = 0; i < sn; i++)
            {
                var u = new StringBuilder(); var want = new List<string>();
                int words = 3 + rng.Next(4);
                for (int k = 0; k < words; k++)
                {
                    int n = 1 + rng.Next(4);
                    var parts = Enumerable.Range(0, n).Select(_ => pool[rng.Next(pool.Count)]).ToList();
                    u.Append(string.Concat(parts.Select(p => p.U))); want.AddRange(parts.Select(p => p.Want));
                    if (k < words - 1) { int s = rng.Next(seps.Length); u.Append(seps[s]); want.Add(sepHex[s]); }
                }
                string got = K.Hex(K.Fwd.ConvertText(u.ToString()));
                if (got == string.Join(" ", want)) sok++;
                else w.WriteLine($"sentence seed {seed}\t{u.ToString().Replace("\n", "\\n")}\twant {string.Join(" ", want)}\tgot {got}");
            }
            Console.WriteLine($"  seed {seed}: words {count}: exact {ok} ({100.0 * ok / count:F2}%), with an unmapped report {rep}; sentences {sn}: exact {sok}");
        }
    }

    // ---------------------------------------------------------------- reverse: every composer spelling
    public static void Reverse(string outDir)
    {
        var gt = K.ReadJson(K.GT);
        var items = new List<(string U, string Hex, string Kind)>();
        var seen = new HashSet<(string, string)>();
        void Add(string u, string hex, string kind)
        {
            hex = K.Norm(hex);
            if (hex.Length == 0) return;
            if (seen.Add((u, hex))) items.Add((u, hex, kind));
        }
        foreach (var (u, node) in gt["truth"]!.AsObject())
        {
            Add(u, (string)node!["preferred"]!, "truth-preferred");
            foreach (var a in node["accepted"]!.AsArray()) Add(u, (string)a!, "truth-accepted");
            if (node["alternatives"] is JsonArray alts) foreach (var a in alts) Add(u, a is JsonObject o ? (string)o["bytes"]! : (string)a!, "truth-alternative");
        }
        foreach (var p in gt["pairs"]!.AsArray())
        {
            string kind = ((string)p!["kind"]!).Split(new[] { " (", ": " }, StringSplitOptions.None)[0];
            if (kind.Contains("U-composer-wrote")) kind = "quirk:U-composer-wrote-*";
            if (kind.Contains("carrier-in-answer")) kind = "quirk:U-composer-carrier-in-answer";
            Add((string)p["unicode"]!, (string)p["bytes"]!, "pair " + kind);
        }
        if (File.Exists(K.Evidence))
            foreach (var r in K.ReadJson(K.Evidence)["rows"]!.AsArray())
                Add((string)r!["unicodeIfE1IsBaPhala"] ?? "", (string)r["bytes"]!, "evidence 7-only codes");
        Console.WriteLine($"REVERSE: distinct composer spellings {items.Count} (truth, pairs of every kind, 7-only evidence)");
        using var w = K.Out(outDir, "rev-detail.tsv");
        foreach (bool asDoc in new[] { true, false })
        {
            var byKind = new SortedDictionary<string, int[]>(); // exact, sameBytes, other, bad, malformed, carrier07
            int total = 0;
            foreach (var (u, hex, kind) in items)
            {
                if (!asDoc && !hex.Split(' ').Contains("9C")) continue;   // raw pass: only the spellings 9C->5B changes
                var arr = byKind.TryGetValue(kind, out var a) ? a : byKind[kind] = new int[6];
                total++;
                if (hex.Split(' ').Contains("07")) { arr[5]++; continue; }
                string h = asDoc ? K.Ex(hex, true, false) : hex;
                string docText = K.Doc(h);
                string read = K.Pipe.Convert(docText, FontSoftware.Core.Conversion.LayoutKey.Legacy("shree-ori7"), FontSoftware.Core.Conversion.LayoutKey.Unicode).Text;
                string direct = K.Rev.ConvertText(docText);
                string bad = K.BadChars(read) + K.BadChars(direct);
                string mal = K.Malformed(read);
                string nu = u.Normalize(NormalizationForm.FormC), nr = read.Normalize(NormalizationForm.FormC);
                bool exact = nr == nu || nr.Replace("ଡ଼", "ଡ଼").Replace("ଢ଼", "ଢ଼") == nu.Replace("ଡ଼", "ଡ଼").Replace("ଢ଼", "ଢ଼");
                string again = K.Hex(K.Fwd.ConvertText(read));
                bool same = again == K.Ex(h, false, true) || again == h;
                if (bad.Length > 0) { arr[3]++; w.WriteLine($"{(asDoc ? "doc" : "raw")}\tBAD\t{kind}\t{u}\t{hex}\tread {read} [{K.Cps(read)}]\t{bad}"); }
                else if (exact) arr[0]++;
                else if (same) arr[1]++;
                else if (mal.Length > 0 && K.Malformed(u).Length == 0) { arr[4]++; w.WriteLine($"{(asDoc ? "doc" : "raw")}\tMALFORMED\t{kind}\t{u}\t{hex}\tread {read} [{K.Cps(read)}]\t{mal}\tours-again {again}"); }
                else { arr[2]++; w.WriteLine($"{(asDoc ? "doc" : "raw")}\tOTHER\t{kind}\t{u}\t{hex}\tread {read} [{K.Cps(read)}]\tours-again {again}"); }
            }
            Console.WriteLine(asDoc ? $"  as documents hold them (9C->5B), {total}:" : $"  RAW 9C kept (a document with the composer's raw 9C), {total} spellings that hold 9C:");
            int[] sum = new int[6];
            foreach (var (k, a) in byKind)
            {
                for (int i = 0; i < 6; i++) sum[i] += a[i];
                Console.WriteLine($"    {k}: exact {a[0]}, same bytes again {a[1]}, other text {a[2]}, BAD (Latin/control/nonchar/PUA) {a[3]}, malformed {a[4]}, carrier 07 skipped {a[5]}");
            }
            Console.WriteLine($"    TOTAL: exact {sum[0]}, same bytes again {sum[1]}, other text {sum[2]}, BAD {sum[3]}, malformed {sum[4]}, carrier 07 {sum[5]}");
        }
    }

    // ---------------------------------------------------------------- word-final virama
    public static void WordFinal(string outDir)
    {
        var rows = Truth.Load().Where(r => r.U.Length > 1 && r.U[^1] == K.Virama).ToList();
        int fa = rows.Count(r => r.Pref.Split(' ').Contains("FA")), alone = 0;
        (string Text, string Hex)[] after = { ("", ""), (" ଅ", "20 41"), ("।", "3E"), (",", "2C"), (" ", "20"), ("\n", "0A"), ("?", "3F"), ("୧", "31"), (")", "29"), ("\u200C", "") };
        int n = 0, ok = 0;
        using var w = K.Out(outDir, "wordfinal-fail.tsv");
        foreach (var r in rows)
        {
            string want = K.Ex(r.Pref, true, false);
            if (K.Hex(K.Fwd.ConvertText(r.U)) == want) alone++;
            foreach (var (t, h) in after)
            {
                n++;
                string u = "କ" + r.U + t, wantAll = K.Norm("4C FF " + want + " " + h);
                string got = K.Hex(K.Fwd.ConvertText(u));
                if (got == wantAll) ok++; else w.WriteLine($"{u.Replace("\n", "\\n")}\twant {wantAll}\tgot {got}");
            }
        }
        Console.WriteLine($"WORD-FINAL VIRAMA: truth rows ending in a virama {rows.Count}, composer writes the visible virama FA in {fa}; product alone = composer {alone}/{rows.Count}; after ka and before {after.Length} followers (end, space+word, danda, comma, space, newline, ?, digit, ')', ZWNJ): {ok}/{n}");
    }

    // ---------------------------------------------------------------- BMP non-Indic sweep
    public static readonly string Itself = "!%()*,-./:?0123456789";

    public static void Sweep(string outDir)
    {
        var union = Ttf.Union; var inter = Ttf.Intersection;
        var counts = new SortedDictionary<string, int>();
        var lookalike = new SortedDictionary<string, List<string>>();
        using var w = K.Out(outDir, "sweep-bad.tsv");
        int total = 0;
        for (int cp = 0; cp <= 0xFFFF; cp++)
        {
            if (cp >= 0xD800 && cp <= 0xDFFF) continue;
            if (cp >= 0x0B00 && cp <= 0x0B7F) continue;
            char c = (char)cp;
            total++;
            string s = "କ" + c + "କ";
            ConversionResult r;
            try { r = K.Fwd.Convert(s); } catch (Exception ex) { counts["EXCEPTION"] = counts.GetValueOrDefault("EXCEPTION") + 1; w.WriteLine($"EXC\tU+{cp:X4}\t{ex.Message}"); continue; }
            string t = r.Text;
            bool rep = r.UnmappedCharacters.Contains(c.ToString()) || (cp >= 0xFDD0 && cp <= 0xFDDF && r.UnmappedCharacters.Contains("�"));
            string cls;
            if (!(t.Length >= 4 && t[0] == (char)0x4C && t[1] == (char)0xFF && t[^2] == (char)0x4C && t[^1] == (char)0xFF) || false && !(t.StartsWith("\u004C\u00FF") && t.EndsWith("\u004C\u00FF") && t.Length >= 4)) { cls = "BAD neighbours changed"; w.WriteLine($"{cls}\tU+{cp:X4}\t{K.Hex(t)}"); }
            else
            {
                string mid = t[2..^2];
                bool ws = char.IsWhiteSpace(c);
                var cat = char.GetUnicodeCategory(c);
                bool ctl = cat is UnicodeCategory.Control or UnicodeCategory.Format || (cp >= 0xFDD0 && cp <= 0xFDEF) || (cp & 0xFFFE) == 0xFFFE;
                if (mid == c.ToString())
                {
                    if (!rep)
                    {
                        if (Itself.Contains(c) || c == ' ') cls = "ok itself (drawn as itself)";
                        else if (ws) cls = union.Contains(cp) ? "CHECK whitespace in cmap, unreported" : "ok whitespace passed through (no glyph)";
                        else { cls = union.Contains(cp) ? "BAD written as own code the fonts draw otherwise, unreported" : "BAD passed through UNREPORTED"; }
                    }
                    else cls = union.Contains(cp) ? "BAD passed through (reported) but the fonts map this code to another glyph" : "ok P3b passed through + reported (no glyph)";
                }
                else if (mid.Length == 0)
                {
                    if (rep) cls = union.Contains(cp) ? "ok left out + reported (own code draws another glyph)" : ctl ? "ok left out + reported (control/format/nonchar)" : ws ? "ok left out + reported (whitespace)" : "BAD left out but no font code draws it (P3b says pass through)";
                    else cls = c is '\u200C' or '\u200D' ? "ok joiner (draws nothing)" : (cp >= 0xFE00 && cp <= 0xFE0F) ? "ok variation selector dropped (by design)" : "BAD silently dropped";
                }
                else
                {
                    bool allIn = mid.All(ch => inter.Contains(ch) || ch == ' ');
                    cls = allIn ? (rep ? "ok look-alike (reported)" : "ok look-alike") : (rep ? "CHECK replacement not in every face (reported)" : "BAD replacement not in every face, unreported");
                    var key = $"{K.Hex(mid)}{(rep ? " (reported)" : "")}";
                    (lookalike.TryGetValue(key, out var l) ? l : lookalike[key] = new List<string>()).Add(c.ToString());
                }
                if (cls.StartsWith("BAD") || cls.StartsWith("CHECK")) w.WriteLine($"{cls}\tU+{cp:X4}\t{c}\t{K.Hex(mid)}\treported {rep}");
            }
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
        }
        Console.WriteLine($"NON-INDIC SWEEP: BMP code points outside U+0B00-U+0B7F (no surrogates): {total}, each between two କ");
        foreach (var (k, v) in counts) Console.WriteLine($"  {k}: {v}");
        Console.WriteLine("  look-alikes / replacements (written -> characters):");
        foreach (var (k, v) in lookalike) Console.WriteLine($"    {k} <- {string.Join(" ", v.Take(40))}{(v.Count > 40 ? $" ... ({v.Count})" : "")}");
    }

    // ---------------------------------------------------------------- what is written, against the cmaps and the Word map
    public static void Written(int[] seeds)
    {
        var faces = Ttf.Faces;
        Console.WriteLine("FACES:");
        foreach (var f in faces) Console.WriteLine($"  {f.File}\tfamily '{f.Family}'\tcmap {f.Cmap.Count}");
        var rows = Truth.Load();
        var written = new Dictionary<int, string>();
        void Note(string u)
        {
            var r = K.Fwd.Convert(u);
            foreach (char c in r.Text) if (!written.ContainsKey(c)) written[c] = u;
        }
        foreach (var r in rows) Note(r.U);
        var pool = rows.Where(r => r.U.Length > 0 && (K.IsCons(r.U[0]) || K.IsIndVowel(r.U[0]))).ToList();
        foreach (int seed in seeds) { var rng = new Random(seed); for (int i = 0; i < 20000; i++) Note(string.Concat(Enumerable.Range(0, 2 + rng.Next(3)).Select(_ => pool[rng.Next(pool.Count)].U))); }
        for (int cp = 0x20; cp <= 0xFFFF; cp++) { if (cp >= 0xD800 && cp <= 0xDFFF) continue; Note(((char)cp).ToString()); }
        var reported = new HashSet<int>();
        for (int cp = 0x20; cp <= 0xFFFF; cp++)
        {
            if (cp >= 0xD800 && cp <= 0xDFFF) continue;
            var r = K.Fwd.Convert(((char)cp).ToString());
            if (r.UnmappedCharacters.Contains(((char)cp).ToString()) && r.Text == ((char)cp).ToString()) reported.Add(cp);
        }
        var fontCodes = written.Keys.Where(c => !reported.Contains(c) || Ttf.Union.Contains(c)).OrderBy(c => c).ToList();
        Console.WriteLine($"CODE POINTS WRITTEN (truth rows, 40,000 words, every BMP character alone): {written.Count}; of them passed through as themselves and reported (no face draws them): {written.Keys.Count(c => reported.Contains(c) && !Ttf.Union.Contains(c))}; font codes: {fontCodes.Count}");
        foreach (var f in faces)
        {
            var miss = fontCodes.Where(c => !f.Cmap.Contains(c) && c != 0x0A && c != 0x0D && c != 0x09).ToList();
            Console.WriteLine($"  {f.File}: missing {miss.Count}{(miss.Count > 0 ? ": " + string.Join(" ", miss.Select(c => $"U+{c:X4}(from {written[c]})")) : "")}");
        }
        int[] invisible = { 0x8F, 0x90, 0xAD };
        int[] cs238 = { 0x152, 0x153, 0x160, 0x161, 0x178, 0x192 };
        Console.WriteLine($"  written that Word/RichEdit draw as nothing (U+008F U+0090 U+00AD): {string.Join(" ", fontCodes.Where(c => invisible.Contains(c)).Select(c => $"U+{c:X4}"))}");
        Console.WriteLine($"  written that RichEdit binds to Arial (cs238; Word draws them): {string.Join(" ", fontCodes.Where(c => cs238.Contains(c)).Select(c => $"U+{c:X4}={K.Hex(((char)c).ToString())} e.g. {written[c]}"))}");
        Console.WriteLine($"  C0/C1 written: {string.Join(" ", fontCodes.Where(c => c < 0x20 || (c >= 0x7F && c <= 0x9F)).Select(c => $"U+{c:X4}"))}");
        Console.WriteLine($"  all font codes written: {string.Join(" ", fontCodes.Select(c => K.Hex(((char)c).ToString())))}");
    }

    // ---------------------------------------------------------------- the table shipped before: its accuracy, and its documents read back now
    public static void OldRead(string oldDir)
    {
        FontSoftware.Core.Models.MappingTable Load(string f) => System.Text.Json.JsonSerializer.Deserialize<FontSoftware.Core.Models.MappingTable>(File.ReadAllText(Path.Combine(oldDir, f)), FontSoftware.Core.Config.ConfigLoader.JsonOptions);
        var oldFwd = new FontConverter(Load("unicode-to-shree-ori7.json"));
        var oldRev = new FontConverter(Load("shree-ori7-to-unicode.json"));
        var rows = Truth.Load();
        int pref = 0, acc = 0, prefNa = 0;
        foreach (var r in rows)
        {
            string got = K.Hex(oldFwd.ConvertText(r.U));
            if (got == r.Pref) pref++; else if (r.Acc.Contains(got)) acc++;
            if (got == K.Ex(r.Pref, true, true) || got == r.Pref) prefNa++;
        }
        Console.WriteLine($"OLD TABLE (main's current shree-ori7): forward = composer preferred {pref}/{rows.Count} ({100.0 * pref / rows.Count:F2}%), other accepted {acc}; = the new table's target (na 5B, P3b) {prefNa}");
        // old reverse on the composer's preferred bytes (as documents hold them)
        int oexact = 0, obad = 0;
        foreach (var r in rows)
        {
            string read = oldRev.ConvertText(K.Doc(K.Ex(r.Pref, true, false)));
            if (read.Normalize(NormalizationForm.FormC) == r.U.Normalize(NormalizationForm.FormC)) oexact++;
            if (K.BadChars(read).Length > 0) obad++;
        }
        Console.WriteLine($"OLD REVERSE on the composer's preferred bytes (9C->5B): exact {oexact}/{rows.Count}, with Latin/control/nonchar {obad}");
        // documents the old table wrote, read back by the new reverse
        var items = rows.Select(r => r.U).ToList();
        var pool = rows.Where(r => r.U.Length > 0 && (K.IsCons(r.U[0]) || K.IsIndVowel(r.U[0])) && r.U[^1] is not (K.Virama or '‌' or '‍')).ToList();
        var rng = new Random(31337);
        for (int i = 0; i < 20000; i++) items.Add(string.Concat(Enumerable.Range(0, 2 + rng.Next(3)).Select(_ => pool[rng.Next(pool.Count)].U)));
        int n = 0, skipped = 0, exact = 0, same = 0, bad = 0, mal = 0, other = 0;
        var ex = new List<string>();
        foreach (var u in items)
        {
            var w = oldFwd.Convert(u);
            if (w.HasUnmapped) { skipped++; continue; }
            n++;
            string read = K.Rev.ConvertText(w.Text);
            if (read == u || read.Normalize(NormalizationForm.FormC) == u.Normalize(NormalizationForm.FormC)) exact++;
            else if (K.BadChars(read).Length > 0) { bad++; if (ex.Count < 12) ex.Add($"BAD {u} old {K.Hex(w.Text)} read {read} {K.BadChars(read)}"); }
            else if (K.Malformed(read).Length > 0 && K.Malformed(u).Length == 0) { mal++; if (ex.Count < 12) ex.Add($"MALFORMED {u} old {K.Hex(w.Text)} read {read}"); }
            else if (K.Hex(K.Fwd.ConvertText(read)) == K.Hex(K.Fwd.ConvertText(u))) same++;
            else { other++; if (ex.Count < 12) ex.Add($"OTHER {u} old {K.Hex(w.Text)} read {read}"); }
        }
        Console.WriteLine($"DOCUMENTS THE OLD TABLE WROTE (truth rows + 20,000 words, seed 31337) read by the NEW reverse: {n} (skipped {skipped} the old table reported unmapped): exact {exact}, same text for the new table {same}, Latin/control {bad}, malformed {mal}, other text {other}");
        foreach (var e in ex) Console.WriteLine("    " + e);
    }

    // ---------------------------------------------------------------- P6: the same code points on every output path
    public static void Paths()
    {
        var t = typeof(FontSoftware.Engine.TypingEngine).Assembly.GetType("FontSoftware.Engine.RichTextClipboard");
        var build = t!.GetMethod("BuildRtf", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        string[] samples = { "ଓଡ଼ିଆ ଭାଷା", "ଜଗନ୍ନାଥ", "ଧର୍ମ", "ସ୍ୱାସ୍ଥ୍ୟ", "କାର୍ଯ୍ୟ", "ଅଙ୍କ୍ତ", "ଦଣ୍ଡ ଧ୍ୟାନ ଅନ୍ନ ଉତ୍ପଳ ଆତ୍ମା", "₹୧୦୦ ୰ ସୋଽହମ୍", "“ଓଡ଼ିଆ” – ଠିକ୍।" };
        int ok = 0;
        foreach (var s in samples)
        {
            var r = K.Fwd.Convert(s);
            string rtf = (string)build.Invoke(null, new object[] { r.Text, "SHREE-ORI7-0600" })!;
            string back = RtfText(rtf);
            // .txt (main's FileConversion.LegacyBytes rule: <= U+00FF as its byte, else Windows-1252 or left out)
            var txt = new List<byte>();
            foreach (char c in r.Text) { if (c <= 'ÿ') txt.Add((byte)c); else { try { txt.AddRange(K.Cp.GetBytes(c.ToString())); } catch (System.Text.EncoderFallbackException) { } } }
            string fromTxt = string.Concat(txt.Select(b => K.Doc(b.ToString("X2"))));
            bool same = back == r.Text;
            if (same) ok++;
            Console.WriteLine($"  {s}\ttyped/docx {K.Hex(r.Text)}\tRTF {(same ? "same" : "DIFFERS " + K.Hex(back))}\t.txt {(fromTxt == r.Text ? "same" : "differs: " + K.Hex(fromTxt))}\treported {string.Join("", r.UnmappedCharacters)}");
        }
        Console.WriteLine($"PATHS: RTF clipboard text = converter text {ok}/{samples.Length}");
    }

    static string RtfText(string rtf)
    {
        int body = rtf.IndexOf(@"\f0 ", rtf.IndexOf("}}") , StringComparison.Ordinal) + 4;
        var sb = new StringBuilder();
        for (int i = body; i < rtf.Length - 1; i++)
        {
            char c = rtf[i];
            if (c == '\\')
            {
                if (rtf[i + 1] is '\\' or '{' or '}') { sb.Append(rtf[i + 1]); i++; continue; }
                if (rtf[i + 1] == 'u' && (char.IsDigit(rtf[i + 2]) || rtf[i + 2] == '-'))
                {
                    int j = i + 2; while (char.IsDigit(rtf[j]) || rtf[j] == '-') j++;
                    sb.Append((char)(short)int.Parse(rtf[(i + 2)..j])); i = j; // skip the '?'
                    continue;
                }
                int k = i + 1; while (k < rtf.Length && char.IsLetter(rtf[k])) k++;
                string word = rtf[(i + 1)..k];
                if (word == "par") sb.Append('\n'); else if (word == "tab") sb.Append('\t');
                if (k < rtf.Length && rtf[k] == ' ') k++;
                i = k - 1; continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
