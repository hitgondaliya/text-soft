using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using FontSoftware.Engine;

/// <summary>The application at the other end: a buffer. Nothing reaches the machine (every send is overridden).</summary>
sealed class Pad : InputInjector
{
    public readonly StringBuilder Buf = new();
    public override void SendText(string text) => Buf.Append(text);
    public override void SendRichText(string text, string fontName) => Buf.Append(text);
    public override void SendBackspace(int count = 1) => Buf.Length = Math.Max(0, Buf.Length - count);
    public override void Run(Action work) => work();
    public override void LiftHeldCtrl() { }
}

/// <summary>Drives the real TypingEngine the way TypingEngineTests does: the hook's event raised by reflection; no hook installed.</summary>
sealed class Typer : IDisposable
{
    const int Shift = 0x10, Ctrl = 0x11, Space = 0x20, Back = 0x08;
    static readonly HashSet<int> Down = new();
    static Typer() => TypingEngine.KeyDownProbe = vk => Down.Contains(vk);
    readonly LowLevelKeyboardHook _hook = new();
    public readonly Pad App = new();
    readonly TypingEngine _e;
    static readonly FieldInfo Field = typeof(LowLevelKeyboardHook).GetField("KeyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public Typer(KeyboardLayout layout, FontConverter legacy)
    {
        Down.Clear();
        _e = new TypingEngine(_hook, App);
        _e.SetProfile(layout, "Odia", legacy is null ? "Nirmala UI" : "SHREE-ORI7-0600");
        _e.SetTransliterator(K.B.GetTransliterator(K.Or));
        _e.SetLegacyConverter(legacy, null);
        _e.SetEnabled(true);
    }

    bool Send(int vk, bool down)
    {
        var a = new HookKeyEventArgs { VirtualKey = vk, IsKeyDown = down };
        ((EventHandler<HookKeyEventArgs>)Field.GetValue(_hook))!(_hook, a);
        return a.Handled;
    }

    public static (int Vk, bool Shift) Vk(char c)
    {
        if (c >= 'a' && c <= 'z') return (char.ToUpperInvariant(c), false);
        if (c >= 'A' && c <= 'Z') return (c, true);
        if (c >= '0' && c <= '9') return (c, false);
        int i = ")!@#$%^&*(".IndexOf(c);
        if (i >= 0) return ('0' + i, true);
        return c switch
        {
            ';' => (186, false), ':' => (186, true), '=' => (187, false), '+' => (187, true), ',' => (188, false), '<' => (188, true),
            '-' => (189, false), '_' => (189, true), '.' => (190, false), '>' => (190, true), '/' => (191, false), '?' => (191, true),
            '`' => (192, false), '~' => (192, true), '[' => (219, false), '{' => (219, true), '\\' => (220, false), '|' => (220, true),
            ']' => (221, false), '}' => (221, true), '\'' => (222, false), '"' => (222, true), ' ' => (Space, false), '\b' => (Back, false),
            _ => (0, false),
        };
    }

    /// <summary>One keystroke: a US key character, Ø = Ctrl+Space, ¤ = Ctrl+Shift+1, \b = Backspace. Returns whether the engine took it.</summary>
    public bool Key(char c)
    {
        if (c == EscapeLayer.Mark || c == EscapeLayer.SymbolMark)
        {
            Down.Add(Ctrl); if (c == EscapeLayer.SymbolMark) Down.Add(Shift);
            int vk = c == EscapeLayer.Mark ? Space : '1';
            bool h = Send(vk, true); Send(vk, false);
            Down.Remove(Ctrl); Down.Remove(Shift);
            if (!h) App.Buf.Append(c == EscapeLayer.Mark ? " " : "!");
            return h;
        }
        var (v, sh) = Vk(c);
        if (v == 0) throw new ArgumentException("no key for " + (int)c);
        if (sh) Down.Add(Shift);
        bool handled = Send(v, true); Send(v, false);
        Down.Remove(Shift);
        if (!handled) { if (c == '\b') { if (App.Buf.Length > 0) App.Buf.Length--; } else App.Buf.Append(c); }
        return handled;
    }

    public void Dispose() { _e.Dispose(); _hook.Dispose(); }
}

static class TypingAudit
{
    static readonly (string Id, string File)[] Map =
    {
        ("or-inscript", "INSCRIPT.ORI"), ("ori-crtronic", "CRTRONIC.ORI"), ("ori-doe", "DOE.ORI"), ("ori-eng", "ENG.ORI"),
        ("ori-newroma", "NEWROMA.ORI"), ("ori-phonetic", "PHONETIC.ORI"), ("ori-roma", "ROMA.ORI"), ("ori-typewrit", "TYPEWRIT.ORI"),
    };

