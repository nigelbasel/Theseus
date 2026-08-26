using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Shows an empty-state message only when a count is zero.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
