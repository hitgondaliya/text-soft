using System.Text;
using System.Text.Json.Serialization;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Keyboards;

/// <summary>
/// What each of Shree-Lipi's internal character codes is, for one script. A
/// keyboard file stores codes, not letters; this is the key to reading one.
/// Read out of Shree's own composer, code by code (tools/KbdFileProbe).
/// </summary>
public sealed class ShreeCodeTable
{
    /// <summary>The keyboard-file extension this table reads: GUJ, DEV, BAN, ...</summary>
    [JsonPropertyName("script")]
    public string Script { get; set; } = string.Empty;

    /// <summary>Code, as two hex digits, to the text it types.</summary>
    [JsonPropertyName("codes")]
    public Dictionary<string, string> Codes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Two pieces of a conjunct, first typed first, that type something other than
    /// their two texts run together: अ ा is आ, ी ર્ is ર્ી, ெ with the au length
    /// mark is ௌ. A piece is a code ("A7") or, for a letter Shree builds and has no
    /// code of its own, that letter ("U0A86", આ) - so આ with the e sign can still
    /// become ઓ. Asked of Shree's composer.
    /// </summary>
    [JsonPropertyName("pairs")]
    public Dictionary<string, string> Pairs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Text(int code) => Codes.TryGetValue(code.ToString("X2"), out var t) ? t : null;

    public string? Pair(string first, string second) =>
        Pairs.TryGetValue(first + " " + second, out var t) ? t : null;

    private Dictionary<string, string>? _idOf;

    /// <summary>
    /// What a piece of text is called in <see cref="Pairs"/>: the one code that
    /// types it, or for a single letter no code types, the letter itself. Null
    /// when it can join nothing further.
    /// </summary>
    public string? IdOf(string text)
    {
        if (_idOf is null)
        {
            _idOf = new Dictionary<string, string>(StringComparer.Ordinal);
            var twice = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (hex, t) in Codes)
                if (!_idOf.TryAdd(t, hex.ToUpperInvariant())) twice.Add(t);
            foreach (string t in twice) _idOf.Remove(t);
        }
        if (_idOf.TryGetValue(text, out string? id)) return id;
        return text.Length is 1 or 2 ? TextId(text) : null;
    }

    /// <summary>The name of a piece no code types: "U" and its characters in hex (ઓ is U0A93, ଓ୍ is U0B130B4D).</summary>
    public static string TextId(string text) => "U" + string.Concat(text.Select(c => ((int)c).ToString("X4")));
}

/// <summary>A keyboard file, read: what every key types.</summary>
public sealed class ShreeKeyboard
{
    /// <summary>Normal and Shift layers, keyed by the character a US keyboard types there ("K" is Shift+k).</summary>
    public SortedDictionary<char, string> Keys { get; } = new();

    /// <summary>The Ctrl+Space layer, keyed by the unshifted key pressed after it.</summary>
    public SortedDictionary<char, string> Escape { get; } = new();

    /// <summary>How many keys reach one of the file's own conjuncts.</summary>
    public int ConjunctKeys { get; set; }

    /// <summary>
    /// Keys whose conjunct has a piece this version cannot read. They are left
    /// empty, and the import says which they are.
    /// </summary>
    public List<char> Unreadable { get; } = new();
}

/// <summary>
/// Shree-Lipi's keyboard files: the ones it ships (INSCRIPT.GUJ, ITR.DEV, ...)
/// and the ones its Keyboard Generator saves, which the typist then copies into
/// the Shree-Lipi folder. Same format, always 406 bytes, the extension naming
/// the script.
///
///   0-127    one byte per ASCII character: the code the key with that character
///            types. Normal and Shift are both here, because "k" and "K" are two
///            characters. 0xFF = the key types nothing.
///   128-165  the Ctrl+Space layer ("Escapement" in the generator): one byte per
///            key, in keyboard order - 1 to 0, - and =, then the q, a and z rows.
///   166-405  48 conjuncts of 5 bytes: a 4, then up to four codes, padded with
///            0xFF and stored last character first. A key reaches conjunct n
///            through code 0xB0+n (n below 32) or 0x90+n-32.
///
/// Worked out by writing files of our own and asking Shree's composer what they
/// type; the generator's help agrees - three layers, at most 48 conjuncts of at
/// most four characters.
/// </summary>
public static class ShreeKeyboardFile
{
    public const int Length = 406;
    private const int EscapeStart = 0x80, ConjunctStart = 166, ConjunctSize = 5, ConjunctCount = 48;

