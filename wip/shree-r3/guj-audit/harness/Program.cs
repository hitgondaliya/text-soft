using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

static partial class Program
{
    public static readonly string A = AppContext.GetData("AUDIT") as string
        ?? @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\guj-audit";
    public static AssetBundle Bundle = null!;
    public static LanguageProfile Gu = null!;
    public static FontConverter Fwd7 = null!, Fwd456 = null!, Rev7 = null!, Rev456 = null!;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Profiles: name, converter, font name the app would show.
    public static (string Name, FontConverter? Conv, string Font)[] Profiles = null!;

    [STAThread]
    static int Main(string[] args)
    {
        Bundle = new ConfigLoader(Path.Combine(A, "repo", "assets", "config")).Load();
        Gu = Bundle.Languages.First(l => l.Id == "gu");
        Fwd7 = Bundle.CreateConverterToLegacy("shree-guj7")!;
        Fwd456 = Bundle.CreateConverterToLegacy("shree-guj456")!;
        Rev7 = Bundle.CreateConverter("shree-guj7", "unicode") ?? new FontConverter(Bundle.MappingTables.First(m => m.SourceFontId == "shree-guj7" && m.TargetType == FontType.Unicode));
        Rev456 = new FontConverter(Bundle.MappingTables.First(m => m.SourceFontId == "shree-guj456" && m.TargetType == FontType.Unicode));
        Profiles = new (string, FontConverter?, string)[] { ("uni", null, "Unicode"), ("g7", Fwd7, "SHREE-GUJ7-0750 (Shree-Lipi 7.0)"), ("g456", Fwd456, "SHREE-GUJ-0750 (Shree-Lipi 4.0/5.0/6.0)") };
        string mode = args.Length > 0 ? args[0] : "";
        return mode switch
        {
            "l1" => L1(),
            "l2" => L2(args),
            "conv" => Conv(args[1], args[2]),
            "l6" => L6(args),
            "l5" => L5(args),
            "fonts" => FontsMode(args),
            "fuzz" => Fuzz(args),
            "type" => TypeMode(args),
            "docx" => DocxMode(args),
            "typeu" => TypeUMode(args),
            _ => Usage(),
        };
    }

    static int Usage() { Console.Error.WriteLine("harness l1 | l2 <words> | conv <in.json> <out.json> | l5 | l6"); return 1; }

    public static string Hex(string s) => string.Join(" ", s.Select(c => c <= 0xFF ? ((int)c).ToString("X2") : Cp1252Back(c)));
    static readonly Encoding Cp1252 = GetCp();
    static Encoding GetCp() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(1252); }
    /// <summary>A legacy-text character as the byte it stands for (Windows-1252), or U+XXXX when it is not one.</summary>
    public static string Cp1252Back(char c)
    {
        var b = Cp1252.GetBytes(new[] { c });
        if (b.Length == 1 && Cp1252.GetString(b) == c.ToString()) return b[0].ToString("X2");
        return "U+" + ((int)c).ToString("X4");
    }
    public static string UHex(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    public static KeyboardLayout Layout(string id) => Bundle.GetLayout(Gu.Layouts.First(l => l.Id == id));

    // ---------------- L1: every key on every layer, every profile ----------------
    static int L1()
    {
        var result = new Dictionary<string, object>();
        var translit = Bundle.GetTransliterator(Gu);
        foreach (var lref in Gu.Layouts)
        {
            var layout = Bundle.GetLayout(lref);
            var keys = new List<object>();
            foreach (int layer in new[] { 0, 1, 2 })
            for (char k = '!'; k <= '~'; k++)
            {
                var row = new Dictionary<string, object> { ["layer"] = layer, ["key"] = k.ToString() };
                foreach (var (name, conv, font) in Profiles)
                {
                    using var t = new Typist(layout, translit, conv, font);
                    if (layer == 1) t.Key(EscapeLayer.Mark);
                    if (layer == 2) t.Key(EscapeLayer.SymbolMark);
                    var st = t.Key(k);
                    // a run on the layer may still be waiting for a key that finishes it: say what it shows
                    row[name] = st.Buffer;
                    row[name + "Hex"] = name == "uni" ? UHex(st.Buffer) : Hex(st.Buffer);
                    row[name + "Handled"] = st.Handled;
                    row[name + "Ev"] = st.Events;
                }
                keys.Add(row);
            }
            result[lref.Id] = new { name = layout.Name, phonetic = layout.Phonetic, order = layout.Order.ToString(), keys };
            Console.WriteLine($"{lref.Id}: {keys.Count} keys");
        }
        File.WriteAllText(Path.Combine(A, "results", "l1-textsoft-keys.json"), JsonSerializer.Serialize(result, Json));
        return 0;
    }

    // ---------------- conv: run strings through the real converters ----------------
    sealed class ConvIn { public string Dir { get; set; } = ""; public string Text { get; set; } = ""; public string? Tag { get; set; } }
    static int Conv(string inFile, string outFile)
    {
        var items = JsonSerializer.Deserialize<List<ConvIn>>(File.ReadAllText(inFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var outList = new List<object>();
        foreach (var it in items)
        {
            var conv = it.Dir switch { "fwd7" => Fwd7, "fwd456" => Fwd456, "rev7" => Rev7, "rev456" => Rev456, _ => throw new Exception(it.Dir) };
            var r = conv.Convert(it.Text);
            bool toLegacy = it.Dir.StartsWith("fwd");
            outList.Add(new
            {
                it.Dir, it.Tag, it.Text,
                inHex = toLegacy ? UHex(it.Text) : Hex(it.Text),
                @out = r.Text,
                outHex = toLegacy ? Hex(r.Text) : UHex(r.Text),
                unmapped = r.UnmappedCharacters,
                // what the Converter screen reports (ConverterViewModel keeps only characters above U+007F)
                reported = r.UnmappedCharacters.Where(u => u.Length > 0 && u[0] > '\x7F').ToArray(),
            });
        }
        File.WriteAllText(outFile, JsonSerializer.Serialize(outList, Json));
        Console.WriteLine($"{outList.Count} conversions -> {outFile}");
        return 0;
    }
}
