using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static U;

static partial class Program
{
    static readonly Dictionary<string, string> KbToLayout = new()
    {
        ["APPLE"] = "tel-apple", ["CRTRONIC"] = "tel-crtronic", ["DOE"] = "tel-doe", ["ENG"] = "tel-eng", ["GODREJ"] = "tel-godrej",
        ["INDICA"] = "tel-indica", ["INSCRIPT"] = "te-inscript", ["ITR"] = "tel-itr", ["MICRO"] = "tel-micro", ["MONO"] = "tel-mono",
        ["NEWROMA"] = "tel-newroma", ["PHONETIC"] = "tel-phonetic", ["PRAKASH"] = "tel-prakash", ["ROMA"] = "tel-roma",
        ["TYPEWRIT"] = "tel-typewrit", ["VISION"] = "tel-vision",
    };

    static (string Uni, string T7, string T7w) TypeAll(KeyboardLayout layout, Transliterator? tr, string keys)
    {
        string[] o = new string[3];
        int i = 0;
        foreach (var (_, conv) in Profiles())
        {
            using var t = new Typist(layout, tr, conv);
            o[i++] = t.Type(keys);
        }
        return (o[0], o[1], o[2]);
    }

    static void Keys()
    {
        var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(CapDir, "tel-vendor-keys.json"), Encoding.UTF8))!;
        var tr = B.GetTransliterator(Te);
        var counts = new SortedDictionary<string, int>();
        var rows = new List<string>();
        int positions = 0, p6bad = 0, p6badW = 0;
        var p6 = new List<string>();
        foreach (var (kb, node) in doc["keyboards"]!.AsObject())
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == KbToLayout[kb]));
            foreach (var layer in new[] { "0", "1" })
            {
                var answers = node!["layers"]![layer]!.AsObject();
                for (char k = '!'; k <= '~'; k++)
                {
                    positions++;
                    var v = answers[k.ToString()];
                    string? sU = (string?)v?["uRead"];
                    string? sB = v?["b7"] is JsonValue bv ? Norm((string?)bv) : null;
                    if (sB is { Length: 0 }) sB = null;
                    string keys = (layer == "1" ? "Ø" : "") + k;
                    var (uni, t7, t7w) = TypeAll(layout, tr, keys);
                    string hb = Hex(t7), hw = Hex(t7w);
                    // P6: typing in a legacy font = the converter on what the same keys type in Unicode
                    if (t7 != Fwd["shree-tel7"].ConvertText(uni)) { p6bad++; p6.Add($"{kb}\t{layer}\t{k}\tuni {uni} [{Cps(uni)}]\tt7 {hb}\tconverter {Hex(Fwd["shree-tel7"].ConvertText(uni))}"); }
                    if (t7w != Fwd["shree-tel7w"].ConvertText(uni)) { p6badW++; p6.Add($"{kb}\t{layer}\t{k}\tuni {uni} [{Cps(uni)}]\tt7w {hw}\tconverter {Hex(Fwd["shree-tel7w"].ConvertText(uni))}"); }
                    bool shreeNothing = string.IsNullOrEmpty(sU) && sB is null;
                    bool tsNothing = uni.Length == 0 && t7.Length == 0;
                    string cls;
                    if (sB == "E6 61 E6 61") cls = uni == sU ? "lone-sign key: same Unicode (7 answers its carrier)" : "lone-sign key: Unicode differs";
                    else if (shreeNothing) cls = tsNothing ? "both type nothing" : "SHREE NOTHING, TEXTSOFT TYPES";
                    else if (string.IsNullOrEmpty(sU) && sB is not null) cls = hb == sB ? "7-only code: same bytes" : tsNothing ? "7-only code: TextSoft nothing" : "7-only code: TEXTSOFT OTHER";
                    else if (hb == sB) cls = "same bytes";
                    else if (sB is not null && hb == Fix(sU!, sB)) cls = "same after glyph fix (" + FixWhy(sU!, sB) + ")";
                    else if (uni == sU) cls = "same Unicode, other bytes";
                    else if (tsNothing) cls = "TEXTSOFT NOTHING, SHREE TYPES";
                    else cls = "DIFFERENT UNICODE";
                    counts[cls] = counts.GetValueOrDefault(cls) + 1;
                    if (cls != "same bytes" && cls != "both type nothing")
                        rows.Add($"{cls}\t{kb}\t{(layer == "1" ? "Ctrl+Space " : "")}{k}\tshree U '{sU}' [{(sU is null ? "" : Cps(sU))}] b7 {sB}\tours U '{uni}' [{Cps(uni)}] t7 {hb} t7w {hw}");
                }
            }
        }
        // Ctrl+Shift+1: the charts' 16 symbol keys, identical on every keyboard
        var cs = doc["ctrlShift1"]!["keys"]!.AsObject();
        int csN = 0, csUni = 0;
        foreach (var (kb, _) in doc["keyboards"]!.AsObject())
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == KbToLayout[kb]));
            foreach (var (key, v) in cs)
            {
                csN++;
                string ch = (string)v!["char"]!;
                string? b7 = (string?)v["b7"];
                var (uni, t7, t7w) = TypeAll(layout, tr, "¤" + key);
                if (uni == ch) csUni++;
                string want7 = Hex(Fwd["shree-tel7"].ConvertText(ch)), want7w = Hex(Fwd["shree-tel7w"].ConvertText(ch));
                string cls = uni != ch ? "CS1: UNICODE DIFFERS FROM CHART" : Hex(t7) == b7 ? "CS1: same bytes as Shree" : Hex(t7) == want7 ? "CS1: our policy bytes (Shree " + (b7 ?? "nothing") + ")" : "CS1: LEGACY != CONVERTER";
                counts[cls] = counts.GetValueOrDefault(cls) + 1;
                if (!cls.Contains("same bytes")) rows.Add($"{cls}\t{kb}\tCtrl+Shift+1 {key}\tchart '{ch}' b7 {b7}\tours U '{uni}' t7 {Hex(t7)} t7w {Hex(t7w)}");
            }
        }
        Tsv("keys-mismatch.tsv", rows);
        Tsv("keys-p6.tsv", p6);
        Say($"[keys] real TypingEngine, 16 vendor keyboards, layer 0 + Ctrl+Space, every printable key: {positions} positions; Ctrl+Shift+1 {csN} positions (Unicode = chart {csUni})");
        foreach (var (c, n) in counts) Say($"   {c}: {n}");
        Say($"[keys] typing in SHREE-TEL7 != converter(what the keys type in Unicode): {p6bad}; in Shree-Tel7-001: {p6badW}");
        foreach (var l in p6.Take(12)) Say("   P6 " + l);
        foreach (var g in rows.GroupBy(r => r.Split('\t')[0]).Where(g => g.Key.Any(char.IsUpper) && g.Key.ToUpperInvariant() == g.Key || g.Key.StartsWith("same Unicode") || g.Key.StartsWith("lone")))
        {
            Say($"   -- {g.Key}: {g.Count()}");
            foreach (var r in g.Take(8)) Say("      " + r);
        }
    }

    static void Sequences()
    {
        var tr = B.GetTransliterator(Te);
        int n = 0, uniOk = 0, legOk = 0, legOkW = 0;
        var bad = new List<string>();
        foreach (var lref in Te.Layouts)
        {
            var layout = B.GetLayout(lref);
            foreach (var s in layout.Sequences)
            {
                n++;
                var (uni, t7, t7w) = TypeAll(layout, tr, s.Keys);
                if (uni == s.To) uniOk++; else bad.Add($"UNI\t{lref.Id}\t{s.Keys}\twant {s.To} [{Cps(s.To)}]\tgot {uni} [{Cps(uni)}]");
                string w7 = Fwd["shree-tel7"].ConvertText(s.To), w7w = Fwd["shree-tel7w"].ConvertText(s.To);
                if (t7 == w7) legOk++; else bad.Add($"T7\t{lref.Id}\t{s.Keys}\t{s.To}\twant {Hex(w7)}\tgot {Hex(t7)}");
                if (t7w == w7w) legOkW++; else bad.Add($"T7W\t{lref.Id}\t{s.Keys}\t{s.To}\twant {Hex(w7w)}\tgot {Hex(t7w)}");
            }
        }
        Tsv("sequences.tsv", bad);
        Say($"[seq] every chart run of the 18 Telugu layouts typed through the real engine: {n}; Unicode = chart {uniOk}; SHREE-TEL7 bytes = converter(chart) {legOk}; Shree-Tel7-001 {legOkW}");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0] + " " + b.Split('\t')[1]).Take(30)) Say($"   {g.Key}: {g.Count()}  e.g. {g.First()}");

        // Words typed key by key: the ra-vattu struck first and Backspace (the build agent's open engine items)
        var probes = new (string Layout, string Keys, string Want)[]
        {
            ("tel-roma", "Rk", "క్ర"), ("tel-roma", "Rp", "ప్ర"), ("tel-roma", "RpcM", "ప్రచం"), ("tel-typewrit", "mKjk", "చక్రం"),
            ("tel-typewrit", "Kj", "క్ర"), ("tel-doe", "ke\b", "క"), ("tel-doe", "kdj\b", "క"), ("tel-roma", "Rk\b", ""),
            ("te-inscript", "ks\b", "క"), ("tel-doe", "kdj", "క్ర"), ("tel-crtronic", "Tw", "క్ర"), ("tel-micro", "Qe", "క్ర"),
        };
        foreach (var (lid, keys, want) in probes)
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == lid));
            var (uni, t7, _) = TypeAll(layout, tr, keys);
            Say($"   probe {lid} '{keys.Replace("\b", "<BS>")}': Unicode {uni} [{Cps(uni)}]{(want.Length > 0 || keys.Contains('\b') ? (uni == want ? " = " : " WANT ") + want : "")}; SHREE-TEL7 {Hex(t7)}; converter({want}) {Hex(Fwd["shree-tel7"].ConvertText(want))}; malformed: {string.Join(";", Malformed(uni))}");
        }
    }
}

