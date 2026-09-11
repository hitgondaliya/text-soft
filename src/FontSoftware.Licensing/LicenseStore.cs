using System.IO;

namespace FontSoftware.Licensing;

/// <summary>Persists the opaque license blob locally (FR-5: state survives restart).</summary>
public interface ILicenseStore
{
    string? Read();
    void Write(string blob);
    void Delete();
}

/// <summary>Stores the license blob under %AppData%/TextSoft/license.dat.</summary>
public sealed class FileLicenseStore : ILicenseStore
{
    private readonly string _path;

    public FileLicenseStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextSoft");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "license.dat");
    }

    public string? Read() => File.Exists(_path) ? File.ReadAllText(_path) : null;

    public void Write(string blob) => File.WriteAllText(_path, blob);

    public void Delete()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
