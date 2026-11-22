namespace Theseus.Core.Projects;

/// <summary>
/// The rolled-up mark of an objective or a phase in the objectives lens, visible even when folded, with who is working on
/// it and the three answers in its hover (Nigel, 2026-11-22: the objectives lens is the main view).
/// </summary>
/// <param name="State">Met, partly met, in progress, waiting for you or not started.</param>
/// <param name="Text">The mark and words, such as "◐ Partly met (2 of 5)".</param>
/// <param name="Who">Who is working on it, such as "Theseus 1.1g", or empty.</param>
/// <param name="ToolTip">What it means, who acts next, and what to do.</param>
public sealed record Rollup(RollupState State, string Text, string Who, string ToolTip)
{
    /// <summary>The roll-up of one objective from its criteria and the pieces serving it; waiting for you wins.</summary>
    public static Rollup Of(ProjectObjective objective, IReadOnlyList<PieceOfWork> serving)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(serving);

        var who = string.Join(", ", serving.Where(piece => piece.IsCurrentPiece && piece.Card is { HasSession: true })
            .Select(piece => piece.Workstream).Distinct(StringComparer.OrdinalIgnoreCase));
        var waiting = objective.Criteria.Any(criterion => criterion.WaitsOnOwner) || serving.Any(piece => piece.Status.WaitsForYou);

        if (objective.IsDone)
        {
            return new Rollup(RollupState.Met, "✓ Met", who, "Met: every criterion is ticked. Nobody acts; nothing for you.");
        }

        if (waiting)
        {
            return new Rollup(RollupState.WaitingForYou, "◆ Waiting for you", who,
                "Waiting for you: a criterion waits on you, or a piece serving it does. You act: see Waits for you.");
        }

        if (serving.Any(piece => piece.Status.State == StageNodeState.Busy))
        {
            return new Rollup(RollupState.InProgress, "● In progress", who,
                $"In progress: a session is working on it ({who}). That session acts; nothing for you.");
        }

        if (objective.TickedCount > 0)
        {
            return new Rollup(RollupState.PartlyMet, $"◐ Partly met ({objective.TickedCount} of {objective.Criteria.Count})", who,
                "Partly met: some criteria are ticked. The sessions serving it act next.");
        }

        return serving.Count > 0 && serving.Any(piece => piece.Status.State != StageNodeState.ToDo)
            ? new Rollup(RollupState.InProgress, "● In progress", who, "In progress: work is recorded, nothing ticked yet. Its sessions act next.")
            : new Rollup(RollupState.NotStarted, "– Not started", who, "Not started: no work recorded for it yet. Nobody acts until a piece of work serves it.");
    }

    /// <summary>A phase's roll-up from its objectives', the same way: waiting for you wins, then in progress.</summary>
    public static Rollup Of(IReadOnlyList<Rollup> objectives)
    {
        ArgumentNullException.ThrowIfNull(objectives);

        var who = string.Join(", ", objectives.Select(rollup => rollup.Who).Where(each => each.Length > 0).Distinct(StringComparer.Ordinal));
        var met = objectives.Count(rollup => rollup.State == RollupState.Met);

        if (objectives.Count > 0 && met == objectives.Count)
        {
            return new Rollup(RollupState.Met, "✓ Met", who, "Met: every objective is met. Nobody acts; nothing for you.");
        }

        if (objectives.Any(rollup => rollup.State == RollupState.WaitingForYou))
        {
            return new Rollup(RollupState.WaitingForYou, "◆ Waiting for you", who, "Waiting for you: one of its objectives waits on you.");
        }

        if (objectives.Any(rollup => rollup.State == RollupState.InProgress))
        {
            return new Rollup(RollupState.InProgress, "● In progress", who, "In progress: work goes on in one of its objectives.");
        }

        return met > 0 || objectives.Any(rollup => rollup.State == RollupState.PartlyMet)
            ? new Rollup(RollupState.PartlyMet, $"◐ Partly met ({met} of {objectives.Count})", who, "Partly met: some of its objectives are met.")
            : new Rollup(RollupState.NotStarted, "– Not started", who, "Not started: none of its objectives has work recorded.");
    }
}