    static KeyboardLayout Layout(string id) => K.B.GetLayout(K.Or.Layouts.First(l => l.Id == id));

    static (string Uni, string Leg, bool HU, bool HL, string UniSp, string LegSp) TypeBoth(KeyboardLayout layout, string keys)
    {
        string u, l, us, ls; bool hu = true, hl = true;
        using (var t = new Typer(layout, null)) { foreach (char k in keys) hu = t.Key(k); u = t.App.Buf.ToString(); t.Key(' '); us = t.App.Buf.ToString().TrimEnd(' '); }
        using (var t = new Typer(layout, K.Fwd)) { foreach (char k in keys) hl = t.Key(k); l = t.App.Buf.ToString(); t.Key(' '); ls = t.App.Buf.ToString().TrimEnd(' '); }
        return (u, l, hu, hl, us, ls);
    }

    /// <summary>Every key of every layer of the 8 vendor keyboards against Shree's own per-key answers.</summary>
    public static void Keys(string outDir)
    {
        var V = K.ReadJson(K.VendorKeys);
        var tot = new SortedDictionary<string, int>();
        var perLayout = new SortedDictionary<string, SortedDictionary<string, int>>();
        using var w = K.Out(outDir, "keys-detail.tsv");
        foreach (var (id, file) in Map)
        {
            var layout = Layout(id);
            var layers = V["keyboards"]![file]!["layers"]!;
            var pl = perLayout[id] = new SortedDictionary<string, int>();
            foreach (int layer in new[] { 0, 1, 3 })
                for (char key = '!'; key <= '~'; key++)
                {
                    JsonNode v;
                    if (layer == 3) { v = V["ctrlShift1"]!["keys"]![key.ToString()]; }
                    else v = layers[layer.ToString()]![key.ToString()];
                    string prefix = layer == 0 ? "" : layer == 1 ? EscapeLayer.Mark.ToString() : EscapeLayer.SymbolMark.ToString();
                    var (uni, leg, hu, hl, uniSp, legSp) = TypeBoth(layout, prefix + key);
                    string b7 = v is null ? null : (string)v["b7"];
                    string su = v is null ? null : (string)v["u"];
                    string codes = v is null ? null : (string)(v["codes"] ?? v["code"]);
                    bool shreeTypes = !string.IsNullOrEmpty(b7);
                    string want = shreeTypes ? K.Ex(b7, true, true) : "";
                    string legHex = K.Hex(leg), legSpHex = K.Hex(legSp);
                    bool raw = !hl && leg == key.ToString();
                    bool rawDrawsItself = Measure.Itself.Contains(key);
                    string cls;
                    bool phon = file is "ENG.ORI" or "PHONETIC.ORI";
                    if (b7 == "07") cls = "q quirk: composer carrier 07";
                    else if (!shreeTypes)
                    {
                        if (leg.Length == 0) cls = "ok both nothing";
                        else if (raw && rawDrawsItself) cls = "ok Shree nothing, key reaches the app as itself (drawn as itself)";
                        else if (raw) cls = "BAD Shree nothing, raw ASCII reaches the Shree font (draws Odia)";
                        else cls = "X Shree types nothing, TextSoft types something";
                    }
                    else if (legHex == want || (legSpHex == want && legSp.Length > 0)) cls = legHex == want ? "ok same bytes" : "ok same bytes after Space (run pending)";
                    else if (leg.Length == 0) cls = "A TextSoft types nothing, Shree types";
                    else if (raw) cls = rawDrawsItself && want == K.Hex(key.ToString()) ? "ok same (raw itself)" : "A raw ASCII reaches the font, Shree types other";
                    else if (phon && su is not null && su.EndsWith("୍") && uni.Length > 0 && su.StartsWith(uni)) cls = "q phonetic mode: Shree answers the virama form";
                    else if (su is not null && (uni == su || uniSp == su)) cls = "B same Unicode, other bytes";
                    else cls = "C other text";
                    string lname = layer == 3 ? "C+S+1" : layer == 1 ? "C+Sp" : "L0";
                    tot[cls] = tot.GetValueOrDefault(cls) + 1;
                    pl[cls] = pl.GetValueOrDefault(cls) + 1;
                    if (!cls.StartsWith("ok"))
                        w.WriteLine($"{cls}\t{id}\t{lname}\t{key}\tcodes {codes}\tShree u {su} b7 {b7} (want {want})\tTS uni {uni} [{K.Cps(uni)}]\tTS leg {legHex} | after space {legSpHex}\thandled {hl}");
                }
        }
        Console.WriteLine("PER-KEY AUDIT (8 vendor keyboards x 94 keys x {normal/shift, Ctrl+Space, Ctrl+Shift+1}; SHREE-ORI7 profile vs Shree's 7 composer bytes):");
        foreach (var (k, v) in tot) Console.WriteLine($"  {k}: {v}");
        foreach (var (id, c) in perLayout) Console.WriteLine($"  {id}: {string.Join("  ", c.Where(x => !x.Key.StartsWith("ok")).Select(x => $"{x.Key.Split(' ')[0]}{x.Key[(x.Key.IndexOf(' ') + 1)..][..Math.Min(18, x.Key.Length - x.Key.IndexOf(' ') - 1)]}={x.Value}"))}");
    }

