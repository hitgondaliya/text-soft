using FontSoftware.Core.Models;
using static Common;
using static Dev7;

/// <summary>
/// Shree-Lipi 7 Sanskrit (SHREE-SAN7-*, S09xx892.TTF): an encoding of its own. Most
/// letters are a body and a stem (ख = 41 22, ग = 42 70; the half letter is the body
/// alone), क फ and the vowels ऊ ऋ ॠ take the spacer A1 and ङ छ ट ठ ड ढ the spacer 3E,
/// with the signs below and above a letter (and the reph) before A1. It has a na-sign
/// (्न = 6E) and no nukta. The byte classes below come from what the Sanskrit composer
/// writes for single letters and ligatures (san7-groundtruth.json).
/// </summary>
public static class San7
{
    /// <summary>Signs drawn below a letter (before its spacer A1 or 3E). The vocalic-l signs A7 6B stay after it.</summary>
    public const string Below = "73 74 77 25 F8 6E 6F";

    /// <summary>Signs drawn above a letter (before the spacer A1, after 3E).</summary>
    public const string Above = "75 76 78 79 7A";

    /// <summary>ट ठ ड ढ (and ङ छ ळ): the round-bottomed letters take the ra-sign 7E.</summary>
    public const string Round = "44 46 4A 4B 4C 4D";

    /// <summary>द ह, their ligatures and the stacked ligatures with no stem: u 6C, uu 6D.</summary>
    public const string UHook = "51 60 E7 E3 E6 A2 8A 8B 8C FB FE 27 99 CB CC 9A 9B 89 9C FF AF A9 AB A8 AC 9F E4 84 86 87 BD 88 95 B1";

    /// <summary>Signs that follow a letter in the bytes (and so sit between a letter and its reph).</summary>
    public const string SignsAfter = "22 70 72 73 74 75 76 77 25 78 79 7A EB 94 F8 7E F7 6E 6C 6D A7 6B 5D 7D";

    static (string Bases, string Stemmed, string Halves)? _classes;

    /// <summary>
    /// Bases: the codes of single letters and ligatures (alone or with their stem/spacer).
    /// Stemmed: codes whose letter is the code and a stem 22/70 - alone, the code is the half letter.
    /// Halves: the half letters the composer writes as a code of their own.
    /// </summary>
    public static (string Bases, string Stemmed, string Halves) Classes => _classes ??= ComputeClasses();

