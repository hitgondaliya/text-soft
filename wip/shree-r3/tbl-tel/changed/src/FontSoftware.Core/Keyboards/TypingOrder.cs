using System.Text;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Keyboards;

/// <summary>
/// The order a keyboard is typed in, read off the keyboard's own runs.
///
/// A vendor chart shows how a sign goes onto a letter once, on ka. On the ITR
/// Gujarati keyboard "rf" is કિ - the i-sign struck first, the way a typewriter
/// strikes it - and "fo" is ર્ક, the reph struck after its letter. The layouts
/// carry exactly those runs, so ka was the only letter that came out right: r then
/// h typed િર where the keyboard means રિ, and m then the reph typed મર્ for ર્મ.
/// What a chart shows on ka is how the keyboard treats every consonant, so it is
/// learned here from those runs and applied to all of them as the word is typed:
///
/// - a sign struck before its letter goes after that letter's whole cluster once
///   the cluster is complete (િ, then સ્, then થ is સ્થિ);
/// - the reph struck after its letter goes in front of that letter's cluster;
/// - a key that types a vowel on its own types the vowel's sign straight after a
///   consonant, as Newroma's "e" is એ but "ke" is કે - so "re" is રે, not રએ;
/// - two signs a run puts together into one (ા then ે is ો) become that one.
///
/// A keyboard whose runs teach none of this - InScript, typed in Unicode's own
/// order - is not <see cref="Active"/> and types exactly as it always did.
/// </summary>
public sealed class TypingOrder
{
    /// <summary>Stands in, in the typed word, for the virama of a reph struck after its letter.</summary>
    public const char RephMark = (char)0xE000;

    // Each sign struck before its letter has a stand-in of its own, from here up.
    private const char FirstPreMark = (char)0xE001;

    // A sign struck before its letter that is longer than one character - Telugu's ra-vattu ్ర, which the typewriter
    // keyboards strike before its consonant (Roma "Rk" is క్ర) - stands in as its own mark followed by these, so the
    // stand-in is as long as the text.
    private const char PreFill = (char)0xE0FF;

    private const char Zwj = (char)0x200D;
    private const char Zwnj = (char)0x200C;

    private readonly List<string> _preSigns = new();
    // Keyed by the keystroke: a key ("e"), or a key on a layer an escape opens ("Øe": Ctrl+Space then e).
    private readonly Dictionary<string, (string Alone, string Sign)> _signAfter = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _joins = new(StringComparer.Ordinal);
    private string? _reph;

    /// <summary>The order of a keyboard that is typed the way Unicode stores text.</summary>
    public static readonly TypingOrder None = new();

    /// <summary>True when this keyboard is typed in an order other than Unicode's own.</summary>
    public bool Active => _preSigns.Count > 0 || _reph is not null || _signAfter.Count > 0 || _joins.Count > 0;

    /// <summary>Signs struck before their letter (the i-sign, on a typewriter keyboard).</summary>
    public IReadOnlyList<string> PreSigns => _preSigns;

    /// <summary>The reph, when this keyboard strikes it after its letter.</summary>
    public string? Reph => _reph;

    public override string ToString() =>
        $"struck before the letter: [{string.Join(" ", _preSigns)}], reph after its letter: {_reph ?? "-"}, " +
        $"a sign after a consonant: [{string.Join(" ", _signAfter.Select(r => r.Key + "=" + r.Value.Sign))}], " +
        $"joins: [{string.Join(" ", _joins.Select(j => j.Key + "=" + j.Value))}]";

    /// <summary>Read the typing order off a layout's own two- and three-key runs.</summary>
    public static TypingOrder Learn(KeyboardLayout layout)
    {
        var order = new TypingOrder();
        if (layout.Phonetic || layout.Sequences.Count == 0) return order;

        // what each key types on its own - and each key of a layer an escape opens: a chart run
        // writes Ctrl+Space then e as "Øe", one keystroke on that layer (Newroma "kØe" is కై)
        var alone = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (vk, normal, shift) in ShreeKeyboardFile.PhysicalKeys)
        {
            var m = layout.Find(vk);
            if (!string.IsNullOrEmpty(m?.Base)) alone[normal.ToString()] = m.Base!;
            if (!string.IsNullOrEmpty(m?.Shift)) alone[shift.ToString()] = m.Shift!;
        }
        foreach (var s in layout.Sequences)
            if (s.Keys.Length == 2 && IsEscape(s.Keys[0]) && !string.IsNullOrEmpty(s.To))
                alone.TryAdd(s.Keys, s.To);

