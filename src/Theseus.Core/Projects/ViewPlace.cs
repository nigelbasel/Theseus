namespace Theseus.Core.Projects;

/// <summary>Where the owner was: the board, or a project with its lens and what was selected (1.1f I46).</summary>
/// <param name="Project">The project's name, or null for the board.</param>
/// <param name="Lens">The lens: rounds and work, or objectives only; null on the board.</param>
/// <param name="Selection">What was selected, by name (a piece, a stage as "piece/stage", an objective as "objective:n"), or null.</param>
public sealed record ViewPlace(string? Project, string? Lens, string? Selection)
{
    /// <summary>The board.</summary>
    public static ViewPlace Board { get; } = new(null, null, null);

    /// <summary>Whether it is the board.</summary>
    public bool IsBoard => Project is null;
}
