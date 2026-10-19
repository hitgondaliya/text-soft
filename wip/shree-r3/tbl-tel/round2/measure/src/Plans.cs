using System.Text;
using static U;

static partial class Program
{
    /// <summary>Whole words typed key by key (the build agent's word plans: keys TAB unicode), in all three profiles.</summary>
    static void Plans(string dir)
    {
        var tr = B.GetTransliterator(Te);
        int n = 0, uni = 0, leg = 0, legw = 0;
        var bad = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*plan-*.txt").Where(f => !Path.GetFileName(f).StartsWith("final") && !Path.GetFileName(f).StartsWith("r5")))
        {
            string lid = Path.GetFileNameWithoutExtension(file).Split("plan-")[1].Replace("telugu-inscript", "te-inscript");
            var lref = Te.Layouts.FirstOrDefault(l => l.Id == lid);
            if (lref is null) { Say($"   plans: no layout {lid}"); continue; }
            var layout = B.GetLayout(lref);
            foreach (var line in File.ReadAllLines(file, Encoding.UTF8).Where(l => l.Contains('\t')))
            {
                var p = line.Split('\t');
                string keys = p[0].Replace("<BS>", "\b").Replace("<CS>", "Ø").Replace("<CS1>", "¤"), want = p[1];
                var (u, t7, t7w) = TypeAll(layout, tr, keys);
                n++;
                if (u == want) uni++; else bad.Add($"UNI\t{lid}\t{p[0]}\twant {want}\tgot {u} [{Cps(u)}]");
                if (t7 == Fwd["shree-tel7"].ConvertText(want)) leg++; else bad.Add($"T7\t{lid}\t{p[0]}\t{want}\twant {Hex(Fwd["shree-tel7"].ConvertText(want))}\tgot {Hex(t7)}");
                if (t7w == Fwd["shree-tel7w"].ConvertText(want)) legw++; else bad.Add($"T7W\t{lid}\t{p[0]}\t{want}\tgot {Hex(t7w)}");
            }
        }
        Tsv("plans.tsv", bad);
        Say($"[plans] {n} word plans typed key by key: Unicode = word {uni}; SHREE-TEL7 = converter(word) {leg}; Shree-Tel7-001 {legw}");
        foreach (var b in bad.Take(10)) Say("   " + b);
    }
}
