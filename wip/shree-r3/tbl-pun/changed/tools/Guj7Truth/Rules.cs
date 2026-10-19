using System.Text;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of Shree-Lipi 7 Gujarati, as rewrite rules. With them a syllable
/// the composer was never asked about is still built the way the composer builds
/// one: reph after the letter, signs before the spacer, the i-sign glyph by base.
/// The learner then only needs pairs for the true exceptions.
/// </summary>
public static class Rules
{
    // ---------- legacy code classes (hex) ----------

    /// <summary>Zero-width spacer codes that follow some letters.</summary>
    public const string Spacers = "24 3E";

    /// <summary>Signs drawn above the letter (the composer puts them before a spacer).</summary>
    public const string Above = "83 84 A1 A2 A5 A6 B8 B5 AE AF A3 A4 A7 A8";

    /// <summary>Signs drawn below the letter.</summary>
    public const string Below = "79 7C A9 AB BA 9F 7A 77 B3 B4 B0 B2";

    /// <summary>Letters whose spacer stays before the signs above them (છ જ દ જ્ર).</summary>
    public const string TallTop = "52 53 76";

    /// <summary>Independent vowels (a reph can sit on one, but it never ends a cluster).</summary>
    public const string VowelCodes = "41 42 43 44 45 46 47 48 49 4A 4B";

    /// <summary>Letters with no half form: in a cluster they keep a visible virama (ઙ છ ટ ઠ ડ ઢ દ).</summary>
    public const string NoHalf = "50 52 56 57 58 59 76";

    /// <summary>
    /// Letters (and the ligatures ક્ષ ત્ત ત્ર શ્ર) that have a half form: the composer writes them + virama + ZWJ as one
    /// half code (truth rows C્‍). A visible virama after one of these inside a word was typed as a halant (virama + ZWNJ).
    /// </summary>
    public const string HasHalf = "4C 4D 4E 4F 51 53 54 55 5A 5C 5E 5F 60 61 62 63 64 65 67 68 69 6A 6B 6C 6D 6E 73 D1 D3 EE";

    /// <summary>Letters a following va joins as the va-sign 99.</summary>
    public const string VaSignCons = "50 52 56 57 58 59 76";

    /// <summary>Letters that take the u/uu variants BA/9F.</summary>
    public const string UVariant = "61 6C D6 E4 FA";

    /// <summary>Letters the narrow i-sign 71 goes with (all others take 72).</summary>
    public const string INarrow = "4C 50 56 57 58 59 76 61 66 BE D6 E4";

    /// <summary>Half letters (a following letter makes a cluster).</summary>
    public const string Halves = "BC BF C1 C2 C3 C4 C8 CA CE D0 D5 DD DE E0 E3 E5 E6 E7 E8 EB EC ED F3 F5 F8 FD FF D2 D4 EF";

    /// <summary>Full letters and ligatures (a base a sign can sit on).</summary>
    public const string Bases =
        "41 42 43 44 45 46 47 48 49 4A 4B 4C 4D 4E 4F 50 51 52 53 54 55 56 57 58 59 5A 5C 5E 5F 60 61 62 63 64 65 66 67 68 69 6A 6B 6C 6D 6E 6F " +
        "73 76 78 7B 7E 82 86 87 88 89 8A 8B 8C 95 96 98 9A 9B AA AC BD BE C0 C5 C6 C7 C9 CB CC CD CF D1 D3 D6 D7 D8 D9 DA DB DC DF E1 E2 E4 EA EE F0 F1 F2 F4 F6 F7 F9 FA FB FC";

    /// <summary>A regex character class of legacy codes.</summary>
    public static string Class(string hexList)
    {
        var sb = new StringBuilder("[");
        foreach (var h in hexList.Split(' ', StringSplitOptions.RemoveEmptyEntries)) sb.Append(Lit(h));
        return sb.Append(']').ToString();
    }

    /// <summary>One legacy code as a regex literal.</summary>
    public static string Lit(string hex)
    {
        char c = FromHex(hex)[0];
        return "\\^$.|?*+()[]{}-/>".Contains(c) ? "\\" + c : c.ToString();
    }

    /// <summary>One legacy code in a replacement (only '$' is special there).</summary>
    public static string Rep(string hex)
    {
        char c = FromHex(hex)[0];
        return c == '$' ? "$$" : c.ToString();
    }

    static RewriteRule R(string pattern, string replace, string note) => new() { Pattern = pattern, Replace = replace, Note = note };

    // ---------- Unicode -> 7 ----------

    const string Cons = "\u0A95-\u0AB9";
    const string Signs = "\u0A81-\u0A83\u0ABE-\u0ACC\u0AE2\u0AE3";
    const string Vowels = "\u0A85-\u0A94\u0AE0\u0AE1";

