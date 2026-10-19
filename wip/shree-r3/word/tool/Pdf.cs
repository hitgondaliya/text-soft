using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace WordVis;

public sealed class PRef { public int N; public override string ToString() => N + " R"; }
public sealed class PName { public string V = ""; public override string ToString() => "/" + V; }
public sealed class PStr { public byte[] V = Array.Empty<byte>(); public bool Hex; }
public sealed class POp { public string V = ""; public override string ToString() => V; }

/// Minimal PDF reader for Word's own PDF export: objects, object streams, pages, fonts, content.
public sealed class PdfFile
{
    readonly byte[] b;
    readonly string s;   // latin1 view
    public readonly Dictionary<int, object> Objs = new();
    public readonly Dictionary<int, byte[]> Streams = new();

    public PdfFile(string path)
    {
        b = File.ReadAllBytes(path);
        s = Encoding.Latin1.GetString(b);
        foreach (Match m in Regex.Matches(s, @"(?<![0-9])(\d+)\s+(\d+)\s+obj\b"))
        {
            int n = int.Parse(m.Groups[1].Value);
            int p = m.Index + m.Length;
            var lx = new Lexer(s, p);
            object v;
            try { v = lx.Value(); } catch { continue; }
            Objs[n] = v;
            int q = lx.Pos;
            while (q < s.Length && char.IsWhiteSpace(s[q])) q++;
            if (string.CompareOrdinal(s, q, "stream", 0, 6) == 0)
            {
                q += 6;
                if (s[q] == '\r') q++;
                if (s[q] == '\n') q++;
                int len = -1;
                if (v is Dictionary<string, object> dd && dd.TryGetValue("Length", out var lo) && lo is double dl) len = (int)dl;
                int end = len >= 0 && q + len <= s.Length && s.IndexOf("endstream", q + len, Math.Min(20, s.Length - q - len), StringComparison.Ordinal) >= 0 ? q + len : s.IndexOf("endstream", q, StringComparison.Ordinal);
                var raw = new byte[end - q];
                Array.Copy(b, q, raw, 0, raw.Length);
                Streams[n] = Decode(v as Dictionary<string, object>, raw);
            }
        }
        // object streams
        foreach (var kv in Objs.ToList())
        {
            if (kv.Value is Dictionary<string, object> d && d.TryGetValue("Type", out var t) && t is PName pn && pn.V == "ObjStm" && Streams.TryGetValue(kv.Key, out var data))
            {
                string ss = Encoding.Latin1.GetString(data);
                int count = (int)(double)d["N"], first = (int)(double)d["First"];
                var hdr = new Lexer(ss, 0);
                var pairs = new List<(int n, int off)>();
                for (int i = 0; i < count; i++) pairs.Add(((int)(double)hdr.Value(), (int)(double)hdr.Value()));
                foreach (var (n, off) in pairs)
                {
                    if (Objs.ContainsKey(n)) continue;
                    try { Objs[n] = new Lexer(ss, first + off).Value(); } catch { }
                }
            }
        }
    }

    static byte[] Decode(Dictionary<string, object> d, byte[] raw)
    {
        if (d != null && d.TryGetValue("Filter", out var f))
        {
            string fn = f is PName p ? p.V : f is List<object> l && l.Count > 0 && l[0] is PName p2 ? p2.V : "";
            if (fn == "FlateDecode")
            {
                try
                {
                    using var ms = new MemoryStream(raw);
                    using var z = new ZLibStream(ms, CompressionMode.Decompress);
                    using var o = new MemoryStream();
                    z.CopyTo(o);
                    return o.ToArray();
                }
                catch { return raw; }
            }
        }
        return raw;
    }

    public object Res(object o) { int guard = 0; while (o is PRef r && guard++ < 20) o = Objs.TryGetValue(r.N, out var v) ? v : null; return o; }
    public Dictionary<string, object> Dict(object o) => Res(o) as Dictionary<string, object>;
    public byte[] StreamOf(object o) => o is PRef r && Streams.TryGetValue(r.N, out var d) ? d : null;

    public List<Dictionary<string, object>> Pages()
    {
        var res = new List<Dictionary<string, object>>();
        var cat = Objs.Values.OfType<Dictionary<string, object>>().First(d => d.TryGetValue("Type", out var t) && t is PName p && p.V == "Catalog");
        void Walk(Dictionary<string, object> node)
        {
            var type = node.TryGetValue("Type", out var t) && t is PName p ? p.V : "";
            if (type == "Page") { res.Add(node); return; }
            if (node.TryGetValue("Kids", out var k) && Res(k) is List<object> kids)
                foreach (var kid in kids) { var kd = Dict(kid); if (kd != null) Walk(kd); }
        }
        Walk(Dict(cat["Pages"]));
        return res;
    }

