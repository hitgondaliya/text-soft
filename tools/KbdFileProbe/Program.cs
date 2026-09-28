using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Keyboards;

// KbdFileProbe - read Shree-Lipi's internal character codes out of Shree's own
// composer, and prove the keyboard-file importer against it.
//
// A keyboard file (INSCRIPT.GUJ, and whatever the vendor's Keyboard Generator
// saves) is 406 bytes of internal codes that nothing documents. But the composer
// DLL answers "on this keyboard, what does this key type", and loads any file we
// hand it. So a file whose keys carry codes we chose is a question, and the
// answers are the code table:
//
//   kbdfileprobe --table  [--script GUJ]     write assets/config/shree-kbd/<script>.json
//   kbdfileprobe --verify [--script GUJ]     every keyboard Shree ships: importer vs composer
//
// Nothing touches the keyboard or the screen; it runs while the machine is in use.

string shree = @"C:\Shree-Lipi nxt";
string? only = null;
bool table = args.Contains("--table"), verify = args.Contains("--verify");
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--shree") shree = args[++i];
    if (args[i] == "--script") only = args[++i].ToUpperInvariant();
}
string repo = FindRoot();
string tables = Path.Combine(repo, "assets", "config", "shree-kbd");
Directory.CreateDirectory(tables);

