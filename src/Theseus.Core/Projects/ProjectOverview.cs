using System.Globalization;
using Theseus.Core.Links;
using Theseus.Core.Proposals;

namespace Theseus.Core.Projects;

/// <summary>
/// What the top of the project view says (1.1f G33 to G37b, C10): one summary sentence, a line per round, the phase strip,
/// the process tallies, and every thing that waits for the owner. The count in the sentence is the count of the list:
/// one source.
/// </summary>
public sealed class ProjectOverview
{
    private static readonly string[] Numbers = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

    private ProjectOverview(string sentence, IReadOnlyDictionary<int, string> roundLines, IReadOnlyList<PhaseCell> phases,
        IReadOnlyList<string> tallies, IReadOnlyList<WaitItem> waits)
    {
        Sentence = sentence;
        RoundLines = roundLines;
        Phases = phases;
        Tallies = tallies;
        Waits = waits;
    }

    /// <summary>The summary sentence under the project's name (G33).</summary>
    public string Sentence { get; }

    /// <summary>Each round's one line, by its number (C10).</summary>
    public IReadOnlyDictionary<int, string> RoundLines { get; }

    /// <summary>The phase strip, one cell per phase (G34).</summary>
    public IReadOnlyList<PhaseCell> Phases { get; }

    /// <summary>The value of the process, as tallies (G35).</summary>
    public IReadOnlyList<string> Tallies { get; }

    /// <summary>Everything that waits for the owner, grouped as G36 groups it (G36, G37a).</summary>
    public IReadOnlyList<WaitItem> Waits { get; }

    /// <summary>Works out the overview of a project.</summary>
    /// <param name="file">The project file.</param>
    /// <param name="pieces">Its pieces, as <see cref="PiecesOfWork.Build"/> gave them.</param>
    /// <param name="links">The links; reports waiting for confirmation come from them.</param>
    /// <param name="proposals">Every proposal; the pending ones raised by this project's cards wait for him.</param>
    /// <param name="accepted">How many of its specs he has accepted.</param>
    /// <param name="startRequests">Sessions' requests to start cards he paused; the pending ones for this project's cards wait for him.</param>
    /// <param name="titleOf">A card's title from its key, for a card that works no current piece; null shows the key.</param>
    public static ProjectOverview For(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, IEnumerable<Link> links,
        IEnumerable<WorkstreamProposal> proposals, int accepted, IEnumerable<StartRequest>? startRequests = null,
        Func<string, string>? titleOf = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(proposals);

        var waits = WaitsOf(file, pieces, [.. links], proposals, startRequests ?? [], titleOf);
        var roundLines = file.Rounds.ToDictionary(round => round.Number, round => RoundLine(pieces.Where(piece => piece.Round == round.Number).ToList()));

        return new ProjectOverview(SentenceOf(file, pieces, waits), roundLines, [.. file.Phases.Select(phase => Cell(phase, pieces))],
            TalliesOf(file, pieces, accepted), waits);
    }

    private static List<WaitItem> WaitsOf(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, List<Link> links,
        IEnumerable<WorkstreamProposal> proposals, IEnumerable<StartRequest> startRequests, Func<string, string>? titleOf)
    {
        var waits = new List<WaitItem>();

        foreach (var piece in pieces)
        {
            var status = piece.Status;

            if (piece.Reported is not null)
            {
                waits.Add(new WaitItem(WaitKind.ConfirmAcceptance,
                    $"{piece.Title}: you accepted its spec in its tab; confirm it so the acceptance is recorded.", [piece.Name]));

                continue;
            }

            if (!status.WaitsForYou || status.State == StageNodeState.HoldoutsNotRun)
            {
                continue;
            }

            var kind = status.Action switch
            {
                PieceAction.Accept => WaitKind.AcceptSpec,
                PieceAction.Answer => WaitKind.Answer,
                PieceAction.AddToObjective => WaitKind.AddToObjective,
                _ => WaitKind.Unblock,
            };

            var text = status.Question is { } question
                ? Questions.QuestionWords.Named(piece.Title, question)
                : $"{piece.Title}: {status.WhoActs}";

            waits.Add(new WaitItem(kind, text, [piece.Name]) { CardKey = piece.CardKey });
        }

        // One line for a round's independent checks, with the list in the dialog (G36).
        foreach (var round in pieces.Where(piece => piece.Status.State == StageNodeState.HoldoutsNotRun).GroupBy(piece => piece.Round))
        {
            var builds = round.ToList();
            var busy = builds.FirstOrDefault(piece => piece.Card is { IsWorking: true } && piece.IsCurrentPiece);
            var from = file.Rounds.Count > 1 ? $" from round {round.Key}" : string.Empty;
            var text = $"{Capital(Count(builds.Count, "build"))}{from} {(builds.Count == 1 ? "needs" : "need")} "
                       + "their independent check, which only you start."
                       + (busy is null ? string.Empty : $" One of them, {busy.Title}, is still busy: check it after it stops.");
            waits.Add(new WaitItem(WaitKind.StartChecks, text, [.. builds.Select(piece => piece.Name)]));
        }

        foreach (var objective in file.Objectives)
        {
            foreach (var criterion in objective.Criteria.Where(criterion => criterion.WaitsOnOwner))
            {
                waits.Add(new WaitItem(WaitKind.Outside,
                    $"Objective {objective.Number}, {objective.Title}: {criterion.Waiting!.What}. This happens outside Theseus.", [])
                {
                    Objective = objective.Number,
                });
            }
        }

        var cards = pieces.Select(piece => piece.CardKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack && link.LatestReport is not null
                                                 && link.SatisfiedUtc is null && cards.Contains(link.From)))
        {
            var from = pieces.FirstOrDefault(piece => piece.IsCurrentPiece && Same(piece.CardKey, link.From));
            waits.Add(new WaitItem(WaitKind.ConfirmReport,
                $"{from?.Title ?? link.From} reported back: read it and confirm it on the board, which lets its objective's criteria be ticked.",
                from is null ? [] : [from.Name]) { CardKey = link.From });
        }

