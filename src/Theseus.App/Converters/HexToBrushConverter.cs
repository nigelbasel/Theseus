using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Converters;

/// <summary>Turns a workstream's stored hex colour into a brush for its card stripe.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch (FormatException)
            {
                TheseusLog.Warning("A workstream colour is not a colour: {Hex}", hex);
            }
        }

        return Brushes.Transparent;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
