using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Models;
using static U;

// Round 2 (tbl-tel builder): every key class of every Shree Telugu keyboard on every consonant key, not only on క.
// Which key is which: the repo's tests/FontSoftware.Tests/Data/shree-tel-keys.json (teltruth keys, read off Shree's own
// keyboard files as the composers answered them, cap-tel\tel-vendor-keys.json). What it must type: what Shree's composer
// does with that code - the sign after a letter, the link between two letters, the visible halant, the tail, and the
// ra-vattu on the NEXT consonant.
static partial class Program
{
    static void KeyClasses()
    {
        var tr = B.GetTransliterator(Te);
        string fixture = Environment.GetEnvironmentVariable("TELKEYS") is { Length: > 0 } tk ? tk : Path.GetFullPath(Path.Combine(Cfg, "..", "..", "tests", "FontSoftware.Tests", "Data", "shree-tel-keys.json"));
        var doc = JsonNode.Parse(File.ReadAllText(fixture, Encoding.UTF8))!["layouts"]!.AsObject();
        var bad = new List<string>();
        int total = 0, okU = 0, okL = 0;
        var perClass = new SortedDictionary<string, (int N, int U, int L)>();
        void Probe(string cls, KeyboardLayout layout, string id, string typedKeys, string want)
        {
            var (u, t7, _) = TypeAll(layout, tr, typedKeys);
            string w7 = Fwd["shree-tel7"].ConvertText(want);
            total++;
            bool a = u == want, b = t7 == w7;
            if (a) okU++;
            if (b) okL++;
            var c = perClass.GetValueOrDefault(cls);
            perClass[cls] = (c.N + 1, c.U + (a ? 1 : 0), c.L + (b ? 1 : 0));
            if (!a || !b) bad.Add($"{cls}\t{id}\t{typedKeys}\twant {want} [{Cps(want)}] {Hex(w7)}\tgot {u} [{Cps(u)}] {Hex(t7)}");
        }
        foreach (var (id, node) in doc)
        {
            var layout = B.GetLayout(Te.Layouts.First(l => l.Id == id));
            var cons = node!["consonants"]!.AsObject().Select(kv => (Keys: kv.Key, U: (string)kv.Value!)).ToList();
            var signs = node["signs"]!.AsObject().Select(kv => (Keys: kv.Key, S: (string)kv.Value!)).ToList();
            var tails = node["tails"]!.AsObject().Select(kv => (Keys: kv.Key, T: (string)kv.Value!)).ToList();
            var aa = signs.FirstOrDefault(s => s.S == "ా" && !s.Keys.StartsWith("Ø"));
            foreach (var s in signs)
                foreach (var c in cons)
                    Probe("sign key after a consonant", layout, id, c.Keys + s.Keys, c.U + s.S);
            foreach (var l in node["links"]!.AsArray().Select(x => (string)x!))
                foreach (var c1 in cons)
                    foreach (var c2 in cons.Take(6))
                        Probe("link key between consonants", layout, id, c1.Keys + l + c2.Keys, c1.U + "్" + c2.U);
            foreach (var h in node["halants"]!.AsArray().Select(x => (string)x!))
                foreach (var c in cons)
                    Probe("halant key after a consonant", layout, id, c.Keys + h, c.U + "్‌");
            foreach (var t in tails)
                foreach (var c in cons)
                {
                    Probe("tail key after a consonant", layout, id, c.Keys + t.Keys, c.U + t.T);
                    if (aa.Keys is not null) Probe("tail key after a sign", layout, id, c.Keys + aa.Keys + t.Keys, c.U + "ా" + t.T);
                }
            foreach (var r in node["ravattu"]!.AsArray().Select(x => (string)x!))
                foreach (var c in cons)
                {
                    Probe("ra-vattu key struck before a consonant", layout, id, r + c.Keys, c.U + "్ర");
                    Probe("ra-vattu key struck before a consonant, mid-word", layout, id, cons[0].Keys + r + c.Keys, cons[0].U + c.U + "్ర");
                    if (aa.Keys is not null) Probe("ra-vattu key, consonant, sign", layout, id, r + c.Keys + aa.Keys, c.U + "్రా");
                }
        }
        Tsv("classes.tsv", bad);
        Say($"[classes] every key class on every consonant key, 14 fixed vendor keyboards ({fixture}): {total} probes; Unicode right {okU}; SHREE-TEL7 = converter {okL}");
        foreach (var (c, v) in perClass) Say($"   {c}: {v.N} probes, Unicode right {v.U}, SHREE-TEL7 right {v.L}");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0] + " / " + b.Split('\t')[1]))
            Say($"   {g.Key}: {g.Count()} wrong, e.g. {g.First().Split('\t', 3)[2]}");
    }
}
