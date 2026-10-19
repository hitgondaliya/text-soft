using static Common;

/// <summary>
/// Where the truth is not the composer's bytes as they come. Each of these is a place where
/// the composer's spelling does not draw the text it was typed (rendered in every family to
/// check), so the fixture carries the spelling that does, in SHREE-BAN 4/5/6 codes like the
/// rest of it:
///
///  1. Assamese ৰ as reph or ra-phala (ৰ্ক, ক্ৰ). Shree's Assamese keyboards type the Bengali
///     র for ra, so its composer only ever meets ৰ as a letter of its own and writes ৰ্ক as
///     ৰ + a visible hasanta + ক. Assamese Unicode text spells ra ৰ everywhere, and must draw
///     the reph and the ra-phala: these rows take the bytes of the same text with র there.
///  2. The filler. Between দ্গ দ্ঘ ধ্ব ব্ব ড়্গ ৱ্ৱ the composer writes FF, which draws
///     nothing in the 4/5/6 fonts (and ড়্গ in the 7 fonts), so দ্গ is drawn দগ - two
///     letters. Never a spelling that draws two letters: ধ্ব ব্ব take the subscript ব AB the
///     composer writes for খ্ব থ্ব শ্ব (with ু ূ as E6 E9 under it); the others a visible
///     hasanta (দ্‌গ: 83 F4 4B; ড়্গ: D8 6C F4 A1 4B). A family whose fonts have the conjunct
///     moves these to it (Family.cs: the 7 fonts' ড়্গ FF and joining দ A8).
///  3. র্ that ends the text. Alone the composer writes the reph A2 - a pending ra, waiting for
///     the letter it will sit on. At the end of a word nothing comes: ra with a visible
///     hasanta, B9 F4 (which it also accepts).
///  4. ব্ঢ ব্ড চ্ঞ. The composer writes them 81 8D 90, which the fonts hold at U+0081 U+008D
///     U+0090 - C1 control codes, which Word and RichEdit draw as nothing at all. So they are
///     written letter + visible hasanta + letter, the way the composer writes a conjunct the
///     fonts have no glyph for (দ্গ = 83 F4 4B): ব্ + the composer's spelling of the second
///     letter with every sign the row has (ব্ঢু = ব্ + ঢু: A4 F4 6E E5 A1; চ্ঞি = 5B 57 F4 A1 64 A1).
///  5. Assamese ৰ rows whose র twin the composer was never asked about (127; before, they were
///     left out). A ra-phala that carries a hasanta of its own or another ra-phala (ক্ৰ্, ক্ৰ্র):
///     the composer's spelling - the letter with a visible hasanta, then ৰ as a letter (ক্ৰ্র =
///     41 F4 A1 B9 F8) - which draws both ras; two ra-phalas at one spot (41 F6 F6 A1) draw one.
///     A ৰ that is a reph (ৰ্ক্য ৰ্ক্ষ ৰ্কঃ): the reph, as decision 1 - with the bytes the
///     composer itself writes for that text typed in Shree's order (its Unicode composer mislabels
///     those rows: "quirk:U-composer-wrote-X-for-Y"; র্ক্য = 41 A1 B8 A2, র্ক্ষা = DB A1 E0 A2),
///     and where the capture has none, from the pieces: ঃ where ং stands (র্কঃ as র্কং = 41 A2 A1
///     40), the ya-phala B8 then the reph (র্র্য = B9 B8 A2, as র্ক্য).
///  6. ড় ঢ় with ু ূ and ঁ, a reph or a ra-phala under a reph (ড়ুঁ র্ড়ু র্ড়ুং র্ড়ুঁ র্ড়্রু ...): the
///     composer's spelling loses the sign (D8 6C A1 F2 A1, D8 6C A1 A2 A1); its own alternative keeps it
///     (র্ড়ু = D8 6C A2 A1 E6), and the rest are built the same way (<see cref="USign"/>).
/// </summary>
public static class Decisions
{
    /// <summary>The composer's bytes for text its Unicode composer mislabelled: intended text -> (bytes, source).</summary>
    public static Dictionary<string, List<(string Hex, string Source)>> Evidence = new(StringComparer.Ordinal);

