using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Builds a project's pieces of work and their stages (1.1d AC 1 to 6, 13; Nigel accepted the mockup, 2026-11-20). Stage
/// and gate logic is <see cref="StageCalculator"/>'s and the evidence block's; this only names the stages, words their
/// states and lays the card's session over its current piece.
/// </summary>
public static class PiecesOfWork
{
    private enum DraftKind
    {
        Spec,
        Work,
        Verify,
        Record,
        Freeform,
    }

    /// <summary>
    /// A piece of work for each workstreams row of a readable project, in file order, then one for each card handed work
    /// by one of them that no project file lists, in link order. None for a project that does not read.
    /// </summary>
    /// <param name="reading">The project as read, with its records.</param>
    /// <param name="acceptanceOf">The owner's latest acceptance of a piece's spec, by piece name, or null.</param>
    /// <param name="cards">Every card on the board.</param>
    /// <param name="links">The links; removed ones are ignored.</param>
    /// <param name="members">
    /// Every project's members. A card's current piece is in the project that owns it (the first by name when two name
    /// it), so a card two projects name is live in one view only; and a linked card another project lists is not made a
    /// piece here. When null, only this project is read.
    /// </param>
    public static IReadOnlyList<PieceOfWork> Build(ProjectFileReading reading, Func<string, SpecAcceptance?> acceptanceOf,
        IEnumerable<PieceCard> cards, IEnumerable<Link> links, ProjectMembers? members = null)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(acceptanceOf);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(links);

        if (!reading.IsReadable)
        {
            return [];
        }

