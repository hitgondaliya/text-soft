using System.IO;
using System.Text.Json;

namespace FontSoftware.Licensing;

/// <summary>
/// The serial and key the installer collected and verified with the server. It
/// drops them in %ProgramData%\TextSoft\pending-activation.json; the app picks the
/// file up on first launch, activates with this PC's fingerprint, and deletes it.
/// The installer cannot activate itself because the fingerprint the licence is
/// bound to is computed by the app.
/// </summary>
public sealed record PendingActivation(string Serial, string Key)
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TextSoft", "pending-activation.json");

    public static PendingActivation? TryRead(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            string serial = r.TryGetProperty("serial", out var s) ? s.GetString() ?? "" : "";
            string key = r.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(key)) return null;
            return new PendingActivation(serial.Trim(), key.Trim());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Remove the file once the app has consumed it (best effort).</summary>
    public static void Clear(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // the installer gave Users modify rights on the folder; if that failed the
            // file is harmless — activation ignores it once a licence exists
        }
    }
}
