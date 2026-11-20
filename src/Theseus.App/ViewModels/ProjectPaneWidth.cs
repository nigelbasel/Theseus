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

    /// <summary>The widest it may be in a view this wide; never under <see cref="Minimum"/>.</summary>
    public static double Maximum(double viewWidth) =>
        Math.Max(Minimum, Math.Min(viewWidth / 2, viewWidth - MinimumFlow - Splitter));

    /// <summary>A width kept within its bounds in a view this wide; a width that is not a number is the default.</summary>
    public static double Clamp(double width, double viewWidth) =>
        double.IsFinite(width) ? Math.Clamp(width, Minimum, Maximum(viewWidth)) : Default;
}
