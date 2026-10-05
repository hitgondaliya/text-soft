using System.Linq;
using System.Windows.Markup;
using System.Windows.Media;

namespace FontSoftware.App.Services;

/// <summary>
/// Enumerates the fonts actually installed on this Windows system, so any font
/// the user installs shows up in the app without editing config. Names are the
/// family names WPF/other apps use to render text.
/// </summary>
public static class SystemFontService
{
    private static readonly XmlLanguage En = XmlLanguage.GetLanguage("en-US");

    /// <summary>All installed font family names, sorted alphabetically.</summary>
    public static IReadOnlyList<string> InstalledFamilies()
    {
        var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var family in Fonts.SystemFontFamilies)
        {
            string name = family.FamilyNames.TryGetValue(En, out var en)
                ? en
                : family.Source;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
        return names.ToList();
    }

    /// <summary>True if a font family with this name is installed on the system.</summary>
    public static bool IsInstalled(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return false;
        // Trimmed: some old faces carry a trailing space in their name ("MarutiStrip ").
        family = family.Trim();
        return Fonts.SystemFontFamilies.Any(f =>
            (f.FamilyNames.TryGetValue(En, out var en) && string.Equals(en.Trim(), family, StringComparison.OrdinalIgnoreCase))
            || string.Equals(f.Source.Trim(), family, StringComparison.OrdinalIgnoreCase));
    }
}
