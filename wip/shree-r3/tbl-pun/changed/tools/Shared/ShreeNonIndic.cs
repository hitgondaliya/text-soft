// Shared by tools/Guj7Truth, tools/ShreeDevTruth and tools/BanTruth (linked into each project).

using System.Globalization;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;

/// <summary>
/// Text that is not Indic - typographic punctuation, symbols, accented Latin letters - in
/// a Shree 7 or 4/5/6 font. These are 8-bit fonts: every code of the Windows-1252 range
/// is a glyph of the script (“ is 93, which draws ન in SHREE-GUJ7 and ઙ્ગ in SHREE-GUJ;
/// © is A9, the vowel sign ૃ). A character with no pair passes through the converter as
/// itself, so it would print that glyph. The policy, the same for every table:
/// <list type="number">
/// <item>Punctuation the fonts can show is written as the look-alike they draw: “ ” as
///   two single quotes (as the Devanagari composer writes them), … as three periods,
///   • as the font's own bullet B7, ‚ „ as commas. (– — ‘ ’ × ÷ already have the
///   composer's own codes where the script has them.)</item>
/// <item>Any other character whose code the fonts map is reported as unmapped and left
///   out - never written as the letter its code draws (a pair marked "unmapped").</item>
/// <item>Everything else passes through unmapped and is reported, as before: the fonts
///   have no glyph for it, so it is drawn in a fallback font, as itself.</item>
/// <item>P3b (2026-10-18): so does a character NO code of the fonts draws visibly - ₹ (the
///   composer's 3C has no glyph), ૱, ૰ (its glyph sits at 8F, which Word draws as nothing), the
///   vocalic-l letters and signs the fonts have no glyph for: passed through as itself and reported,
///   never left out, because Word draws it in a fallback font and the typist sees the right
///   character. Left out (and reported) stays only for a character whose own code point the fonts
///   map to another glyph (Latin letters, '&lt;' - Shree's rupee code -, '[', °, which is B0) and for
///   the control characters (<see cref="Controls"/>), which Word draws as nothing.</item>
/// </list>
/// Printable ASCII and the codes Word cannot draw follow <see cref="Policy"/> (P2-P4, 2026-10-18):
/// ASCII the fonts draw as itself is written as itself, any other ASCII as its look-alike or
/// left out and reported (a Latin letter's code draws a letter of the script, so English
/// text is set in an English font), and a code Word draws as nothing is never written.
/// </summary>
public static class NonIndic
{
    /// <summary>
    /// The code points beyond ASCII that the (3,1) cmap of the fonts of each family maps -
    /// read from every SHREE-GUJ7-*, Shree-Guj7-*, SHREE-GUJ-*, SHREE-DEV7-*, Shree-Dev7-*,
    /// SHREE-DEV-* and SHREE-SAN7-* font in C:\Windows\Fonts (2026-10-12; all fonts of a
    /// family map the same set): the Windows-1252 range as Windows maps it, the unassigned
    /// bytes 81 8D 8F 90 (9D) as their C1 code points, and U+2219 (a second bullet). The
    /// 4/5/6 Devanagari fonts add U+0120-012F (signs) and U+0200-021C (ligatures). The
    /// Sanskrit fonts lack the C1 codes and U+00AD.
    /// </summary>
    public static SortedSet<char> FontCodes(string family)
    {
        var set = new SortedSet<char>();
        for (char c = '\u00A0'; c <= '\u00FF'; c++) set.Add(c);
        foreach (char c in "\u0152\u0153\u0160\u0161\u0178\u0192\u02C6\u02DC\u2013\u2014\u2018\u2019\u201A\u201C\u201D\u201E\u2020\u2021\u2022\u2026\u2030\u2039\u203A\u2122\u2219")
            set.Add(c);
        switch (family)
        {
            case "guj7" or "guj456":
                foreach (char c in "\u0081\u008D\u008F\u0090") set.Add(c);
                break;
            case "dev7" or "dev456":
                foreach (char c in "\u0081\u008D\u008F\u0090\u009D") set.Add(c);
                if (family == "dev456")
                {
                    for (char c = '\u0120'; c <= '\u012F'; c++) set.Add(c);
                    for (char c = '\u0200'; c <= '\u021C'; c++) set.Add(c);
                }
                break;
            case "san7":
                set.Remove('\u00AD');
                break;
            default:
                throw new ArgumentException("unknown family " + family);
        }
        return set;
    }

