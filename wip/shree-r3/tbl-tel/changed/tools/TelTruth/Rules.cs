using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of Shree-Lipi 7 Telugu as rewrite rules on the Unicode text, so a syllable the composer was never
/// asked about is still built the way it builds one (every rule is read off tel7-groundtruth.json, tel7-patterns.txt):
/// <list type="bullet">
/// <item>a vowel sign on a cluster shapes its first letter, and the subjoined letters (vattu) follow: C1 + sign + vattu
///   (క్కి = కి 4D EC FC + ్క 50); ృ stays after the vattu with the spacer A0 before it (క్కృ = 4D E6 FC 50 A0 96);</item>
/// <item>ై is ె + the length mark \u0C56 (its canonical decomposition): (C1 + ె) + 4F + vattu, 95 in place of 4F before
///   the wide vattu of ఖ గ ఘ ఙ జ ఞ ట ఠ డ ఢ ణ త థ ద ధ ల ష;</item>
/// <item>the ra-vattu ్ర is a hook BEFORE its cluster (క్ర = 7B 4D E6 FC, క్క్ర = 7B 4D E6 FC 50), the deeper hook 5B
///   when the syllable has ై (క్రై = 5B 4D F0 FC 4F); స్త్ర and ష్ట్ర take the combined glyphs 88 and 89 (E7 DC 88);</item>
/// <item>క్ష is one letter, 84 E6 FC (its vattu follows a spacer, A0 or 9B by the vattu, when it has no sign of its own or
///   ై: క్ష్క = 84 E6 FC A0 50); క్ష్మ is క + 87;</item>
/// <item>a virama that has no consonant after it is the visible halant (the composer's own spelling of C + ్ + ZWNJ:
///   క్\u200C = 4D 8A FC) - at a word end too (tools/Shared/ShreeWordFinal.cs); on a cluster it shapes the first letter, as a
///   sign does; ఝ has no subjoined form in any face, so a virama before it is the visible halant too.</item>
/// </list>
/// The rules work on Unicode (never on the legacy codes), so ASCII passing through - English next to Telugu - is never
/// taken for a sign. Places the rules decide are marked with noncharacters, which the pairs turn into codes. The learner
/// then needs pairs only for the letter-specific shapes (gunintham, the halant form) and the true exceptions.
/// </summary>
public static class Rules
{
    /// <summary>The consonants the fonts draw (క-హ but U+0C29 and ఴ) and the marker of క్ష.</summary>
    public const string Cons = "క-నప-ళవ-హ" + K;

    /// <summary>Put between the codes of a pair the rules must leave alone, and the output of what draws nothing (a noncharacter; the last rule removes it).</summary>
    public const string Keep = "\uFDD1";
    /// <summary>క్ష, one letter (84 E6 FC).</summary>
    public const string K = "\uFDD2";
    /// <summary>The ra-vattu hook 7B, before its cluster.</summary>
    public const string Ra = "\uFDD3";
    /// <summary>The deeper ra-vattu hook 5B, before a cluster with ై.</summary>
    public const string RaDeep = "\uFDD4";
    /// <summary>్త్ర after స: 88.</summary>
    public const string TaRa = "\uFDD5";
    /// <summary>్ట్ర after ష: 89.</summary>
    public const string TtaRa = "\uFDD6";
    /// <summary>్ష్మ after క: 87.</summary>
    public const string SsaMa = "\uFDD7";
    /// <summary>The ై mark 95 before a wide vattu.</summary>
    public const string AiWide = "\uFDD8";
    /// <summary>The spacer A0.</summary>
    public const string SpaceA0 = "\uFDD9";
    /// <summary>The wide spacer 9B (a blank glyph, advance 269/1000).</summary>
    public const string Space9B = "\uFDDA";

    /// <summary>The marks the rules place (each a pair of its own).</summary>
    public static readonly (string Mark, string Hex, string What)[] Marks =
    {
        (K, "84 E6 FC", "క్ష"), (Ra, "7B", "the ra-vattu hook"), (RaDeep, "5B", "the deeper ra-vattu hook (with ై)"),
        (TaRa, "88", "్త్ర after స"), (TtaRa, "89", "్ట్ర after ష"), (SsaMa, "87", "్ష్మ after క"),
        (AiWide, "95", "the ై mark before a wide vattu"), (SpaceA0, "A0", "the spacer A0"), (Space9B, "9B", "the wide spacer 9B"),
    };

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>A cluster: letters joined by virama (and the combined vattu marks).</summary>
    public const string Cluster = "[" + Cons + "](?:్[" + Cons + "]|[" + SsaMa + TaRa + TtaRa + "])*";
    /// <summary>A subjoined letter: virama + consonant, or one of the combined vattu marks.</summary>
    public const string Vattu = "(?:్[" + Cons + "]|[" + SsaMa + TaRa + TtaRa + "])";
    /// <summary>The vowel signs that shape the first letter of a cluster (all but ృ ౄ), and ై's length mark.</summary>
    public const string Signs = "[ా-ూెేొ-ౌ\u0C56]";
    /// <summary>The vattu that take the wide ై mark 95 and the wide spacer 9B after క్ష.</summary>
    public const string Wide = "ఖగఘఙజఞటఠడఢణతథదధలష";

    /// <summary>Characters an opening quote follows: the start, a space, an opening bracket or quote, a dash.</summary>
    const string Opens = "(?<=^|[\\s(\\[{“‘—–-])";

    /// <summary>
    /// The tables' marks are noncharacters (U+FDD1-U+FDDA): one already in the text would be taken for a mark. It becomes
    /// U+FFFD, which no pair takes: passed through and reported (as tools/BanTruth and tools/PunTruth do).
    /// </summary>
    public static readonly RewriteRule NoMarksIn = R("[\uFDD0-\uFDDF]", "\uFFFD",
        "A noncharacter U+FDD0-U+FDDF in the text is not one of the tables' marks: U+FFFD, passed through and reported.");

