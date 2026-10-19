using System.Text;
using System.Text.RegularExpressions;
using FontSoftware.Core.Models;

/// <summary>
/// FontConverter's algorithm (pre rules, greedy longest match keyed on the first
/// character, post rules) over a pair dictionary that can change in place - so the
/// learner can try a pair without rebuilding and recompiling a whole table. The
/// tables it learns are always checked again with the real FontConverter.
/// </summary>
public sealed class FastConv
{
    readonly List<Regex> _preRx, _postRx;
    readonly List<string> _preRep, _postRep;
    readonly Dictionary<string, string> _pairs;
    readonly Dictionary<char, List<string>> _byFirst = new();

    public FastConv(IEnumerable<RewriteRule> pre, IEnumerable<RewriteRule> post, Dictionary<string, string> pairs)
    {
        _preRx = pre.Select(r => new Regex(r.Pattern, RegexOptions.Compiled)).ToList();
        _preRep = pre.Select(r => r.Replace).ToList();
        _postRx = post.Select(r => new Regex(r.Pattern, RegexOptions.Compiled)).ToList();
        _postRep = post.Select(r => r.Replace).ToList();
        _pairs = pairs;
        foreach (var k in pairs.Keys) Index(k);
    }

    void Index(string key)
    {
        if (!_byFirst.TryGetValue(key[0], out var list)) _byFirst[key[0]] = list = new List<string>();
        if (list.Contains(key)) return;
        int at = list.FindIndex(k => k.Length < key.Length);
        if (at < 0) list.Add(key); else list.Insert(at, key);
    }

    public void Set(string key, string value)
    {
        bool isNew = !_pairs.ContainsKey(key);
        _pairs[key] = value;
        if (isNew) Index(key);
    }

    public bool Has(string key) => _pairs.ContainsKey(key);

    public string Get(string key) => _pairs[key];

    public void Remove(string key)
    {
        if (!_pairs.Remove(key)) return;
        _byFirst[key[0]].Remove(key);
    }

    public string Pre(string text)
    {
        for (int i = 0; i < _preRx.Count; i++) text = _preRx[i].Replace(text, _preRep[i]);
        return text;
    }

    public string Post(string text)
    {
        for (int i = 0; i < _postRx.Count; i++) text = _postRx[i].Replace(text, _postRep[i]);
        return text;
    }

    /// <summary>Greedy longest match over an already staged string, then the post rules.</summary>
    public string ConvertStaged(string staged)
    {
        var sb = new StringBuilder(staged.Length * 2);
        int i = 0;
        while (i < staged.Length)
        {
            string? hit = null;
            if (_byFirst.TryGetValue(staged[i], out var list))
                foreach (var k in list)
                    if (k.Length <= staged.Length - i && string.CompareOrdinal(staged, i, k, 0, k.Length) == 0) { hit = k; break; }
            if (hit is null) { sb.Append(staged[i]); i++; }
            else { sb.Append(_pairs[hit]); i += hit.Length; }
        }
        return Post(sb.ToString());
    }

    public string Convert(string unicode) => ConvertStaged(Pre(unicode));
}
