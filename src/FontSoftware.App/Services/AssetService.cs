using System.IO;
using FontSoftware.Core.Config;

namespace FontSoftware.App.Services;

/// <summary>
/// Resolves the config directory and loads the asset bundle for the app.
/// Config ships under <c>config/</c> next to the executable and is replaced by
/// the client with real assets.
/// </summary>
public sealed class AssetService
{
    public string ConfigRoot { get; }

    public AssetService()
    {
        ConfigRoot = Path.Combine(AppContext.BaseDirectory, "config");
    }

    /// <summary>Load the full bundle. Throws <see cref="ConfigException"/> on bad assets.</summary>
    public AssetBundle Load()
    {
        var loader = new ConfigLoader(ConfigRoot);
        return loader.Load();
    }
}
