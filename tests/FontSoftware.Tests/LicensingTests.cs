using System.Text;
using FontSoftware.Licensing;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// Online-model licensing tests. A throwaway Ed25519 keypair is generated per test
/// so we can mint server-shaped tokens and verify the full activate → store →
/// re-evaluate → heartbeat lifecycle without a network.
/// </summary>
public class LicensingTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    // ---- test doubles ----

    private sealed class MemoryStore : ILicenseStore
    {
        private string? _blob;
        public string? Read() => _blob;
        public void Write(string blob) => _blob = blob;
        public void Delete() => _blob = null;
        public string? Raw => _blob;
        public void Set(string? blob) => _blob = blob;
    }

    private sealed class FixedDevice : IDeviceIdProvider
    {
        private readonly string _id;
        public FixedDevice(string id) => _id = id;
        public string GetDeviceId() => _id;
    }

    /// <summary>Signs tokens with a test private key; the public half feeds the verifier.</summary>
    private sealed class TestSigner
    {
        private readonly Ed25519PrivateKeyParameters _priv;
        public string PublicKeyBase64 { get; }

        public TestSigner()
        {
            var gen = new Ed25519KeyPairGenerator();
            gen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
            var pair = gen.GenerateKeyPair();
            _priv = (Ed25519PrivateKeyParameters)pair.Private;
            var pub = (Ed25519PublicKeyParameters)pair.Public;
            PublicKeyBase64 = Convert.ToBase64String(pub.GetEncoded());
        }

        public string MakeToken(string deviceFp, LicenseEdition edition, DateTime serverTime,
            DateTime? expiry, int graceDays = 5, int heartbeatHours = 24)
        {
            string exp = expiry is { } e ? $"\"{e:yyyy-MM-ddTHH:mm:ss.fffZ}\"" : "null";
            string json =
                $"{{\"v\":1,\"keyId\":\"k1\",\"edition\":\"{(edition == LicenseEdition.Full ? "full" : "demo")}\"," +
                $"\"expiryUtc\":{exp},\"deviceFp\":\"{deviceFp}\"," +
                $"\"issuedUtc\":\"{serverTime:yyyy-MM-ddTHH:mm:ss.fffZ}\"," +
                $"\"serverTimeUtc\":\"{serverTime:yyyy-MM-ddTHH:mm:ss.fffZ}\"," +
                $"\"graceDays\":{graceDays},\"heartbeatHours\":{heartbeatHours},\"nonce\":\"n1\"}}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var signer = new Ed25519Signer();
            signer.Init(true, _priv);
            signer.BlockUpdate(bytes, 0, bytes.Length);
            byte[] sig = signer.GenerateSignature();
            return $"{TokenReader.ToB64Url(bytes)}.{TokenReader.ToB64Url(sig)}";
        }
    }

    private sealed class FakeApi : ILicenseApi
    {
        public Func<string, ApiResult>? OnActivate;
        public Func<string, ApiResult>? OnValidate;

        public Task<ApiResult> ActivateAsync(string key, string deviceFp, string deviceName, string appVersion, CancellationToken ct = default)
            => Task.FromResult(OnActivate?.Invoke(key) ?? new ApiResult(LicenseResult.Invalid, null));

        public Task<ApiResult> ValidateAsync(string token, string appVersion, CancellationToken ct = default)
            => Task.FromResult(OnValidate?.Invoke(token) ?? new ApiResult(LicenseResult.Network, null));

        public Task<ApiResult> DeactivateAsync(string token, CancellationToken ct = default)
            => Task.FromResult(new ApiResult(LicenseResult.Valid, null));
    }

    private static (LicenseManager mgr, MemoryStore store, TokenReader reader) Build(
        TestSigner signer, FakeApi api, string device)
    {
        var reader = new TokenReader(new Ed25519Verifier(signer.PublicKeyBase64));
        var store = new MemoryStore();
        var mgr = new LicenseManager(api, reader, new FixedDevice(device), store, "1.0.0");
        return (mgr, store, reader);
    }

    // ---- crypto primitives ----

    [Fact]
    public void VerifierAcceptsGoodSignatureRejectsTamper()
    {
        var signer = new TestSigner();
        var reader = new TokenReader(new Ed25519Verifier(signer.PublicKeyBase64));
        string token = signer.MakeToken("DEV", LicenseEdition.Full, Now, null);

        Assert.NotNull(reader.Read(token));

        // Flip a char in the payload → signature no longer matches → null.
        string bad = (token[0] == 'A' ? 'B' : 'A') + token[1..];
        Assert.Null(reader.Read(bad));
    }

    [Fact]
    public void WrongKeyIsRejected()
    {
        var a = new TestSigner();
        var b = new TestSigner();
        var reader = new TokenReader(new Ed25519Verifier(b.PublicKeyBase64));
        Assert.Null(reader.Read(a.MakeToken("DEV", LicenseEdition.Full, Now, null)));
    }

    // ---- lifecycle ----

    [Fact]
    public async Task ActivatePersistsAndReloads()
    {
        var signer = new TestSigner();
        var api = new FakeApi { OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Full, Now, null)) };
        var (mgr, store, _) = Build(signer, api, "DEV-A");

        Assert.Equal(LicenseResult.Valid, await mgr.ActivateAsync("TXSF-x", "PC", Now));
        Assert.True(mgr.IsActive);
        Assert.Equal(LicenseEdition.Full, mgr.Current.Edition);
        Assert.NotNull(store.Raw);

        // Reload offline shortly after (no heartbeat due) → still active.
        var reloaded = new LicenseManager(api, new TokenReader(new Ed25519Verifier(signer.PublicKeyBase64)),
            new FixedDevice("DEV-A"), store, "1.0.0");
        Assert.Equal(LicenseResult.Valid, await reloaded.LoadAndRefreshAsync(Now.AddHours(1)));
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task CopiedStoreFailsOnDifferentDevice()
    {
        var signer = new TestSigner();
        var api = new FakeApi { OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Full, Now, null)) };
        var (mgr, store, _) = Build(signer, api, "DEV-A");
        await mgr.ActivateAsync("k", "PC", Now);

        // Same blob, different machine → MAC + device-fp checks fail → locked.
        var other = new LicenseManager(api, new TokenReader(new Ed25519Verifier(signer.PublicKeyBase64)),
            new FixedDevice("DEV-B"), store, "1.0.0");
        Assert.Equal(LicenseResult.DeviceMismatch, await other.LoadAndRefreshAsync(Now.AddHours(1)));
        Assert.False(other.IsActive);
    }

    [Fact]
    public async Task ExpiredTokenLocks()
    {
        var signer = new TestSigner();
        // Token already expired at issue-time+... reload well past expiry, no heartbeat.
        var api = new FakeApi
        {
            OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Demo, Now, Now.AddDays(7))),
            OnValidate = _ => new ApiResult(LicenseResult.Network, null) // offline so local expiry decides
        };
        var (mgr, store, _) = Build(signer, api, "DEV-A");
        await mgr.ActivateAsync("k", "PC", Now);

        Assert.Equal(LicenseResult.Expired, await mgr.LoadAndRefreshAsync(Now.AddDays(8)));
        Assert.False(mgr.IsActive);
    }

    [Fact]
    public async Task RevokeViaHeartbeatLocks()
    {
        var signer = new TestSigner();
        var api = new FakeApi
        {
            OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Full, Now, null)),
            OnValidate = _ => new ApiResult(LicenseResult.Revoked, null)
        };
        var (mgr, store, _) = Build(signer, api, "DEV-A");
        await mgr.ActivateAsync("k", "PC", Now);

        // Heartbeat due (25h later) → server says revoked → locked.
        Assert.Equal(LicenseResult.Revoked, await mgr.LoadAndRefreshAsync(Now.AddHours(25)));
        Assert.False(mgr.IsActive);
    }

    [Fact]
    public async Task OfflineWithinGraceStaysActivePastGraceLocks()
    {
        var signer = new TestSigner();
        var api = new FakeApi
        {
            OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Full, Now, null, graceDays: 5)),
            OnValidate = _ => new ApiResult(LicenseResult.Network, null) // always offline
        };
        var (mgr, store, _) = Build(signer, api, "DEV-A");
        await mgr.ActivateAsync("k", "PC", Now);

        // 3 days offline → within 5-day grace → active.
        Assert.Equal(LicenseResult.Valid, await mgr.LoadAndRefreshAsync(Now.AddDays(3)));
        Assert.True(mgr.IsActive);

        // 6+ days offline → past grace → locked.
        Assert.Equal(LicenseResult.Offline, await mgr.LoadAndRefreshAsync(Now.AddDays(6)));
        Assert.False(mgr.IsActive);
    }

    [Fact]
    public async Task ClockRolledBackLocksWhenOffline()
    {
        var signer = new TestSigner();
        var api = new FakeApi
        {
            OnActivate = _ => new ApiResult(LicenseResult.Valid, signer.MakeToken("DEV-A", LicenseEdition.Full, Now, null)),
            OnValidate = _ => new ApiResult(LicenseResult.Network, null)
        };
        var (mgr, store, _) = Build(signer, api, "DEV-A");
        await mgr.ActivateAsync("k", "PC", Now);

        // System clock set well before the last server-seen time → rollback → locked.
        Assert.Equal(LicenseResult.ClockRolledBack, await mgr.LoadAndRefreshAsync(Now.AddDays(-30)));
        Assert.False(mgr.IsActive);
    }

    [Fact]
    public async Task InvalidKeyStaysLocked()
    {
        var signer = new TestSigner();
        var api = new FakeApi { OnActivate = _ => new ApiResult(LicenseResult.Invalid, null) };
        var (mgr, _, _) = Build(signer, api, "DEV-A");
        Assert.Equal(LicenseResult.Invalid, await mgr.ActivateAsync("BAD", "PC", Now));
        Assert.False(mgr.IsActive);
    }
}
