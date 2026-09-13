using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FontSoftware.Licensing;
using Microsoft.Win32;

namespace FontSoftware.App.Services;

/// <summary>
/// Windows device fingerprint for license binding. Hashed together from the
/// registry <c>MachineGuid</c>, the system-drive volume serial, the BIOS / board
/// identity and the CPU identifier. Stable across reboots, app updates and a
/// renamed PC (the machine name is deliberately NOT included), but different on
/// any other machine — so a copied activation will not verify, and a key stays
/// on the one PC it was activated on.
///
/// A reinstall of Windows changes the MachineGuid and usually the volume serial,
/// which counts as a new device; support moves the key from the panel.
/// </summary>
public sealed class WindowsDeviceIdProvider : IDeviceIdProvider
{
    private string? _cached;

    public string GetDeviceId()
    {
        if (_cached is not null) return _cached;
        var sb = new StringBuilder();
        sb.Append(MachineGuid()).Append('|');
        sb.Append(SystemVolumeSerial()).Append('|');
        sb.Append(Reg(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer")).Append('|');
        sb.Append(Reg(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName")).Append('|');
        sb.Append(Reg(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct")).Append('|');
        sb.Append(Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "Identifier")).Append('|');
        sb.Append(RuntimeInformation.ProcessArchitecture);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return _cached = Convert.ToHexString(hash); // 64 hex chars
    }

    /// <summary>The raw MachineGuid — the same value the installer reads, so the
    /// server can tell a reinstall on this PC from an install on another one.</summary>
    public string GetMachineHint() => MachineGuid();

    private static string MachineGuid()
    {
        var v = Reg(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid");
        return string.IsNullOrEmpty(v) ? "no-guid" : v;
    }

    private static string Reg(string subKey, string value)
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(subKey);
            return key?.GetValue(value)?.ToString()?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string SystemVolumeSerial()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var fs = new StringBuilder(261);
            if (GetVolumeInformation(root, null, 0, out uint serial, out _, out _, fs, fs.Capacity))
                return serial.ToString("X8");
        }
        catch
        {
            // fall through
        }
        return "no-vol";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);
}
