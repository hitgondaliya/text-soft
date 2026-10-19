using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// The Shree-Lipi 4/5/6 Gujarati fonts (SHREE-GUJ-*) share every code with the 7
/// fonts (SHREE-GUJ7-*) except 91 92 93 94 B6: ઙ conjuncts there, copies of પ થ ન
/// સ્ત્ર and the ્રૂ mark in 7.
/// </summary>
public static class Family
{
    /// <summary>How the 7 composer spells each ઙ conjunct, longest first.</summary>
    public static readonly (string Seven, string Code, string Unicode)[] Nga =
    {
        ("50 B9 24 4C 24", "91", "ઙ્ક"),
        ("50 B9 24 6E", "B6", "ઙ્ક્ષ"),
        ("50 B9 24 4D", "92", "ઙ્ખ"),
        ("50 B9 24 4E", "93", "ઙ્ગ"),
        ("50 B9 24 4F", "94", "ઙ્ઘ"),
    };

    /// <summary>A 7 spelling with the ઙ conjuncts written as the 4/5/6 codes.</summary>
    public static string To456(string hex)
    {
        string s = " " + hex + " ";
        foreach (var (seven, code, _) in Nga) s = s.Replace(" " + seven + " ", " " + code + " ");
        return s.Trim();
    }

    public static bool Accept456(string got, Truth t) =>
        got == t.Pref || t.Acc.Contains(got) || got == To456(t.Pref) || t.Acc.Any(a => To456(a) == got);

    /// <summary>
    /// The 4/5/6 post rules: the 7 ones, plus one per ઙ conjunct that puts the conjunct
    /// on its own code - only where it stands whole: not under an i-sign (which glyph the
    /// 4/5/6 composer takes over it is not known) and with no sign drawn on it. Anywhere
    /// else the 7 spelling stays, and it draws the same in 4/5/6.
    /// </summary>
    public static List<RewriteRule> PostRules456()
    {
        var rules = Learner.PostRules.Where(r => r.Pattern != Rules.Keep).ToList();
        string ision = Rules.Class("5B 71 72 74");
        string onIt = Rules.Class(Rules.Above + " " + Rules.Below + " E9 99 B9");
        foreach (var (seven, code, uni) in Nga)
        {
            string lit = string.Concat(seven.Split(' ').Select(Rules.Lit));
            rules.Add(new RewriteRule
            {
                Pattern = $"(?<!{ision}){lit}(?!{onIt})",
                Replace = Rules.Rep(code),
                Note = $"4/5/6: {uni} standing whole is the one code {code}.",
            });
        }
        rules.Add(Learner.PostRules.Single(r => r.Pattern == Rules.Keep));
        return rules;
    }

    public static MappingTable Table456(Dictionary<string, string> pairs) =>
        Table("unicode-to-shree-guj456", "Unicode -> Shree-Lipi 4.0/5.0/6.0 Gujarati", "shruti", "shree-guj456",
            FontType.Unicode, FontType.Legacy, Learner.PreRules,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            PostRules456());

    /// <summary>The 7 pairs under the 4/5/6 rules; a truth string they would spell wrongly keeps a pair of its own.</summary>
    public static Dictionary<string, string> Forward456(Dictionary<string, string> seven, IReadOnlyList<Truth> truth, out string stats)
    {
        var pairs = new Dictionary<string, string>(seven, StringComparer.Ordinal);
        var stager = Learner.Stager();
        int added = 0, kept = 0;
        var conv = new FontConverter(Table456(pairs));
        foreach (var t in truth.OrderBy(t => t.U.Length))
        {
            string got = Hex(conv.ConvertText(t.U));
            if (Accept456(got, t)) continue;
            pairs[stager.ConvertText(t.U)] = Rules.Protect(FromHex(t.Pref));
            added++; kept++;
            conv = new FontConverter(Table456(pairs));
        }
        stats = $"4/5/6: 5 conjunct rules, {added} strings with a pair of their own, total {pairs.Count}";
        return pairs;
    }

    /// <summary>Codes a table writes that draw something else in its family.</summary>
    public static List<string> Leaks(Dictionary<string, string> pairs, bool is456)
    {
        var bad = new List<string>();
        foreach (var (from, to) in pairs)
        {
            foreach (var code in Hex(to).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Learner.FamilyCodes.Contains(code)) continue;
                if (!is456) { bad.Add($"{from} -> {Hex(to)}"); continue; }
                var nga = Nga.First(n => n.Code == code);
                if (!from.Contains(nga.Unicode)) bad.Add($"{from} -> {Hex(to)}");
            }
        }
        return bad;
    }
}
