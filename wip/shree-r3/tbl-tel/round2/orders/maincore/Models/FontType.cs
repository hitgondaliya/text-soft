namespace FontSoftware.Core.Models;

/// <summary>
/// Classifies a font per FRD §1.3. Unicode fonts store real code points and are
/// portable; Legacy fonts store English glyphs drawn to look like the script.
/// </summary>
public enum FontType
{
    Unicode,
    Legacy
}