    /// <summary>Marks where a reph is written: a noncharacter, so it can never be text.</summary>
    public const string RephMark = "\uFDD0";

    /// <summary>Marks a ya written as the ya-sign E9 under the full letter (a noncharacter).</summary>
    public const string YaMark = "﷒";

    /// <summary>Marks a virama with no letter before it, before ન (a noncharacter): the composer writes ્ન alone as ન.</summary>
    public const string LoneVirama = "﷓";

    /// <summary>Letters a bare ya-cluster keeps whole, with the ya-sign under them (not ક જ દ).</summary>
    const string YaSignCons = "ખ-છઝ-થધ-યલ-હ";

    /// <summary>
    /// What a word goes on with after a virama (a regex class body): the letters and signs of
    /// the Gujarati block (not the digits, ૰ or ₹) and the joiners. A virama before anything
    /// else - a space, punctuation, the danda U+0964, a digit - or at the end of the text ends its word.
    /// </summary>
    public const string WordGoesOn = "઀-ૣૹ-૿‌‍" + Waiting;

    /// <summary>
    /// The engine puts this noncharacter after a word that is still being typed (FontConverter.WaitingMark):
    /// a virama before it waits for its next letter, so it is written as the composer shows it while it
    /// waits - the half form, the reph - not with the visible halant of a word that ends there. It writes nothing.
    /// </summary>
    public const string Waiting = "﷯";

    public static List<RewriteRule> ForwardPre() => new()
    {
        R($"્(?![{WordGoesOn}])", "્‌",
          "A virama that ends a word (before a space, punctuation, a danda, a digit, or at the end of the text) is the visible halant: written as ્+ZWNJ, as the composer writes that. " +
          "Every Unicode font draws it so; the half form the composer answers for a bare virama is only what it shows while it waits for the next letter."),
        R($"(?<![{Cons}઼])્(?=ન)", LoneVirama,
          "A virama with no letter before it, before ન: the composer writes ્ન alone as ન 5F (U+FDD3 marks it for that pair). " +
          "After a letter the virama is the letter's own (જ્ઞ્ન = 6F B9 5F, as જ્ઞ્મ = 6F B9 64), never dropped."),
        R($"(?<!\u0ACD)\u0AB0\u0ACD(?!\u0AB0)((?:[{Cons}]\u0ABC?\u0ACD)*[{Cons}]\u0ABC?(?!\u0ACD(?!\u200C))|[{Vowels}])([{Signs}]*)", "$1$2" + RephMark,
          "Reph: ર્ before a letter is written after that letter and its signs (U+FDD0 marks it for the pairs) - " +
          "and before the halant of a virama that ends the word (આર્ત્ = 41 70 73 AE B9: the reph on ત, not on આ). Before ર the composer writes ર and the ra-sign instead."),
        R($"([{YaSignCons}]|(?<=્)ર)્ય(?![{Signs}઼{RephMark}]|્(?!‌))", "$1" + YaMark,
          "A cluster ending in ya with no sign after it (or with the halant of a word-final virama): the full letter and the ya-sign E9 (U+FDD2 marks it for the pairs)."),
        Learner.ISignRule,
        R($"((?:[{Cons}]\u0ABC?\u0ACD)*[{Cons}])\u0ABC", "\u0ABC$1",
          "Nukta: written before its letter (and before a conjunct it ends)."),
    };

