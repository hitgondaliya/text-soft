using System.Text;
using FontSoftware.Core.Config;
// Every layout's learned typing order (TypingOrder.Learn), one line each, for a before/after comparison.
Console.OutputEncoding = Encoding.UTF8;
var b = new ConfigLoader(args[0]).Load();
var lines = new List<string>();
foreach (var lang in b.Languages)
    foreach (var lref in lang.Layouts)
    {
        var layout = b.GetLayout(lref);
        var o = layout.Order;
        lines.Add($"{lang.Id}\t{lref.Id}\tactive={o.Active}\t{o}");
    }
File.WriteAllLines(args[1], lines, new UTF8Encoding(false));
Console.WriteLine($"{lines.Count} layouts");
