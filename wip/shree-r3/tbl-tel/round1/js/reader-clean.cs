using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree-Tel -> Unicode pairs of one family: every preferred spelling must read back to its Unicode, or to a
/// Unicode string the table writes with the very same bytes - and so must every other spelling Shree's composer wrote
/// (the accepted ones, the alternatives, the vendor keys, the quirks: Capture.Spellings), as text, never Latin.
///
/// The pre rules drop the spacers the composer puts before a vattu (A0 or 9B after క్ష, A0 before ృ). The pairs answer
/// in visual order - a sign on a cluster right after its first letter, as the bytes are, the ra-vattu hook as a mark
/// before its cluster - and the post rules put Unicode order back: the sign after the subjoined letters, ె + \u0C56 = ై, the
/// hook as ్ర after the last consonant of the cluster it stands before (or, with no cluster after it - the composer's
/// pending hook - of the one before it), and a visible halant (్ + ZWNJ) that ends a word as the virama alone.
/// </summary>
public sealed class Reader
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules;
    public readonly List<RewriteRule> PostRules;
    readonly FontConverter _stager, _visual;

    /// <summary>The ra-vattu hook in visual order (a noncharacter): 7B, 5B, 93.</summary>
    public const string Hook = Rules.Ra;

    /// <summary>The vattu codes (and the combined 87 88 89, and the composer's ్ఱ CA and ్ఙ 5D): a spacer before one is dropped.</summary>
    public static string VattuCodes => string.Join(" ", Learner.Vattus.Select(v => v.Hex).Concat(new[] { "87", "88", "89", "CA", "5D" }));

    public Reader(Fam f)
    {
        F = f;
        PreRules = ReversePre();
        PostRules = ReversePost();
        _stager = new FontConverter(Table("s", "s", "a", "b", FontType.Legacy, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));
        _visual = new FontConverter(Table("v", "v", "a", "b", FontType.Unicode, FontType.Unicode, Rules.ForwardPre(), Array.Empty<(string, string)>()));
    }

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>One legacy code as a regex literal.</summary>
    public static string Lit(string hex)
    {
        char c = FromHex(hex)[0];
        return "\\^$.|?*+()[]{}-/>".Contains(c) ? "\\" + c : c.ToString();
    }

    public static string Class(string hexList) => "[" + string.Concat(hexList.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Lit)) + "]";

    const string Cons = "\u0C15-\u0C28\u0C2A-\u0C33\u0C35-\u0C39";
    const string Cluster = "(?:[" + Cons + "]\u0C4D)*[" + Cons + "]";
    const string Signs = "[\u0C3E-\u0C42\u0C46\u0C47\u0C4A-\u0C4C\u0C56]";

    /// <summary>
    /// Unicode in the order the pairs answer: the forward table's own pre rules (Rules.ForwardPre - the sign on the first
    /// letter of a cluster, ై split, the hook before its cluster, the visible halant), with its marks written back as the
    /// Unicode they stand for (క్ష, ్ష్మ, ్త్ర, ్ట్ర, \u0C56) or as the hook mark, and its spacers dropped.
    /// </summary>
    static string Unmark(string staged) => staged
        .Replace(Rules.K, "క్ష").Replace(Rules.SsaMa, "్ష్మ").Replace(Rules.TaRa, "్త్ర").Replace(Rules.TtaRa, "్ట్ర")
        .Replace(Rules.RaDeep, Hook).Replace(Rules.AiWide, "\u0C56").Replace(Rules.SpaceA0, "").Replace(Rules.Space9B, "");

    /// <summary>Every dependent vowel sign, ై and the length marks included (the backward hook and the move back take them all).</summary>
    const string AnySign = "[ా-ౄె-ైొ-ౌౕ\u0C56]";

    public static List<RewriteRule> ReversePre() => new()
    {
        Rules.NoMarksIn,
        R($"(?<={Class("5E 62 99 AF D0 D4")}){Class("40 B6")}(?={Class("FC FD FE FF")})", FromHex("E8"),
          "40 or B6 between a letter's body and its spacer is the talakattu the earlier TextSoft table wrote there (D0 40 FE = వ; the composer writes 40 B6 only for ః ఁ, after a syllable): the composer's E8 after the bodies of చ ఛ త న మ వ శ ..."),
        R($"{Class("40 B6")}(?={Class("FC FD FE FF")})", FromHex("E6"),
          "... and E6 after the others (C4 B6 FD = య)."),
        R($"(?<={Class("FD FE FF 9C")}){Lit("AC")}", FromHex("24").Replace("$", "$$"),
          "AC after a letter's spacer, or after ఘ ఫ, is the second u-hook the earlier TextSoft table wrote there (ము, యు, ఘు, ఋ = BA AC); the composer writes 24 (ము = D0 E8 FE 24 24) and AC only for ఽ, which follows a vowel sign or a vowel."),
        R($"(?<={Lit("BA")}){Lit("AC")}", FromHex("24 24").Replace("$", "$$"),
          "BA AC is the earlier table's ఋ: the composer's BA 24 24."),
        R($"{Lit("5D")}(?={Class("40 B6")}|{Lit("A0")}{Lit("96")})", FromHex("94"),
          "5D before the composer's own ః ఁ (40 B6, codes the table never writes) or before ృ behind its spacer (A0 96) is the composer's subjoined ఙ (its 5D), not the visarga: read as ్ఙ (94)."),
        R($"(?<!{Lit("84")}{Lit("F0")}{Lit("FC")}){Lit("95")}{Lit("5D")}", FromHex("95 94"),
          "The ై mark 95 before 5D (but after క్ష + ై, whose mark is always 95) is the composer's ై before its subjoined ఙ: read as ్ఙ (94)."),
        R($"{Lit("9B")}{Lit("5D")}", FromHex("94"),
          "The wide spacer 9B before 5D (after క్ష: 84 E6 FC 9B 5D) is the composer's spacer and subjoined ఙ (its 5D): the table writes ః after క్ష with no spacer. Read as ్ఙ (94)."),
        R($"{Lit("9B")}(?={Class("A0 7B 5B")})", "",
          "The wide spacer 9B before a spacer or a hook is the composer's, left where it dropped the vattu (a subjoined ఝ: 84 E6 FC 9B A0 96): not text."),
        R($"{Class("A0 9B")}(?={Class(VattuCodes)}|{Lit("96")}|{Class("85 5D 97 40 B6")})", "",
          "The spacer the composer puts before a vattu (A0 or 9B after క్ష: 84 E6 FC A0 50 = క్ష్క) or before ృ after a vattu (50 A0 96) is not text; nor is one left before a tail where the composer dropped what stood there (ౄ, a subjoined ఝ: 84 E6 FC 9B 85)."),
    };

    public static List<RewriteRule> ReversePost() => new()
    {
        R("([" + Cons + "])((?:" + AnySign + "|్\u200C)+)((?:్[" + Cons + "])+)", "$1$3$2",
          "A sign (or the visible halant) the bytes put on the first letter of a cluster goes after its subjoined letters (4D EC FC 50 = క్కి; ృ struck before the link, 4D E6 FC 96 50, too)."),
        R("ె\u0C56", "ై", "ె + \u0C56 is ై (కై = 4D F0 FC 4F)."),
        R(Hook + "(" + Cluster + ")", "$1్ర",
          "The ra-vattu hook (7B, 5B, 93) belongs to the cluster after it: ్ర after its last consonant, before its sign (7B 4D EC FC = క్రి; 4D E6 FC 7B 4D E6 FC = కక్ర, as the composer reads it)."),
        R("(" + Cluster + ")((?:" + AnySign + "|[ఀ-ఄ])*)" + Hook, "$1్ర$2",
          "A hook with no letter after it is the composer's pending ra-vattu (C 7B: the hook struck after the letter, or its third hook 93 typed after a syllable): the cluster before it."),
        R(Hook, "్ర", "Any other hook: the ra-vattu."),
        R("్\u200C(?![" + Cons + "])", "్",
          "A visible halant with no consonant after it is the virama alone (a word-final virama is drawn with the halant anyway); before a consonant the ZWNJ stays (క్\u200Cక, not క్క)."),
        R(Rules.Keep, "", "U+FDD1 is not text."),
    };

    public string Staged(string legacy) => _stager.ConvertText(legacy);
    public string Visual(string unicode) => Unmark(_visual.ConvertText(unicode));

    static bool Plain(string u) => !u.Any(c => IsJoiner(c) || Learner.NoGlyph.Contains(c.ToString()) || c == '₹');

    /// <summary>The Unicode a spelling reads back as, when several strings share it: the plainest, then the shortest.</summary>
    public static Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        truth.Where(t => t.Pref.Length > 0 && Reported(t.Pref).Count == 0).GroupBy(t => t.Pref)
             .ToDictionary(g => g.Key, g => g.OrderBy(t => Plain(t.U) ? 0 : 1)
                                              .ThenBy(t => Malformed(t.U).Count > 0 ? 1 : 0)
                                              .ThenBy(t => t.U.Length)
                                              .ThenBy(t => t.U, StringComparer.Ordinal).First().U, StringComparer.Ordinal);

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table(F.Id + "-to-unicode", F.Name + " -> Unicode", F.Id, "gautami", FontType.Legacy, FontType.Unicode, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> in this family: equal, or text the
    /// table writes with the very same codes as <paramref name="hex"/>.
    /// </summary>
    public static bool Same(string got, string want, string hex, FontConverter forward)
    {
        if (got == want) return true;
        var r = forward.Convert(got);
        return Hex(r.Text) == hex && ReportsAsSpelled(r, hex);
    }

    /// <summary>
    /// Codes the composer writes that no preferred spelling holds, and codes the table writes for what the truth spells
    /// otherwise: what each reads as. The hooks 7B 5B 93 (93: the 7-only code E1, the same glyph as 7B); the vattus; the
    /// combined vattus 87 88 89; the ై marks 4F 95; the halant marks 8A 8C on nothing; the tails, the composer's codes for
    /// ః ఁ (40 B6, talakattu ticks in the fonts) and the fonts' own (5D 97); CA, the composer's ్ఱ (a ూ tail in the fonts); the blank 9B (the composer's
    /// en dash; two of them its em dash) and 2D; the hyphen 26; the bullet B7; the blank 90 (the same glyph as A0); the
    /// digits 30-39, which draw Latin digits (the composer writes ౦-౯ there); 92 as the family draws it.
    /// </summary>
    public (string Hex, string Unicode)[] FontCodes => Learner.Vattus.Select(v => (v.Hex, v.U)).Concat(new[]
    {
        ("7B", Hook), ("5B", Hook), ("93", Hook), ("87", "\u0C4D\u0C37\u0C4D\u0C2E"), ("88", "\u0C4D\u0C24\u0C4D\u0C30"), ("89", "\u0C4D\u0C1F\u0C4D\u0C30"),
        ("4F", "\u0C56"), ("95", "\u0C56"), ("8A", "\u0C4D\u200C"), ("8C", "\u0C4D\u200C"),
        ("85", "\u0C02"), ("5D", "\u0C03"), ("40", "\u0C03"), ("97", "\u0C01"), ("B6", "\u0C01"), ("CA", "\u0C4D\u0C31"),
        ("9B 9B", "\u2014"), ("9B", "\u2013"), ("2D", "-"), ("26", "-"), ("B7", "\u2022"), ("90", "\u00A0"),
        ("92", F.RupeeAt92 ? "\u20B9" : "*"),
    }).Concat(Enumerable.Range(0, 10).Select(d => ((0x30 + d).ToString("X2"), ((char)('0' + d)).ToString()))).ToArray();

    public Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, IEnumerable<char> fontCodes, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (hex, u) in FontCodes) pairs[Staged(FromHex(hex))] = u;
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0))
            if (canon[t.Pref] == t.U) pairs.TryAdd(Staged(FromHex(t.Pref)), Visual(t.U));
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
        int pieces = 0;
        foreach (var (code, u) in Pieces(truth, fontCodes))
            if (pairs.TryAdd(Staged(code.ToString()), u)) pieces++;
        stats = $"{F.Id} reverse: atoms {atoms}, learned {added}, unkeyable {unkeyable}, pieces read alone {pieces}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>
    /// Every code of the fonts read alone, so no code ever passes through as the Latin letter of its code point (a piece
    /// of a glyph the composer wrote on its own: its carrier E6 61 E6 61 for a lone sign, the e-shape F2 of its lone ై key,
    /// the pieces of ధ it writes for ౢ). Read off the truth: a code that begins the spelling of a letter alone (ధ = AB A7 E6 FE)
    /// reads as that letter; a code a sign adds to a letter's spelling (కా = 4D 3E: 3E) reads as that sign; any other code
    /// of a letter's spelling (the talakattu E6, the spacers FC-FF) as nothing. The most frequent reading wins.
    /// </summary>
    public static List<(char Code, string Unicode)> Pieces(IReadOnlyList<Truth> truth, IEnumerable<char> fontCodes)
    {
        var byU = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var votes = new Dictionary<char, Dictionary<string, int>>();
        void Vote(char c, string u) { if (!votes.TryGetValue(c, out var d)) votes[c] = d = new(); d[u] = d.GetValueOrDefault(u) + 1; }
        foreach (var t in truth.Where(t => t.Composer is null && t.Pref.Length > 0 && Reported(t.Pref).Count == 0))
        {
            var codes = FromHex(t.Pref);
            if (t.U.Length == 1 && (IsCons(t.U[0]) || IsVowel(t.U[0])))
                for (int i = 0; i < codes.Length; i++) Vote(codes[i], i == 0 ? t.U : "");
            else if (t.U.Length == 2 && IsCons(t.U[0]) && IsVowelSign(t.U[1]) && byU.TryGetValue(t.U[..1], out var letter))
            {
                var own = FromHex(letter.Pref);
                foreach (char c in codes.Where(c => !own.Contains(c))) Vote(c, t.U[1..]);
            }
        }
        var list = new List<(char, string)>();
        foreach (char c in fontCodes.Where(c => c > ' '))
            list.Add((c, votes.TryGetValue(c, out var d) ? d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key : ""));
        return list;
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
    /// Every spelling of Shree's composer in the capture (Capture.Spellings: every kind), read back: never Latin, a control
    /// code or a noncharacter; and the text typed, or text the table writes with those very bytes, wherever the text typed
    /// was well formed. Returns the counts by outcome.
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
               $"other text (the bytes are another text's) {other}, Latin/control/noncharacter {latin}, malformed {malformed}";
    }
}
