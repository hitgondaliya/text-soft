using static Common;

/// <summary>
/// The Shree-Lipi Bangla / Assamese font families and the codes each draws differently.
/// The truth (Capture.cs) is in SHREE-BAN 4/5/6 codes; a family's truth is that with
/// its own codes moved (every fact below is from the glyph compare and renders of the
/// installed faces):
///
///   Every family          no code point U+0080-U+009F is ever written: Word and RichEdit draw
///                         nothing for a C1 control code, whatever the cmap holds (Common.Wa).
///                         The truth has none (Decisions.cs 4 writes ব্ঢ ব্ড চ্ঞ, 81 8D 90, as
///                         letter + visible hasanta + letter); the Bangla families' ৱ (9E, held
///                         at U+009E) is not written at all: it passes through as U+09F1, reported
///                         (a spelling's "U+09F1" token), and Word's font fallback draws it.
///   SHREE-BAN-* (4/5/6)   the truth as it is, but : (3A) - 3A is a zero-width mark below
///                         the letter there, and no code draws a colon: : is left unmapped.
///                         The glyphs of / = ; + * × ÷ [ ] — ॥ ড়্গ and a joining ব দ sit at
///                         the control codes 08-0F, 1A-1F, which no document keeps as text
///                         (09-0D are its tab and line breaks): never written, read back -
///                         but 09-0D are read as the tab and line breaks they are in every
///                         document (by design: a document that holds them means whitespace).
///   SHREE-BAN7-*, Shree-Ban7-001/002
///                         ঃ 40 -> 96 and the half-ন hook 96 -> 40 (swapped), subscript ্ন
///                         97 -> A5 and ৺ A5 -> 97 (swapped); no zero-width filler: FF is the
///                         ligature ড়্গ, and A8 is a joining দ (দ্গ = A8 4B, দ্ঘ = A8 51); no
///                         ধ্ম or ব্ঝ glyph (8C is a ব, A8 that দ), so those are written ধ্ + ম
///                         and ব্ + ঝ; no % or : (25 and 3A are marks below the letter, or nothing).
///   SHREE-ASS-* (4/5/6)   ৱ is AF (9E is missing), no ব্ড glyph (8D missing).
///                         Here 25 is % and 3A is a colon; the control code 0F is a Bengali র.
///   SHREE-ASS7-*          both of the above; 25 and 3A have no glyph.
///   Shree-Ass7-001/002    the Assamese codes, ঃ hook ্ন ৺ in the 4/5/6 order, % and :, the 7
///                         fonts' ড়্গ FF and joining দ A8, no ধ্ম or ব্ঝ glyph.
/// </summary>
public sealed record Fam(string Id, string Name, string Fonts, string FilePattern, IReadOnlyDictionary<string, string> Map,
    (string From, string To)[] Seq, string Never, string Itself)
{
    // Itself: the ASCII punctuation every face of the family draws as itself at its own code
    // (Ascii.Complete: written as itself, not reported). From the
    // Word-visibility map of every installed face (glyph, advance, render; 2026-10-18): ! ( ) , - . ?
    // everywhere; % in SHREE-BAN, SHREE-ASS and Shree-Ass7-00x (in SHREE-BAN7 it is a zero-width
    // mark, in Shree-Ban7-00x blank, SHREE-ASS7 has none); : only in SHREE-ASS and Shree-Ass7-00x
    // (in SHREE-BAN and SHREE-BAN7 a zero-width mark - advance 5 of 1000 -, in Shree-Ban7-00x
    // blank, SHREE-ASS7 has none). Every other ASCII code but the letters and digits draws a letter
    // or a piece of one (/ ব্ভ, = থ, ; ৎ, + ঔ, * ও, [ the i-sign, " অ, ' ঐ ...).

    /// <summary>A truth spelling (4/5/6 codes) in this family's codes: codes moved one by one, then the sequences.</summary>
    public string Apply(string hex)
    {
        if (hex.Length == 0) return hex;
        string s = Norm(string.Join(" ", hex.Split(' ').Select(t => Map.TryGetValue(t, out var m) ? m : t)));
        foreach (var (from, to) in Seq) s = ReplaceSeq(s, from, to);
        return s;
    }

    /// <summary>
    /// Composer bytes as a document in this family holds them: the codes moved as <see cref="Apply"/> moves
    /// them, but a code the tables only pass a character through for (the Bangla fonts' ৱ 9E) stays the code.
    /// </summary>
    public string Doc(string hex)
    {
        if (hex.Length == 0) return hex;
        string s = Norm(string.Join(" ", hex.Split(' ').Select(t => Map.TryGetValue(t, out var m) && !m.StartsWith("U+") ? m : t)));
        foreach (var (from, to) in Seq) s = ReplaceSeq(s, from, to);
        return s;
    }

    /// <summary>Replace a run of whole codes.</summary>
    public static string ReplaceSeq(string hex, string from, string to)
    {
        if (hex.Length == 0) return hex;
        string padded = " " + hex + " ";
        return Norm(padded.Replace(" " + from + " ", " " + to + " "));
    }

    /// <summary>One code in this family (only for codes that move one to one).</summary>
    public string Code(string hex)
    {
        if (!Map.TryGetValue(hex, out var m)) return hex;
        if (m.Length != 2) throw new InvalidOperationException($"{Id}: code {hex} is not one code here ({m}).");
        return m;
    }

    /// <summary>The family's code for a 4/5/6 code that moved one to one, the other way.</summary>
    public string Back(string hex) => Map.FirstOrDefault(kv => kv.Value == hex && kv.Value.Length == 2).Key ?? hex;

    public List<Truth> Truth(IEnumerable<Truth> truth) => truth.Select(t =>
    {
        string pref = Apply(t.Pref);
        var acc = t.Acc.Select(Apply).Distinct().ToList();
        acc.Remove(pref); acc.Insert(0, pref);
        return new Truth(t.U, pref, acc.ToArray());
    }).ToList();

    /// <summary>Codes this family's tables must never write: they draw something else here, or nothing.</summary>
    public HashSet<string> NeverCodes => Never.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public bool IsAssamese => Map.TryGetValue("9E", out var wa) && wa == "AF";
    public bool IsSeven => Map.ContainsKey("8C");
    public bool IsFourFiveSix => !IsSeven;

    /// <summary>The ra this family's text is written with: ৰ in Assamese, র in Bangla.</summary>
    public char Ra => IsAssamese ? 'ৰ' : 'র';

    /// <summary>
    /// Codes the composer writes only as another accepted spelling, or never, that the reverse
    /// table still reads, as the family draws them: EB is the other e-sign glyph, EE the ai-sign
    /// on a letter (alone the composer writes ED), E6 E9 the ু ূ under a subscript (under a
    /// conjunct with its ra-phala, স্ট্রু = D0 A1 F6 E6, no pair holds them); in the 7 fonts 8C
    /// and A8 are a ব and a দ (and 8B 8C, A4 8C are how the earlier table wrote ধ্ব ব্ব); in the
    /// 4/5/6 fonts the glyphs at control codes a document could carry, and the filler FF.
    /// </summary>
    public (string Hex, string Unicode)[] FontCodes =>
        new[] { ("EB", "ে"), ("EE", "ৈ"), ("E6", "ু"), ("E9", "ূ"),
                // the composer's ব্ঢ ব্ড চ্ঞ, at C1 code points (U+0081 U+008D U+0090): no table writes them
                ("81", "ব্ঢ"), ("8D", "ব্ড"), ("90", "চ্ঞ") }
            // ৱ: the Bangla fonts' 9E (U+009E, or U+017E from a .txt file), and U+09F1 itself, as the tables pass it through.
            // The Assamese fonts draw ৱ at AF and have no 9E, but a document of the same script that holds 9E means ৱ too.
            .Concat(new[] { ("9E", "ৱ") })
            .Concat(IsAssamese ? Array.Empty<(string, string)>() : new[] { ("U+09F1", "ৱ") })
            .Concat(IsSeven ? new[] { ("8C", "ব"), ("A8", "দ"), ("8B 8C", "ধ্ব"), ("A4 8C", "ব্ব") } : Array.Empty<(string, string)>())
            // Shree's 7 composer writes its dashes with the 4/5/6 filler between the hyphens (– = 2D FF 2D, — = 2D FF 2D FF 2D),
            // where the 7 fonts draw ড়্গ at FF: read as the dashes (P5, round 2; a lone FF is still ড়্গ).
            .Concat(IsSeven ? new[] { ("2D FF 2D", "–"), ("2D FF 2D FF 2D", "—") } : Array.Empty<(string, string)>())
            // AF is the ligature ব্ল in a Bangla font (the composer writes it A4 C3); in an Assamese one it is ৱ.
            .Concat(IsAssamese ? Array.Empty<(string, string)>() : new[] { ("AF", "ব্ল") })
            // Half letters and subscripts the composer only writes inside a conjunct (ম্প = B4 9A, ল্ট = C2 69 A1,
            // প্ট = 9F 69 A1, ত্ন = E2 97, চ্চ = 5A 57 A1, গ্ম = 4F B5, ম্ব = B4 AC), read alone too: the earlier
            // table wrote them in other company (প্ম = 9F B5, চ্ব = 5A AB), and a document it made reads them
            // (a hasanta twice, half letter + subscript, is one: ReversePost).
            .Concat(new[] { ("B4", "ম্"), ("C2", "ল্"), ("9F", "প্"), ("E2", "ত্"), ("5A", "চ্"), ("B5", "্ম"), ("AC", "্ব") })
            // The subscript ত 7A (স্ত = D1 7A, ন্ত = 94 7A) and ন 95 (স্ন = D1 95): zero-width pieces in every face, read
            // alone as the subscripts they draw - the table shipped before the 7 / 4/5/6 split wrote ম্ত as B4 7A and
            // ম্ন as B4 95, which read ম্z and ম্• (its z and • at those codes were reported as unmapped when written).
            .Concat(new[] { ("7A", "্ত"), ("95", "্ন") })
            // 09-0D are tab, line feed, vertical tab (Word's line break), form feed and carriage
            // return in every document: read as what they are, never as দ ড়্গ / = ;.
            .Concat(IsFourFiveSix ? new[]
            {
                ("08", "ব"), ("0E", "+"), ("0F", IsAssamese ? Reader.BengaliRa : "*"), ("1A", "—"), ("1B", "×"), ("1C", "÷"),
                ("1D", "["), ("1E", "]"), ("1F", "॥"),
                // The filler, between two letters in what Shree's composer writes (দ্গ = 83 FF 4B): it
                // draws nothing, the composer means the conjunct. (Between hyphens it is a dash: a pair.)
                ("FF", "্"),
            } : Array.Empty<(string, string)>())
            // 96 is the half-ন hook where the family keeps the 4/5/6 order (SHREE-BAN, SHREE-ASS, Shree-Ass7-00x),
            // which only ever comes before its letter (ন্দ = 96 83). Alone it is what the earlier table wrote for ঃ in
            // every Bangla font (the 7 fonts' code): read so.
            .Concat(Map.ContainsKey("96") ? Array.Empty<(string, string)>() : new[] { ("96", "ঃ") })
            .ToArray();

    /// <summary>Control codes 08-1F hold glyphs in the 4/5/6 fonts, but no document keeps them as text.</summary>
    const string Controls = "08 09 0A 0B 0C 0D 0E 0F 1A 1B 1C 1D 1E 1F";

    /// <summary>The codes Windows-1252 leaves undefined (81 8D 8F 90 9D) and 9E as the fonts hold it (U+009E): C1 code points, drawn by nothing.</summary>
    const string C1 = "81 8D 8F 90 9D 9E";

    static readonly Dictionary<string, string> Seven = new()
    {
        ["40"] = "96", ["96"] = "40", ["97"] = "A5", ["A5"] = "97",
        ["FF"] = "", ["8C"] = "8B F4 B3", ["A8"] = "A4 F4 63",
    };
    static readonly Dictionary<string, string> SevenGlyphs = new() { ["FF"] = "", ["8C"] = "8B F4 B3", ["A8"] = "A4 F4 63" };
    static readonly Dictionary<string, string> Assam = new() { ["9E"] = "AF" };
    /// <summary>The Bangla fonts hold ৱ only at U+009E, a C1 code nothing draws: ৱ passes through, reported.</summary>
    static readonly Dictionary<string, string> BanglaWa = new() { ["9E"] = "U+09F1" };
    static readonly Dictionary<string, string> NoPercent = new() { ["25"] = "" };
    static readonly Dictionary<string, string> NoColon = new() { ["3A"] = "" };

    /// <summary>The 7 fonts' own conjuncts for what the 4/5/6 fonts write with a visible hasanta (Decisions.cs 2).</summary>
    static readonly (string, string)[] SevenConjuncts = { ("D8 6C F4 A1 4B", "FF"), ("83 F4 4B", "A8 4B"), ("83 F4 51", "A8 51") };

    static Dictionary<string, string> Join(params Dictionary<string, string>[] maps)
    {
        var d = new Dictionary<string, string>();
        foreach (var m in maps) foreach (var (k, v) in m) d[k] = v;
        return d;
    }

    public static readonly Fam Ban456 = new("shree-ban456", "Shree-Lipi 4.0/5.0/6.0 Bangla", "SHREE-BAN-*", @"^S05\d\d0F2\.TTF$",
        Join(NoColon, BanglaWa), Array.Empty<(string, string)>(), "3A " + C1 + " " + Controls, "!%(),-.?");
    public static readonly Fam Ban7 = new("shree-ban7", "Shree-Lipi 7.0 Bangla", "SHREE-BAN7-*, Shree-Ban7-001/002", @"^(S05\d\d892|WBAN700[12])\.TTF$",
        Join(Seven, NoPercent, NoColon, BanglaWa), SevenConjuncts, "8C 25 3A " + C1 + " " + Controls, "!(),-.?");
    public static readonly Fam Ass456 = new("shree-ass456", "Shree-Lipi 4.0/5.0/6.0 Assamese", "SHREE-ASS-*", @"^S05\d\d0F6\.TTF$",
        Assam, Array.Empty<(string, string)>(), C1 + " " + Controls, "!%(),-.:?");
    public static readonly Fam Ass7 = new("shree-ass7", "Shree-Lipi 7.0 Assamese", "SHREE-ASS7-*", @"^S05\d\d896\.TTF$",
        Join(Seven, Assam, NoPercent, NoColon), SevenConjuncts, "8C 25 3A " + C1 + " " + Controls, "!(),-.?");
    public static readonly Fam Ass7W = new("shree-ass7w", "Shree-Lipi 7.0 Assamese (Shree-Ass7-001/002)", "Shree-Ass7-001/002", @"^WASS700[12]\.TTF$",
        Join(SevenGlyphs, Assam), SevenConjuncts, "8C " + C1 + " " + Controls, "!%(),-.:?");

    public static readonly Fam[] All = { Ban7, Ban456, Ass7, Ass456, Ass7W };

    public static Fam Get(string id) => All.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("no family " + id);
}
