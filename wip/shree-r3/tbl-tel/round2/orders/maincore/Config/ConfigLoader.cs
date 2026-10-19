using System.Text.Json;
using System.Text.Json.Serialization;
using FontSoftware.Core.Models;

namespace FontSoftware.Core.Config;

/// <summary>
/// Raised when config/asset files are missing or malformed, so FR-1's
/// "invalid/missing assets handled gracefully with a clear message" can be met.
/// </summary>
public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>
/// Loads the client-supplied asset catalog, keyboard layouts and mapping tables
/// from a config directory. All game data is external per FRD — nothing here is
/// hardcoded to a specific language.
/// </summary>
public sealed class ConfigLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _configRoot;

    /// <param name="configRoot">
    /// Directory containing <c>languages.json</c> plus <c>layouts/</c> and
    /// <c>mappings/</c> referenced files.
    /// </param>
    public ConfigLoader(string configRoot)
    {
        _configRoot = configRoot ?? throw new ArgumentNullException(nameof(configRoot));
    }

    /// <summary>Load the full catalog and all referenced mapping tables.</summary>
    public AssetBundle Load()
    {
        var catalog = LoadCatalog();

        var mappings = new List<MappingTable>();
        foreach (var rel in catalog.MappingFiles)
        {
            var table = LoadMappingTable(rel);
            mappings.Add(table);
        }

        return new AssetBundle(catalog, mappings, this);
    }

    public AssetCatalog LoadCatalog()
    {
        string path = Path.Combine(_configRoot, "languages.json");
        return Deserialize<AssetCatalog>(path, "asset catalog");
    }

    /// <summary>Load a keyboard layout body referenced by a <see cref="LayoutRef"/>.</summary>
    public KeyboardLayout LoadLayout(LayoutRef reference)
    {
        string path = Path.Combine(_configRoot, reference.File);
        var layout = Deserialize<KeyboardLayout>(path, $"keyboard layout '{reference.Name}'");
        layout.Reindex();
        return layout;
    }

    public MappingTable LoadMappingTable(string relativePath)
    {
        string path = Path.Combine(_configRoot, relativePath);
        return Deserialize<MappingTable>(path, $"mapping table '{relativePath}'");
    }

    /// <summary>
    /// Shree-Lipi's internal character codes for one script (GUJ, DEV, ...), the
    /// key to reading its keyboard files. Null when this build has no table for it.
    /// </summary>
    public Keyboards.ShreeCodeTable? LoadShreeCodeTable(string script)
    {
        string path = Path.Combine(_configRoot, "shree-kbd", script.ToUpperInvariant() + ".json");
        return File.Exists(path) ? Deserialize<Keyboards.ShreeCodeTable>(path, $"Shree-Lipi code table '{script}'") : null;
    }

    /// <summary>Load a phonetic "type by sound" scheme referenced by a language.</summary>
    public TransliterationScheme LoadScheme(string relativePath)
    {
        string path = Path.Combine(_configRoot, relativePath);
        return Deserialize<TransliterationScheme>(path, $"transliteration scheme '{relativePath}'");
    }

    private static T Deserialize<T>(string path, string what)
    {
        if (!File.Exists(path))
            throw new ConfigException($"Missing {what}: file not found at '{path}'.");

        try
        {
            using var stream = File.OpenRead(path);
            var value = JsonSerializer.Deserialize<T>(stream, JsonOptions);
            if (value is null)
                throw new ConfigException($"The {what} at '{path}' is empty or null.");
            return value;
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"The {what} at '{path}' is not valid JSON: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Could not read {what} at '{path}': {ex.Message}", ex);
        }
    }
}