int worst = 0;
foreach (string script in ShreeKeyboardFile.Scripts.Where(s => only is null || s == only))
{
    // One process per composer would be cleaner, but every composer exports the
    // same names, so they can share one as long as each is asked through its own
    // entry points.
    var composer = Composer.Open(shree, script);
    if (composer is null) { Console.WriteLine($"{script}: no composer"); continue; }

    // --ask 73,74 : what one conjunct of these codes (first typed first) types, on every key.
    int ask = Array.IndexOf(args, "--ask");
    if (ask >= 0)
    {
        var run = args[ask + 1].Split(',').Select(h => Convert.ToInt32(h, 16)).ToArray();
        foreach (var (key, answer) in composer.AskEverywhere(run))
            Console.WriteLine($"  key '{key}': {Show(answer)}");
        continue;
    }

    if (table)
    {
        var codes = composer.Codes();
        string path = Path.Combine(tables, script + ".json");
        var doc = new ShreeCodeTable { Script = script };
        foreach (var (code, text) in codes) doc.Codes[code.ToString("X2")] = text;

        // How the composer shows a lone vowel sign depends on Shree-Lipi's own
        // settings at the time, which it reads from the running SLNXT: on one day
        // the Tamil e sign comes back as ே, on another as the bare dot it sits on,
        // even beside a consonant. A sign read once is never traded for a carrier:
        // such codes keep the text, and the joins, the table already has.
        var before = File.Exists(path) ? JsonSerializer.Deserialize<ShreeCodeTable>(File.ReadAllText(path)) : null;
        static bool Script(string? t) => t is not null && t.Any(ch => ch is >= 'ऀ' and <= '෿');
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (before is not null)
            foreach (var (hex, old) in before.Codes)
                if (Script(old) && !Script(doc.Codes.GetValueOrDefault(hex)))
                {
                    doc.Codes[hex] = old;
                    codes[Convert.ToInt32(hex, 16)] = old;
                    kept.Add(hex);
                }
        if (kept.Count > 0) Console.WriteLine($"  {script}: kept {kept.Count} sign(s) the composer shows only as a carrier today: {string.Join(" ", kept)}");

        // Two codes side by side in a conjunct are not always the two texts side by
        // side: अ ा is आ, ी ર્ is ર્ી. Ask about every pair and keep the ones where
        // the answer is not simply the two run together.
        foreach (var (key, text) in composer.Pairs(codes, doc))
            if (!key.Split(' ').Any(kept.Contains)) doc.Pairs[key] = text;
        if (before is not null)
            foreach (var (key, text) in before.Pairs)
            {
                var parts = key.Split(' ');
                if (!parts.Any(kept.Contains)) continue;
                // the same rule the fresh pairs pass: a join that just drops a piece is no join
                if (parts.Length == 2 && (text == doc.Codes.GetValueOrDefault(parts[0]) || text == doc.Codes.GetValueOrDefault(parts[1]))) continue;
                doc.Pairs[key] = text;
            }
        File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }), new UTF8Encoding(false));
        Console.WriteLine($"{script}: {codes.Count} codes, {doc.Pairs.Count} pairs that join -> {path}");
    }

    if (verify)
    {
        var codeTable = JsonSerializer.Deserialize<ShreeCodeTable>(File.ReadAllText(Path.Combine(tables, script + ".json")))!;
        int keys = 0, same = 0;
        var wrong = new List<string>();
        var skipped = new List<string>();
        foreach (string file in Directory.EnumerateFiles(shree, "*." + script).OrderBy(f => f))
        {
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length != ShreeKeyboardFile.Length) { Console.WriteLine($"  {Path.GetFileName(file)}: {bytes.Length} bytes, skipped"); continue; }
            var mine = ShreeKeyboardFile.Decode(bytes, codeTable);
            composer.Load(file);
            string name = Path.GetFileNameWithoutExtension(file);
            // Eng and Phonetic are typed by sound: Shree's composer runs its own
            // phonetic logic on them (a consonant key answers with its half form)
            // whatever the file says, and our Eng keyboards do the same with a
            // phonetic scheme. A file imported from the generator is a key map.
            if (name.ToUpperInvariant() is "ENG" or "PHONETIC" or "ISMENG") { skipped.Add(name); continue; }

            for (char c = '!'; c <= '~'; c++)
            {
                string? theirs = composer.Ask(c, 0);
                mine.Keys.TryGetValue(c, out string? ours);
                if (theirs is null && ours is null) continue;
                keys++;
                if (theirs == ours || Carrier(theirs, ours)) same++;
                else wrong.Add($"{name} {c}: shree {Show(theirs)} / ours {Show(ours)}");
            }
            foreach (char c in ShreeKeyboardFile.EscapeKeys)
            {
                string? theirs = composer.Ask(c, 1);
                mine.Escape.TryGetValue(c, out string? ours);
                if (theirs is null && ours is null) continue;
                // A key with nothing of its own on the layer answers with its
                // printed character; the file has 0xFF there and so do we.
                if (ours is null && theirs == composer.Ask(c, 0)) continue;
                keys++;
                if (theirs == ours || Carrier(theirs, ours)) same++;
                else wrong.Add($"{name} Ctrl+Space {c}: shree {Show(theirs)} / ours {Show(ours)}");
            }
        }
        Console.WriteLine($"{script}: {same}/{keys} keys the same ({(keys == 0 ? 100 : 100.0 * same / keys):F2}%)" +
                          (skipped.Count > 0 ? $"; typed by sound, not compared: {string.Join(", ", skipped)}" : ""));
        foreach (string w in wrong.Take(args.Contains("--all") ? int.MaxValue : 25)) Console.WriteLine("   " + w);
        worst = Math.Max(worst, keys - same);
    }
}
return worst == 0 ? 0 : 2;

// Asked about a lone vowel sign, the composer often shows only what it would sit
// on - a dot, a comma, or nothing - and not the sign: its answer for the same key
// changes from one run to the next. The file's code is the sign, and a chart
// prints the sign, so that is not a difference in what the keyboard types.
static bool Carrier(string? theirs, string? ours) =>
    ours is { Length: 1 } && char.GetUnicodeCategory(ours[0]) is System.Globalization.UnicodeCategory.NonSpacingMark
                                                               or System.Globalization.UnicodeCategory.SpacingCombiningMark
    && theirs is null or "." or "," or " ";