    public static List<Truth> Apply(List<Truth> rows, List<string> left, List<string>? log = null)
    {
        // 2 and 3 first, so a ৰ row that takes its র twin's bytes takes them fixed.
        var fixedRows = rows.Select(t =>
        {
            if (USign(t) is { } kept)
            {
                log?.Add($"u sign kept\t{t.U}\t{t.Pref} -> {kept}");
                return new Truth(t.U, kept, new[] { kept });
            }
            if (t.U == "র্")
            {
                log?.Add($"word-final ra\t{t.U}\t{t.Pref} -> B9 F4");
                return new Truth(t.U, "B9 F4", new[] { "B9 F4" });
            }
            if (!t.Acc.Any(a => a.Split(' ').Contains("FF")) || !t.U.Any(IsCons)) return t;
            string pref = Filler(t.U, t.Pref);
            var acc = t.Acc.Select(a => Filler(t.U, a)).Distinct().ToList();
            acc.Remove(pref); acc.Insert(0, pref);
            log?.Add($"filler\t{t.U}\t{t.Pref} -> {pref}");
            return new Truth(t.U, pref, acc.ToArray());
        }).ToList();

        // 6b. A sign the reph row loses that its bare syllable has (র্হ্রৃ = D2 EA A1 A2 A1, হ্রৃ = D2 EA A1 F9: the
        // ra-phala F9 is gone): the bare syllable's spelling with the reph before its spacer (D2 EA A2 A1 F9), as
        // র্হৃ = D2 EA A2 A1 and the composer's own র্ড়ু = D8 6C A2 A1 E6.
        var bare = fixedRows.ToDictionary(t => t.U, StringComparer.Ordinal);
        fixedRows = fixedRows.Select(t =>
        {
            if (!t.U.StartsWith("র্", StringComparison.Ordinal) || t.Pref.Length == 0) return t;
            if (!bare.TryGetValue(t.U[2..], out var x) || !x.Pref.Split(' ').Contains("A1")) return t;
            var have = t.Pref.Split(' ').Where(c => c is not ("A1" or "A2")).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            bool lost = x.Pref.Split(' ').Where(c => c != "A1").GroupBy(c => c).Any(g => have.GetValueOrDefault(g.Key) < g.Count());
            if (!lost) return t;
            var toks = x.Pref.Split(' ').ToList();
            toks.Insert(toks.IndexOf("A1"), "A2");
            string kept = string.Join(" ", toks);
            log?.Add($"sign kept under a reph\t{t.U}\t{t.Pref} -> {kept} (as {x.U} = {x.Pref})");
            return new Truth(t.U, kept, new[] { kept, t.Pref });   // the composer's own spelling reads as the text too (P5)
        }).ToList();

        var byU = fixedRows.ToDictionary(t => t.U, StringComparer.Ordinal);
        var result = new List<Truth>(fixedRows.Count);
        foreach (var t in fixedRows)
        {
            string twin = RaTwin(t.U);
            if (twin == t.U) { result.Add(t); continue; }
            if (!byU.TryGetValue(twin, out var tw))
            {
                // 5. No র twin in the capture.
                var no = NoTwin(t, twin, byU);
                if (no is null) { left.Add($"rra-no-ra-twin\t{t.U}\t{t.Pref}"); continue; }
                log?.Add($"rra no twin ({no.Value.Why})\t{t.U}\t{t.Pref} -> {no.Value.Hex}");
                result.Add(new Truth(t.U, no.Value.Hex, new[] { no.Value.Hex }));
                continue;
            }
            log?.Add($"rra reph/ra-phala\t{t.U}\t{t.Pref} -> {tw.Pref} (as {twin})");
            result.Add(new Truth(t.U, tw.Pref, tw.Acc));
        }
        return C1Conjuncts(result, left, log);
    }

