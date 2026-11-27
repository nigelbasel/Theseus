namespace Theseus.App.ViewModels;

/// <summary>
/// One project as the kanban gets it from the shell's refresh: its own project view, built the way an open project's is,
/// so every card is that view's box and never a second model (projects-kanban spec AC 16).
/// </summary>
/// <param name="Name">The project's name.</param>
/// <param name="Title">Its title.</param>
/// <param name="View">Its project view, or null when its file does not read.</param>
public sealed record KanbanProjectSource(string Name, string Title, ProjectViewModel? View)
{
    /// <summary>Its own colour, or null for the accent colour.</summary>
    public string? ColourHex { get; init; }

    /// <summary>How many lines its Waits for you has: the count its tab and chip show.</summary>
    public int WaitingCount { get; init; }

    /// <summary>"closed" or "archived" when it is put away and shown only on request (AC 4); null otherwise.</summary>
    public string? PutAway { get; init; }

    /// <summary>Why its file does not read, said on its row header (AC 7); null when it reads.</summary>
    public string? Problem { get; init; }
}
