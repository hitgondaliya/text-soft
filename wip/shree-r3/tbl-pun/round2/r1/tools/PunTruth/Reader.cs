using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree-Pun -> Unicode pairs of one family: every preferred composer spelling must read back to its
/// Unicode, or to a Unicode string the table writes with the very same bytes.
///
/// The pre rules undo what the composer does to the order around ਠ and ਨ: the marks it writes before the spacer BE
/// go back after the letter's ੁ ੂ and subjoined form, and BE (and the blank BF) go. The pairs answer in visual
/// order (the i-sign in front of its cluster, as the bytes are), and the post rules move it to its Unicode place,
/// join ੲ ਅ ੳ with a sign into the vowel the Unicode composer writes for them (ਇ ਈ ਏ ਆ ਐ ਔ ਉ ਊ), ੱ + ਂ into ਁ
/// (as the Unicode composer writes it), and keep a visible halant before ਰ ਹ ਵ ਯ apart with a ZWNJ (ਕ੍‌ਰ, 48 B5 63
/// - plain ਕ੍ਰ is the subjoined form 6E).
/// </summary>
public sealed class Reader
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules;
    public readonly List<RewriteRule> PostRules;
    readonly FontConverter _stager, _visual;

    /// <summary>Marks the virama of the visible halant B5 (a noncharacter): before ਰ ਹ ਵ ਯ it becomes a ZWNJ.</summary>
    public const string HalantMark = "\uFDDD";

    /// <summary>Holds an i-sign in place while the i-sign rule moves the others (a noncharacter).</summary>
    public const string HeldISign = "\uFDDE";

    /// <summary>The marks the composer writes before the spacer of ਠ ਨ: ਂ ੰ ੱ ੇ ੈ ੋ ੌ and the sign+bindi glyphs.</summary>
    public const string TopCodes = "A3 A5 A7 79 7A B1 7C 7D 7E A1 A2";

    public Reader(Fam f)
    {
        F = f;
        PreRules = ReversePre();
        PostRules = ReversePost();
        _stager = new FontConverter(Table("s", "s", "a", "b", FontType.Legacy, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));
        _visual = new FontConverter(Table("v", "v", "a", "b", FontType.Unicode, FontType.Unicode, VisualRules(), Array.Empty<(string, string)>()));
    }

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>One legacy code as a regex literal.</summary>
    public static string Lit(string hex)
    {
        char c = FromHex(hex)[0];
        return "\\^$.|?*+()[]{}-/>".Contains(c) ? "\\" + c : c.ToString();
    }

    public static string Class(string hexList) => "[" + string.Concat(hexList.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Lit)) + "]";

    /// <summary>A cluster in Unicode: letters joined by virama (a joiner may follow it), each with an optional nukta.</summary>
    const string Cluster = "(?:[" + Rules.Cons + "]਼?੍[\u200C\u200D]?)*[" + Rules.Cons + "]਼?";

    /// <summary>Unicode in the order the pairs answer: the i-sign in front of its cluster, the vowels split as the composer types them.</summary>
    static List<RewriteRule> VisualRules() => new()
    {
        R("ਇ", "ੲਿ", "split"), R("ਈ", "ੲੀ", "split"), R("ਏ", "ੲੇ", "split"),
        R("ਆ", "ਅਾ", "split"), R("ਐ", "ਅੈ", "split"), R("ਔ", "ਅੌ", "split"),
        R("ਁ", "ੱਂ", "split"),
        R($"({Cluster}|[{Rules.Bases}])ਿ", "ਿ$1", "pre-base"),
    };

    public static List<RewriteRule> ReversePre() => new()
    {
        R($"({Class(TopCodes)}+){Lit("BE")}({Class("6F AA AB")}?{Class("74 76 AE AF")}?)", "$2$1",
          "The marks the composer writes before ਠ/ਨ's spacer BE (ੇ ੈ ੋ ੌ ਂ ੰ ੱ and the sign+bindi glyphs) go back after the letter's subjoined form and ੁ ੂ, where Unicode has them."),
        R($"{Lit("BE")}|{Lit("BF")}", "", "The spacer BE after ਠ ਨ, and the blank BF, draw nothing: they are not text."),
    };

    public static List<RewriteRule> ReversePost() => new()
    {
        R($"{HalantMark}(?=[ਰਹਵਯ])", "\u200C", "The visible halant B5 before ਰ ਹ ਵ ਯ is ੍ + ZWNJ (ਕ੍‌ਰ = 48 B5 63): plain ਕ੍ਰ is the subjoined form (6E), which the bytes do not show."),
        R(HalantMark, "", "Elsewhere the halant B5 is the virama alone (ਕ੍ਤ = 48 B5 57, ਕ੍ at the end of a word = 48 B5)."),
        R("ਿ((?:੍[ਰਹਵਯ])+)", "$1ਿ", "An i-sign the composer wrote between a letter and its subjoined form (48 71 62, ਕ੍ਯ typed with the i-sign after the letter) belongs to the whole cluster: after the subjoined form."),
        R($"(?<=[{Rules.Cons}{Rules.Bases}]਼?)ਿ(?=(?:{Cluster})[ਾੀੁੂੇੈੋੌ])", HeldISign,
          "An i-sign after a letter and before a cluster that has a vowel sign of its own cannot be that cluster's: it is the letter's, " +
          "written after it (the earlier shree-pun7 table wrote ੜਿ so: 6D 71). It stays where it is (U+FDDE holds it)."),
        R($"ਿ({Cluster}|[{Rules.Bases}])", "$1ਿ", "The i-sign goes after its cluster (visual order -> Unicode order); ਅ ੲ ੳ ਓ take it too."),
        R(HeldISign, "ਿ", "The held i-sign."),
        R("([ੇੈੋੌ])\\1", "$1", "A vowel sign twice in a row draws one sign: the composer's own broken answer for ਠ/ਨ + subjoined + a top sign + bindi (ਠ੍ਰੇਂ written 53 79 BE 7A, the subjoined form lost) reads as the one sign it shows."),
        R("ੱਂ", "ਁ", "ੱ + ਂ is ਁ, as the Unicode composer writes it (ਕਁ = 48 A7 A3)."),
        R("ੲਿ", "ਇ", "ੲ + ਿ is ਇ (71 42): the vowels the Unicode composer writes for a carrier and a sign."),
        R("ੲੀ", "ਈ", "ੲ + ੀ is ਈ (42 72)."),
        R("ੲੇ", "ਏ", "ੲ + ੇ is ਏ (42 79)."),
        R("ਅਾ", "ਆ", "ਅ + ਾ is ਆ (41 70)."),
        R("ਅੈ", "ਐ", "ਅ + ੈ is ਐ (41 B1)."),
        R("ਅੌ", "ਔ", "ਅ + ੌ is ਔ (41 A1)."),
        R("ੳਂ([ੁੂ])", "ੳ$1ਂ", "ੳ with the bindi (44) and a u-sign after it (RAMING's key '>': 44 74) is the vowel with the bindi: the sign first."),
        R("ੳੁ", "ਉ", "ੳ + ੁ is ਉ."),
        R("ੳੂ", "ਊ", "ੳ + ੂ is ਊ."),
        R(Rules.Keep, "", "U+FDD1 is not text."),
    };

    public string Staged(string legacy) => _stager.ConvertText(legacy);
    public string Visual(string unicode) => _visual.ConvertText(unicode);

    static bool Plain(string u) => !u.Any(c => IsJoiner(c) || Learner.NoGlyph.Contains(c.ToString()) || c == '₹');

    /// <summary>The Unicode a spelling reads back as, when several strings share it: the plainest, then the shortest.</summary>
    public static Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        truth.Where(t => t.Pref.Length > 0 && Reported(t.Pref).Count == 0).GroupBy(t => t.Pref)
             .ToDictionary(g => g.Key, g => g.OrderBy(t => Plain(t.U) ? 0 : 1)
                                              .ThenBy(t => Malformed(t.U).Count > 0 ? 1 : 0)
                                              .ThenBy(t => t.U.Length)
                                              .ThenBy(t => t.U, StringComparer.Ordinal).First().U, StringComparer.Ordinal);

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table(F.Id + "-to-unicode", F.Name + " -> Unicode", F.Id, "raavi", FontType.Legacy, FontType.Unicode, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> in this family: equal, or text the
    /// table writes with the very same codes as <paramref name="hex"/> (so a character the family has no glyph for may be
    /// missing - nothing in the bytes says it was there - and the link spelling of ਕ੍ਤ, 48 57, is the text ਕਤ).
    /// </summary>
    public static bool Same(string got, string want, string hex, FontConverter forward)
    {
        if (got == want) return true;
        var r = forward.Convert(got);
        return Hex(r.Text) == hex && ReportsAsSpelled(r, hex);
    }

    public Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0))
            if (canon[t.Pref] == t.U) pairs[Staged(FromHex(t.Pref))] = t.U;
        // The halant B5 alone: the virama, marked so a ZWNJ keeps it from a following ਰ ਹ ਵ ਯ.
        pairs[FromHex("B5")] = "੍" + HalantMark;
        // (not \u0965: 26 26 reads as the two dandas the composer writes for \u0964\u0964)
        foreach (var (u, hex) in Learner.Extra.Where(e => !e.U.Contains('\u200C') && e.U != "\u0965")) pairs.TryAdd(Staged(FromHex(F.Apply(hex))), Visual(u));
        // ਠ ਨ without their spacer, the u/uu after it, the far addak, the bindi after ਉ ਊ.
        pairs.TryAdd(FromHex("53"), "ਠ");
        pairs.TryAdd(FromHex("B2"), "ਨ");
        pairs.TryAdd(FromHex("AE"), "ੁ");
        pairs.TryAdd(FromHex("AF"), "ੂ");
        pairs.TryAdd(FromHex("A8"), "ੱ");
        pairs.TryAdd(FromHex("A4"), "ਂ");
        foreach (var (hex, u) in F.FontCodes) pairs.TryAdd(Staged(FromHex(hex)), u);
        int atoms = pairs.Count, added = 0, unkeyable = 0;

        foreach (var level in canon.GroupBy(kv => Staged(FromHex(kv.Key)).Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs));
            foreach (var (hex, u) in level.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                string got = conv.ConvertText(FromHex(hex));
                if (Same(got, u, hex, forward)) continue;
                string key = Staged(FromHex(hex));
                if (key.Length == 0) { unkeyable++; continue; }
                log?.Add($"{u}\t{hex}\t{Hex(key)}\tread {got}");
                pairs[key] = Visual(u);
                added++;
            }
        }
        stats = $"{F.Id} reverse: atoms {atoms}, learned {added}, unkeyable {unkeyable}, total {pairs.Count}";
        return pairs;
    }

    public static string Check(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0;
        foreach (var t in truth)
        {
            if (t.Pref.Length == 0 || Reported(t.Pref).Count > 0) continue;
            string got = reader.ConvertText(FromHex(t.Pref));
            if (got == t.U) exact++;
            else if (Same(got, t.U, t.Pref, forward)) same++;
            else fails.Add($"{t.Pref}\twant {t.U} [{Cps(t.U)}]\tgot {got} [{Cps(got)}]");
        }
        int n = exact + same + fails.Count;
        return $"read back {n}: exact {exact}, same text {same}, wrong {fails.Count} ({100.0 * (exact + same) / n:F2}%)";
    }

    /// <summary>Joined syllables read back.</summary>
    public static string CheckWords(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, int count, int seed, out List<string> fails)
    {
        var pool = truth.Where(t => t.Pref.Length > 0 && Reported(t.Pref).Count == 0 && !t.U.Any(IsJoiner)).ToList();
        fails = new List<string>();
        int ok = 0;
        foreach (var (u, want) in Checks.MakeWords(pool, count, seed))
        {
            string got = reader.ConvertText(FromHex(want));
            if (Same(got, u, want, forward)) ok++;
            else fails.Add($"{want}\twant {u}\tgot {got} [{Cps(got)}]");
        }
        return $"words read back {count} (seed {seed}): right {ok}, wrong {fails.Count} ({100.0 * ok / count:F2}%)";
    }

    /// <summary>The other spellings the composer accepts, read back: as the text, or as text the table writes with those very bytes.</summary>
    public static string CheckAccepted(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0, n = 0;
        foreach (var t in truth)
            foreach (var a in t.Acc.Where(a => a != t.Pref && a.Length > 0 && Reported(a).Count == 0))
            {
                n++;
                string got = reader.ConvertText(FromHex(a));
                if (got == t.U) exact++;
                else if (Same(got, t.U, a, forward) && Malformed(got).Count == 0) same++;
                else fails.Add($"{a}\twant {t.U}\tgot {got} [{Cps(got)}]");
            }
        return $"other accepted spellings read back {n}: exact {exact}, as text the table writes with those very bytes {same}, wrong {fails.Count}";
    }

    /// <summary>Every reverse output well formed where the text read was: each truth spelling and joined syllables.</summary>
    public static string CheckWellFormed(FontConverter reader, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0;
        var pool = truth.Where(t => t.Pref.Length > 0 && Reported(t.Pref).Count == 0).ToList();
        var items = pool.Select(t => (t.U, t.Pref)).Concat(Checks.MakeWords(pool, 20000, 7)).Concat(Checks.MakeWords(pool, 20000, 11));
        foreach (var (u, hex) in items)
        {
            if (Malformed(u).Count > 0) continue;
            n++;
            string got = reader.ConvertText(FromHex(hex));
            var bad = Malformed(got);
            if (bad.Count > 0) fails.Add($"{hex}\tread {got} [{Cps(got)}] for {u}: {string.Join("; ", bad)}");
        }
        return $"well-formed read back {n}: malformed {fails.Count}";
    }

    /// <summary>
    /// Every spelling of Shree's composer in the capture (Capture.Spellings: every kind - the truth's preferred,
    /// accepted and alternative spellings, the vendor keys, the alt and quirk rows), read back: never Latin, a control
    /// code or a noncharacter; and the text typed, or text the table writes with those very bytes, wherever the text
    /// typed was well formed. Returns the counts by outcome.
    /// </summary>
    public static string CheckCapture(FontConverter reader, FontConverter forward, IEnumerable<(string U, string Hex)> spellings, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0, exact = 0, same = 0, other = 0, latin = 0, malformed = 0;
        foreach (var (u, hex) in spellings)
        {
            n++;
            string got = reader.ConvertText(FromHex(hex));
            var bad = Malformed(got);
            if (bad.Any(b => b.StartsWith("Latin") || b.StartsWith("C1") || b.StartsWith("control") || b.StartsWith("noncharacter")))
            {
                latin++;
                fails.Add($"LATIN\t{hex}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join("; ", bad)}");
                continue;
            }
            if (got == u) { exact++; continue; }
            if (Same(got, u, hex, forward)) { same++; continue; }
            bool typedOk = Malformed(u).Count == 0;
            if (typedOk && bad.Count > 0)
            {
                malformed++;
                fails.Add($"MALFORMED\t{hex}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join("; ", bad)}");
                continue;
            }
            other++;
            fails.Add($"OTHER\t{hex}\ttyped {u}\tread {got} [{Cps(got)}]\twritten back {Hex(forward.ConvertText(got))}");
        }
        return $"composer spellings of the whole capture read back {n}: exact {exact}, as text the table writes with those very bytes {same}, " +
               $"other text (the bytes are another text's: a link virama has no glyph, a sign struck out of order) {other}, Latin/control/noncharacter {latin}, malformed {malformed}";
    }
}
