using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of Shree-Lipi 7 Gurmukhi as rewrite rules on the Unicode text, so a syllable the composer
/// was never asked about is still built the way it builds one (every rule is read off pun7-groundtruth.json):
/// the i-sign first, the spacer BE after ਠ and ਨ with the marks that sit on top of the letter before it and
/// ੁ ੂ after it as AE AF, the far addak A8 after a mark that reaches the top, the bindi A4 after ਉ ਊ, a tippi
/// struck after a vowel sign written as bindi. The rules work on Unicode (never on the legacy codes), so ASCII
/// passing through - English text next to Gurmukhi - is never taken for a sign. Places the rules decide are
/// marked with noncharacters, which the pairs turn into codes. The learner then needs pairs only for the true
/// exceptions.
/// </summary>
public static class Rules
{
    public const string Cons = "\u0A15-\u0A28\u0A2A-\u0A30\u0A32\u0A33\u0A35\u0A36\u0A38\u0A39\u0A59-\u0A5C\u0A5E";
    /// <summary>The independent vowels and carriers that take the i-sign in front of them (ਅਿ ੲਿ ੳਿ ਓਿ are composer rows).</summary>
    public const string Bases = "ਅੲੳਓ";
    /// <summary>The marks that sit on top of the letter: on ਠ / ਨ they go before its spacer.</summary>
    public const string Top = "ਂੰੱੇੈੋੌ";

    /// <summary>The spacer BE after ਠ and ਨ (a noncharacter).</summary>
    public const string Spacer = "﷘";
    /// <summary>ੁ after the spacer: AE (a noncharacter).</summary>
    public const string ULow = "﷙";
    /// <summary>ੂ after the spacer: AF (a noncharacter).</summary>
    public const string UuLow = "﷚";
    /// <summary>The addak further right, A8, after a mark that reaches the top (a noncharacter).</summary>
    public const string FarAddak = "﷛";
    /// <summary>The bindi A4 to the right of ਉ ਊ (a noncharacter).</summary>
    public const string RightBindi = "﷜";
    /// <summary>Put between the codes of a pair the rules must leave alone, and the output of what draws nothing (a noncharacter; the last rule removes it).</summary>
    public const string Keep = "﷑";

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    /// <summary>A cluster: letters joined by virama (a joiner may follow the virama), each with an optional nukta.</summary>
    const string Cluster = "(?:[" + Cons + "]਼?੍[‌‍]?)*[" + Cons + "]਼?";

    /// <summary>Characters an opening quote follows: the start, a space, an opening bracket or quote, a dash.</summary>
    const string Opens = "(?<=^|[\\s(\\[{“‘—–-])";