    /// <summary>
    /// Codes whose glyph is blank or draws the character itself, in every font of these
    /// families: A0 and AD are empty, B7 and U+2219 a bullet ●. Passing them through is right.
    /// </summary>
    public static readonly HashSet<char> Harmless = new() { '\u00A0', '\u00AD', '\u00B7', '\u2219' };

    /// <summary>Typographic punctuation and the codes of the look-alike the fonts draw (hex); q1 q2 are the font's single quotes ‘ ’.</summary>
    public static (char C, string Hex, string Why)[] LookAlikes(string q1, string q2) => new[]
    {
        ('\u201C', $"{q1} {q1}", "“: two single quotes ‘‘"),
        ('\u201D', $"{q2} {q2}", "”: two single quotes ’’"),
        ('\u2026', "2E 2E 2E", "…: three periods"),
        ('\u2022', "B7", "•: the font's bullet ● (B7)"),
        ('\u201A', "2C", "‚: a comma"),
        ('\u201E', "2C 2C", "„: two commas"),
    };

    /// <summary>
    /// Characters written as a glyph of the fonts that is like them but not them - reported, as every
    /// family does (round 2: the Bangla tables wrote · unreported, the others reported it with no pair):
    /// · (U+00B7) and ∙ (U+2219) as B7, which every face draws as a large bullet ●, not a middle dot.
    /// ∙ was passed through as itself, which a .txt cannot hold (P6).
    /// </summary>
    public static readonly (char C, string Hex, string Why)[] StandIns =
    {
        ('·', "B7", "·: the fonts' B7 is a large bullet ●, not a middle dot - written as a stand-in, reported"),
        ('∙', "B7", "∙: the same bullet ● at B7 (U+2219 draws it too, but a .txt has no byte for U+2219) - a stand-in, reported"),
    };

    /// <summary>
    /// Completes a Unicode -> Shree table: a pair for every font code a non-Indic character
    /// would otherwise pass through onto - its look-alike, or nothing, reported as unmapped.
    /// Characters the table already has a pair for keep it. The pairs stay in the house order.
    /// </summary>
    public static void Complete(MappingTable table, string family, string q1, string q2) =>
        Complete(table, FontCodes(family), q1, q2);

