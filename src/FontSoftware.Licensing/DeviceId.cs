using System.Security.Cryptography;
using System.Text;

namespace FontSoftware.Licensing;

/// <summary>Supplies a stable per-device identifier for license binding (FR-5).</summary>
public interface IDeviceIdProvider
{
    string GetDeviceId();
}

/// <summary>
/// Cross-platform fallback device id derived from machine name + OS user domain.
/// Adequate for the offline "limit trivial sharing" goal. On Windows, prefer a
/// provider that reads the registry MachineGuid (injected from the app layer)
/// for a hardware-stable id.
/// </summary>
public sealed class DefaultDeviceIdProvider : IDeviceIdProvider
{
    public string GetDeviceId()
    {
        string raw = $"{Environment.MachineName}|{Environment.UserDomainName}|{Environment.OSVersion.Platform}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash, 0, 8); // 16 hex chars
    }
}
