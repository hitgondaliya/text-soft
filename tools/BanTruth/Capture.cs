using System.Text;
using System.Text.Json.Nodes;
using static Common;

/// <summary>
/// Reads the composer capture (ban7-groundtruth.json) into the truth the tables are
/// built against. Two things are done to the composer's bytes, both proven from the
/// fonts (glyph compare and renders of every SHREE-BAN / SHREE-ASS face, 7 and 4/5/6):
///
///  1. ব ৰ ৱ are moved to the codes that draw them. The composer writes ব and ৰ as AF
///     and ৱ as A4; in every SHREE-BAN font AF draws the ligature ব্ল and A4 draws ব,
///     and ৱ is at 9E. So ব = A4 (the code the composer itself uses for ব inside ব্ব
///     and ব্ল), ৱ = 9E, and ৰ = B9: no SHREE-BAN font has ৰ, and the SHREE-ASS fonts
///     draw ৰ at B9 (their র code).
///  2. Rows where the composer loses a letter it was typed are left out (they are
///     its bugs, not spellings): a leading C্ dropped before গু শু হু or a ra-phala
///     ligature (ক্গু = গু), ৱ or ৰ dropped or written with the র glyph - and the two rows that lose the '.' a lone
///     sign was typed on (া.ে = E0 EC, ৗ.ে = EF EC: the composer's carrier, not text).
///
///  3. Where the composer's spelling does not draw the text it was typed, the spelling that
///     does (Decisions.cs: Assamese ৰ as reph and ra-phala, the filler, a word-final র্).
///
/// What is left are the codes a SHREE-BAN 4/5/6 font draws the text with; the other
/// families are this with their own codes moved (Family.cs).
/// </summary>
public static class Capture
{
    /// <summary>Codes that draw no letter: signs, marks, the spacer, the filler (SHREE-BAN 4/5/6 codes).</summary>
    public static readonly HashSet<string> SignCodes = new(
        "A1 A2 E0 5B E3 E5 E6 E7 E8 E9 EA F5 EC EB EE ED EF 7D F2 40 F4 B8 F8 F6 F9 F7 FB 97 C3 AB AC B5 B6 FF 7B 85 5D FC FA 3A 25"
            .Split(' '));

    public static (List<Truth> Rows, List<string> Left) Read(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var truth = doc["truth"]!.AsObject();
        var raw = new Dictionary<string, (string Pref, List<string> Acc)>(StringComparer.Ordinal);
        foreach (var (u, node) in truth)
        {
            string pref = Norm((string)node!["preferred"]!);
            var acc = node["accepted"]!.AsArray().Select(x => Norm((string)x!)).ToList();
            if (!acc.Contains(pref)) acc.Insert(0, pref);
            raw[u] = (pref, acc);
        }

        var left = new List<string>();
        var rows = new List<Truth>();
        foreach (var (u, (pref, acc)) in raw)
        {
            string? why = Lost(u, pref, raw);
            if (why is not null) { left.Add($"{why}\t{u}\t{pref}"); continue; }
            string? p = Fix(u, pref, out why);
            if (p is null) { left.Add($"{why}\t{u}\t{pref}"); continue; }
            var fixedAcc = new List<string> { p };
            foreach (var a in acc.Where(a => a != pref))
            {
                var f = Fix(u, a, out _);
                if (f is not null && !fixedAcc.Contains(f)) fixedAcc.Add(f);
            }
            rows.Add(new Truth(u, p, fixedAcc.ToArray()));
        }
        // 3. Where the composer's spelling does not draw the text (Decisions.cs). Its evidence for
        // text the Unicode composer mislabelled: the 7 composer's bytes, filed under what was typed.
        Decisions.Evidence = new(StringComparer.Ordinal);
        foreach (var p in doc["pairs"]!.AsArray())
        {
            var m = System.Text.RegularExpressions.Regex.Match((string)p!["kind"]!, "^quirk:U-composer-wrote-\"(.*)\"-for-\"(.*)\"$");
            if (!m.Success) continue;
            string meant = m.Groups[2].Value;
            string? hex = Fix(meant, Norm((string)p["bytes"]!), out _);
            if (hex is null) continue;
            if (!Decisions.Evidence.TryGetValue(meant, out var l)) Decisions.Evidence[meant] = l = new();
            l.Add((hex, (string)p["source"]!));
        }
        rows = Decisions.Apply(rows, left, Log);
        return (rows.OrderBy(r => r.U, StringComparer.Ordinal).ToList(), left);
    }