        foreach (var proposal in proposals.Where(proposal => proposal.IsPending && cards.Contains(proposal.RequesterKey)))
        {
            var by = pieces.FirstOrDefault(piece => piece.IsCurrentPiece && Same(piece.CardKey, proposal.RequesterKey));
            waits.Add(new WaitItem(WaitKind.Proposal,
                $"{by?.Title ?? proposal.RequesterKey} proposes a new piece of work, {proposal.Request.Title}: approve or reject it.", [])
            {
                CardKey = proposal.RequesterKey,
                ProposalId = proposal.Id,
            });
        }

        string Title(string key) =>
            pieces.FirstOrDefault(piece => piece.IsCurrentPiece && Same(piece.CardKey, key))?.Title ?? titleOf?.Invoke(key) ?? key;

        var current = pieces.Where(piece => piece.IsCurrentPiece).Select(piece => piece.CardKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var request in startRequests.Where(request => request.IsPending && current.Contains(request.CardKey)))
        {
            waits.Add(new WaitItem(WaitKind.StartRequest,
                $"{Title(request.RequesterKey)} wants to start {Title(request.CardKey)}, which you paused"
                + (string.IsNullOrWhiteSpace(request.Reason) ? "." : $": {request.Reason.Trim()}"), [])
            {
                CardKey = request.CardKey,
                StartRequestId = request.Id,
            });
        }

