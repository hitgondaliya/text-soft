using System.Text;
using FontSoftware.Core.Conversion;
using static Common;

// ShreeDevTruth - build and check the Shree-Lipi 7 Devanagari tables (DEV7, and the
// 4/5/6 DEV fonts that share its codes; SAN7, the Sanskrit fonts) against what Shree's
// own composers write (tests/FontSoftware.Tests/Data/shree-<script>-truth.json).
//
// The tables are rules plus pairs, as for Gujarati (tools/Guj7Truth). The rules (Dev7.cs,
// San7.cs) carry what the composer does everywhere: reph after its syllable, the ra-, ya-,
// va- and na-signs, signs before a letter's spacer, the i-sign glyph by what it stands
// over, the sign + anusvara and sign + reph glyphs, half letters for a conjunct a virama
// follows. The learner (Learner.cs) then adds a pair only where composing from smaller
// pieces gives the wrong bytes - the half letters, the ligatures, the composer's quirks -
// trying a head of the string (a cluster without its signs) before the whole string.
//
// A virama that ends a word is the visible halant, as every Unicode font draws it - not the
// half form the composer answers for a bare virama, which is only what it shows while it
// waits for the next letter (tools/Shared/ShreeWordFinal.cs says how each such row is spelled;
// the fixture's "exceptions" list them, and the composer's waiting answers are still learned
// as the half forms a following letter joins).
//
// Text that is not Devanagari follows tools/Shared/ShreeNonIndic.cs: punctuation the fonts
// draw is written as its look-alike (“ ” as ‘‘ ’’, … as ...), any other character whose
// code the fonts map is reported as unmapped and left out, never printed as that code's letter.
//
//   shreedevtruth <script> build [outDir]           learn and write the tables (default assets/config/mappings)
//   shreedevtruth <script> verify [dir]              every check on the tables as written
//   shreedevtruth <script> measure [table] [show]    truth strings through the real FontConverter
//   shreedevtruth <script> learned [show]            which strings needed a pair of their own, by shape
//   shreedevtruth <script> back [show]               learn both ways and read the truth back
//   shreedevtruth <script> cv [folds] [seed] [show]  hold-out: learn without some strings, convert them
//   shreedevtruth <script> words [table] [n] [seed]  joined truth syllables, exact bytes
//   shreedevtruth <script> rulecheck [show]          which composer spellings the post rules would change
//   shreedevtruth <script> convert <dir> <file.txt>  lines to bytes and back with the tables in dir
//   shreedevtruth <script> fixture [capture.json]    write the test fixture from the full capture
//   shreedevtruth <script> survey|grep|stage|rstage|show|classes   looking at the data and the rules
//
// <script> is dev7 or san7. The full captures (dev7-groundtruth.json, san7-groundtruth.json)
// are only needed to rebuild a fixture; set SHREE7_CAPTURES to their folder.

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 2) { Console.Error.WriteLine("usage: shreedevtruth <dev7|san7> <mode> ..."); return 1; }
var S = Script.ByName(args[0]);
string mode = args[1];
string Arg(int i, string def) => args.Length > i + 1 ? args[i + 1] : def;