    public byte[] Contents(Dictionary<string, object> page)
    {
        var c = page["Contents"];
        var parts = new List<byte[]>();
        if (Res(c) is List<object> arr) { foreach (var x in arr) { var st = StreamOf(x); if (st != null) parts.Add(st); } }
        else { var st = StreamOf(c); if (st != null) parts.Add(st); }
        var ms = new MemoryStream();
        foreach (var p in parts) { ms.Write(p); ms.WriteByte((byte)'\n'); }
        return ms.ToArray();
    }
}

public sealed class Lexer
{
    readonly string s;
    public int Pos;
    public Lexer(string s, int pos) { this.s = s; Pos = pos; }

    void Ws()
    {
        while (Pos < s.Length)
        {
            char c = s[Pos];
            if (c == '%') { while (Pos < s.Length && s[Pos] != '\n' && s[Pos] != '\r') Pos++; }
            else if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\0') Pos++;
            else break;
        }
    }

    static bool Delim(char c) => c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '%' || char.IsWhiteSpace(c) || c == '\0';

    /// Next token: values, or a POp for operators / closers.
    public object Token()
    {
        Ws();
        if (Pos >= s.Length) return null;
        char c = s[Pos];
        if (c == '/')
        {
            int st = ++Pos;
            while (Pos < s.Length && !Delim(s[Pos])) Pos++;
            string nm = s[st..Pos];
            nm = Regex.Replace(nm, "#([0-9A-Fa-f]{2})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            return new PName { V = nm };
        }
        if (c == '(')
        {
            Pos++;
            var bytes = new List<byte>();
            int depth = 1;
            while (Pos < s.Length)
            {
                char ch = s[Pos++];
                if (ch == '\\')
                {
                    char e = s[Pos++];
                    switch (e)
                    {
                        case 'n': bytes.Add(10); break;
                        case 'r': bytes.Add(13); break;
                        case 't': bytes.Add(9); break;
                        case 'b': bytes.Add(8); break;
                        case 'f': bytes.Add(12); break;
                        case '\r': if (Pos < s.Length && s[Pos] == '\n') Pos++; break;
                        case '\n': break;
                        default:
                            if (e >= '0' && e <= '7')
                            {
                                int v = e - '0', k = 1;
                                while (k < 3 && Pos < s.Length && s[Pos] >= '0' && s[Pos] <= '7') { v = v * 8 + (s[Pos++] - '0'); k++; }
                                bytes.Add((byte)v);
                            }
                            else bytes.Add((byte)e);
                            break;
                    }
                }
                else if (ch == '(') { depth++; bytes.Add((byte)ch); }
                else if (ch == ')') { if (--depth == 0) break; bytes.Add((byte)ch); }
                else bytes.Add((byte)ch);
            }
            return new PStr { V = bytes.ToArray() };
        }
        if (c == '<')
        {
            if (Pos + 1 < s.Length && s[Pos + 1] == '<') { Pos += 2; return new POp { V = "<<" }; }
            Pos++;
            var sb = new StringBuilder();
            while (Pos < s.Length && s[Pos] != '>') { if (Uri.IsHexDigit(s[Pos])) sb.Append(s[Pos]); Pos++; }
            Pos++;
            if (sb.Length % 2 == 1) sb.Append('0');
            return new PStr { V = Convert.FromHexString(sb.ToString()), Hex = true };
        }
        if (c == '>' && Pos + 1 < s.Length && s[Pos + 1] == '>') { Pos += 2; return new POp { V = ">>" }; }
        if (c == '[' || c == ']' || c == '{' || c == '}') { Pos++; return new POp { V = c.ToString() }; }
        int st2 = Pos;
        while (Pos < s.Length && !Delim(s[Pos])) Pos++;
        if (Pos == st2) { Pos++; return new POp { V = c.ToString() }; }
        string w = s[st2..Pos];
        if (double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv)) return dv;
        if (w == "true") return true;
        if (w == "false") return false;
        if (w == "null") return null;
        return new POp { V = w };
    }

    public object Value()
    {
        var t = Token();
        return Complete(t);
    }

    object Complete(object t)
    {
        if (t is POp op)
        {
            if (op.V == "<<")
            {
                var d = new Dictionary<string, object>();
                while (true)
                {
                    var k = Token();
                    if (k is POp e && e.V == ">>") break;
                    if (k is not PName kn) throw new FormatException("dict key");
                    d[kn.V] = Value();
                }
                return d;
            }
            if (op.V == "[")
            {
                var l = new List<object>();
                while (true)
                {
                    var x = Token();
                    if (x is POp e && e.V == "]") break;
                    l.Add(Complete(x));
                }
                return l;
            }
            return op;
        }
        if (t is double d1)
        {
            // possible "n g R"
            int save = Pos;
            var t2 = Token();
            if (t2 is double)
            {
                var t3 = Token();
                if (t3 is POp r && r.V == "R") return new PRef { N = (int)d1 };
            }
            Pos = save;
            return d1;
        }
        return t;
    }
}
