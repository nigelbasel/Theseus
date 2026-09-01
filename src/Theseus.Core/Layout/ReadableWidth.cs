namespace Theseus.Core.Layout;

/// <summary>
/// How many columns of text a window of a given width will hold, and whether that is enough.
///
/// This is the constraint that decides how many windows are worth arranging at once. Claude Code
/// wraps poorly below eighty columns, so an arrangement that produces sixty is not a smaller version
/// of a good one - it is unusable, and better refused than applied.
///
/// The arithmetic is deliberately crude. An exact answer would need the terminal's font metrics at
/// its current zoom, which are not available from outside it; what is needed here is only enough
/// precision to tell four windows from nine.
/// </summary>
public static class ReadableWidth
{
    /// <summary>
    /// The advance width of one character, in physical pixels, for the default profile: Cascadia
    /// Mono at 12 point, which is 0.6 em, on a display at 150%. Configurable because it is neither.
    /// </summary>
    public const double DefaultAdvance = 14.4;

    /// <summary>What the window spends on borders, padding and a scrollbar rather than on text.</summary>
    public const int DefaultChrome = 60;

    /// <summary>The width below which Claude Code's output starts folding badly.</summary>
    public const int Wanted = 80;

    /// <summary>How many columns of text fit in a window of this width.</summary>
    public static int Columns(int pixels, double advance = DefaultAdvance, int chrome = DefaultChrome) =>
        advance <= 0 ? 0 : Math.Max(0, (int)((pixels - chrome) / advance));

    /// <summary>What to make of that many columns.</summary>
    public static WidthVerdict Judge(int columns) => columns switch
    {
        >= 100 => WidthVerdict.Comfortable,
        >= Wanted => WidthVerdict.Usable,
        >= 60 => WidthVerdict.Cramped,
        _ => WidthVerdict.Unusable,
    };

    /// <summary>The verdict on a window of this width.</summary>
    public static WidthVerdict Of(int pixels, double advance = DefaultAdvance, int chrome = DefaultChrome) =>
        Judge(Columns(pixels, advance, chrome));

    /// <summary>What to tell the user about an arrangement whose narrowest window is this wide.</summary>
    public static string Describe(int pixels, double advance = DefaultAdvance, int chrome = DefaultChrome)
    {
        var columns = Columns(pixels, advance, chrome);

        return Judge(columns) switch
        {
            WidthVerdict.Comfortable => $"about {columns} columns each — comfortable",
            WidthVerdict.Usable => $"about {columns} columns each — usable",
            WidthVerdict.Cramped => $"about {columns} columns each — cramped, output will fold",
            _ => $"about {columns} columns each — too narrow to read",
        };
    }
}
