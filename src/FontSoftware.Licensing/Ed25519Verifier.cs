using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace FontSoftware.Licensing;

/// <summary>
/// Verifies Ed25519 signatures produced by the license server, using only the
/// embedded PUBLIC key. The private key never ships — so even a fully
/// reverse-engineered client cannot forge a license token.
///
/// The key is the raw 32-byte Ed25519 public key, base64-encoded. This is the
/// value printed by the server's `npm run genkeys` as `PublicKeyBase64`.
/// </summary>
public sealed class Ed25519Verifier
{
    // The PRODUCTION public key, stamped in for the 13 Sep 2026 release: its
    // private half lives only in the deployed server's .env. Do not "restore" a
    // dev key here - every licence the live server has issued verifies against
    // this one, and changing it locks every customer out until they are reissued.
    // A new keypair means: npm run genkeys -> the server's .env ->
    // scripts\set-prod-config.ps1 -> installer\build-release.ps1.
    public const string DefaultPublicKeyBase64 = "OvrGren3lqZss8fLcfI5M2/otEqL4TtHbXaYrlUqeqc=";

    private readonly Ed25519PublicKeyParameters _key;

    public Ed25519Verifier(string? publicKeyBase64 = null)
    {
        byte[] raw = Convert.FromBase64String(publicKeyBase64 ?? DefaultPublicKeyBase64);
        if (raw.Length != 32)
            throw new ArgumentException("Ed25519 public key must be 32 raw bytes.", nameof(publicKeyBase64));
        _key = new Ed25519PublicKeyParameters(raw, 0);
    }

    /// <summary>True if <paramref name="signature"/> is a valid Ed25519 signature of
    /// <paramref name="message"/> under the embedded public key.</summary>
    public bool Verify(byte[] message, byte[] signature)
    {
        if (signature.Length != 64) return false;
        var signer = new Ed25519Signer();
        signer.Init(forSigning: false, _key);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.VerifySignature(signature);
    }
}

