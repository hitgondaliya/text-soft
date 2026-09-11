using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FontSoftware.Licensing;
using Microsoft.Win32;

namespace FontSoftware.App.Services;

/// <summary>
/// Windows device fingerprint for license binding. Derived from stable hardware/OS
/// identifiers — the registry <c>MachineGuid</c>, the system-drive volume serial, and
/// the processor count/architecture — hashed together. Stable across reboots and app
/// updates, but different on another machine, so a copied activation will not verify.
/// </summary>
public sealed class WindowsDeviceIdProvider : IDeviceIdProvider
{
    public string GetDeviceId()
    {
        var sb = new StringBuilder();
        sb.Append(MachineGuid()).Append('|');
        sb.Append(SystemVolumeSerial()).Append('|');
        sb.Append(Environment.ProcessorCount).Append('|');
        sb.Append(RuntimeInformation.ProcessArchitecture).Append('|');
        sb.Append(Environment.MachineName);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash); // 64 hex chars
    }

    private static string MachineGuid()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? "no-guid";
        }
        catch
        {
            return "no-guid";
        }
    }

    private static string SystemVolumeSerial()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var sb = new StringBuilder(261);
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
