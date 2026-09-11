using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FontSoftware.Licensing;

/// <summary>
/// Online licensing facade (full-lock model). Activates a key against the server,
/// stores the returned Ed25519-signed token in a device-bound tamper-evident blob,
/// and re-evaluates on every launch:
///
///  - signature + device-binding checks (offline, always),
///  - expiry check against a clock-rollback-guarded "now",
///  - a periodic online heartbeat that is the source of truth for revoke/expiry,
///  - an offline grace window so a briefly-offline machine keeps working.
///
/// Anything that is not a valid, unexpired, non-revoked, this-device license ends in
/// <see cref="LicenseStatus.Locked"/> — there is no free tier.
/// </summary>
public sealed class LicenseManager
{
    // Tolerance for NTP jitter before we treat an earlier clock as a rollback.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromHours(1);

    private readonly ILicenseApi _api;
    private readonly TokenReader _reader;
    private readonly IDeviceIdProvider _device;
    private readonly ILicenseStore _store;
    private readonly string _appVersion;
    private readonly byte[] _protectSecret;

    public LicenseState Current { get; private set; } = LicenseState.LockedState();
    public LicenseResult LastReason { get; private set; } = LicenseResult.NotActivated;

    public bool IsActive => Current.IsActive;

    public LicenseManager(
        ILicenseApi api,
        TokenReader reader,
        IDeviceIdProvider device,
        ILicenseStore store,
        string appVersion = "0.0.0",
        byte[]? protectSecret = null)
    {
        _api = api;
        _reader = reader;
        _device = device;
        _store = store;
        _appVersion = appVersion;
        _protectSecret = protectSecret ?? Encoding.UTF8.GetBytes("TextSoft-license-protect-v2");
    }

    /// <summary>Activate a key online. On success the token is stored and unlocked.</summary>
    public async Task<LicenseResult> ActivateAsync(string key, string deviceName = "", DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;
        string fp = _device.GetDeviceId();

        var res = await _api.ActivateAsync(key.Trim(), fp, deviceName, _appVersion).ConfigureAwait(false);
        if (res.Result != LicenseResult.Valid || res.Token is null)
            return Lock(res.Result);

        var token = _reader.Read(res.Token);
        if (token is null) return Lock(LicenseResult.Tampered);
        if (!string.Equals(token.DeviceFp, fp, StringComparison.Ordinal))
            return Lock(LicenseResult.DeviceMismatch);

        var state = new LicenseState
        {
            Token = res.Token,
            LastServerSeenUtc = token.ServerTimeUtc,
            LastContactUtc = now
        };
        Persist(state);
        return Evaluate(state, token, now);
    }

    /// <summary>
    /// Load the stored license and (if a heartbeat is due or the clock looks wrong)
    /// re-validate online. Always leaves <see cref="Current"/> in a valid state.
    /// </summary>
    public async Task<LicenseResult> LoadAndRefreshAsync(DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;

        if (!TryLoadState(out var state, out var loadReason) || state is null)
            return Lock(loadReason);

        var token = _reader.Read(state.Token);
        if (token is null) return Lock(LicenseResult.Tampered);

        bool clockBack = now + ClockSkew < state.LastServerSeenUtc;
        bool heartbeatDue = (now - state.LastContactUtc) >= TimeSpan.FromHours(Math.Max(1, token.HeartbeatHours));

        if (heartbeatDue || clockBack)
        {
            var online = await HeartbeatOnceAsync(state, now).ConfigureAwait(false);
            // Network failure falls through to offline evaluation (grace window).
            if (online != LicenseResult.Network)
                return online;
        }

        return Evaluate(state, token, now, clockBack);
    }

    /// <summary>Force an online heartbeat now (e.g. a periodic timer in the app).</summary>
    public async Task<LicenseResult> HeartbeatAsync(DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;
        if (!TryLoadState(out var state, out var reason) || state is null)
            return Lock(reason);
        var r = await HeartbeatOnceAsync(state, now).ConfigureAwait(false);
        if (r == LicenseResult.Network)
        {
            var token = _reader.Read(state.Token);
            return token is null ? Lock(LicenseResult.Tampered) : Evaluate(state, token, now);
        }
        return r;
    }

    /// <summary>Release this device's slot on the server and revert to locked.</summary>
    public async Task DeactivateAsync()
    {
        if (TryLoadState(out var state, out _) && state?.Token is not null)
        {
            try { await _api.DeactivateAsync(state.Token).ConfigureAwait(false); }
            catch { /* best effort */ }
        }
        _store.Delete();
        Lock(LicenseResult.NotActivated);
    }