    public static List<RewriteRule> ForwardPost()
    {
        string sp = Class(Spacers), above = Class(Above), below = Class(Below), narrow = Class(INarrow);
        string halves = Class(Halves), bases = Class(Bases), tall = Class(TallTop);
        return new()
        {
            R($"({Class(VaSignCons)}){Lit("B9")}({sp}){Lit("68")}", "$1$2" + Rep("99"), "Va after ઙ છ ટ ઠ ડ ઢ દ: the va-sign 99 after the spacer."),
            R($"({Class(UVariant)}){Lit("79")}", "$1" + Rep("BA"), "u after ફ હ દ્ર ફ્ર હ્ર: BA."),
            R($"({Class(UVariant)}){Lit("7C")}", "$1" + Rep("9F"), "uu after ફ હ દ્ર ફ્ર હ્ર: 9F."),
            R($"({sp}){Lit("B0")}", Rep("B2") + "$1", "Ra-sign after a letter with a spacer: B2, before the spacer."),
            R($"({Lit("6D")}){Lit("B0")}", "$1" + Rep("B2"), "Ra-sign under ળ: B2."),
            R($"(?<!(?:{tall}|{Lit("C7")}){below}*)({sp})((?:{above}|{below})+)", "$2$1", "Signs above and below a letter go before its spacer."),
            R($"(?<={tall}{below}*)({sp})({below}+)", "$2$1", "છ જ દ: only the signs below go before the spacer."),
            R($"({sp})((?:{above})*){Lit("B9")}", Rep("B9") + "$1$2",
              "The halant goes before its letter's spacer, as the composer writes ક્‌ = 4C B9 24 - after a reph too (ર્ક્ ending a word = 4C AE B9 24); a tall letter keeps its signs above after the spacer."),
            R($"{Lit("B2")}{Lit("79")}", Rep("B3"), "Ra-sign + u: one glyph."),
            R($"{Lit("B2")}{Lit("7C")}", Rep("B4"), "Ra-sign + uu: one glyph."),
            R($"{Lit("A1")}{Lit("84")}", Rep("A2"), "e + anusvara: one glyph."),
            R($"{Lit("A5")}{Lit("84")}", Rep("A6"), "ai + anusvara: one glyph."),
            R($"{Lit("75")}{Lit("84")}", Rep("5D"), "ii + anusvara: one glyph."),
            R($"{Lit("71")}(?!{narrow})(?={bases})", Rep("72"), "i-sign: the wide glyph 72 except before ક ઙ ટ ઠ ડ ઢ દ ફ ર."),
            R($"{Lit("72")}({bases}{sp}?(?:{Lit("B0")}|{Lit("B2")})?){Lit("84")}", Rep("74") + "$1", "i-sign + anusvara over one letter: 74."),
            R($"[{Lit("71")}{Lit("72")}](?={halves}|.{Lit("B9")}|.{sp}?{Lit("99")})", Rep("5B"), "i-sign over a cluster: the long glyph 5B."),
            R($"{Lit("A1")}{Lit("AE")}", Rep("A3"), "e + reph: one glyph."),
            R($"{Lit("A5")}{Lit("AE")}", Rep("A7"), "ai + reph: one glyph."),
            R($"{Lit("75")}{Lit("AE")}", Rep("7D"), "ii + reph: one glyph."),
            R($"{Lit("5D")}{Lit("AE")}", Rep("85"), "ii + anusvara + reph: one glyph."),
            R($"{Lit("A2")}{Lit("AE")}", Rep("A4"), "e + anusvara + reph: one glyph."),
            R($"{Lit("A6")}{Lit("AE")}", Rep("A8"), "ai + anusvara + reph: one glyph."),
            R($"{Lit("84")}{Lit("AE")}", Rep("AF"), "anusvara + reph: one glyph."),
            R(Keep, "", "U+FDD1 in a pair keeps the rules above off a spelling the composer writes its own way; it is not text."),
        };
    }

    // ---------- 7 -> Unicode ----------

    /// <summary>Signs that follow a letter in the bytes (and so sit between a letter and its reph).</summary>
    public const string SignsAfter = "70 75 5D 79 7C 7A 77 BA 9F A9 AB A1 A2 A5 A6 B8 B5 83 84 BB 3A B0 B2 B3 B4 B6 E9 99 9C";

