using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Keyboards;
using FontSoftware.Core.Models;

// telaudit - the real TypingEngine (repo copy r3\tbl-tel\repo) on every Telugu layout, key by key, in a Unicode, a
// SHREE-TEL7 (shree-tel7 table) and a Shree-Tel7-001 (shree-tel7w table) profile. Nothing reaches the machine: the engine
// is driven through its hook event (no hook installed) into a simulated application (Sim.cs, from the Punjabi audit).
//   telaudit l1                      every key of every layout on layer 0, Ctrl+Space and Ctrl+Shift+1 -> results/l1-textsoft-keys.json
//   telaudit type <layoutId> <keys>  step by step (<CS> = Ctrl+Space, <CS1> = Ctrl+Shift+1, <BS> = Backspace)
//   telaudit words <layoutId> <keys;unicode;...>  each run typed in every profile against FontConverter(unicode)
static partial class Program
{
    public static string Repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "repo"));
    public static AssetBundle Bundle = null!;
    public static LanguageProfile Te = null!;
    public static FontConverter Fwd7 = null!, Fwd7w = null!;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static (string Name, FontConverter? Conv, string Font)[] Profiles = null!;
    static string Out => Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "results");

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string cfg = Environment.GetEnvironmentVariable("TELAUDIT_CONFIG") is { Length: > 0 } c ? c : Path.Combine(Repo, "assets", "config");
        Bundle = new ConfigLoader(cfg).Load();
        Te = Bundle.Languages.First(l => l.Id == "te");
        Fwd7 = Bundle.CreateConverterToLegacy("shree-tel7")!;
        Fwd7w = Bundle.CreateConverterToLegacy("shree-tel7w") ?? Fwd7;   // the shipped config has no tel7w: Shree-Tel7-00x used shree-tel7
        Profiles = new (string, FontConverter?, string)[] { ("uni", null, "Unicode"), ("t7", Fwd7, "SHREE-TEL7-Apoorva-0900 (Shree-Lipi 7.0)"), ("t7w", Fwd7w, "Shree-Tel7-001 (Shree-Lipi 7.0)") };
        Directory.CreateDirectory(Out);
        string mode = args.Length > 0 ? args[0] : "";
        return mode switch
        {
            "l1" => L1(),
            "type" => TypeMode(args),
            "words" => WordsMode(args),
            _ => Usage(),
        };
    }

    static int Usage() { Console.Error.WriteLine("telaudit l1 | type <layout> <keys> | words <layout> <keys;unicode;...>"); return 1; }

    static readonly Encoding Cp1252 = GetCp();
    static Encoding GetCp() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(1252); }
    public static string Hex(string s) => string.Join(" ", s.Select(c =>
    {
        if (c == '\u0090') return "90";
        var b = Cp1252.GetBytes(new[] { c });
        return b.Length == 1 && Cp1252.GetString(b) == c.ToString() ? b[0].ToString("X2") : "U+" + ((int)c).ToString("X4");
    }));
    public static string UHex(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    static int L1()
    {
        var result = new Dictionary<string, object>();
        foreach (var lref in Te.Layouts)
        {
            var layout = Bundle.GetLayout(lref);
            var tr = layout.Phonetic ? Bundle.GetTransliterator(Te) : null;
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
        File.WriteAllText(Path.Combine(Out, Environment.GetEnvironmentVariable("TELAUDIT_OUT") ?? "l1-textsoft-keys.json"), JsonSerializer.Serialize(result, Json));
        return 0;
    }

    static int TypeMode(string[] args)
    {
        var layout = Bundle.GetLayout(Te.Layouts.First(l => l.Id == args[1]));
        var tr = layout.Phonetic ? Bundle.GetTransliterator(Te) : null;
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

    /// <summary>words &lt;layout&gt; "keys=unicode;keys=unicode": each run typed in every profile; the legacy buffers against the converter's bytes for the Unicode.</summary>
    static int WordsMode(string[] args)
    {
        var layout = Bundle.GetLayout(Te.Layouts.First(l => l.Id == args[1]));
        var tr = layout.Phonetic ? Bundle.GetTransliterator(Te) : null;
        int ok = 0, n = 0;
        var items = File.Exists(args[2]) ? File.ReadAllLines(args[2], Encoding.UTF8).Where(l => l.Length > 0).Select(l => l.Split('	')).ToArray()
                  : args[2].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Split('=')).ToArray();
        foreach (var parts in items)
        {
            string keys = parts[0].Replace("<BS>", "\b").Replace("<CS>", "Ø").Replace("<CS1>", "¤"), want = parts.Length > 1 ? parts[1] : "";
            foreach (var (name, conv, font) in Profiles)
            {
                using var t = new Typist(layout, tr, conv, font);
                foreach (char k in keys) t.Key(k);
                string buf = t.App.Buf.ToString();
                string expect = conv is null ? want : conv.ConvertText(want);
                n++;
                bool good = want.Length == 0 || buf == expect;
                if (good) ok++;
                Console.WriteLine($"{(good ? "ok " : "BAD")} {args[1]} {parts[0]} {name,-4} {(conv is null ? buf + " [" + UHex(buf) + "]" : Hex(buf))}{(good ? "" : "   want " + (conv is null ? want + " [" + UHex(want) + "]" : Hex(expect)))}");
            }
        }
        Console.WriteLine($"{ok}/{n}");
        return ok == n ? 0 : 2;
    }
}
