using Theseus.Core.Domain;
using Theseus.Core.Questions;

namespace Theseus.Core.Projects;

/// <summary>
/// Where a piece of work is, with the three answers every state gives (1.1f F31): what it means, who acts next and when,
/// and the action. The box shows the state line and the who-acts line; hover and Properties give the meaning too.
/// </summary>
/// <param name="State">The state its box and the project's counts show.</param>
/// <param name="Text">The state line, such as "Waiting for you: accept the spec."</param>
public sealed record PieceStatus(StageNodeState State, string Text)
{
    /// <summary>Who acts next, and when: the box's who-acts line, in the same words as its "Waits for you" item (G37).</summary>
    public string WhoActs { get; init; } = "No action needed.";

    /// <summary>The box's single primary action when folded.</summary>
    public PieceAction Action { get; init; } = PieceAction.None;

    /// <summary>Whether it waits on the owner, and so is a "Waits for you" item.</summary>
    public bool WaitsForYou { get; init; }

    /// <summary>The question its session asks the owner, which its "Waits for you" item quotes (1.6); null when none.</summary>
    public OwnerQuestion? Question { get; init; }

    /// <summary>The card the owner acts on, when it is not the piece's own: a verifier asking him something (item 6).</summary>
    public string? ActsOnCard { get; init; }

    /// <summary>The state's mark.</summary>
    public string Mark => StageNodeStates.Mark(State);

    /// <summary>The state's word.</summary>
    public string Word => StageNodeStates.Word(State);

    /// <summary>What the state means.</summary>
    public string Meaning => StageNodeStates.Meaning(State);

    /// <summary>
    /// Works out a piece's status, the first that applies of: a session asking, stuck, busy on a stage it named, a gate
    /// waiting, built and not checked, busy with nothing said, kept elsewhere, answered, finished, not in the project,
    /// not started, and otherwise the next stage.
    /// </summary>
    public static PieceStatus For(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var stages = piece.Stages;
        var card = piece.Card;
        var isWorking = piece.IsCurrentPiece && card is { IsWorking: true };
        var isAsking = piece.IsCurrentPiece && card is { Lifecycle: SessionLifecycle.NeedsAttention };

        if (isAsking)
        {
            if (card!.Prompt is { } prompt)
            {
                return new PieceStatus(StageNodeState.WaitingOnYou, $"Waiting for you: {QuestionWords.Named(piece.Workstream, prompt)}")
                {
                    WhoActs = "You; it does nothing until you answer in its tab.",
                    Action = PieceAction.Answer,
                    WaitsForYou = true,
                };
            }

            return card.Question is { } question
                ? new PieceStatus(StageNodeState.WaitingOnYou, $"Waiting for you: {QuestionWords.Named(piece.Workstream, question)}")
                {
                    WhoActs = QuestionWords.WhoActs(question),
                    Action = PieceAction.Answer,
                    WaitsForYou = true,
                    Question = question,
                }
                : new PieceStatus(StageNodeState.WaitingOnYou, $"Waiting for you: {piece.Workstream} has asked you something in its terminal.")
                {
                    WhoActs = "You; it does nothing until you answer.",
                    Action = PieceAction.Answer,
                    WaitsForYou = true,
                };
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.Stuck) is { } stuck)
        {
            var blocked = piece.IsCurrentPiece && card is { IsBlocked: true };

            return new PieceStatus(StageNodeState.Stuck,
                stuck.IsSessionState ? $"Stuck: {stuck.StatusReason}." : $"Stuck at {stuck.Name}: {stuck.StatusReason}.")
            {
                WhoActs = blocked ? "You: it is marked blocked." : $"{piece.Workstream}; nothing for you unless it asks.",
                Action = stuck is VerifyStageNode && stuck.RecordFile is not null ? PieceAction.OpenEvidence
                    : piece.IsCurrentPiece && card is { HasSession: true } ? PieceAction.Focus : PieceAction.None,
                WaitsForYou = blocked,
            };
        }