    /// <summary>
    /// The reverse pre rules of one family. 91 92 93 94 are letters in both - copies of પ થ ન
    /// સ્ત્ર in 7, the ઙ conjuncts ઙ્ક ઙ્ખ ઙ્ગ ઙ્ઘ in 4/5/6 - and a reph or nukta moves past them
    /// as past any letter; B6 is the ્રૂ mark in 7 (a sign) and the conjunct ઙ્ક્ષ in 4/5/6 (a letter).
    /// </summary>
    public static List<RewriteRule> ReversePre(bool fourFiveSix = false)
    {
        string halves = Class(Halves), bases = Class(Bases + (fourFiveSix ? " 91 92 93 94 B6" : " 91 92 93 94"));
        string signs = Class(fourFiveSix ? SignsAfter.Replace(" B6", "") : SignsAfter);
        // Inside a cluster a letter is a half form, or - for the letters with none - the
        // letter and a visible virama. Any other letter + virama ended its syllable.
        string cluster = $"(?:(?:{halves}|{Class(NoHalf)}{Lit("B9")})*{bases}(?<!{Class(VowelCodes)})|{Class(VowelCodes)})";
        var rules = new List<RewriteRule>();
        foreach (var (lig, sign) in new[] { ("7D", "75"), ("A3", "A1"), ("A7", "A5"), ("AF", "84"), ("85", "5D"), ("A4", "A2"), ("A8", "A6") })
            rules.Add(R(Lit(lig), Rep(sign) + Rep("AE"), $"{lig}: the sign {sign} with a reph - two pieces, so the reph can move."));
        rules.Add(R($"{Lit("B9")}({Class(Spacers)})((?:{Class(Above)})*){Lit("AE")}", Rep("AE") + Rep("B9") + "$1$2",
            "A reph on a word-final letter that keeps its signs above after the spacer (ર્છ્ = 52 B9 3E AE) comes after the halant and the spacer: " +
            "put it before the halant, so it moves with the letter as it does in ર્ત્ = 73 AE B9."));
        // Before a letter: the next letter's i-sign glyph or nukta may stand between (they are drawn before it).
        string letterNext = $"(?=(?:{Class(Spacers)})?(?:{Class("71 72 74 5B 9C")})*(?:{bases}|{halves}))";
        rules.Add(R($"(?<=(?:{Class(HasHalf + " 66")})(?:{Class(Above)})*){Lit("B9")}{letterNext}", Rep("B9") + ZwnjMark,
            "A visible halant inside a word (ક્‌ક = 4C B9 24 4C 24, ર્‌ક = 66 B9 4C 24): a letter that has a half form shows its halant only when it was typed so - " +
            "virama + ZWNJ (U+FDD5 marks it for the pairs). The letters with no half form (ઙ છ ટ ઠ ડ ઢ દ જ્ઞ) keep a visible virama in every cluster."));
        rules.Add(R($"({halves})(?!(?:{Class(Spacers)})?(?:{Class("71 72 74 5B 9C")})*(?:{bases}|{halves}))", "$1" + ZwjMark,
            "A half form with no letter after it (BC before a space) is the letter + virama + ZWJ, as the composer writes ક્‍ (U+FDD6 marks it for the pairs) - " +
            "so the next conversion writes the same half form, not the halant."));
        rules.Add(R(Class(Spacers), "", "The spacers 24 and 3E draw nothing; they only follow a letter."));
        rules.Add(R($"{Lit("9C")}({cluster})", "$1" + Rep("9C"), "Nukta is written before its letter; Unicode has it after."));
        // P3b: the vocalic-l letters and signs the tables pass through as themselves (the fonts have no glyph for
        // them) stand where they were typed - after the letter, before the reph (ર્કૢ = 4C 24 U+0AE2 AE): the reph
        // moves past them as past a sign.
        string passed = "[" + string.Concat(Policy.NoGlyph) + "]";
        rules.Add(R($"((?:{cluster})?(?:{signs}|{passed})*){Lit("AE")}", Rep("AE") + "$1",
            "Reph is written after its letter and signs (or after a sign with no letter, as the composer writes ર્ે; and after a vocalic-l letter or sign, which the fonts have no glyph for and the tables pass through); Unicode has ર્ first."));
        return rules;
    }

    public static List<RewriteRule> ReversePost() => new()
    {
        R($"([{Cons}])((?:્[યરવ])?[ઁ-ઃા-ૌ]*)઼", "$1઼$2",
          "Nukta: right after its letter. The bytes have it before the glyph it sits under, and the pre rules after that glyph - which can be a letter " +
          "with its vowel sign (જા C5, ણુ CF) or with the ra-, ya- or va-sign (ક્ર BE): જ઼ા and ક઼્ર, not જા઼ and ક્ર઼."),
        R("ાૅ", "ૉ", "ા + ૅ is written ૉ."),
        R("ાે", "ો", "ા + ે is written ો."),
        R("ાૈ", "ૌ", "ા + ૈ is written ૌ."),
        R("અા", "આ", "અ + ા is written આ."),
        R("અૅ", "ઍ", "અ + ૅ is written ઍ."),
        R("અે", "એ", "અ + ે is written એ."),
        R("અૈ", "ઐ", "અ + ૈ is written ઐ."),
        R("આૅ", "ઑ", "આ + ૅ is written ઑ."),
        R("આે", "ઓ", "આ + ે is written ઓ."),
        R("આૈ", "ઔ", "આ + ૈ is written ઔ."),
        R($"િ(ં?)((?:[{Cons}]઼?્)*[{Cons}]઼?)", "$2િ$1",
          "Move pre-base matra U+0ABF (and the anusvara of 74) after the consonant cluster (legacy visual order -> Unicode logical order)."),
        R("(?<![અ-ઔક-હ઼ા-ૌૠ-ૣ])ઃ", ":",
          "Shree writes the colon and the visarga with one code (3A). Where it cannot be a visarga - not after a letter, a vowel or a vowel sign: " +
          "after a space, a digit, punctuation, the anusvara (કહ્યું:) or at the start - it is the colon."),
    };

    /// <summary>Marks a halant typed inside a word (virama + ZWNJ) for the reverse pairs (a noncharacter).</summary>
    public const string ZwnjMark = "﷕";

    /// <summary>Marks a half form with no letter after it (virama + ZWJ) for the reverse pairs (a noncharacter).</summary>
    public const string ZwjMark = "﷖";

    /// <summary>Put between the codes of a pair the rules must leave alone (a noncharacter; the last rule removes it).</summary>
    public const string Keep = "﷑";

    public static string Protect(string legacy) => string.Join(Keep, legacy.Select(c => c.ToString()));
}
