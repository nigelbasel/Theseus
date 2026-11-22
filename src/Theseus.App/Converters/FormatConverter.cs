using System.Globalization;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>
/// Writes a value into the format given as the parameter, for a tooltip or other object-typed property, where a binding's
/// own StringFormat is ignored: "Take #{0} off" with "bym" is "Take #bym off".
/// </summary>
public sealed class FormatConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Format(culture, parameter as string ?? "{0}", value);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
