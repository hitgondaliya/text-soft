using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

namespace FontSoftware.App.Services;

/// <summary>
/// Keyboards the user designed themselves. The vendor's own Keyboard Generator
/// tells the typist to "copy it in the directory where Shree-Lipi is installed
/// and then select it"; a keyboard saved here simply appears in the keyboard
/// list, because the shipped assets sit in Program Files where an ordinary user
/// cannot write.
/// </summary>
public sealed class UserKeyboards
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Root { get; }

    public UserKeyboards(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextSoft", "keyboards");
    }

    /// <summary>Add every saved keyboard to the language it was designed for.</summary>
    public int Attach(AssetBundle bundle)
    {
        if (!Directory.Exists(Root)) return 0;
        int added = 0;
        foreach (var language in bundle.Languages)
        {
            string dir = Path.Combine(Root, language.Id);
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
            {
                string id = "user-" + language.Id + "-" + Path.GetFileNameWithoutExtension(file);
                if (language.Layouts.Any(l => l.Id == id)) continue;
                language.Layouts.Add(new LayoutRef { Id = id, Name = NameOf(file), File = file });
                added++;
            }
        }
        return added;
    }

    /// <summary>Write a designed keyboard and hand back the reference the app lists it by.</summary>
    public LayoutRef Save(string languageId, KeyboardLayout layout)
    {
        if (string.IsNullOrWhiteSpace(layout.Name))
            throw new ArgumentException("a keyboard needs a name", nameof(layout));

        string dir = Path.Combine(Root, languageId);
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, Slug(layout.Name) + ".json");
        layout.Id = "user-" + languageId + "-" + Slug(layout.Name);
        File.WriteAllText(file, JsonSerializer.Serialize(layout, Options), new UTF8Encoding(false));
        return new LayoutRef { Id = layout.Id, Name = layout.Name, File = file };
    }

    public void Delete(LayoutRef reference)
    {
        if (IsUserKeyboard(reference) && File.Exists(reference.File)) File.Delete(reference.File);
    }

    public bool IsUserKeyboard(LayoutRef reference) =>
        reference.File.StartsWith(Root, StringComparison.OrdinalIgnoreCase);

    private static string NameOf(string file)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                return name;
        }
        catch (Exception ex) when (ex is IOException or JsonException) { }
        return Path.GetFileNameWithoutExtension(file);
    }

    private static string Slug(string s)
    {
        var chars = s.Trim().ToLowerInvariant()
                     .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                     .ToArray();
        return new string(chars).Trim('-') is { Length: > 0 } slug ? slug : "keyboard";
    }
}
