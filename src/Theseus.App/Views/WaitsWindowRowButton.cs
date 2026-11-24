using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Theseus.App.Views;

/// <summary>Whether a row of the waits checklist shows its own button: only a wait with one that is not a criterion's ask.</summary>
public sealed class WaitsWindowRowButton : IMultiValueConverter
{
    /// <summary>The converter, for <c>x:Static</c>.</summary>
    public static WaitsWindowRowButton Visibility { get; } = new();

    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [true, false] ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
