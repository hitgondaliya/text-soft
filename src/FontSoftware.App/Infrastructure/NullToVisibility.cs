using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FontSoftware.App.Infrastructure;

/// <summary>Visible when the bound value is a non-empty string (used for inline field errors).</summary>
public sealed class NullToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
