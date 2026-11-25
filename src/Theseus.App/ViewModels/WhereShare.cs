namespace Theseus.App.ViewModels;

/// <summary>
/// The most of the project view's height the summary at its top may take before it scrolls, so the work below always has
/// room (project-view-scale, 2026-11-24: 68 lines of Waits for you left the page with nothing to scroll or press).
/// </summary>
public static class WhereShare
{
    /// <summary>The share of the view's height.</summary>
    public const double Share = 0.4;

    /// <summary>The least it is given, however short the view, so its title and first wait still show.</summary>
    public const double Minimum = 160;

    /// <summary>Its most height in a view this tall.</summary>
    public static double MaxHeight(double viewHeight) =>
        double.IsFinite(viewHeight) && viewHeight > 0 ? Math.Max(Minimum, viewHeight * Share) : double.PositiveInfinity;
}
