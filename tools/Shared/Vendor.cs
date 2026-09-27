using System.Text.Json;

namespace FontSoftware.Tools;

/// <summary>
/// The vendor's own names for things, and how our names find them.
///
/// Shree-Lipi keeps one help file and one runtime file per keyboard, named after
/// the keyboard: <c>TypewritGUJ.chm</c> and <c>TYPEWRIT.GUJ</c>. Our layouts are
/// named after those files (<c>guj-typewrit.json</c>) but carry a readable display
/// name ("Typewriter"), so a lookup has to try both - and a handful of keyboards
/// need to be told apart by hand, either because the vendor spells the name two
/// ways or because the keyboard has no file of its own.
/// </summary>
public static class Vendor
{
    /// <summary>Which script each language is written in - the folder the vendor keeps it under.</summary>
    public static readonly Dictionary<string, string> ScriptOf = new()
    {
        ["hi"] = "dev", ["mr"] = "dev", ["sa"] = "dev", ["gu"] = "guj", ["pa"] = "pun",
        ["bn"] = "ban", ["as"] = "ban", ["mni"] = "ban", ["te"] = "tel", ["kn"] = "kan",
        ["ml"] = "mal", ["or"] = "ori", ["ta"] = "tam",
    };

    /// <summary>
    /// A language that has runtime keyboard files of its own inside another
    /// language's script. The vendor ships INSCRIPT.ASS beside INSCRIPT.BAN - the
    /// same keyboard, with Assamese ৱ where Bengali has ব - but writes only one
    /// help file for the two, so the charts stay under the script and the captures
    /// of these files get a folder of their own.
    /// </summary>
    public static readonly Dictionary<string, string> CaptureScriptOf = new() { ["as"] = "ass" };

    /// <summary>Where this language's captures live: its own folder, or the script's.</summary>
    public static string CaptureScript(string langId, string script) =>
        CaptureScriptOf.GetValueOrDefault(langId, script);

