using System.Text;
using FontSoftware.Core.Config;
// Which layouts of every language learn the after-virama vowel joins (TypingOrder.AfterVirama), and what they learn.
Console.OutputEncoding = Encoding.UTF8;
var b = new ConfigLoader(Path.Combine(args[0], "assets", "config")).Load();
int n = 0, with = 0;
foreach (var lang in b.Languages)
    foreach (var lref in lang.Layouts)
    {
        n++;
        var l = b.GetLayout(lref);
        if (l.Order.AfterVirama.Count == 0) continue;
        with++;
        Console.WriteLine($"{lang.Id}\t{lref.Id}\t{l.Order}");
    }
Console.WriteLine($"layouts {n}, with after-virama joins {with}");
