using static Common;

/// <summary>
/// The Shree-Lipi Odia fonts. The capture's family check (cap-ori\fonts\family-check.json: (3,1) cmap, glyph id, advance
/// and outline bytes of every installed face) found the 7 faces S0600892-S0604892 (SHREE-ORI7-0600..0604) and the
/// 4/5/6 faces S06000F2-S06040F2 (SHREE-ORI-0600..0604) identical code for code: 216 codes, same glyphs, same advances.
/// WORI7001/2.TTF carry the family names SHREE-ORI7-0603/0604 too and add one code, ₹ at 3C. So ONE table pair serves
/// both families (shree-ori7), and ₹ - which no face but WORI7001/2 draws, and which a document set in SHREE-ORI7-0600..0602
/// or SHREE-ORI-* shows as Word's fallback '&lt;' - is passed through as itself and reported (P3b), as the Gujarati tables do.
///
/// Two corrections turn the composer's raw bytes into what the tables write:
/// <list type="bullet">
/// <item><b>ନ: raw 9C, written 5B.</b> S8Ori764 answers 9C FF for ନ (code 65). Every installed face (all 12, both families)
///   draws the ଙ୍କ୍ତ ligature at 9C (U+0153) and ନ at 5B '[' (rendered by glyph index and in WPF: r3\tbl-ori\round1\glyphs3.png,
///   sheet-00.png row 47, sheet-01.png row 101, sheet-02.png row 148). The advances agree: 9C is 566/1000 wide, so 9C + the
///   spacer FF would be 729 - wider than any letter - while 5B is 341, and 5B FF (504) is as wide as କ 4C FF (517) or
///   ତ 86 FF (503). Every other raw byte of the truth draws its text (259 spellings checked by eye). The composer never
///   writes 5B. So ନ is 5B FF, and the reverse tables read 9C as ଙ୍କ୍ତ, the glyph a document shows there.</item>
/// <item><b>₹ 3C, ୰ 8F, ଽ 90: passed through.</b> 3C is in WORI7001/2 only; U+008F and U+0090 are C1 controls, which
///   Word and RichEdit draw as nothing (r3\word\final-invisible.txt: SHREE-ORI, SHREE-ORI7, Shree-Ori7-00x), and no other
///   code point of the fonts reaches those glyphs (final-alternatives.txt). P3b: written as themselves, reported; the
///   reverse tables still read 3C 8F 90 (documents typed with Shree's own composer have them).</item>
/// </list>
///
/// Itself: the ASCII the fonts draw as itself at its own code (Word-visibility map, r3\word\final-ascii.txt: ! % ( ) , - . /
/// : ?) and * (a six-armed star, and the composer's own spelling of *: code 2A -> 2A). Every other ASCII code but the
/// digits draws something else: " ‘, # a hook, $ ଥ, &amp; nothing (the lowering spacer), ' ’, + ଣ୍ଟ, ; ନ୍ତ, = ଣ୍ଠ, &gt; ।,
/// @ ୍ଣ, [ ନ, \ ଙ୍ଖ, ] ିଁ, ^ ଙ୍ଘ, _ ଙ୍କ୍ଷ, ` ଚ, { େ, | ଢ, } ିର୍, ~ ଣ, the letters Odia letters; &lt; is no code of the
/// SHREE-ORI7-0600..0602 / SHREE-ORI faces (₹ in WORI7001/2).
/// </summary>
public sealed record Fam(string Id, string Name, string Fonts, (string Label, string FilePattern, string Family)[] FaceSets,
    IReadOnlyDictionary<string, string> DocMap, IReadOnlyDictionary<string, string> PassMap, string Never, string Itself)
{
    /// <summary>A raw composer spelling as a document in these fonts holds it (9C -> 5B).</summary>
    public string Doc(string hex) => hex.Length == 0 ? hex
        : Norm(string.Join(" ", hex.Split(' ').Select(t => DocMap.TryGetValue(t, out var m) ? m : t)));

    /// <summary>A raw composer spelling as the table writes it: <see cref="Doc"/>, then the codes passed through (3C 8F 90).</summary>
    public string Apply(string hex)
    {
        string d = Doc(hex);
        return d.Length == 0 ? d : Norm(string.Join(" ", d.Split(' ').Select(t => PassMap.TryGetValue(t, out var m) ? m : t)));
    }

    /// <summary>
    /// The truth as the table writes it: preferred and accepted spellings through <see cref="Apply"/>; the document spellings
    /// (<see cref="Doc"/>: Shree's 3C 8F 90) stay accepted, so the reverse tables are checked on them too.
    /// </summary>
    public List<Truth> Truth(IEnumerable<Truth> truth) => truth.Select(t =>
    {
        string pref = Apply(t.Pref);
        var acc = t.Acc.Select(Apply).Concat(t.Acc.Select(Doc)).Distinct().ToList();
        acc.Remove(pref); acc.Insert(0, pref);
        return t with { Pref = pref, Acc = acc.ToArray(), Composer = t.Composer ?? (pref != t.Pref ? t.Pref : null) };
    }).ToList();

    /// <summary>Codes this family's tables must never write: they draw something else here, or nothing.</summary>
    public HashSet<string> NeverCodes => Never.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    /// <summary>
    /// Codes the composer writes only in another typing order, or never, that the reverse tables still read (as every
    /// face draws them): ₹ 3C, ୰ 8F, ଽ 90 (Shree's own spellings); ଙ୍କ୍ତ at 9C (the raw byte of ନ, and what the
    /// earlier table wrote for ଙ୍କ୍ତ); the four ୱ-ligatures 89 96 A3 A5 (drawn ତ୍ତ୍ୱ ଦ୍ଧ୍ୱ ନ୍ଦ୍ୱ ନ୍ଧ୍ୱ, read with ବ as the
    /// truth reads the ba-phala stroke B4); FD, the second ba-phala (code E1); the hook 23 (the i-sign of ଖ ଥ ଧ); E3 (code
    /// E0: ଚ with a dot below); B7 the bullet.
    /// </summary>
    public (string Hex, string Unicode)[] FontCodes => new[]
    {
        ("3C", "₹"), ("8F", "୰"), ("90", "ଽ"), ("9C", "ଙ୍କ୍ତ"),
        ("89", "ତ୍ତ୍ବ"), ("96", "ଦ୍ଧ୍ବ"), ("A3", "ନ୍ଦ୍ବ"), ("A5", "ନ୍ଧ୍ବ"),
        ("FD", "୍ବ"), ("23", "ି"), ("E3", "ଚ଼"), ("B7", "•"),
    };

    public static readonly Fam Ori7 = new("shree-ori7", "Shree-Lipi 7.0 and 4.0/5.0/6.0 Odia",
        "SHREE-ORI7-0600..0604, SHREE-ORI-0600..0604",
        new[]
        {
            ("SHREE-ORI7 (Shree-Lipi 7.0)", @"^(S060[0-4]892|WORI700[12])\.TTF$", "^SHREE-ORI7-"),
            ("SHREE-ORI (Shree-Lipi 4.0/5.0/6.0)", @"^S060[0-4]0F2\.TTF$", "^SHREE-ORI-"),
        },
        new Dictionary<string, string> { ["9C"] = "5B" },
        new Dictionary<string, string> { ["3C"] = "U+20B9", ["8F"] = "U+0B70", ["90"] = "U+0B3D" },
        "3C 8F 90 81 8D 9D AD 07", "!%()*,-./:?");

    public static readonly Fam[] All = { Ori7 };

    public static Fam Get(string id) => All.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("no family " + id);
}
