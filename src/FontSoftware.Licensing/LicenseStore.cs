using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FontSoftware.Licensing;

/// <summary>Persists the opaque license blob locally (FR-5: state survives restart).</summary>
public interface ILicenseStore
{
    string? Read();
    void Write(string blob);
    void Delete();
}

/// <summary>
/// Stores the license blob under %AppData%/TextSoft/license.dat.
///
/// On Windows the file is wrapped with DPAPI in machine scope on top of the
/// device-keyed HMAC that <see cref="LicenseManager"/> already applies: the OS
/// itself refuses to decrypt it on any other machine, so copying license.dat to a
/// second PC yields nothing readable, and hand-editing it fails the MAC.
/// </summary>
public sealed class FileLicenseStore : ILicenseStore
{
    private const string Magic = "TSL1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TextSoft/licence/v3");

    private readonly string _path;

    public FileLicenseStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextSoft");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "license.dat");
    }

    public string? Read()
    {
        if (!File.Exists(_path)) return null;
        string raw = File.ReadAllText(_path);
        if (!raw.StartsWith(Magic, StringComparison.Ordinal))
            return raw; // pre-DPAPI file from an earlier build; the MAC still guards it
        try
        {
            byte[] wrapped = Convert.FromBase64String(raw[Magic.Length..]);
            byte[] plain = Unwrap(wrapped);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null; // another machine, another user store, or damaged
        }
    }

    public void Write(string blob)
    {
        byte[] plain = Encoding.UTF8.GetBytes(blob);
        byte[] wrapped = Wrap(plain);
        File.WriteAllText(_path, Magic + Convert.ToBase64String(wrapped));
    }

    public void Delete()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static byte[] Wrap(byte[] plain) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine)
            : plain;

    private static byte[] Unwrap(byte[] wrapped) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(wrapped, Entropy, DataProtectionScope.LocalMachine)
            : wrapped;
}
