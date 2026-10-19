using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of Shree-Lipi 7 Odia as rewrite rules on the Unicode text, so a syllable the composer was never
/// asked about is still built the way it builds one (every rule is read off ori7-groundtruth.json, ori7-patterns.txt):
/// the reph ର୍ written after its syllable - the cluster and its vowel signs - and before ଂ ଁ ଃ (ର୍କା = 4C FF E6 F6); the
/// e-sign written before its cluster, and ୈ ୋ ୌ split into that e-sign and the length mark or ା after the cluster
/// (କୋ = 7B 4C FF E6, ର୍କୈ = 7B 4C FF F1). The rules work on Unicode (never on the legacy codes), so ASCII passing
/// through - English next to Odia - is never taken for a sign. Places the rules decide are marked with noncharacters,
/// which the pairs turn into codes. The learner then needs pairs only for the true exceptions.
/// </summary>
public static class Rules
{
    /// <summary>The consonants (ଵ, which no face draws, is not one here: it passes through).</summary>
    public const string Cons = "କ-ନପ-ରଲଳଶ-ହ\u0B5C\u0B5Dୟୱ";

    /// <summary>
    /// The signs a reph is written after: every vowel sign but ୄ ୢ ୣ, which no face has a glyph for and the tables pass
    /// through (P3b) - Word draws them in a fallback font, and a reph after one would sit on that.
    /// </summary>
    public const string RephSigns = "ା-ୃେୈୋୌୖୗ";

    /// <summary>Marks where the reph F6 is written (a noncharacter).</summary>
    public const string Reph = "\uFDD0";
    /// <summary>Put between the codes of a pair the rules must leave alone, and the output of what draws nothing (a noncharacter; the last rule removes it).</summary>
    public const string Keep = "\uFDD1";
    /// <summary>Marks the e-sign 7B, written before its cluster (a noncharacter).</summary>
    public const string ESign = "\uFDD2";

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>A cluster: letters joined by virama, each with an optional nukta (no joiner: ୍‌ ends a cluster).</summary>
    public const string Cluster = "(?:[" + Cons + "]\u0B3C?୍)*[" + Cons + "]\u0B3C?";

    /// <summary>Characters an opening quote follows: the start, a space, an opening bracket or quote, a dash.</summary>
    const string Opens = "(?<=^|[\\s(\\[{“‘—–-])";

    /// <summary>
    /// The tables' marks are noncharacters (U+FDD0-U+FDDF): one already in the text would be taken for a mark (the reph,
    /// the e-sign ...). It becomes U+FFFD, which no pair takes: passed through and reported (as tools/BanTruth does).
    /// </summary>
    public static readonly RewriteRule NoMarksIn = R("[" + (char)0xFDD0 + "-" + (char)0xFDDF + "]", "" + (char)0xFFFD,
        "A noncharacter U+FDD0-U+FDDF in the text is not one of the tables' marks: U+FFFD, passed through and reported.");

    /// <summary>The reph: ର୍ that starts a cluster goes after the cluster and its vowel signs (U+FDD0 marks it), before ଂ ଁ ଃ.</summary>
    public static readonly RewriteRule RephRule = R($"(?<![{Cons}]\u0B3C?୍)ର୍([{Cons}]\u0B3C?(?:୍[{Cons}]\u0B3C?)*|[{Vowels}])([{RephSigns}]*)", "$1$2" + Reph,
        "The reph ର୍ (ra + virama starting a cluster) is written F6 after its syllable - the cluster and its vowel signs - and before ଂ ଁ ଃ (ର୍କା = 4C FF E6 F6; U+FDD0 marks it); on an independent vowel too, as the composer writes it (ର୍ଅ = 41 F6). Not after a virama: there ର belongs to the cluster (a ra-phala).");

    /// <summary>The independent vowels.</summary>
    public const string Vowels = "ଅ-ଌଏଐଓଔୠୡ";

