namespace Theseus.Core.Projects;

/// <summary>
/// One round of a project: a row of its <c>## Rounds</c> table, begun when the owner changed the requirements and work
/// went back to research (1.1f B6; Nigel chose the Rounds table with a Round line, 2026-11-21).
/// </summary>
/// <param name="Number">Its number, from 1.</param>
/// <param name="Name">Its name, such as "GUI parity".</param>
/// <param name="Began">When it began, or null when the file does not say.</param>
/// <param name="Why">Why the requirements changed, in the owner's words; empty for the first round.</param>
/// <param name="By">Who changed them.</param>
/// <param name="Line">Its row's line in the project file, from 1; 0 for the round a file with no table reads as.</param>
public sealed record ProjectRound(int Number, string Name, DateOnly? Began, string Why, string By, int Line)
{
    /// <summary>
    /// Whether it is the one round a file with no Rounds table reads as (1.1f B8, D4): "Round 1", with no change banner.
    /// </summary>
    public bool IsImplicit { get; init; }

    /// <summary>The round a file with no Rounds table reads as, begun on that date.</summary>
    public static ProjectRound Implicit(DateOnly? began) => new(1, "Round 1", began, string.Empty, string.Empty, 0) { IsImplicit = true };
}
