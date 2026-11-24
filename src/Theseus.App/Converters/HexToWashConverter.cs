using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Theseus.App.Services;

namespace Theseus.App.Converters;

/// <summary>
/// Turns a workstream's stored hex colour into the faint wash behind a card or a project tag. Under a contrast theme
/// the wash is dropped, since there the user's own window colour is the background (W47, G162).
/// </summary>
public sealed class HexToWashConverter : IValueConverter
{
    private readonly HexToSwatchConverter _colour = new();

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ThemeApplier.IsHighContrast() ? Brushes.Transparent : _colour.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