static string Show(string? s) => s is null ? "(nothing)" : $"'{s}' [{string.Join(' ', s.Select(ch => ((int)ch).ToString("X4")))}]";

static string FindRoot()
{
    string? dir = AppContext.BaseDirectory;
    while (dir is not null && !File.Exists(Path.Combine(dir, "FontSoftware.sln"))) dir = Path.GetDirectoryName(dir);
    return dir ?? throw new InvalidOperationException("run from inside the repository");
}

/// <summary>One of Shree's composers, loaded on its own and asked about keys.</summary>
sealed class Composer
{
    private readonly GetMappedCode _mapped;
    private readonly LoadKbd _load;
    private readonly IntPtr[] _page = new IntPtr[9];
    private readonly string _script, _scratch;

    private Composer(IntPtr lib, string script)
    {
        _script = script;
        _mapped = Marshal.GetDelegateForFunctionPointer<GetMappedCode>(GetProcAddress(lib, "TutorGetMappedCodeW"));
        _load = Marshal.GetDelegateForFunctionPointer<LoadKbd>(GetProcAddress(lib, "LoadKbdOverlay"));
        // Every argument gets a zeroed page of its own: a null crashes the process.
        for (int i = 1; i <= 8; i++)
        {
            _page[i] = Marshal.AllocHGlobal(4096);
            for (int k = 0; k < 4096; k++) Marshal.WriteByte(_page[i], k, 0);
        }
        _scratch = Path.Combine(Path.GetTempPath(), "kbdfileprobe." + script);
    }

    public static Composer? Open(string shree, string script)
    {
        // Assamese has no composer of its own; its keyboards run on the Bengali one.
        string name = script == "ASS" ? "Ban" : script[0] + script[1..].ToLowerInvariant();
        string dll = Path.Combine(shree, "S8" + name + "U64.dll");
        if (!File.Exists(dll)) return null;
        SetDllDirectory(shree);
        Directory.SetCurrentDirectory(shree);
        // A private copy per script: two scripts sharing the Bengali composer, or
        // LoadLibrary handing back the one already loaded, would share its state.
        string copy = Path.Combine(Path.GetTempPath(), $"kbdfileprobe-{script}.dll");
        File.Copy(dll, copy, overwrite: true);
        IntPtr lib = LoadLibrary(copy);
        return lib == IntPtr.Zero ? null : new Composer(lib, script);
    }

    /// <summary>Load a keyboard file. ANSI, full path with extension - the one form it takes.</summary>
    public void Load(string path) => _load(Marshal.StringToHGlobalAnsi(path), _page[2], _page[3], _page[4]);

    /// <summary>What a key types on layer 0 (printed) or 1 (Ctrl+Space), cleaned of the tutor's carriers.</summary>
    public string? Ask(char key, int layer)
    {
        for (int k = 0; k < 64; k++) Marshal.WriteByte(_page[7], k, 0);
        int units = _mapped(new IntPtr(key), _page[2], _page[3], _page[4], _page[5], new IntPtr(layer), _page[7], _page[8]);
        if (units <= 0) return null;
        var bytes = new byte[60];
        Marshal.Copy(_page[7], bytes, 0, bytes.Length);
        string all = Encoding.Unicode.GetString(bytes);
        string rawText = all[..Math.Min(units, 30)].TrimEnd('\0');
        if (Environment.GetEnvironmentVariable("KBDPROBE_RAW") is not null)
            Console.WriteLine($"    raw units={units} [{string.Join(" ", all.TrimEnd('\0').Select(ch => ((int)ch).ToString("X4")))}]");
        return Clean(rawText);
    }

