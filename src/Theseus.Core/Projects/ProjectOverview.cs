using System.Globalization;
using Theseus.Core.Domain;
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
        IReadOnlyList<string> tallies, IReadOnlyList<WaitItem> waits, IReadOnlyList<string> underway)
    {
        Sentence = sentence;
        RoundLines = roundLines;
        Phases = phases;
        Tallies = tallies;
        Waits = waits;
        Underway = underway;
    }

    /// <summary>The summary sentence under the project's name (G33).</summary>
    public string Sentence { get; }

    /// <summary>Each round's one line, by its number (C10).</summary>
    public IReadOnlyDictionary<int, string> RoundLines { get; }

    /// <summary>The phase strip, one cell per phase (G34).</summary>
    public IReadOnlyList<PhaseCell> Phases { get; }

    /// <summary>The value of the process, as tallies (G35).</summary>
    public IReadOnlyList<string> Tallies { get; }

    /// <summary>
    /// Everything that asks something of the owner, grouped as G36 groups it (G36, G37a). The sentence, the heading, the
    /// "waiting on you" chip and the project card's badge all count this, so they never disagree (walkthrough rows 6, 14).
    /// </summary>
    public IReadOnlyList<WaitItem> Waits { get; }

    /// <summary>
    /// What Theseus is doing itself for its checks, such as "Checking one build…": said apart from Waits for you, since it
    /// asks nothing (git-22, 2026-11-24: it was counted in the heading as a wait).
    /// </summary>
    public IReadOnlyList<string> Underway { get; }

    /// <summary>
    /// What waits on the project's lead rather than the owner, such as "Thirty-five objectives have no workstream yet.":
    /// the lead plans an objective's work, so it is said beside Waits for you and not counted in it (project-view-scale,
    /// 2026-11-24).
    /// </summary>
    public IReadOnlyList<string> ForTheLead { get; init; } = [];

    /// <summary>
    /// The criteria's asks of someone other than the owner in the phases under way, as "Waits on the gateway team: …": said
    /// beside Waits for you and never counted in it (project-view-scale, git-22 for Nigel, 2026-11-24).
    /// </summary>
    public IReadOnlyList<WaitItem> Others { get; init; } = [];

    /// <summary>
    /// The criteria's asks in phases not yet started, by phase number, the owner's and others': held under their phase,
    /// not in the band, since the band shows what blocks the phase under way (git-22 for Nigel, 2026-11-24).
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<WaitItem>> Held { get; init; } = new Dictionary<int, IReadOnlyList<WaitItem>>();

    /// <summary>The owner's names the asks were read against, for lines written before the <c>on</c> form.</summary>
    public IReadOnlyCollection<string> OwnerNames { get; init; } = [];

    /// <summary>The line its last clear-up leaves under the heading, with Undo and Dismiss, or null.</summary>
    public string? ClearUpNotice { get; init; }

    /// <summary>Works out the overview of a project.</summary>
    /// <param name="file">The project file.</param>
    /// <param name="pieces">Its pieces, as <see cref="PiecesOfWork.Build"/> gave them.</param>
    /// <param name="links">The links; reports waiting for confirmation come from them.</param>
    /// <param name="proposals">Every proposal; the pending ones raised by this project's cards wait for him.</param>
    /// <param name="accepted">How many of its specs he has accepted.</param>
    /// <param name="startRequests">Sessions' requests to start cards he paused; the pending ones for this project's cards wait for him.</param>
    /// <param name="titleOf">A card's title from its key, for a card that works no current piece; null shows the key.</param>
    /// <param name="others">The project's cards that work no current piece, such as its verifiers: one waiting on the owner waits here.</param>
    /// <param name="wrongVerifiers">The project's verifier cards that should not be kept; any makes one line to clear them up.</param>
    /// <param name="clearUpNotice">The line its last clear-up leaves while it can be undone, or null: a notice, not a wait.</param>
    /// <param name="underway">What Theseus is doing itself for its checks, said with nothing to press (git-22, 2026-11-23).</param>
    /// <param name="otherWaits">Lines the shell works out that the file and pieces cannot say, such as a check Theseus cannot start and why.</param>
    /// <param name="ownerNames">The owner's names, for criteria lines written before the <c>on &lt;who&gt;:</c> form.</param>
    public static ProjectOverview For(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, IEnumerable<Link> links,
        IEnumerable<WorkstreamProposal> proposals, int accepted, IEnumerable<StartRequest>? startRequests = null,
        Func<string, string>? titleOf = null, IReadOnlyList<WrongVerifier>? wrongVerifiers = null, string? clearUpNotice = null,
        IReadOnlyList<string>? underway = null, IEnumerable<PieceCard>? others = null, IReadOnlyList<WaitItem>? otherWaits = null,
        IReadOnlyCollection<string>? ownerNames = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(proposals);

        var names = ownerNames ?? [];
        var asks = CriterionAsks(file, names);
        var waits = WaitsOf(file, pieces, [.. links], proposals, startRequests ?? [], titleOf, others ?? []);
        waits.AddRange(asks.Where(ask => ask.Phase is null && ask.Item.Who is null).Select(ask => ask.Item));
        waits.AddRange(otherWaits ?? []);

        if (wrongVerifiers is { Count: > 0 } wrong)
        {
            waits.Insert(0, new WaitItem(WaitKind.ClearVerifiers,
                $"{Capital(Count(wrong.Count, "card"))} Theseus made should not be kept: a copy of a build's check, a check of another project's scenarios, or a scenario writer aimed at the wrong folder.",
                []));
        }

        waits = Distinct(waits);
        var sentence = SentenceOf(file, pieces, waits);
        var roundLines = file.Rounds.ToDictionary(round => round.Number, round => RoundLine(pieces.Where(piece => piece.Round == round.Number).ToList()));

        var unplanned = file.Objectives.Count(objective => !objective.IsDone && objective.Workstreams.Count == 0);

        return new ProjectOverview(sentence, roundLines, [.. file.Phases.Select(phase => Cell(phase, pieces))],
            TalliesOf(file, pieces, accepted), waits, underway ?? [])
        {
            ClearUpNotice = clearUpNotice,
            Others = [.. asks.Where(ask => ask.Phase is null && ask.Item.Who is not null).Select(ask => ask.Item)],
            Held = asks.Where(ask => ask.Phase is not null).GroupBy(ask => ask.Phase!.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<WaitItem>)[.. group.Select(ask => ask.Item)]),
            OwnerNames = names,
            ForTheLead = unplanned == 0 ? [] : [$"{Capital(Count(unplanned, "objective"))} {(unplanned == 1 ? "has" : "have")} no workstream yet."],
        };
    }

    private static List<WaitItem> WaitsOf(ProjectFile file, IReadOnlyList<PieceOfWork> pieces, List<Link> links,
        IEnumerable<WorkstreamProposal> proposals, IEnumerable<StartRequest> startRequests, Func<string, string>? titleOf,
        IEnumerable<PieceCard> others)
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

            if (!status.WaitsForYou || (status.State == StageNodeState.HoldoutsNotRun && status.ActsOnCard is null))
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

            waits.Add(new WaitItem(kind, text, [piece.Name]) { CardKey = status.ActsOnCard ?? piece.CardKey });
        }

        // A card working no piece, such as a verifier, shows in no box, so its waiting on the owner is said here or nowhere
        // (1.6, Nigel 2026-11-23: "there is no indication in theseus that I need to respond").
        foreach (var card in others.Where(card => card.Lifecycle == SessionLifecycle.NeedsAttention))
        {
            if (waits.Any(wait => string.Equals(wait.CardKey, card.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            waits.Add(new WaitItem(WaitKind.Answer, $"{Questions.QuestionWords.Named(card.Title, card.Prompt, card.Question)}; answer it in its tab.", [])
            {
                CardKey = card.Key,
            });
        }

        // Theseus starts a verifier itself as it makes one (Nigel, 2026-11-23), so one waits for the owner only when it is
        // neither running nor starting nor reported, whatever its subject's state: this line and its subject's Verify stage
        // say so, with Start again.
        foreach (var piece in pieces)
        {
            foreach (var verifier in piece.PendingVerifiers.Where(VerifierCards.NeedsAStart))
            {
                waits.Add(new WaitItem(WaitKind.StartVerifier, VerifierCards.NotRunning(verifier), [piece.Name]) { CardKey = verifier.Key });
            }
        }

        // No line asks the owner to start a round's independent checks: Theseus starts each itself once a scenario names
        // the build, and only a start that failed waits for him, as a verifier's Start again above (git-22, 2026-11-23).


        var cards = pieces.Select(piece => piece.CardKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack && link.LatestReport is not null
                                                 && link.SatisfiedUtc is null && !link.ReportIsRejected && cards.Contains(link.From)))
        {
            var from = pieces.FirstOrDefault(piece => piece.IsCurrentPiece && Same(piece.CardKey, link.From));
            waits.Add(new WaitItem(WaitKind.ConfirmReport,
                $"{from?.Title ?? link.From} reported back: read it and confirm it on the board, which lets its objective's criteria be ticked.",
                from is null ? [] : [from.Name]) { CardKey = link.From });
        }

        // A verifier is never a piece, so its report is found from the build it checks.
        foreach (var piece in pieces.Where(piece => piece.CheckReported is not null))
        {
            waits.Add(new WaitItem(WaitKind.ConfirmReport,
                $"{piece.CheckReported!.Title} reported on {piece.Title}: read it and confirm or reject it on the board.", [piece.Name])
            {
                CardKey = piece.CheckReported.Key,
            });
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

    // One line per thing asked of one person, however many criteria wait on it: a real project's 68 owner notes were 47
    // asks, one of them on sixteen criteria (project-view-scale, 2026-11-24). An ask in a phase after the one under way is
    // held there; Phase is null for the phases under way and done.
    private static List<(WaitItem Item, int? Phase)> CriterionAsks(ProjectFile file, IReadOnlyCollection<string> names)
    {
        var current = file.CurrentPhase?.Number ?? int.MaxValue;
        var phaseOf = file.Phases.SelectMany(phase => phase.Objectives.Select(objective => (objective.Number, phase.Number)))
            .ToDictionary(pair => pair.Item1, pair => pair.Item2, StringComparer.Ordinal);
        var entries = file.Objectives
            .SelectMany(objective => objective.Criteria
                .Select((criterion, index) => (Criterion: criterion, Index: index))
                .Where(each => each.Criterion.WaitsOnOwner)
                .SelectMany(each => each.Criterion.Waiting!.OwnerAsks.Select(ask => (
                    Objective: objective,
                    Who: ask.Who(names),
                    Asked: ask.Asked(names).TrimEnd('.', ' '),
                    Criterion: new WaitCriterion(objective.Number, objective.Title, each.Index, each.Criterion.Text, ask.What)
                    {
                        IsOwnerAsk = ask.IsOwner,
                        On = ask.On,
                        Asked = ask.Asked(names),
                    }))))
            .Select(entry => (Entry: entry, Phase: phaseOf.TryGetValue(entry.Objective.Number, out var phase) && phase > current ? phase : (int?)null));

        return
        [
            .. entries.GroupBy(each => (each.Phase, Who: each.Entry.Who?.ToUpperInvariant(), Asked: each.Entry.Asked.ToUpperInvariant()))
                .Select(group => (Item: AskLine([.. group.Select(each => each.Entry)]), group.Key.Phase)),
        ];
    }

    private static WaitItem AskLine(List<(ProjectObjective Objective, string? Who, string Asked, WaitCriterion Criterion)> entries)
    {
        var (_, who, asked, _) = entries[0];
        var objectives = entries.Select(entry => entry.Objective).DistinctBy(objective => objective.Number).ToList();
        var criteria = entries.Count;
        var where = objectives.Count == 1
            ? $"objective {objectives[0].Number}, {objectives[0].Title}" + (criteria == 1 ? string.Empty : $", {Count(criteria, "criterion", "criteria")}")
            : $"{Count(criteria, "criterion", "criteria")}, in objectives {List(objectives.Select(objective => objective.Number))}";
        var text = who is null ? $"{Capital(asked)} ({where})." : $"Waits on {who}: {asked} ({where}).";

        return new WaitItem(who is null ? WaitKind.Decide : WaitKind.Outside, text, [])
        {
            Who = who,
            Ask = asked,
            Criteria = [.. entries.Select(entry => entry.Criterion)],
            Objective = objectives[0].Number,
            Objectives = [.. objectives.Select(objective => objective.Number)],
        };
    }

    // The same ask of the same person is one line: a card serving two objectives is two pieces, and its question was two
    // lines (project-view-scale, 2026-11-24: "many duplicates as well"). A card's question is its session's one question,
    // so it merges across the card's pieces; a block or a spec to accept is the piece's own and keeps its line.
    private static List<WaitItem> Distinct(List<WaitItem> waits) =>
    [
        .. waits.GroupBy(wait => wait.Kind is WaitKind.Answer && wait.CardKey is not null
                ? (wait.Kind, Card: wait.CardKey.ToUpperInvariant(), wait.ProposalId, wait.StartRequestId, Text: string.Empty, Pieces: string.Empty)
                : (wait.Kind, Card: wait.CardKey?.ToUpperInvariant(), wait.ProposalId, wait.StartRequestId, wait.Text, Pieces: string.Join("|", wait.Pieces)))
            .Select(group => group.Skip(1).Aggregate(group.First(), (kept, next) => kept with
            {
                Pieces = [.. kept.Pieces.Union(next.Pieces, StringComparer.Ordinal)],
                Objectives = [.. kept.Objectives.Union(next.Objectives, StringComparer.Ordinal)],
            })),
    ];

    // A line names at most four objectives, so an ask on every objective stays one line.
    private static string List(IEnumerable<string> items)
    {
        var all = items.ToList();

        return all.Count switch
        {
            <= 2 => string.Join(" and ", all),
            <= 4 => $"{string.Join(", ", all[..^1])} and {all[^1]}",
            _ => $"{string.Join(", ", all[..3])} and {Count(all.Count - 3, "more", "more")}",
        };
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
