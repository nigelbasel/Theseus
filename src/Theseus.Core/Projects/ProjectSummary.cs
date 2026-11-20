using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// Where a whole project is, in counts and one sentence (1.1d AC 8 to 10): Nigel wants to see where a project is
/// without going in and asking the session (2026-11-20). The view's summary and the project card both read this, and
/// nothing else counts pieces.
/// </summary>
public sealed class ProjectSummary
{
    private static readonly StageNodeState[] PartOrder =
    [
        StageNodeState.Done, StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.HoldoutsNotRun,
        StageNodeState.Stuck, StageNodeState.ToDo,
    ];

    private readonly Dictionary<StageNodeState, int> _counts;

    private ProjectSummary(Dictionary<StageNodeState, int> counts, string sentence)
    {
        _counts = counts;
        Sentence = sentence;
    }

    /// <summary>How many pieces have each status state; every state is present, zeros included.</summary>
    public IReadOnlyDictionary<StageNodeState, int> Counts => _counts;

    /// <summary>
    /// The sentence, such as "12 pieces of work across 5 objectives: 7 done, … Next: …". Next answers a session that
    /// asks, else accepts the last spec or rule waiting, else runs the holdout scenarios.
    /// </summary>
    public string Sentence { get; }

    /// <summary>How many pieces have this status state.</summary>
    public int CountOf(StageNodeState state) => _counts[state];

    /// <summary>Sums up a project's pieces of work, as <see cref="PiecesOfWork.Build"/> gave them.</summary>
    public static ProjectSummary For(IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var statuses = pieces.Select(piece => (Piece: piece, Status: piece.Status)).ToList();
        var counts = Enum.GetValues<StageNodeState>()
            .ToDictionary(state => state, state => statuses.Count(entry => entry.Status.State == state));
        var objectives = pieces.Select(piece => piece.ObjectiveNumber).OfType<string>().Distinct(StringComparer.Ordinal).Count();
        var parts = PartOrder.Where(state => counts[state] > 0).Select(state => $"{counts[state]} {PartWords(state)}").ToList();

        var sentence = $"{Count(pieces.Count, "piece of work", "pieces of work")} across "
            + $"{Count(objectives, "objective", "objectives")}{(parts.Count > 0 ? ": " + string.Join(", ", parts) : string.Empty)}."
            + $" {Sessions(pieces)}{Next(statuses)}";

        return new ProjectSummary(counts, sentence);
    }

    private static string PartWords(StageNodeState state) => state switch
    {
        StageNodeState.Done => "done",
        StageNodeState.Busy => "busy",
        StageNodeState.WaitingOnYou => "waiting on you",
        StageNodeState.HoldoutsNotRun => "waiting for their holdout check",
        StageNodeState.Stuck => "stuck",
        _ => "still to do or not started",
    };

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Sessions(IReadOnlyList<PieceOfWork> pieces)
    {
        var live = pieces
            .Where(piece => piece.IsCurrentPiece && piece.Card is { HasSession: true })
            .Select(piece => $"{piece.Workstream} is {SessionWords(piece.Card!)} on {piece.Name}")
            .ToList();

        return live.Count == 0 ? "No session is working on it right now." : string.Join("; ", live) + ".";
    }

    private static string SessionWords(PieceCard card) => card switch
    {
        { IsWorking: true } => "busy",
        { Lifecycle: SessionLifecycle.NeedsAttention } => "waiting on you",
        _ => "idle",
    };

    // A session asking holds its work up until it is answered, so it comes before any spec to accept.
    private static string Next(List<(PieceOfWork Piece, PieceStatus Status)> statuses)
    {
        var asking = statuses.LastOrDefault(entry => entry.Piece.Stages.Any(stage =>
            stage.IsSessionState && stage.State == StageNodeState.WaitingOnYou));

        if (asking.Piece is { } asked)
        {
            return $" Next: answer {asked.Workstream} in its terminal.";
        }

        var accepting = statuses.LastOrDefault(entry => entry.Status.State == StageNodeState.WaitingOnYou
                                                        && entry.Piece.Stages.OfType<SpecStageNode>().Any(stage => stage.AwaitsAcceptance));

        if (accepting.Piece is { } piece)
        {
            return $" Next: you accept the {(piece.Pipeline == PipelineKind.Bug ? "rule" : "spec")} of {piece.Name}.";
        }

        return statuses.Any(entry => entry.Piece.Stages.Any(stage => stage.State == StageNodeState.HoldoutsNotRun))
            ? " Next: run the holdout scenarios."
            : string.Empty;
    }
}
