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

/// <summary>Outcome of an activation, heartbeat, or local re-evaluation.</summary>
public enum LicenseResult
{
    Valid,
    NotActivated,
    Invalid,          // key/token not recognized
    Revoked,          // admin revoked it
    Expired,          // past expiry
    DeviceMismatch,   // token bound to another machine, or store copied
    DeviceLimitReached,
    Tampered,         // bad signature or edited local store
    ClockRolledBack,  // system clock earlier than last server-seen time
    Offline,          // offline and past the grace window
    Network           // could not reach the server (transient)
}

/// <summary>Verified contents of a server-signed license token.</summary>
public sealed class LicenseToken
{
    public int V { get; init; }
    public string KeyId { get; init; } = "";
    public LicenseEdition Edition { get; init; }
    public DateTime? ExpiryUtc { get; init; }
    public string DeviceFp { get; init; } = "";
    public DateTime IssuedUtc { get; init; }
    public DateTime ServerTimeUtc { get; init; }
    public int GraceDays { get; init; }
    public int HeartbeatHours { get; init; }
    public string Nonce { get; init; } = "";

    public bool IsExpired(DateTime nowUtc) => ExpiryUtc is { } e && nowUtc > e;
}

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

    // ---- convenience, filled from the verified token at evaluation time ----
    public LicenseStatus Status { get; set; } = LicenseStatus.Locked;
    public LicenseEdition Edition { get; set; } = LicenseEdition.Demo;
    public DateTime? ExpiryUtc { get; set; }

    public bool IsActive => Status == LicenseStatus.Active;

    public static LicenseState LockedState() => new() { Status = LicenseStatus.Locked };
}
