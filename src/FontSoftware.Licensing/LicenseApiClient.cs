using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace FontSoftware.Licensing;

/// <summary>Result of a server call: the outcome plus a fresh token on success.</summary>
public readonly record struct ApiResult(LicenseResult Result, string? Token);

/// <summary>Client-side transport to the license server (activate / heartbeat / deactivate).</summary>
public interface ILicenseApi
{
    Task<ApiResult> ActivateAsync(string key, string deviceFp, string deviceName, string appVersion, CancellationToken ct = default);
    Task<ApiResult> ValidateAsync(string token, string appVersion, CancellationToken ct = default);
    Task<ApiResult> DeactivateAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// HTTP implementation. Talks to the Next.js server over HTTPS. Any network/parse
/// failure maps to <see cref="LicenseResult.Network"/> so the caller can fall back
/// to the offline grace window instead of hard-locking on a transient outage.
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

    public Task<ApiResult> ActivateAsync(string key, string deviceFp, string deviceName, string appVersion, CancellationToken ct = default)
        => PostAsync("api/activate", new { key, deviceFp, deviceName, appVersion }, ct);

    public Task<ApiResult> ValidateAsync(string token, string appVersion, CancellationToken ct = default)
        => PostAsync("api/validate", new { token, appVersion }, ct);

    public Task<ApiResult> DeactivateAsync(string token, CancellationToken ct = default)
        => PostAsync("api/deactivate", new { token }, ct);

    private async Task<ApiResult> PostAsync(string path, object body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (resp.IsSuccessStatusCode)
            {
                string? token = TryGet(json, "token");
                return new ApiResult(LicenseResult.Valid, token);
            }

            // Error body looks like {"error":"revoked"}.
            string? err = TryGet(json, "error");
            return new ApiResult(MapError(err), null);
        }
        catch (OperationCanceledException)
        {
            return new ApiResult(LicenseResult.Network, null);
        }
        catch (HttpRequestException)
        {
            return new ApiResult(LicenseResult.Network, null);
        }
        catch (Exception)
        {
            return new ApiResult(LicenseResult.Network, null);
        }
    }

    private static LicenseResult MapError(string? err) => err switch
    {
        "invalid" => LicenseResult.Invalid,
        "revoked" => LicenseResult.Revoked,
        "expired" => LicenseResult.Expired,
        "device_limit_reached" => LicenseResult.DeviceLimitReached,
        "deactivated" => LicenseResult.DeviceMismatch,
        _ => LicenseResult.Network
    };

    private static string? TryGet(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(prop, out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
