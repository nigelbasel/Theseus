using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Shows an element when a flag is set, and collapses it rather than hiding it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
