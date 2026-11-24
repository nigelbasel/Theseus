using System.Globalization;
using System.Windows.Data;
using Theseus.App.Text;

namespace Theseus.App.Converters;

/// <summary>
/// <see cref="AccessKeyText.Escape"/> for a binding, as <c>Header="{Binding Display, Converter={StaticResource
/// AccessKeyEscape}}"</c>: a name bound into a header keeps its underscores.
/// </summary>
public sealed class AccessKeyEscapeConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => AccessKeyText.Escape(value?.ToString());

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