    /// <summary>
    /// Decision 6: ড় ঢ় with ু or ূ and a ঁ, a reph or a ra-phala under the reph. The composer writes
    /// ড়ুঁ ড়ূঁ as D8 6C A1 F2 A1, র্ড়ু র্ড়ূ as D8 6C A1 A2 A1, র্ড়ুং as D8 6C A1 A2 A1 7D, র্ড়ুঁ as
    /// D8 6C A1 A2 F2 A1, র্ড়্রু as D8 6C F6 A1 A2 A1 (and the same with ঢ় 6E): nukta, ড, the blank
    /// spacer A1, the reph A2, ঁ F2 - nothing there draws ু or ূ, and u and uu are written alike. Its
    /// own alternatives keep the sign, the way ড়ু itself is D8 6C A1 E6: র্ড়ু = D8 6C A2 A1 E6, র্ড়ূ =
    /// D8 6C A2 A1 E9 (also র্ঢ়ু র্ঢ়ূ). So these 20 rows (and the Assamese ৰ rows that take their
    /// bytes) are written that way: the letter, then the ra-phala F6, the reph A2 and ঁ F2 (each where
    /// the row has it, in that order - র্ড়্র = D8 6C F6 A2 A1, র্কুঁ = 41 E5 A2 F2 A1, ড়ঁ = D8 6C F2 A1),
    /// the spacer, ু E6 or ূ E9, then ং 7D (ড়ুং = D8 6C A1 E6 7D). The composer's own spelling still
    /// reads back (its kept spacer is the ু, Reader.UMark).
    /// </summary>
    static readonly System.Text.RegularExpressions.Regex RraU =
        new("^(র্)?(ড়|ঢ়)(্র)?([ুূ])([ঁং])?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    static string? USign(Truth t)
    {
        var m = RraU.Match(t.U);
        if (!m.Success) return null;
        if (t.Pref.Split(' ').Any(c => c is "E5" or "E6" or "E8" or "E9")) return null;   // the sign is drawn
        var codes = new List<string> { "D8", m.Groups[2].Value == "ড়" ? "6C" : "6E" };
        if (m.Groups[3].Success) codes.Add("F6");
        if (m.Groups[1].Success) codes.Add("A2");
        if (m.Groups[5].Value == "ঁ") codes.Add("F2");
        codes.Add("A1");
        codes.Add(m.Groups[4].Value == "ু" ? "E6" : "E9");
        if (m.Groups[5].Value == "ং") codes.Add("7D");
        return string.Join(" ", codes);
    }

    /// <summary>The ra-phala ্ৰ that carries a hasanta or another ra-phala: ক্ৰ্ ক্ৰ্র (not a reph ৰ্).</summary>
    static bool RaPhalaWithHasanta(string u)
    {
        for (int i = 2; i + 1 < u.Length; i++)
            if (u[i] == 'ৰ' && IsVirama(u[i - 1]) && IsVirama(u[i + 1]) && (IsCons(u[i - 2]) || u[i - 2] == '়')) return true;
        return false;
    }

    /// <summary>Decision 5: a ৰ row whose র twin is not in the capture.</summary>
    static (string Hex, string Why)? NoTwin(Truth t, string twin, Dictionary<string, Truth> byU)
    {
        // A ra-phala with a hasanta of its own: the composer's spelling draws both ras.
        if (RaPhalaWithHasanta(t.U) && !t.U.StartsWith("ৰ্")) return (t.Pref, "composer's own, a ra-phala with a hasanta");
        if (!twin.StartsWith("র্")) return null;
        // The reph: what the composer writes for the twin typed in Shree's order (reph struck last).
        if (Evidence.TryGetValue(twin, out var ev))
        {
            var pick = ev.OrderBy(e => e.Source.Contains(" 40 =") ? 0 : 1).First();
            return (pick.Hex, "reph, the composer's bytes for " + twin + " (" + pick.Source + ")");
        }
        string rest = twin[2..];
        // ঃ where ং stands: র্কঃ as র্কং.
        if (rest.EndsWith("ঃ") && byU.TryGetValue(twin[..^1] + "ং", out var anus) && anus.Pref.EndsWith("7D"))
            return (anus.Pref[..^2] + "40", "reph, ঃ where " + anus.U + " has ং");
        // The ya-phala, then the reph (as র্ক্য = 41 A1 B8 A2).
        if (rest.EndsWith("্য"))
        {
            // (র্য in the capture is the reph on য; under a reph it is র with the ya-phala, র‌্য = B9 B8)
            string c = rest[..^2];
            string? hex = c == "র" ? (byU.TryGetValue("র\u200C্য", out var rya) ? rya.Pref : null)
                : byU.TryGetValue(rest, out var ya) ? ya.Pref
                : byU.TryGetValue(c, out var cr) ? cr.Pref + " B8" : null;
            if (hex is not null) return (hex + " A2", "reph after the ya-phala, as র্ক্য");
        }
        return null;
    }

    /// <summary>Decision 4: ব্ঢ ব্ড চ্ঞ (C1 code points) as letter + visible hasanta + letter.</summary>
    static readonly (string Code, string Conj, string Second, string SecondCode, string Half)[] C1 =
    {
        ("81", "ব্ঢ", "ঢ", "6E", "A4 F4"),
        ("8D", "ব্ড", "ড", "6C", "A4 F4"),
        ("90", "চ্ঞ", "ঞ", "64", "57 F4 A1"),
    };

    static List<Truth> C1Conjuncts(List<Truth> rows, List<string> left, List<string>? log)
    {
        var byU = rows.ToDictionary(t => t.U, StringComparer.Ordinal);
        var result = new List<Truth>(rows.Count);
        foreach (var t in rows)
        {
            var hit = C1.Where(c => t.Acc.Any(a => a.Split(' ').Contains(c.Code))).ToList();
            if (hit.Count == 0) { result.Add(t); continue; }
            var (code, conj, second, secondCode, half) = hit[0];
            int at = t.U.IndexOf(conj, StringComparison.Ordinal);
            string twin = at < 0 ? "" : t.U.Remove(at, conj.Length).Insert(at, second);
            if (hit.Count > 1 || at < 0 || !byU.TryGetValue(twin, out var tw) || C1.Any(c => tw.Acc.Any(a => a.Split(' ').Contains(c.Code))))
            {
                left.Add($"c1-no-twin\t{t.U}\t{t.Pref}");
                continue;
            }
            string? Insert(string hex)
            {
                var toks = hex.Split(' ').ToList();
                int k = toks.IndexOf(secondCode);
                if (k < 0) return null;
                toks.Insert(k, half);
                return string.Join(" ", toks);
            }
            var acc = tw.Acc.Select(Insert).Where(a => a is not null).Select(a => a!).Distinct().ToArray();
            if (acc.Length == 0 || Insert(tw.Pref) is not string pref) { left.Add($"c1-no-twin\t{t.U}\t{t.Pref}"); continue; }
            log?.Add($"c1 conjunct\t{t.U}\t{t.Pref} -> {pref} (as {conj[..2]} + {twin})");
            result.Add(new Truth(t.U, pref, acc));
        }
        return result;
    }

    /// <summary>The text with every ৰ that stands as a reph or a ra-phala written র.</summary>
    public static string RaTwin(string u)
    {
        var s = u.ToCharArray();
        var reph = new bool[s.Length];
        for (int i = 0; i + 2 < s.Length; i++)
            if (s[i] is 'ৰ' or 'র' && IsVirama(s[i + 1]) && (IsCons(s[i + 2]) || IsVowel(s[i + 2])) && (i == 0 || !IsVirama(s[i - 1])))
                reph[i] = true;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != 'ৰ') continue;
            if (reph[i]) { s[i] = 'র'; continue; }
            // ra-phala: after a hasanta on a letter (with its nukta) that is not itself a reph
            int j = i - 2;
            if (i >= 2 && IsVirama(s[i - 1]) && s[j] == '়') j--;
            if (i >= 2 && IsVirama(s[i - 1]) && j >= 0 && IsCons(s[j]) && !reph[j]) s[i] = 'র';
        }
        return new string(s);
    }

    /// <summary>A spelling with the filler FF between two letters replaced (decision 2).</summary>
    public static string Filler(string u, string hex)
    {
        var t = hex.Split(' ').ToList();
        for (int k = 1; k + 1 < t.Count; k++)
        {
            if (t[k] != "FF") continue;
            if (t[k + 1] == "A4" && (t[k - 1] is "8B" or "A4") && (u.Contains("ধ্ব") || u.Contains("ব্ব")))
            {
                // ধ্ব ব্ব: the subscript ব, and ু ূ under it as E6 E9 (as থ্ব = 3D AB, থ্বু = 3D AB E6)
                t[k] = "AB"; t.RemoveAt(k + 1);
                if (k + 1 < t.Count) t[k + 1] = t[k + 1] switch { "E5" => "E6", "E8" => "E9", var x => x };
            }
            else if (t[k - 1] == "A1")
            {
                // a letter with its spacer (ড়্গ: D8 6C A1 FF 4B): the hasanta goes before the spacer
                t[k] = "A1"; t[k - 1] = "F4";
            }
            else t[k] = "F4";
        }
        return string.Join(" ", t);
    }
}
