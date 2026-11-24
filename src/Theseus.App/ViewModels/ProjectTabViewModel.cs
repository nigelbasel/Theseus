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
    /// <summary>What it does.</summary>
    public string ToolTip => IsOpen ? $"{Title} is open" : Key is { } key ? $"Open {Title} ({key})" : $"Open {Title}";

    /// <summary>The key that opens it (Ctrl+1 to Ctrl+9), or null when no number key reaches it.</summary>
    public string? Key { get; init; }

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
    public static IReadOnlyList<ProjectTabViewModel> For(IEnumerable<ProjectFileReading> readings, string? open,
        Func<string, ICommand?> commandFor, Func<string, string?>? colourOf = null)
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
                Key = ProjectTabKeys.KeyFor(index, readable.Count),
            }),
        ];
    }
}