static partial class Program
{
    /// <summary>The ra-vattu struck BEFORE its consonant, the way 8 charts teach it, on every consonant, word-initial and mid-word.</summary>
    static void RaFirst()
    {
        var tr = B.GetTransliterator(Te);
        var raKeys = new Dictionary<string, string> { ["tel-crtronic"] = "T", ["tel-godrej"] = "Øf", ["tel-itr"] = "Øg", ["tel-micro"] = "Q", ["tel-modular"] = "\\", ["tel-mono"] = "&", ["tel-roma"] = "R", ["tel-typewrit"] = "K" };
        int total = 0, uniOk = 0, legOk = 0, malformed = 0;
        var bad = new List<string>();
        foreach (var (lid, ra) in raKeys)
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == lid));
            // consonant keys: a key whose base or shift types exactly one drawn consonant
            var cons = new SortedDictionary<char, char>();
            foreach (var k in layout.Keys)
                foreach (var (txt, shift) in new[] { (k.Base, false), (k.Shift, true) })
                    if (txt is { Length: 1 } && IsDrawnCons(txt[0]) && txt[0] != '\u0C30' && k.VirtualKey >= 'A' && k.VirtualKey <= 'Z' && !cons.ContainsKey(txt[0]))
                        cons[txt[0]] = shift ? (char)k.VirtualKey : char.ToLowerInvariant((char)k.VirtualKey);
            char kaKey = cons.TryGetValue('\u0C15', out var kk) ? kk : cons.First().Value;
            int lt = 0, lu = 0, ll = 0;
            foreach (var (c, key) in cons)
                foreach (var (prefix, preU) in new[] { ("", ""), (kaKey.ToString(), "\u0C15") })
                {
                    string keys = prefix + ra + key, want = preU + c + "\u0C4D\u0C30";
                    var (uni, t7, _) = TypeAll(layout, tr, keys);
                    string w7 = Fwd["shree-tel7"].ConvertText(want);
                    total++; lt++;
                    bool u = uni == want, l = t7 == w7;
                    if (u) { uniOk++; lu++; }
                    if (l) { legOk++; ll++; }
                    if (Malformed(uni).Count > 0) malformed++;
                    if (!u || !l) bad.Add($"{lid}\t{keys}\twant {want} [{Cps(want)}] {Hex(w7)}\tgot {uni} [{Cps(uni)}] {Hex(t7)}");
                }
            Say($"   ra-first {lid} (ra key '{ra}', {cons.Count} consonant keys): {lt} probes, Unicode right {lu}, SHREE-TEL7 bytes right {ll}");
        }
        Tsv("ra-first.tsv", bad);
        Say($"[ra-first] ra-vattu struck before its consonant on the 8 layouts whose charts teach it so: {total} probes (each consonant, word-initial and after క); Unicode right {uniOk}, malformed Unicode {malformed}; SHREE-TEL7 bytes right {legOk}");
        foreach (var b in bad.Take(8)) Say("   " + b);
    }
}
