using System.IO;
using FontSoftware.Core.Diagnostics;

namespace FontSoftware.App.Services;

/// <summary>
/// Appends log entries to a daily file under
/// <c>%AppData%\TextSoft\logs\</c>. Gives users (and us) a way to
/// diagnose hook/injection problems on their machine without a debugger.
/// Best-effort: logging failures never throw into the app.
/// </summary>
public sealed class FileLog : IAppLog
{
    private readonly object _gate = new();
    private readonly string _dir;

    public FileLog()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextSoft", "logs");
        try { Directory.CreateDirectory(_dir); } catch { /* ignore */ }
    }

    public string LogDirectory => _dir;

    public void Log(LogLevel level, string message, Exception? error = null)
    {
        try
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}";
            if (error is not null)
                line += Environment.NewLine + error;

            string path = Path.Combine(_dir, $"app-{DateTime.Now:yyyyMMdd}.log");
            lock (_gate)
                File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // Never let logging break the app.
        }
    }
}
