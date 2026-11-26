namespace Theseus.App.ViewModels;

/// <summary>
/// Which project the board's Project view opens (board-small-changes item 3, Nigel, 2026-11-26: "switch to the project
/// board without having to click on an individual project"): the one last opened, or the first that reads. The view's
/// tabs switch from there.
/// </summary>
public static class ProjectViewTarget
{
    /// <summary>The project to open, or null when none reads.</summary>
    /// <param name="readable">The projects that read, in tab order.</param>
    /// <param name="lastOpened">The project last opened this run, or null.</param>
    public static string? Choose(IReadOnlyList<string> readable, string? lastOpened)
    {
        ArgumentNullException.ThrowIfNull(readable);

        return readable.FirstOrDefault(name => string.Equals(name, lastOpened, StringComparison.OrdinalIgnoreCase))
               ?? readable.FirstOrDefault();
    }
}
