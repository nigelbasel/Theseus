using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Shows an element only when a string has something in it.</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
