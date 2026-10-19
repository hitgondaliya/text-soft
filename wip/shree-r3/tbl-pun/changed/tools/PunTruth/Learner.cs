using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree-Pun pairs of one family from its truth. It starts from atoms (one pair per character,
/// the subjoined forms, the sign+bindi glyphs and the marks the rules place), then walks the truth strings shortest
/// first and adds a pair only for a string the table so far does not already get right. So a pair is in the table
/// because composing it from smaller pieces gave the wrong bytes - which keeps the table small, and keeps
/// composition honest for strings the composer was never asked about.
/// </summary>
public sealed class Learner
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules = Rules.ForwardPre();
    public readonly List<RewriteRule> PostRules = Rules.ForwardPost();

    public Learner(Fam f) => F = f;

    /// <summary>The joiners: no glyph of their own, the spelling around them carries them (ਕ੍‌ = 48 B5, ਕ੍‍ = 48 B5).</summary>
    public static readonly string[] Dropped = { "‌", "‍" };

    /// <summary>Gurmukhi the fonts have no glyph for: udaat, yakash, the abbreviation sign. They pass through, reported.</summary>
    public static readonly string[] NoGlyph = { "ੑ", "ੵ", "੶" };

    /// <summary>
    /// Pieces every learning run starts from, beyond the truth's single characters (all of them the composer's own
    /// codes, read off the truth): the subjoined ੍ਰ ੍ਹ ੍ਵ ੍ਯ (6F AA AB 62, after any letter: ਪ੍ਰ = 5C 6F), the sign+bindi
    /// glyphs ੀਂ 73 ੇਂ 7A ੈਂ 7C ੋਂ 7E ੌਂ A2 (ਕੀਂ = 48 73; ਾਂ stays 70 A3), ੳ + bindi 44, a virama before a
    /// ZWJ or a ZWNJ (ਕ੍‍ = ਕ੍‌ = 48 B5: the visible halant); and outside the truth ॥, which the 7 composer types
    /// as nothing (code D9): two dandas 26 26, the fonts' look-alike.
    /// </summary>
    public static readonly (string U, string Hex)[] Extra =
    {
        ("੍ਰ", "6F"), ("੍ਹ", "AA"), ("੍ਵ", "AB"), ("੍ਯ", "62"),
        ("ੀਂ", "73"), ("ੇਂ", "7A"), ("ੈਂ", "7C"), ("ੋਂ", "7E"), ("ੌਂ", "A2"),
        ("ੳਂ", "44"),
        ("੍‌", "B5"),
        // A virama before a ZWJ: Gurmukhi has no half forms, so a Unicode font draws the visible halant there, as before a
        // ZWNJ. The composer's link spelling (no glyph: C + virama + ZWJ = C alone) loses the virama (Capture.HalantBeforeZwj).
        ("੍‍", "B5"),
        ("॥", "26 26"),
        // The avagraha-like glyph 40, which the 7 composer types for code 41 (14 keys on 12 vendor keyboards; the Unicode
        // composer answers nothing for it): written for U+093D ऽ, the character it draws, so TextSoft's keys for it
        // type Shree's byte (and the reverse tables read 40 as ऽ).
        ("ऽ", "40"),
    };

    public FontConverter Stager() =>
        new(Table("s", "s", "a", "b", FontType.Unicode, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));

    /// <summary>Atoms every learning run starts from.</summary>
    public Dictionary<string, string> Atoms(IEnumerable<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        // A character the table passes through (its spelling is itself, U+XXXX: ₹ in a 4/5/6 font) gets no pair: it must be reported.
        foreach (var t in train.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0)) atoms[t.U] = FromHex(t.Pref);
        foreach (var (u, hex) in Extra) atoms[u] = FromHex(F.Apply(hex));
        foreach (var d in Dropped) atoms[d] = Rules.Keep;
        // ਠ ਨ without their spacer: the rules place it (and the marks around it).
        atoms["ਠ"] = FromHex("53");
        atoms["ਨ"] = FromHex("B2");
        atoms[Rules.Spacer] = FromHex("BE");
        atoms[Rules.ULow] = FromHex("AE");
        atoms[Rules.UuLow] = FromHex("AF");
        atoms[Rules.FarAddak] = FromHex("A8");
        atoms[Rules.RightBindi] = FromHex("A4");
        return atoms;
    }

    /// <summary>The nukta on a letter the fonts have no nukta glyph for (only \u0A59 \u0A5A \u0A5B \u0A5E \u0A33 \u0A36 have one): left out, reported - the composer writes the plain letter.</summary>
    public static readonly MappingPair Nukta = new() { From = "਼", To = "", Unmapped = true };

    public MappingTable Build(Dictionary<string, string> pairs)
    {
        var t = Table("unicode-to-" + F.Id, "Unicode -> " + F.Name, "raavi", F.Id, FontType.Unicode, FontType.Legacy, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);
        t.Pairs.Add(new MappingPair { From = Nukta.From, To = Nukta.To, Unmapped = true });
        t.Pairs = t.Pairs.OrderBy(p => p.From.Length).ThenBy(p => p.From, StringComparer.Ordinal).ToList();
        return t;
    }

    public Dictionary<string, string> Learn(IEnumerable<Truth> trainSet, out string stats, List<string>? log = null)
    {
        var train = trainSet.ToList();
        var pairs = Atoms(train);
        int atoms = pairs.Count;
        var stager = Stager();
        var staged = train.ToDictionary(t => t.U, t => stager.ConvertText(t.U), StringComparer.Ordinal);
        int added = 0, conflicts = 0;
        foreach (var level in train.GroupBy(t => staged[t.U].Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs));
            foreach (var t in level.OrderBy(t => t.U, StringComparer.Ordinal))
            {
                if (t.Pref.Length == 0) continue;
                // A spelling with a character passed through: no pair may hold it (it would no longer be reported).
                if (Reported(t.Pref).Count > 0) continue;
                string got = Hex(conv.ConvertText(t.U));
                if (got == t.Pref) continue;
                string key = staged[t.U];
                if (key.Length == 0) continue;
                if (pairs.TryGetValue(key, out var have) && Hex(have.Replace(Rules.Keep, "")) != t.Pref) conflicts++;
                log?.Add($"{t.U}\t{Show(key)}\t{t.Pref}\tcomposed {got}");
                pairs[key] = FromHex(t.Pref);
                added++;
            }
        }
        stats = $"{F.Id}: atoms {atoms}, learned {added}, conflicts {conflicts}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>A staged key, readable: the rules' marks shown by name.</summary>
    public static string Show(string key) => key.Replace(Rules.Spacer, "<BE>").Replace(Rules.ULow, "<AE>").Replace(Rules.UuLow, "<AF>")
        .Replace(Rules.FarAddak, "<A8>").Replace(Rules.RightBindi, "<A4>").Replace(Rules.Keep, "<K>");
}
