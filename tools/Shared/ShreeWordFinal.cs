// Shared by tools/Guj7Truth and tools/ShreeDevTruth (linked into both projects; each
// has its own Truth record of the same shape).

/// <summary>
/// A virama that ends a word. Shree's composer is asked one key at a time, so a string
/// that ends in a bare virama (C + virama, kind canonical:pending-link) is answered with
/// the half form the composer shows while it WAITS for the next letter - and a half letter
/// standing alone draws a broken letter. Unicode text that ends a word in a virama is drawn
/// by every Unicode font with a visible halant, so the table writes what the composer
/// itself writes for C + virama + ZWNJ (the visible halant). The rows keep the composer's
/// pending answer; this says what the table writes instead, and why.
/// </summary>
public static class WordFinal
{
    public const char Zwnj = '‌';

    /// <summary>
    /// For a truth row that ends in letter + virama: the halant spelling and how it was
    /// derived from the truth; null for any other row, or when the composer's own answer
    /// already is the halant spelling (the letters with no half form).
    /// <list type="number">
    /// <item>The truth has the same string + ZWNJ (every single letter, and the ligature
    ///   letters क्ष ज्ञ त्त त्र श्र): the composer's spelling of that.</item>
    /// <item>A cluster whose spelling ends in its last letter's own spelling (स्त = half स +
    ///   त): the earlier letters as the composer writes them in the cluster, the last letter
    ///   as the composer writes it with ZWNJ (त्‌).</item>
    /// <item>Otherwise the last letter is inside a ligature (क्क द्ध) or is a sign of the
    ///   letter before it (the ya-, va-, ra-, na-signs): the cluster keeps its spelling and the
    ///   halant follows it, before a spacer that ends it - as the truth's own ligature rows
    ///   do (क्ष्‌ = 6A B2, त्र्‌ = CC B2 in DEV7). This is a choice: the composer has no answer for it.</item>
    /// </list>
    /// </summary>
    /// <param name="raw">The composer's rows (unadjusted), by Unicode.</param>
    /// <param name="halant">The halant code of the script (hex).</param>
    /// <param name="spacers">The zero-width spacer codes a letter can end in (hex); the halant goes before one.</param>
    public static (string Pref, string Why)? Spell(Truth t, IReadOnlyDictionary<string, Truth> raw, char virama, char nukta,
        string halant, string[] spacers)
    {
        string u = t.U;
        if (u.Length < 2 || u[^1] != virama || u[^2] == virama || u[^2] == Zwnj || u[^2] == '‍') return null;
        string waits = t.Pref.Length == 0 ? "nothing" : t.Pref;
        string why;
        string pref;
        if (raw.TryGetValue(u + Zwnj, out var visible))
        {
            pref = visible.Pref;
            why = $"a virama that ends a word is drawn with the halant, as the composer writes {u}{Zwnj}; the composer's own {waits} for {u} is what it shows while it waits for the next letter";
        }
        else
        {
            string x = u[..^1];
            int k = x.Length - 1;
            if (x[k] == nukta && k > 0) k--;
            string last = x[k..];
            if (!raw.TryGetValue(x, out var cluster) || !raw.TryGetValue(last, out var letter) || !raw.TryGetValue(last + virama + Zwnj, out var halanted))
                throw new InvalidOperationException($"no truth to spell {u} at the end of a word from (need {x}, {last} and {last}{virama}+ZWNJ)");
            string px = " " + cluster.Pref, pl = " " + letter.Pref;
            if (letter.Pref.Length > 0 && px.Length > pl.Length && px.EndsWith(pl, StringComparison.Ordinal))
            {
                string head = px[..^pl.Length].Trim();
                pref = (head + " " + halanted.Pref).Trim();
                why = $"a virama that ends a word is drawn with the halant: {x} as the composer writes it, its last letter as the composer writes {last}{virama}{Zwnj}; the composer's own {waits} for {u} is what it shows while it waits for the next letter";
            }
            else
            {
                var codes = cluster.Pref.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                int at = codes.Count > 0 && spacers.Contains(codes[^1]) ? codes.Count - 1 : codes.Count;
                codes.Insert(at, halant);
                pref = string.Join(" ", codes);
                why = $"a virama that ends a word is drawn with the halant: {x} is written {cluster.Pref} (a ligature, or the letter and a sign), which it keeps, with the halant {halant} after it{(at < codes.Count - 1 ? " and before its spacer" : "")}; the composer's own {waits} for {u} is what it shows while it waits for the next letter";
            }
        }
        return pref == t.Pref ? null : (pref, why);
    }

    /// <summary>Every row adjusted: the table's spelling as Pref, the composer's own answer kept in Acc and Composer.</summary>
    public static List<Truth> Adjust(List<Truth> raw, Func<Truth, IReadOnlyDictionary<string, Truth>, (string Pref, string Why)?> adjust)
    {
        var byU = raw.ToDictionary(t => t.U, StringComparer.Ordinal);
        var list = new List<Truth>(raw.Count);
        foreach (var t in raw)
            list.Add(adjust(t, byU) is { } a ? t with { Pref = a.Pref, Acc = t.Acc.Append(a.Pref).Distinct().ToArray(), Composer = t.Pref } : t);
        return list;
    }
}
