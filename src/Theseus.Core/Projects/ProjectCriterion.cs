namespace Theseus.Core.Projects;

/// <summary>One acceptance criterion under an objective: a <c>- [ ]</c> or <c>- [x]</c> line.</summary>
/// <param name="Text">The criterion, its continuation lines joined with spaces.</param>
/// <param name="IsTicked">Whether it is ticked as met.</param>
/// <param name="Line">Its line in the project file, from 1.</param>
public sealed record ProjectCriterion(string Text, bool IsTicked, int Line)
{
    /// <summary>The prefix of a criterion's evidence note, the pointer <c>tick_criterion</c> keeps (brief, PR 2).</summary>
    public const string EvidenceLabel = "Evidence:";

    /// <summary>Who it waits on, from its waiting note (1.1f G37b); null when it has none, which means it waits on work.</summary>
    public CriterionWaiting? Waiting { get; init; }

    /// <summary>Its evidence note, a one-line pointer to what shows it is met; null when it has none.</summary>
    public string? Evidence { get; init; }

    /// <summary>Whether it waits on the owner: unticked, with an owner note (G37b).</summary>
    public bool WaitsOnOwner => !IsTicked && Waiting is { IsOwner: true };
}
