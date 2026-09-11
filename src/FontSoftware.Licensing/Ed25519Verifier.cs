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
    // TODO(client): DEV key for the generated dev keypair. Replace with your
    // PRODUCTION public key (from `npm run genkeys`) before shipping real builds.
    public const string DefaultPublicKeyBase64 = "4+s4/OseeXn6dCpVDXrRcM8BgDJ9BFoLak8hFs7if0U=";

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
