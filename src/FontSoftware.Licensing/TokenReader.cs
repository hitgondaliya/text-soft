using System.Text;
using System.Text.Json;

namespace FontSoftware.Licensing;

/// <summary>
/// Parses and verifies the server's signed license token.
/// Wire format: <c>base64url(payloadJson).base64url(ed25519sig)</c>.
/// The signature is verified over the exact decoded JSON bytes before the payload
/// is trusted — see the contract in server/README.md.
/// </summary>
public sealed class TokenReader
{
    private readonly Ed25519Verifier _verifier;

    public TokenReader(Ed25519Verifier verifier) => _verifier = verifier;

    /// <summary>Verify + parse. Returns null if malformed, forged, or unparseable.</summary>
    public LicenseToken? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        int dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return null;

        try
        {
            byte[] json = FromB64Url(token[..dot]);
            byte[] sig = FromB64Url(token[(dot + 1)..]);
            if (!_verifier.Verify(json, sig)) return null;

            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            return new LicenseToken
            {
                V = r.GetProperty("v").GetInt32(),
                KeyId = r.GetProperty("keyId").GetString() ?? "",
                Edition = ParseEdition(r.GetProperty("edition").GetString()),
                ExpiryUtc = ReadUtc(r, "expiryUtc"),
                DeviceFp = r.GetProperty("deviceFp").GetString() ?? "",
                IssuedUtc = ReadUtc(r, "issuedUtc") ?? default,
                ServerTimeUtc = ReadUtc(r, "serverTimeUtc") ?? default,
                GraceDays = r.TryGetProperty("graceDays", out var g) ? g.GetInt32() : 5,
                HeartbeatHours = r.TryGetProperty("heartbeatHours", out var h) ? h.GetInt32() : 24,
                Nonce = r.TryGetProperty("nonce", out var n) ? n.GetString() ?? "" : ""
            };
        }
        catch
        {
            return null;
        }
    }

    private static LicenseEdition ParseEdition(string? s) =>
        string.Equals(s, "full", StringComparison.OrdinalIgnoreCase)
            ? LicenseEdition.Full
            : LicenseEdition.Demo;

    private static DateTime? ReadUtc(JsonElement r, string name)
    {
        if (!r.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null)
            return null;
        var s = el.GetString();
        if (string.IsNullOrEmpty(s)) return null;
        return DateTime.Parse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal
            | System.Globalization.DateTimeStyles.AssumeUniversal);
    }

    private static byte[] FromB64Url(string s)
    {
        string b = s.Replace('-', '+').Replace('_', '/');
        switch (b.Length % 4)
        {
            case 2: b += "=="; break;
            case 3: b += "="; break;
        }
        return Convert.FromBase64String(b);
    }

    /// <summary>Encode helper (used by tests to build tokens).</summary>
    public static string ToB64Url(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>UTF-8 bytes of a payload string (used by tests).</summary>
    public static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);
}
