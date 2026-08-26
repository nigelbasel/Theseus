using System.Globalization;
using System.Windows.Data;

namespace Theseus.App.Converters;

/// <summary>Renders an instant as the card's third line: how long ago, not when.</summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    /// <summary>Formats an instant relative to now.</summary>
    public static string Format(DateTimeOffset? instant, DateTimeOffset now)
    {
        if (instant is null || instant == DateTimeOffset.MinValue)
        {
            return string.Empty;
        }

        var elapsed = now - instant.Value;

        return elapsed switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d",
            _ => instant.Value.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture),
        };
    }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Format(value as DateTimeOffset?, DateTimeOffset.UtcNow);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
