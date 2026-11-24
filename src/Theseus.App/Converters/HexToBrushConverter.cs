using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Theseus.App.Services;

namespace Theseus.App.Converters;

/// <summary>
/// Turns a workstream's stored hex colour into a brush for its card stripe or border. Under a contrast theme a
/// coloured stripe or border is drawn in the window-text colour instead, so it still shows against the user's own
/// background (W47, G162). A wash uses <see cref="HexToWashConverter"/>, and a colour swatch
/// <see cref="HexToSwatchConverter"/>.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private readonly HexToSwatchConverter _colour = new();

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var brush = _colour.Convert(value, targetType, parameter, culture);

        return brush != Brushes.Transparent && ThemeApplier.IsHighContrast() ? SystemColors.WindowTextBrush : brush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