    /// <summary>Every code, by giving each key a code of our choosing.</summary>
    public SortedDictionary<int, string> Codes()
    {
        var answers = new SortedDictionary<int, string>();
        var keys = Enumerable.Range('!', '~' - '!' + 1).Select(c => (char)c).ToArray();
        for (int start = 0; start < 256; start += keys.Length)
        {
            var f = Blank();
            for (int i = 0; i < keys.Length && start + i < 256; i++) f[keys[i]] = (byte)(start + i);
            File.WriteAllBytes(_scratch, f);
            Load(_scratch);
            for (int i = 0; i < keys.Length && start + i < 256; i++)
            {
                int code = start + i;
                if (code == 0xFF) continue;
                if (Ask(keys[i], 0) is { } text) answers[code] = text;
            }
        }

        // A sign that cannot stand alone is not always shown alone: asked by
        // itself, the Gujarati i sign has come back as the dot it sits on, and on
        // other days as the sign. Next to a consonant it is always itself - the
        // i sign then ક is કિ (Shree spells in the order things are drawn, and
        // that sign is drawn first) - so every code that typed nothing of the
        // script is asked again, on both sides of a consonant, and what is left
        // once the consonant is taken away is its text.
        char ka = (char)(_script switch
        {
            "DEV" or "SAN" => 0x915, "GUJ" => 0xA95, "PUN" => 0xA15, "BAN" or "ASS" => 0x995,
            "ORI" => 0xB15, "TAM" => 0xB95, "TEL" => 0xC15, "KAN" => 0xC95, "MAL" => 0xD15, _ => 0,
        });
        int kaCode = answers.FirstOrDefault(kv => kv.Value == ka.ToString()).Key;
        if (kaCode != 0)
        {
            var unsure = Enumerable.Range(0x20, 0xFF - 0x20)
                                   .Where(c => c is not (>= 0x90 and <= 0x9F) and not (>= 0xB0 and <= 0xCF))
                                   .Where(c => !answers.TryGetValue(c, out var t) || t.All(ch => ch <= '~'))
                                   .SelectMany(c => new[] { new[] { kaCode, c }, new[] { c, kaCode } })
                                   .ToList();
            foreach (var (run, answer) in AskConjuncts(unsure))
            {
                int code = run[0] == kaCode ? run[1] : run[0];
                if (answer.Length < 2 || answer[0] != ka) continue;
                string rest = answer[1..];
                if (rest.Any(ch => ch is >= 'ऀ' and <= '෿')) answers[code] = rest;
            }
        }
        return answers;
    }

