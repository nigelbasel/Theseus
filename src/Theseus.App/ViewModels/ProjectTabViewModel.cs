using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One project tab in the view's title bar (1.1d AC 17): the stacked-card mark and the project's title. The open one is
/// marked; a click opens that project.
/// </summary>
/// <param name="Name">The project's name.</param>
/// <param name="Title">Its title.</param>
/// <param name="IsOpen">Whether it is the project open now.</param>
/// <param name="OpenCommand">Opens it.</param>
public sealed record ProjectTabViewModel(string Name, string Title, bool IsOpen, ICommand? OpenCommand)
{
    /// <summary>What it does, and how many things wait for the owner in its project.</summary>
    public string ToolTip => HasWaiting ? $"{Action}{Environment.NewLine}{WaitingText}" : Action;

    private string Action => IsOpen ? $"{Title} is open" : Key is { } key ? $"Open {Title} ({key})" : $"Open {Title}";

    /// <summary>
    /// Whether this is the strip's first tab, All projects, which shows 1.5b's kanban rather than a project (agreed with
    /// 1.5b, 2026-11-26). It has no project, no number key of the projects', and no project menu.
    /// </summary>
    public bool IsAllProjects { get; init; }

    /// <summary>Start the project, on the tab's menu (project-view-as-tabs AC 4); null offers none.</summary>
    public ICommand? StartCommand { get; init; }

    /// <summary>Stop the project…, on the tab's menu; null offers none.</summary>
    public ICommand? StopCommand { get; init; }

    /// <summary>The All projects tab: first in the strip, opened by Ctrl+0.</summary>
    /// <param name="isOpen">Whether All projects is what the view shows.</param>
    /// <param name="open">Opens it.</param>
    public static ProjectTabViewModel AllProjects(bool isOpen, ICommand? open) =>
        new(string.Empty, "All projects", isOpen, open) { IsAllProjects = true, Key = "Ctrl+0" };

    /// <summary>
    /// How many lines its project's Waits for you has: the count its amber dot stands for (board-small-changes item 2,
    /// Nigel, 2026-11-26).
    /// </summary>
    public int WaitingCount { get; init; }

    /// <summary>Whether its dot shows.</summary>
    public bool HasWaiting => WaitingCount > 0;

    /// <summary>"3 waiting for you": the dot in words, for its tooltip and name, since a state is never colour alone.</summary>
    public string WaitingText => $"{WaitingCount} waiting for you";

    /// <summary>Its name to assistive technology: its title, and its waits when it has any.</summary>
    public string AutomationName => HasWaiting ? $"{Title}, {WaitingText}" : Title;

    /// <summary>The key that opens it (Ctrl+1 to Ctrl+9), or null when no number key reaches it.</summary>
    public string? Key { get; init; }

    /// <summary>Close project, on the tab's menu (archive-a-project, 2026-11-26); null offers none.</summary>
    public ICommand? CloseCommand { get; init; }

    /// <summary>Archive project, on the tab's menu; null offers none.</summary>
    public ICommand? ArchiveCommand { get; init; }

    /// <summary>The project's own colour, for the tab's mark (1.1d AC 40), or null, when the mark takes the accent colour.</summary>
    public string? ColourHex { get; init; }

    /// <summary>
    /// One tab per project that reads, in the order given (the order of View › Open project); a project that does not
    /// read has none.
    /// </summary>
    /// <param name="readings">The projects as read, in order.</param>
    /// <param name="open">The open project's name.</param>
    /// <param name="commandFor">The command that opens a project, by name.</param>
    /// <param name="colourOf">Each project's own colour, by name; none gives every mark the accent colour.</param>
    /// <param name="waitsOf">How many lines each project's Waits for you has, by name; none gives no tab a dot.</param>
    /// <param name="closeFor">Close project for a tab's menu, by name; none offers it.</param>
    /// <param name="archiveFor">Archive project for a tab's menu, by name; none offers it.</param>
    /// <param name="startFor">Start the project for a tab's menu, by name; none offers it.</param>
    /// <param name="stopFor">Stop the project… for a tab's menu, by name; none offers it.</param>
    public static IReadOnlyList<ProjectTabViewModel> For(IEnumerable<ProjectFileReading> readings, string? open,
        Func<string, ICommand?> commandFor, Func<string, string?>? colourOf = null, Func<string, int>? waitsOf = null,
        Func<string, ICommand?>? closeFor = null, Func<string, ICommand?>? archiveFor = null,
        Func<string, ICommand?>? startFor = null, Func<string, ICommand?>? stopFor = null)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(commandFor);

        List<ProjectFileReading> readable = [.. readings.Where(reading => reading.IsReadable)];

        return
        [
            .. readable.Select((reading, index) => new ProjectTabViewModel(reading.Name,
                reading.File!.Title, string.Equals(reading.Name, open, StringComparison.OrdinalIgnoreCase), commandFor(reading.Name))
            {
                ColourHex = colourOf?.Invoke(reading.Name),
                WaitingCount = waitsOf?.Invoke(reading.Name) ?? 0,
                CloseCommand = closeFor?.Invoke(reading.Name),
                ArchiveCommand = archiveFor?.Invoke(reading.Name),
                StartCommand = startFor?.Invoke(reading.Name),
                StopCommand = stopFor?.Invoke(reading.Name),
                Key = ProjectTabKeys.KeyFor(index, readable.Count),
            }),
        ];
    }
}
