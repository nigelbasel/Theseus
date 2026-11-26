namespace Theseus.App.ViewModels;

/// <summary>
/// The one Board / Projects toggle on the toolbar (project-view-as-tabs AC 5; Nigel, 2026-11-26: "we should have a single
/// toolbar button that changes depending on context"). Its words and glyph follow where you are. From the board it opens
/// the project view where it was last left; when 1.5b's All projects tab exists, it opens that tab on first use (agreed
/// with 1.5b, 2026-11-26).
/// </summary>
public static class ViewToggle
{
    /// <summary>The toggle's words: where it takes you.</summary>
    public static string Text(bool inView) => inView ? "Board" : "Projects";

    /// <summary>The toggle's glyph: Home for the board, the library mark for the projects.</summary>
    public static string Glyph(bool inView) => inView ? "" : "";

    /// <summary>What the toggle does, with its key.</summary>
    public static string ToolTip(bool inView) => inView
        ? "Back to the board (Ctrl+B)"
        : "Open the project view where you left it (Ctrl+B)";

    /// <summary>Where the toggle opens the project view, or null when there is nothing to open.</summary>
    /// <param name="readable">The projects that read, in tab order.</param>
    /// <param name="lastProject">The project last open this run, or null.</param>
    /// <param name="lastWasAllProjects">Whether the view was last left on All projects.</param>
    /// <param name="hasAllProjects">Whether the All projects tab exists.</param>
    public static ViewTarget? Target(IReadOnlyList<string> readable, string? lastProject, bool lastWasAllProjects, bool hasAllProjects)
    {
        ArgumentNullException.ThrowIfNull(readable);

        if (hasAllProjects && (lastWasAllProjects || lastProject is null))
        {
            return new ViewTarget(true, null);
        }

        if (ProjectViewTarget.Choose(readable, lastProject) is { } project)
        {
            return new ViewTarget(false, project);
        }

        return hasAllProjects ? new ViewTarget(true, null) : null;
    }

    /// <summary>
    /// Why a project command (Start, Stop, Close, Archive, New workstream in it) cannot act, or null when a project's tab is
    /// open.
    /// </summary>
    public static string? WhyNoProject(bool inView, bool allProjects) =>
        !inView ? "Open a project to act on it"
        : allProjects ? "Open a project's tab to act on it"
        : null;
}