    /// <summary>
    /// Random keystrokes on each layout in a Unicode and a SHREE-ORI7 profile: after every key, the legacy screen must be
    /// the converter's output for the Unicode screen (P6, typing = conversion); and one Backspace must take the screen back
    /// to a state it had before (never more than the last keystroke's text).
    /// </summary>
    public static void Random(string outDir, int perLayout, int seed)
    {
        using var w = K.Out(outDir, $"random-{seed}.tsv");
        Console.WriteLine($"RANDOM TYPING (seed {seed}, {perLayout} runs per layout, 1-7 keys, then Space; then Backspace checks):");
        foreach (var lref in K.Or.Layouts)
        {
            var layout = K.B.GetLayout(lref);
            // the keys this layout maps on the normal/shift layers
            var keys = new List<char>();
            for (char k = '!'; k <= '~'; k++) keys.Add(k);
            var rng = new Random(seed);
            int steps = 0, mism = 0, finals = 0, finalMism = 0, bsN = 0, bsOver = 0, bsUnder = 0, bsOverL = 0, bsUnderL = 0, rawSteps = 0;
            for (int run = 0; run < perLayout; run++)
            {
                int n = 1 + rng.Next(7);
                var seq = new StringBuilder();
                for (int i = 0; i < n; i++) { if (rng.Next(12) == 0) seq.Append(EscapeLayer.Mark); seq.Append(keys[rng.Next(keys.Count)]); }
                string s = seq.ToString();
                using var tu = new Typer(layout, null);
                using var tl = new Typer(layout, K.Fwd);
                var histU = new List<string> { "" }; var histL = new List<string> { "" };
                foreach (char k in s)
                {
                    tu.Key(k); bool hl = tl.Key(k);
                    string u = tu.App.Buf.ToString(), l = tl.App.Buf.ToString();
                    histU.Add(u); histL.Add(l);
                    if (k == EscapeLayer.Mark) continue;
                    steps++;
                    if (!hl) rawSteps++;
                    string conv = K.Hex(K.Fwd.ConvertText(u)), got = K.Hex(l);
                    if (conv != got)
                    {
                        mism++;
                        if (mism <= 300) w.WriteLine($"STEP\t{lref.Id}\t{Show(s)}\tafter '{Show(k.ToString())}'\tuni {u} [{K.Cps(u)}]\tconv {conv}\tlegacy {got}");
                    }
                }
                // Backspace once: the screen must be one of the earlier screens (one keystroke's worth back), not shorter
                {
                    string beforeU = tu.App.Buf.ToString(), beforeL = tl.App.Buf.ToString();
                    tu.Key('\b'); tl.Key('\b');
                    string au = tu.App.Buf.ToString(), al = tl.App.Buf.ToString();
                    bsN++;
                    string prevU = histU[^2], prevL = histL[^2];
                    if (au != prevU) { if (prevU.StartsWith(au) && au.Length < prevU.Length) bsOver++; else bsUnder++; if (bsOver + bsUnder <= 200) w.WriteLine($"BS-UNI\t{lref.Id}\t{Show(s)}\tbefore {beforeU}\tprev {prevU}\tafter BS {au}"); }
                    if (al != prevL) { if (prevL.StartsWith(al) && al.Length < prevL.Length) bsOverL++; else bsUnderL++; if (bsOverL + bsUnderL <= 200) w.WriteLine($"BS-LEG\t{lref.Id}\t{Show(s)}\tbefore {K.Hex(beforeL)}\tprev {K.Hex(prevL)}\tafter BS {K.Hex(al)}"); }
                }
                // then finish the word: the final legacy text must be the converter's output for the final Unicode text
                tu.Key(' '); tl.Key(' ');
                finals++;
                string fu = tu.App.Buf.ToString(), fl = tl.App.Buf.ToString();
                if (K.Hex(K.Fwd.ConvertText(fu)) != K.Hex(fl))
                {
                    finalMism++;
                    if (finalMism <= 100) w.WriteLine($"FINAL\t{lref.Id}\t{Show(s)}+BS+Space\tuni {fu} [{K.Cps(fu)}]\tconv {K.Hex(K.Fwd.ConvertText(fu))}\tlegacy {K.Hex(fl)}");
                }
            }
            Console.WriteLine($"  {lref.Id,-13} keystrokes {steps} (not taken by the engine {rawSteps}): legacy screen != converter(Unicode screen) {mism}; finished words {finals}: mismatch {finalMism}; Backspace {bsN}: Unicode over-deletes {bsOver}, other {bsUnder}; legacy over-deletes {bsOverL}, other {bsUnderL}");
        }
    }

