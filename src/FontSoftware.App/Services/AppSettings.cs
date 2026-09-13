using System.Text.Json.Serialization;

namespace FontSoftware.App.Services;

/// <summary>
/// User preferences persisted locally (FR-6). Loaded on launch, saved on change.
/// Stores the default typing profile and the toggle key.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Windows virtual-key code of the toggle key. Default 0x14 = CapsLock.</summary>
    [JsonPropertyName("toggleKey")]
    public int ToggleKey { get; set; } = 0x14;

    [JsonPropertyName("defaultLanguageId")]
    public string? DefaultLanguageId { get; set; }

    [JsonPropertyName("defaultFontId")]
    public string? DefaultFontId { get; set; }

    [JsonPropertyName("defaultLayoutId")]
    public string? DefaultLayoutId { get; set; }

    /// <summary>Show the on-screen notification when typing is toggled (FR-3/FR-6).</summary>
    [JsonPropertyName("showToggleNotification")]
    public bool ShowToggleNotification { get; set; } = true;

    /// <summary>Start the app automatically at Windows login (installer/NFR option).</summary>
    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    /// <summary>
    /// Process names (no extension) where system-wide typing is suppressed.
    /// FR-3 lists secure fields / elevated windows / games as out of scope; this
    /// lets the user add known-problem apps. Defaults cover common cases.
    /// </summary>
    [JsonPropertyName("excludedProcesses")]
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        "mstsc",      // Remote Desktop
        "keepass",    // password manager
        "1password"   // password manager
    };
}