    public static List<RewriteRule> ForwardPre() => new()
    {
        NoMarksIn,
        R("ై", "ె\u0C56", "ై is ె + \u0C56 (its canonical decomposition): the composer writes the e-shape of the letter and the length mark 4F (కై = 4D F0 FC 4F)."),
        R("క్ష్మ(?!ె\u0C56)", "క" + SsaMa, "క్ష్మ is క + the combined vattu 87 (క్ష్మి = 4D EC FC 87); with ై it is క్ష + ్మ (84 F0 FC 95 C3)."),
        R("(?<!్)క్ష", K, "క్ష is one letter: 84 E6 FC (U+FDD2 marks it)."),
        R("్\u200D", "్\u200C", "A virama before a ZWJ: the fonts have no half forms; drawn as the visible halant, as before a ZWNJ."),
        R("్(?=ఝ)", "్\u200C", "No face has a subjoined ఝ (the composer drops ఝ after the link): the visible halant of the letter before it, then ఝ in full."),
        R("స్త్ర", "స" + TaRa, "స్త్ర: స + the combined ్త్ర glyph 88 (E7 DC 88), no hook."),
        R("ష్ట్ర", "ష" + TtaRa, "ష్ట్ర: ష + the combined ్ట్ర glyph 89 (E7 D9 89), no hook."),
        R("్(?![" + Cons + "\u200C])", "్\u200C",
          "A virama with no consonant after it (at a word end, before a space, punctuation or a sign) is the visible halant: the composer's spelling of C + ్ + ZWNJ (క్\u200C = 4D 8A FC). Its pending link writes the bare letter, losing the virama (tools/Shared/ShreeWordFinal.cs)."),
        R("(" + Cluster + ")్ర(" + Vattu + "*ె\u0C56)", RaDeep + "$1$2",
          "The ra-vattu with ై: the deeper hook 5B before the cluster (క్రై = 5B 4D F0 FC 4F)."),
        R("(" + Cluster + ")్ర", Ra + "$1",
          "The ra-vattu is the hook 7B BEFORE its cluster, where the composer writes it (క్ర = 7B 4D E6 FC, క్క్ర = 7B 4D E6 FC 50; U+FDD3 marks it)."),
        R("(" + Cluster + ")్ర", Ra + "$1", "Once more, for a second ra-vattu in one cluster (not well-formed text): a hook too, so no virama is left over."),
        R("్ర(?=" + Cluster + Vattu + "*ె\u0C56)", RaDeep,
          "A ra-vattu with no letter before it - struck first, before its letter, the vendor keyboards' order (TYPEWRIT K, DOE #, CRTRONIC T then the letter) - is the hook alone, where Shree writes D7 alone: the deeper 5B before a letter with ై ..."),
        R("్ర", Ra, "... else 7B. Followed by its letter these are Shree's own bytes (్రక = 7B 4D E6 FC, what the composer writes for క్ర)."),
        R("([" + Cons + "])(" + Vattu + "+)((?:" + Signs + "|్\u200C)+)", "$1$3$2",
          "A vowel sign (or the visible halant) on a cluster shapes its first letter; the subjoined letters follow (క్కి = 4D EC FC 50). ృ stays after them."),
        R("\u0C56(?=్[" + Wide + "])", AiWide, "The ై mark is 95 before the wide vattu of ఖ గ ఘ ఙ జ ఞ ట ఠ డ ఢ ణ త థ ద ధ ల ష (ఖ్ఖై = 52 F1 FD 95 55)."),
        R("(?<=" + K + "(?:ె(?:\u0C56|" + AiWide + "))?)(?=్[కచఛనపఫబభయళవశస])", SpaceA0,
          "After క్ష with no sign of its own (or with ై) a narrow vattu follows the spacer A0 (క్ష్క = 84 E6 FC A0 50, క్ష్కై = 84 F0 FC 95 A0 50; క్ష్కి = 84 EC FC 50)."),
        R("(?<=" + K + "(?:[ా-ూెేౌ]|ె(?:\u0C56|" + AiWide + "))?)(?=్[" + Wide + "ఱహ])", Space9B,
          "After క్ష (with any sign but ొ ో) a wide vattu follows the wide spacer 9B (క్ష్ఖ = 84 E6 FC 9B 55, క్ష్ఖి = 84 EC FC 9B 55; క్ష్ఖొ = 84 F6 55)."),
        R("(?<=్[" + Cons + "]|[" + SsaMa + TaRa + TtaRa + K + "])(?=[ృౄ])", SpaceA0,
          "ృ after a vattu, or after క్ష: the spacer A0 before it (క్కృ = 4D E6 FC 50 A0 96, క్షృ = 84 E6 FC A0 96)."),
        // ASCII quotes: their own bytes 22 27 draw the fonts' ‘ and ’, a single quote each.
        R(Opens + "\"", "“", "ASCII \" that opens a quote (at the start, after a space, a bracket or a dash) is “, two ‘ (22 22)."),
        R("\"", "”", "Any other ASCII \" closes a quote: ”, two ’ (27 27)."),
        R(Opens + "'", "‘", "ASCII ' that opens a quote is ‘ (22)."),
        R("'", "’", "Any other ASCII ' (a closing quote, an apostrophe) is ’ (27)."),
    };

    public static List<RewriteRule> ForwardPost() => new()
    {
        R(Keep, "", "U+FDD1 marks what draws nothing (a joiner) and keeps a pair's codes apart; it is not text."),
    };
}
