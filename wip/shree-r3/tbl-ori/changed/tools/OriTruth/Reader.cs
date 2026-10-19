using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree-Ori -> Unicode pairs: every preferred composer spelling must read back to its Unicode, or to a
/// Unicode string the table writes with the very same bytes.
///
/// The pre rules drop what draws nothing: the spacer FF after a letter and the lowering spacer 26 before a sign under a
/// hanging subjoined form. The pairs answer in visual order with marks (the e-sign 7B as U+FDD2 in front of its cluster,
/// the reph F6 as U+FDD0 after its syllable, the visible virama FA as ୍ + U+FDD3), and the post rules move them to their
/// Unicode places: the e-sign after its cluster (joined with ୖ ା ୗ into ୈ ୋ ୌ), ର୍ in front of the reph's cluster, and a
/// ZWNJ after a visible virama before a letter that would otherwise join it (କ୍‌କ = 4C FF FA 4C FF; plain କ୍କ is 4D).
/// </summary>
public sealed class Reader
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules;
    public readonly List<RewriteRule> PostRules;
    readonly FontConverter _stager, _visual;

    /// <summary>Marks the virama of the visible virama FA (a noncharacter): before a letter with a subjoined form it becomes a ZWNJ.</summary>
    public const string HalantMark = "\uFDD3";

    /// <summary>The letters with a subjoined form or a phala in these fonts (ori7-patterns.txt 5, 7): after a visible virama they take a ZWNJ.</summary>
    public const string Joins = "କଖଗଘଚଛଜଝଟଠଡଢଣଥଦଧନପଫବଭମଯରଲଳସୟୱ";

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

    /// <summary>A cluster in Unicode: letters joined by virama, each with an optional nukta.</summary>
    const string Cluster = Rules.Cluster;

    /// <summary>Unicode in the order the pairs answer: the reph after its syllable, the e-sign in front of its cluster.</summary>
    static List<RewriteRule> VisualRules()
    {
        var list = new List<RewriteRule> { Rules.RephRule };
        list.AddRange(Rules.EMoves());
        return list;
    }

    /// <summary>
    /// ନ as Shree's composer writes it, 9C FF (the raw byte of code 65 with its spacer), and as the earlier TextSoft table
    /// wrote it (9C FF, ନ୍ 9C FF FA). Every face draws ଙ୍କ୍ତ at 9C, but the composer writes 9C nowhere else (1,810 of
    /// 1,810 times in the capture it is followed by FF), and the tables write ଙ୍କ୍ତ as a bare 9C, never before FF
    /// (Build checks it). So 9C FF reads as ନ (P5), and a bare 9C as ଙ୍କ୍ତ.
    /// </summary>
    public static readonly RewriteRule ComposerNa = R(Lit("9C") + Lit("FF"), FromHex("5B FF"),
        "9C FF is ନ as Shree's composer writes it (and the earlier table, ନ୍ = 9C FF FA): the tables write ଙ୍କ୍ତ as a bare 9C, never before the spacer FF. Read as 5B FF, the ନ the fonts draw.");

    public static List<RewriteRule> ReversePre() => new()
    {
        Rules.NoMarksIn,
        ComposerNa,
        R($"{Lit("FF")}|{Lit("26")}", "", "The spacer FF after a letter and the lowering spacer 26 under a hanging subjoined form draw nothing: they are not text."),
    };

    /// <summary>The vowel signs a reph is read after (visual order; ୍ for a reph struck before a visible virama).</summary>
    const string Signs = "\u0B3E-\u0B44\u0B47\u0B48\u0B4B\u0B4C\u0B56\u0B57\u0B62\u0B63\u0B4D";

    /// <summary>The independent vowels (a reph or an e-sign may stand on one: ର୍ଅ = 41 F6, ଅେ = 7B 41).</summary>
    const string Vowels = "\u0B05-\u0B0C\u0B0F\u0B10\u0B13\u0B14\u0B60\u0B61";

    public static List<RewriteRule> ReversePost() => new()
    {
        R($"{HalantMark}(?={Rules.ESign}|(?!କ୍ଷ)[{Joins}])", "\u200C", "The visible virama FA before a letter with a subjoined form, or before the next syllable's e-sign, is ୍ + ZWNJ (କ୍\u200Cକ = 4C FF FA 4C FF): plain କ୍କ is the ligature 4D, which the bytes do not show. Not before the ligature କ୍ଷ E4: C + virama + କ୍ଷ is C FA E4 (ଖ୍କ୍ଷ = 51 FA E4)."),
        R(HalantMark, "", "Elsewhere FA is the virama alone (କ୍ତ = 4C FF FA 86 FF, କ୍ at the end of a word = 4C FF FA)."),
        R("([\u0B3E-\u0B44\u0B56\u0B57\u0B62\u0B63]+)(\u0B4D[\u0B30\u0B5F\u0B2C])", "$2$1",
          "A sign the composer wrote before a phala (struck before the ra-, ya- or ba-phala code: କୃ then the ra-phala = 4C FF F5 26 F7) belongs after it: କ୍ରୃ."),
        R($"{Rules.ESign}({Rules.Reph}?)((?:[{Rules.Cons}]\u0B3C?୍\u200D?)*[{Rules.Cons}]\u0B3C?|[{Vowels}])", "$1$2େ", "The e-sign 7B goes after its cluster (visual order -> Unicode order; a ZWJ half form is part of it: ତ୍\u200Dନେ = 7B 86 FF A7), or after the vowel it was struck on (ଅେ = 7B 41); a reph written between them (7B F6 C) stays in front."),
        R($"\u0B47({Rules.Reph}?)\u0B56", "\u0B48$1", "େ + ୖ is ୈ (7B C F0; a reph the composer wrote between them, 7B C F6 F0, goes after both)."),
        R($"\u0B47({Rules.Reph}?)\u0B3E", "\u0B4B$1", "େ + ା is ୋ (7B C E6)."),
        R($"\u0B47({Rules.Reph}?)\u0B57", "\u0B4C$1", "େ + ୗ is ୌ (7B C F2)."),
        R($"([{Rules.Cons}]\u0B3C?(?:\u0B4D[{Rules.Cons}]\u0B3C?)*|[{Vowels}])([{Signs}]*)([\u0B01-\u0B03]*){Rules.Reph}", "\u0B30\u0B4D$1$2$3",
          "The reph F6 after a syllable is ର୍ in front of its cluster (ର୍କା = 4C FF E6 F6; written after ଂ ଁ ଃ too, as the composer accepts: 4C FF F3 F6), or of the vowel it stands on (ର୍ଅ = 41 F6)."),
        R(Rules.Reph, "\u0B30\u0B4D", "A reph with no syllable before it (the lone reph key, or one struck before its letter) is ର୍."),
        R(Rules.ESign, "\u0B47", "An e-sign with no letter after it is େ."),
        R("\u0B05\u0B3E", "\u0B06", "ଅ + ା is ଆ (41 E6), as the Unicode composer writes it."),
        R(Rules.Keep, "", "U+FDD1 is not text."),
    };

    public string Staged(string legacy) => _stager.ConvertText(legacy);
    public string Visual(string unicode) => _visual.ConvertText(unicode);

    static bool Plain(string u) => !u.Any(c => IsJoiner(c) || Policy.NoGlyph.Contains(c.ToString()));

    /// <summary>The Unicode a spelling reads back as, when several strings share it: the plainest, then the shortest.</summary>
    public static Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        truth.Where(t => t.Pref.Length > 0 && Reported(t.Pref).Count == 0).GroupBy(t => t.Pref)
             .ToDictionary(g => g.Key, g => g.OrderBy(t => Plain(t.U) ? 0 : 1)
                                              .ThenBy(t => Malformed(t.U).Count > 0 ? 1 : 0)
                                              .ThenBy(t => t.U.Length)
                                              .ThenBy(t => t.U, StringComparer.Ordinal).First().U, StringComparer.Ordinal);

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table(F.Id + "-to-unicode", F.Name + " -> Unicode", F.Id, "kalinga", FontType.Legacy, FontType.Unicode, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> in these fonts: equal, or text the
    /// table writes with the very same codes as <paramref name="hex"/> (the joiner of କ୍‌ତ is in no byte, ୍ୱ is the
    /// ba-phala stroke of ୍ବ).
    /// </summary>
    public static bool Same(string got, string want, string hex, FontConverter forward)
    {
        if (got == want) return true;
        var r = forward.Convert(got);
        return Hex(r.Text) == hex && ReportsAsSpelled(r, hex);
    }

    /// <summary>The reverse atoms: the forward table's pieces read back (the subjoined forms, the low u/uu, the phalas).</summary>
    public IEnumerable<(string Hex, string U)> Atoms(IReadOnlyList<Truth> truth)
    {
        foreach (var (u, hex) in Learner.Subjoined(truth))
            if (u.Length == 2 && !hex.StartsWith("FA")) yield return (hex, u);      // ୍କ 50, ୍ର F7, ୍ୟ BF, ୍ବ B4 ...
        yield return ("EC", "\u0B41");
        yield return ("EE", "\u0B42");
        yield return ("F8", "\u0B4D\u0B30\u0B41");
        yield return ("F9", "\u0B4D\u0B30\u0B42");
        yield return ("5D", "\u0B3F\u0B01");
    }

    public Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0))
            if (canon[t.Pref] == t.U) pairs[Staged(FromHex(t.Pref))] = t.U;
        pairs[FromHex("FA")] = "\u0B4D" + HalantMark;
        pairs[FromHex("F6")] = Rules.Reph;
        pairs[FromHex("7D")] = "\u0B3F" + Rules.Reph;
        pairs[FromHex("F1")] = "\u0B56" + Rules.Reph;
        pairs[FromHex("7B")] = Rules.ESign;
        foreach (var (hex, u) in Atoms(truth)) pairs.TryAdd(Staged(FromHex(hex)), u);
        foreach (var (u, hex) in Learner.Extra.Where(e => e.U != "\u0965")) pairs.TryAdd(Staged(FromHex(hex)), Visual(u));
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
    /// Every spelling of Shree's composer in the capture (every kind - the truth's preferred, accepted and alternative
    /// spellings, the vendor keys, the alt and quirk rows - as a document holds it, <see cref="Fam.Doc"/>), read back:
    /// never Latin, a control code or a noncharacter; and the text typed, or text the table writes with those very bytes,
    /// wherever the text typed was well formed. Returns the counts by outcome.
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
               $"other text (the bytes are another text's: a quirk row, a sign struck out of order) {other}, Latin/control/noncharacter {latin}, malformed {malformed}";
    }
}
