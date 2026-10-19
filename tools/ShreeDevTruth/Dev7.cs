using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The regularities of the Shree-Lipi 7 Devanagari composer (S8Dev764.dll), as
/// rewrite rules: reph after its syllable, the ra-, ya- and va-signs, signs before a
/// letter's spacer, the i-sign glyph by what it stands over, the sign + anusvara and
/// sign + reph glyphs. The learner then only needs pairs for what the rules do not
/// say: the half letters, the ligatures and the composer's own quirks.
/// </summary>
public static class Dev7
{
    // ---------- Unicode classes ----------

    public const string Cons = "क-ह\U00000958-\U0000095F";
    /// <summary>Signs that follow a letter (matras, bindus, visarga, vedic marks) - not the virama or nukta.</summary>
    public const string Signs = "ऀ-ःऺऻा-ौॎॏ॑-ॗॢॣ";
    public const string SignsNoVisarga = "ऀ-ंऺऻा-ौॎॏ॑-ॗॢॣ";
    public const string Vowels = "ऄ-औॠॡॲ-ॷॐ";

    public const string ZWJ = "\U0000200D", ZWNJ = "\U0000200C", Virama = "\U0000094D", Nukta = "\U0000093C";

    // Markers the pre rules leave for the pairs (noncharacters: never text).
    public const string RephMark = "\U0000FDD0";
    public const string Keep = "\U0000FDD1";
    public const string YaMark = "\U0000FDD2";
    public const string RaMark = "\U0000FDD3";
    public const string HalantMark = "\U0000FDD4";
    public const string PendingMark = "\U0000FDD5";
    public const string YaMark2 = "\U0000FDD6";
    public const string YaMarkM = "\U0000FDD7";
    public const string YaMark3 = "\U0000FDD8";
    public const string NaMark = "\U0000FDD9";
    public const string YaMarkT = "\U0000FDDA";
    public const string DropSpacer = "\U0000FDDB";
    public const string Soft = "\U0000FDDD";

    /// <summary>
    /// What a word goes on with after a virama (a regex class body): the letters and signs of
    /// the Devanagari block (not the dandas U+0964-0965, the digits or ॰) and the joiners.
    /// A virama before anything else - or at the end of the text - ends its word.
    /// </summary>
    public const string WordGoesOn = "\U00000900-\U00000963\U00000971-\U0000097F" + ZWJ + ZWNJ;

    /// <summary>A consonant cluster (nukta written before its letter by then).</summary>
    public const string ClusterBare = "(?:" + Nukta + "?[" + Cons + "]" + Virama + ZWJ + "?)*" + Nukta + "?[" + Cons + "]";
    /// <summary>The same, with the ra- and ya-sign markers after it.</summary>
    public const string Cluster = ClusterBare + "(?:[" + RaMark + YaMark + YaMark2 + YaMarkM + YaMark3 + NaMark + YaMarkT + "])*";

    // ---------- legacy code classes (hex) ----------

    /// <summary>Full letters and ligatures: what a sign sits on.</summary>
    public const string Bases =
        "40 41 42 43 44 45 46 47 48 49 4A 4B 4C 4D 4E 4F 50 51 52 53 54 55 56 57 58 59 5A 5C 5E 5F 60 61 62 63 64 65 66 67 68 69 6A 6B 6C 6E 7E 9B " +
        "23 82 83 84 86 87 88 93 94 95 96 98 9C AC B8 B9 BA BC BE C3 C4 C5 C6 C7 C8 C9 CC CE D0 D1 D2 D3 D4 D5 D6 D7 D8 D9 DA DB DE E0 E1 E9 EA EF F0 F2 F3 F5 F6 F7 F8 FC FD";

    /// <summary>Half letters (C4 and CE are also the stems of ञ and त्त, with 6D after them).</summary>
    public const string Halves = "8A 99 BB BD BF C0 C1 C2 C4 CA CB CD CE CF DC DD DF E2 E3 E4 E5 E6 E8 EB EC ED EE F1 F4 F9 FA FB";

    /// <summary>Signs drawn below a letter (they go before its spacer).</summary>
    public const string Below = "77 78 79 7A A5 A6 AB AE AF B2 B4 89 FE FF";

