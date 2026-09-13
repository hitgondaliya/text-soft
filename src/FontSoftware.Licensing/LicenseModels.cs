namespace FontSoftware.Licensing;

/// <summary>Product edition carried by a license (set server-side).</summary>
public enum LicenseEdition
{
    Demo = 0,
    Full = 1
}

/// <summary>
/// Whether the app is usable right now. Under the full-lock model there is no free
/// tier: a valid (demo or full) license means <see cref="Active"/>; anything else —
/// no license, expired, revoked, tampered, grace exhausted — means <see cref="Locked"/>.
/// </summary>
public enum LicenseStatus
{
    Locked = 0,
    Active = 1
}

/// <summary>Outcome of an activation, heartbeat, registration or local re-evaluation.</summary>
public enum LicenseResult
{
    Valid,
    NotActivated,
    /// <summary>The key is on this PC but the person has not registered yet
    /// (name / email / mobile). The app stays on the registration screen.</summary>
    RegistrationRequired,
    Invalid,          // key/token not recognized
    SerialMismatch,   // key exists but not under the serial that was typed
    Revoked,          // admin revoked it
    Expired,          // past expiry
    AlreadyBound,     // key lives on another PC — the server refused and logged it
    Released,         // an admin moved the key off this PC
    DeviceMismatch,   // token bound to another machine, or store copied
    Tampered,         // bad signature or edited local store
    ClockRolledBack,  // system clock earlier than last server-seen time
    Offline,          // offline and past the grace window
    Rejected,         // server refused the request (bad input) — not a network fault
    RateLimited,      // too many tries; wait
    Network           // could not reach the server (transient)
}

/// <summary>Verified contents of a server-signed license token.</summary>
public sealed class LicenseToken
{
    public int V { get; init; }
    public string KeyId { get; init; } = "";
    public string Serial { get; init; } = "";
    public LicenseEdition Edition { get; init; }
    public DateTime? ExpiryUtc { get; init; }
    public string DeviceFp { get; init; } = "";
    public bool Registered { get; init; }
    public DateTime IssuedUtc { get; init; }
    public DateTime ServerTimeUtc { get; init; }
    /// <summary>Hard ceiling on how long this token may be trusted without a heartbeat.
    /// Null for v1 tokens.</summary>
    public DateTime? ValidUntilUtc { get; init; }
    public int GraceDays { get; init; }
    public int HeartbeatHours { get; init; }
    public string Nonce { get; init; } = "";

    public bool IsExpired(DateTime nowUtc) => ExpiryUtc is { } e && nowUtc > e;
    public bool IsStale(DateTime nowUtc) => ValidUntilUtc is { } v && nowUtc > v;
}

/// <summary>What the person typed on the registration screen.</summary>
public sealed record RegistrationInfo(string Name, string Email, string PhoneCountry, string Phone);

/// <summary>
/// Persisted, device-bound license state. The raw <see cref="Token"/> is the signed
/// server token; the two timestamps drive the offline-grace and clock-rollback guards.
/// </summary>
public sealed class LicenseState
{
    public string? Token { get; set; }

    /// <summary>Highest server UTC we have ever seen. A system clock earlier than
    /// this means the clock was rolled back.</summary>
    public DateTime LastServerSeenUtc { get; set; }

    /// <summary>Last successful server contact (activate/heartbeat). Grace is measured
    /// from here.</summary>
    public DateTime LastContactUtc { get; set; }

    /// <summary>Serial the key was activated with, shown on the licence page.</summary>
    public string Serial { get; set; } = "";

    /// <summary>Name/email/mobile the person registered with (display only; the
    /// server copy is authoritative).</summary>
    public string RegisteredName { get; set; } = "";
    public string RegisteredEmail { get; set; } = "";
    public string RegisteredPhone { get; set; } = "";

    // ---- convenience, filled from the verified token at evaluation time ----
    public LicenseStatus Status { get; set; } = LicenseStatus.Locked;
    public LicenseEdition Edition { get; set; } = LicenseEdition.Demo;
    public DateTime? ExpiryUtc { get; set; }
    public bool Registered { get; set; }

    public bool IsActive => Status == LicenseStatus.Active;

    public static LicenseState LockedState() => new() { Status = LicenseStatus.Locked };
}
