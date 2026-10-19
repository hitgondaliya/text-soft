using System.Text;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Shree-Ban -> Unicode pairs of one family: every preferred composer spelling
/// must read back to its Unicode, or to a Unicode string the composer writes the same way.
///
/// The pre rules undo what the composer does to the order: the spacer A1 (and FE) after a
/// letter goes, and the reph A2 - written after its letter and the signs on it - moves in
/// front of its cluster, where Unicode has র্. The pairs answer in visual order (the
/// pre-base signs ি ে ৈ in front of their cluster, as the bytes are), and the post rules
/// move those signs to their Unicode place and join ে..া / ে..ৗ into ো / ৌ.
///
/// Which codes are letters and which are signs is read off the family's truth: a code that
/// starts the spelling of a letter or vowel is a letter, a code that never does is a sign.
/// </summary>
public sealed class Reader
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules;
    public readonly List<RewriteRule> PostRules;
    readonly FontConverter _stager, _visual;

    /// <summary>Marks the Bengali র the SHREE-ASS 4/5/6 fonts draw at 0F, so the Assamese ৰ rule leaves it (a noncharacter).</summary>
    public const string BengaliRa = "\uFDD7";

    /// <summary>Marks the spacer the composer keeps after ড় ঢ় for their ু (a noncharacter).</summary>
    public const string UMark = "\uFDD9";

    /// <summary>The pre-base sign codes (4/5/6): ি ে ৈ, their other glyphs, ি+ঁ.</summary>
    public const string PreBase = "5B EC EE EB ED 7B";

    public Reader(Fam f, IReadOnlyList<Truth> truth)
    {
        F = f;
        PreRules = ReversePre(f, truth);
        PostRules = ReversePost(f);
        _stager = new FontConverter(Table("s", "s", "a", "b", FontType.Legacy, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));
        _visual = new FontConverter(Table("v", "v", "a", "b", FontType.Unicode, FontType.Unicode, VisualRules(), Array.Empty<(string, string)>()));
    }

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>
    /// A cluster in Unicode: letters joined by virama (a ZWNJ may keep a ra-phala or ya-phala apart,
    /// র‌্য, or stand after the hasanta of a ra drawn with it, র্‌ক: a pre-base sign written before
    /// that ra - 5B B9 F4 41 A1 - is the sign of the whole, র্‌কি, not রি্‌ক).
    /// </summary>
    const string Cluster = "(?:[" + Rules.Cons + "]়?(?:্\u200C|\u200C?্))*[" + Rules.Cons + "]়?";

    /// <summary>Unicode in the order the pairs answer: the pre-base signs in front of their cluster.</summary>
    static List<RewriteRule> VisualRules() => new()
    {
        R("ো", "ো", "split"),
        R("ৌ", "ৌ", "split"),
        R($"({Cluster})িঁ", "িঁ$1", "i + candrabindu"),
        R($"({Cluster})([িেৈ])", "$2$1", "pre-base"),
    };

    /// <summary>The letter and sign codes of a family, read off its truth.</summary>
    public static (HashSet<string> Letters, HashSet<string> Signs) Classes(Fam f, IReadOnlyList<Truth> truth)
    {
        var pre = PreBase.Split(' ').Select(f.Code).ToHashSet();
        string nukta = f.Code("D8"), spacer = f.Code("A1"), reph = f.Code("A2"), hasanta = f.Code("F4");
        var letters = new HashSet<string>();
        var all = new HashSet<string>();
        foreach (var t in truth)
        {
            // Letters and vowels only: digits and punctuation never carry a reph.
            if (t.U.Length == 0 || !(IsCons(t.U[0]) || IsVowel(t.U[0]))) continue;
            var toks = t.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var c in toks) all.Add(c);
            var first = toks.SkipWhile(c => pre.Contains(c) || c == nukta).FirstOrDefault();
            if (first is not null && first != reph && first != spacer) letters.Add(first);
        }
        // The ligatures outside the truth (ত্ত্ব 77 ...) are letters too.
        foreach (var (u, hex) in Learner.Extra.Where(e => IsCons(e.U[0])))
        {
            var toks = f.Apply(hex).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            letters.Add(toks[0]);
            foreach (var c in toks) all.Add(c);
        }
        // And the letter codes no table writes but a document can hold (ব্ঢ 81, ৱ 9E, ...).
        foreach (var (hex, u) in f.FontCodes.Where(e => !e.Hex.Contains(' ') && e.Unicode.Length > 0 && IsCons(e.Unicode[0])))
            letters.Add(hex);
        var signs = all.Where(c => !letters.Contains(c) && !pre.Contains(c) && c != nukta && c != spacer && c != reph && c != hasanta
                                   && c != f.Code("FE")).ToHashSet();
        return (letters, signs);
    }

    /// <summary>
    /// The half letters of a family, read off its truth: letter codes that never end a spelling and
    /// stand straight before another letter (ঙ্ 56 in ঙ্ক্ষ = 56 DB, ণ্ 73, the ন্ hook 96 - 40 in the
    /// 7 fonts -, গ্ 4F, চ্ 5A, প্ 9F, ম্ B4, ল্ C2, শ্ C6, ষ্ CD, স্ D1). With the letter after them they
    /// are one cluster: the reph written after it (র্ঙ্ক্ষ = 56 DB A2 A1) belongs to all of it.
    /// </summary>
    public static HashSet<string> Halves(Fam f, IReadOnlyList<Truth> truth, HashSet<string> letters)
    {
        string spacer = f.Code("A1"), fe = f.Code("FE"), nukta = f.Code("D8");
        var ends = new HashSet<string>();
        var before = new HashSet<string>();
        foreach (var t in truth)
        {
            var toks = t.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(c => c != spacer && c != fe).ToList();
            for (int i = 0; i < toks.Count; i++)
            {
                if (!letters.Contains(toks[i])) continue;
                if (i + 1 == toks.Count) ends.Add(toks[i]);
                else if (letters.Contains(toks[i + 1]) || toks[i + 1] == nukta) before.Add(toks[i]);
            }
        }
        return before.Where(c => !ends.Contains(c)).ToHashSet();
    }

    static string ClassOf(IEnumerable<string> codes)
    {
        var sb = new StringBuilder("[");
        foreach (var h in codes.OrderBy(h => h))
        {
            char c = FromHex(h)[0];
            sb.Append("\\^$.|?*+()[]{}-/>".Contains(c) ? "\\" + c : c.ToString());
        }
        return sb.Append(']').ToString();
    }

    public static List<RewriteRule> ReversePre(Fam f, IReadOnlyList<Truth> truth)
    {
        var (letters, signs) = Classes(f, truth);
        var halves = Halves(f, truth, letters);
        // Not the ন hook 96 where 96 alone is also read as ঃ (the 4/5/6 order: the earlier table wrote ঃ as 96 in
        // these fonts, ঃর্প = 96 9A A2): the composer's own hook + reph spellings (র্ন্দ = 96 83 A2) have pairs.
        if (!f.Map.ContainsKey("96")) halves.Remove("96");
        string L = ClassOf(letters), S = ClassOf(signs);
        string sp = Rules.Lit(f, "A1"), fe = Rules.Lit(f, "FE"), danda = Rules.Lit(f, "FA");
        string nukta = Rules.Lit(f, "D8"), hasanta = Rules.Lit(f, "F4"), reph = Rules.Lit(f, "A2");
        // A half letter before the letter is part of the cluster (র্ঙ্ক্ষ = 56 DB A2 A1: the reph goes in front of 56).
        string cluster = (halves.Count > 0 ? $"(?:{ClassOf(halves)})*" : "") + $"(?:{nukta}?{L}{hasanta})*{nukta}?{L}";
        string twice = $"[{Rules.Lit(f, "F2")}{reph}]+{sp}";
        var rules = new List<RewriteRule> { Rules.NoMarksIn };
        var raw = RawComposer(f, truth, letters, halves);
        // ৱ is 9E in the Bangla fonts, at U+009E in their cmaps (Common.Wa); a .txt decoded as
        // Windows-1252 carries it as U+017E. Both are read, in every family (Fam.FontCodes: 9E is ৱ).
        rules.Add(R("\u017E", "\u009E", "U+017E (Windows-1252's 9E, as a .txt file decodes it) is the 9E the Bangla fonts draw ৱ at (U+009E)."));
        rules.AddRange(new[]
        {
            R($"{sp}(?!{danda}|{twice})|{fe}", "",
              "The spacers A1 and FE draw nothing; they only follow a letter. Kept: A1 before FA (the danda), and an A1 a ঁ or reph " +
              "and a second A1 follow - the composer's ড়ু ঢ়ূ (D8 6C A1 F2 A1), র্ৰ্রু, র্হ্রৃ, told from ড়ঁ র্রু র্হৃ only by it."),
            // ড়ু ঢ়ু: the composer marks the ু with that kept spacer (D8 6C A1 A2 A1 is র্ড়ু). As a sign of
            // its own it is read wherever the ড় stands - after a letter that joins the cluster too (র্ষ্ড়ু).
            R($"(?<!{Rules.Class(f, PreBase)})({nukta}[{Rules.Lit(f, "6C")}{Rules.Lit(f, "6E")}]{Rules.Lit(f, "F6")}?){sp}(?!{danda})", "$1" + UMark,
              "The spacer kept after ড় ঢ় (and their ra-phala) is the composer's ু there (U+FDD9 marks it) - not after a pre-base sign: " +
              "ি ে ৈ and ু on one letter is no text (5B D8 6C A1 A2 A1 is র্ড়িং with its ং lost, the composer writes A1 for it)."),
            R($"({cluster})((?:{S}|{hasanta}|{sp}|{UMark})*){reph}", Rules.Rep(f, "A2") + "$1$2",
              "The reph is written after its letter (a conjunct with a visible hasanta, all of it) and the signs on it; Unicode has র্ first."),
        });
        // Before the spacers go (a letter with its spacer before ৺ is a letter and ৺; the composer's ্ন follows the letter's
        // code straight - গ্ন = 4E 97), and before the reph moves (it takes the ন hook into its cluster).
        rules.InsertRange(2, raw);
        return rules;
    }

    /// <summary>
    /// P5 (round 2 merge): what Shree's own 7 composer writes in a 7 font, and the table shipped before the 7 / 4/5/6 split
    /// wrote in a 4/5/6 one, read as the text it was typed for - merge-check1 found the 7 composer's raw bytes read as '@'
    /// (2,154 spellings) and 'N' (18) by the ban7 and ass7 readers: that composer writes the 4/5/6 codes, ঃ 40 and ্ন 97.
    /// <list type="bullet">
    /// <item>The subscript ন: in each order the other code is ৺ (A5 in 4/5/6, 97 in 7), which never follows a letter, so
    ///   straight after a code the subscript ন follows in the truth (no spacer between) it is the subscript ন (the composer's
    ///   গ্ন = 4E 97 in a 7 font, the old table's 4E A5 in a 4/5/6 one); after a letter's spacer it is ৺.</item>
    /// <item>ঃ in the 7 order: 40 is the half-ন hook there, which only ever comes before ট দ ড or ্ম (the codes after it in
    ///   the truth); before anything else - or at the end - it is the composer's ঃ (দুঃখ = 83 E5 40 4A), read as the
    ///   7 fonts' ঃ 96.</item>
    /// </list>
    /// </summary>
    static List<RewriteRule> RawComposer(Fam f, IReadOnlyList<Truth> truth, HashSet<string> letters, HashSet<string> halves)
    {
        var rules = new List<RewriteRule>();
        string isshar = f.Code("A5"), na = f.Code("97");
        // The codes the subscript ন follows in the truth (গ্ন ক্ন ঘ্ন ত্ন ধ্ন প্ন ম্ন শ্ন): no spacer between.
        var before = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var t in truth)
        {
            var toks = t.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < toks.Length; i++) if (toks[i] == na) before.Add(toks[i - 1]);
        }
        rules.Add(R($"(?<={ClassOf(before)}){ClassOf(new[] { isshar })}", FromHex(na),
            $"{isshar} (৺ in these fonts) after a letter is the subscript ন {na}: ৺ never follows a letter, and Shree's 7 composer (97) and the " +
            "table shipped before the 7 / 4/5/6 split (A5) wrote the other order's code for it (গ্ন = 4E 97 / 4E A5)."));
        if (f.Map.TryGetValue("40", out var visarga) && visarga == "96")
        {
            var partners = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in truth)
            {
                var toks = t.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 1 < toks.Length; i++) if (toks[i] == "40") partners.Add(toks[i + 1]);
            }
            rules.Add(R($"{ClassOf(new[] { "40" })}(?!{ClassOf(partners)})", FromHex("96"),
                $"40 is the half-ন hook in these fonts, which only comes before {string.Join(" ", partners)}; anywhere else it is the ঃ " +
                "Shree's 7 composer writes there (its 4/5/6 code; দুঃখ = 83 E5 40 4A): read as the 7 fonts' ঃ 96."));
        }
        return rules;
    }

    public static List<RewriteRule> ReversePost(Fam f)
    {
        var rules = Post();
        rules.Insert(0, R("্্", "্", "A half letter read alone (ম্ B4) and a subscript (্ম B5) after it are one conjunct: one hasanta."));
        // ZWNJ after a hasanta keeps a ra from becoming a reph (র্‌ক); with no letter after it, it says nothing.
        rules.Add(R($"্\u200C(?![{Rules.Cons}{Rules.Vowels}])", "্", "A hasanta with nothing after it needs no ZWNJ (কর্: B9 F4 at the end of a word)."));
        if (f.IsAssamese)
            rules.Add(R("র", "ৰ", "Assamese: every ra is ৰ - the letter B9, the reph and the ra-phala alike; a word never mixes ৰ and র."));
        else
            rules.Add(R("ৰ", "র", "Bangla: every ra is র - B9 draws র in these fonts (they have no ৰ), the reph and the ra-phala too."));
        rules.Add(R(BengaliRa, "র", "The Bengali র the Assamese 4/5/6 fonts hold at 0F."));
        rules.Add(R("¡", "", "A spacer A1 no pair took draws nothing: it is not text."));
        return rules;
    }

    static List<RewriteRule> Post() => new()
    {
        R($"ে({Cluster})া", "$1ো", "ে ... া around a cluster is ো."),
        R($"ে({Cluster})ৗ", "$1ৌ", "ে ... ৗ around a cluster is ৌ."),
        R($"ি(ঁ?)({Cluster})", "$2ি$1", "The i-sign (and the candrabindu of 7B) goes after its cluster (visual order -> Unicode order)."),
        R($"([েৈ])({Cluster})", "$2$1", "The e/ai-signs go after their cluster."),
        R("৯(?=[ঁংঃ])", "ঌ", "ঌ is drawn with the digit ৯ (39); with ঁ ং ঃ on it, it is the vowel ঌ, not a digit."),
        R("অা", "আ", "অ + া is written আ."),
        R("র([ুূৃ])্য", "র‌্য$1",
          "রু/রূ + ya-phala (B9 E7 A1 B8) is র‌্যু: the ya-phala under র needs the ZWNJ, and the sign comes after it."),
        R("([ুূৃ])্য", "্য$1",
          "The composer writes গু শু হু হৃ with the ya-phala after them (গ্যু = 50 A1 B8): in Unicode the ya-phala comes first."),
        R("([ুূৃ])্র", "্র$1",
          "And রু রূ (B9 E7, B9 EA: glyphs of their own) with the ra-phala after them (র্রু typed as র + ra-phala: B9 E7 F6 A1): the ra-phala first."),
        R("ো", "ো", "ে + া with no letter between is ো."),
        R("ৌ", "ৌ", "ে + ৗ with no letter between is ৌ."),
    };

    public string Staged(string legacy) => _stager.ConvertText(legacy);
    public string Visual(string unicode) => _visual.ConvertText(unicode);

    static bool Plain(string u) => !u.Any(c => IsJoiner(c) || Learner.Dropped.Contains(c.ToString()) || Learner.NoGlyph.Contains(c.ToString()));

    /// <summary>The Unicode a spelling reads back as, when several strings share it.</summary>
    public Dictionary<string, string> Canonical(IReadOnlyList<Truth> truth) =>
        truth.Where(t => t.Pref.Length > 0).GroupBy(t => t.Pref)
             // An Assamese font draws ৰ at B9: read it as ৰ there, as র in a Bangla font (B9 F4 is
             // র্ + ZWNJ there, the visible-hasanta ra, not the Assamese ৰ্).
             .ToDictionary(g => g.Key, g => g.OrderBy(t => F.IsAssamese ? (t.U.Contains('ৰ') ? 0 : 1) : (t.U.Contains('ৰ') ? 1 : 0))
                                              .ThenBy(t => Plain(t.U) ? 0 : 1)
                                              // ঌ is drawn as the digit ৯ (39): read the digit.
                                              .ThenBy(t => t.U.Contains('ঌ') ? 1 : 0)
                                              .ThenBy(t => t.U.Length)
                                              .ThenBy(t => t.U, StringComparer.Ordinal).First().U, StringComparer.Ordinal);

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table(F.Id + "-to-unicode", F.Name + " -> Unicode", F.Id, "vrinda", FontType.Legacy, FontType.Unicode, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    /// <summary>
    /// True when <paramref name="got"/> is the same text as <paramref name="want"/> in this
    /// family: equal, or text the composer writes with the very same codes (so a character
    /// the family has no glyph for may be missing: nothing in the bytes says it was there).
    /// Not "equal but for the joiners": র‌্য (B9 B8) and র্য (99 A2) are different spellings.
    /// </summary>
    public static bool Same(string got, string want, string prefHex, FontConverter forward)
    {
        if (got == want) return true;
        var r = forward.Convert(got);
        return Hex(r.Text) == prefHex && ReportsAsSpelled(r, prefHex);
    }

    /// <summary>
    /// What a sign code reads as alone, from the truth: a spelling that is a letter's spelling and
    /// one code more (খ্র = 4A F8, খ = 4A: F8 is ্র). A document may hold a sign where no pair of
    /// the truth puts it - ৰ্র typed as ৰ + ra-phala, B9 F8 - and it must read as the sign, not as
    /// the Latin letter its byte is (ø).
    /// </summary>
    public Dictionary<string, string> SignReadings(IReadOnlyList<Truth> truth)
    {
        // On the staged spelling (spacers gone, the reph in front), so রু = B9 E7 A1 gives E7 = ু.
        var byStaged = truth.Where(t => t.Pref.Length > 0).GroupBy(t => Staged(FromHex(t.Pref)))
                            .ToDictionary(g => g.Key, g => g.Select(t => t.U).ToList(), StringComparer.Ordinal);
        var votes = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var t in truth.Where(t => t.Pref.Length > 0 && Malformed(t.U).Count == 0))
        {
            string st = Staged(FromHex(t.Pref));
            if (st.Length < 2) continue;
            string head = st[..^1], tail = st[^1].ToString();
            if (!byStaged.TryGetValue(head, out var us)) continue;
            foreach (var u1 in us.Where(u1 => t.U.Length > u1.Length && t.U.StartsWith(u1, StringComparison.Ordinal) && (IsCons(u1[^1]) || u1[^1] == '়')))
            {
                string sign = t.U[u1.Length..];
                if (!(IsMark(sign[0]) || IsJoiner(sign[0]))) continue;
                if (!votes.TryGetValue(tail, out var v)) votes[tail] = v = new();
                v[sign] = v.GetValueOrDefault(sign) + 1;
            }
        }
        return votes.ToDictionary(kv => kv.Key, kv => kv.Value.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key);
    }

    /// <summary>Text without the characters the family writes as nothing.</summary>
    public static string Visible(string u, FontConverter forward) => new(u.Where(c => forward.ConvertText(c.ToString()).Length > 0).ToArray());

    public Dictionary<string, string> Learn(IReadOnlyList<Truth> truth, FontConverter forward, out string stats, List<string>? log = null)
    {
        var canon = Canonical(truth);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in truth.Where(t => t.U.Length == 1 && t.Pref.Length > 0))
            if (canon[t.Pref] == t.U) pairs[Staged(FromHex(t.Pref))] = t.U;
        // FA alone is a danda stroke: read it as । (so ।। = A1 FA FA reads ।।), not as the rare ৷ the forward table also writes FA for.
        pairs.TryAdd(FromHex(F.Code("FA")), "।");
        // 7B is ি + ঁ, one glyph in front of its cluster, whatever the cluster (স্তিঁ = 7B D1 7A, র্ক্ষিঁ =
        // 7B DB A2 A1): read alone, in visual order (the post rule moves it after the cluster), so no
        // cluster needs a pair of its own for it - 7B is never '{'.
        pairs.TryAdd(FromHex(F.Code("7B")), "িঁ");
        // 3C is where Shree's composer writes ₹ (no face has a glyph there: "quirk:7-font-has-no-₹" in the
        // capture). The tables never write 3C ('<' is left out, reported; ₹ passes through), so 3C is ₹.
        pairs.TryAdd(FromHex("3C"), "₹");
        foreach (var (u, hex) in Learner.Extra) pairs.TryAdd(Staged(FromHex(F.Apply(hex))), Visual(u));
        foreach (var (hex, u) in F.FontCodes) pairs.TryAdd(Staged(FromHex(hex)), u);
        // Every sign code read alone as the sign it is (F8 = ্র), where nothing above reads it.
        foreach (var (key, u) in SignReadings(truth)) pairs.TryAdd(key, u);
        // P5, our own output (round 2): what the forward table passes through as itself (P3b: ₹, ৱ in a Bangla font, ৄ ৢ ৣ
        // ৠ ৡ, a nukta on a letter with no nukta spelling) reads back as itself, unreported - text in a fallback font.
        foreach (var c in NonIndic.PassedThrough(forward, 0x0980)) pairs.TryAdd(Staged(c), Visual(c));
        // ী struck before ঁ and a reph: the composer writes E3 85, ী and ীঁ - the long i twice, the reph
        // lost (its quirk). One ীঁ is the text it draws.
        pairs.TryAdd(FromHex(F.Code("E3") + " " + F.Code("85")), "ীঁ");
        // The reph alone (its letter is read after it: the pre rule put it in front), and ra with a
        // visible hasanta, B9 F4: র্‌ before a letter (else the text would be written with a reph), র্
        // at the end of a word (the post rule drops the ZWNJ there). The family's ra: ৰ in Assamese.
        pairs[FromHex(F.Code("A2"))] = F.Ra + "্";
        pairs[FromHex("B9 F4")] = F.Ra + "্\u200C";
        pairs[UMark] = "ু";
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
        // The other spellings the composer accepts - and its own spelling of a row a decision changed (র্হ্রৃ
        // = D2 EA A1 A2 A1, which loses the ra-phala F9) - read as their text too (P5), where the pairs so far
        // read them otherwise and no preferred spelling has the same staged key.
        var prefKeys = canon.Keys.Select(h => Staged(FromHex(h))).ToHashSet(StringComparer.Ordinal);
        var sofar = new FontConverter(Build(pairs));
        int accepted = 0;
        foreach (var t in truth.Where(t => Malformed(t.U).Count == 0))
            foreach (var a in t.Acc.Where(a => a != t.Pref && a.Length > 0))
            {
                if (Same(sofar.ConvertText(FromHex(a)), t.U, t.Pref, forward)) continue;
                string key = Staged(FromHex(a));
                if (key.Length == 0 || prefKeys.Contains(key) || pairs.ContainsKey(key)) continue;
                log?.Add($"{t.U}	{a}	{Hex(key)}	accepted, read {sofar.ConvertText(FromHex(a))}");
                pairs[key] = Visual(t.U);
                accepted++;
            }
        stats = $"{F.Id} reverse: atoms {atoms}, learned {added}, accepted spellings learned {accepted}, unkeyable {unkeyable}, total {pairs.Count}";
        return pairs;
    }

    public static string Check(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0;
        foreach (var t in truth)
        {
            if (t.Pref.Length == 0) continue;
            string got = reader.ConvertText(FromHex(t.Pref));
            if (got == t.U) exact++;
            else if (Same(got, t.U, t.Pref, forward)) same++;
            else fails.Add($"{t.Pref}\twant {t.U} [{Cps(t.U)}]\tgot {got} [{Cps(got)}]");
        }
        int n = exact + same + fails.Count;
        return $"read back {n}: exact {exact}, same text {same}, wrong {fails.Count} ({100.0 * (exact + same) / n:F2}%)";
    }

    /// <summary>
    /// Joined syllables read back. <paramref name="visibleOnly"/> leaves out the syllables
    /// with a character the family writes as nothing: their bytes carry no trace of it.
    /// </summary>
    public static string CheckWords(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, int count, int seed,
        bool visibleOnly, out List<string> fails)
    {
        var pool = visibleOnly ? truth.Where(t => Visible(t.U, forward) == t.U && t.Pref.Length > 0).ToList() : truth.ToList();
        fails = new List<string>();
        int ok = 0;
        foreach (var (u, want) in Checks.MakeWords(pool, count, seed))
        {
            string got = reader.ConvertText(FromHex(want));
            if (Same(got, u, want, forward)) ok++;
            else fails.Add($"{want}\twant {u}\tgot {got} [{Cps(got)}]");
        }
        return $"words read back {count}{(visibleOnly ? " (visible characters only)" : "")} (seed {seed}): right {ok}, wrong {fails.Count} ({100.0 * ok / count:F2}%)";
    }

    /// <summary>The other spellings the composer accepts, read back.</summary>
    public static string CheckAccepted(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int ok = 0, n = 0;
        foreach (var t in truth)
            foreach (var a in t.Acc.Where(a => a != t.Pref && a.Length > 0))
            {
                n++;
                string got = reader.ConvertText(FromHex(a));
                if (Same(got, t.U, t.Pref, forward)) ok++;
                else fails.Add($"{a}\twant {t.U}\tgot {got} [{Cps(got)}]");
            }
        return $"other accepted spellings read back {n}: right {ok}, wrong {fails.Count}";
    }

    /// <summary>
    /// Every reverse output well formed (Common.Malformed) where the text read was: each truth
    /// spelling and joined syllables. The "same bytes" check alone let রি্‌ক pass for র্‌কি.
    /// </summary>
    public static string CheckWellFormed(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0;
        var items = truth.Where(t => t.Pref.Length > 0).Select(t => (t.U, t.Pref))
            .Concat(Checks.MakeWords(truth, 20000, 7)).Concat(Checks.MakeWords(truth, 20000, 11));
        foreach (var (u, hex) in items)
        {
            if (Malformed(u).Count > 0) continue;
            n++;
            string got = reader.ConvertText(FromHex(hex));
            var bad = Malformed(got);
            bad.AddRange(AsciiLeaks(got, u));
            if (bad.Count > 0) fails.Add($"{hex}\tread {got} [{Cps(got)}] for {u}: {string.Join("; ", bad)}");
        }
        return $"well-formed read back {n}: malformed {fails.Count}";
    }

    /// <summary>
    /// Every spelling of Shree's composer in the capture (Capture.Spellings: every kind - the truth's
    /// preferred, accepted and alternative spellings, the vendor keys, the alt and quirk rows - as
    /// it wrote them and with ব ৰ ৱ moved), as a document in this family holds it, read back: never
    /// Latin, a C1 code or a noncharacter, and well formed wherever the text typed was.
    /// </summary>
    public static string CheckCapture(FontConverter reader, Fam fam, IEnumerable<(string U, string Hex)> spellings, out List<string> fails)
    {
        fails = new List<string>();
        int n = 0, typedWellFormed = 0, latin = 0, malformed = 0;
        foreach (var (u, hex) in spellings)
        {
            string doc = fam.Doc(hex);
            if (doc.Length == 0) continue;
            n++;
            // Well formed wherever the text typed is - and the bytes draw a letter: a spelling of signs
            // alone (the composer lost the letters it was typed: ক্হঃ written 40) can only read as them.
            bool typedOk = Malformed(u).Count == 0 && !hex.Split(' ').All(Capture.SignCodes.Contains);
            if (typedOk) typedWellFormed++;
            string got = reader.ConvertText(FromHex(doc));
            var bad = Malformed(got);
            bad.AddRange(AsciiLeaks(got, u));
            if (bad.Any(b => b.StartsWith("Latin") || b.StartsWith("ASCII") || b.StartsWith("C1") || b.StartsWith("noncharacter")))
            {
                latin++;
                fails.Add($"LATIN\t{doc}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join("; ", bad)}");
            }
            else if (typedOk && bad.Count > 0)
            {
                malformed++;
                fails.Add($"MALFORMED\t{doc}\ttyped {u}\tread {got} [{Cps(got)}]\t{string.Join("; ", bad)}");
            }
        }
        return $"composer spellings of the whole capture read back {n} ({typedWellFormed} must read well formed: typed well formed, a letter drawn): Latin/ASCII/C1/noncharacter {latin}, malformed {malformed}";
    }

    /// <summary>
    /// ASCII in a read back that the text typed did not have (Malformed only knows Latin-1 and beyond):
    /// '{' for the 7B of স্তিঁ, 'V' for the half ঙ 56. Digits are fine (30-39 read as the ASCII digits,
    /// which the pipeline's numerals step makes Bengali), and so is ASCII the typed text has.
    /// </summary>
    public static List<string> AsciiLeaks(string got, string typed)
    {
        var bad = new List<string>();
        for (int i = 0; i < got.Length; i++)
            if (got[i] is >= '!' and <= '~' && !char.IsDigit(got[i]) && !typed.Contains(got[i])) bad.Add($"ASCII {got[i]} at {i}");
        return bad;
    }

    /// <summary>
    /// Every cluster of the truth (letters joined by a hasanta, no sign: ক, স্ত, ক্ষ, ঙ্ক্ষ ...) with
    /// every vowel sign (none, া ি ী ু ূ ৃ ে ৈ ো ৌ), every mark after it (none, ঁ ং ঃ) and with and without
    /// a reph - 88 syllables a cluster, most the composer was never asked about - written by the
    /// forward table and read back: the same text (equal, or written with the very same bytes),
    /// well formed, and no ASCII the syllable did not have.
    /// </summary>
    public static string CheckSyllables(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, char ra, out List<string> fails)
    {
        fails = new List<string>();
        var clusterRe = new System.Text.RegularExpressions.Regex($"^(?:[{Rules.Cons}]়?্)*[{Rules.Cons}]়?$");
        var clusters = truth.Select(t => t.U).Where(u => clusterRe.IsMatch(u) && !(u.Length > 2 && u[0] is 'র' or 'ৰ' && IsVirama(u[1])))
                            .Distinct().OrderBy(u => u, StringComparer.Ordinal).ToList();
        string[] vowels = { "", "া", "ি", "ী", "ু", "ূ", "ৃ", "ে", "ৈ", "ো", "ৌ" };
        string[] marks = { "", "ঁ", "ং", "ঃ" };
        string[] rephs = { "", ra + "্" };
        int n = 0, exact = 0, same = 0, skipped = 0;
        foreach (var c in clusters)
            foreach (var v in vowels)
                foreach (var m in marks)
                    foreach (var r in rephs)
                    {
                        string u = r + c + v + m;
                        var w = forward.Convert(u);
                        // (ৱ passes through a Bangla table, reported: its bytes carry it, and it reads back)
                        if (w.UnmappedCharacters.Any(x => x != "ৱ")) { skipped++; continue; }
                        n++;
                        string hex = Hex(w.Text), got = reader.ConvertText(w.Text);
                        var bad = Malformed(got);
                        bad.AddRange(AsciiLeaks(got, u));
                        if (got == u && bad.Count == 0) exact++;
                        else if (Same(got, u, hex, forward) && bad.Count == 0) same++;
                        else fails.Add($"{u}\t{hex}\tread {got} [{Cps(got)}]\t{string.Join("; ", bad)}");
                    }
        return $"syllables (clusters {clusters.Count} x 11 vowel signs x 4 marks x reph or not) {n}: exact {exact}, same text {same}, wrong {fails.Count}; not drawn (a character left out or passed through) {skipped}";
    }

    /// <summary>
    /// Every two-letter cluster of the truth with the ra-phala and ু or ূ (স্ট্রু, ক্ক্রূ): no
    /// pair holds the ু ূ glyphs E6 E9 under a conjunct's ra-phala, so they must be read as signs.
    /// </summary>
    public static string CheckRaPhalaU(FontConverter reader, FontConverter forward, IReadOnlyList<Truth> truth, char ra, out List<string> fails)
    {
        fails = new List<string>();
        int exact = 0, same = 0, n = 0;
        // (not র্ + a letter: that is a reph, not a cluster)
        foreach (var c in truth.Select(t => t.U).Where(u => u.Length == 3 && IsCons(u[0]) && u[0] is not ('র' or 'ৰ') && IsVirama(u[1]) && IsCons(u[2])).Distinct())
            foreach (var sign in new[] { "ু", "ূ" })
            {
                string u = c + "্" + ra + sign;
                var w = forward.Convert(u);
                if (w.HasUnmapped) continue;
                n++;
                string hex = Hex(w.Text), got = reader.ConvertText(w.Text);
                if (got == u) exact++;
                else if (Same(got, u, hex, forward) && Malformed(got).Count == 0) same++;
                else fails.Add($"{hex}\twant {u}\tgot {got} [{Cps(got)}] {string.Join("; ", Malformed(got))}");
            }
        return $"cluster + ra-phala + u/uu {n}: exact {exact}, same text {same}, wrong {fails.Count}";
    }
}