    /// <summary>Signs drawn above a letter (before a 24 spacer, after a 3E one).</summary>
    public const string Above = "6F A1 A7 B1 B0";

    /// <summary>Letters with no half form: in a conjunct they take the halant B2 (before their spacer).</summary>
    public const string NoHalf = "4C 4E 51 52 53 54 58";

    /// <summary>Letters with a round bottom: the ra-sign B4, the va-sign 89, the nukta 8B.</summary>
    public const string Round = "4C 4E 51 52 53 54";

    /// <summary>Letters and ligatures ending in द or ह: u FE, uu FF.</summary>
    public const string UHook = "58 68 86 87 88 95 D0 D2 D3 D4 D5 D6 D7 D8 DB F5";

    /// <summary>Stacked ligatures: the short u 78 / uu 7A.</summary>
    public const string UShort = "84 93 94 96 AC C5 C6 C7 C8 C9";

    /// <summary>The precomposed nukta letters (U+0958-095F and ऩ ऱ ऴ) and their letter.</summary>
    public static readonly (string Pre, string Letter)[] NuktaLetters =
    {
        ("\U00000958", "क"), ("\U00000959", "ख"), ("\U0000095A", "ग"), ("\U0000095B", "ज"),
        ("\U0000095C", "ड"), ("\U0000095D", "ढ"), ("\U0000095E", "फ"), ("\U0000095F", "य"),
        ("\U00000929", "न"), ("\U00000931", "र"), ("\U00000934", "ळ"),
    };

