using System.Reflection;
using System.Text.Json;
using FontSoftware.Core.Models;
using static U;

// Round 2: every chart run of every fixed Telugu layout against what the engine types for the same keys WITHOUT that run
// (the keys' own texts put together by the typing order the layout teaches). A run the keys already make is redundant; a
// run they do not make is either a real special (a ligature, a quirk of the keyboard) or a chart artefact - the help
// page's word "Link" on APPLE, ITR's "ZtZ" = న్న where Z is స (Shree's ITR.TEL: Z = 6C స, t = 64 the link).
static partial class Program
{
    static void RunAudit()
    {
        var tr = B.GetTransliterator(Te);
        var orderField = typeof(KeyboardLayout).GetField("_order", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var lines = new List<string>();
        int runs = 0, same = 0;
        foreach (var lref in Te.Layouts)
        {
            var layout = B.GetLayout(lref);
            if (layout.Phonetic) continue;
            var order = layout.Order;
            string json = JsonSerializer.Serialize(layout);
            foreach (var s in layout.Sequences)
            {
                if (s.Keys.Contains('¤') || (s.Keys.Length == 2 && s.Keys[0] == 'Ø')) continue;
                runs++;
                var without = JsonSerializer.Deserialize<KeyboardLayout>(json)!;
                without.Sequences.RemoveAll(x => x.Keys == s.Keys);
                orderField.SetValue(without, order);
                using var t = new Typist(without, tr, null);
                string got = t.Type(s.Keys);
                if (got == s.To) { same++; continue; }
                lines.Add($"{lref.Id}\t{s.Keys}\trun {s.To} [{Cps(s.To)}]\twithout it {got} [{Cps(got)}]");
            }
        }
        Tsv("runaudit.tsv", lines);
        Say($"[runaudit] chart runs of the fixed Telugu layouts: {runs}; the keys make the same text without the run: {same}; a run of its own: {lines.Count}");
    }
}