    /// <summary>
    /// Where two pieces of a conjunct type something other than their two texts
    /// run together. Pieces are the script's own codes - punctuation joins
    /// nothing, and a code that types nothing alone counts only when it changes
    /// its neighbour (the Kannada length mark turns ೆ into ೇ). Then, for each
    /// letter that comes out of a pair and has no code of its own (आ, ो), the
    /// pieces it joins in turn: that is how अ ा े reaches ओ.
    /// </summary>
    public Dictionary<string, string> Pairs(IReadOnlyDictionary<int, string> texts, ShreeCodeTable table)
    {
        bool Script(int c) => !texts.TryGetValue(c, out var t) || t.Any(ch => ch > '~');
        var codes = Enumerable.Range(0x20, 0xFF - 0x20)
                              .Where(c => c is not (>= 0x90 and <= 0x9F) and not (>= 0xB0 and <= 0xCF))
                              .Where(Script)
                              .ToArray();
        string? T(int c) => texts.TryGetValue(c, out var t) ? t : null;

        // Does this answer say anything the pieces run together do not? An answer
        // that is just one of two pieces that both type something is not a join -
        // it is the composer showing one piece alone, and taking it for a rule
        // would make every conjunct with that piece lose the other.
        bool Joins(string answer, string? left, string? right)
        {
            if (left is null && right is null) return false;
            if (left is null) return answer != right;
            if (right is null) return answer != left;
            if (answer == left || answer == right) return false;
            return answer != left + right;
        }

        var joined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var twos = new List<int[]>();
        foreach (int a in codes) foreach (int b in codes) twos.Add(new[] { a, b });
        foreach (var (typed, answer) in AskConjuncts(twos))
            if (Joins(answer, T(typed[0]), T(typed[1])))
                joined[$"{typed[0]:X2} {typed[1]:X2}"] = answer;

        // What Shree builds out of two pieces and has no code for, and what those
        // join in turn. A letter (आ, ो) can meet a piece on either side. A half
        // letter - a consonant and the link, ଓ୍ - is only ever followed, and only
        // by a consonant: that is how ଓ୍ବ becomes the Odia wa, ୱ.
        var built = new Dictionary<string, List<int[]>>(StringComparer.Ordinal);
        foreach (var (key, text) in joined)
        {
            if (table.IdOf(text) is not { } id || !id.StartsWith('U')) continue;
            bool half = text.Length == 2 && IsVirama(text[1]);
            if (text.Length != 1 && !half) continue;
            Built(text, key.Split(' ').Select(h => Convert.ToInt32(h, 16)).ToArray());
        }
        var consonants = codes.Where(c => T(c) is { Length: 1 } t && char.IsLetter(t[0])).ToArray();
        // A letter and the link run together plainly, so they are not in the joins
        // above - but the half letter they make can still join what follows.
        foreach (int link in codes.Where(c => T(c) is { Length: 1 } t && IsVirama(t[0])))
            foreach (int letter in consonants)
                if (table.IdOf(T(letter)! + T(link)!) is { } hid && hid.StartsWith('U'))
                    Built(T(letter)! + T(link)!, new[] { letter, link });
        var threes = new List<int[]>();
        var about = new Dictionary<int[], (bool After, string Built)>(ReferenceEqualityComparer.Instance);
        foreach (var (text, all) in built)
        foreach (var parts in all)
        {
            bool half = text.Length == 2;
            foreach (int c in half ? consonants : codes)
            {
                var after = new[] { parts[0], parts[1], c };
                threes.Add(after); about[after] = (true, text);
                if (half) continue;
                var before = new[] { c, parts[0], parts[1] };
                threes.Add(before); about[before] = (false, text);
            }
        }
        // A built piece is named by its text, and two different pairs of codes can
        // make the same text - Telugu has two codes that both show as the link,
        // and they do not join the same way. Where the pairs behind one name
        // disagree about what follows, the name joins nothing.
        var heard = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (typed, answer) in AskConjuncts(threes))
        {
            var (isAfter, text) = about[typed];
            string id = ShreeCodeTable.TextId(text);
            string key = isAfter ? $"{id} {typed[2]:X2}" : $"{typed[0]:X2} {id}";
            string? rule = (isAfter ? Joins(answer, text, T(typed[2])) : Joins(answer, T(typed[0]), text)) ? answer : null;
            if (!heard.TryGetValue(key, out var earlier)) heard[key] = rule;
            else if (earlier != rule) heard[key] = null;
        }
        foreach (var (key, rule) in heard)
            if (rule is not null) joined.TryAdd(key, rule);
        return joined;

        void Built(string text, int[] parts)
        {
            if (!built.TryGetValue(text, out var all)) built[text] = all = new List<int[]>();
            all.Add(parts);
        }