    public static List<RewriteRule> EMoves() => new()
    {
        R($"({Cluster})େ", ESign + "$1", "The e-sign େ is written 7B before its cluster (U+FDD2 marks it; କ୍ରେ = 7B 4C FF F7)."),
        R($"({Cluster})ୈ", ESign + "$1ୖ", "ୈ = the e-sign before the cluster and the length mark ୖ F0 after it (କୈ = 7B 4C FF F0)."),
        R($"({Cluster})ୋ", ESign + "$1ା", "ୋ = the e-sign before the cluster and ା E6 after it (କୋ = 7B 4C FF E6)."),
        R($"({Cluster})ୌ", ESign + "$1ୗ", "ୌ = the e-sign before the cluster and the length mark ୗ F2 after it (କୌ = 7B 4C FF F2)."),
    };

    public static List<RewriteRule> ForwardPre()
    {
        var list = new List<RewriteRule>
        {
            NoMarksIn,
            R("ଡ\u0B3C", "\u0B5C", "ଡ + nukta is ଡ\u0B3C (normalised text keeps them apart: ଡ\u0B3C is a composition exclusion); the composer writes the letter, DD."),
            R("ଢ\u0B3C", "\u0B5D", "ଢ + nukta is ଢ\u0B3C (DE)."),
            R("\u0B47\u0B56", "\u0B48", "େ + ୖ is ୈ (canonically equivalent)."),
            R("\u0B47\u0B3E", "\u0B4B", "େ + ା is ୋ (canonically equivalent)."),
            R("\u0B47\u0B57", "\u0B4C", "େ + ୗ is ୌ (canonically equivalent)."),
            // ASCII quotes: their own bytes 22 27 draw the fonts' quotes ‘ and ’, a single quote each.
            R(Opens + "\"", "“", "ASCII \" that opens a quote (at the start, after a space, a bracket or a dash) is “, two ‘ (22 22)."),
            R("\"", "”", "Any other ASCII \" closes a quote: ”, two ’ (27 27)."),
            R(Opens + "'", "‘", "ASCII ' that opens a quote is ‘ (22)."),
            R("'", "’", "Any other ASCII ' (a closing quote, an apostrophe) is ’ (27)."),
            RephRule,
            R($"(?<=[{Cons}]\u0B3C?)୍ୱ", "୍ବ",
              "ୱ after a virama (the wa-phala, ସ୍ୱ) has no Shree spelling (the composer drops ୱ): it is written as ୍ବ, the ba-phala stroke B4, " +
              "which is the glyph the composer writes for C + link + ବ and the shape Nirmala UI draws for both ୍ୱ and ୍ବ (r3\\tbl-ori\\round1\\sheet2-00.png)."),
            R($"(?<=[{Cons}]\u0B3C?)୍ଯ", "୍ୟ",
              "The ya-phala typed with ଯ (U+0B2F) is written as Shree's ya-phala ୍ୟ (U+0B5F, BF): Nirmala UI and Kalinga draw କ୍ଯ and କ୍ୟ alike (width 113.4 at 100 px, measured), " +
              "and the composer's own C + link + ଯ answer is a visible virama and a full ଯ (FA BE)."),
        };
        list.AddRange(EMoves());
        // after the e-sign moves: ଖ୍କ୍ଷେ is one cluster, its e-sign in front of it all (7B 51 FA E4)
        list.Add(R($"(?<=[{Cons}]\u0B3C?୍)(?=କ୍ଷ)", Keep,
              "A letter + virama before କ୍ଷ keeps a visible virama, and କ୍ଷ is its ligature E4 (ଖ୍କ୍ଷ = 51 FA E4, କ୍କ୍ଷ = 4C FF FA E4, ଳ୍କ୍ଷ = C1 FF FA E4: " +
              "the composer's spelling for every letter but ଙ, whose ଙ୍କ୍ଷ is a ligature of its own, 5F FF): U+FDD1 keeps ୍କ from being read as the subjoined କ."));
        return list;
    }

    public static List<RewriteRule> ForwardPost() => new()
    {
        R(Keep, "", "U+FDD1 marks what draws nothing (a joiner) and keeps a pair's codes apart; it is not text."),
    };

    /// <summary>A staged key, readable: the rules' marks shown by name.</summary>
    public static string Show(string key) => key.Replace(Reph, "<R>").Replace(ESign, "<E>").Replace(Keep, "<K>");
}
