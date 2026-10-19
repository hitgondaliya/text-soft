namespace FontSoftware.Core.Diagnostics;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

/// <summary>
/// Minimal logging seam shared across layers. The app provides a file-backed
/// implementation so hook/injection issues on a user's Windows machine can be
/// diagnosed from a log file without a debugger attached.
/// </summary>
public interface IAppLog
{
    void Log(LogLevel level, string message, Exception? error = null);
}

/// <summary>Convenience extensions over <see cref="IAppLog"/>.</summary>
public static class AppLogExtensions
{
    public static void Debug(this IAppLog log, string message) => log.Log(LogLevel.Debug, message);
    public static void Info(this IAppLog log, string message) => log.Log(LogLevel.Info, message);
    public static void Warn(this IAppLog log, string message) => log.Log(LogLevel.Warn, message);
    public static void Error(this IAppLog log, string message, Exception? error = null) => log.Log(LogLevel.Error, message, error);
}

/// <summary>A no-op log, used when logging is not configured.</summary>
public sealed class NullLog : IAppLog
{
    public static readonly NullLog Instance = new();
    public void Log(LogLevel level, string message, Exception? error = null) { }
}
