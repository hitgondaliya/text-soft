using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

// oriaudit - the real TypingEngine on every Odia layout, key by key and word by word, in a Unicode and a SHREE-ORI7
// profile (r3\tbl-ori\repo). Nothing reaches the machine: Sim.cs overrides every injector method.
//   oriaudit l1 <out.json>                     every key of every layer of every Odia layout, both profiles
//   oriaudit type <layoutId> <keys>            one key run, both profiles (Ø = Ctrl+Space, ¤ = Ctrl+Shift+1, \b = Backspace)
//   oriaudit l2 <words.txt> <out.json>         words typed by key plans: final and every intermediate state against the converter
static partial class Program
{
    public static readonly string Repo = Environment.GetEnvironmentVariable("ORI_REPO")
        ?? @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\tbl-ori\repo";
    public static AssetBundle Bundle = null!;
    public static LanguageProfile Or = null!;
    public static FontConverter Fwd = null!;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static (string Name, FontConverter? Conv, string Font)[] Profiles = null!;

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Bundle = new ConfigLoader(Path.Combine(Repo, "assets", "config")).Load();
        Or = Bundle.Languages.First(l => l.Id == "or");
        Fwd = Bundle.CreateConverterToLegacy("shree-ori7")!;
        Profiles = new (string, FontConverter?, string)[] { ("uni", null, "Unicode"), ("o7", Fwd, "SHREE-ORI7-0600 (Shree-Lipi 7.0)") };
        string mode = args.Length > 0 ? args[0] : "";
        return mode switch
        {
            "l1" => L1(args[1]),
            "type" => TypeMode(args[1], args[2]),
            "l2" => L2(args[1], args[2]),
            _ => Usage(),
        };
    }

    static int Usage() { Console.Error.WriteLine("oriaudit l1 <out> | type <layout> <keys> | l2 <words> <out>"); return 1; }

    static readonly Encoding Cp1252 = GetCp();
    static Encoding GetCp() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }

    /// <summary>Legacy text as hex (U+008F U+0090 as 8F 90; a character that is no Windows-1252 byte as U+XXXX).</summary>
    public static string Hex(string s) => string.Join(" ", s.Select(c =>
    {
        if (c is '\u008F' or '\u0090') return ((int)c).ToString("X2");
        try { return Cp1252.GetBytes(c.ToString())[0].ToString("X2"); }
        catch (EncoderFallbackException) { return "U+" + ((int)c).ToString("X4"); }
    }));
    public static string UHex(string s) => string.Join(" ", s.Select(c => ((int)c).ToString("X4")));

    public static KeyboardLayout Layout(string id) => Bundle.GetLayout(Or.Layouts.First(l => l.Id == id));

    static int L1(string outFile)
    {
        var result = new Dictionary<string, object>();
        var translit = Bundle.GetTransliterator(Or);
        foreach (var lref in Or.Layouts)
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
                        if (layer == 1) t.Key(FontSoftware.Core.Models.EscapeLayer.Mark);
                        if (layer == 2) t.Key(FontSoftware.Core.Models.EscapeLayer.SymbolMark);
                        int before = t.App.Buf.Length;
                        var st = t.Key(k);
                        string buf = st.Buffer;
                        row[name] = buf;
                        row[name + "Hex"] = name == "uni" ? UHex(buf) : Hex(buf);
                        row[name + "Handled"] = st.Handled;
                        row[name + "Ev"] = st.Events;
                        // the key, then a space: a run still waiting for its next key shows what it ends as
                        var sp = t.Key(' ');
                        string after = sp.Buffer.EndsWith(' ') ? sp.Buffer[..^1] : sp.Buffer;
                        row[name + "Space"] = name == "uni" ? UHex(after) : Hex(after);
                    }
                    keys.Add(row);
                }
            result[lref.Id] = new { name = layout.Name, phonetic = layout.Phonetic, order = layout.Order.ToString(), keys };
            Console.WriteLine($"{lref.Id}: {keys.Count} keys");
        }
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, Json));
        return 0;
    }

    static int TypeMode(string layoutId, string keys)
    {
        var layout = Layout(layoutId);
        var tr = Bundle.GetTransliterator(Or);
        keys = keys.Replace("\\b", "\b").Replace("\\n", "\n");
        foreach (var (name, conv, font) in Profiles)
        {
            using var t = new Typist(layout, tr, conv, font);
            foreach (char k in keys) t.Key(k);
            foreach (var s in t.Steps)
                Console.WriteLine($"{name}\t{s.Key}\t{(s.Handled ? "H" : "-")}\t{(name == "uni" ? s.Buffer + " [" + UHex(s.Buffer) + "]" : Hex(s.Buffer))}\t{s.Events}");
        }
        return 0;
    }
}