    /// <summary>The Unicode pre rules. SAN7 has no nukta (its composer drops it) and a na-sign (्न).</summary>
    /// <summary>A regex class body (characters and ranges, no escapes) without some characters, its ranges written out.</summary>
    public static string Without(string cls, string remove)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < cls.Length; i++)
        {
            char a = cls[i], b = a;
            if (i + 2 < cls.Length && cls[i + 1] == (char)0x2D) { b = cls[i + 2]; i += 2; }
            for (char c = a; c <= b; c++) if (!remove.Contains(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    public static List<RewriteRule> ForwardPre(bool sanskrit = false)
    {
        var rules = new List<RewriteRule>();
        if (sanskrit)
        {
            // The Sanskrit fonts have no nukta and Shree's Sanskrit composer drops it: the letter is written
            // as the composer writes it, and the nukta - moved before its letter, as for DEV7, where the
            // cluster rules expect it - meets a pair that leaves it out and reports it (Policy: never silently).
            foreach (var (pre, letter) in NuktaLetters)
                rules.Add(R(pre, letter + Nukta, $"{pre} is {letter} + nukta: the Sanskrit fonts have no nukta (Shree's Sanskrit composer drops it), so {letter}, and the nukta is reported."));
            rules.Add(R($"([{Cons}]){Nukta}", Nukta + "$1", "Nukta: the Sanskrit fonts have none; moved before its letter, where the pairs leave it out and report it."));
        }
        else
        {
            foreach (var (pre, letter) in NuktaLetters)
                rules.Add(R(pre, letter + Nukta, $"{pre} is {letter} + nukta: the nukta is drawn as a sign of its own."));
            rules.Add(R($"([{Cons}]){Nukta}", Nukta + "$1", "Nukta: written before its letter."));
        }
        string halant = sanskrit ? "6F" : "B2";
        rules.Add(R($"{Virama}(?:{ZWNJ}|(?![{WordGoesOn}]))", HalantMark,
            $"A visible virama is the halant {halant} (U+FDD4 marks it for the pairs): ्+ZWNJ, and a virama that ends a word - before a space, punctuation, a danda, a digit, anything but a letter or sign of the script or a joiner, or at the end of the text. " +
            "Every Unicode font draws a word-final virama as a visible halant; the half form the composer answers for a bare virama is only what it shows while it waits for the next letter."));
        rules.Add(R($"(?<![{Virama}{ZWJ}{Nukta}])र{Virama}(?![{ZWJ}{Virama}])(?:({ClusterBare}(?!{Virama})|[{Vowels}])|(?![{Cons}{Nukta}]))([{Without(SignsNoVisarga, Policy.PassedSigns(sanskrit ? "san7" : "dev7"))}]*)", "$1$2" + RephMark,
            "Reph: र् before a letter is written after the letter and its signs (before a visarga; and before a sign the fonts have no glyph for, which the table passes through - Word draws it on a dotted circle of its own, and the reph belongs on its letter); U+FDD0 marks it for the pairs. र्+ZWJ is the eyelash ra, ऱ् the half ra."));
        rules.Add(R($"([शत]{Virama}र{Virama})(?=य[ाी-ौॢॣ])", "$1" + ZWJ, "श्र्य त्र्य with a vowel sign: the half श्र् त्र् and य, as with a ZWJ."));
        string notYa = $"िऀ-ः{Virama}{ZWJ}{RephMark}{YaMark}{YaMark2}{YaMark3}{YaMarkM}{YaMarkT}";
        rules.Add(R($"(?<=[{Cons}]{Virama}{ZWJ}?{Nukta}?[{Cons}]{Virama}{ZWJ}?{Nukta}?[{Cons}]){Virama}य(?![{notYa}])", YaMark3,
            "Ya-sign after a conjunct of three letters or more, with no sign: E7 (U+FDD8 marks it)."));
        rules.Add(R($"(?<=[{Cons}]{Virama}{ZWJ}?{Nukta}?[{Cons}]){Virama}य(?![{notYa}])", YaMark2,
            "Ya-sign after a conjunct of two letters, with no sign or a vowel sign: E7 (U+FDD6 marks it; त्त्य is written with the half त्त there)."));
        if (sanskrit)
            rules.Add(R($"(?<=[टठडढ]){Virama}य(?![{ZWJ}]|{Virama}(?!र))", YaMarkT, "Ya-sign: after ट ठ ड ढ ्य is always the ya-sign F7, in place of the spacer 3E (U+FDDA marks it)."));
        rules.Add(R($"{Virama}य(?=[ाी-ौॢॣ])", YaMarkM, "Ya-sign with a vowel sign (not the i-sign): E7 (U+FDD7 marks it)."));
        if (!sanskrit) rules.Add(R($"(?<=[टठडढ]){Virama}य(?![{ZWJ}{YaMarkM}]|{Virama}(?!र))", YaMark, "Ya-sign: after ट ठ ड ढ ्य is always the ya-sign E7."));
        rules.Add(R($"{Virama}य(?![{notYa}])", YaMark,
            "Ya-sign: ्य alone is the ya-sign E7 (U+FDD2 marks it) - but before ि ं ँ ः, a reph or the ra-sign the composer writes the half letter and य."));
        rules.Add(R($"{Virama}र(?!{ZWJ}|{Virama})", RaMark, "Ra-sign: ्र after a letter (U+FDD3 marks it for the pairs)."));
        if (sanskrit)
            rules.Add(R($"(?<=[कखघचजझधपफबभमव]){Virama}न(?![{Virama}{ZWJ}])", NaMark, "Na-sign: ्न after क ख घ च ज झ ध प फ ब भ म व is the subscript 6E (U+FDD9 marks it for the pairs); other letters take their half form (or halant) and न."));
        rules.Add(R($"({Cluster})ि", "ि$1", "The i-sign is written before its letter or conjunct (Unicode logical order -> visual order)."));
        rules.Add(R($"{Virama}(?![{Cons}{Nukta}{ZWJ}])", Virama + PendingMark,
            "A virama before a vowel letter or a sign (a word-final one is the halant, above): the composer writes the half form it is waiting with (U+FDD5 marks it for the pairs)."));
        rules.Add(R($"([{Cons}]){Virama}(?!र)(?=(?:{Nukta}?[{Cons}]{Virama})+(?:{PendingMark}|{Nukta}?[{Cons}]))", "$1" + Virama + ZWJ,
            "A conjunct with a virama after it (waiting for its next letter, or with one more) is written as half letters, not as a ligature - the composer breaks क्क into क् क् when a link follows: as if each virama but the last had a ZWJ."));
        return rules;
    }

    public static List<RewriteRule> ForwardPost()
    {
        string below = Class(Below), above = Class(Above), bases = Class(Bases), halves = Class(Halves), round = Class(Round);
        string nukta = Class("B5 8B");
        return new()
        {
            R(Soft, "", "U+FDDD stands for a joiner: no glyph, and nothing for the rules below to stop at."),
            R($"{Lit("B5")}({Class("53 54 69")})", Rep("8B") + "$1", "Nukta under ड ढ ळ: 8B."),
            R($"({round}){Lits("B2 3E 64")}", "$1" + Rep("89 3E"), "Va after ङ छ ट ठ ड ढ: the va-sign 89 under the letter."),
            R($"{Lit("24")}((?:{below}|{above}|{Lit("A9")})+)", "$1" + Rep("24"), "Signs below and above a letter, and the reph, go before its spacer 24."),
            R($"{Lit("3E")}({below}+)", "$1" + Rep("3E"), "Signs below a letter go before its spacer 3E."),
            R($"({Lits("B2 3E")}{Class("C4 CE")}){Lit("6D")}({Class("77 79 A5 A6")}+)", "$1$2" + Rep("3E"), "ञ त्त after a letter with the halant: a sign below takes the 3E spacer in place of the 6D stem."),
            R($"({Class("89 C5 C6 C7 C8 C9")}){Lit("AB")}({Lit("3E")})", "$1$2" + Rep("AB"), "Ra-sign after the va-sign or a ligature of ट ठ ड ढ: after the spacer."),
            R($"({Class(UHook)}){Lit("77")}", "$1" + Rep("FE"), "u after द ह and their ligatures: FE."),
            R($"({Class(UHook)}){Lit("79")}", "$1" + Rep("FF"), "uu after द ह and their ligatures: FF."),
            R($"({Class(UShort)}|{Lit("8B")}{Class("53 54")}){Lit("77")}", "$1" + Rep("78"), "u under a stacked ligature or a nukta: the short 78."),
            R($"({Class(UShort)}|{Lit("8B")}{Class("53 54")}){Lit("79")}", "$1" + Rep("7A"), "uu there: the short 7A."),
            R($"({Class(Round + " 69 EF F0")}){Lit("AB")}", "$1" + Rep("B4"), "Ra-sign under the round-bottomed letters and ळ: B4."),
            R(Lits("B4 77"), Rep("AE"), "Ra-sign B4 + u: one glyph."),
            R(Lits("B4 79"), Rep("AF"), "Ra-sign B4 + uu: one glyph."),
            R($"({Lit("B4")})({Class("A5 A6")})({Lit("3E")})", "$1$3$2", "Ra-sign B4 + vocalic r: the sign after the spacer."),
            R(Lits("24 72"), Rep("24 73"), "ii after a spacer 24: 73."),
            R($"{Lit("5B")}(?={nukta}?(?:{bases}|{halves}))(?!{Lit("61")})", Rep("7B"), "i-sign over one letter or ligature: 7B (5B only over र and alone)."),
            R($"{Lit("7B")}(?={nukta}?(?:{halves}(?!{Lit("6D")})|.{Class("B2 89")}))", Rep("70"), "i-sign over a conjunct of two glyphs: the long 70."),
            R($"{Lit("7B")}({nukta}?{bases}{below}*{Class("3E 6D")}?{below}*){Lit("A7")}", Rep("71") + "$1", "i-sign + anusvara over one letter: 71."),
            R($"({Lits("5B 61")}){Lit("A7")}", "$1" + Rep("A8"), "i-sign + anusvara over र: A8."),
            R(Lits("72 A7"), Rep("74"), "ii + anusvara: one glyph."),
            R(Lits("6F A7"), Rep("7C"), "e + anusvara: one glyph."),
            R(Lits("A1 A7"), Rep("A2"), "ai + anusvara: one glyph."),
            R(Lits("73 A7"), Rep("73 A8"), "ii (after a spacer) + anusvara: A8."),
            R($"({Class("7B 5B")}{nukta}?{bases}{below}*{Class("3E 6D")}?{below}*|{Class("72 73 6F A1")}){Lit("B1")}", "$1" + Rep("3E 3E B1"),
                "Candrabindu after a tall sign (i, ii, e, ai): moved right by two 3E."),
            R(Lits("6F A9"), Rep("7D"), "e + reph: one glyph."),
            R(Lits("A1 A9"), Rep("A3"), "ai + reph: one glyph."),
            R(Lits("7C A9"), Rep("5D"), "e + anusvara + reph: one glyph."),
            R(Lits("A2 A9"), Rep("A4"), "ai + anusvara + reph: one glyph."),
            R(Lits("A7 A9"), Rep("AA"), "anusvara + reph: one glyph."),
            R(Lits("A8 A9"), Rep("AA"), "anusvara (after ii) + reph: one glyph."),
            R(Lits("72 A9"), Rep("75"), "ii + reph: one glyph."),
            R(Lits("74 A9"), Rep("76"), "ii + anusvara + reph: one glyph."),
            R(Keep, "", "U+FDD1 in a pair keeps the rules above off a spelling the composer writes its own way; it is not text."),
        };
    }

    // ---------- DEV7 -> Unicode ----------

    /// <summary>Signs that follow a letter in the bytes (so sit between a letter and its reph).</summary>
    public const string SignsAfter = "6D 6F 72 73 74 77 78 79 7A 7C A1 A2 A5 A6 A7 A8 AB AE AF B0 B1 B4 89 E7 FE FF";

    /// <summary>The reverse pre rules; a sister family adds the codes it draws differently to the classes.</summary>
    public static List<RewriteRule> ReversePre(string extraBases = "91 92 B6", string extraHalves = "B3", string extraSigns = "")
    {
        string halves = Class(Halves + " " + extraHalves), bases = Class(Bases.Replace("42 ", "") + " " + extraBases), signs = Class(SignsAfter + " " + extraSigns), nukta = Class("B5 8B");
        var rules = new List<RewriteRule>();
        foreach (var (lig, parts) in new[] { ("7D", "6F A9"), ("A3", "A1 A9"), ("5D", "7C A9"), ("A4", "A2 A9"), ("AA", "A7 A9"), ("75", "72 A9"), ("76", "74 A9") })
            rules.Add(R(Lit(lig), Rep(parts), $"{lig}: the sign {parts[..2]} with a reph - two pieces, so the reph can move."));
        rules.Add(R($"({Class("C4 CE")})({Class("77 79 A5 A6")}+){Lit("3E")}", "$1" + Rep("6D") + "$2", "ञ त्त with a sign below after a halant letter: the composer writes the 3E spacer for the 6D stem."));
        string withHalf = string.Join(" ", (Bases + " " + extraBases).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(b => !NoHalf.Split(' ').Contains(b))) + " 6D";
        rules.Add(R($"(?<={Class(withHalf)}){Lit("B2")}(?!{Lit("3E")})", HalantMark,
            "The halant B2 after a letter that has a half form is a visible virama (्+ZWNJ); U+FDD4 marks it for the pairs."));
        rules.Add(R(Class("24 3E"), "", "The spacers 24 and 3E draw nothing; they only follow a letter (or shift a candrabindu)."));
        rules.Add(R($"({halves}(?<!{Lit("E8")})|{Lit("B2")})(?={Class("61 E9 EA")})", "$1" + ZWJ, "A half letter before the full र: ्+ZWJ+र (्र alone would be the ra-sign)."));
        rules.Add(R($"((?:(?:{nukta}?(?:{halves}|{bases}{Lit("B2")}))*{nukta}?{bases})?{signs}*)(?<!{Lit("42")}){Lit("A9")}", RephMark + "$1",
            "Reph is written after its syllable (or after a sign with no letter); Unicode has र् first - U+FDD0 marks it there for the pairs. (42 A9 is ई, not a reph on इ.)"));
        return rules;
    }

    public static List<RewriteRule> ReversePost()
    {
        var rules = new List<RewriteRule>
        {
            R($"{Nukta}([{Cons}])", "$1" + Nukta, "The nukta is drawn before its letter; Unicode has it after."),
        };
        foreach (var (a, b, one) in new[] { ("आ", "े", "ओ"), ("आ", "ै", "औ"), ("आ", "ॅ", "ऑ"), ("आ", "ॆ", "ऒ"), ("अ", "ॆ", "ऄ"), ("ए", "े", "ऐ"), ("ए", "ॅ", "ऍ"), ("ए", "ॆ", "ऎ"), ("ा", "े", "ो"), ("ा", "ै", "ौ"), ("ा", "ॅ", "ॉ"), ("ा", "ॆ", "ॊ") })
            rules.Add(R(a + b, one, $"{a} + {b} is written {one}."));
        foreach (var (pre, letter) in NuktaLetters)
            rules.Add(R(letter + Nukta, pre, $"{letter} + nukta is written {pre}, as Shree's Unicode composer writes it."));
        rules.Add(R($"ि(ं?)((?:[{Cons}]{Nukta}?{Virama}{ZWJ}?)*[{Cons}]{Nukta}?)", "$2ि$1",
            "The i-sign (and the anusvara of 71) is drawn before its letter or conjunct; Unicode has it after (visual order -> logical order)."));
        rules.Add(R($"{Virama}{ZWNJ}(?![{WordGoesOn}])", Virama,
            "The halant at the end of a word - before a space, punctuation, a danda, a digit or the end of the text - is a plain virama, as Unicode text " +
            "writes a word-final one (अर्थात्, not अर्थात्+ZWNJ; the Gujarati tables read it so too). Before a letter it stays ्+ZWNJ, the visible halant."));
        return rules;
    }

    /// <summary>Unicode in the visual order the reverse pairs answer in.</summary>
    public static List<RewriteRule> VisualRules()
    {
        var rules = new List<RewriteRule>();
        foreach (var (pre, letter) in NuktaLetters) rules.Add(R(pre, letter + Nukta, ""));
        rules.Add(R($"([{Cons}]){Nukta}", Nukta + "$1", ""));
        rules.Add(R($"((?:{Nukta}?[{Cons}]{Virama}{ZWJ}?)*{Nukta}?[{Cons}])ि", "ि$1", ""));
        return rules;
    }

    /// <summary>Codes the composer only writes inside a longer spelling, as the font draws them.</summary>
    public static readonly (string Hex, string Unicode)[] FontCodes =
    {
        ("7B", "ि"), ("70", "ि"), ("5B", "ि"), ("71", "िं"),
        ("72", "ी"), ("73", "ी"), ("74", "ीं"),
        ("77", "ु"), ("78", "ु"), ("FE", "ु"), ("79", "ू"), ("7A", "ू"), ("FF", "ू"),
        ("A5", "ृ"), ("A6", "ॄ"), ("6F", "े"), ("7C", "ें"), ("A1", "ै"), ("A2", "ैं"),
        ("A7", "ं"), ("A8", "ं"), ("B1", "ँ"), ("B0", "ॅ"), ("85", "ः"), ("6D", "ा"),
        ("A9", "र्"), ("E8", "र्" + ZWJ), ("AB", "्र"), ("B4", "्र"), ("AE", "्रु"), ("AF", "्रू"),
        ("E7", "्य"), ("89", "्व"), ("B2", "्"), ("B5", Nukta), ("8B", Nukta), ("63", "ल"),
        ("8A", "क्"), ("BB", "ख्"), ("BD", "ग्"), ("BF", "घ्"), ("C0", "च्"), ("C1", "ज्"), ("C2", "झ्"), ("C4", "ञ्"),
        ("CA", "ण्"), ("CB", "त्"), ("CD", "त्र्"), ("CE", "त्त्"), ("CF", "थ्"), ("DC", "ध्"), ("DD", "न्"), ("DF", "प्"),
        ("E2", "फ्"), ("E3", "ब्"), ("E4", "भ्"), ("E5", "म्"), ("E6", "य्"), ("EB", "ल्"), ("EC", "व्"), ("ED", "श्"),
        ("EE", "ष्"), ("F1", "स्"), ("F4", "ह्"), ("F9", "ळ्"), ("FA", "क्ष्"), ("FB", "श्र्"), ("99", "ख" + Nukta + "्"),
        ("B8", "क्क"), ("BA", "क्त"), ("B9", "क्व"), ("6A", "क्ष"), ("BE", "ग्न"), ("93", "ङ्ग"), ("94", "ङ्घ"), ("AC", "ङ्क्ष"),
        ("83", "च्च"), ("6B", "ज्ञ"),
        ("23", "ञ्च्"), ("82", "ज्ज्"), ("23 6D", "ञ्च"), ("82 6D", "ज्ज"), ("C4 6D", "ञ"), ("CE 6D", "त्त"),
        ("U+200D", ZWJ), ("U+FDD4", Virama + ZWNJ), ("U+FDD0", "र्"), ("C5", "ट्ट"), ("C6", "ट्ठ"), ("C7", "ठ्ठ"), ("C8", "ड्ड"),
        ("96", "ड्ढ"), ("C9", "ढ्ढ"), ("CC", "त्र"), ("D0", "द्र"), ("D1", "दृ"), ("D2", "द्ग"), ("D3", "द्घ"), ("D4", "द्द"),
        ("D5", "द्ध"), ("D6", "द्न"), ("D7", "द्ब"), ("D8", "द्भ"), ("D9", "द्म"), ("DA", "द्य"), ("DB", "द्व"), ("DE", "न्न"),
        ("E0", "प्र"), ("E1", "प्त"), ("84", "ल्ल"), ("6C", "श्र"), ("EF", "ष्ट"), ("F0", "ष्ठ"), ("F2", "स्र"), ("F3", "स्त्र"),
        ("86", "ह्ण"), ("95", "ह्न"), ("F5", "ह्र"), ("F6", "हृ"), ("F7", "ह्म"), ("F8", "ह्य"), ("87", "ह्ल"), ("88", "ह्व"),
        ("BC", "ख्र"), ("C3", "झ्र"), ("E9", "रु"), ("EA", "रू"), ("98", "ख" + Nukta), ("9C", "श्र्व"), ("FC", "श्र्च"), ("FD", "श्र्न"),
        ("22", "‘"), ("27", "’"), ("81", "×"), ("8F", "÷"), ("8D", "–"), ("9A", "॒"), ("90", "ॆ"), ("26", "।"), ("40", "ऽ"), ("9B", "ॐ"),
        ("B7", "•"),
    };

    /// <summary>In the 7 fonts these four codes are copies of other glyphs.</summary>
    public static readonly (string Hex, string Unicode)[] SevenCopies = { ("91", "म"), ("92", "फ"), ("B3", "क्"), ("B6", "य") };

    /// <summary>In the 4/5/6 fonts (SHREE-DEV-*) the same four codes are ligatures of their own.</summary>
    public static readonly (string Hex, string Unicode)[] FourFiveSixCodes = { ("91", "ङ्क"), ("92", "ङ्ख"), ("B3", "्न"), ("B6", "ङ्म") };

    /// <summary>The 4/5/6 fonts also draw ligatures at U+0200-0218 (U+0219-021C are blank).</summary>
    public static readonly string[] FourFiveSixLigatures =
    {
        "क्क", "क्ल", "क्व", "ङ्क", "ङ्ख", "ङ्ग", "ङ्घ", "ङ्म", "ङ्क्ष", "छ्व", "ञ्च", "ञ्ज", "प्ल",
        "फ्ल", "ल्ल", "ट्व", "ट्ट", "ट्ठ", "ठ्ठ", "ड्ड", "ड्ढ", "द्ढ", "क्त", "न्न", "प्त",
    };
    public static Script Script => _script ??= new Script
    {
        Name = "dev7",
        Title = "Shree-Lipi 7.0 Devanagari",
        CaptureFile = "dev7-groundtruth.json",
        ForwardPre = ForwardPre(),
        ForwardPost = ForwardPost(),
        ReversePre = ReversePre(),
        ReversePost = ReversePost(),
        VisualRules = VisualRules(),
        MarkerAtoms = new[] { (RephMark, "A9"), (YaMark, "E7"), (YaMark2, "E7"), (YaMarkM, "E7"), (YaMark3, "E7"), (RaMark, "AB"), (HalantMark, "B2"), (PendingMark, "") },
        Extra = new (string, string, string)[]
        {
            ("्", "B2", "a virama with no letter: the halant"),
            (Nukta, "B5", "nukta: drawn before its letter (8B under ड ढ ळ)"),
            (ZWNJ, "U+FDDD", "ZWNJ: no glyph (U+FDDD, which the first post rule takes out)"), (ZWJ, "U+FDDD", "ZWJ: no glyph - it asks for the half form, which the pairs give (U+FDDD, which the first post rule takes out)"),
            ("॥", "26 26", "double danda: two dandas (the composer has no code for it)"),
            ("“", "22 22", "no glyph: two single quotes (the composer writes nothing)"),
            ("”", "27 27", "no glyph: two single quotes (the composer writes B3, which draws क्)"),
            ("ॆ", "90", "the composer's empty glyph 90 (the font has no short e)"),
            ("ॊ", "6D 90", "aa + the empty glyph 90"),
            ("ऎ", "45 90", "ए + the empty glyph 90"),
            ("ऒ", "41 6D 90", "आ + the empty glyph 90"),
            ("ऄ", "41 90", "अ + the empty glyph 90"),
            ("ऌ", "", "no glyph (the composer writes the * glyph)"),
            ("ॡ", "", "no glyph (the composer writes nothing)"),
            ("ॢ", "", "no glyph (the composer writes nothing)"),
            ("ॣ", "", "no glyph (the composer writes nothing)"),
            ("॑", "", "no glyph (the composer writes nothing)"),
        },
        FontCodes = FontCodes.Concat(SevenCopies).ToArray(),
        NeverWrite = new() { "91", "92", "B3", "B6" },
        Visible = (u, hex) => DrawnAs.TryGetValue(hex, out var d) ? d : HasNukta(hex) ? u : StripNukta(u),
        Halant = "B2",
        Spacers = new[] { "24", "3E" },
        Adjust = (t, raw) => Policy.Adjust("dev7", t) ?? WordFinal.Spell(t, raw, '\U0000094D', '\U0000093C', "B2", new[] { "24", "3E" }),
    };
    static Script? _script;

    /// <summary>DEV7 as the 4/5/6 reverse table sees it: other reverse pre rules, the 4/5/6 codes.</summary>
    public static Script Script456(List<RewriteRule> reversePre) => new()
    {
        Name = "dev456",
        Title = "Shree-Lipi 4.0/5.0/6.0 Devanagari",
        CaptureFile = Script.CaptureFile,
        ForwardPre = Script.ForwardPre,
        ForwardPost = Script.ForwardPost,
        ReversePre = reversePre,
        ReversePost = Script.ReversePost,
        VisualRules = Script.VisualRules,
        MarkerAtoms = Script.MarkerAtoms,
        Extra = Script.Extra,
        FontCodes = FontCodes.Concat(FourFiveSixCodes).ToArray(),
        NeverWrite = Script.NeverWrite,
        Visible = Script.Visible,
        Halant = Script.Halant,
        Spacers = Script.Spacers,
        Adjust = Script.Adjust,
    };

    /// <summary>Composer spellings that drop a mark: what the bytes really draw.</summary>
    public static readonly Dictionary<string, string> DrawnAs = new(StringComparer.Ordinal)
    {
        ["4C EC"] = "ङव्", ["52 EC"] = "ठव्", ["53 EC"] = "डव्", ["54 EC"] = "ढव्",   // ङ्व् ठ्व् ड्व् ढ्व्: no halant on the first letter
    };

    /// <summary>Bytes with a nukta code (B5 8B, or the nukta ligatures 98 99).</summary>
    public static bool HasNukta(string hex) => hex.Split(' ').Any(h => h is "B5" or "8B" or "98" or "99");

    /// <summary>Text without its nukta: what bytes with no nukta code draw (the composer drops it in a few spellings).</summary>
    public static string StripNukta(string u) => FontSoftware.Core.Conversion.Nukta.Spell(u).Replace(Nukta, "");
}