        return waits;
    }

    private static string SentenceOf(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, List<WaitItem> waits)
    {
        var rounds = file.Rounds;
        var latest = rounds[^1];
        // A built piece awaiting its check keeps that state while its session works on, so "moving" also counts a working session.
        var moving = pieces.Where(piece => piece.Round == latest.Number
                                           && (piece.Status.State == StageNodeState.Busy || piece.IsCurrentPiece && piece.Card is { IsWorking: true }))
            .ToList();

        if (rounds.Count == 1)
        {
            var named = moving.Count == 0
                ? "Nothing is moving"
                : Moving(moving);
            var waiting = waits.Count switch
            {
                0 => "Nothing waits for you.",
                1 => $"One thing waits for you: {Uncapital(waits[0].Text)}",
                _ => $"{Capital(Count(waits.Count, "thing"))} wait for you.",
            };

            return $"{named}. {waiting}";
        }

        var movingWords = moving.Count == 0
            ? "nothing is moving"
            : moving.All(piece => piece.Kind == PieceKind.Build)
                ? $"{Count(moving.Count, "build")} {(moving.Count == 1 ? "is" : "are")} under way"
                : $"{Count(moving.Count, "piece of work", "pieces of work")} {(moving.Count == 1 ? "is" : "are")} under way";
        var sentence = $"Round {latest.Number} of {rounds.Count}, {latest.Name}: {movingWords} and "
                       + $"{(waits.Count == 0 ? "nothing waits" : $"{Count(waits.Count, "thing")} {(waits.Count == 1 ? "waits" : "wait")}")} for you.";

        foreach (var round in rounds.Take(rounds.Count - 1))
        {
            sentence += " " + EarlierRound(round, pieces.Where(piece => piece.Round == round.Number).ToList());
        }

        return sentence;
    }

    private static string EarlierRound(ProjectRound round, List<PieceOfWork> pieces)
    {
        var checks = pieces.Count(piece => piece.Status.State == StageNodeState.HoldoutsNotRun);
        var open = pieces.Count(piece => !IsFinished(piece.Status.State) && piece.Status.State != StageNodeState.HoldoutsNotRun);

        return (checks, open) switch
        {
            (0, 0) => $"Round {round.Number} is finished.",
            (_, 0) => $"Round {round.Number}'s build is finished except for {Count(checks, "independent check")}.",
            _ => $"Round {round.Number} has {Count(open, "piece of work", "pieces of work")} still open"
                 + (checks > 0 ? $" and {Count(checks, "independent check")} to run." : "."),
        };
    }

    private static string RoundLine(List<PieceOfWork> pieces)
    {
        if (pieces.Count == 0)
        {
            return "No work yet.";
        }

        var parts = pieces.GroupBy(piece => piece.Status.State)
            .OrderBy(group => group.Key)
            .Select(group => $"{Number(group.Count())} {StageNodeStates.Word(group.Key).ToLowerInvariant()}");

        return $"{Capital(Count(pieces.Count, "piece of work", "pieces of work"))}: {string.Join("; ", parts)}.";
    }

    private static PhaseCell Cell(ProjectPhase phase, IReadOnlyList<PieceOfWork> pieces)
    {
        if (phase.Objectives.Count == 0)
        {
            return new PhaseCell(phase, "not planned", StageNodeState.ToDo);
        }

        if (!phase.HasOpenObjective)
        {
            return new PhaseCell(phase, "✓ gate passed", StageNodeState.Done);
        }

        var numbers = phase.Objectives.Select(objective => objective.Number).ToHashSet(StringComparer.Ordinal);
        var inPhase = pieces.Where(piece => piece.ObjectiveNumber is { } number && numbers.Contains(number)).ToList();

        return inPhase.Any(piece => !IsFinished(piece.Status.State)) || phase.Objectives.Any(objective => objective.IsDone)
            ? new PhaseCell(phase, $"● under way · {phase.Objectives.Count(objective => objective.IsDone)} of {phase.Objectives.Count} objectives met",
                StageNodeState.Busy)
            : new PhaseCell(phase, "not started", StageNodeState.ToDo);
    }

    private static List<string> TalliesOf(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, int accepted)
    {
        var gates = file.Phases.Count(phase => phase.Objectives.Count > 0 && !phase.HasOpenObjective);
        var checksRun = pieces.Count(piece => piece.Stages.Any(stage => stage is VerifyStageNode { State: StageNodeState.Done }));
        var checksOutstanding = pieces.Count(piece => piece.Stages.Any(stage => stage.State == StageNodeState.HoldoutsNotRun));
        var reviews = pieces.Count(piece => piece.Stages.Any(stage => stage.Stage == PipelineStage.Review && stage.RecordFile is not null));
        var changes = file.Rounds.Count - 1;

        return
        [
            $"{Count(gates, "phase gate", "phase gates")} passed",
            $"{Count(accepted, "spec", "specs")} accepted by you",
            $"{checksRun} of {checksRun + checksOutstanding} independent checks run",
            $"{Count(reviews, "review", "reviews")} written",
            changes == 0
                ? "1 round: the requirements have not changed"
                : $"{file.Rounds.Count} rounds: the requirements changed {(changes == 1 ? "once" : $"{changes} times")}",
        ];
    }

    private static bool IsFinished(StageNodeState state) => StageNodeStates.IsFinished(state);

    private static string Count(int count, string one, string? many = null) =>
        $"{(count < Numbers.Length ? Numbers[count] : count.ToString(CultureInfo.InvariantCulture))} {(count == 1 ? one : many ?? one + "s")}";

    // Titles are quoted after a lead-in, so each keeps its own capitals and none runs into the verb (Nigel's capture,
    // 2026-11-22: "…obvious: build is being built and Every question…").
    private static string Moving(IReadOnlyList<PieceOfWork> moving)
    {
        static string Quoted(IReadOnlyList<PieceOfWork> pieces)
        {
            var titles = pieces.Select(piece => $"“{piece.Title}”").ToList();

            return titles.Count <= 2 ? string.Join(" and ", titles) : $"{string.Join(", ", titles[..^1])} and {titles[^1]}";
        }

        var built = moving.Where(piece => piece.Kind == PieceKind.Build).ToList();
        var other = moving.Where(piece => piece.Kind != PieceKind.Build).ToList();
        var parts = new List<string>();

        if (built.Count > 0)
        {
            parts.Add($"Being built: {Quoted(built)}");
        }

        if (other.Count > 0)
        {
            parts.Add($"{(parts.Count > 0 ? "under" : "Under")} way: {Quoted(other)}");
        }

        return string.Join("; ", parts);
    }

    private static string Number(int count) => count < Numbers.Length ? Numbers[count] : count.ToString(CultureInfo.InvariantCulture);

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // Only a line that starts with a counted word is lowered after a colon; a title keeps its own capital.
    private static string Uncapital(string text) =>
        Numbers.Any(number => text.StartsWith(Capital(number) + " ", StringComparison.Ordinal)) ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