switch (mode)
{
    case "measure":
    {
        var truth = S.LoadTruth();
        var conv = new FontConverter(LoadTable(Arg(1, S.ForwardId + ".json")));
        Console.WriteLine(Checks.Truth(conv, truth, out var fails, S.Excluded));
        foreach (var f in fails.Take(int.Parse(Arg(2, "40")))) Console.WriteLine("  " + f);
        break;
    }
    case "words":
    {
        var truth = S.LoadTruth();
        var conv = new FontConverter(LoadTable(Arg(1, S.ForwardId + ".json")));
        Console.WriteLine(Checks.Words(conv, truth.Where(t => S.Excluded(t) is null).ToList(), int.Parse(Arg(2, "20000")), int.Parse(Arg(3, "7")), out var fails));
        foreach (var f in fails.Take(int.Parse(Arg(4, "40")))) Console.WriteLine("  " + f);
        break;
    }
    case "rulecheck":
    {
        var truth = S.LoadTruth();
        var L = new Learner(S);
        var bad = new List<string>();
        foreach (var t in truth)
        {
            string got = Hex(L.Post(FromHex(t.Pref)));
            if (got != t.Pref) bad.Add($"{Shape(t.U)}\t{t.U}\t{t.Pref}\t-> {got}");
        }
        Console.WriteLine($"post rules change {bad.Count} composer spellings");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(int.Parse(Arg(1, "8")))) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        break;
    }
    case "learn":
    case "learned":
    {
        var truth = S.LoadTruth();
        var L = new Learner(S);
        var log = new List<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pairs = L.Learn(truth, out var stats, log);
        Console.WriteLine($"{stats} ({sw.Elapsed.TotalSeconds:F0}s)");
        var conv = new FontConverter(L.Build(pairs));
        Console.WriteLine(Checks.Truth(conv, truth, out var fails, S.Excluded));
        foreach (var f in fails.Take(10)) Console.WriteLine("  " + f);
        Console.WriteLine(Checks.Words(conv, truth.Where(t => S.Excluded(t) is null).ToList(), 20000, 7, out var wf));
        foreach (var f in wf.Take(10)) Console.WriteLine("  " + f);
        if (mode == "learn")
        {
            string path = Arg(1, Path.Combine(Path.GetTempPath(), S.ForwardId + ".json"));
            Save(L.Build(pairs), path);
            Console.WriteLine("wrote " + path + $" ({new FileInfo(path).Length / 1024} KB)");
        }
        else
        {
            int show = int.Parse(Arg(1, "8"));
            foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {g.Key}: {g.Count()}");
                foreach (var b in g.Take(show)) Console.WriteLine("      " + b);
            }
        }
        break;
    }
    case "cv":
    {
        // cv [folds] [seed] [show] [shapeRegex]: learn on all folds but one, convert the one left out.
        var truth = S.LoadTruth();
        int folds = int.Parse(Arg(1, "10")), seed = int.Parse(Arg(2, "1")), show = int.Parse(Arg(3, "30"));
        var only = args.Length > 5 ? new System.Text.RegularExpressions.Regex(args[5]) : null;
        var rng = new Random(seed);
        var fold = truth.ToDictionary(t => t.U, t => Unit(t.U) ? -1 : rng.Next(folds), StringComparer.Ordinal);
        int ok = 0, n = 0;
        var bad = new List<string>();
        var L = new Learner(S);
        for (int f = 0; f < folds; f++)
        {
            var pairs = L.Learn(truth.Where(t => fold[t.U] != f), out _);
            var conv = new FontConverter(L.Build(pairs));
            foreach (var t in truth.Where(t => fold[t.U] == f && S.Excluded(t) is null))
            {
                if (only is not null && !only.IsMatch(t.U)) continue;
                n++;
                string got = Hex(conv.ConvertText(t.U));
                if (got == t.Pref || t.Acc.Contains(got)) ok++;
                else bad.Add($"{Shape(t.U)}\t{t.U}\twant {t.Pref}\tgot {got}");
            }
            Console.Error.Write($"fold {f} ");
        }
        Console.Error.WriteLine();
        Console.WriteLine($"held out {n}: {ok} right ({100.0 * ok / n:F2}%), {bad.Count} wrong");
        foreach (var g in bad.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(show)) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        break;
    }
    case "back":
    {
        // back [show]: learn the forward table, then the reverse, and read the truth back.
        var truth = S.LoadTruth();
        var L = new Learner(S);
        var fwd = new FontConverter(L.Build(L.Learn(truth, out var fs)));
        Console.WriteLine("forward: " + fs);
        var Rd = new Reader(S, truth);
        var log = new List<string>();
        var back = Rd.Learn(truth, fwd, out var rs, log);
        Console.WriteLine("reverse: " + rs);
        var rev = new FontConverter(Rd.Build(back));
        int show = int.Parse(Arg(1, "15"));
        Console.WriteLine(Rd.Check(rev, fwd, truth, out var rf));
        foreach (var g in Rd.SameText.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  same text {g.Key}: {g.Count()}");
            foreach (var b in g.Take(3)) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        foreach (var g in rf.GroupBy(b => b.Split('\t')[0]).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var b in g.Take(show)) Console.WriteLine("      " + b.Substring(b.IndexOf('\t') + 1));
        }
        foreach (int seed in new[] { 7, 11 })
        {
            Console.WriteLine(Rd.CheckWords(rev, fwd, truth, 20000, seed, out var wf));
            foreach (var f in wf.Take(show)) Console.WriteLine("      " + f);
        }
        Console.WriteLine(Rd.CheckAccepted(rev, fwd, truth, out var af));
        foreach (var f in af.Take(show)) Console.WriteLine("      " + f);
        if (args.Length > 3)
            foreach (var g in log.GroupBy(l => Shape(l.Split('\t')[0])).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  learned {g.Key}: {g.Count()}");
                foreach (var b in g.Take(show)) Console.WriteLine("      " + b);
            }
        break;
    }
    case "build":
    {
        // build [outDir]: learn and write the tables (default: assets/config/mappings).
        Build.All(S, Arg(1, MapDir));
        break;
    }
    case "verify":
    {
        // verify [dir]: every check on the tables as written.
        return Build.Verify(S, Arg(1, MapDir)) ? 0 : 2;
    }
    case "survey":
    {
        // survey [regex on shape] [n]: truth by shape, with examples.
        var truth = S.LoadTruth();
        var rx = new System.Text.RegularExpressions.Regex(Arg(1, "."));
        int n = int.Parse(Arg(2, "12"));
        foreach (var g in truth.Where(t => rx.IsMatch(Shape(t.U))).GroupBy(t => Shape(t.U)).OrderByDescending(g => g.Count()))
            Console.WriteLine($"{g.Key}\t{g.Count()}\t" + string.Join("  ", g.OrderBy(t => t.U, StringComparer.Ordinal).Take(n).Select(t => $"{t.U}={t.Pref}")));
        break;
    }
    case "grep":
    {
        // grep <regex on unicode> [n]: truth rows whose Unicode matches.
        var truth = S.LoadTruth();
        var rx = new System.Text.RegularExpressions.Regex(args[2]);
        int n = int.Parse(Arg(2, "200"));
        foreach (var t in truth.Where(t => rx.IsMatch(t.U)).OrderBy(t => t.U, StringComparer.Ordinal).Take(n))
            Console.WriteLine($"{Shape(t.U)}\t{t.U}\t{t.Pref}{(t.Acc.Length > 1 ? "\t[" + string.Join(" | ", t.Acc.Where(a => a != t.Pref)) + "]" : "")}");
        break;
    }
    case "fixture":
    {
        // fixture [groundtruth.json]: write the compact test fixture from the full capture (spellings as captured).
        var truth = Common.LoadTruth(Arg(1, S.CapturePath));
        var enc = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Joiners and the precomposed nukta letters are written as escapes (an editor could normalise them away).
        string J(string s)
        {
            var sb = new StringBuilder(System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = enc }));
            foreach (char c in "‌‍क़ख़ग़ज़ड़ढ़फ़य़") sb.Replace(c.ToString(), $"\\u{(int)c:X4}");
            return sb.ToString();
        }
        string about = S.Name == "dev7"
            ? "What Shree-Lipi's own composers write for Unicode Devanagari in the Shree-Lipi 7 Devanagari fonts (SHREE-DEV7-*; the 4/5/6 SHREE-DEV-* fonts draw all of these codes the same way). " +
              "Each row: the Unicode string, the preferred bytes (hex, one byte per glyph code: what the 7 composer types for it), and, when there are any, the other spellings the composer also accepts. " +
              "Captured from S8DevU64.dll and S8Dev764.dll (Shree-Lipi nxt) by typing the same keys into both; tools/ShreeDevTruth builds and checks the shree-dev7 and shree-dev456 tables against it."
            : "What Shree-Lipi's own composers write for Unicode Devanagari in the Shree-Lipi 7 Sanskrit fonts (SHREE-SAN7-*, an encoding of their own). " +
              "Each row: the Unicode string, the preferred bytes (hex, one byte per glyph code), and, when there are any, the other spellings the composer also accepts. " +
              "Captured from S8SanU64.dll and S8San764.dll (Shree-Lipi nxt) by typing the same keys into both; tools/ShreeDevTruth builds and checks the shree-san7 tables against it.";
        about += " Rows that end in a bare virama hold the composer's answer while it waits for the next letter (a half form); at the end of a word the table writes " +
                 "the visible halant instead, and \"exceptions\" says, row by row, what it writes (\"the table writes <hex>: why\") - as it does for every row the table spells otherwise.";
        var sbOut = new StringBuilder();
        sbOut.Append("{\n  \"about\": ").Append(J(about));
        var raw = truth.ToDictionary(t => t.U, StringComparer.Ordinal);
        var exceptions = truth.Select(t => (t, why: S.Excluded(t) ?? S.Adjust(t, raw)?.Why)).Where(x => x.why is not null).ToList();
        if (exceptions.Count > 0)
        {
            sbOut.Append(",\n  \"exceptions\": {\n");
            for (int i = 0; i < exceptions.Count; i++)
            {
                var (t, why) = exceptions[i];
                string what = S.Excluded(t) is not null ? "not checked: " + why
                    : S.Adjust(t, raw)!.Value.Pref.Length == 0 ? "the table leaves it out and reports it: " + why
                    : $"the table writes {S.Adjust(t, raw)!.Value.Pref}: " + why;
                sbOut.Append("    ").Append(J(t.U)).Append(": ").Append(J(what)).Append(i < exceptions.Count - 1 ? ",\n" : "\n");
            }
            sbOut.Append("  }");
        }
        sbOut.Append(",\n  \"rows\": [\n");
        var rows = truth.OrderBy(t => t.U, StringComparer.Ordinal).ToList();
        for (int i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var other = t.Acc.Where(a => a != t.Pref).ToList();
            sbOut.Append("    [").Append(J(t.U)).Append(", ").Append(J(t.Pref));
            if (other.Count > 0) sbOut.Append(", [").Append(string.Join(", ", other.Select(J))).Append(']');
            sbOut.Append(']').Append(i < rows.Count - 1 ? ",\n" : "\n");
        }
        sbOut.Append("  ]\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(S.Fixture)!);
        File.WriteAllText(S.Fixture, sbOut.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"wrote {rows.Count} rows, {exceptions.Count} exceptions to {S.Fixture} ({new FileInfo(S.Fixture).Length / 1024} KB)");
        // The fixture must read back as the capture did.
        var again = Common.LoadTruth(S.Fixture).ToDictionary(t => t.U, StringComparer.Ordinal);
        int same = truth.Count(t => again.TryGetValue(t.U, out var a) && a.Pref == t.Pref && a.Acc.OrderBy(x => x).SequenceEqual(t.Acc.OrderBy(x => x)));
        Console.WriteLine($"read back from the fixture: {same}/{truth.Count} rows identical");
        break;
    }
    case "convert":
    {
        // convert <dir> <file.txt>: each line to bytes and back with the tables in dir.
        var fwd = new FontConverter(LoadTable(Path.Combine(args[2], S.ForwardId + ".json")));
        var rev = new FontConverter(LoadTable(Path.Combine(args[2], S.ReverseId + ".json")));
        foreach (var line in File.ReadAllLines(args[3], Encoding.UTF8).Where(l => l.Trim().Length > 0))
        {
            var r = fwd.Convert(line);
            string back = rev.ConvertText(r.Text);
            bool same = FontSoftware.Core.Conversion.Nukta.Spell(back) == FontSoftware.Core.Conversion.Nukta.Spell(line);
            Console.WriteLine($"{line}\n  {Hex(r.Text)}\n  {(same ? "= round trip" : "BACK " + back)}{(r.HasUnmapped ? "  UNMAPPED " + string.Join(" ", r.UnmappedCharacters.Select(Cps)) : "")}");
        }
        break;
    }
    case "classes":
    {
        var (b, st, h) = San7.Classes;
        Console.WriteLine($"bases: {b}\nstemmed: {st}\nhalves: {h}");
        foreach (var r in S.ReversePre.Concat(S.ForwardPost)) Console.WriteLine($"{r.Pattern}  =>  {r.Replace}");
        break;
    }
    case "rstage":
    {
        // rstage <hex>...: what the reverse pre rules make of each spelling.
        var Rd = new Reader(S);
        foreach (var h in args.Skip(2)) Console.WriteLine($"{h}\t-> {Hex(Rd.Staged(FromHex(h)))}");
        break;
    }
    case "stage":
    {
        // stage <unicode>...: what the pre rules make of each string.
        var L = new Learner(S);
        foreach (var u in args.Skip(2)) Console.WriteLine($"{u}\t[{Cps(u)}]\t-> [{Cps(L.Stage(u))}]");
        break;
    }
    case "show":
    {
        // show <table> <unicode>...: convert with a table.
        var conv = new FontConverter(LoadTable(args[2]));
        foreach (var u in args.Skip(3)) Console.WriteLine($"{u}\t[{Cps(u)}]\t-> {Hex(conv.ConvertText(u))}");
        break;
    }
    default:
        Console.Error.WriteLine("unknown mode " + mode);
        return 1;
}
return 0;

// The units text is built from are always known: single characters, half letters
// (C+virama, with or without a joiner), the ra- and ya-clusters (C+virama+ra/ya).
// Everything longer is held out in turn.
static bool Unit(string u) => u.Length <= 2 || Shape(u) is "C_R" or "C_R_" or "C_^" or "C_~" or "R_^" or "R_~" ||
    (u.Length == 3 && (u.EndsWith("्य") || u.EndsWith("्र")));
