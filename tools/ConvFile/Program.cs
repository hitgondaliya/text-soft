using FontSoftware.Core.Config;

// ConvFile — convert a text file line-by-line through the real bundle. Used to
// self-test the capture pipeline (simulate a Shree legacy export) and for ad-hoc
// conversion.  convfile <in> <out> <sourceEnc> <targetEnc>
if (args.Length < 4) { Console.Error.WriteLine("convfile <in> <out> <sourceEnc> <targetEnc>"); return 1; }
var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();
var conv = bundle.CreateConverter(args[2], args[3]);
if (conv is null) { Console.Error.WriteLine($"no converter {args[2]} -> {args[3]}"); return 1; }
var outLines = File.ReadAllLines(args[0]).Select(l => l.Length == 0 ? "" : conv.ConvertText(l));
File.WriteAllLines(args[1], outLines);
Console.WriteLine($"converted {args[0]} -> {args[1]} ({args[2]}->{args[3]})");
return 0;
