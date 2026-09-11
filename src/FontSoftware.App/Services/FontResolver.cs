using System.IO;
using System.Windows.Media;
using FontSoftware.Core.Models;

namespace FontSoftware.App.Services;

/// <summary>
/// Turns a <see cref="FontAsset"/> into a renderable WPF <see cref="FontFamily"/>.
/// Bundled fonts (those with a <c>file</c> under the config root) are loaded
/// privately from disk, so legacy Shree fonts render in the app WITHOUT being
/// installed system-wide. Fonts without a bundle fall back to the installed
/// family by name.
/// </summary>
public sealed class FontResolver
{
    // Nirmala UI (Windows 8+) renders every supported Indic script, so it is the
    // primary fallback: a Unicode font that isn't installed still shows its
    // glyphs instead of tofu. Segoe UI is the final Latin fallback.
    private const string Fallback = "Nirmala UI, Segoe UI";
    private readonly string _configRoot;

    public FontResolver(string configRoot) => _configRoot = configRoot;

    /// <summary>Absolute path to a font asset's bundled file, or null if none / missing.</summary>
    public string? BundledPath(FontAsset? font)
    {
        if (string.IsNullOrWhiteSpace(font?.File)) return null;
        string full = Path.GetFullPath(Path.Combine(_configRoot, font.File));
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// True if the font can be rendered: it's bundled with the app, or its family
    /// is installed on this PC. Bundled fonts are always available.
    /// </summary>
    public bool IsAvailable(FontAsset font) =>
        BundledPath(font) is not null
        || (!string.IsNullOrWhiteSpace(font.Family) && SystemFontService.IsInstalled(font.Family));

    /// <summary>The WPF font family to render this asset's text in.</summary>
    public FontFamily Resolve(FontAsset? font)
    {
        if (font is null) return new FontFamily(Fallback);

        string? bundled = BundledPath(font);
        if (bundled is not null && !string.IsNullOrWhiteSpace(font.Family))
        {
            // Base URI must be the folder (with trailing slash); location is
            // "./#<FamilyName>" so WPF loads the private font from that folder.
            string dir = Path.GetDirectoryName(bundled)!;
            if (!dir.EndsWith(Path.DirectorySeparatorChar)) dir += Path.DirectorySeparatorChar;
            return new FontFamily(new Uri(dir), $"./#{font.Family}");
        }

        return !string.IsNullOrWhiteSpace(font.Family)
            ? new FontFamily($"{font.Family}, {Fallback}")
            : new FontFamily(Fallback);
    }
}