    /// <summary>
    /// <see cref="Complete(MappingTable, string, string, string)"/> for a family whose code points
    /// beyond ASCII are given (read from its fonts' cmaps: tools/BanTruth for Bangla / Assamese).
    /// </summary>
    public static void Complete(MappingTable table, IEnumerable<char> fontCodes, string q1, string q2)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        var look = LookAlikes(q1, q2).ToDictionary(l => l.C, l => l.Hex);
        foreach (var (c, hex, _) in StandIns)
            if (have.Add(c.ToString()))
                table.Pairs.Add(new MappingPair { From = c.ToString(), To = Common.FromHex(hex), Unmapped = true });
        foreach (char c in fontCodes.Where(c => c > '\u007F' && !IsIndic(c)).Distinct())
        {
            if (have.Contains(c.ToString()) || Harmless.Contains(c)) continue;
            table.Pairs.Add(look.TryGetValue(c, out var hex)
                ? new MappingPair { From = c.ToString(), To = Common.FromHex(hex) }
                : new MappingPair { From = c.ToString(), To = "", Unmapped = true });
        }
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
    }

    // ---------- the output policy (P2-P4), 2026-10-18 ----------

    /// <summary>
    /// Printable ASCII in the fonts of each family, read from the Word-visibility map of
    /// 2026-10-18 (every SHREE-GUJ7-*, Shree-Guj7-*, SHREE-GUJ-*, SHREE-DEV7-*, Shree-Dev7-*,
    /// SHREE-DEV-* and SHREE-SAN7-* face; what each code draws): the codes that do NOT draw
    /// their own character. Every letter draws a letter of the script; every digit draws the
    /// script's own digit (which is how Shree writes digits); every other printable ASCII
    /// character draws itself.
    /// </summary>
    static string NotItself(string family) => family switch
    {
        // " \u2018  # \u00D7  $ blank  & danda bar  ' \u2019  * \u2605 (SHREE-GUJ7; * in Shree-Guj7-00x)  : \u0A83 (a colon's two dots)
        // < no glyph  > blank  @ \u0ABD  [ \u0ABF  \ \u0AA5  ] \u0AC0\u0A82  ^ \u0AA7  _ \u0AA8  ` \u0AAA  { \u0AB0\u0AC1  | \u0AC2  } \u0AB0\u0ACD\u0AC0  ~ the new ruu
        "guj7" or "guj456" => "\"#$&'*:<>@[\\]^_`{|}~",
        // " \u2018  # \u091E\u094D\u091A\u094D  $ blank  & \u0964  ' \u2019  < no glyph  > blank  @ \u093D  [ \u093F  \ \u092B  ] \u0930\u094D\u0947\u0902  ^ \u092D  _ \u092E  ` \u092F  { \u093F  | \u0947\u0902  } \u0930\u094D\u0947  ~ \u092C
        "dev7" or "dev456" => "\"#$&'<>@[\\]^_`{|}~",
        // " a piece of \u092D  # \u0903  $ \u0964  % \u0944  & \u093D  ' \u091B\u094D\u0935  < no glyph  > blank  @ \u0915  [ \u093F  \ \u0935\u094D  ] \u0951  ^ \u0937\u094D  _ \u0938\u094D  ` \u0939  { \u093F  | \u0930\u094D\u0902  } \u1CDA  ~ \u094D\u0930
        "san7" => "\"#$%&'<>@[\\]^_`{|}~",
        _ => throw new ArgumentException("unknown family " + family),
    };

    /// <summary>
    /// What a table writes for one printable ASCII character (hex; null = left out and reported;
    /// "=" = written as itself but reported, because the fonts have no glyph for it), and why.
    /// </summary>
    public static (string? Hex, string Why) Ascii(char c, string family, string q1, string q2)
    {
        string hex = ((int)c).ToString("X2");
        bool sanskrit = family == "san7";
        if (char.IsAsciiLetter(c)) return (null, "P3: a Latin letter's code draws a letter of the script; English is set in an English font");
        if (char.IsAsciiDigit(c)) return (hex, "P2: the fonts draw the script's own digit here, as Shree writes digits");
        if (!NotItself(family).Contains(c)) return (hex, "P2: the fonts draw it as itself");
        return c switch
        {
            '"' => ($"{q1} {q1}", "P3: the code draws a single quote (or a letter); written as two of the fonts' opening single quotes"),
            '\'' when !sanskrit => (hex, "P2: the code draws \u2019, the apostrophe's look-alike"),
            '\'' => (q2, "P3: the code draws \u091B\u094D\u0935; written as the fonts' closing single quote"),
            '*' => (hex, "P2: the code draws a star (SHREE-GUJ7-*) or an asterisk (Shree-Guj7-*)"),
            ':' => (hex, "P2: the code draws the two dots of a colon (Shree writes both colon and visarga there)"),
            '<' => (null, "P3: no glyph in any of the fonts (Word draws a fallback '<', RichEdit a box), and Shree's Gujarati composer writes its rupee sign there (code F8 = 3C)"),
            _ => (null, "P3: the code draws something else in these fonts (a letter, a sign, a blank)"),
        };
    }

    /// <summary>
    /// Unicode whitespace beyond ASCII, U+00A0 and the joiners, that no SHREE-GUJ7/GUJ/DEV7/DEV/SAN7 face maps
    /// (round 2, r3\guj-check1): NEL, the Ogham space, the en quad to the hair space, the line and paragraph
    /// separators, the narrow no-break, medium mathematical and ideographic spaces.
    /// </summary>
    public static readonly char[] OtherSpaces =
    {
        '\u0085', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008',
        '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000',
    };

    /// <summary>The C1 code points Word and RichEdit draw as nothing (advance kept, no ink) - Word-visibility map 2026-10-18.</summary>
    public static readonly HashSet<char> InvisibleInWord = new() { '\u0081', '\u008D', '\u008F', '\u0090', '\u009D' };

    /// <summary>
    /// The control characters a table leaves out and reports (P4, 2026-10-18 merge): C0 but TAB, LF, VT, FF
    /// and CR (whitespace, kept), DEL, and C1. Word draws none of them (a C1 code point no face maps falls
    /// back and draws nothing too), and a .txt writes U+0080-U+009F as their raw bytes, which every Shree
    /// font draws as letters (a stray U+0092 - ’ mis-decoded from Windows-1252 - is byte 92, થ in SHREE-GUJ7).
    /// A control a family's fonts do map (U+0081 in SHREE-GUJ7) already has its pair (see <see cref="Complete"/>).
    /// </summary>
    public static IEnumerable<char> Controls() =>
        Enumerable.Range(0x00, 0x20).Where(i => i is < 0x09 or > 0x0D).Append(0x7F).Concat(Enumerable.Range(0x80, 0x20)).Select(i => (char)i);

    /// <summary>A pair "left out, reported" for every <see cref="Controls"/> character the table has no pair for; one log line.</summary>
    public static List<string> LeaveOutControls(MappingTable table)
    {
        var have = new HashSet<string>(table.Pairs.Select(p => p.From), StringComparer.Ordinal);
        int n = 0;
        foreach (char c in Controls())
        {
            if (have.Contains(c.ToString())) continue;
            table.Pairs.Add(new MappingPair { From = c.ToString(), To = "", Unmapped = true });
            n++;
        }
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
        return new List<string> { $"{n} control characters (C0 but TAB LF VT FF CR, DEL, C1) -> left out, reported: Word draws them as nothing, and a .txt would write U+0080-U+009F as bytes the fonts draw as letters" };
    }

    /// <summary>
    /// P3b: the forward table as Shree's composer spells the text - each pair that passes a
    /// <paramref name="noGlyph"/> character through writes nothing for it instead. The checks that
    /// compare with the composer's bytes run on this; <see cref="CheckPassThrough"/> proves the real
    /// table is exactly this plus the characters themselves.
    /// </summary>
    public static MappingTable AsComposer(MappingTable table, IEnumerable<string> noGlyph)
    {
        var copy = JsonSerializer.Deserialize<MappingTable>(JsonSerializer.Serialize(table, ConfigLoader.JsonOptions), ConfigLoader.JsonOptions)!;
        var set = noGlyph.ToHashSet(StringComparer.Ordinal);
        foreach (var p in copy.Pairs.Where(p => set.Contains(p.From)))
            p.To = p.To.Replace(p.From, "", StringComparison.Ordinal);
        return copy;
    }

    /// <summary>
    /// P5 for our own output (2026-10-18 merge, round 2): the characters of the script's block (from
    /// <paramref name="blockStart"/>, 128 code points) and ₹ that the forward table writes as themselves - P3b,
    /// no code of the fonts draws them; by a pair or with none - so the reverse table reads each as itself,
    /// unreported: reading back a document we wrote, ₹ or ૢ in a fallback font is text, not a code it cannot read.
    /// </summary>
    public static List<string> PassedThrough(FontConverter forward, int blockStart)
    {
        var list = new List<string>();
        foreach (char c in Enumerable.Range(blockStart, 0x80).Append(0x20B9).Select(i => (char)i))
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.OtherNotAssigned) continue;
            if (forward.ConvertText(c.ToString()).Contains(c)) list.Add(c.ToString());
        }
        return list;
    }

    /// <summary>
    /// P3b: every input through the real table gives the composer's spelling (<paramref name="asComposer"/>)
    /// with each no-glyph character of the input passed through as itself - as many as were typed, reported,
    /// and nothing else changed.
    /// </summary>
    public static string CheckPassThrough(FontConverter real, FontConverter asComposer, IEnumerable<string> inputs, IReadOnlyCollection<string> noGlyph, out List<string> fails)
    {
        fails = new List<string>();
        var chars = noGlyph.Where(u => u.Length == 1).Select(u => u[0]).ToHashSet();
        int n = 0, with = 0;
        foreach (var u in inputs)
        {
            n++;
            var r = real.Convert(u);
            string composer = asComposer.ConvertText(u);
            string stripped = new(r.Text.Where(c => !chars.Contains(c)).ToArray());
            int typed = u.Count(chars.Contains), written = r.Text.Count(chars.Contains);
            if (typed > 0) with++;
            if (stripped != composer || typed != written || (typed > 0 && !u.Where(chars.Contains).All(c => r.UnmappedCharacters.Contains(c.ToString()))))
                fails.Add($"{u} [{string.Join(" ", u.Select(c => ((int)c).ToString("X4")))}]: writes {Common.Hex(r.Text)}, the composer's spelling {Common.Hex(composer)}{(typed > 0 && !r.HasUnmapped ? ", not reported" : "")}");
        }
        return $"P3b no-glyph characters passed through, {n} strings ({with} with one): the composer's bytes plus the characters themselves, reported; wrong {fails.Count}";
    }

    /// <summary>
    /// Applies the output policy to a finished Unicode -> Shree table (after <see cref="Complete"/>):
    /// <list type="bullet">
    /// <item>P2/P3: a pair for every printable ASCII character (see <see cref="Ascii"/>) - unless
    ///   the table already has one that writes no invisible code.</item>
    /// <item>P4: a pair that writes a code Word draws as nothing takes the spelling in
    ///   <paramref name="overrides"/> (hex; null = left out and reported; "=" = passed through as
    ///   itself and reported, P3b); a pair with no override throws, so every such character gets a decision.</item>
    /// <item>P3b: the characters in <paramref name="noGlyph"/> (the fonts draw nothing for them) are
    ///   passed through as themselves and reported, never left out: their pair keeps what it wrote
    ///   (U+FDD1, which the rules need to keep the signs around them in place) and writes the
    ///   character after it.</item>
    /// <item>P4: the control characters are left out and reported (<see cref="LeaveOutControls"/>).</item>
    /// </list>
    /// Returns one line per decision, for the build log.
    /// </summary>
    public static List<string> Policy(MappingTable table, string family, string q1, string q2,
        IReadOnlyDictionary<string, (string? Hex, bool Report, string Why)> overrides, IEnumerable<string> noGlyph)
    {
        var log = new List<string>();
        var byFrom = table.Pairs.ToDictionary(p => p.From, StringComparer.Ordinal);
        for (char c = '!'; c <= '~'; c++)
        {
            string s = c.ToString();
            if (byFrom.TryGetValue(s, out var have) && !have.To.Any(InvisibleInWord.Contains) && !overrides.ContainsKey(s)) continue;
            var (hex, why) = Ascii(c, family, q1, q2);
            var p = hex is null ? new MappingPair { From = s, To = "", Unmapped = true }
                  : hex == "=" ? new MappingPair { From = s, To = s, Unmapped = true }
                  : new MappingPair { From = s, To = Common.FromHex(hex) };
            if (have is not null) table.Pairs.Remove(have);
            table.Pairs.Add(p);
            byFrom[s] = p;
            if (hex != ((int)c).ToString("X2")) log.Add($"{s} -> {(hex is null ? "left out, reported" : hex == "=" ? "itself, reported" : hex)}: {why}");
        }
        // P3/P6: the Unicode spaces no face of these families has a glyph for passed through unreported (the
        // converter lets whitespace by), and a .txt can only write them as '?' - U+0085 as its raw byte, a letter
        // of the script. They are written as the fonts' own space; the line and paragraph separators, which a run
        // of text cannot hold, as a space too, and reported.
        foreach (char c in OtherSpaces)
        {
            string s = c.ToString();
            if (byFrom.ContainsKey(s)) continue;
            bool isBreak = c is '\u0085' or '\u2028' or '\u2029';
            var p = new MappingPair { From = s, To = " ", Unmapped = isBreak };
            table.Pairs.Add(p);
            byFrom[s] = p;
            log.Add($"U+{(int)c:X4} -> 20{(isBreak ? ", reported: a line break the text cannot hold here" : ": the fonts' space (P3 look-alike)")}");
        }
        foreach (var (from, (hex, report, why)) in overrides)
        {
            var p = hex is null ? new MappingPair { From = from, To = "", Unmapped = true }
                  : hex == "=" ? new MappingPair { From = from, To = from, Unmapped = true }
                  : new MappingPair { From = from, To = Common.FromHex(hex), Unmapped = report };
            if (byFrom.TryGetValue(from, out var have)) table.Pairs.Remove(have);
            table.Pairs.Add(p);
            byFrom[from] = p;
            log.Add($"{from} [{string.Join(" ", from.Select(ch => ((int)ch).ToString("X4")))}] -> {(hex is null ? "left out, reported" : hex == "=" ? "itself, reported" : hex + (p.Unmapped ? ", reported" : ""))}: {why}");
        }
        foreach (var u in noGlyph)
        {
            if (!byFrom.TryGetValue(u, out var p))
                throw new InvalidOperationException($"{family}: no pair for the no-glyph character {u}");
            if (!p.To.Contains(u, StringComparison.Ordinal)) p.To += u;
            p.Unmapped = true;
            log.Add($"{u} [{string.Join(" ", u.Select(ch => ((int)ch).ToString("X4")))}] -> itself, reported (P3b): the fonts have no glyph for it; Word draws it in a fallback font");
        }
        log.AddRange(LeaveOutControls(table));
        var invisible = table.Pairs.Where(p => p.To.Any(InvisibleInWord.Contains)).ToList();
        if (invisible.Count > 0)
            throw new InvalidOperationException($"{family}: pairs that write a code Word draws as nothing and have no decision: " +
                string.Join(", ", invisible.Select(p => p.From + " -> " + Common.Hex(p.To))));
        foreach (var r in table.PostRules.Where(r => r.Replace.Any(InvisibleInWord.Contains)))
            throw new InvalidOperationException($"{family}: a post rule writes a code Word draws as nothing: {r.Pattern} -> {r.Replace}");
        table.Pairs = table.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
        return log;
    }

    /// <summary>
    /// Printable ASCII alone and between two letters: written as <see cref="Ascii"/> says, reported when
    /// it says so, and never as a code the reverse table reads as Indic text (but a digit, which the fonts
    /// draw as the script's digit, and the colon, whose code Shree writes for the visarga too).
    /// </summary>
    public static string CheckAscii(FontConverter forward, FontConverter reverse, string family, string q1, string q2, string letter, out List<string> fails)
    {
        fails = new List<string>();
        string alone = forward.ConvertText(letter);
        int itself = 0, look = 0, reported = 0;
        for (char c = '!'; c <= '~'; c++)
        {
            var (hex, _) = Ascii(c, family, q1, q2);
            var r = forward.Convert(c.ToString());
            bool rep = r.UnmappedCharacters.Contains(c.ToString());
            string want = hex is null ? "" : hex == "=" ? c.ToString() : Common.FromHex(hex);
            if (r.Text != want) fails.Add($"{c}: writes {Common.Hex(r.Text)}, want {Common.Hex(want)}");
            if ((hex is null || hex == "=") != rep) fails.Add($"{c}: {(rep ? "reported" : "not reported")}");
            if (hex is not null && hex != "=" && !char.IsAsciiDigit(c) && c != ':' && reverse.ConvertText(r.Text).Any(IsIndic))
                fails.Add($"{c}: writes {Common.Hex(r.Text)}, which reads {reverse.ConvertText(r.Text)}");
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + r.Text + alone) fails.Add($"{c}: between two letters {Common.Hex(mid)}");
            if (hex is null || hex == "=") reported++; else if (r.Text == c.ToString()) itself++; else look++;
        }
        return $"printable ASCII 94: written as itself {itself}, as a look-alike {look}, left out or passed and reported {reported}; wrong {fails.Count}";
    }

    static bool IsIndic(char c) => c >= '\u0900' && c <= '\u0DFF';

    /// <summary>
    /// Every BMP character outside ASCII and the Indic blocks (not the surrogates or the
    /// noncharacters, which the tables use as their own markers and are never text), alone
    /// and between two letters: no code the table writes for it may be one the fonts draw
    /// as an Indic glyph - one the reverse table reads as Indic text, or a font code the
    /// reverse table cannot read at all - and what the table leaves out is reported.
    /// </summary>
    public static string Check(FontConverter forward, FontConverter reverse, string family, string letter, out List<string> fails) =>
        Check(forward, reverse, FontCodes(family), letter, out fails);

    /// <summary>
    /// <see cref="Check(FontConverter, FontConverter, string, string, out List{string})"/> for a family whose
    /// code points are given; <paramref name="blank"/> are more codes whose glyph is blank in every font of
    /// the family (the zero-width filler FF of the Bangla 4/5/6 fonts, in the composer's dash 2D FF 2D).
    /// </summary>
    public static string Check(FontConverter forward, FontConverter reverse, ISet<char> codes, string letter, out List<string> fails,
        string blank = "")
    {
        fails = new List<string>();
        int n = 0, lookAlike = 0, reported = 0, passed = 0;
        string alone = forward.ConvertText(letter);
        for (int cp = 0x80; cp <= 0xFFFD; cp++)
        {
            char c = (char)cp;
            if (IsIndic(c) || char.IsSurrogate(c) || (cp >= 0xFDD0 && cp <= 0xFDEF)) continue;
            n++;
            var r = forward.Convert(c.ToString());
            foreach (char o in r.Text)
            {
                if (blank.Contains(o)) continue;
                string read = reverse.ConvertText(o.ToString());
                if (read.Any(IsIndic)) fails.Add($"U+{cp:X4} {c}: writes {Common.Hex(o.ToString())}, which reads {read}");
                else if (codes.Contains(o) && !Harmless.Contains(o) && reverse.Convert(o.ToString()).HasUnmapped)
                    fails.Add($"U+{cp:X4} {c}: writes {Common.Hex(o.ToString())}, a font code the reverse table cannot read");
            }
            bool left = r.Text.Length == 0 && cp is not (0x200C or 0x200D) && !(cp >= 0xFE00 && cp <= 0xFE0F);
            if (left && !r.UnmappedCharacters.Contains(c.ToString())) fails.Add($"U+{cp:X4} {c}: left out without being reported");
            // P3b: left out only when its own code point is one of the fonts' (it would print another glyph) or a control.
            var cat = char.GetUnicodeCategory(c);
            if (left && !codes.Contains(c) && cat is not (UnicodeCategory.Control or UnicodeCategory.Format) && !char.IsWhiteSpace(c))
                fails.Add($"U+{cp:X4} {c}: left out, though no code of the fonts is its own - P3b: pass it through, reported");
            if (cat == UnicodeCategory.Control && r.Text.Contains(c)) fails.Add($"U+{cp:X4}: a control character written as itself (Word draws nothing; a .txt writes its byte)");
            if (left) reported++;
            else if (r.HasUnmapped) passed++;
            else if (r.Text != c.ToString()) lookAlike++;
            // Between two letters: the same codes, and nothing reaches into the letters.
            string mid = forward.ConvertText(letter + c + letter);
            if (mid != alone + r.Text + alone) fails.Add($"U+{cp:X4} {c}: between two letters {Common.Hex(mid)}, alone {Common.Hex(r.Text)}");
        }
        // C0 and DEL (the loop above starts at U+0080): left out and reported, never written.
        foreach (char c in Controls().Where(c => c < '\u0080'))
        {
            var r = forward.Convert(c.ToString());
            if (r.Text.Length > 0 || !r.UnmappedCharacters.Contains(c.ToString()))
                fails.Add($"U+{(int)c:X4}: a control character writes {Common.Hex(r.Text)}{(r.HasUnmapped ? "" : ", not reported")} - left out and reported");
        }
        return $"non-Indic characters {n}: written as a look-alike or the font's own code {lookAlike}, left out and reported {reported}, passed through and reported {passed}; wrong {fails.Count}";
    }
}