    /// <summary>
    /// Every spelling Shree's composer wrote in the capture, of every kind: the truth's preferred,
    /// accepted and alternative spellings (the rows left out too) and every probe in "pairs" - the
    /// vendor keys, canonical, alt:* and quirk:* rows - each as it wrote it (SHREE-BAN 4/5/6 codes,
    /// ব ৰ as AF and ৱ as A4) and with ব ৰ ৱ moved to the codes that draw them, where that works.
    /// Distinct (typed text, bytes); what a document typed with the composer can hold.
    /// </summary>
    public static List<(string U, string Hex, string Kind)> Spellings(string path)
    {
        var doc = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;
        var seen = new HashSet<(string, string)>();
        var list = new List<(string, string, string)>();
        void Add(string u, string hex, string kind)
        {
            hex = Norm(hex);
            if (hex.Length == 0) return;
            foreach (var h in new[] { hex, Fix(u, hex, out _) })
                if (h is not null && seen.Add((u, h))) list.Add((u, h, kind));
        }
        foreach (var (u, node) in doc["truth"]!.AsObject())
        {
            Add(u, (string)node!["preferred"]!, "truth");
            foreach (var a in node["accepted"]!.AsArray()) Add(u, (string)a!, "truth accepted");
            if (node["alternatives"] is JsonArray alts)
                foreach (var a in alts) Add(u, a is JsonObject o ? (string)o["bytes"]! : (string)a!, "truth alternative");
        }
        foreach (var p in doc["pairs"]!.AsArray()) Add((string)p!["unicode"]!, (string)p["bytes"]!, (string)p["kind"]!);
        return list;
    }

    /// <summary>What <see cref="Decisions"/> changed, one line per row (for prep and the fixture note).</summary>
    public static readonly List<string> Log = new();

    /// <summary>Why the composer's spelling lost a letter, or null.</summary>
    static string? Lost(string u, string pref, Dictionary<string, (string Pref, List<string> Acc)> raw)
    {
        // C্ + rest written exactly as the rest alone: the leading letter is gone.
        int i = 0;
        while (i < u.Length && IsCons(u[i]))
        {
            int j = i + 1;
            if (j < u.Length && u[j] == '়') j++;
            if (j < u.Length && IsVirama(u[j]) && j + 1 < u.Length && IsCons(u[j + 1]))
            {
                string rest = u[(j + 1)..];
                if (raw.TryGetValue(rest, out var r) && r.Acc.Contains(pref)) return "lost-letter";
                i = j + 1;
            }
            else break;
        }
        // The composer's carrier (round 2 merge): a lone sign is asked for on a '.' (া.ে, ৗ.ে - the pieces of ো ৌ),
        // and the bytes it answers have no '.' (E0 EC, EF EC). Learned as pairs, they dropped the '.' of every text with a
        // syllable ending in া ো ৗ ৌ, a '.' and one with ে ো ৌ (ডা.সেন = 6C A1 E0 EC CE 3E, read ডাসেন): not spellings.
        if (u.Contains('.') && u != "." && !pref.Split(' ').Contains("2E")) return "lost-carrier";
        // Consonants typed, but no code that draws a letter.
        // (A র before a virama is a reph, a র or য after one a phala: those are signs too.)
        bool letter = Enumerable.Range(0, u.Length).Any(k => IsCons(u[k])
            && !(u[k] == 'র' && k + 1 < u.Length && IsVirama(u[k + 1]))
            && !(u[k] is 'র' or 'য' && k > 0 && IsVirama(u[k - 1])));
        if (letter && pref.Length > 0 && pref.Split(' ').All(SignCodes.Contains)) return "lost-letter";
        return null;
    }

    /// <summary>
    /// The composer bytes with ব (AF) as A4, ৰ (AF) as B9 and ৱ (A4) as 9E. AF and A4 are
    /// matched in order to the ব ৰ ৱ of the text (a ব inside a ligature has no code of its own).
    /// </summary>
    public static string? Fix(string u, string hex, out string? why)
    {
        why = null;
        if (hex.Length == 0) return hex;
        var letters = new Queue<char>(u.Where(c => c is 'ব' or 'ৰ' or 'ৱ'));
        var toks = hex.Split(' ');
        for (int k = 0; k < toks.Length; k++)
        {
            if (toks[k] is not ("AF" or "A4")) continue;
            bool af = toks[k] == "AF";
            char? hit = null;
            while (letters.Count > 0)
            {
                char c = letters.Dequeue();
                if (af ? c is 'ব' or 'ৰ' : c is 'ব' or 'ৱ') { hit = c; break; }
                if (c != 'ব') { why = $"unaligned-{toks[k]}"; return null; }   // ৰ/ৱ skipped: lost
            }
            if (hit is null) { why = $"unaligned-{toks[k]}"; return null; }
            toks[k] = hit switch { 'ব' => "A4", 'ৰ' => "B9", _ => "9E" };
        }
        // ৰ and ৱ have no ligature: one left over was dropped, or written with the র glyph.
        if (letters.Any(c => c != 'ব')) { why = "lost-rra-or-wa"; return null; }
        return string.Join(" ", toks);
    }
}
