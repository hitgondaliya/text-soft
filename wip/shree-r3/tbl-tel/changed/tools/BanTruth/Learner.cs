using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree-Ban pairs of one family from its truth. It starts from
/// atoms (one pair per character), then walks the truth strings shortest first and
/// adds a pair only for a string the table so far does not already get right. So a
/// pair is in the table because composing it from smaller pieces gave the wrong
/// bytes - which keeps the table small, and keeps composition honest for strings the
/// composer was never asked about.
/// </summary>
public sealed class Learner
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules = Rules.ForwardPre();
    public readonly List<RewriteRule> PostRules;

    public Learner(Fam f)
    {
        F = f;
        PostRules = Rules.ForwardPost(f);
    }

    /// <summary>The joiners: no glyph of their own, the spelling around them carries them (র্‌ক, ক্‍).</summary>
    public static readonly string[] Dropped = { "\u200C", "\u200D" };

    /// <summary>
    /// Characters no code of these fonts draws, left unmapped: the converter passes them
    /// through and reports them, never drops them unseen. (/ = ; + * [ ] × ÷ have glyphs in
    /// the 4/5/6 fonts only at the control codes 0B-0F, 1B-1E, which no document keeps as
    /// text, and at their own codes every font draws a letter: / ব's base 2F, = থ 3D, ; ৎ 3B,
    /// + ঔ 2B, * ও 2A, [ the i-sign 5B; × ÷ are D7 F7, হু and a ra-phala. ₹ ৄ ৠ ৡ ৢ ৣ have no
    /// glyph anywhere. In some families % and : too: Family.cs.)
    /// </summary>
    public static readonly string[] NoGlyph = { "/", "=", ";", "+", "*", "[", "]", "×", "÷", "₹", "ৄ", "ৠ", "ৡ", "ৢ", "ৣ" };

    /// <summary>
    /// Characters outside the truth: as the earlier table wrote them (॥ ৷ ্ ীঁ ৗঁ), and the
    /// curly double quotes and the ellipsis - their own bytes 93 94 85 draw ন্ড ন্ত ীঁ in these
    /// fonts - drawn with the quotes and the full stop the fonts have (‘ 91, ’ 92, . 2E).
    /// </summary>
    public static readonly (string U, string Hex)[] Extra =
    {
        ("॥", "FA FA"), ("৷", "FA"), ("্", "F4"),
        ("ীঁ", "85"), ("ৗঁ", "5D"),
        ("“", "91 91"), ("”", "92 92"), ("…", "2E 2E 2E"),
        // Three-letter clusters ending in ্ব take five codes to type, more than a composer key
        // holds, so the truth has none. The fonts have ligatures for four (the codes the earlier
        // reverse table read them by; rendered in SHREE-BAN-0550 and SHREE-BAN7-0550 to check),
        // and জ্জ ন্ত take the subscript ব AB the composer writes under ত্ব (E2 AB). Without these,
        // তত্ত্ব উচ্ছ্বাস উজ্জ্বল সান্ত্বনা came out with a visible hasanta.
        ("ক্ত্ব", "44"), ("চ্ছ্ব", "59"), ("ত্ত্ব", "77"), ("দ্দ্ব", "E4"),
        ("জ্জ্ব", "F0 AB"), ("ন্ত্ব", "94 7A AB"),
    };

    public FontConverter Stager() =>
        new(Table("s", "s", "a", "b", FontType.Unicode, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));

    /// <summary>Atoms every learning run starts from.</summary>
    public Dictionary<string, string> Atoms(IEnumerable<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (u, hex) in Extra) atoms[u] = FromHex(F.Apply(hex));
        foreach (var d in Dropped) atoms[d] = Rules.Keep;
        atoms[Rules.RephMark] = FromHex(F.Code("A2"));
        atoms[Rules.YaMark] = FromHex(F.Code("B8"));
        atoms[Rules.ICandraMark] = FromHex(F.Code("7B"));
        atoms[Rules.RaMark] = FromHex(F.Code("F6"));
        atoms[Rules.AiAloneMark] = FromHex(F.Code("ED"));
        atoms[Rules.RaOwnMark] = FromHex(F.Code("F8"));
        // A character the family has no glyph for (% and : in some) gets no pair: it passes
        // through, reported.
        // A character the table passes through (its spelling is itself, U+XXXX: ৱ in a Bangla font) gets no
        // pair either: it must be reported.
        foreach (var t in train.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0)) atoms[t.U] = FromHex(t.Pref);
        // The e/ai signs as the composer writes them on a letter (alone, it also accepts EC/EE).
        atoms["ে"] = FromHex(F.Code("EC"));
        atoms["ৈ"] = FromHex(F.Code("EE"));
        return atoms;
    }

    public MappingTable Build(Dictionary<string, string> pairs) =>
        Table("unicode-to-" + F.Id, "Unicode -> " + F.Name, "vrinda", F.Id, FontType.Unicode, FontType.Legacy, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);

    public Dictionary<string, string> Learn(IEnumerable<Truth> trainSet, out string stats, List<string>? log = null)
    {
        var train = trainSet.ToList();
        var pairs = Atoms(train);
        int atoms = pairs.Count;
        var stager = Stager();
        var staged = train.ToDictionary(t => t.U, t => stager.ConvertText(t.U), StringComparer.Ordinal);
        int added = 0, conflicts = 0, kept = 0;
        foreach (var level in train.GroupBy(t => staged[t.U].Length).OrderBy(g => g.Key))
        {
            var conv = new FontConverter(Build(pairs));
            foreach (var t in level.OrderBy(t => t.U, StringComparer.Ordinal))
            {
                if (t.Pref.Length == 0) continue;   // left unmapped (a character the family cannot draw)
                // A spelling with a character passed through: no pair may hold it (it would no longer be reported).
                if (Reported(t.Pref).Count > 0) continue;
                string got = Hex(conv.ConvertText(t.U));
                if (got == t.Pref) continue;
                string key = staged[t.U];
                if (key.Length == 0) continue;
                if (pairs.TryGetValue(key, out var have) && Hex(Post(have)) != t.Pref) conflicts++;
                log?.Add($"{t.U}\t{Hex2(key)}\t{t.Pref}\tcomposed {got}");
                pairs[key] = Spell(t.Pref, ref kept);
                added++;
            }
        }
        stats = $"{F.Id}: atoms {atoms}, learned {added} ({kept} kept from the rules), conflicts {conflicts}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>A staged key, readable: marks shown by name.</summary>
    public static string Hex2(string key) => key.Replace(Rules.RephMark, "<R>").Replace(Rules.YaMark, "<Y>").Replace(Rules.ICandraMark, "<IC>").Replace(Rules.RaMark, "<RA>").Replace(Rules.AiAloneMark, "<AI>");

    FontConverter? _post;

    /// <summary>What the post rules make of a legacy string.</summary>
    public string Post(string legacy)
    {
        _post ??= new FontConverter(Table("p", "p", "a", "b", FontType.Legacy, FontType.Legacy,
            Array.Empty<RewriteRule>(), Array.Empty<(string, string)>(), PostRules));
        return _post.ConvertText(legacy);
    }

    /// <summary>
    /// The pair output for a composer spelling: as is, or kept away from the rules when they
    /// would change it - with a Keep mark only between the codes a rule would otherwise
    /// touch. Few marks matter: the app tells an 8-bit font by its table writing Windows-1252
    /// characters (90% of them at least), and the mark is not one.
    /// </summary>
    public string Spell(string prefHex, ref int kept)
    {
        string legacy = FromHex(prefHex);
        if (Post(legacy) == legacy) return legacy;
        kept++;
        string s = Rules.Protect(legacy);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != Rules.Keep[0]) continue;
            string without = s.Remove(i, 1);
            if (Post(without) == legacy) { s = without; i--; }
        }
        return s;
    }
}
