using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FontSoftware.App.Infrastructure;

/// <summary>
/// The other half of a pair of views that swap: where one is shown for true, this
/// shows the other for false. Used to put the ordinary text box and the glyph-index
/// renderer in the same cell.
/// </summary>
public sealed class InverseBoolToVisibility : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
