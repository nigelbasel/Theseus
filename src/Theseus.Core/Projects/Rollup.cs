using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// The rolled-up mark of an objective or a phase in the objectives lens, shown even when folded, with who is working on it
/// and, in its hover, what it means, who acts and what to do.
/// </summary>
/// <param name="State">Met, partly met, in progress, waiting for you or not started.</param>
/// <param name="Text">The mark and words, such as "◐ Partly met (2 of 5)".</param>
/// <param name="Workers">The sessions working on it, or asking the owner something; empty when met.</param>
/// <param name="ToolTip">What it means, who acts, and what to do.</param>
public sealed record Rollup(RollupState State, string Text, IReadOnlyList<string> Workers, string ToolTip)
{
    /// <summary>Who is working on it, at most two names: "A, B and 3 more"; empty when nobody is.</summary>
    public string Who => Join(Workers, ", ");

    /// <summary>The roll-up of one objective from its criteria and the pieces serving it; waiting for you wins.</summary>
    public static Rollup Of(ProjectObjective objective, IReadOnlyList<PieceOfWork> serving)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(serving);

        var working = Names(serving.Where(piece => piece.IsCurrentPiece && piece.Card is { IsWorking: true }));
        var asking = Names(serving.Where(piece => piece.IsCurrentPiece && piece.Card is { Lifecycle: SessionLifecycle.NeedsAttention }));
        var actors = Actors(working);

        if (objective.IsDone)
        {
            return new Rollup(RollupState.Met, "✓ Met", [], "Met: every criterion is ticked. Nobody acts. Nothing for you to do.");
        }

        if (objective.Criteria.Any(criterion => criterion.WaitsOnOwner) || serving.Any(piece => piece.Status.WaitsForYou))
        {
            return new Rollup(RollupState.WaitingForYou, "◆ Waiting for you", [.. asking.Union(working)],
                "Waiting for you: a criterion waits on you, or a piece of work serving it does. You act. See Waits for you at the top.");
        }

        if (working.Count > 0)
        {
            return new Rollup(RollupState.InProgress, "● In progress", working,
                $"In progress: a session is working on it. {actors}. Nothing for you to do.");
        }

        if (objective.TickedCount > 0)
        {
            return new Rollup(RollupState.PartlyMet, $"◐ Partly met ({objective.TickedCount} of {objective.Criteria.Count})", [],
                $"Partly met: some of its criteria are ticked. {actors}. Start a piece of work for the rest when you choose.");
        }

        return serving.Any(piece => piece.Stages.Any(stage => stage.State != StageNodeState.ToDo || stage.Reason is not null))
            ? new Rollup(RollupState.InProgress, "● In progress", [],
                $"In progress: work is recorded, no criterion ticked yet. {actors}. Start its card when you choose.")
            : new Rollup(RollupState.NotStarted, "– Not started", [],
                "Not started: no work recorded for it yet. Nobody acts. Start a piece of work for it when you choose.");
    }

    /// <summary>A phase's roll-up from its objectives', the same way: waiting for you wins, then in progress.</summary>
    public static Rollup Of(IReadOnlyList<Rollup> objectives)
    {
        ArgumentNullException.ThrowIfNull(objectives);

        IReadOnlyList<string> workers = [.. objectives.SelectMany(rollup => rollup.Workers).Distinct(StringComparer.Ordinal)];
        var met = objectives.Count(rollup => rollup.State == RollupState.Met);
        var actors = Actors(workers);

        if (objectives.Count > 0 && met == objectives.Count)
        {
            return new Rollup(RollupState.Met, "✓ Met", [], "Met: every objective is met. Nobody acts. Nothing for you to do.");
        }

        if (objectives.Any(rollup => rollup.State == RollupState.WaitingForYou))
        {
            return new Rollup(RollupState.WaitingForYou, "◆ Waiting for you", workers,
                "Waiting for you: one of its objectives waits on you. You act. See Waits for you at the top.");
        }

        if (objectives.Any(rollup => rollup.State == RollupState.InProgress))
        {
            return new Rollup(RollupState.InProgress, "● In progress", workers,
                $"In progress: work goes on in one of its objectives. {actors}. Nothing for you to do.");
        }

        return met > 0 || objectives.Any(rollup => rollup.State == RollupState.PartlyMet)
            ? new Rollup(RollupState.PartlyMet, $"◐ Partly met ({met} of {objectives.Count})", [],
                $"Partly met: some of its objectives are met. {actors}. Start work on the rest when you choose.")
            : new Rollup(RollupState.NotStarted, "– Not started", [],
                "Not started: none of its objectives has work recorded. Nobody acts. Start work on one when you choose.");
    }

    private static IReadOnlyList<string> Names(IEnumerable<PieceOfWork> pieces) =>
        [.. pieces.Select(piece => piece.Workstream).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string Actors(IReadOnlyList<string> names) =>
        names.Count == 0 ? "Nobody is working on it" : $"{Join(names, " and ")} act{(names.Count == 1 ? "s" : string.Empty)}";

    private static string Join(IReadOnlyList<string> names, string pair) => names.Count <= 2
        ? string.Join(pair, names)
        : $"{names[0]}, {names[1]} and {names.Count - 2} more";
}
