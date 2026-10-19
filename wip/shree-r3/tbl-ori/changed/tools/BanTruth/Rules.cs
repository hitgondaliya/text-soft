using System.Text;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of Shree-Lipi Bangla as rewrite rules, so a syllable the composer
/// was never asked about is still built the way it builds one: the pre-base signs
/// first, the reph after the letter and its signs, the signs below a letter before its
/// spacer. The learner then only needs pairs for the true exceptions. Rules are written
/// in SHREE-BAN 4/5/6 codes and moved to a family's codes by <see cref="Fam.Code"/>.
/// </summary>
public static class Rules
{
    // ---------- Unicode ----------

    public const string Cons = "ক-হড়ঢ়য়ৰৱ";
    /// <summary>Dependent signs that follow a cluster (the pre-base ones too, before they move).</summary>
    public const string Signs = "ঁ-ঃা-ৌৗৢৣ";
    /// <summary>
    /// The signs a reph is written after: all but ৄ ৢ ৣ, which no face has a glyph for and the tables pass through (P3b) -
    /// Word draws them in a fallback font on a dotted circle of their own, and a reph after one sat on that circle (round 2).
    /// </summary>
    public const string RephSigns = "ঁ-ঃা-ৃে-ৌৗ";
    public const string Vowels = "অ-ঔৠৡ";

    /// <summary>Marks where a reph is written (a noncharacter: never text).</summary>
    public const string RephMark = "\uFDD0";
    /// <summary>Marks the ya-phala B8 (a noncharacter).</summary>
    public const string YaMark = "\uFDD2";
    /// <summary>Marks the i-sign + candrabindu glyph 7B, written before the cluster (a noncharacter).</summary>
    public const string ICandraMark = "\uFDD3";
    /// <summary>Marks the ra-phala on a cluster (a noncharacter); a letter of its own has its own ra-phala glyph.</summary>
    public const string RaMark = "\uFDD4";
    /// <summary>Marks an ai-sign standing on no letter: the composer writes it ED (a noncharacter).</summary>
    public const string AiAloneMark = "\uFDD5";
    /// <summary>Put between the codes of a pair the rules must leave alone (a noncharacter; the last rule removes it).</summary>
    public const string Keep = "\uFDD1";
    /// <summary>Marks the ra-phala F8 of a letter of its own (\u09B0\u09CD\u09B0 in \u0995\u09CD\u09B0\u09CD\u09B0: B9 F8), a noncharacter.</summary>
    public const string RaOwnMark = "\uFDD6";

    /// <summary>Characters an opening quote follows: the start, a space, an opening bracket or quote, a dash.</summary>
    const string Opens = "(?<=^|[\\s(\\[{\u201C\u2018\u2014\u2013-])";

    public static string Protect(string legacy) => string.Join(Keep, legacy.Select(c => c.ToString()));

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>A cluster: letters joined by virama, each with an optional nukta.</summary>
    const string Cluster = "(?:[" + Cons + "]়?্)*[" + Cons + "]়?";

    public static readonly RewriteRule PreBaseRule = R($"({Cluster})([িেৈ])", "$2$1",
        "Pre-base signs ি ে ৈ: written before the cluster (Unicode logical order -> legacy visual order).");

    /// <summary>
    /// The first rule of every table, both ways: the tables mark places with noncharacters (U+FDD0-U+FDDF:
    /// the reph, the ya-phala, ি + ঁ, a kept spelling ...), so one that comes in with the text is never taken
    /// for a mark - it becomes U+FFFD, which no pair takes: passed through and reported.
    /// </summary>
    public static readonly RewriteRule NoMarksIn = R("[" + (char)0xFDD0 + "-" + (char)0xFDDF + "]", "" + (char)0xFFFD,
        "A noncharacter U+FDD0-U+FDDF in the text is not one of the tables' marks: U+FFFD, passed through and reported.");

