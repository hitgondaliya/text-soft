using System.Text;
using System.Text.RegularExpressions;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Conversion;

/// <summary>
/// FR-4 conversion engine. Applies a client-supplied <see cref="MappingTable"/>
/// to text using a greedy longest-match substitution, wrapped by optional pre-
/// and post-processing regex rules for reordering edge cases.
///
/// The engine is deliberately dumb: it faithfully applies the table. Any wrong
/// output caused by a faulty table is a client-side data fix, not a code defect
/// (per FRD §FR-4 business rules).
/// </summary>
public sealed class FontConverter
{
    private readonly MappingTable _table;

    // First-character index → candidate pairs sorted by source length (desc),
    // so the longest possible match at any position is tried first.
    private readonly Dictionary<char, List<MappingPair>> _byFirstChar;
    private readonly int _maxSourceLength;

    private readonly List<(Regex Rx, string Replace)> _preRules;
    private readonly List<(Regex Rx, string Replace)> _postRules;

    public FontConverter(MappingTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));

        _byFirstChar = new Dictionary<char, List<MappingPair>>();
        int max = 0;
        foreach (var pair in table.Pairs)
        {
            if (string.IsNullOrEmpty(pair.From))
                continue;

            char key = pair.From[0];
            if (!_byFirstChar.TryGetValue(key, out var list))
            {
                list = new List<MappingPair>();
                _byFirstChar[key] = list;
            }
            list.Add(pair);
            if (pair.From.Length > max)
                max = pair.From.Length;
        }

        // Longest source first — critical so "kshi" wins over "k".
        foreach (var list in _byFirstChar.Values)
            list.Sort((a, b) => b.From.Length.CompareTo(a.From.Length));

        _maxSourceLength = max;

        _preRules = Compile(table.PreRules);
        _postRules = Compile(table.PostRules);
    }

    public MappingTable Table => _table;

    /// <summary>
    /// A noncharacter the typing engine puts after a word it is still typing, when the table has a
    /// pair for it (<see cref="KnowsWaiting"/>): a virama before it is waiting for its next letter,
    /// so a Shree table writes the half form or the reph Shree's composer shows meanwhile - not the
    /// visible halant of a word that ends there. The pair writes nothing.
    /// </summary>
    public const char WaitingMark = '﷯';

    /// <summary>True when the table reads <see cref="WaitingMark"/> (the Shree-Lipi 7 and 4/5/6 Gujarati tables).</summary>
    public bool KnowsWaiting => _byFirstChar.ContainsKey(WaitingMark);

    /// <summary>Convenience overload that discards the unmapped report.</summary>
    public string ConvertText(string input) => Convert(input).Text;

    /// <summary>Convert <paramref name="input"/> per the mapping table.</summary>
    public ConversionResult Convert(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new ConversionResult { Text = string.Empty };

        string staged = ApplyRules(_preRules, input);

        var sb = new StringBuilder(staged.Length);
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var leftOut = new HashSet<string>(StringComparer.Ordinal);
        var kept = new HashSet<string>(StringComparer.Ordinal);

        int i = 0;
        while (i < staged.Length)
        {
            MappingPair? match = MatchAt(staged, i);
            if (match is not null)
            {
                sb.Append(match.To);
                // A pair for a character the target cannot show: reported all the same - as kept when the
                // pair writes the character itself and it is no code of an 8-bit font (U+0100 and up: ₹ in a
                // Shree font, drawn in a fallback font), else as left out or written as a stand-in (a code
                // of the font, a space, U+FFFD).
                if (match.Unmapped)
                {
                    unmapped.Add(match.From);
                    if (match.To.Contains(match.From, StringComparison.Ordinal) && match.From.Any(c => c > (char)0xFF))
                        kept.Add(match.From);
                    else
                        leftOut.Add(match.From);
                }
                i += match.From.Length;
            }
            else if (staged[i] is >= '\uFE00' and <= '\uFE0F')
            {
                // A variation selector picks one shape of what comes before it (the new
                // ruu, U+E3C6, against the old one Shree fonts draw for plain રૂ). A table
                // that has no entry for it has one shape only: it is dropped, never
                // passed into a legacy font as a character of its own.
                i++;
            }
            else
            {
                // No mapping: pass the character through and record it (FR-4).
                // Respect surrogate pairs so emoji/rare glyphs aren't split.
                int len = char.IsHighSurrogate(staged[i]) && i + 1 < staged.Length ? 2 : 1;
                string ch = staged.Substring(i, len);
                sb.Append(ch);
                if (!char.IsWhiteSpace(staged[i]))
                {
                    unmapped.Add(ch);
                    kept.Add(ch);
                }
                i += len;
            }
        }

        string result = ApplyRules(_postRules, sb.ToString());
        return new ConversionResult
        {
            Text = result,
            UnmappedCharacters = unmapped.ToArray(),
            LeftOutCharacters = leftOut.ToArray(),
            KeptCharacters = kept.ToArray()
        };
    }

    private MappingPair? MatchAt(string text, int index)
    {
        if (!_byFirstChar.TryGetValue(text[index], out var candidates))
            return null;

        int remaining = text.Length - index;
        foreach (var pair in candidates)
        {
            int len = pair.From.Length;
            if (len > remaining || len > _maxSourceLength)
                continue;
            if (string.CompareOrdinal(text, index, pair.From, 0, len) == 0)
                return pair;
        }
        return null;
    }

    private static List<(Regex, string)> Compile(IEnumerable<RewriteRule> rules)
    {
        var compiled = new List<(Regex, string)>();
        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule.Pattern))
                continue;
            compiled.Add((new Regex(rule.Pattern, RegexOptions.Compiled), rule.Replace));
        }
        return compiled;
    }

    private static string ApplyRules(List<(Regex Rx, string Replace)> rules, string text)
    {
        foreach (var (rx, replace) in rules)
            text = rx.Replace(text, replace);
        return text;
    }
}
