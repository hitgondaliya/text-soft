using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace FontSoftware.Licensing;

/// <summary>Result of a server call: the outcome, a fresh token on success, and
/// whatever extra the server said (e.g. which PC holds the key).</summary>
public readonly record struct ApiResult(LicenseResult Result, string? Token, string? Detail = null, RegisteredUser? User = null)
{
    public static ApiResult Net => new(LicenseResult.Network, null);
}

/// <summary>Who the server has on record for this key+device (display only).</summary>
public sealed record RegisteredUser(string Name, string Email, string Phone);

/// <summary>Everything the app sends when it claims a key for this PC.</summary>
public sealed record ActivationRequest(
    string Serial,
    string Key,
    string DeviceFp,
    string MachineHint,
    string DeviceName,
    string AppVersion);

/// <summary>Client-side transport to the license server (activate / heartbeat / register).</summary>
public interface ILicenseApi
{
    Task<ApiResult> ActivateAsync(ActivationRequest req, CancellationToken ct = default);
    Task<ApiResult> ValidateAsync(string token, string appVersion, CancellationToken ct = default);
    Task<ApiResult> RegisterAsync(string token, RegistrationInfo info, CancellationToken ct = default);
}

/// <summary>
/// HTTP implementation. Talks to the Next.js server over HTTPS. Only a genuine
/// transport failure maps to <see cref="LicenseResult.Network"/> (which the caller
/// treats as "try the offline grace window"); a server that answers with an error
/// is never mistaken for an outage.
/// </summary>
public sealed class LicenseApiClient : ILicenseApi
{
    private readonly HttpClient _http;

    /// <param name="baseUrl">e.g. https://api.yourdomain.com</param>
    public LicenseApiClient(string baseUrl, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (_http.Timeout == TimeSpan.FromSeconds(100)) // default → tighten
            _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public Task<ApiResult> ActivateAsync(ActivationRequest r, CancellationToken ct = default)
        => PostAsync("api/activate", new
        {
            serial = r.Serial,
            key = r.Key,
            deviceFp = r.DeviceFp,
            machineHint = r.MachineHint,
            deviceName = r.DeviceName,
            appVersion = r.AppVersion
        }, ct);

    public Task<ApiResult> ValidateAsync(string token, string appVersion, CancellationToken ct = default)
        => PostAsync("api/validate", new { token, appVersion }, ct);

    public Task<ApiResult> RegisterAsync(string token, RegistrationInfo info, CancellationToken ct = default)
        => PostAsync("api/register", new
        {
            token,
            name = info.Name,
            email = info.Email,
            phoneCountry = info.PhoneCountry,
            phone = info.Phone
        }, ct);

    private async Task<ApiResult> PostAsync(string path, object body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (resp.IsSuccessStatusCode)
            {
                string? token = TryGet(json, "token");
                return new ApiResult(LicenseResult.Valid, token, null, TryUser(json));
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                return new ApiResult(LicenseResult.RateLimited, null);

            // A 5xx is the server falling over, which for the caller is the same as
            // not being reachable. Anything else is a real answer.
            if ((int)resp.StatusCode >= 500)
                return ApiResult.Net;

            // Error body looks like {"error":"already_bound","boundDeviceName":"OFFICE-PC1"}.
            string? err = TryGet(json, "error");
            string? detail = TryGet(json, "boundDeviceName") ?? TryGet(json, "field") ?? TryGet(json, "reason");
            return new ApiResult(MapError(err), null, detail);
        }
        catch (Exception)
        {
            // Timeouts, DNS, refused connections, TLS, malformed body — all transport.
            return ApiResult.Net;
        }
    }

    private static LicenseResult MapError(string? err) => err switch
    {
        "invalid" => LicenseResult.Invalid,
        "serial_mismatch" => LicenseResult.SerialMismatch,
        "revoked" => LicenseResult.Revoked,
        "expired" => LicenseResult.Expired,
        "already_bound" => LicenseResult.AlreadyBound,
        "device_limit_reached" => LicenseResult.AlreadyBound,
        "released" => LicenseResult.Released,
        "deactivated" => LicenseResult.Released,
        "rate_limited" => LicenseResult.RateLimited,
        _ => LicenseResult.Rejected
    };

    private static RegisteredUser? TryUser(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("user", out var u) || u.ValueKind != JsonValueKind.Object) return null;
            string S(string n) => u.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
            return new RegisteredUser(S("name"), S("email"), S("phone"));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGet(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
