using System.Diagnostics;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>
/// Reports the process behind the focused window, so the typing engine can skip
/// apps the user excludes (FR-3 lists admin-elevated windows, full-screen games
/// and secure fields as out of guaranteed scope).
/// </summary>
internal static class ForegroundApp
{
    /// <summary>
    /// Process name (without extension, lower-case) of the focused window, or
    /// null if it cannot be determined.
    /// </summary>
    public static string? GetProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return null;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.ToLowerInvariant();
        }
        catch
        {
            // Process may have exited, or be inaccessible (e.g. elevated) — treat
            // as unknown rather than throwing inside the hook path.
            return null;
        }
    }
}
