using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;

// punaudit - the real TypingEngine (repo copy r3\tbl-pun\repo) on every Punjabi layout, key by key and word by word,
// in a Unicode, a SHREE-PUN7 (shree-pun7 table) and a SHREE-PUN (shree-pun456 table) profile. Nothing reaches the
// machine: the engine is driven through its hook event (no hook installed) into a simulated application (Sim.cs).
//   punaudit l1                      every key of every layout on layer 0, Ctrl+Space and Ctrl+Shift+1 -> results/l1-textsoft-keys.json
//   punaudit l2 [layoutIds]          the capture's 238 common words, typed by plan -> results/l2-words.json
//   punaudit type <layoutId> <keys>  step by step (<CS> = Ctrl+Space, <CS1> = Ctrl+Shift+1, <BS> = Backspace)
static partial class Program
{
    public static readonly string A = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd('\\', '/')))!)!;
    public static string Repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "repo"));
    public static AssetBundle Bundle = null!;
    public static LanguageProfile Pa = null!;
    public static FontConverter Fwd7 = null!, Fwd456 = null!;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static (string Name, FontConverter? Conv, string Font)[] Profiles = null!;
    static string Out => Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "results");

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string cfg = Environment.GetEnvironmentVariable("PUNAUDIT_CONFIG") is { Length: > 0 } c ? c : Path.Combine(Repo, "assets", "config");
        Bundle = new ConfigLoader(cfg).Load();
        Pa = Bundle.Languages.First(l => l.Id == "pa");
        Fwd7 = Bundle.CreateConverterToLegacy("shree-pun7")!;
        Fwd456 = Bundle.CreateConverterToLegacy("shree-pun456") ?? Fwd7;   // the shipped config has no pun456: its 4/5/6 fonts used shree-pun7
        Profiles = new (string, FontConverter?, string)[] { ("uni", null, "Unicode"), ("p7", Fwd7, "SHREE-PUN7-0951 (Shree-Lipi 7.0)"), ("p456", Fwd456, "SHREE-PUN-0950 (Shree-Lipi 4.0/5.0/6.0)") };
        Directory.CreateDirectory(Out);
        string mode = args.Length > 0 ? args[0] : "";
        return mode switch
        {
            "l1" => L1(),
            "l2" => L2(args),
            "type" => TypeMode(args),
            _ => Usage(),
        };
    }

    static int Usage() { Console.Error.WriteLine("punaudit l1 | l2 [layouts] | type <layout> <keys>"); return 1; }

    static readonly Encoding Cp1252 = GetCp();
    static Encoding GetCp() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(1252); }
    public static string Hex(string s) => string.Join(" ", s.Select(c =>
    {
        var b = Cp1252.GetBytes(new[] { c });
        return b.Length == 1 && Cp1252.GetString(b) == c.ToString() ? b[0].ToString("X2") : "U+" + ((int)c).ToString("X4");
    }));
    public static string UHex(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    static int L1()
    {
        var result = new Dictionary<string, object>();
        foreach (var lref in Pa.Layouts)
        {
            var layout = Bundle.GetLayout(lref);
            var tr = layout.Phonetic ? Bundle.GetTransliterator(Pa) : null;
            var keys = new List<object>();
            foreach (int layer in new[] { 0, 1, 2 })
                for (char k = '!'; k <= '~'; k++)
                {
                    var row = new Dictionary<string, object> { ["layer"] = layer, ["key"] = k.ToString() };
                    string uni = "";
                    foreach (var (name, conv, font) in Profiles)
                    {
                        using var t = new Typist(layout, tr, conv, font);
                        if (layer == 1) t.Key(EscapeLayer.Mark);
                        if (layer == 2) t.Key(EscapeLayer.SymbolMark);
                        var st = t.Key(k);
                        if (name == "uni") uni = st.Buffer;
                        row[name] = st.Buffer;
                        row[name + "Hex"] = name == "uni" ? UHex(st.Buffer) : Hex(st.Buffer);
                        row[name + "Handled"] = st.Handled;
                        if (conv is not null) row[name + "IsConverterOfUnicode"] = st.Buffer == conv.ConvertText(uni);
                    }
                    keys.Add(row);
                }
            result[lref.Id] = new { name = layout.Name, phonetic = layout.Phonetic, order = layout.Order.ToString(), keys };
            Console.WriteLine($"{lref.Id}: {keys.Count} keys");
        }
        File.WriteAllText(Path.Combine(Out, Environment.GetEnvironmentVariable("PUNAUDIT_OUT") ?? "l1-textsoft-keys.json"), JsonSerializer.Serialize(result, Json));
        return 0;
    }

    static int TypeMode(string[] args)
    {
        var layout = Bundle.GetLayout(Pa.Layouts.First(l => l.Id == args[1]));
        var tr = layout.Phonetic ? Bundle.GetTransliterator(Pa) : null;
        for (int i = 2; i < args.Length; i++)
        {
            string keys = args[i].Replace("<BS>", "\b").Replace("<CS>", "Ø").Replace("<CS1>", "¤");
            Console.WriteLine($"== {args[1]} keys {args[i]}   (order: {layout.Order})");
            foreach (var (name, conv, font) in Profiles)
            {
                using var t = new Typist(layout, tr, conv, font);
                foreach (char k in keys) t.Key(k);
                Console.WriteLine($"  {name,-5} " + string.Join("  ", t.Steps.Select(s => $"[{(s.Key == "\b" ? "BS" : s.Key)}{(s.Handled ? "" : "*")}] {(name == "uni" ? UHex(s.Buffer) + " " + s.Buffer : Hex(s.Buffer))}")));
            }
        }
        return 0;
    }
}
