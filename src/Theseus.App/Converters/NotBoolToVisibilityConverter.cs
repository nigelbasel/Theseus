using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Shows an element only when a flag is clear.</summary>
public sealed class NotBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