        // Evidence missing after later records: nothing blocks it, so not Stuck, and never done or "next" either (G15). A spec
        // waiting on the owner, or a build ahead of it, says so first.
        if (!stages.Any(stage => stage.State == StageNodeState.WaitingOnYou)
            && !(isWorking && piece.AwaitsSpecAcceptance && piece.Kind == PieceKind.Build)
            && stages.FirstOrDefault(stage => stage.Reason == PiecesOfWork.NoEvidenceYet) is { } evidence)
        {
            var hasSession = piece.IsCurrentPiece && card is { HasSession: true };
            var canStart = piece.IsCurrentPiece && card is { HasSession: false, CanStart: true };

            return new PieceStatus(isWorking ? StageNodeState.Busy : StageNodeState.ToDo,
                $"Evidence missing at {evidence.Name}: {evidence.Reason}; {(hasSession ? "its session can write it" : "nobody is working on it")}.")
            {
                WhoActs = hasSession ? $"{piece.Workstream}; nothing for you unless it asks."
                    : canStart ? $"{piece.Workstream}, once you start it." : "Nobody yet.",
                Action = hasSession ? PieceAction.Focus : canStart ? PieceAction.Start : PieceAction.None,
            };
        }

        if (isWorking && piece.AwaitsSpecAcceptance && piece.Kind == PieceKind.Build)
        {
            return new PieceStatus(StageNodeState.Busy, "Busy building, ahead of your acceptance of its spec (at its own risk).")
            {
                WhoActs = "You: accept its spec; until you do it builds at its own risk.",
                Action = PieceAction.Accept,
                WaitsForYou = true,
            };
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.Busy) is { } busy)
        {
            return new PieceStatus(StageNodeState.Busy, $"Busy: {busy.Reason ?? $"working on {busy.Name}"}.")
            {
                WhoActs = $"{piece.Workstream}; no action needed.",
                Action = PieceAction.Focus,
            };
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.WaitingOnYou) is { } waiting)
        {
            return new PieceStatus(StageNodeState.WaitingOnYou, $"Waiting for you: {waiting.StatusReason}.")
            {
                WhoActs = "You; nothing moves until you do.",
                Action = waiting is SpecStageNode { AwaitsAcceptance: true } ? PieceAction.Accept : PieceAction.None,
                WaitsForYou = true,
            };
        }

        // A verifier with a session means its check is under way: nothing waits on the owner unless it asks him (item 6).
        if (stages.Any(stage => stage.State == StageNodeState.HoldoutsNotRun)
            && piece.PendingVerifiers.FirstOrDefault(verifier => verifier.HasSession && (verifier.Lifecycle == SessionLifecycle.NeedsAttention || verifier.IsBlocked)) is { } asking)
        {
            return new PieceStatus(StageNodeState.HoldoutsNotRun, $"Built; its check waits for you: "
                + (asking.IsBlocked ? $"{asking.Title} is blocked." : $"{QuestionWords.Named(asking.Title, asking.Prompt, asking.Question)}."))
            {
                WhoActs = "You; the check does nothing until you answer.",
                Action = PieceAction.Answer,
                WaitsForYou = true,
                ActsOnCard = asking.Key,
            };
        }

        if (stages.Any(stage => stage.State == StageNodeState.HoldoutsNotRun)
            && piece.PendingVerifiers.FirstOrDefault(verifier => verifier.HasSession) is { } checking)
        {
            return new PieceStatus(StageNodeState.HoldoutsNotRun, "Built; its independent check is under way.")
            {
                WhoActs = $"{checking.Title} is checking it; no action needed.",
                Action = PieceAction.None,
            };
        }

        if (stages.Any(stage => stage.State == StageNodeState.HoldoutsNotRun))
        {
            // Theseus runs the check itself (git-22, 2026-11-23), so it waits for nobody; Start the checks… runs it now.
            return new PieceStatus(StageNodeState.HoldoutsNotRun, "Built, not yet checked by an independent test.")
            {
                WhoActs = isWorking
                    ? "Theseus checks it once its session stops."
                    : "Theseus checks it: it makes its verifier once a scenario names it.",
                Action = PieceAction.StartCheck,
            };
        }

        if (isWorking)
        {
            return new PieceStatus(StageNodeState.Busy, "Busy, but has not said on what.")
            {
                WhoActs = $"{piece.Workstream}; no action needed.",
                Action = PieceAction.Focus,
            };
        }

        if (stages.Count == 1 && stages[0].State == StageNodeState.RecordsElsewhere)
        {
            return new PieceStatus(StageNodeState.RecordsElsewhere,
                piece.IsReportConfirmed ? "Reported back; you confirmed it." : "Reported back; waiting for your confirmation on the board.")
            {
                WhoActs = "No action needed. Its records are kept outside the project.",
            };
        }

        var finished = stages.All(stage => StageNodeStates.IsFinished(stage.State));

        if (piece.IsAnswered && (finished || piece.IsFreeform))
        {
            return new PieceStatus(StageNodeState.Answered, "Reported back; you confirmed it.")
            {
                WhoActs = $"The {piece.ParentTitle ?? "parent"} session ticks objective {piece.ObjectiveNumber}'s criteria; nothing for you.",
            };
        }

        var noSpecHere = stages.Any(stage => stage.Reason == PiecesOfWork.NoSpecHere);

        if (finished && noSpecHere)
        {
            return new PieceStatus(StageNodeState.RecordsElsewhere,
                "Its stages are recorded, but its spec gate is not passed here: no spec.md in its records.")
            {
                WhoActs = "You, if its spec was accepted elsewhere; Theseus has no record of that acceptance.",
                Action = piece.RecordsFolder is null ? PieceAction.None : PieceAction.OpenRecords,
            };
        }

        if (finished)
        {
            return new PieceStatus(StageNodeState.Done, Finished(piece))
            {
                Action = piece.RecordsFolder is null ? PieceAction.None : PieceAction.OpenRecords,
            };
        }

        if (piece.IsBeforeKit && piece.IsObjectiveDone)
        {
            var missing = stages.Where(stage => stage.State == StageNodeState.ToDo).Select(stage => stage.Name.ToLowerInvariant()).ToList();

            return new PieceStatus(StageNodeState.Done,
                $"{(piece.Pipeline == PipelineKind.Bug ? "Fixed and checked" : "Done")} before method-v0.2; no {Join(missing)} {(missing.Count == 1 ? "was" : "were")} written.")
            {
                Action = piece.RecordsFolder is null ? PieceAction.None : PieceAction.OpenRecords,
            };
        }

        if (stages.All(stage => stage.State == StageNodeState.HandedOn || StageNodeStates.IsFinished(stage.State) || stage.Stage == PipelineStage.Compound)
            && stages.Any(stage => stage.State == StageNodeState.HandedOn))
        {
            return new PieceStatus(StageNodeState.HandedOn, $"Handed on to {Count(piece.HandedOnTo.Count)}; then its lessons.")
            {
                WhoActs = "The pieces it handed on to; shown under it.",
            };
        }

        var startable = piece.IsCurrentPiece && card is { HasSession: false, CanStart: true };

        if (!piece.IsListed)
        {
            return new PieceStatus(StageNodeState.NotInProject, "Not started; it is not listed in project.md.")
            {
                WhoActs = "Nobody yet. Add it to an objective, or start it.",
                Action = PieceAction.AddToObjective,
                WaitsForYou = true,
            };
        }

        if (stages.All(stage => stage.State == StageNodeState.ToDo))
        {
            var line = piece.IsFreeform && piece.RecordsFolder is not null && piece.ObjectiveNumber is { } objective
                ? $"Freeform: done when objective {objective} is done."
                : "Not started: no records yet.";

            return new PieceStatus(StageNodeState.ToDo, line)
            {
                WhoActs = startable ? $"{piece.Workstream}, once you start it." : "Nobody yet.",
                Action = startable ? PieceAction.Start : PieceAction.None,
            };
        }

        var next = StageOrder.NextToDo(stages, stage => stage.State);
        var nextName = next < 0 ? stages.First(stage => !StageNodeStates.IsFinished(stage.State)).Name : stages[next].Name;

        return new PieceStatus(StageNodeState.ToDo, $"Next: {nextName}.{(noSpecHere ? " Its spec gate is not passed here: no spec.md in its records." : string.Empty)}")
        {
            WhoActs = startable ? $"{piece.Workstream}, once you start it." : $"{piece.Workstream}, when it gets there.",
            Action = startable ? PieceAction.Start : PieceAction.None,
        };
    }

    private static string Finished(PieceOfWork piece)
    {
        if (piece.HandedOnTo.Count > 0)
        {
            return $"Done: handed on to {Count(piece.HandedOnTo.Count)}.";
        }

        return piece.IsBeforeKit ? "Done, before method-v0.2." : "Done.";
    }

    private static string Count(int count) => count == 1 ? "1 piece of work" : $"{count} pieces of work";

    private static string Join(IReadOnlyList<string> words) => words.Count switch
    {
        0 => string.Empty,
        1 => words[0],
        _ => string.Join(", ", words.Take(words.Count - 1)) + " or " + words[^1],
    };
}
