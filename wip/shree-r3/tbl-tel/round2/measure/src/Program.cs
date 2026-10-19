using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static U;

// telcheck - the independent Telugu skeptic. Real FontConverter / ConversionPipeline / TypingEngine (built from the source
// tree the csproj points at) against Shree's raw composer capture (cap-tel\tel7-groundtruth.json), byte for byte.
//   telcheck all      every check below, results into out\<variant>\
static partial class Program
{
    static readonly string Check1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    static string Cfg => Environment.GetEnvironmentVariable("TELCHECK_CONFIG") is { Length: > 0 } c ? c : Path.Combine(Check1, "repo", "assets", "config");
    static readonly string CapDir = Path.GetFullPath(Path.Combine(Check1, "..", "..", "cap-tel"));
    static string Variant => Environment.GetEnvironmentVariable("TELCHECK_VARIANT") is { Length: > 0 } v ? v : "a";
    static string Out => Path.Combine(Check1, "out", Variant);

    static AssetBundle B = null!;
    static LanguageProfile Te = null!;
    static ConversionPipeline Pipe = null!;
    static Capture Cap = null!;
    static readonly string[] Encs = { "shree-tel7", "shree-tel7w" };
    static readonly Dictionary<string, FontConverter> Fwd = new(), Rev = new();
    static readonly List<string> Summary = new();
    static readonly Dictionary<string, SortedSet<char>> Written = new();