    // ---- internals ----

    private async Task<LicenseResult> HeartbeatOnceAsync(LicenseState state, DateTime now)
    {
        if (state.Token is null) return Lock(LicenseResult.NotActivated);

        var res = await _api.ValidateAsync(state.Token, _appVersion).ConfigureAwait(false);
        if (res.Result == LicenseResult.Network) return LicenseResult.Network;

        if (res.Result != LicenseResult.Valid || res.Token is null)
            return Lock(res.Result); // revoked / expired / invalid / device removed

        var token = _reader.Read(res.Token);
        if (token is null) return Lock(LicenseResult.Tampered);
        if (!string.Equals(token.DeviceFp, _device.GetDeviceId(), StringComparison.Ordinal))
            return Lock(LicenseResult.DeviceMismatch);

        var fresh = new LicenseState
        {
            Token = res.Token,
            LastServerSeenUtc = Max(state.LastServerSeenUtc, token.ServerTimeUtc),
            LastContactUtc = now
        };
        Persist(fresh);
        return Evaluate(fresh, token, now);
    }

    /// <summary>Pure local evaluation: device match, clock guard, expiry, offline grace.</summary>
    private LicenseResult Evaluate(LicenseState state, LicenseToken token, DateTime now, bool clockBack = false)
    {
        if (!string.Equals(token.DeviceFp, _device.GetDeviceId(), StringComparison.Ordinal))
            return Lock(LicenseResult.DeviceMismatch);

        if (clockBack || now + ClockSkew < state.LastServerSeenUtc)
            return Lock(LicenseResult.ClockRolledBack);

        // Guard expiry against the highest trusted time, so setting the clock back
        // cannot buy extra days even within tolerance.
        DateTime effectiveNow = Max(now, state.LastServerSeenUtc);
        if (token.IsExpired(effectiveNow))
            return Lock(LicenseResult.Expired);

        int graceDays = Math.Max(0, token.GraceDays);
        if ((now - state.LastContactUtc) > TimeSpan.FromDays(graceDays) + TimeSpan.FromHours(12))
            return Lock(LicenseResult.Offline);

        state.Status = LicenseStatus.Active;
        state.Edition = token.Edition;
        state.ExpiryUtc = token.ExpiryUtc;
        Current = state;
        LastReason = LicenseResult.Valid;
        return LicenseResult.Valid;
    }

    private LicenseResult Lock(LicenseResult reason)
    {
        Current = LicenseState.LockedState();
        LastReason = reason;
        return reason;
    }

    private bool TryLoadState(out LicenseState? state, out LicenseResult reason)
    {
        state = null;
        reason = LicenseResult.NotActivated;

        string? blob = _store.Read();
        if (string.IsNullOrEmpty(blob)) { reason = LicenseResult.NotActivated; return false; }

        if (!TryUnprotect(blob, out state) || state is null)
        {
            reason = LicenseResult.DeviceMismatch; // copied to another PC or hand-edited
            state = null;
            return false;
        }
        return true;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private void Persist(LicenseState state) => _store.Write(Protect(state));

    // ---- device-bound tamper-evident envelope: base64(json) "." base64(mac) ----

    private string Protect(LicenseState state)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(state);
        byte[] mac = Mac(json);
        return $"{Convert.ToBase64String(json)}.{Convert.ToBase64String(mac)}";
    }

    private bool TryUnprotect(string blob, out LicenseState? state)
    {
        state = null;
        int dot = blob.IndexOf('.');
        if (dot <= 0 || dot == blob.Length - 1) return false;
        try
        {
            byte[] json = Convert.FromBase64String(blob[..dot]);
            byte[] mac = Convert.FromBase64String(blob[(dot + 1)..]);
            if (!CryptographicOperations.FixedTimeEquals(mac, Mac(json)))
                return false;
            state = JsonSerializer.Deserialize<LicenseState>(json);
            return state is not null;
        }
        catch
        {
            return false;
        }
    }

    // MAC keyed by the current device id, so the envelope only verifies on the
    // machine it was written on (copying license.dat to another PC fails).
    private byte[] Mac(byte[] data)
    {
        byte[] deviceBytes = Encoding.UTF8.GetBytes(_device.GetDeviceId());
        byte[] key = new byte[_protectSecret.Length + deviceBytes.Length];
        Buffer.BlockCopy(_protectSecret, 0, key, 0, _protectSecret.Length);
        Buffer.BlockCopy(deviceBytes, 0, key, _protectSecret.Length, deviceBytes.Length);
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }
}
