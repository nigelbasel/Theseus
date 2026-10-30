namespace Theseus.Core.Domain;

/// <summary>
/// How the board's other views name their columns and rows (Option E). The stage and area views use
/// the configured names; these are worked out from each card instead, so they need no setting.
/// </summary>
public static class BoardView
{
    /// <summary>The column for a session that is waiting on the user.</summary>
    public const string NeedsYou = "Needs you";

    /// <summary>The column for a session busy on a turn.</summary>
    public const string Working = "Working";

    /// <summary>The column for a session that is running and quiet.</summary>
    public const string Idle = "Idle";

    /// <summary>The column for a workstream with no session.</summary>
    public const string NotRunning = "Not running";

    /// <summary>The column for finished work with no session.</summary>
    public const string Done = "Done";

    /// <summary>The row for cards in no chain.</summary>
    public const string NoChain = "Not in a chain";

    /// <summary>The single row when rows show nothing.</summary>
    public const string AllWork = "All work";

    /// <summary>The row for a card whose working directory is not set.</summary>
    public const string NoProject = "No folder";

    /// <summary>The session-state columns, in the order they are read: what needs you first.</summary>
    public static IReadOnlyList<string> StateColumns { get; } = [NeedsYou, Working, Idle, NotRunning, Done];

    /// <summary>The session-state column a card belongs in.</summary>
    /// <param name="lifecycle">What its session is doing.</param>
    /// <param name="isInLastStage">Whether it sits in the last stage, which is finished work.</param>
    public static string StateColumn(SessionLifecycle lifecycle, bool isInLastStage) => lifecycle switch
    {
        SessionLifecycle.NeedsAttention => NeedsYou,
        SessionLifecycle.Launching or SessionLifecycle.Running => Working,
        SessionLifecycle.Idle => Idle,
        _ => isInLastStage ? Done : NotRunning,
    };

    /// <summary>
    /// The project a working directory belongs to: its folder's own name, so C:\dev\git\Theseus is
    /// Theseus and a feature worktree is named after the repository it checks out.
    /// </summary>
    public static string Project(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return NoProject;
        }

        var trimmed = workingDirectory.Trim().TrimEnd('\\', '/');
        var name = trimmed[(trimmed.LastIndexOfAny(['\\', '/']) + 1)..];

        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }
}
