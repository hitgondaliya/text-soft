using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontSoftware.App.Services;

/// <summary>A country in the mobile-number picker: name, ISO code, dial code, flag.</summary>
public sealed class Country
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Dial { get; init; } = "";

    /// <summary>"+91"</summary>
    public string DialDisplay => "+" + Dial;

    /// <summary>"India (+91)" for the closed combo box.</summary>
    public string Display => $"{Name} (+{Dial})";

    private ImageSource? _flag;
    /// <summary>Flag bitmap from the embedded flag set (Windows has no emoji flags).</summary>
    public ImageSource? Flag => _flag ??= CountryCatalog.LoadFlag(Code);

    public override string ToString() => Display;
}

/// <summary>
/// The full country list (243 entries) with dialling codes, generated from the
/// libphonenumber metadata and embedded as Assets/countries.json, plus one PNG
/// flag per country under Assets/flags. India is listed first and is the default.
/// </summary>
public static class CountryCatalog
{
    public const string DefaultCode = "IN";

    private static IReadOnlyList<Country>? _all;

    public static IReadOnlyList<Country> All => _all ??= Load();

    public static Country Default => All.FirstOrDefault(c => c.Code == DefaultCode) ?? All[0];

    public static Country? Find(string? code) =>
        string.IsNullOrEmpty(code) ? null : All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<Country> Load()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/countries.json"));
            if (info is null) return Fallback();
            using var reader = new StreamReader(info.Stream);
            var raw = JsonSerializer.Deserialize<List<RawCountry>>(reader.ReadToEnd(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            var list = raw.Where(r => !string.IsNullOrEmpty(r.Code))
                .Select(r => new Country { Code = r.Code!, Name = r.Name ?? r.Code!, Dial = r.Dial ?? "" })
                .ToList();
            // India first, then alphabetical (the source is already alphabetical).
            var india = list.FirstOrDefault(c => c.Code == DefaultCode);
            if (india is not null) { list.Remove(india); list.Insert(0, india); }
            return list.Count > 0 ? list : Fallback();
        }
        catch
        {
            return Fallback();
        }
    }

    private static IReadOnlyList<Country> Fallback() => new[]
    {
        new Country { Code = "IN", Name = "India", Dial = "91" },
        new Country { Code = "US", Name = "United States", Dial = "1" },
        new Country { Code = "GB", Name = "United Kingdom", Dial = "44" },
        new Country { Code = "AE", Name = "United Arab Emirates", Dial = "971" },
    };

    internal static ImageSource? LoadFlag(string code)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri($"pack://application:,,,/Assets/flags/{code.ToLowerInvariant()}.png");
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    private sealed class RawCountry
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Dial { get; set; }
    }
}