        var signAfter = new Dictionary<string, (string Alone, string Sign)>(StringComparer.Ordinal);
        var clash = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in layout.Sequences)
        {
            if (string.IsNullOrEmpty(s.To) || Keystrokes(s.Keys) is not { Count: 2 } keys) continue;
            if (!alone.TryGetValue(keys[0], out var first) || !alone.TryGetValue(keys[1], out var second)) continue;
            // A run through a layer an escape opens teaches two things only: a subjoined letter struck
            // first, and a vowel whose sign it types after a letter (Newroma "kØe" is కై).
            bool escaped = keys[0].Length > 1 || keys[1].Length > 1;

            // sign, then consonant, typing consonant + sign: the sign is struck first - and so is a
            // subjoined letter typed before its consonant (Telugu's ra-vattu: Roma "Rk" is క్ర, Godrej
            // Ctrl+Space f then j too), which goes after that consonant's whole cluster in the same way
            if (IsConsonant(second) && (first.Length == 1 && IsSignChar(first[0]) && !escaped || IsSubjoined(first)) && s.To == second + first
                && !order._preSigns.Contains(first))
                order._preSigns.Add(first);

            if (!IsConsonant(first)) continue;

            // consonant, then reph, typing reph + consonant: the reph is struck after
            if (IsReph(second) && s.To == second + first && !escaped)
                order._reph = second;

            // consonant, then a vowel (or a colon), typing consonant + that vowel's sign
            if (s.To.Length == first.Length + second.Length && s.To.StartsWith(first, StringComparison.Ordinal))
            {
                string sign = s.To[first.Length..];
                bool vowel = SignOf(second) == sign;
                bool visarga = second == ":" && Offset(sign[0]) == 0x03 && !escaped;
                if (sign != second && (vowel || visarga))
                {
                    if (signAfter.TryGetValue(keys[1], out var had) && had.Sign != sign) clash.Add(keys[1]);
                    signAfter[keys[1]] = (second, sign);
                }
            }
        }
        foreach (var (key, rule) in signAfter)
            if (!clash.Contains(key)) order._signAfter[key] = rule;

        // A subjoined letter struck first ahead of a whole cluster (Typewriter "K'>" is ష్ట్ర: ్ర, then ష, then ్ట).
        foreach (var s in layout.Sequences)
        {
            if (Keystrokes(s.Keys) is not { Count: >= 3 } keys || !alone.TryGetValue(keys[0], out var first)
                || !IsSubjoined(first) || order._preSigns.Contains(first)) continue;
            var rest = new StringBuilder();
            bool all = true;
            foreach (var k in keys.Skip(1))
            {
                if (!alone.TryGetValue(k, out var text)) { all = false; break; }
                rest.Append(text);
            }
            if (all && IsCluster(rest.ToString()) && s.To == rest + first)
                order._preSigns.Add(first);
        }

        // A reph struck after its letter is a typewriter's habit, and only a keyboard
        // that strikes the i-sign first is typed like one. InScript's chart has "k$"
        // for र्क too, but its typists strike the reph first, in Unicode's order:
        // moving it there would turn धर्म, typed ध र् म, into र्धम.
        if (!order._preSigns.Any(p => p.Length == 1)) order._reph = null;

        // A three-key run that comes out one sign shorter than its keys typed in
        // order would: ka, then ા, then ે is કો.
        foreach (var s in layout.Sequences)
        {
            if (s.Keys.Length != 3 || s.To.Length < 2) continue;
            var raw = new StringBuilder();
            bool known = true;
            foreach (char k in s.Keys)
            {
                if (!alone.TryGetValue(k.ToString(), out var text)) { known = false; break; }
                raw.Append(order.Mark(text));
            }
            if (!known) continue;
            string typed = order.Compose(raw.ToString());
            if (typed.Length != s.To.Length + 1 || !IsSignChar(s.To[^1])) continue;
            if (!typed.StartsWith(s.To[..^1], StringComparison.Ordinal)) continue;
            if (IsSignChar(typed[^2]) && IsSignChar(typed[^1]))
                order._joins.TryAdd(typed[^2..], s.To[^1..]);
        }
        return order;
    }

    /// <summary>
    /// What a key's text is in the typed word: a sign struck before its letter, or
    /// a reph struck after it, stands in for itself until its cluster is known.
    /// The stand-in is as long as the text, so a run that later replaces it can
    /// count on that.
    /// </summary>
    public string Mark(string output)
    {
        int pre = _preSigns.IndexOf(output);
        if (pre >= 0) return ((char)(FirstPreMark + pre)).ToString() + new string(PreFill, output.Length - 1);
        if (_reph is not null && output == _reph) return _reph[0].ToString() + RephMark;
        // A run still growing shows what its keys typed one after the other: Typewriter K then r, before the a
        // that makes స్త్ర, is ్ర then స. A subjoined letter struck first at its start is still struck first.
        foreach (var p in _preSigns)
            if (p.Length > 1 && output.Length > p.Length && output.StartsWith(p, StringComparison.Ordinal))
                return Mark(p) + Mark(output[p.Length..]);
        return output;
    }

    /// <summary>
    /// The sign a vowel key types instead of its vowel because it follows a
    /// consonant (for the marks that sit on any letter - anusvara, visarga - any
    /// letter or sign), or null when it types what it types alone.
    /// </summary>
    public string? SignAfter(char key, string output, char? before) => SignAfter(key.ToString(), output, before);

    /// <summary>
    /// <see cref="SignAfter(char, string, char?)"/> for a keystroke written as a chart writes it: a key, or an escape
    /// and the key after it ("Øe", Ctrl+Space then e - Newroma's ఐ, ై after a letter).
    /// </summary>
    public string? SignAfter(string key, string output, char? before)
    {
        if (before is not { } b || !_signAfter.TryGetValue(key, out var rule) || output != rule.Alone) return null;
        bool onConsonant = IsConsonantChar(b) || Offset(b) == 0x3C;
        if (Offset(rule.Sign[0]) is >= 0x01 and <= 0x03)
            return onConsonant || IsSignChar(b) || IsIndicLetter(b) ? rule.Sign : null;
        return onConsonant ? rule.Sign : null;
    }

    /// <summary>Put the typed word into Unicode's order.</summary>
    public string Compose(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var pending = new List<string>();   // signs struck before a letter that has not come yet
        int clusterStart = -1;              // where the latest consonant cluster begins in sb
        bool open = false;                  // that cluster ends in a virama, so the next consonant joins it

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c > RephMark && c < FirstPreMark + _preSigns.Count)
            {
                pending.Add(_preSigns[c - FirstPreMark]);
                continue;
            }
            if (c == RephMark) continue;   // a reph whose ra was taken back
            if (c == PreFill) continue;    // the rest of the stand-in of a longer sign struck first


            if (IsConsonantChar(c))
            {
                // the ra of a reph struck after its letter goes in front of that letter's cluster
                if (i + 1 < raw.Length && raw[i + 1] == RephMark)
                {
                    string reph = c.ToString() + _reph![1..];
                    if (open) { Flush(sb, pending); open = false; }
                    if (clusterStart >= 0) sb.Insert(clusterStart, reph);
                    else sb.Append(reph);
                    i++;
                    continue;
                }
                if (!open) clusterStart = sb.Length;
                sb.Append(c);
                open = false;
                if (!ClusterGoesOn(raw, i)) Flush(sb, pending);
                continue;
            }

            if (Offset(c) == 0x3C && clusterStart >= 0)            // nukta, on its letter
            {
                sb.Append(c);
                if (!ClusterGoesOn(raw, i)) Flush(sb, pending);
                continue;
            }

            // A link key (virama, or virama + ZWJ) struck after a letter that already has its virama: one
            // virama - only the joiner the key adds is kept (ક્ then ્‍ is ક્‍, as Shree's composer writes it).
            if (Offset(c) == 0x4D && sb.Length > 0 && Offset(sb[^1]) == 0x4D)
            {
                open = clusterStart >= 0;
                if (i + 1 < raw.Length && raw[i + 1] == Zwj) { sb.Append(Zwj); i++; }
                else if (i + 1 < raw.Length && raw[i + 1] == Zwnj) { sb.Append(Zwnj); i++; open = false; }
                if (!open || !ClusterGoesOn(raw, i)) { Flush(sb, pending); open = false; }
                continue;
            }

            if (Offset(c) == 0x4D && clusterStart >= 0)            // virama: the cluster may go on
            {
                // A sign a run already put after this letter (ITR's "rf" is કિ) was
                // struck before the whole cluster, which only now turns out to go on.
                while (sb.Length - 1 > clusterStart && _preSigns.Contains(sb[^1].ToString()))
                {
                    pending.Insert(0, sb[^1].ToString());
                    sb.Length--;
                }
                sb.Append(c);
                open = true;
                if (i + 1 < raw.Length && raw[i + 1] == Zwj) { sb.Append(Zwj); i++; }
                else if (i + 1 < raw.Length && raw[i + 1] == Zwnj) { sb.Append(Zwnj); i++; open = false; }
                if (!open || !ClusterGoesOn(raw, i)) { Flush(sb, pending); open = false; }
                continue;
            }

            // a variation selector picks the shape of what is before it, and stays with it
            if (c is >= (char)0xFE00 and <= (char)0xFE0F)
            {
                sb.Append(c);
                continue;
            }

            // anything else ends the cluster; a sign leaves it standing as the reph's letter
            Flush(sb, pending);
            open = false;
            sb.Append(c);
            if (!IsSignChar(c)) clusterStart = -1;
        }
        Flush(sb, pending);

        string text = sb.ToString();
        foreach (var (pair, one) in _joins) text = text.Replace(pair, one, StringComparison.Ordinal);
        return text;
    }

    // After raw[i]: does the cluster carry on - a nukta or virama after a letter,
    // or a letter (one that is not a reph's ra) after a virama?
    private static bool ClusterGoesOn(string raw, int i)
    {
        int j = i + 1;
        if (j >= raw.Length) return false;
        char next = raw[j];
        char at = raw[i];
        if (at == Zwj || Offset(at) == 0x4D)
            return IsConsonantChar(next) && !(j + 1 < raw.Length && raw[j + 1] == RephMark);
        return Offset(next) is 0x3C or 0x4D;
    }

    private static void Flush(StringBuilder sb, List<string> pending)
    {
        foreach (var s in pending) sb.Append(s);
        pending.Clear();
    }

    // ---- the nine Indic blocks share one arrangement, so one set of offsets serves them all ----

    private static int Offset(char c) => c is >= (char)0x0900 and <= (char)0x0DFF ? (c - 0x0900) % 0x80 : -1;

    private static bool IsConsonantChar(char c) => Offset(c) is >= 0x15 and <= 0x3A or >= 0x58 and <= 0x5F;

    private static bool IsSignChar(char c) =>
        Offset(c) is >= 0x01 and <= 0x03 or >= 0x3C and <= 0x4C or >= 0x55 and <= 0x57 or 0x62 or 0x63;

    private static bool IsIndicLetter(char c) => Offset(c) is >= 0x05 and <= 0x14 or 0x60 or 0x61 || IsConsonantChar(c);

    private static bool IsConsonant(string s) => s.Length == 1 && IsConsonantChar(s[0]);

    private static bool IsReph(string s) => s.Length == 2 && Offset(s[0]) == 0x30 && Offset(s[1]) == 0x4D;

    // a subjoined letter: virama + consonant (Telugu's ra-vattu ్ర)
    private static bool IsSubjoined(string s) => s.Length == 2 && Offset(s[0]) == 0x4D && IsConsonantChar(s[1]);

    // consonants joined by viramas: ష్ట
    private static bool IsCluster(string s)
    {
        if (s.Length % 2 == 0) return false;
        for (int i = 0; i < s.Length; i++)
            if (!(i % 2 == 0 ? IsConsonantChar(s[i]) : Offset(s[i]) == 0x4D)) return false;
        return true;
    }

    private static bool IsEscape(char c) => c == EscapeLayer.Mark || c == EscapeLayer.SymbolMark;

    /// <summary>A run's keys as keystrokes: an escape and the key after it are one ("kØe" is k, Øe); null when an escape ends it.</summary>
    private static List<string>? Keystrokes(string keys)
    {
        var list = new List<string>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
        {
            if (!IsEscape(keys[i])) { list.Add(keys[i].ToString()); continue; }
            if (i + 1 >= keys.Length) return null;
            list.Add(keys.Substring(i, 2));
            i++;
        }
        return list;
    }

    // an independent vowel's offset in its block -> the offset of its sign
    private static readonly Dictionary<int, int> VowelSign = new()
    {
        [0x06] = 0x3E, [0x07] = 0x3F, [0x08] = 0x40, [0x09] = 0x41, [0x0A] = 0x42, [0x0B] = 0x43,
        [0x60] = 0x44, [0x0C] = 0x62, [0x61] = 0x63, [0x0D] = 0x45, [0x0E] = 0x46, [0x0F] = 0x47,
        [0x10] = 0x48, [0x11] = 0x49, [0x12] = 0x4A, [0x13] = 0x4B, [0x14] = 0x4C,
    };

    /// <summary>True for an independent vowel of any of the nine Indic blocks (అ ... ఔ, ౠ ౡ).</summary>
    public static bool IsIndependentVowel(char c) => Offset(c) is >= 0x05 and <= 0x14 or 0x60 or 0x61;

    /// <summary>What an independent vowel standing alone is after a letter: its sign (ఈ is ీ), nothing for అ; null for anything else.</summary>
    public static string? SignOfVowel(string text) => text.Length == 1 && Offset(text[0]) == 0x05 ? string.Empty : SignOf(text);

    /// <summary>The sign of an independent vowel (એ is ે), or null for anything else.</summary>
    private static string? SignOf(string text)
    {
        if (text.Length != 1) return null;
        char c = text[0];
        int o = Offset(c);
        return o >= 0 && VowelSign.TryGetValue(o, out int sign) ? ((char)(c - o + sign)).ToString() : null;
    }
}