    /// <summary>The keys of the Ctrl+Space layer, in the order the file stores them.</summary>
    public const string EscapeKeys = "1234567890-=qwertyuiopasdfghjklzxcvbnm";

    private static readonly Dictionary<string, string[]> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEV"] = new[] { "hi", "mr", "sa" },
        ["SAN"] = new[] { "sa", "hi", "mr" },
        ["GUJ"] = new[] { "gu" },
        ["PUN"] = new[] { "pa" },
        ["BAN"] = new[] { "bn", "mni" },
        ["ASS"] = new[] { "as" },
        ["ORI"] = new[] { "or" },
        ["TAM"] = new[] { "ta" },
        ["TEL"] = new[] { "te" },
        ["KAN"] = new[] { "kn" },
        ["MAL"] = new[] { "ml" },
    };

    /// <summary>The script a file is for, from its extension (".GUJ" -> "GUJ"), or null.</summary>
    public static string? ScriptOf(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return Languages.ContainsKey(ext) ? ext : null;
    }

    /// <summary>The languages that write a script, first the one a file of it is usually for.</summary>
    public static IReadOnlyList<string> LanguagesFor(string script) =>
        Languages.TryGetValue(script, out var ids) ? ids : Array.Empty<string>();

    public static IEnumerable<string> Scripts => Languages.Keys;

    /// <summary>Read a keyboard file. Throws <see cref="InvalidDataException"/> when it is not one.</summary>
    public static ShreeKeyboard Decode(byte[] file, ShreeCodeTable table)
    {
        if (file.Length != Length)
            throw new InvalidDataException(
                $"A Shree-Lipi keyboard file is {Length} bytes; this one is {file.Length}.");
        for (int r = 0; r < ConjunctCount; r++)
            if (file[ConjunctStart + r * ConjunctSize] != 4)
                throw new InvalidDataException("This is not a Shree-Lipi keyboard file: its conjunct table is not where it should be.");

        var keyboard = new ShreeKeyboard();
        for (char c = '!'; c <= '~'; c++)
            Read(file, file[c], c, table, keyboard, keyboard.Keys);
        for (int i = 0; i < EscapeKeys.Length; i++)
            Read(file, file[EscapeStart + i], EscapeKeys[i], table, keyboard, keyboard.Escape);
        return keyboard;
    }

    private static void Read(byte[] file, byte code, char key, ShreeCodeTable table,
                             ShreeKeyboard keyboard, SortedDictionary<char, string> into)
    {
        if (code == 0xFF) return;
        int slot = Slot(code);
        if (slot >= 0 && table.Text(code) is null)
        {
            var (conjunct, empty) = Conjunct(file, slot, table);
            if (empty) return;                                   // an empty slot types nothing on Shree either
            if (conjunct is null) { keyboard.Unreadable.Add(key); return; }
            keyboard.ConjunctKeys++;
            into[key] = conjunct;
            return;
        }
        // A code the table has no text for is one Shree types nothing for either:
        // the table holds every code its composer answers. The key stays empty.
        if (table.Text(code) is { Length: > 0 } text) into[key] = text;
    }

    private static int Slot(byte code) => code switch
    {
        >= 0xB0 and <= 0xCF => code - 0xB0,
        >= 0x90 and <= 0x9F => 32 + code - 0x90,
        _ => -1,
    };

    /// <summary>A conjunct's text: its codes, first character first, run together.</summary>
    private static (string? Text, bool Empty) Conjunct(byte[] file, int slot, ShreeCodeTable table)
    {
        int at = ConjunctStart + slot * ConjunctSize + 1;
        var codes = new List<byte>(4);
        for (int k = 0; k < 4 && file[at + k] != 0xFF; k++) codes.Add(file[at + k]);
        if (codes.Count == 0) return (null, true);
        codes.Reverse();                 // stored last character first
        return (Join(codes, table), false);
    }

    /// <summary>
    /// Run a conjunct's codes together the way Shree's composer does: wherever two
    /// neighbours join into something of their own (ा े into ो), they become that,
    /// and what they became can join again when it is itself a code (अ ो into ओ).
    /// Which neighbours join first matters - अ ा is आ, and आ is not a code, so
    /// joining there first strands the े - so every order is tried and the one
    /// that joins the most wins. A conjunct is at most four codes; that is at
    /// most six orders.
    /// </summary>
    public static string? Join(IReadOnlyList<byte> codes, ShreeCodeTable table)
    {
        var best = Reduce(codes.Select(c => (Id: (string?)c.ToString("X2"), Text: table.Text(c))).ToList(), table);
        // A piece this version cannot read, left unread by every join, adds
        // nothing - Shree types INSCRIPT.TEL's x (ం and such a piece) as ం, and
        // RAMING.PUN's P as ੧ੳ. But when what is left is only punctuation, it was
        // never a letter anyone built: Tamil Typewriter has such keys, which type
        // nothing on Shree, and running the rest together put "#$<" there. Those
        // are lost, and the key is reported.
        var texts = best.Where(p => p.Text is not null).Select(p => p.Text!).ToList();
        string joined = string.Concat(texts);
        if (joined.Length == 0) return null;
        if (texts.Count < best.Count && !joined.Any(c => c is >= 'ऀ' and <= '෿')) return null;
        return joined;
    }

    private static List<(string? Id, string? Text)> Reduce(List<(string? Id, string? Text)> parts, ShreeCodeTable table)
    {
        var best = parts;
        for (int i = 0; i + 1 < parts.Count; i++)
        {
            if (parts[i].Id is not { } first || parts[i + 1].Id is not { } second) continue;
            if (table.Pair(first, second) is not { } both) continue;
            var fewer = new List<(string? Id, string? Text)>(parts);
            fewer[i] = (table.IdOf(both), both);
            fewer.RemoveAt(i + 1);
            var reduced = Reduce(fewer, table);
            if (reduced.Count < best.Count) best = reduced;
        }

        // Two pieces that simply run together - a letter and the link, ଓ୍ - can
        // still join what follows as one: ଓ୍ then ବ is the Odia wa, ୱ.
        for (int i = 0; i + 2 < parts.Count; i++)
        {
            if (parts[i].Text is not { } a || parts[i + 1].Text is not { } b || parts[i + 2].Id is not { } next) continue;
            if (table.IdOf(a + b) is not { } both || table.Pair(both, next) is not { } joined) continue;
            var fewer = new List<(string? Id, string? Text)>(parts);
            fewer[i] = (table.IdOf(joined), joined);
            fewer.RemoveRange(i + 1, 2);
            var reduced = Reduce(fewer, table);
            if (reduced.Count < best.Count) best = reduced;
        }
        return best;
    }

    /// <summary>
    /// Turn a keyboard file into a keyboard: its two printed layers, its
    /// Ctrl+Space layer, and - because every keyboard of a script shares them and
    /// the file does not carry them - the Ctrl+Shift+1 symbols of
    /// <paramref name="sameScript"/>.
    /// </summary>
    public static KeyboardLayout ToLayout(ShreeKeyboard keyboard, string name, KeyboardLayout? sameScript = null)
    {
        var layout = new KeyboardLayout { Name = name };
        foreach (var (vk, normal, shift) in PhysicalKeys)
        {
            keyboard.Keys.TryGetValue(normal, out string? baseText);
            keyboard.Keys.TryGetValue(shift, out string? shiftText);
            if (baseText is null && shiftText is null) continue;
            layout.Keys.Add(new KeyMapping { VirtualKey = vk, Base = baseText, Shift = shiftText });
        }

        foreach (var (key, text) in keyboard.Escape)
        {
            layout.Escape.Add(new KeySequence { Keys = key.ToString(), To = text });
            layout.Sequences.Add(new KeySequence { Keys = EscapeLayer.Mark + key.ToString(), To = text });
        }

        if (sameScript is not null)
        {
            foreach (var s in sameScript.Symbols) layout.Symbols.Add(new KeySequence { Keys = s.Keys, To = s.To });
            foreach (var s in sameScript.Sequences.Where(s => s.Keys.StartsWith(EscapeLayer.SymbolMark)))
                layout.Sequences.Add(new KeySequence { Keys = s.Keys, To = s.To });
        }

        layout.Reindex();
        return layout;
    }

    /// <summary>Every key a keyboard file can put a character on: virtual key, unshifted and shifted US character.</summary>
    public static IEnumerable<(int Vk, char Normal, char Shift)> PhysicalKeys
    {
        get
        {
            for (char c = 'a'; c <= 'z'; c++) yield return (char.ToUpperInvariant(c), c, char.ToUpperInvariant(c));
            string shifted = ")!@#$%^&*(";
            for (int d = 0; d <= 9; d++) yield return ('0' + d, (char)('0' + d), shifted[d]);
            yield return (186, ';', ':');
            yield return (187, '=', '+');
            yield return (188, ',', '<');
            yield return (189, '-', '_');
            yield return (190, '.', '>');
            yield return (191, '/', '?');
            yield return (192, '`', '~');
            yield return (219, '[', '{');
            yield return (220, '\\', '|');
            yield return (221, ']', '}');
            yield return (222, '\'', '"');
        }
    }
}
