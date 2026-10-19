using FontSoftware.Core.Conversion;
using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// Learns the Unicode -> Shree pairs from the composer truth. It starts from atoms (one
/// pair per character and marker), then walks the truth strings shortest first and adds
/// a pair only for a string the table so far does not already get right. So a pair is
/// in the table because composing it from smaller pieces gave the wrong bytes - which
/// keeps the table small, and keeps composition honest for strings the composer was
/// never asked about. Before it learns a whole string it tries a head of it (a cluster
/// without its signs, a half letter), so one pair serves every sign on that cluster.
/// </summary>
public sealed class Learner
{
    public readonly Script S;
    readonly FontConverter _post;
    readonly FastConv _stager;

    public Learner(Script s)
    {
        S = s;
        _stager = new FastConv(s.ForwardPre, Array.Empty<RewriteRule>(), new Dictionary<string, string>());
        _post = new FontConverter(Table("p", "p", "a", "b", FontType.Legacy, FontType.Legacy,
            Array.Empty<RewriteRule>(), Array.Empty<(string, string)>(), s.ForwardPost));
    }

    public static string Keep => Dev7.Keep;

    /// <summary>
    /// For a row that ends in a virama: what the composer answers while it waits for the next
    /// letter (its own preferred answer - the table writes the halant at the end of a word).
    /// </summary>
    public static string? Waiting(Truth t) => t.U.Length > 1 && IsVirama(t.U[^1]) ? t.Composer ?? t.Pref : null;

    public string Stage(string u) => _stager.Pre(u);

    /// <summary>What the post rules make of a legacy string.</summary>
    public string Post(string legacy) => _post.ConvertText(legacy);

