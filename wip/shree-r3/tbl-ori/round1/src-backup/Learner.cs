using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree-Ori pairs from the truth. It starts from atoms (one pair per character, the subjoined
/// forms and what follows a hanging one, the marks the rules place), then walks the truth strings shortest first and
/// adds a pair only for a string the table so far does not already get right. So a pair is in the table because
/// composing it from smaller pieces gave the wrong bytes - which keeps the table small, and keeps composition honest
/// for strings the composer was never asked about.
/// </summary>
public sealed class Learner
{
    public readonly Fam F;
    public readonly List<RewriteRule> PreRules = Rules.ForwardPre();
    public readonly List<RewriteRule> PostRules = Rules.ForwardPost();

    public Learner(Fam f) => F = f;

    /// <summary>The joiners: no glyph of their own, the spelling around them carries them (କ୍‌ତ = 4C FF FA 86 FF).</summary>
    public static readonly string[] Dropped = { "‌", "‍" };

    /// <summary>The signs whose glyph changes after a subjoined form that hangs below the line (ori7-patterns.txt 6), and the phalas after one.</summary>
    public static readonly string[] AfterSubjoined = { "ୁ", "ୂ", "ୃ", "୍ର", "୍", "୍ୟ" };

    /// <summary>
    /// Pieces the composer has no answer for (five codes, more than a key holds), from the glyphs every face has for them
    /// (rendered beside Nirmala UI: r3\tbl-ori\round1\sheet2-00.png): ି + ଁ the ligature 5D (କିଁ = 4C FF 5D, read off the
    /// truth: every consonant but ଖ ଥ ଧ); ॥ as two dandas 3E 3E (the composer types nothing for it); the ligatures ଙ୍କ୍ତ 9C,
    /// ତ୍ତ୍ବ 89, ଦ୍ଧ୍ବ 96, ନ୍ଦ୍ବ A3, ନ୍ଧ୍ବ A5 (ୱ after the virama is ବ here, Rules.cs) the earlier table wrote too; ଚ଼ E3
    /// (the dotted letter the 7 composer types for code E0).
    /// </summary>
    public static readonly (string U, string Hex)[] Extra =
    {
        ("ିଁ", "5D"),
        ("॥", "3E 3E"),
        ("ଙ୍କ୍ତ", "9C"), ("ତ୍ତ୍ବ", "89"), ("ଦ୍ଧ୍ବ", "96"), ("ନ୍ଦ୍ବ", "A3"), ("ନ୍ଧ୍ବ", "A5"),
        ("ଚ଼", "E3"),
        // ୱ + virama: the ba-phala stroke of ୱ hangs, so the visible virama takes the lowering spacer - the bytes S8Ori764
        // writes for the ୱ key of ENG.ORI and PHONETIC.ORI (codes 54 64 71, answered in their virama form: 48 B4 26 FA),
        // as ୱୁ ୱୂ ୱୃ in the truth take the low forms (48 B4 EC, 48 B4 26 F5).
        ("ୱ୍", "48 B4 26 FA"),
    };

    /// <summary>
    /// The mark main's TypingEngine puts after a word it is still typing when that word ends in a virama and the table has a
    /// pair for it (FontConverter.WaitingMark, U+FDEF: the ra + virama of a reph key struck before its letter). Shree shows
    /// the reph alone, F6, while it waits for the letter (the lone reph key of TYPEWRIT CRTRONIC DOE ROMA: F6); a word that
    /// ends there gets ra + the visible virama (C0 FF FA). Any other waiting virama is the visible virama the composer shows
    /// meanwhile too (C + link = C FA). The mark itself writes nothing.
    /// </summary>
    public static readonly string Waiting = ((char)0xFDEF).ToString();

    public FontConverter Stager() =>
        new(Table("s", "s", "a", "b", FontType.Unicode, FontType.Legacy, PreRules, Array.Empty<(string, string)>()));

    /// <summary>
    /// The subjoined forms and what follows them, read off the truth: for each consonant X, the bytes C୍X adds to C's own
    /// spelling - the most frequent over every consonant C whose C୍X is no ligature (୍କ 50 on 26 letters; ୍ତ FA 86 FF on
    /// 28) - and the same for C୍X + ୁ ୂ ୃ ୍ର ୍ ୍ୟ (after a form that hangs: EC EE 26 F5 26 F7 26 FA, ori7-patterns.txt 6).
    /// ୍ବ after a subjoined form (three letters, five codes: no composer answer) is the ba-phala stroke B4 after it.
    /// </summary>
    public static Dictionary<string, string> Subjoined(IReadOnlyList<Truth> truth)
    {
        var byU = truth.Where(t => t.Pref.Length > 0).ToDictionary(t => t.U, t => t.Pref, StringComparer.Ordinal);
        var cons = byU.Keys.Where(u => u.Length == 1 && IsCons(u[0])).ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? Majority(string x, string s)
        {
            var count = new Dictionary<string, int>();
            foreach (var c in cons)
            {
                if (!byU.TryGetValue(c + "୍" + x + s, out var p) || !byU.TryGetValue(c, out var pc)) continue;
                if (!p.StartsWith(pc + " ", StringComparison.Ordinal)) continue;
                string suf = p[(pc.Length + 1)..];
                count[suf] = count.GetValueOrDefault(suf) + 1;
            }
            if (count.Count == 0) return null;
            var best = count.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
            return best.Value >= 3 ? best.Key : null;
        }
        foreach (var x in cons)
        {
            if (Majority(x, "") is not { } sub) continue;
            result["୍" + x] = sub;
            foreach (var s in AfterSubjoined)
                if (Majority(x, s) is { } m) result["୍" + x + s] = m;
            if (result.TryGetValue("୍" + x + "୍", out var halant) && halant != sub + " FA")
                result["୍" + x + "୍ବ"] = sub + " B4";
        }
        return result;
    }

    /// <summary>Atoms every learning run starts from.</summary>
    public Dictionary<string, string> Atoms(IReadOnlyList<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        // A character the table passes through (its spelling is itself, U+XXXX: ₹ ୰ ଽ) gets no pair here: it must be reported.
        foreach (var t in train.Where(t => t.U.Length == 1 && t.Pref.Length > 0 && Reported(t.Pref).Count == 0)) atoms[t.U] = FromHex(t.Pref);
        foreach (var (u, hex) in Subjoined(train)) atoms.TryAdd(u, FromHex(hex));
        foreach (var (u, hex) in Extra) atoms[u] = FromHex(hex);   // font codes already, not raw composer bytes
        // The virama alone is the visible virama FA (word-final କ୍ = 4C FF FA; କ୍‌ତ = 4C FF FA 86 FF): as a piece, so C + virama
        // is never learned whole and a conjunct still finds its subjoined form.
        atoms["୍"] = FromHex("FA");
        foreach (var d in Dropped) atoms[d] = Rules.Keep;
        // the mark a pre rule puts between two pieces (୍ before କ୍ଷ): it writes nothing (the last rule removes it), never reported
        atoms[Rules.Keep] = Rules.Keep;
        atoms[Rules.Reph] = FromHex("F6");
        atoms["ି" + Rules.Reph] = FromHex("7D");
        atoms["ୖ" + Rules.Reph] = FromHex("F1");
        atoms[Rules.ESign] = FromHex("7B");
        atoms[Waiting] = "";
        atoms["ର୍" + Waiting] = FromHex("F6");
        return atoms;
    }

    public MappingTable Build(Dictionary<string, string> pairs)
    {
        var t = Table("unicode-to-" + F.Id, "Unicode -> " + F.Name, "kalinga", F.Id, FontType.Unicode, FontType.Legacy, PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules);
        foreach (var g in Policy.NoGlyph.Where(g => !pairs.ContainsKey(g)))
            t.Pairs.Add(new MappingPair { From = g, To = g, Unmapped = true });
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
                log?.Add($"{t.U}\t{Rules.Show(key)}\t{t.Pref}\tcomposed {got}");
                pairs[key] = FromHex(t.Pref);
                added++;
            }
        }
        stats = $"{F.Id}: atoms {atoms}, learned {added}, conflicts {conflicts}, total {pairs.Count}";
        return pairs;
    }
}