    /// <summary>
    /// A key that types nothing, struck inside a word: for each layout, the keys that type nothing alone (K), the keys that
    /// type a sign, a virama or a consonant (X), and a key that types a consonant (C): C K X Space must put down what C X Space
    /// puts down (same Unicode), and the legacy screen must be the converter's output for the Unicode screen.
    /// </summary>
    public static void EmptyKey(string outDir)
    {
        using var w = K.Out(outDir, "emptykey.tsv");
        Console.WriteLine("EMPTY KEY INSIDE A WORD (C K X Space vs C X Space):");
        foreach (var lref in K.Or.Layouts)
        {
            var layout = K.B.GetLayout(lref);
            string Alone(string keys, FontConverter conv) { using var t = new Typer(layout, conv); foreach (char k in keys) t.Key(k); return t.App.Buf.ToString(); }
            var empty = new List<char>(); var signs = new List<char>(); char cons = '\0';
            for (char k = '!'; k <= '~'; k++)
            {
                string u = Alone(k.ToString(), null);
                if (u.Length == 0) empty.Add(k);
                else if (u.Length >= 1 && (K.IsMark(u[0]) || u[0] == K.Virama || (u.Length == 1 && K.IsCons(u[0])))) signs.Add(k);
                if (cons == '\0' && u == "କ") cons = k;
            }
            if (cons == '\0') { foreach (char k in "kKjJ") if (Alone(k.ToString(), null).StartsWith("କ")) { cons = k; break; } }
            int n = 0, uniDiff = 0, legBad = 0, legBadBase = 0;
            foreach (char e in empty)
                foreach (char x in signs)
                {
                    string withK = "" + cons + e + x + " ", without = "" + cons + x + " ";
                    string u1 = Alone(withK, null), u0 = Alone(without, null), l1 = Alone(withK, K.Fwd), l0 = Alone(without, K.Fwd);
                    n++;
                    if (u1 != u0) uniDiff++;
                    bool bad = K.Hex(l1) != K.Hex(K.Fwd.ConvertText(u1));
                    if (bad) { legBad++; if (legBad <= 40) w.WriteLine($"{lref.Id}\t{Show(withK)}\tuni {u1.TrimEnd()} [{K.Cps(u1.TrimEnd())}]\tlegacy {K.Hex(l1)}\tconv {K.Hex(K.Fwd.ConvertText(u1))}\twithout the empty key: legacy {K.Hex(l0)}"); }
                    if (K.Hex(l0) != K.Hex(K.Fwd.ConvertText(u0))) legBadBase++;
                }
            Console.WriteLine($"  {lref.Id,-13} empty keys {empty.Count} ({new string(empty.ToArray())}); sign/virama/consonant keys {signs.Count}; ka key '{cons}': runs {n}, Unicode changed by the empty key {uniDiff}, legacy != converter {legBad} (without the empty key: {legBadBase / Math.Max(1, empty.Count)} per empty key)");
        }
    }

    static string Show(string s) => s.Replace(EscapeLayer.Mark.ToString(), "Ø").Replace("\b", "\\b");

    /// <summary>One run of keys on one layout, both profiles, every step.</summary>
    public static void One(string id, string keys)
    {
        var layout = Layout(id);
        keys = keys.Replace("\\b", "\b").Replace("Ø", EscapeLayer.Mark.ToString()).Replace("¤", EscapeLayer.SymbolMark.ToString());
        using var tu = new Typer(layout, null);
        using var tl = new Typer(layout, K.Fwd);
        foreach (char k in keys)
        {
            bool hu = tu.Key(k), hl = tl.Key(k);
            string u = tu.App.Buf.ToString(), l = tl.App.Buf.ToString();
            Console.WriteLine($"  '{Show(k.ToString())}'\t{(hl ? "H" : "-")}\tuni {u} [{K.Cps(u)}]\tlegacy {K.Hex(l)}\tconv {K.Hex(K.Fwd.ConvertText(u))}");
        }
    }
}