    public static List<RewriteRule> ForwardPre() => new()
    {
        NoMarksIn,
        R("ৰ", "র", "Assamese ৰ is র in these fonts: one code, B9, draws র in a Bangla font and ৰ in an Assamese one, and the reph " +
          "and the ra-phala are the same glyphs for both. Assamese Unicode spells ra ৰ, so ৰ্ক is the reph on ক (কৰ্ম = কর্ম) and " +
          "প্ৰ the ra-phala (= প্র), never ৰ with a visible hasanta."),
        R("ড়", "ড়", "ড + nukta is ড় (normalised text keeps them apart)."),
        R("ঢ়", "ঢ়", "ঢ + nukta is ঢ়."),
        R("য়", "য়", "য + nukta is য়."),
        // ASCII quotes: their own bytes 22 27 draw অ and ঐ in these fonts; the fonts' quotes are ‘ 91 and ’ 92.
        R(Opens + "\"", "“", "ASCII \" that opens a quote (at the start, after a space, a bracket or a dash) is “ (91 91): byte 22 draws অ."),
        R("\"", "”", "Any other ASCII \" closes a quote: ” (92 92)."),
        R(Opens + "'", "‘", "ASCII ' that opens a quote is ‘ (91): byte 27 draws ঐ."),
        R("'", "’", "Any other ASCII ' (a closing quote, an apostrophe) is ’ (92)."),
        // A ra-phala that carries a hasanta or a ra-phala of its own (ক্র্র, ক্র্): two ra-phalas at one
        // spot draw one. The composer writes the letter with a visible hasanta, then ra as a letter.
        R($"((?!র)[{Cons}]়?)্র্র", "$1্\u200Cর" + RaOwnMark,
          "C্র্র: C with a visible hasanta, then র with its own ra-phala F8 (ক্র্র = 41 F4 A1 B9 F8, as Shree's composer writes ক্ৰ্র). Not after র: র্র্র is the reph on র্র."),
        R($"((?!র)[{Cons}]়?)্র্(?![{Cons}\u200C\u200D])", "$1্\u200Cর্\u200C",
          "C্র্ with no letter after it: C with a visible hasanta, then র with one (ক্র্ = 41 F4 A1 B9 F4, as the composer writes ক্ৰ্)."),
        R("ো", "ো", "ো is ে ... া: split, so the ে can go first."),
        R("ৌ", "ৌ", "ৌ is ে ... ৗ: split, so the ে can go first."),
        R("র\u200D্য", "র\u200C্য", "র + ZWJ + ্য (ra with the ya-phala) is written as the composer writes র\u200C্য."),
        R($"(?<!্)র্(?![\u200C\u200D])({Cluster}্?|[{Vowels}])([{RephSigns}]*)", "$1$2" + RephMark,
          "Reph: র্ before a letter (or vowel) is written after that letter, its cluster and its signs (U+FDD0 marks it for the pairs)."),
        R($"(?<!্)র্(?![\u200C\u200D])", "র্\u200C",
          "র্ that no letter follows (the end of a word): ra with a visible hasanta, B9 F4 - never the reph A2, which waits for its letter."),
        R($"([াু-ৄেৈ])ঁ{RephMark}", "$1" + RephMark + "ঁ",
          "Reph on a letter with a sign and ঁ: the reph before the ঁ (with the bare letter, ি ী or ৗ the ঁ comes first)."),
        R($"({Cluster})িঁ", ICandraMark + "$1", "ি + ঁ: one glyph 7B before the cluster (U+FDD3 marks it)."),
        PreBaseRule,
        R($"ৈ(?![{Cons}়{RephMark}{ICandraMark}])", AiAloneMark, "ৈ on no letter: ED (U+FDD5 marks it); before a letter (moved there) it is EE."),
        R($"(জ)়", "়$1", "Nukta: the dot D8 is written before its letter - on জ, the one letter (but ড় ঢ় য়, above) the composer writes with it " +
          "(জ় = D8 5C). On any other letter, which no spelling of the composer has, it stays after its letter and passes through, " +
          "reported (P3b): moved before the letter it landed on the letter before (খক় read back খ়ক)."),
        R($"(?<=[{Cons}]|র\u200C)্য(?!্)", YaMark, "Ya-phala: B8 after the letter and its spacer (U+FDD2 marks it)."),
        R($"(?<=[{Cons}])্র(?!্)", RaMark, "Ra-phala (U+FDD4 marks it): F6 after a conjunct; a letter of its own has its own glyph in the pairs."),
    };

    // ---------- legacy codes ----------

    /// <summary>Signs a letter carries before its spacer A1 (below it, or the reph and candrabindu above it).</summary>
    public const string BeforeSpacer = "E5 E8 F5 F2 A2 F4 F6";

    /// <summary>Ligatures that end in ট (ক্ট ট্ট ষ্ট স্ট) and প্ত: their spacer stays before every sign.</summary>
    public const string SpacerFirst = "43 6A 9C CA D0";

    /// <summary>A regex character class of legacy codes, in the family's codes.</summary>
    public static string Class(Fam f, string hexList)
    {
        var sb = new StringBuilder("[");
        foreach (var h in hexList.Split(' ', StringSplitOptions.RemoveEmptyEntries)) sb.Append(Lit(f, h));
        return sb.Append(']').ToString();
    }

    /// <summary>One legacy code as a regex literal (in the family's codes).</summary>
    public static string Lit(Fam f, string hex)
    {
        char c = FromHex(f.Code(hex))[0];
        return "\\^$.|?*+()[]{}-/>".Contains(c) ? "\\" + c : c.ToString();
    }

    /// <summary>One legacy code in a replacement (only '$' is special there).</summary>
    public static string Rep(Fam f, string hex)
    {
        char c = FromHex(f.Code(hex))[0];
        return c == '$' ? "$$" : c.ToString();
    }

    public static List<RewriteRule> ForwardPost(Fam f)
    {
        string sp = Lit(f, "A1"), reph = Lit(f, "A2");
        return new()
        {
            R($"(?<!{Class(f, SpacerFirst)})({sp}|{Lit(f, "FE")})({Class(f, BeforeSpacer)}+)", "$2$1", "Signs below a letter, ঁ, the reph and the hasanta go before its spacer (A1, or FE after ষ্ক স্ক)."),
            R($"({Class(f, "97 AB F6 F9")}){Lit(f, "E5")}", "$1" + Rep(f, "E6"), "ু under a subscript ্ন ্ব or the ra-phala F6/F9: E6."),
            R($"({Class(f, "97 AB F6 F9")}){Lit(f, "E8")}", "$1" + Rep(f, "E9"), "ূ under a subscript ্ন ্ব or the ra-phala F6/F9: E9."),
            R($"({sp})({Class(f, "7D 40")}){reph}", Rep(f, "A2") + "$1$2", "Reph on a letter with ং/ঃ: before the spacer, the sign after it."),
            R(Keep, "", "U+FDD1 in a pair keeps the rules above off a spelling the composer writes its own way; it is not text."),
        };
    }
}
