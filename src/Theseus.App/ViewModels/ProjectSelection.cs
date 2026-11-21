namespace Theseus.App.ViewModels;

/// <summary>
/// What the selection commands do when a project card is selected (1.1d AC 41): with only projects selected, a command
/// for one workstream is disabled and says why; in a mixed selection every command skips the projects and the status
/// bar says so.
/// </summary>
public static class ProjectSelection
{
    /// <summary>What a command for one workstream says while a project is selected.</summary>
    public const string NotForAProject = "Not for a project: open it and select its cards";

    /// <summary>The one selected card a command for one workstream acts on, projects aside, or null.</summary>
    public static WorkstreamCardViewModel? OneCard(BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);

        return board.ChosenCards is [var only] ? only : null;
    }

    /// <summary>A command for one workstream's tooltip: its own, or, with only projects selected, why it is off.</summary>
    public static string ToolTip(BoardViewModel board, string forACard)
    {
        ArgumentNullException.ThrowIfNull(board);

        return board.ChosenCards.Count == 0 && board.ChosenProjects.Count > 0 ? NotForAProject : forACard;
    }

    /// <summary>What a command on the selection adds to its notice for the projects it passed over: " 1 project skipped."</summary>
    public static string Skipped(BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);

        return board.ChosenProjects.Count switch
        {
            0 => string.Empty,
            1 => " 1 project skipped.",
            var count => $" {count} projects skipped.",
        };
    }
}