    /// <summary>
    /// The keyboards whose name does not lead to the vendor's own file on its own:
    /// Modban is what the Bangla list calls Modular, and Typewri1 and Applen are
    /// the vendor's second spelling of a name their help file spells differently.
    ///
    /// Modular deliberately has no capture alias. The table a composer starts with
    /// is InScript (49 of 50 keys of the published Gujarati InScript chart), not
    /// Modular - whatever TutorGetKbdName calls it - so Modular is a keyboard we
    /// take from its chart alone.
    /// </summary>
    static readonly Dictionary<string, string[]> Alias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["modban"] = new[] { "modular" },       // Modular, in the Bangla list
        ["typewri1"] = new[] { "typewirt1" },   // the vendor's typo, in the Malayalam help
        ["applen"] = new[] { "appleen" },       // Apple English, in the Telugu help
    };

    /// <summary>The keyboards we list that Shree-Lipi does not have one of.</summary>
    public static bool NotShree(string name) =>
        name.StartsWith("Aps ", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Sulekh", StringComparison.OrdinalIgnoreCase);

    public static string Norm(string s) =>
        new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// Every name this keyboard might be filed under: its display name, the stem
    /// of our own file (which is the vendor's file name), and any alias of either.
    /// </summary>
    public static List<string> Names(string displayName, string? ourFile)
    {
        var names = new List<string> { Norm(displayName) };
        if (ourFile is { Length: > 0 })
        {
            string stem = Path.GetFileNameWithoutExtension(ourFile);
            int dash = stem.IndexOf('-');          // guj-typewrit -> typewrit
            names.Add(Norm(dash > 0 ? stem[(dash + 1)..] : stem));
        }
        foreach (string n in names.ToList())
            if (Alias.TryGetValue(n, out string[]? more)) names.AddRange(more);
        return names.Distinct().ToList();
    }

    /// <summary>The vendor file for this keyboard in <paramref name="dir"/>/&lt;script&gt;, or null.</summary>
    public static string? Find(string dir, string script, string displayName, string? ourFile)
    {
        if (dir.Length == 0) return null;
        string d = Path.Combine(dir, script);
        if (!Directory.Exists(d)) return null;
        var names = Names(displayName, ourFile);
        return Directory.EnumerateFiles(d, "*.json")
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .FirstOrDefault(f => names.Contains(Norm(Path.GetFileNameWithoutExtension(f))));
    }

    // ---- the vendor's runtime keyboard files ----

    /// <summary>The extension the vendor gives a runtime keyboard file, by script.</summary>
    public static readonly Dictionary<string, string> SchemeExt = new()
    {
        ["dev"] = ".DEV", ["guj"] = ".GUJ", ["ban"] = ".BAN", ["pun"] = ".PUN", ["tel"] = ".TEL",
        ["kan"] = ".KAN", ["mal"] = ".MAL", ["ori"] = ".ORI", ["tam"] = ".TAM",
    };

    /// <summary>The vendor's runtime file for this keyboard, or null when they ship none.</summary>
    public static string? SchemeFile(string schemeDir, string script, string displayName, string? ourFile)
    {
        if (schemeDir.Length == 0 || !SchemeExt.TryGetValue(script, out string? ext)) return null;
        var names = Names(displayName, ourFile);
        return Directory.EnumerateFiles(schemeDir, "*" + ext)
                        .FirstOrDefault(f => names.Contains(Norm(Path.GetFileNameWithoutExtension(f))));
    }

    /// <summary>
    /// code -> character, learned across a script's keyboards.
    ///
    /// A runtime file holds an internal code for every key - not Unicode, and not
    /// the font's byte. But each script has around twenty keyboards, all of them
    /// arrangements of the same codes, so joining each chart (key -> character)
    /// with its own runtime file (key -> code) on the keys that chart does print
    /// gives code -> character, and a code one chart never prints another one does.
    ///
    /// Only readings several keyboards agree on are kept: a code seen once, or one
    /// the keyboards are split over, is a guess, and a guess on a key is worse than
    /// a key that types nothing.
    /// </summary>
    public static Dictionary<int, string> LearnCodes(string schemeDir, string chartDir, string extension)
    {
        var votes = new Dictionary<int, Dictionary<string, int>>();
        if (!Directory.Exists(schemeDir) || !Directory.Exists(chartDir)) return new();

        var charts = Directory.EnumerateFiles(chartDir, "*.json")
                              .ToDictionary(f => Norm(Path.GetFileNameWithoutExtension(f)), f => f);
        foreach (string scheme in Directory.EnumerateFiles(schemeDir, "*" + extension))
        {
            if (!charts.TryGetValue(Norm(Path.GetFileNameWithoutExtension(scheme)), out string? chart)) continue;
            byte[] codes = File.ReadAllBytes(scheme);
            if (codes.Length < 128) continue;

            using var doc = JsonDocument.Parse(File.ReadAllText(chart));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.GetProperty("Pairs").EnumerateArray())
            {
                string key = p.GetProperty("Key").GetString() ?? "";
                string uni = p.GetProperty("Uni").GetString() ?? "";
                if (key.Length != 1 || uni.Length == 0 || !seen.Add(key)) continue;
                if (key[0] is < '!' or > '~') continue;

                int code = codes[key[0]];
                if (code is 0 or 0x20) continue;   // the vendor leaves this key empty
                var reading = votes.TryGetValue(code, out var r) ? r : votes[code] = new(StringComparer.Ordinal);
                reading[uni] = reading.GetValueOrDefault(uni) + 1;
            }
        }

        var settled = new Dictionary<int, string>();
        foreach (var (code, reading) in votes)
        {
            var ranked = reading.OrderByDescending(r => r.Value).ToList();
            int total = ranked.Sum(r => r.Value);
            if (ranked[0].Value >= 2 && ranked[0].Value * 2 > total) settled[code] = ranked[0].Key;
        }
        return settled;
    }

    /// <summary>The solution root, found by walking up from the running tool.</summary>
    public static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("solution root not found");
    }

    /// <summary>An invisible formatting character is not a keystroke anyone is taught.</summary>
    public static bool Formatting(string s) =>
        s.Length > 0 && s.All(c => c is (char)0x00AD or (char)0x200B or (char)0x200C or (char)0x200D
                                     or (char)0x200E or (char)0x200F or (char)0xFEFF);

    // ---- when the chart and the composer disagree about one key ----

    /// <summary>The kinds of difference there are between a chart's answer and the composer's.</summary>
    public enum Difference
    {
        Same,
        /// <summary>The same text, one of them with a joiner in it.</summary>
        JoinerOnly,
        /// <summary>The composer puts a virama after the letter: the half form.</summary>
        HalfForm,
        /// <summary>The composer puts a virama before it: the subjoined form.</summary>
        SubjoinedForm,
        /// <summary>The chart prints the independent vowel, the composer answers with its matra.</summary>
        MatraForVowel,
        /// <summary>Gurmukhi: the composer answers with the bearer the vowel is written on.</summary>
        VowelBearer,
        /// <summary>A mark that cannot stand alone, answered on a carrier letter (reph + ka).</summary>
        MarkOnCarrier,
        /// <summary>The other way round: the chart prints the mark on a carrier letter.</summary>
        ChartOnCarrier,
        /// <summary>The composer spells a vowel in the two pieces it is drawn from.</summary>
        TwoPartVowel,
        /// <summary>The same characters in the order the composer stores them.</summary>
        OtherOrder,
        /// <summary>The composer hands back the key itself, unchanged.</summary>
        PlainKey,
        /// <summary>They name different characters.</summary>
        DifferentCharacters,
    }

    static bool Indic(char c) => c is >= 'ऀ' and <= 'ൿ';
    static bool Virama(char c) => Indic(c) && (c & 0x7F) == 0x4D;
    static bool Consonant(char c) => Indic(c) && (c & 0x7F) is >= 0x15 and <= 0x39;
    static bool Mark(char c) => Indic(c) && ((c & 0x7F) is >= 0x3E and <= 0x57 or 0x62 or 0x63 or <= 0x03);
    static bool VowelLetter(char c) => Indic(c) && ((c & 0x7F) is >= 0x05 and <= 0x14 || c is 'ॲ');
    static readonly char[] Joiners = { '​', '‌', '‍', '‎', '‏', '­', '﻿' };
    static string NoJoin(string s) => new(s.Where(c => !Joiners.Contains(c)).ToArray());
    static string Sorted(string s) { var a = s.ToCharArray(); Array.Sort(a); return new(a); }

    /// <summary>How the composer's answer for a key differs from the chart's.</summary>
    public static Difference Compare(string chart, string composer)
    {
        string a = NoJoin(chart), b = NoJoin(composer);
        if (a == b) return chart == composer ? Difference.Same : Difference.JoinerOnly;
        if (b.Length > 1 && Virama(b[^1]) && b[..^1] == a) return Difference.HalfForm;
        if (b.Length > 1 && Virama(b[0]) && b[1..] == a) return Difference.SubjoinedForm;
        if (a.Length == 1 && VowelLetter(a[0]) && b.Length > 0 && b.All(Mark)) return Difference.MatraForVowel;
        if (a.Length == 1 && VowelLetter(a[0]) && b.Length > 0 && b[0] is 'ੳ' or 'ਅ' or 'ੲ')
            return Difference.VowelBearer;
        if (b.Length == a.Length + 1 && b.StartsWith(a, StringComparison.Ordinal)
            && Virama(a[^1]) && Consonant(b[^1]))
            return Difference.MarkOnCarrier;
        if (b.Length == 3 && a.Length == 1 && b[0] == a[0] && Virama(b[1]) && Consonant(b[2]))
            return Difference.MarkOnCarrier;
        if (a.Length == b.Length + 1 && a.EndsWith(b, StringComparison.Ordinal)
            && Consonant(a[0]) && b.Length > 0 && Virama(b[0]))
            return Difference.ChartOnCarrier;
        if (a.Length > 1 && Sorted(a) == Sorted(b)) return Difference.OtherOrder;
        if (b.Length > 0 && !b.Any(Indic)) return Difference.PlainKey;
        // A two-part vowel: Tamil and Bengali draw some of theirs from two pieces,
        // and that is how the composer spells them back.
        if (b.Length == 2 && b.All(Indic) && a.Length <= 2 && a.All(Indic)
            && (VowelLetter(a[0]) || a.Length == 2 && VowelLetter(a[0]))
            && (b.Any(Mark) || b.Any(VowelLetter)))
            return Difference.TwoPartVowel;
        return Difference.DifferentCharacters;
    }

    /// <summary>
    /// Which of the two vendor sources to believe for a key they disagree about.
    ///
    /// The chart is the vendor's own teaching material and stands by default: asked
    /// about one key on its own the composer answers with the state that key leaves
    /// the text in - a matra where a typist is taught the vowel - and our multi-key
    /// runs are built on the chart's reading.
    ///
    /// The exception is a chart row that repeats itself: one character given to two
    /// keystrokes of the same keyboard, or one keystroke given two characters. One
    /// of the two rows has lost its own character then, and the composer is the one
    /// that still knows what that key types. On the Punjabi Eng keyboard the chart puts ਡ on
    /// both "D" and "D`", and the composer says "D" is ਡ੍: the half form, which is
    /// exactly what the backtick after it undoes. Every family the tell picks out
    /// reads that way - the nukta printed as an avagraha, the subjoined consonants
    /// no Punjabi chart writes down, the reph - and the vowel keys of the phonetic
    /// keyboards, where the chart is right, are not among them.
    /// </summary>
    public static bool ComposerWins(Difference how, bool chartRepeatsItself) => how switch
    {
        Difference.Same => false,
        Difference.MatraForVowel or Difference.VowelBearer => false,
        // A keystroke's output should not carry a letter that is only there to show
        // the mark sitting on something: the Bengali Akruti chart writes the
        // ya-phala as ন্য, and no key types that leading ন.
        Difference.ChartOnCarrier => true,
        _ => chartRepeatsItself,
    };

    /// <summary>
    /// What the key really types, given the composer's answer. A mark that cannot
    /// stand on its own comes back sitting on a carrier letter - the Kannada reph
    /// as ರ್ಕ on eight different keyboards, always the same ಕ - so the carrier
    /// comes off.
    /// </summary>
    public static string Payload(Difference how, string composer) =>
        how == Difference.MarkOnCarrier ? composer[..^1] : composer;

    /// <summary>key -> text out of a capture's "keys" or "alter" object.</summary>
    public static Dictionary<string, string> CaptureMap(JsonElement root, string property)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.Object) return map;
        foreach (var p in node.EnumerateObject())
            if (p.Value.GetString() is { Length: > 0 } text) map[p.Name] = text;
        return map;
    }
}
