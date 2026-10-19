using System.Text;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Conversion;

/// <summary>
/// Phonetic "type by sound" converter. Turns a roman string into Indic script
/// using a <see cref="TransliterationScheme"/> — e.g. "namaste" → नमस्ते,
/// "gujaraatii" → ગુજરાતી. Deterministic and rule-based (not dictionary-based),
/// so words are spelled the way they sound.
///
/// Composition rules:
///  - A consonant carries an inherent 'a'. Two consonants in a row form a conjunct,
///    so a virama is inserted between them.
///  - A vowel after a consonant becomes a matra (the inherent 'a' uses an empty
///    matra, i.e. it just leaves the bare consonant).
///  - A vowel not preceded by a consonant becomes its independent form.
///  - A word-final consonant keeps its inherent 'a' (no trailing virama).
/// </summary>
public sealed class Transliterator
{
    private enum Kind { Consonant, Vowel, Special }

    private readonly TransliterationScheme _scheme;
    // All roman tokens, longest first, so "kh"/"aa" win over "k"/"a".
    private readonly List<(string Roman, Kind Kind)> _tokens;

    public Transliterator(TransliterationScheme scheme)
    {
        _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));

        _tokens = new List<(string, Kind)>();
        foreach (var k in scheme.Consonants.Keys) _tokens.Add((k, Kind.Consonant));
        foreach (var k in scheme.Vowels.Keys) _tokens.Add((k, Kind.Vowel));
        foreach (var k in scheme.Specials.Keys) _tokens.Add((k, Kind.Special));
        // Longest token first; case-sensitive so "T"/"D"/"S" (retroflex) differ from "t"/"d"/"s".
        _tokens.Sort((a, b) => b.Roman.Length.CompareTo(a.Roman.Length));
    }

    public TransliterationScheme Scheme => _scheme;

    /// <summary>Convert a roman string to script.</summary>
    public string Convert(string roman)
    {
        if (string.IsNullOrEmpty(roman))
            return string.Empty;

        var sb = new StringBuilder(roman.Length * 2);
        bool prevConsonant = false; // an unresolved consonant is pending (inherent 'a')
        int i = 0;

        while (i < roman.Length)
        {
            var match = MatchAt(roman, i);
            if (match is null)
            {
                // Unknown character (space, digit, punctuation): the pending consonant
                // keeps its inherent 'a'; emit the character verbatim and reset.
                sb.Append(roman[i]);
                prevConsonant = false;
                i++;
                continue;
            }

            var (token, kind) = match.Value;
            switch (kind)
            {
                case Kind.Consonant:
                    if (prevConsonant)
                        sb.Append(_scheme.Virama); // conjunct join
                    sb.Append(_scheme.Consonants[token]);
                    prevConsonant = true;
                    break;

                case Kind.Vowel:
                    var forms = _scheme.Vowels[token];
                    string independent = forms.Length > 0 ? forms[0] : string.Empty;
                    string matra = forms.Length > 1 ? forms[1] : string.Empty;
                    sb.Append(prevConsonant ? matra : independent);
                    prevConsonant = false;
                    break;

                case Kind.Special:
                    // Attaches to the current syllable; the pending consonant keeps
                    // its inherent 'a' (no virama).
                    sb.Append(_scheme.Specials[token]);
                    prevConsonant = false;
                    break;
            }

            i += token.Length;
        }

        return sb.ToString();
    }

    private (string Token, Kind Kind)? MatchAt(string text, int index)
    {
        foreach (var (roman, kind) in _tokens)
        {
            if (index + roman.Length > text.Length)
                continue;
            if (string.CompareOrdinal(text, index, roman, 0, roman.Length) == 0)
                return (roman, kind);
        }
        return null;
    }
}
