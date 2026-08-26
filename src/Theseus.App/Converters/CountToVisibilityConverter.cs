using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Shows a control only when a count is more than zero.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