    static (string, string, string) ComputeClasses()
    {
        var s = new Script
        {
            Name = "san7", Title = "", CaptureFile = "san7-groundtruth.json", ForwardPre = new(), ForwardPost = new(),
            ReversePre = new(), ReversePost = new(), VisualRules = new(), MarkerAtoms = Array.Empty<(string, string)>(),
            Extra = Array.Empty<(string, string, string)>(), FontCodes = Array.Empty<(string, string)>(), NeverWrite = new(),
            Halant = "6F", Spacers = new[] { "A1", "3E" },
        };
        var truth = s.LoadTruth();
        var units = new HashSet<string> { "C", "C_C", "C_R", "C_C_C", "C_C_R", "C_R_C", "C_C_C_C", "V", "R" };
        var bases = new SortedSet<string>(StringComparer.Ordinal);
        var stemmed = new SortedSet<string>(StringComparer.Ordinal);
        var halves = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var t in truth)
        {
            var b = t.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string shape = Shape(t.U);
            if (units.Contains(shape) && (b.Length == 1 || (b.Length == 2 && b[1] is "22" or "70" or "A1" or "3E")))
            {
                bases.Add(b[0]);
                if (b.Length == 2 && b[1] is "22" or "70" && shape != "V") stemmed.Add(b[0]);   // not आ = अ + ा
            }
            if (shape is "C_" or "C_C_" or "C_R_" && b.Length == 1) halves.Add(b[0]);
        }
        foreach (var x in new[] { "AA", "E2", "E0", "E1", "F3" }) bases.Add(x);   // दृ हृ रु रू, and श्र before ृ
        foreach (var x in new[] { "00", "F2", "71", "7B", "5B", "22", "70", "6E", "F7" }) bases.Remove(x);
        halves.ExceptWith(stemmed);
        return (string.Join(" ", bases), string.Join(" ", stemmed), string.Join(" ", halves));
    }

    public static List<RewriteRule> ForwardPost()
    {
        var (basesHex, stemmedHex, halvesHex) = Classes;
        string below = Class(Below), above = Class(Above), bases = Class(basesHex), stemmed = Class(stemmedHex), halves = Class(halvesHex);
        string half = $"(?:{halves}|{stemmed}(?!{Class("22 70")}))";
        return new()
        {
            R(Soft, "", "U+FDDD stands for a joiner: no glyph, and nothing for the rules below to stop at."),
            R($"({Class(Round)}){Lits("3E F8")}", "$1" + Rep("7E 3E"), "Ra-sign under ङ छ ट ठ ड ढ: 7E, before the spacer (the signs below stay after it)."),
            R($"({Lit("61")}){Lit("F8")}", "$1" + Rep("7E"), "Ra-sign under ळ: 7E."),
            R($"{Lit("A1")}((?:{below}|{above}|{Lit("EA")})+)", "$1" + Rep("A1"), "Signs below and above a letter, and the reph, go before its spacer A1."),
            R($"(?<!{Lit("7E")}){Lit("3E")}({below}+)", "$1" + Rep("3E"), "Signs below a letter go before its spacer 3E."),
            R(Lit("3E") + DropSpacer, "", "Ya-sign after ट ठ ड ढ: in place of the spacer 3E (U+FDDB marks it)."),
            R($"({Class(UHook)}|{Lit("7E")}{Lit("3E")}?){Lit("73")}", "$1" + Rep("6C"), "u after द ह, their ligatures and the stacked ligatures: 6C."),
            R($"({Class(UHook)}|{Lit("7E")}{Lit("3E")}?){Lit("74")}", "$1" + Rep("6D"), "uu there: 6D."),
            R($"({Class("40 55 C8 B1")}{Lit("F8")}?){Lits("A1 72")}", "$1" + Rep("A1 94"), "ii after क फ क्क क्ल (and their ra-sign): 94."),
            R($"{Lit("71")}(?={bases}|{halves}|{stemmed})(?!{Lit("5A")})", Rep("7B"), "i-sign over a letter: 7B (71 over र and alone)."),
            R($"{Lit("7B")}(?=(?>(?:{half}|.{Lit("6F")}{Lit("3E")}?)+)(?!{Class("4A 4B")})|{Class("4A 4B 4C 4D")}{Lit("F7")})", Rep("5B"), "i-sign over a conjunct (not one ending in ट ठ) or over ट ठ ड ढ with the ya-sign: the long 5B."),
            R($"({Class("72 94 75 76")}){Lit("7A")}", "$1" + Rep("EB"), "Anusvara after ii, e, ai: EB."),
            R($"{Class("7A EB")}{Lit("EA")}", Rep("7C"), "Anusvara + reph: one glyph."),
            R($"[{Keep}{DropSpacer}]", "", "U+FDD1 in a pair keeps the rules above off a spelling the composer writes its own way; U+FDDB is spent above. Neither is text."),
        };
    }

    public static List<RewriteRule> ReversePre()
    {
        var (basesHex, stemmedHex, halvesHex) = Classes;
        string halves = Class(halvesHex + " " + stemmedHex), bases = Class(basesHex), signs = Class(SignsAfter);
        string noHalf = "44 4A 4B 4C 4D 51";
        string withHalf = string.Join(" ", basesHex.Split(' ').Where(b => !noHalf.Split(' ').Contains(b)));
        return new()
        {
            R(Lit("7C"), Rep("7A EA"), "7C: the anusvara with a reph - two pieces, so the reph can move."),
            R($"(?<={Class(withHalf)}|{Class("22 70")}){Lit("6F")}", HalantMark, "The halant 6F after a letter that has a half form is a visible virama (्+ZWNJ); U+FDD4 marks it for the pairs."),
            R(Class("A1 3E"), "", "The spacers A1 and 3E draw nothing; they only follow a letter."),
            R($"({halves}|{Lit("6F")})(?={Class("5A E0 E1")})", "$1" + ZWJ, "A half letter before the full र: ्+ZWJ+र (्र alone would be the ra-sign)."),
            R($"((?:(?:{halves}|{bases}{Lit("6F")})*{bases})?{signs}*)(?<!{Lit("F2")}){Lit("EA")}", RephMark + "$1",
                "Reph is written after its syllable; Unicode has र् first - U+FDD0 marks it there for the pairs. (F2 EA is ई, not a reph on इ.)"),
        };
    }

    /// <summary>Codes the composer only writes inside a longer spelling, as the font draws them.</summary>
    public static readonly (string Hex, string Unicode)[] FontCodes =
    {
        ("7B", "ि"), ("5B", "ि"), ("71", "ि"), ("94", "ी"), ("EB", "ं"), ("EA", "र्"), ("DD", "र्" + ZWJ),
        ("F8", "्र"), ("7E", "्र"), ("F7", "्य"), ("6E", "्न"), ("6F", "्"), ("6C", "ु"), ("6D", "ू"),
        ("U+200D", ZWJ), ("U+FDD4", Virama + ZWNJ), ("U+FDD0", "र्"), ("00 75", "ऐ"), ("B7", "•"),
    };

    public static Script Script => _script ??= new Script
    {
        Name = "san7",
        Title = "Shree-Lipi 7.0 Sanskrit",
        CaptureFile = "san7-groundtruth.json",
        ForwardPre = Dev7.ForwardPre(sanskrit: true),
        ForwardPost = ForwardPost(),
        ReversePre = ReversePre(),
        ReversePost = Dev7.ReversePost(),
        VisualRules = Dev7.VisualRules(),
        MarkerAtoms = new[] { (RephMark, "EA"), (YaMark, "F7"), (YaMark2, "F7"), (YaMarkM, "F7"), (YaMark3, "F7"), (YaMarkT, "U+FDDB F7"), (RaMark, "F8"), (NaMark, "6E"), (HalantMark, "6F"), (PendingMark, "") },
        Extra = new (string, string, string)[]
        {
            ("्", "6F", "a virama with no letter: the halant"),
            (ZWNJ, "U+FDDD", "ZWNJ: no glyph (U+FDDD, which the first post rule takes out)"), (ZWJ, "U+FDDD", "ZWJ: no glyph - it asks for the half form, which the pairs give (U+FDDD, which the first post rule takes out)"),
            ("॥", "24 24", "double danda: two dandas (the composer has no code for it)"),
            ("“", "91 91", "no glyph: two single quotes"),
            ("”", "92 92", "no glyph: two single quotes (the composer writes 6E, the na-sign)"),
            ("॒", "85", "the anudatta: byte 85 in the SAN7 fonts, a zero-advance bar under the letter before it (the composer prefers F2, which draws इ, and also accepts 85)"),
            ("ॡ", "", "no glyph (the composer writes nothing)"),
            ("ॆ", "", "no glyph (the composer writes nothing)"),
        },
        FontCodes = FontCodes,
        NeverWrite = new() { "00" },
        Excluded = t => t.U switch
        {
            "”" => "the composer writes 6E, the subscript na-sign (क्न = 40 6E A1), not a quote",
            _ => null,
        },
        Visible = (u, hex) => DrawnAs.TryGetValue(hex, out var d) ? d : u,
        Halant = "6F",
        Spacers = new[] { "A1", "3E" },
        Adjust = (t, raw) => t.Pref.StartsWith("00 75") ? NoNul(t)
            : t.U == "॒" && t.Acc.Contains("85") ? ("85", "the anudatta is byte 85 in the SAN7 fonts, a zero-advance bar under the letter before it, which the composer also accepts; its preferred F2 is the code of इ (इ = F2, ई = F2 EA)")
            : WordFinal.Spell(t, raw, '\U0000094D', '\U0000093C', "6F", new[] { "A1", "3E" }),
    };

    /// <summary>ऐ: the composer writes byte 00 (NUL) and the e-sign 75. The table writes an accepted spelling without the NUL.</summary>
    static (string, string) NoNul(Truth t)
    {
        string? other = t.Acc.FirstOrDefault(a => !a.Split(' ').Contains("00"));
        return other is not null
            ? (other, "the composer writes byte 00 (NUL) and the e-sign 75 for ऐ; a document cannot carry a NUL, so the table writes ए + e-sign (EF 75), which the composer also accepts here")
            : ("EF 75" + t.Pref[5..], "the composer writes byte 00 (NUL) and the e-sign 75 for ऐ; a document cannot carry a NUL, so the table writes ए + e-sign (EF 75) and what follows as the composer writes it - the composer accepts EF 75 for ऐ, ऐं, ऐँ and र्ऐ, not for this spelling");
    }
    static Script? _script;

    /// <summary>Composer spellings that do not draw their text: what the bytes really draw.</summary>
    public static readonly Dictionary<string, string> DrawnAs = new(StringComparer.Ordinal)
    {
        ["40 40 A1 A7"] = "ककॢ", ["54 54 22 A7"] = "पपॢ",   // क्रॢ प्रॢ: the composer doubles the letter and drops the ra-sign
    };
}
