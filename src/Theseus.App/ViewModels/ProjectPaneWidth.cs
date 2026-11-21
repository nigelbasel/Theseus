namespace Theseus.App.ViewModels;

/// <summary>
/// The width of the project view's Properties pane (1.1d AC 35): 320 px to start, kept between 260 px and half the
/// view, and never so wide the flow has less than 400 px.
/// </summary>
public static class ProjectPaneWidth
{
    /// <summary>The settings key it is saved under.</summary>
    public const string SettingsKey = "ProjectView:Pane";

    /// <summary>Its width at first, and after a double-click on the splitter.</summary>
    public const double Default = 320;

    /// <summary>The narrowest it goes.</summary>
    public const double Minimum = 260;

    /// <summary>The narrowest the flow beside it goes.</summary>
    public const double MinimumFlow = 400;

    /// <summary>The splitter's width.</summary>
    public const double Splitter = 6;

    /// <summary>
    /// The narrowest view the pane docks in: the flow's minimum, the splitter and the pane's own (1.1d AC 52). In a
    /// narrower one Properties opens as an overlay instead.
    /// </summary>
    public const double DockingWidth = MinimumFlow + Splitter + Minimum;

    /// <summary>Whether the pane docks beside the flow in a view this wide.</summary>
    public static bool Docks(double viewWidth) => viewWidth >= DockingWidth;

    /// <summary>The widest it may be in a view this wide; never under <see cref="Minimum"/>.</summary>
    public static double Maximum(double viewWidth) =>
        Math.Max(Minimum, Math.Min(viewWidth / 2, viewWidth - MinimumFlow - Splitter));

    /// <summary>A width kept within its bounds in a view this wide; a width that is not a number is the default.</summary>
    public static double Clamp(double width, double viewWidth) =>
        double.IsFinite(width) ? Math.Clamp(width, Minimum, Maximum(viewWidth)) : Default;
}
