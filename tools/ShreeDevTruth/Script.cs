using FontSoftware.Core.Models;
using static Common;

/// <summary>
/// One Shree-Lipi 7 Devanagari encoding: its capture, its rules, and the characters
/// outside the capture. DEV7 (SHREE-DEV7-*, and the 4/5/6 SHREE-DEV-* fonts, which
/// share all but four codes) and SAN7 (SHREE-SAN7-*, an encoding of its own) are the
/// two instances; the learner, the reader and the checks are the same for both.
/// </summary>
public sealed class Script
{
    public required string Name { get; init; }                 // dev7 / san7
    public required string Title { get; init; }                // Shree-Lipi 7.0 Devanagari
    public required string CaptureFile { get; init; }          // dev7-groundtruth.json
    public string Fixture => Path.Combine(DataDir, $"shree-{Name}-truth.json");
    public string ForwardId => $"unicode-to-shree-{Name}";
    public string ReverseId => $"shree-{Name}-to-unicode";
    public string Encoding => $"shree-{Name}";

    public required List<RewriteRule> ForwardPre { get; init; }
    public required List<RewriteRule> ForwardPost { get; init; }
    public required List<RewriteRule> ReversePre { get; init; }
    public required List<RewriteRule> ReversePost { get; init; }
    /// <summary>Unicode -> the visual order the reverse pairs answer in (the inverse of the reverse post rules).</summary>
    public required List<RewriteRule> VisualRules { get; init; }

    /// <summary>Atoms for the markers the pre rules leave in the text (reph, ya-sign, ra-sign, ...).</summary>
    public required (string Marker, string Hex)[] MarkerAtoms { get; init; }

    /// <summary>Characters outside the truth, and what to write for them (hex; "" = the font cannot show it).</summary>
    public required (string U, string Hex, string Why)[] Extra { get; init; }

    /// <summary>Codes of the font the composer does not write alone, as the font draws them (for reading).</summary>
    public required (string Hex, string Unicode)[] FontCodes { get; init; }

    /// <summary>Codes the table must never write (they draw something else in a sister family).</summary>
    public required HashSet<string> NeverWrite { get; init; }

    /// <summary>Truth strings the composer spells with a code the table must not write (by reason).</summary>
    public Func<Truth, string?> Excluded { get; init; } = _ => null;

    /// <summary>The Unicode as the bytes draw it (the composer drops a few marks in some spellings).</summary>
    public Func<string, string, string> Visible { get; init; } = (u, _) => u;

    public string CapturePath => Path.Combine(CaptureDir, CaptureFile);

    /// <summary>A composer spelling the table writes another way (and why), or null; the second argument is every composer row by Unicode.</summary>
    public Func<Truth, IReadOnlyDictionary<string, Truth>, (string Pref, string Why)?> Adjust { get; init; } = (_, _) => null;

    /// <summary>The halant code and the spacer codes a letter can end in (for a virama that ends a word, see WordFinal).</summary>
    public required string Halant { get; init; }
    public required string[] Spacers { get; init; }

    /// <summary>The truth the tables are built and checked against: the composer's rows, with the adjusted ones (Adjust) as the table writes them.</summary>
    public List<Truth> LoadTruth(string? from = null)
    {
        from ??= File.Exists(Fixture) ? Fixture : CapturePath;
        return WordFinal.Adjust(Common.LoadTruth(from), Adjust);
    }

    public static Script ByName(string name) => name switch
    {
        "dev7" => Dev7.Script,
        "san7" => San7.Script,
        _ => throw new ArgumentException("unknown script " + name),
    };
}
