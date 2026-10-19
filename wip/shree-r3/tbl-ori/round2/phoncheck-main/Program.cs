using System.Reflection;
using System.Text;
using FontSoftware.Core.Config;
using FontSoftware.Engine;
// phoncheck <repo> <lang> <layoutId> <keys...>: what the real TypingEngine types for each key run, in Unicode (nothing reaches the machine).
sealed class Pad : InputInjector
{
    public readonly StringBuilder Buf = new();
    public override void SendText(string text) => Buf.Append(text);
    public override void SendRichText(string text, string fontName) => Buf.Append(text);
    public override void SendBackspace(int count = 1) => Buf.Length = Math.Max(0, Buf.Length - count);
    public override void Run(Action work) => work();
    public override void LiftHeldCtrl() { }
}
static class P
{
    static readonly HashSet<int> Down = new();
    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        TypingEngine.KeyDownProbe = vk => Down.Contains(vk);
        var b = new ConfigLoader(Path.Combine(args[0], "assets", "config")).Load();
        var lang = b.Languages.First(l => l.Id == args[1]);
        var layout = b.GetLayout(lang.Layouts.First(l => l.Id == args[2]));
        var field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var keys in args.Skip(3))
        {
            var pad = new Pad();
            using var hook = new LowLevelKeyboardHook();
            using var e = new TypingEngine(hook, pad);
            e.SetProfile(layout, lang.Name, "Unicode");
            e.SetTransliterator(b.GetTransliterator(lang));
            e.SetEnabled(true);
            foreach (char c in keys)
            {
                bool shift = char.IsUpper(c);
                int vk = char.ToUpperInvariant(c);
                if (shift) Down.Add(0x10);
                var a = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = true };
                ((EventHandler<HookKeyEventArgs>)field.GetValue(hook))!(hook, a);
                ((EventHandler<HookKeyEventArgs>)field.GetValue(hook))!(hook, new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = false });
                Down.Remove(0x10);
                if (!a.Handled) pad.Buf.Append(c);
            }
            Console.WriteLine($"{args[2]}\t{keys}\t{pad.Buf}\t[{string.Join(" ", pad.Buf.ToString().Select(ch => ((int)ch).ToString("X4")))}]");
        }
    }
}