        var file = reading.File!;
        var byKey = cards.GroupBy(card => card.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var owners = members ?? ProjectMembers.From([reading]);
        var linkList = links.ToList();
        var verifiers = VerifiersBySubject(byKey, linkList);
        var pieces = new List<PieceOfWork>();

        foreach (var objective in file.Objectives)
        {
            foreach (var row in objective.Workstreams)
            {
                var card = byKey.GetValueOrDefault(row.Workstream);

                // Verifiers join a project through their report-back link, not a row (1.1c plan, departure 1). A row
                // naming a card that is not on the board cannot be told from any other, so it stays a piece.
                if (card?.Role == WorkstreamRole.Verifier)
                {
                    continue;
                }

                var member = owners.Of(row.Workstream);
                var isCurrent = card is not null && member is not null
                                && string.Equals(member.Project, file.Name, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(member.Slug, row.Slug, StringComparison.OrdinalIgnoreCase);

                pieces.Add(Listed(reading, file, objective, row, card, isCurrent, member?.Slug,
                    isCurrent ? verifiers.GetValueOrDefault(row.Workstream, []) : [], acceptanceOf));
            }
        }

        pieces.AddRange(Unlisted(pieces, byKey, linkList, owners, verifiers));

        return pieces;
    }

    // A verifier's subject is the card it reports back to, as ShellViewModel.ApplyMembership finds it.
    private static Dictionary<string, List<string>> VerifiersBySubject(Dictionary<string, PieceCard> byKey, List<Link> links)
    {
        var subjects = links
            .Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                           && byKey.GetValueOrDefault(link.From)?.Role == WorkstreamRole.Verifier)
            .GroupBy(link => link.From, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Verifier: byKey[group.Key].Title, Subject: group.Last().To));
        var bySubject = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (verifier, subject) in subjects)
        {
            if (!bySubject.TryGetValue(subject, out var names))
            {
                bySubject[subject] = names = [];
            }

            names.Add(verifier);
        }

        return bySubject;
    }

    private static PieceOfWork Listed(ProjectFileReading reading, ProjectFile file, ProjectObjective objective,
        ProjectWorkstreamRow row, PieceCard? card, bool isCurrent, string? currentPiece, IReadOnlyList<string> verifiers,
        Func<string, SpecAcceptance?> acceptanceOf)
    {
        var isBeforeKit = file.BeforeKit.Contains(row.Slug);
        var artefacts = (reading.Artefacts.GetValueOrDefault(row.Slug) ?? WorkstreamArtefacts.NotStarted(row.Slug))
            with { IsBeforeKit = isBeforeKit };
        var stage = StageCalculator.Calculate(objective.Pipelines, artefacts, acceptanceOf(row.Slug));
        var workstream = card?.Title ?? row.Workstream;
        var context = new StageNodeContext(workstream, artefacts.Source.Folder, card is not null, isCurrent,
            card?.HasSession == true, card is null ? null : currentPiece, card?.CanStart == true);

        var drafts = stage.Pipeline is null or PipelineKind.Freeform
            ? [FreeformDraft(isBeforeKit || objective.IsDone)]
            : RecordDrafts(stage.Pipeline.Value, stage, artefacts, isBeforeKit);

        return new PieceOfWork(row.Slug, row.Workstream, workstream, objective.Number, objective.Title, stage.Pipeline,
            artefacts.Source.Folder, isBeforeKit,
            Nodes(LaySessionOver(drafts, card, isCurrent, workstream), context, verifiers))
        {
            Card = card,
            IsCurrentPiece = isCurrent,
        };
    }

    // A card handed work by a piece here but listed in no project file is shown under its parent (Question 1, followed
    // provisionally overnight on 2026-11-20), so a hand-over never vanishes from the tree. Only a hand-over from a card
    // whose current piece is here can place it, the same links PieceTree places pieces by.
    private static List<PieceOfWork> Unlisted(List<PieceOfWork> listed, Dictionary<string, PieceCard> byKey, List<Link> links,
        ProjectMembers owners, Dictionary<string, List<string>> verifiers)
    {
        var handOvers = PieceTree.HandOvers(links).ToList();
        var parents = new HashSet<string>(listed.Where(piece => piece.IsCurrentPiece).Select(piece => piece.CardKey),
            StringComparer.OrdinalIgnoreCase);
        var inProject = new HashSet<string>(listed.Select(piece => piece.CardKey), StringComparer.OrdinalIgnoreCase);
        var added = new List<PieceOfWork>();
        bool grew;

        do
        {
            grew = false;

            foreach (var link in handOvers)
            {
                if (!parents.Contains(link.From) || inProject.Contains(link.To)
                    || byKey.GetValueOrDefault(link.To) is not { Role: not WorkstreamRole.Verifier } card
                    || owners.Of(card.Key) is not null)
                {
                    continue;
                }

                parents.Add(card.Key);
                inProject.Add(card.Key);
                added.Add(UnlistedPiece(card, verifiers.GetValueOrDefault(card.Key, [])));
                grew = true;
            }
        }
        while (grew);

        return added;
    }

    private static PieceOfWork UnlistedPiece(PieceCard card, IReadOnlyList<string> verifiers)
    {
        var context = new StageNodeContext(card.Title, null, true, true, card.HasSession, card.Title, card.CanStart);
        var drafts = StageCalculator.StagesOf(PipelineKind.Feature)
            .Select(stage => Draft(PipelineKind.Feature, stage, StageNodeState.ToDo, null, false))
            .ToList();

        return new PieceOfWork(card.Title, card.Key, card.Title, null, null, PipelineKind.Feature, null, false,
            Nodes(LaySessionOver(drafts, card, true, card.Title), context, verifiers))
        {
            Card = card,
            IsCurrentPiece = true,
            IsListed = false,
        };
    }

    private static List<StageDraft> RecordDrafts(PipelineKind pipeline, WorkstreamStage stage, WorkstreamArtefacts artefacts,
        bool isBeforeKit)
    {
        var drafts = new List<StageDraft>();

        foreach (var pipelineStage in StageCalculator.StagesOf(pipeline))
        {
            var isWritten = stage.Reached.Contains(pipelineStage);
            var byRecord = isWritten ? StageNodeState.Done : StageNodeState.ToDo;

            if (isBeforeKit)
            {
                drafts.Add(Draft(pipeline, pipelineStage, byRecord, null, isWritten));

                continue;
            }

            drafts.Add(pipelineStage switch
            {
                PipelineStage.Spec => SpecDraft(pipeline, stage.GateAfter(PipelineStage.Spec), isWritten),
                PipelineStage.Verify => VerifyDraft(pipeline, stage.GateAfter(PipelineStage.Verify), artefacts, isWritten),
                _ => Draft(pipeline, pipelineStage, byRecord, null, isWritten),
            });
        }

        return drafts;
    }

    private static StageDraft SpecDraft(PipelineKind pipeline, StageGate? gate, bool isWritten)
    {
        var noun = pipeline == PipelineKind.Bug ? "rule" : "spec";

        return gate?.State switch
        {
            GateState.Waiting => Draft(pipeline, PipelineStage.Spec, StageNodeState.WaitingOnYou, $"accept the {noun}",
                isWritten) with { AwaitsAcceptance = true },
            GateState.ChangedSinceAccepted => Draft(pipeline, PipelineStage.Spec, StageNodeState.WaitingOnYou,
                $"accept the {noun} again: changed since you accepted it", isWritten) with
            {
                AwaitsAcceptance = true,
                IsChangedSinceAccepted = true,
            },
            GateState.Passed => Draft(pipeline, PipelineStage.Spec, StageNodeState.Done, null, isWritten),
            _ => Draft(pipeline, PipelineStage.Spec, StageNodeState.ToDo, null, isWritten),
        };
    }

    // AC 4: no block, or a last block reading open, is holdouts not run yet; any other last block, one that cannot be
    // read included, is not a pass and not open, so it is Stuck.
    private static StageDraft VerifyDraft(PipelineKind pipeline, StageGate? gate, WorkstreamArtefacts artefacts, bool isWritten)
    {
        StageDraft Verify(StageNodeState state, string? reason = null) =>
            Draft(pipeline, PipelineStage.Verify, state, reason, isWritten);

        if (gate?.State == GateState.Passed)
        {
            return Verify(StageNodeState.Done);
        }

        if (!isWritten)
        {
            return gate?.State == GateState.NoEvidence
                ? Verify(StageNodeState.Stuck, "no evidence, though later records are written")
                : Verify(StageNodeState.ToDo);
        }

        return EvidenceBlockReader.Last(artefacts.EvidenceText) is null or { Result: "open" }
            ? Verify(StageNodeState.HoldoutsNotRun)
            : Verify(StageNodeState.Stuck, "its evidence failed");
    }

    private static StageDraft FreeformDraft(bool isDone) =>
        new(DraftKind.Freeform, null, FreeformStageNode.WorkName, isDone ? StageNodeState.Done : StageNodeState.ToDo, null,
            null, false);

    private static StageDraft Draft(PipelineKind pipeline, PipelineStage stage, StageNodeState state, string? reason,
        bool isWritten)
    {
        var kind = stage switch
        {
            PipelineStage.Spec => DraftKind.Spec,
            PipelineStage.Implement => DraftKind.Work,
            PipelineStage.Verify => DraftKind.Verify,
            _ => DraftKind.Record,
        };

        return new StageDraft(kind, stage, Name(pipeline, stage), state, reason, StageCalculator.RecordFileOf(pipeline, stage),
            isWritten);
    }

    // The session lies over the records on its card's current piece only, at the stage it is working towards; a stage
    // the records already hold waiting on you or stuck keeps that (AC 5). Asking outranks blocked, and blocked outranks
    // working, in the order the status sentence reads them (AC 7).
    private static List<StageDraft> LaySessionOver(List<StageDraft> drafts, PieceCard? card, bool isCurrent, string workstream)
    {
        if (card is null || !isCurrent || drafts.Count == 0)
        {
            return drafts;
        }

        (StageNodeState State, string Reason)? session = card switch
        {
            { Lifecycle: SessionLifecycle.NeedsAttention } => (StageNodeState.WaitingOnYou, $"answer {workstream} in its terminal"),
            { IsBlocked: true } => (StageNodeState.Stuck, $"{workstream} is blocked"),
            { IsWorking: true } => (StageNodeState.Busy, $"{workstream} is working on it"),
            _ => null,
        };

        if (session is not { } laid)
        {
            return drafts;
        }

        var next = StageOrder.NextToDo(drafts, draft => draft.State);
        var at = next >= 0 ? next : drafts.Count - 1;

        if (drafts[at].State is StageNodeState.WaitingOnYou or StageNodeState.Stuck)
        {
            return drafts;
        }

        return [.. drafts.Select((draft, index) =>
            index == at ? draft with { State = laid.State, Reason = laid.Reason, IsSessionState = true } : draft)];
    }

    private static IReadOnlyList<StageNode> Nodes(List<StageDraft> drafts, StageNodeContext context,
        IReadOnlyList<string> verifiers) =>
        [.. drafts.Select(draft => Node(draft, context, verifiers))];

    private static StageNode Node(StageDraft draft, StageNodeContext context, IReadOnlyList<string> verifiers) =>
        draft.Kind switch
        {
            DraftKind.Spec => new SpecStageNode(draft.Stage!.Value, draft.Name, draft.State, draft.Reason, draft.RecordName,
                draft.IsWritten, draft.IsSessionState, context, draft.AwaitsAcceptance, draft.IsChangedSinceAccepted),
            DraftKind.Work => new WorkStageNode(draft.Stage, draft.Name, draft.State, draft.Reason, draft.RecordName,
                draft.IsWritten, draft.IsSessionState, context),
            DraftKind.Verify => new VerifyStageNode(draft.State, draft.Reason, draft.RecordName!, draft.IsWritten,
                draft.IsSessionState, context, verifiers),
            DraftKind.Freeform => new FreeformStageNode(draft.State, draft.Reason, draft.IsSessionState, context),
            _ => new RecordStageNode(draft.Stage!.Value, draft.Name, draft.State, draft.Reason, draft.RecordName,
                draft.IsWritten, draft.IsSessionState, context),
        };

    private static string Name(PipelineKind pipeline, PipelineStage stage) => (pipeline, stage) switch
    {
        (PipelineKind.Bug, PipelineStage.Research) => "Reproduce",
        (PipelineKind.Bug, PipelineStage.Spec) => "Rule",
        (PipelineKind.Bug, PipelineStage.Implement) => "Fix",
        _ => stage.ToString(),
    };

    private sealed record StageDraft(
        DraftKind Kind,
        PipelineStage? Stage,
        string Name,
        StageNodeState State,
        string? Reason,
        string? RecordName,
        bool IsWritten)
    {
        public bool IsSessionState { get; init; }

        public bool AwaitsAcceptance { get; init; }

        public bool IsChangedSinceAccepted { get; init; }
    }
}
