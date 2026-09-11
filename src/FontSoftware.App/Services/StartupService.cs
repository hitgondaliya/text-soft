using Microsoft.Win32;
using FontSoftware.Core.Diagnostics;

namespace FontSoftware.App.Services;

/// <summary>
/// Manages the optional "start at Windows login" entry (FRD NFR: optional
/// auto-start). Writes the same per-user Run key the installer's auto-start task
/// uses, under the same value name, so the in-app toggle and the installer
/// checkbox control one shared entry rather than fighting over two.
/// </summary>
public sealed class StartupService
{
    // MUST match the installer's [Registry] value name ("{#AppName}").
    private const string ValueName = "TextSoft";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IAppLog _log;

    public StartupService(IAppLog? log = null) => _log = log ?? NullLog.Instance;

    /// <summary>Full command written to the Run key: the app exe, quoted.</summary>
    private static string? ExecutablePath
    {
        get
        {
            string? path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) ? null : $"\"{path}\"";
        }
    }

    /// <summary>True if the Run entry currently points at this executable.</summary>
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string existing
                   && string.Equals(existing, ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _log.Error("Could not read auto-start setting.", ex);
            return false;
        }
    }

    /// <summary>
    /// Enable or disable auto-start. Returns true on success; on failure the
    /// caller keeps the old state so the UI never lies about what is persisted.
    /// </summary>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
                return false;

            if (enabled)
            {
                if (ExecutablePath is null)
                {
                    _log.Error("Could not enable auto-start: executable path is unavailable.");
                    return false;
                }
                key.SetValue(ValueName, ExecutablePath, RegistryValueKind.String);
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            _log.Info($"Auto-start {(enabled ? "enabled" : "disabled")}.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Could not {(enabled ? "enable" : "disable")} auto-start.", ex);
            return false;
        }
    }
}