    public static List<RewriteRule> ForwardPre() => new()
    {
        R("\u0A16\u0A3C", "\u0A59", "U+0A16 + nukta U+0A3C is U+0A59: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        R("\u0A17\u0A3C", "\u0A5A", "U+0A17 + nukta U+0A3C is U+0A5A: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        R("\u0A1C\u0A3C", "\u0A5B", "U+0A1C + nukta U+0A3C is U+0A5B: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        R("\u0A2B\u0A3C", "\u0A5E", "U+0A2B + nukta U+0A3C is U+0A5E: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        R("\u0A32\u0A3C", "\u0A33", "U+0A32 + nukta U+0A3C is U+0A33: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        R("\u0A38\u0A3C", "\u0A36", "U+0A38 + nukta U+0A3C is U+0A36: normalised text keeps them apart (the nukta letters are composition exclusions); the composer writes the precomposed letter, one code."),
        // ASCII quotes: their own bytes 22 27 draw the fonts' ‘ and ’, a single quote each.
        R(Opens + "\"", "“", "ASCII \" that opens a quote (at the start, after a space, a bracket or a dash) is “, two ‘ (22 22)."),
        R("\"", "”", "Any other ASCII \" closes a quote: ”, two ’ (27 27)."),
        R(Opens + "'", "‘", "ASCII ' that opens a quote is ‘ (22)."),
        R("'", "’", "Any other ASCII ' (a closing quote, an apostrophe) is ’ (27)."),
        R("ਆ", "ਅਾ", "ਆ is ਅ + ਾ (41 70), ਐ ਅ + ੈ, ਔ ਅ + ੌ: split, so the signs' own rules (a tippi written as bindi, the bindi ligatures, the far addak) apply."),
        R("ਐ", "ਅੈ", "ਐ = ਅ + ੈ (41 B1)."),
        R("ਔ", "ਅੌ", "ਔ = ਅ + ੌ (41 A1)."),
        R("ਇ", "ੲਿ", "ਇ = ੲ + ਿ, written 71 42 (the i-sign first; the code E4 that types ਇ writes nothing in the 7 composer)."),
        R("ਈ", "ੲੀ", "ਈ = ੲ + ੀ (42 72)."),
        R("ਏ", "ੲੇ", "ਏ = ੲ + ੇ (42 79)."),
        R("ਁ", "ੱਂ", "ਁ (adak bindi) is the addak and the bindi, A7 A3: the Unicode composer writes ੱ + ਂ as U+0A01."),
        R("([ਾੀੇੈੋੌਉਊੳ])ੰ", "$1ਂ", "A tippi after ਾ ੀ ੇ ੈ ੋ ੌ ਉ ਊ ੳ is written as the bindi, as both of Shree's composers write it."),
        R($"({Cluster}|[{Bases}])ਿ", "ਿ$1", "The i-sign ਿ is written before its cluster (Unicode logical order -> legacy visual order); ਅ ੲ ੳ ਓ take it in front too."),
        R("([ਠਨ]਼?)", "$1" + Spacer, "ਠ and ਨ are followed by the spacer BE (U+FDD8 marks it)."),
        R($"([ੀੇੈੋੌਂੰਉਊੳ])ੱ", "$1" + FarAddak, "The addak after a mark that reaches the top (ੀ ੇ ੈ ੋ ੌ ਂ ੰ) and after ਉ ਊ ੳ is A8, further right (U+FDDB marks it); else A7."),
        R($"(?<=[ਉਊ]{FarAddak}?[ਂ{RightBindi}]*)ਂ", RightBindi, "The bindi after ਉ ਊ is A4, to the right of the curl (U+FDDC marks it): ਉਂ = 45 A4, ਉਁ = 45 A8 A4, ਉਂਂ = 45 A4 A4."),
        R($"{Spacer}((?:੍[ਰਹਵ])?[{Top}]+)ੁ", Spacer + "$1" + ULow, "ੁ typed after a mark on top of ਠ/ਨ (ਨੰੁ): AE (U+FDD9) - the composer's answer for that order (ਨੰੂ = B2 A5 BE AF, where ਨੂੰ is B2 A5 BE 76)."),
        R($"{Spacer}((?:੍[ਰਹਵ])?[{Top}]+)ੂ", Spacer + "$1" + UuLow, "ੂ there: AF (U+FDDA)."),
        R($"{Spacer}((?:੍[ਰਹਵ])?)ੁ(?![{Top}])", Spacer + "$1" + ULow, "ੁ after ਠ/ਨ's spacer (and a subjoined ੍ਰ ੍ਹ ੍ਵ) with no mark on top: AE (U+FDD9)."),
        R($"{Spacer}((?:੍[ਰਹਵ])?)ੂ(?![{Top}])", Spacer + "$1" + UuLow, "ੂ there: AF (U+FDDA)."),
        R($"{Spacer}((?:੍[ਰਹਵ])?[ੁੂ]?)([{Top}]+)", "$2" + Spacer + "$1",
          "The marks on top of ਠ/ਨ (ੇ ੈ ੋ ੌ ਂ ੰ and the near addak) go before its spacer; ਾ ੀ, the halant, ੍ਯ and ਃ stay after it, and ੁ ੂ (then 74 76) and ੍ਰ ੍ਹ ੍ਵ go after it."),
    };

    public static List<RewriteRule> ForwardPost() => new()
    {
        R(Keep, "", "U+FDD1 marks what draws nothing (a joiner, the virama of a link) and keeps a pair's codes apart; it is not text."),
    };
}
