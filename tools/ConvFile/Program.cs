using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;

// ConvFile — convert a text file line-by-line through the real bundle, on the same
// path the app uses (the conversion pipeline, not a bare table), so what this tool
// reports is what a customer would see.  convfile <in> <out> <source> <target>
// A layout is an encoding id, or "unicode" for the Unicode side.
if (args.Length < 4) { Console.Error.WriteLine("convfile <in> <out> <source> <target>"); return 1; }
var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
var pipeline = new ConversionPipeline(bundle);

static LayoutKey Layout(string name) =>
    name is "unicode" || name is "mangal" or "shruti" or "vrinda" or "raavi" or "gautami"
        or "tunga" or "kartika" or "kalinga" or "latha" or "nirmala"
        ? LayoutKey.Unicode
        : LayoutKey.Legacy(name);

var source = Layout(args[2]);
var target = Layout(args[3]);
if (!pipeline.CanConvert(source, target, out string? why))
{ Console.Error.WriteLine($"no converter {args[2]} -> {args[3]}: {why}"); return 1; }

var outLines = File.ReadAllLines(args[0])
    .Select(l => l.Length == 0 ? "" : pipeline.Convert(l, source, target).Text);
File.WriteAllLines(args[1], outLines);
Console.WriteLine($"converted {args[0]} -> {args[1]} ({args[2]}->{args[3]})");
return 0;