    static void Say(string s) { Console.WriteLine(s); Summary.Add(s); }
    static void Tsv(string name, IEnumerable<string> lines) => File.WriteAllLines(Path.Combine(Out, name), lines, new UTF8Encoding(false));

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Directory.CreateDirectory(Out);
        B = new ConfigLoader(Cfg).Load();
        Te = B.Languages.First(l => l.Id == "te");
        Pipe = new ConversionPipeline(B);
        foreach (var e in Encs)
        {
            Fwd[e] = B.CreateConverterToLegacy(e) ?? throw new InvalidOperationException("no forward table for " + e);
            var revTable = B.MappingTables.FirstOrDefault(m => m.SourceFontId == e && m.SourceType == FontType.Legacy && m.TargetType == FontType.Unicode)
                           ?? throw new InvalidOperationException("no reverse table for " + e);
            Rev[e] = new FontConverter(revTable);
            Written[e] = new SortedSet<char>();
        }
        Cap = new Capture(Path.Combine(CapDir, "tel7-groundtruth.json"));
        Say($"config {Cfg}; variant {Variant}; capture truth {Cap.Truth.Count}, pairs {Cap.Pairs.Count}, gaps {Cap.Gaps.Count}");
        string mode = args.Length > 0 ? args[0] : "all";
        if (mode is "all" or "wiring") Wiring();
        if (mode is "all" or "truth" or "words" or "written") foreach (var e in Encs) TruthCheck(e);
        if (mode is "all" or "words") { Concat(); foreach (var e in Encs) { Words(e, 424242); Words(e, 90017); } }
        if (mode is "all" or "final") foreach (var e in Encs) Final(e);
        if (mode is "all" or "reverse") foreach (var e in Encs) Reverse(e);
        if (mode is "all" or "sweep") foreach (var e in Encs) Sweep(e);
        if (mode is "all" or "written") WrittenCheck();
        if (mode is "all" or "keys") Keys();
        if (mode is "all" or "seq") Sequences();
        if (mode is "all" or "ra") RaFirst();
        if (mode is "docx") Docx(args[1]);
        if (mode is "plans") Plans(args[1]);
        if (mode is "all" or "general") { General(); Phon(); }
        if (mode is "all" or "mix") foreach (var e in Encs) { Mix(e, 515); Mix(e, 8080); }
        if (mode is "type") TypeMode(args);
        if (mode is "all" or "classes") KeyClasses();
        if (mode is "all" or "words2") Words2();
        if (mode is "runaudit") RunAudit();
        File.WriteAllLines(Path.Combine(Out, $"summary-{mode}.txt"), Summary, new UTF8Encoding(false));
        return 0;
    }

    // ---------------------------------------------------------------- wiring
    static void Wiring()
    {
        var fl = Te.FontLayouts;
        foreach (var f in Te.Fonts.Where(f => f.Type == FontType.Legacy && !f.Id.Contains("telx")))
        {
            var layouts = fl.Where(l => !l.IsUnicode && l.Includes(f, B.FontsFor("te").FirstOrDefault(x => x.Id == l.FontId)?.ConversionKey)).Select(l => l.Name);
            var conv = B.CreateConverterToLegacy(f.ConversionKey);
            Say($"wiring: font {f.Id} family '{f.Family}' encoding {f.ConversionKey} -> forward table {(conv is null ? "NONE" : conv.Table.Id)}; listed under: {string.Join(" | ", layouts)}");
        }
    }

    // ---------------------------------------------------------------- truth
    static string Fix(string u, string hex)
    {
        bool nga = u.Contains("్ఙ"), rra = u.Contains("్ఱ"), vis = u.Contains('ః'), can = u.Contains('ఁ');
        return string.Join(" ", hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t =>
            t == "5D" && nga ? "94" : t == "CA" && rra ? "3C" : t == "40" && vis ? "5D" : t == "B6" && can ? "97" : t));
    }

    static string FixWhy(string u, string hex)
    {
        var t = hex.Split(' ');
        var w = new List<string>();
        if (u.Contains("్ఙ") && t.Contains("5D")) w.Add("nga5D>94");
        if (u.Contains("్ఱ") && t.Contains("CA")) w.Add("rraCA>3C");
        if (u.Contains('ః') && t.Contains("40")) w.Add("visarga40>5D");
        if (u.Contains('ఁ') && t.Contains("B6")) w.Add("candrabinduB6>97");
        return string.Join("+", w);
    }

    static string? JhaForm(string u)
    {
        int at = u.IndexOf("్ఝ", StringComparison.Ordinal);
        if (at < 0) return null;
        string head = u[..at] + "్\u200C", tail = u[(at + 1)..];
        if (!Cap.Truth.TryGetValue(head, out var h) || !Cap.Truth.TryGetValue(tail, out var t) || h.Pref.Length == 0 || t.Pref.Length == 0) return null;
        return Norm(Fix(head, h.Pref) + " " + Fix(tail, t.Pref));
    }

    /// <summary>Per encoding: u -> (class, our bytes).</summary>
    static readonly Dictionary<string, Dictionary<string, (string Cls, string Got)>> TruthRes = new();

    static string Shape(string u) => new(u.Select(c => IsLetter(c) ? 'C' : IsVirama(c) ? 'H' : IsSign(c) ? 's' : IsTail(c) ? 't' : IsVowel(c) ? 'V' : IsJoiner(c) ? 'J' : 'o').ToArray());

    static void TruthCheck(string enc)
    {
        if (TruthRes.ContainsKey(enc)) return;
        var conv = Fwd[enc];
        var res = new Dictionary<string, (string Cls, string Got)>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>();
        var bad = new List<string>();
        var lines = new List<string>();
        foreach (var (u, t) in Cap.Truth)
        {
            var r = conv.Convert(u);
            string got = Hex(r.Text);
            foreach (char c in r.Text) if (!r.UnmappedCharacters.Contains(c.ToString())) Written[enc].Add(c);
            string cls;
            if (got == t.Pref) cls = "1 composer-preferred";
            else if (t.Acc.Contains(got)) cls = "2 composer-accepted";
            else if (got == Fix(u, t.Pref)) cls = "3 glyph-fix " + FixWhy(u, t.Pref);
            else if (t.Acc.Any(a => Fix(u, a) == got)) cls = "3 glyph-fix(accepted) " + FixWhy(u, t.Acc.First(a => Fix(u, a) == got));
            else if (JhaForm(u) is { } j && j == got) cls = "4 jha-dropped (halant + ఝ)";
            else if (u is "ౢ" or "*" or "—" or "–") cls = "5 policy " + u;
            else cls = "9 WRONG";
            if (r.HasUnmapped && !cls.StartsWith("5")) cls += " +REPORTED(" + string.Join("", r.UnmappedCharacters) + ")";
            counts[cls.Split(' ')[0] + " " + cls.Split(' ')[1]] = counts.GetValueOrDefault(cls.Split(' ')[0] + " " + cls.Split(' ')[1]) + 1;
            res[u] = (cls, got);
            lines.Add($"{u}\t{Cps(u)}\t{Shape(u)}\t{cls}\tours {got}\tshree {t.Pref}{(t.Acc.Length > 1 ? " | acc " + string.Join(" / ", t.Acc.Where(a => a != t.Pref)) : "")}{(r.HasUnmapped ? "\treported " + string.Join("", r.UnmappedCharacters) : "")}");
            if (cls.StartsWith("9") || cls.Contains("REPORTED")) bad.Add(lines[^1]);
        }
        TruthRes[enc] = res;
        Tsv($"truth-{enc}.tsv", lines);
        Say($"[truth {enc}] {Cap.Truth.Count} composer strings: " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")));
        var fixes = res.Values.Where(v => v.Cls.StartsWith("3")).GroupBy(v => v.Cls).Select(g => $"{g.Key} {g.Count()}");
        Say($"[truth {enc}] glyph-fix detail: {string.Join("; ", fixes)}");
        foreach (var l in res.Where(kv => kv.Value.Cls.StartsWith("5")))
        {
            var rr = conv.Convert(l.Key);
            Say($"[truth {enc}] policy row {l.Key} U+{(int)l.Key[0]:X4}: shree {Cap.Truth[l.Key].Pref}, ours '{Hex(rr.Text)}' reported [{string.Join("", rr.UnmappedCharacters)}]");
        }
        foreach (var l in bad.Take(30)) Say("   BAD " + l);
    }

    // ---------------------------------------------------------------- concatenation (composer's own multi-syllable rows)
    static bool Base(char c) => IsLetter(c) || IsVowel(c) || IsDigit(c);

    static void Concat()
    {
        int n = 0, ok = 0;
        var bad = new List<string>();
        foreach (var (u, t) in Cap.Truth)
        {
            if (t.Pref.Length == 0) continue;
            for (int k = 1; k < u.Length; k++)
            {
                if (!Base(u[k]) || IsVirama(u[k - 1]) || IsJoiner(u[k - 1])) continue;
                string a = u[..k], b = u[k..];
                if (!Cap.Truth.TryGetValue(a, out var ta) || !Cap.Truth.TryGetValue(b, out var tb) || ta.Pref.Length == 0 || tb.Pref.Length == 0) continue;
                n++;
                string want = Norm(ta.Pref + " " + tb.Pref);
                if (want == t.Pref || t.Acc.Contains(want)) ok++; else bad.Add($"{u} = {a}+{b}: composer {t.Pref}, joined {want}");
            }
        }
        Say($"[concat] composer strings that split into two composer syllables: {n}; whole = the two joined: {ok}; differ {bad.Count}");
        foreach (var b in bad.Take(15)) Say("   " + b);
        Tsv("concat-diff.tsv", bad);
    }

    static bool IsSyllable(string u)
    {
        if (u.Length == 0 || !(IsLetter(u[0]) || IsVowel(u[0]) || IsDigit(u[0]))) return false;
        if (IsVirama(u[^1]) || IsJoiner(u[^1])) return false;
        for (int i = 1; i < u.Length; i++)
            if ((IsVirama(u[i - 1]) || IsJoiner(u[i - 1])) && (IsSign(u[i]) || IsTail(u[i]))) return false;
        return Malformed(u).Count == 0;
    }

    static void Words(string enc, int seed)
    {
        var conv = Fwd[enc];
        var res = TruthRes[enc];
        var pool = res.Where(kv => IsSyllable(kv.Key) && kv.Value.Cls[0] is '1' or '2' or '3' or '4' && !kv.Value.Cls.Contains("REPORTED"))
                      .OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        var rng = new Random(seed);
        int ok = 0, n = 20000, revOk = 0, revMal = 0, revBadChar = 0;
        var fails = new List<string>();
        var rev = Rev[enc];
        for (int i = 0; i < n; i++)
        {
            int k = 2 + rng.Next(4);
            var parts = Enumerable.Range(0, k).Select(_ => pool[rng.Next(pool.Count)]).ToList();
            string u = string.Concat(parts.Select(p => p.Key));
            string want = Norm(string.Join(" ", parts.Select(p => p.Value.Got)));
            var r = conv.Convert(u);
            string got = Hex(r.Text);
            foreach (char c in r.Text) Written[enc].Add(c);
            if (got == want && !r.HasUnmapped) ok++;
            else fails.Add($"{u}\t{string.Join("+", parts.Select(p => p.Key))}\twant {want}\tgot {got}\t{string.Join("", r.UnmappedCharacters)}");
            // P5: our own bytes read back
            string back = rev.ConvertText(r.Text);
            if (back == u || Hex(conv.ConvertText(back)) == got) revOk++;
            if (Malformed(back).Count > 0) revMal++;
            if (back.Any(c => Forbidden(c) is not null)) revBadChar++;
        }
        Tsv($"words-{enc}-{seed}.tsv", fails);
        Say($"[words {enc} seed {seed}] {n} words of 2-5 composer syllables (pool {pool.Count}): exact {ok}, wrong {n - ok}; read back same text {revOk}, malformed {revMal}, forbidden chars {revBadChar}");
        foreach (var f in fails.Take(10)) Say("   " + f);
    }

    // ---------------------------------------------------------------- word-final virama
    static void Final(string enc)
    {
        var conv = Fwd[enc];
        int n = 0, ok = 0, noRef = 0;
        var fails = new List<string>();
        string[] tails = { "", " ", " తెలుగు", "।", ",", ".", "?", "\n", ")", "1", "\u200D" };
        foreach (char c in Enumerable.Range(0x0C15, 0x25).Select(i => (char)i).Where(IsDrawnCons))
        {
            foreach (string pre in new[] { "", "అ", "కా" })
            {
                string halantKey = c + "్\u200C";
                if (!Cap.Truth.TryGetValue(halantKey, out var h) || h.Pref.Length == 0) { noRef++; continue; }
                string hal = Fix(halantKey, h.Pref);
                string preHex = pre.Length == 0 ? "" : TruthRes.ContainsKey(enc) && TruthRes[enc].TryGetValue(pre, out var pr) ? pr.Got : Hex(conv.ConvertText(pre));
                foreach (var tail in tails)
                {
                    string u = pre + c + "్" + tail;
                    string tailHex = tail switch { "" => "", "\u200D" => "", _ => Hex(conv.ConvertText(tail)) };
                    string want = Norm(preHex + " " + hal + " " + tailHex);
                    var r = conv.Convert(u);
                    string got = Hex(r.Text);
                    n++;
                    if (got == want && !r.HasUnmapped) ok++;
                    else fails.Add($"{U.Esc(u)}\t{Cps(u)}\twant {want}\tgot {got}\t{string.Join("", r.UnmappedCharacters)}");
                }
            }
        }
        // the composer gaps (derived C్ / C్ర్ spellings) and a few real words with a final virama
        int gOk = 0;
        foreach (var (u, d) in Cap.Gaps)
        {
            string got = Hex(conv.ConvertText(u));
            if (d is not null && got == Fix(u, d)) gOk++;
            else fails.Add($"GAP {u}\t{Cps(u)}\tderived {d}\tgot {got}");
        }
        Tsv($"final-{enc}.tsv", fails);
        Say($"[final {enc}] C+virama at a word end in {tails.Length} contexts x 3 prefixes: {n} cases, as the composer's halant (C్\u200C) {ok}, wrong {n - ok}, consonants without a halant row {noRef}; composer gaps {Cap.Gaps.Count}: = derived {gOk}");
        foreach (var f in fails.Take(10)) Say("   " + f);
        foreach (var w in new[] { "ఫోన్", "కంప్యూటర్", "ఇంటర్నెట్", "హైదరాబాద్", "ఆంధ్రప్రదేశ్", "క్రికెట్", "ఫోన్ కాల్", "కర్ర్" })
        {
            var r = conv.Convert(w);
            Say($"   word {w}: {Hex(r.Text)} -> back {Rev[enc].ConvertText(r.Text)}{(r.HasUnmapped ? " REPORTED " + string.Join("", r.UnmappedCharacters) : "")}");
        }
    }

    // ---------------------------------------------------------------- reverse
    static void Reverse(string enc)
    {
        var rev = Rev[enc];
        var fwd = Fwd[enc];
        var seen = new HashSet<(string, string)>();
        var items = new List<(string U, string Hex, string Kind)>();
        void Add(string u, string hex, string kind) { if (hex.Length > 0 && seen.Add((u, hex))) items.Add((u, hex, kind)); }
        foreach (var (u, t) in Cap.Truth)
        {
            Add(u, t.Pref, "truth-preferred");
            foreach (var a in t.Acc) Add(u, a, "truth-accepted");
            foreach (var a in t.Alts) Add(u, a, "truth-alternative");
        }
        foreach (var p in Cap.Pairs) Add(p.U, p.Bytes, p.Kind.Split(' ')[0].Split('(')[0]);
        var counts = new SortedDictionary<string, int>();
        var forb = new List<string>();
        var other = new List<string>();
        foreach (var (u, hex, kind) in items)
        {
            var r = rev.Convert(FromHex(hex));
            string got = r.Text;
            string cls;
            var fc = got.Where(c => Forbidden(c) is not null && !u.Contains(c)).Distinct().ToList();
            if (fc.Count > 0) { cls = "FORBIDDEN"; forb.Add($"{kind}\t{hex}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join(",", fc.Select(c => Forbidden(c) + " U+" + ((int)c).ToString("X4")))}"); }
            else if (got == u) cls = "exact";
            else if (Hex(fwd.ConvertText(got)) == Hex(fwd.ConvertText(u))) cls = "same text (we write both alike)";
            else if (Hex(fwd.ConvertText(got)) == hex || Hex(fwd.ConvertText(got)) == Fix(u, hex)) cls = "the text those bytes are (we write it so)";
            else if (Malformed(got).Count > 0 && Malformed(u).Count == 0) { cls = "MALFORMED"; other.Add($"MALFORMED\t{kind}\t{hex}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join(";", Malformed(got))}"); }
            else { cls = "other text"; other.Add($"OTHER\t{kind}\t{hex}\ttyped {u} [{Cps(u)}]\tread {got} [{Cps(got)}]\twe write {Hex(fwd.ConvertText(got))}"); }
            if (r.HasUnmapped) { cls += " +reported"; other.Add($"REPORTED\t{kind}\t{hex}\ttyped {u}\tread {got}\treported [{Cps(string.Concat(r.UnmappedCharacters))}]"); }
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
        }
        Tsv($"reverse-forbidden-{enc}.tsv", forb);
        Tsv($"reverse-other-{enc}.tsv", other);
        Say($"[reverse {enc}] every composer spelling in the capture ({items.Count} distinct text+bytes): " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (var g in other.GroupBy(o => o.Split('\t')[0] + " " + o.Split('\t')[1]).OrderByDescending(g => g.Count()).Take(14))
            Say($"   {g.Key}: {g.Count()}  e.g. {g.First().Split('\t', 3)[2]}");
        foreach (var f in forb.Take(10)) Say("   FORBIDDEN " + f);

        // every code the fonts map, alone, after a letter and before one
        int bytes = 0, badBytes = 0;
        var bl = new List<string>();
        for (int b = 0x20; b <= 0xFF; b++)
        {
            if (b is 0x7F or 0x81 or 0x8D or 0x8E or 0x8F or 0x9D or 0x9E) continue;
            string h = b.ToString("X2");
            foreach (var ctx in new[] { h, "4D E6 FC " + h, h + " 4D E6 FC", "41 " + h + " 41" })
            {
                bytes++;
                var r = rev.Convert(FromHex(ctx));
                var fc = r.Text.Where(c => Forbidden(c) is not null).Distinct().ToList();
                if (fc.Count > 0 || r.HasUnmapped) { badBytes++; bl.Add($"{ctx}\tread {r.Text} [{Cps(r.Text)}]\t{string.Join(",", fc.Select(c => Forbidden(c)))}\treported {string.Join("", r.UnmappedCharacters)}"); }
            }
        }
        Tsv($"reverse-bytes-{enc}.tsv", bl);
        Say($"[reverse {enc}] every code 20-FF the faces map, alone and next to letters: {bytes} probes, Latin/control/reported {badBytes}");
        foreach (var l in bl.Take(12)) Say("   " + l);
    }

    // ---------------------------------------------------------------- BMP non-Indic sweep
    static void Sweep(string enc)
    {
        var conv = Fwd[enc];
        var cmap = Cmaps.Common(enc);
        string ka = conv.ConvertText("క");
        var counts = new SortedDictionary<string, int>();
        var viol = new List<string>();
        var look = new SortedDictionary<string, List<string>>();
        for (int i = 0; i <= 0xFFFF; i++)
        {
            char c = (char)i;
            if (char.IsSurrogate(c) || (i >= 0x0C00 && i <= 0x0C7F)) continue;
            string s = c.ToString();
            var r = conv.Convert(s);
            bool rep = r.UnmappedCharacters.Contains(s);
            string cls;
            if (r.Text.Length == 0) cls = rep ? "left out, reported" : (i is 0x200C or 0x200D || (i >= 0xFE00 && i <= 0xFE0F) ? "dropped silently (joiner / variation selector)" : "DROPPED SILENTLY");
            else if (r.Text == s && rep) cls = cmap.Contains(c) ? "PASSED THROUGH BUT THE FONTS MAP THIS CODE" : "passed through, reported (no face maps it)";
            else if (r.Text == s && !rep) cls = char.IsWhiteSpace(c) ? "whitespace as itself" : cmap.Contains(c) ? "as itself (the fonts' code)" : "PASSED THROUGH UNREPORTED, NOT IN CMAP";
            else
            {
                cls = rep ? "stand-in, reported" : "look-alike";
                string key = $"{(rep ? "R " : "")}{Hex(r.Text)}";
                if (!look.TryGetValue(key, out var l)) look[key] = l = new List<string>();
                l.Add($"U+{i:X4}");
                if (r.Text.Any(x => !cmap.Contains(x) && !char.IsWhiteSpace(x))) cls = "WRITES A CODE NO FACE MAPS";
            }
            if (cls == "as itself (the fonts' code)") cls += " " + (char.IsAsciiDigit(c) || "!%()+,./:;=?".Contains(c) ? "[drawn as itself]" : "[CHECK: drawn as something else?]");
            counts[cls] = counts.GetValueOrDefault(cls) + 1;
            foreach (char x in r.Text) if (!(rep && r.Text == s)) Written[enc].Add(x);
            // between two letters: nothing may leak or join
            var m = conv.Convert("క" + s + "క");
            if (m.Text != ka + r.Text + ka) viol.Add($"U+{i:X4}\tbetween letters {Hex(m.Text)}\talone {Hex(r.Text)}");
            if (cls.Any(char.IsUpper) && cls.StartsWith("PASSED") || cls.StartsWith("DROPPED") || cls.StartsWith("WRITES") || cls.Contains("CHECK"))
                viol.Add($"U+{i:X4} {(char.IsControl(c) ? "" : s)}\t{cls}\t{Hex(r.Text)}");
        }
        Tsv($"sweep-{enc}.tsv", viol);
        Tsv($"sweep-lookalikes-{enc}.tsv", look.Select(kv => $"{kv.Key}\t{kv.Value.Count}\t{string.Join(" ", kv.Value.Take(40))}"));
        Say($"[sweep {enc}] BMP outside U+0C00-0C7F ({counts.Values.Sum()} chars): " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")));
        Say($"[sweep {enc}] look-alikes/stand-ins: " + string.Join("; ", look.Select(kv => $"{kv.Key} <- {string.Join(" ", kv.Value.Take(6))}{(kv.Value.Count > 6 ? $" (+{kv.Value.Count - 6})" : "")}")));
        Say($"[sweep {enc}] violations: {viol.Count}");
        foreach (var v in viol.Take(20)) Say("   " + v);
        // the Telugu block itself: what is passed through (P3b)
        var tb = new List<string>();
        for (int i = 0x0C00; i <= 0x0C7F; i++)
        {
            char c = (char)i;
            if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherNotAssigned) continue;
            var r = conv.Convert(c.ToString());
            if (r.HasUnmapped) tb.Add($"{c}(U+{i:X4})={(r.Text == c.ToString() ? "kept" : Hex(r.Text))}");
        }
        Say($"[sweep {enc}] Telugu block chars reported: {string.Join(" ", tb)}");
    }

    // ---------------------------------------------------------------- written code points vs cmaps + Word map
    static void WrittenCheck()
    {
        foreach (var e in Encs)
        {
            var set = Written[e];
            Say($"[written {e}] {set.Count} distinct code points written (truth, words, sweep): {string.Join(" ", set.Select(c => ((int)c).ToString("X4")))}");
            foreach (var (file, fam, keys) in Cmaps.Faces(e))
            {
                var miss = set.Where(c => !keys.Contains(c) && !char.IsWhiteSpace(c) && c is not ('\u2028' or '\u2029')).ToList();
                Say($"   face {file} ({fam}): missing {miss.Count} {string.Join(" ", miss.Select(c => "U+" + ((int)c).ToString("X4")))}");
            }
            var invisible = new[] { '\u0081', '\u008D', '\u008F', '\u0090', '\u009D', '\u009E', '\u00AD' };
            var cs238 = new[] { 'Œ', 'œ', 'Š', 'š', 'Ÿ', 'ƒ' };
            Say($"   Word/RichEdit draw nothing (C1 81 8D 8F 90 9D 9E, soft hyphen AD): written {string.Join(" ", set.Where(invisible.Contains).Select(c => "U+" + ((int)c).ToString("X4")))}");
            Say($"   RichEdit charset-238 Latin letters (8C 9C 8A 9A 9F 83; Word draws the font's glyph): written {string.Join(" ", set.Where(cs238.Contains).Select(c => "U+" + ((int)c).ToString("X4") + "=" + Hex(c.ToString())))}");
        }
    }

    // ---------------------------------------------------------------- one-off typing
    static void TypeMode(string[] args)
    {
        var lref = Te.Layouts.First(l => l.Id == args[1]);
        var layout = B.GetLayout(lref);
        var tr = B.GetTransliterator(Te);
        foreach (var keys in args.Skip(2))
        {
            string k = keys.Replace("<BS>", "\b").Replace("<CS>", "Ø").Replace("<CS1>", "¤");
            foreach (var (name, conv) in Profiles())
            {
                using var t = new Typist(layout, tr, conv);
                string buf = t.Type(k);
                Console.WriteLine($"{args[1]} {keys} {name}: {(conv is null ? buf + " [" + Cps(buf) + "]" : Hex(buf))}");
            }
        }
    }

    static IEnumerable<(string Name, FontConverter? Conv)> Profiles() => new (string, FontConverter?)[] { ("uni", null), ("t7", Fwd["shree-tel7"]), ("t7w", Fwd["shree-tel7w"]) };
}

/// <summary>The faces' cmaps, as WPF's GlyphTypeface read them (glyphs\cmaps-wpf.txt, written by PowerShell).</summary>
static class Cmaps
{
    static List<(string File, string Fam, HashSet<char> Keys)>? _all;
    static List<(string File, string Fam, HashSet<char> Keys)> All => _all ??= File.ReadAllLines(Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")), "glyphs", "cmaps-wpf.txt"))
        .Select(l => l.Split('\t')).Select(p => (p[0], p[1], p[2].Split(' ').Select(h => (char)Convert.ToInt32(h, 16)).ToHashSet())).ToList();

    public static List<(string File, string Fam, HashSet<char> Keys)> Faces(string enc) =>
        All.Where(f => enc == "shree-tel7w" ? f.Fam is "Shree-Tel7-001" or "Shree-Tel7-002" : f.Fam.StartsWith("SHREE-TEL")).ToList();

    public static HashSet<char> Common(string enc)
    {
        HashSet<char>? s = null;
        foreach (var f in Faces(enc)) { if (s is null) s = new HashSet<char>(f.Keys); else s.IntersectWith(f.Keys); }
        return s!;
    }
}
