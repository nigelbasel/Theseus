using System.Globalization;
using System.Windows.Data;
using Theseus.Core.Domain;

namespace Theseus.App.Converters;

/// <summary>
/// Renders a lifecycle state as a glyph. Shape carries the meaning as well as colour, so the board
/// still reads correctly in greyscale and to a colour-blind eye.
/// </summary>
public sealed class LifecycleGlyphConverter : IValueConverter
{
    /// <summary>The glyph for a lifecycle state.</summary>
    public static string Glyph(SessionLifecycle lifecycle) => lifecycle switch
    {
        SessionLifecycle.Running => "●",
        SessionLifecycle.Idle or SessionLifecycle.Launching => "○",
        SessionLifecycle.NeedsAttention => "◆",
        SessionLifecycle.Ended => "·",
        SessionLifecycle.Ghost => "▲",
        _ => string.Empty,
    };

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SessionLifecycle lifecycle ? Glyph(lifecycle) : string.Empty;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