        static bool IsVirama(char c) => c is '्' or '્' or '੍' or '্' or '୍'
                                           or '்' or '్' or '್' or '്';
    }

    /// <summary>One conjunct, in slot 0, asked from every key pointed at it.</summary>
    public IEnumerable<(char, string?)> AskEverywhere(int[] run)
    {
        var f = Blank();
        for (int k = 0; k < run.Length; k++) f[167 + k] = (byte)run[run.Length - 1 - k];
        for (char key = '!'; key <= '~'; key++) f[key] = 0xB0;
        File.WriteAllBytes(_scratch, f);
        Load(_scratch);
        for (char key = '!'; key <= '~'; key++) yield return (key, Ask(key, 0));
    }

    /// <summary>
    /// What each run of codes types as a conjunct: one per slot, a key on each.
    /// Not slot 31 - its code, 0xCF, reaches nothing on Shree itself (DEVYANI.DEV
    /// has a conjunct there, and Shree types nothing for that key).
    /// </summary>
    private IEnumerable<(int[] Typed, string Answer)> AskConjuncts(List<int[]> runs)
    {
        int[] slots = Enumerable.Range(0, 48).Where(r => r != 31).ToArray();
        var keys = Enumerable.Range('!', slots.Length).Select(c => (char)c).ToArray();
        for (int start = 0; start < runs.Count; start += slots.Length)
        {
            var f = Blank();
            int n = Math.Min(slots.Length, runs.Count - start);
            for (int i = 0; i < n; i++)
            {
                var run = runs[start + i];
                int r = slots[i];
                for (int k = 0; k < run.Length; k++)
                    f[166 + r * 5 + 1 + k] = (byte)run[run.Length - 1 - k];   // stored last character first
                f[keys[i]] = (byte)(r < 32 ? 0xB0 + r : 0x90 + r - 32);
            }
            File.WriteAllBytes(_scratch, f);
            Load(_scratch);
            var answers = new List<(int[], string)>();
            for (int i = 0; i < n; i++)
                if (Ask(keys[i], 0) is { } answer) answers.Add((runs[start + i], answer));
            foreach (var a in answers) yield return a;
        }
    }

    private static byte[] Blank()
    {
        var f = new byte[ShreeKeyboardFile.Length];
        for (int i = 0; i < 32; i++) f[i] = (byte)i;
        for (int i = 32; i < 166; i++) f[i] = 0xFF;
        for (int r = 0; r < 48; r++)
        {
            f[166 + r * 5] = 4;
            for (int k = 1; k < 5; k++) f[166 + r * 5 + k] = 0xFF;
        }
        return f;
    }

    /// <summary>
    /// The composer answers about a key in isolation, so it shows a lone vowel sign
    /// on a carrier - a space, or a dot before the i sign - and the joining signs
    /// as the generator's own button names: "Lk" (link: the virama that makes a
    /// conjunct) and "Hf" (half: the virama that leaves a half letter).
    /// </summary>
    private string? Clean(string raw)
    {
        if (raw.Length == 0) return null;
        string virama = _script switch
        {
            "DEV" or "SAN" => "\u094D", "GUJ" => "\u0ACD", "PUN" => "\u0A4D", "BAN" or "ASS" => "\u09CD",
            "ORI" => "\u0B4D", "TAM" => "\u0BCD", "TEL" => "\u0C4D", "KAN" => "\u0CCD", "MAL" => "\u0D4D",
            _ => "",
        };
        if (raw == "Lk") return virama;
        // The Bengali composer's "nothing": it answers ৻ for a key with no character.
        if (raw == "৻" && _script is "BAN" or "ASS") return null;
        if (raw == "Hf") return virama + "\u200D";
        // In a longer answer the same names stand for the same signs: ଜ Lk ଞ is ଜ୍ଞ.
        // Dropping them with the other Latin carriers left ଜଞ.
        if (raw.Any(c => c is >= '\u0900' and <= '\u0DFF'))
            raw = raw.Replace("Lk", virama).Replace("Hf", virama + "\u200D");
        if (raw.Any(c => c is >= '\u0900' and <= '\u0DFF'))
            raw = new string(raw.Where(c => c > '~').ToArray());
        else if (raw.All(c => c < ' ')) return null;              // the control codes answer as themselves
        else if (raw.Length > 1 && raw.All(char.IsAsciiLetter)) return null;
        return raw.Length == 0 ? null : raw;
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetMappedCode(IntPtr key, IntPtr a2, IntPtr a3, IntPtr a4, IntPtr a5, IntPtr a6, IntPtr outText, IntPtr a8);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LoadKbd(IntPtr name, IntPtr a2, IntPtr a3, IntPtr a4);
}