    /// <summary>The pair output for a composer spelling: as is, or kept away from the rules when they would change it.</summary>
    public string Spell(string prefHex, ref int kept)
    {
        string legacy = FromHex(prefHex);
        if (Post(legacy) == legacy) return legacy;
        kept++;
        // U+FDD1 between every two codes, then take out each one the rules do not need.
        var at = Enumerable.Repeat(true, Math.Max(0, legacy.Length - 1)).ToArray();
        string Make()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < legacy.Length; i++) { sb.Append(legacy[i]); if (i < at.Length && at[i]) sb.Append(Keep); }
            return sb.ToString();
        }
        if (Post(Make()) != legacy) return Make();
        for (int j = 0; j < at.Length; j++)
        {
            at[j] = false;
            if (Post(Make()) != legacy) at[j] = true;
        }
        return Make();
    }

    /// <summary>Atoms every learning run starts from.</summary>
    public Dictionary<string, string> Atoms(IEnumerable<Truth> train)
    {
        var atoms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (u, hex, _) in S.Extra) atoms[u] = hex.Length == 0 ? Keep : FromHex(hex);
        foreach (var (m, hex) in S.MarkerAtoms) atoms[m] = hex.Length == 0 ? Keep : FromHex(hex);
        int kept = 0;
        foreach (var t in train.Where(t => t.U.Length == 1)) atoms[t.U] = Spell(t.Pref, ref kept);
        // The half letters: what the composer answers for a letter + virama while it waits
        // for the next letter. At the end of a word the table writes the halant instead (the
        // row's Pref), but before a letter this is the half form that letter joins - keyed
        // as the pre rules leave a letter + virama before a letter, and only where the
        // pieces do not already compose to it.
        var fc = new FastConv(S.ForwardPre, S.ForwardPost, atoms);
        foreach (var t in train.Where(t => Waiting(t) is { Length: > 0 } && t.U.Length == 2 && IsVirama(t.U[1]) && IsCons(t.U[0])))
        {
            string staged = fc.Pre(t.U + "क");
            if (!staged.EndsWith("्क", StringComparison.Ordinal)) continue;   // र्: a reph before a letter
            string key = staged[..^1];
            if (Hex(fc.ConvertStaged(key)) != Waiting(t)) fc.Set(key, Spell(Waiting(t)!, ref kept));
        }
        return atoms;
    }

    public MappingTable Build(Dictionary<string, string> pairs, string? id = null, string? name = null, string? target = null,
        List<RewriteRule>? post = null) =>
        Table(id ?? S.ForwardId, name ?? $"Unicode -> {S.Title}", "mangal", target ?? S.Encoding, FontType.Unicode, FontType.Legacy, S.ForwardPre,
            pairs.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value)),
            post ?? S.ForwardPost);

    public static bool UseHeads = Environment.GetEnvironmentVariable("SHREE_HEADS") != "0";

    public Dictionary<string, string> Learn(IEnumerable<Truth> trainSet, out string stats, List<string>? log = null)
    {
        var train = trainSet.Where(t => S.Excluded(t) is null).ToList();
        var pairs = Atoms(train);
        int atoms = pairs.Count;
        var fc = new FastConv(S.ForwardPre, S.ForwardPost, pairs);
        var staged = train.ToDictionary(t => t.U, t => fc.Pre(t.U), StringComparer.Ordinal);
        var right = new List<Truth>();
        int added = 0, heads = 0, kept = 0;
        // Strings that end on a pending virama come last: the composer answers them with the half form it waits with, which for a conjunct is not the half it uses inside a longer one.
        foreach (var t in train.OrderBy(t => staged[t.U].EndsWith(Dev7.PendingMark) ? 1 : 0).ThenBy(t => staged[t.U].Length).ThenBy(t => t.U, StringComparer.Ordinal))
        {
            string key = staged[t.U];
            string got = Hex(fc.ConvertStaged(key));
            if (got == t.Pref) { right.Add(t); continue; }
            if (UseHeads && key.Length > 2 && TryHead(fc, t, key, staged, right, ref kept, log)) { heads++; right.Add(t); continue; }
            log?.Add($"{t.U}\t{Hex(key)}\t{t.Pref}\tcomposed {got}");
            fc.Set(key, Spell(t.Pref, ref kept));
            added++;
            right.Add(t);
        }
        // The composer's waiting answers (a row that ends in a bare virama, which the table
        // writes with the halant at the end of a word) are still what it writes for those
        // letters when the next letter comes: the half forms a following letter joins - ञ्च्
        // is the half ligature 23, a conjunct and a link are its halves (क्क् = 8A 8A). Learn
        // each where the pre rules leave it before a letter, last, and only where it spoils
        // no string already right.
        int waiting = 0;
        foreach (var t in train.Where(t => Waiting(t) is { Length: > 0 } && IsVirama(t.U[^1])).OrderBy(t => t.U.Length).ThenBy(t => t.U, StringComparer.Ordinal))
        {
            string before = fc.Pre(t.U + "क");
            if (!before.EndsWith("्क", StringComparison.Ordinal)) continue;   // र्: a reph on the letter
            string key = before[..^1];
            if (Hex(fc.ConvertStaged(key)) == Waiting(t)) continue;
            bool had = fc.Has(key);
            string? old = had ? fc.Get(key) : null;
            int k0 = kept;
            fc.Set(key, Spell(Waiting(t)!, ref kept));
            if (right.Any(r => staged[r.U].Contains(key, StringComparison.Ordinal) && Hex(fc.ConvertStaged(staged[r.U])) != r.Pref))
            {
                if (had) fc.Set(key, old!); else fc.Remove(key);
                kept = k0;
                continue;
            }
            log?.Add($"{t.U}\t{Hex(key)}\t{Waiting(t)}\twaiting");
            waiting++;
        }
        // A pair learned later can spoil a string learned earlier: give those a pair of their own.
        int fixes = 0;
        for (int pass = 0; pass < 5; pass++)
        {
            var bad = train.Where(t => Hex(fc.ConvertStaged(staged[t.U])) != t.Pref).ToList();
            if (bad.Count == 0) break;
            foreach (var t in bad)
            {
                log?.Add($"{t.U}\t{Hex(staged[t.U])}\t{t.Pref}\tfix-up");
                fc.Set(staged[t.U], Spell(t.Pref, ref kept));
                fixes++;
            }
        }
        stats = $"atoms {atoms}, heads {heads}, learned {added} ({kept} kept from the rules), waiting half forms {waiting}, fix-ups {fixes}, total {pairs.Count}";
        return pairs;
    }

    /// <summary>
    /// Try a pair for a head of the string (a prefix with no pair yet) whose bytes are
    /// the preferred bytes less what the rest of the string composes to. Keep it when it
    /// makes the string right and spoils no string that was right.
    /// </summary>
    bool TryHead(FastConv fc, Truth t, string key, Dictionary<string, string> staged, List<Truth> right, ref int kept, List<string>? log)
    {
        string pref = " " + t.Pref;
        for (int i = 2; i < key.Length; i++)
        {
            string head = key[..i], rest = key[i..];
            if (fc_Has(fc, head)) continue;
            if (head[^1] == '़' || rest[0] == '‍' || rest[0] == '़') continue;
            string q = Hex(fc.ConvertStaged(rest));
            if (q.Length > 0 && !pref.EndsWith(" " + q)) continue;
            string h = q.Length == 0 ? t.Pref : pref[..^(q.Length + 1)].Trim();
            if (h.Length == 0) continue;
            int k0 = kept;
            fc.Set(head, Spell(h, ref kept));
            bool ok = Hex(fc.ConvertStaged(key)) == t.Pref;
            if (ok)
                foreach (var r in right)
                    if (staged[r.U].Contains(head, StringComparison.Ordinal) && Hex(fc.ConvertStaged(staged[r.U])) != r.Pref) { ok = false; break; }
            if (ok)
            {
                log?.Add($"{t.U}\t{Hex(key)}\t{t.Pref}\thead {Hex(head)} = {h}");
                return true;
            }
            fc.Remove(head);
            kept = k0;
        }
        return false;
    }

    static bool fc_Has(FastConv fc, string key) => fc.Has(key);
}
