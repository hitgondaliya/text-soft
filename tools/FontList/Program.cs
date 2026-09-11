using System.Windows.Media;

// FontList — enumerate installed font families as .NET 8 (WPF) sees them, so we
// can confirm per-user installed fonts are visible to the TextSoft app.
var en = System.Windows.Markup.XmlLanguage.GetLanguage("en-US");
var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
foreach (var f in Fonts.SystemFontFamilies)
    names.Add(f.FamilyNames.TryGetValue(en, out var n) ? n : f.Source);

string[] want = { "aakar", "Shruti", "Amita", "Biryani", "Mukta", "SHREE-DEVX-0700",
    "SHREE-GUJX-0756", "SHREE-GUJX-0757", "SHREE-GUJX-0793", "SHREE-GUJX-1120",
    "B Bharati Aswad", "B Bharati Kautilya", "B Bharati Sharmila", "AabadTwo",
    "AakarTwo", "AjantaTwo", "Hitarth Guj Neena", "ITRFK-Guj-Brush" };

Console.WriteLine($"total families visible to .NET 8: {names.Count}\n");
foreach (var w in want)
{
    var hit = names.FirstOrDefault(x => x.Contains(w, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"  {w,-22} {(hit != null ? "INSTALLED (" + hit + ")" : "missing")}");
}
