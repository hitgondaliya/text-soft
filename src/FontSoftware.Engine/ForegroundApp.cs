using System.Text;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>
/// Reports the process behind the focused window, so the typing engine can skip
/// apps the user excludes (FR-3 lists admin-elevated windows, full-screen games
/// and secure fields as out of guaranteed scope).
///
/// This is asked on every keystroke, inside the keyboard hook, where the whole
/// system's input waits for the answer. <c>Process.GetProcessById</c> builds a
/// managed process object to read one string and costs about 7 ms on a fast
/// machine - several times that on a slow one - so this asks Win32 directly and
/// remembers the answer: the window in front rarely changes between two
/// keystrokes, and when it does the process id changes with it.
/// </summary>
internal static class ForegroundApp
{
    private static IntPtr _window;
    private static uint _pid;
    private static string? _name;

    /// <summary>
    /// Process name (without extension, lower-case) of the focused window, or
    /// null if it cannot be determined.
    /// </summary>
    public static string? GetProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        // Both of these are a few microseconds; only the name lookup is dear.
        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return null;

        // Same window and same process as the last keystroke: nothing to look up.
        // The process id is checked too, so a recycled window handle cannot serve
        // the name of a process that has since exited.
        if (hwnd == _window && pid == _pid)
            return _name;

        _window = hwnd;
        _pid = pid;
        _name = Lookup(pid);
        return _name;
    }

    /// <summary>
    /// Is the window in front a spreadsheet? LibreOffice runs Writer and Calc in
    /// one soffice process, and WPS Office its writer and spreadsheets in one
    /// wps process, so only the window title tells them apart.
    /// </summary>
    public static bool IsSpreadsheet()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        var title = new StringBuilder(256);
        if (GetWindowTextW(hwnd, title, title.Capacity) <= 0) return false;
        string t = title.ToString();
        return t.Contains("LibreOffice Calc", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("WPS Spreadsheets", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("WPS Spreadsheet", StringComparison.OrdinalIgnoreCase);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>Forget the cached answer - for a test, or when the setting changes.</summary>
    public static void Forget()
    {
        _window = IntPtr.Zero;
        _pid = 0;
        _name = null;
    }

    private static string? Lookup(uint pid)
    {
        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return null;   // exited, or elevated and out of reach - unknown, not an error

        try
        {
            var path = new StringBuilder(260);
            int size = path.Capacity;
            if (!QueryFullProcessImageName(handle, 0, path, ref size))
                return null;
            return Path.GetFileNameWithoutExtension(path.ToString(0, size)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
