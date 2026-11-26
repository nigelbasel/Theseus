using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Builds a project's pieces of work and their stages (1.1d AC 1 to 6, 13; 1.1f A, E). Stage and gate logic is
/// <see cref="StageCalculator"/>'s and the evidence block's; this names the stages, words their states, says what the
/// hand-overs did, and lays the card's session over its current piece from what the session said (E24, D8).
/// </summary>
public static class PiecesOfWork
{
    private const string SpecCoveredIt = "the project's spec covered it";

    /// <summary>
    /// What a Verify stage says when later records are written but no evidence.md: to do, not Stuck, since nothing blocks it
    /// (Nigel, 2026-11-22: "saying stuck and not being stuck are unacceptable").
    /// </summary>
    public const string NoEvidenceYet = "evidence.md not written, though later records are";

    /// <summary>What a feature's spec stage says when its records hold later work but no spec.md.</summary>
    public const string NoSpecHere = "no spec.md in its records, so its gate is not shown as passed";

    private enum DraftKind
    {
        Spec,
        Work,
        Verify,
        Record,
        Freeform,
        Elsewhere,
    }

    /// <summary>
    /// The pieces with what their sessions reported the owner accepted in their tabs: a report counts only for the version
    /// on disk, so one of text since changed leaves the spec waiting for his Accept again (git-22, 2026-11-26).
    /// </summary>
    /// <param name="pieces">The pieces.</param>
    /// <param name="reading">The project as read, whose records hold each spec's version.</param>
    /// <param name="reported">Every reported acceptance, any project.</param>
    public static IReadOnlyList<PieceOfWork> WithReports(IReadOnlyList<PieceOfWork> pieces, ProjectFileReading reading,
        IEnumerable<ReportedAcceptance> reported)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(reported);
        var reports = reported.ToList();

        return [.. pieces.Select(piece => reports.LastOrDefault(report =>
                string.Equals(report.Project, reading.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(report.Slug, piece.Name, StringComparison.OrdinalIgnoreCase)) is { } report
                && SpecGates.SpecOf(reading, piece.Name) is { } spec
                && string.Equals(report.ContentHash, spec.ContentHash, StringComparison.Ordinal)
            ? piece with { Reported = report }
            : piece)];
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
    /// <param name="reports">What sessions said about their stages; none when null.</param>
    public static IReadOnlyList<PieceOfWork> Build(ProjectFileReading reading, Func<string, SpecAcceptance?> acceptanceOf,
        IEnumerable<PieceCard> cards, IEnumerable<Link> links, ProjectMembers? members = null, StageReports? reports = null)
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
        var said = reports ?? StageReports.None;
        var byKey = cards.GroupBy(card => card.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var owners = members ?? ProjectMembers.From([reading]);
        var linkList = links.Where(link => link.IsActive).ToList();
        var verifiers = VerifiersBySubject(byKey, linkList);
        var drafts = new List<PieceDraft>();

        foreach (var objective in file.Objectives)
        {
            foreach (var row in objective.Workstreams)
            {
                var card = byKey.GetValueOrDefault(row.Workstream);

                // A verifier's row (written since 1.1e PR 6) is not a piece of work: it checks one. Its card may be gone (cleared
                // up and archived), so its row is known by the shape ProposalRow.ForVerifier writes, too: drawn as a piece, it
                // showed as "Spec · <objective title> · not started" (walkthrough, 2026-11-23).
                if (card?.Role == WorkstreamRole.Verifier || (card is null && Theseus.Core.Proposals.ProposalRow.IsVerifierRow(row)))
                {
                    continue;
                }

                var member = owners.Of(row.Workstream);
                var isCurrent = card is not null && member is not null
                                && string.Equals(member.Project, file.Name, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(member.Slug, row.Slug, StringComparison.OrdinalIgnoreCase);

                drafts.Add(Listed(reading, file, objective, row, card, isCurrent, member?.Slug,
                    isCurrent ? verifiers.GetValueOrDefault(row.Workstream, []) : [], acceptanceOf, said));
            }
        }

        drafts.AddRange(Unlisted(drafts, byKey, linkList, owners, verifiers));

        var handOvers = PieceHandOvers.Resolve(drafts, draft => draft.CardKey, draft => draft.Name, draft => draft.IsCurrent,
            draft => draft.Objective?.Round, linkList);
        var parentOf = handOvers.ToDictionary(pair => pair.Child, pair => pair.Parent);
        var latestRound = file.Rounds[^1].Number;
        var rounds = new Dictionary<int, int>();

        // A piece no project file lists takes its parent's round, which may itself be inherited (1.1f C14).
        int RoundOf(int index)
        {
            if (rounds.TryGetValue(index, out var known))
            {
                return known;
            }

            // Set before following the parent, so a loop of hand-overs ends in the latest round instead of recursing.
            rounds[index] = latestRound;

            return rounds[index] = drafts[index].Objective?.Round
                                   ?? (parentOf.TryGetValue(index, out var parent) ? RoundOf(parent) : latestRound);
        }

        var pending = VerifierCards.BySubject(byKey.Values, linkList);
        var reported = new Dictionary<string, PieceCard>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in linkList.Where(link => link is { IsActive: true, Kind: LinkKind.ReportsBack, LatestReport: not null, SatisfiedUtc: null, ReportIsRejected: false }))
        {
            if (byKey.GetValueOrDefault(link.From) is { Role: WorkstreamRole.Verifier, IsArchived: false } verifier)
            {
                reported[link.To] = verifier;
            }
        }

        return [.. drafts.Select((draft, index) => Finish(draft, index, drafts, handOvers, parentOf, linkList, said, RoundOf(index)))
            .Select(piece => piece.IsCurrentPiece && pending.TryGetValue(piece.CardKey, out var verifiers)
                ? piece with { PendingVerifiers = verifiers }
                : piece)
            .Select(piece => piece.IsCurrentPiece && reported.TryGetValue(piece.CardKey, out var verifier)
                ? piece with { CheckReported = verifier }
                : piece)];
    }

    // A verifier's subject is the card it reports back to, as ShellViewModel.ApplyMembership finds it.
    private static Dictionary<string, List<string>> VerifiersBySubject(Dictionary<string, PieceCard> byKey, List<Link> links)
    {
        var subjects = links
            .Where(link => link.Kind == LinkKind.ReportsBack && byKey.GetValueOrDefault(link.From)?.Role == WorkstreamRole.Verifier)
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

    private static PieceDraft Listed(ProjectFileReading reading, ProjectFile file, ProjectObjective objective,
        ProjectWorkstreamRow row, PieceCard? card, bool isCurrent, string? currentPiece, IReadOnlyList<string> verifiers,
        Func<string, SpecAcceptance?> acceptanceOf, StageReports said)
    {
        var isBeforeKit = file.BeforeKit.Contains(row.Slug);
        var artefacts = (reading.Artefacts.GetValueOrDefault(row.Slug) ?? WorkstreamArtefacts.NotStarted(row.Slug))
            with { IsBeforeKit = isBeforeKit };
        var stage = StageCalculator.Calculate(objective.Pipelines, artefacts, acceptanceOf(row.Slug));
        var workstream = card?.Title ?? row.Workstream;
        var context = new StageNodeContext(workstream, artefacts.Source.Folder, card is not null, isCurrent,
            card?.HasSession == true, card is null ? null : currentPiece, card?.CanStart == true);
        var stages = stage.Pipeline is null or PipelineKind.Freeform
            ? [FreeformDraft(isBeforeKit || objective.IsDone)]
            : RecordDrafts(stage.Pipeline.Value, stage, artefacts, isBeforeKit);
        var skips = new Dictionary<PipelineStage, string>(artefacts.Skipped);

        foreach (var (skipped, reason) in said.SkipsOf(file.Name, row.Slug))
        {
            skips[skipped] = reason;
        }

        return new PieceDraft(row.Slug, row.Workstream, workstream, card, isCurrent, stage.Pipeline, context, verifiers, stages)
        {
            Objective = objective,
            Row = row,
            RecordsFolder = artefacts.Source.Folder,
            IsBeforeKit = isBeforeKit,
            Skips = skips,
            Kind = PieceKinds.Of(card?.Role ?? WorkstreamRole.None, stage.Pipeline, row.Branch),
            Title = PieceTitle.For(row.What, objective.Title, objective.Workstreams.Count, workstream),
            AwaitsSpecAcceptance = stage.IsWaitingOnOwner,
            Acceptance = acceptanceOf(row.Slug),
        };
    }

    // A card handed work by a piece here but listed in no project file is shown under its parent (Question 1, followed
    // provisionally overnight on 2026-11-20), so a hand-over never vanishes from the tree.
    private static List<PieceDraft> Unlisted(List<PieceDraft> listed, Dictionary<string, PieceCard> byKey, List<Link> links,
        ProjectMembers owners, Dictionary<string, List<string>> verifiers)
    {
        var inProject = new HashSet<string>(listed.Select(piece => piece.CardKey), StringComparer.OrdinalIgnoreCase);
        var added = new List<PieceDraft>();
        bool grew;

        do
        {
            grew = false;
            var all = listed.Concat(added).ToList();

            foreach (var link in PieceTree.HandOvers(links))
            {
                if (inProject.Contains(link.To)
                    || byKey.GetValueOrDefault(link.To) is not { Role: not WorkstreamRole.Verifier } card
                    || owners.Of(card.Key) is not null
                    || !HasParentHere(all, link))
                {
                    continue;
                }

                inProject.Add(card.Key);
                added.Add(UnlistedPiece(card, verifiers.GetValueOrDefault(card.Key, [])));
                grew = true;
            }
        }
        while (grew);

        return added;
    }

    private static bool HasParentHere(List<PieceDraft> pieces, Link link) =>
        link.ParentPiece is { } named
            ? pieces.Any(piece => piece.Card is not null && Same(piece.CardKey, link.From) && Same(piece.Name, named))
            : pieces.Any(piece => piece.Card is not null && Same(piece.CardKey, link.From));

    private static PieceDraft UnlistedPiece(PieceCard card, IReadOnlyList<string> verifiers)
    {
        var context = new StageNodeContext(card.Title, null, true, true, card.HasSession, card.Title, card.CanStart);
        var stages = StageCalculator.StagesOf(PipelineKind.Feature)
            .Select(stage => Draft(PipelineKind.Feature, stage, StageNodeState.ToDo, null, false))
            .ToList();

        return new PieceDraft(card.Title, card.Key, card.Title, card, true, PipelineKind.Feature, context, verifiers, stages)
        {
            IsListed = false,
            Kind = PieceKinds.Of(card.Role, PipelineKind.Feature, null),
            Title = PieceTitle.For(null, null, 0, card.Title),
        };
    }

    private static PieceOfWork Finish(PieceDraft draft, int index, List<PieceDraft> all, List<(int Parent, int Child)> handOvers,
        Dictionary<int, int> parentOf, List<Link> links, StageReports said, int round)
    {
        var parent = parentOf.TryGetValue(index, out var parentIndex) ? all[parentIndex] : null;
        var children = handOvers.Where(pair => pair.Parent == index).Select(pair => all[pair.Child]).ToList();
        var backLink = parent is null
            ? null
            : links.LastOrDefault(link => link.Kind == LinkKind.ReportsBack && Same(link.From, draft.CardKey) && Same(link.To, parent.CardKey));
        var reported = draft.IsCurrent && backLink is not null && !backLink.ReportIsRejected && (backLink.LatestReport is not null || backLink.SatisfiedUtc is not null);
        var confirmed = draft.IsCurrent && backLink?.SatisfiedUtc is not null;
        // The document is the link's own, set by the owner, not the one the session named (review of the later report).
        var later = draft.IsCurrent && backLink is { SatisfiedUtc: { } satisfiedAt, LatestReport: { } latest } && latest.AtUtc > satisfiedAt
            ? latest with { Document = backLink.Output ?? latest.Document }
            : null;
        var nowAt = draft.IsCurrent && draft.Card is { IsWorking: true } ? said.NowAtOf(draft.CardKey) : null;
        var isObjectiveDone = draft.Objective?.IsDone == true;
        var stages = draft.Stages;

        if (draft.IsListed && draft.RecordsFolder is null && !draft.IsBeforeKit && reported)
        {
            stages = [new StageDraft(DraftKind.Elsewhere, null, "Records", StageNodeState.RecordsElsewhere,
                "its records are kept outside the project", null, false)];
        }
        else if (draft.Pipeline is PipelineKind.Feature or PipelineKind.Bug && draft.IsListed)
        {
            stages = HandOn(stages, draft.Kind, children);
            stages = BeforeFurthest(stages, draft, parent);
        }
        else if (draft.IsListed && confirmed && !isObjectiveDone && stages.Count == 1 && stages[0].State == StageNodeState.ToDo)
        {
            stages = [stages[0] with { State = StageNodeState.Answered }];
        }

        var handIn = draft.IsCurrent && draft.Card is { HandIn: { } given } giver && IsOfLatestRun(given, giver) ? given : null;

        if (handIn is not null && IsHandInOpen(handIn, draft.Card!, draft.CardKey, links))
        {
            stages = Reopen(stages, handIn, draft.Card is { IsWorking: true, IsBlocked: false } && nowAt is null);
        }

        stages = LaySessionOver(stages, draft.Card, draft.IsCurrent, draft.Workstream, nowAt);

        return new PieceOfWork(draft.Name, draft.CardKey, draft.Workstream, draft.Objective?.Number, draft.Objective?.Title,
            draft.Pipeline, draft.RecordsFolder, draft.IsBeforeKit, Nodes(stages, draft.Context, draft.Verifiers))
        {
            Card = draft.Card,
            IsCurrentPiece = draft.IsCurrent,
            IsListed = draft.IsListed,
            Kind = draft.Kind,
            Title = draft.Title,
            Round = round,
            Branch = draft.Row?.Branch,
            Repos = draft.Row?.Repos,
            IsObjectiveDone = isObjectiveDone,
            ParentTitle = parent?.Title,
            ParentCardKey = parent?.CardKey,
            HandedOnTo = [.. children.Select(child => child.Title)],
            HasReportedBack = reported,
            IsReportConfirmed = confirmed,
            LaterReport = later,
            LaterReportLink = later is null ? null : backLink!.Id,
            NowAt = nowAt,
            HandIn = handIn,
            AwaitsSpecAcceptance = draft.AwaitsSpecAcceptance,
            Acceptance = draft.Acceptance,
        };
    }

    // The run a hand-in launched starts just before the hand-in is recorded. A later run, such as the owner's restart for
    // something else, is not that work: the old hand-in is then neither shown nor reopens the piece (review of hand-ins, 2).
    private static readonly TimeSpan LaunchSlack = TimeSpan.FromMinutes(2);

    private static bool IsOfLatestRun(HandIn handIn, PieceCard card) =>
        card.LastRunStartedUtc is not { } started || started <= handIn.AtUtc + LaunchSlack && started >= handIn.AtUtc - LaunchSlack;

    // Open while the session it launched is open, idle between turns included, until this card reports back after it and
    // the owner has not rejected that report. Once the session ends, nothing in the records says whether the work is done.
    private static bool IsHandInOpen(HandIn handIn, PieceCard card, string cardKey, List<Link> links) =>
        (card.HasSession || card.IsStarting)
        && !links.Any(link => link.Kind == LinkKind.ReportsBack && Same(link.From, cardKey) && !link.ReportIsRejected
                              && link.LatestReport is { } report && report.AtUtc > handIn.AtUtc);

    // A hand-in reopens the piece at its build or fix stage, and every stage after it (git-22, 2026-11-24); a piece of one
    // stage reopens that one. A spec piece that handed its build on reopens at its own last stage before what it handed on,
    // which stays its children's. Busy goes on the reopened stage when the session works and has not said where it is.
    private static List<StageDraft> Reopen(List<StageDraft> stages, HandIn handIn, bool busy)
    {
        var at = stages.FindIndex(stage => stage.Stage == PipelineStage.Implement);

        if (at >= 0 && stages[at].State == StageNodeState.HandedOn)
        {
            at = stages.FindIndex(stage => stage.State == StageNodeState.HandedOn) - 1;
        }
        else if (at < 0 && stages.Count == 1)
        {
            at = 0;
        }

        if (at < 0)
        {
            return stages;
        }

        var why = $"work handed in by {handIn.FromTitle}";

        return [.. stages.Select((stage, index) => index < at || stage.State == StageNodeState.HandedOn
            ? stage
            : index == at
                ? stage with { State = busy ? StageNodeState.Busy : StageNodeState.ToDo, Reason = why, IsSessionState = busy }
                : stage with { State = StageNodeState.ToDo, Reason = null })];
    }

    // A spec piece's later stages are done by the pieces it handed work to: every stage after the last it wrote itself, up
    // to the one before its own lessons (1.1f E26); never its own research or spec.
    private static List<StageDraft> HandOn(List<StageDraft> stages, PieceKind kind, List<PieceDraft> children)
    {
        if (kind != PieceKind.Spec || children.Count == 0)
        {
            return stages;
        }

        var lessons = stages.FindIndex(stage => stage.Stage == PipelineStage.Compound);
        var end = lessons < 0 ? stages.Count : lessons;

        if (end == 0)
        {
            return stages;
        }

        var spec = stages.FindIndex(stage => stage.Stage == PipelineStage.Spec);
        var lastOwn = Math.Max(spec, stages.FindLastIndex(end - 1, end, stage => stage.IsWritten));
        var names = string.Join(", ", children.Select(child => child.Title));

        return [.. stages.Select((stage, index) => index > lastOwn && index < end && !stage.IsWritten
            ? stage with { State = StageNodeState.HandedOn, Reason = $"done by {names}" }
            : stage)];
    }

    // Nothing before a finished stage is To do (1.1f E30a): a stage before the furthest recorded one is skipped with its
    // reason, or done with no record. Under the kit the owner's spec gate and the evidence are never assumed done: a
    // missing one before later records is said as missing, never Stuck, since nothing blocks it. Work from before the kit has no gate
    // states (1.1 AC 9), so there they too are done with no record. A session's own skip never stands in for either: only
    // the owner passes the spec gate, and only the independent check passes Verify (review of PR 1, finding 1).
    private static List<StageDraft> BeforeFurthest(List<StageDraft> stages, PieceDraft draft, PieceDraft? parent)
    {
        var furthest = stages.FindLastIndex(stage => stage.IsWritten);
        var specStandsIn = draft.Kind == PieceKind.Build && parent is { Kind: PieceKind.Spec }
                           && !string.Equals(parent.Objective?.Number, draft.Objective?.Number, StringComparison.Ordinal);

        return [.. stages.Select((stage, index) =>
        {
            if (index >= furthest || stage.IsWritten || stage.State is not (StageNodeState.ToDo or StageNodeState.Stuck))
            {
                return stage;
            }

            if (stage.Stage is { } named and not (PipelineStage.Spec or PipelineStage.Verify) && draft.Skips.TryGetValue(named, out var reason))
            {
                return stage with { State = StageNodeState.NotNeeded, Reason = reason };
            }

            if (stage.Stage == PipelineStage.Research && specStandsIn)
            {
                return stage with { State = StageNodeState.NotNeeded, Reason = SpecCoveredIt };
            }

            if (stage.Stage == PipelineStage.Verify && !draft.IsBeforeKit)
            {
                return stage with { State = StageNodeState.ToDo, Reason = NoEvidenceYet };
            }

            // Not Stuck: nothing is blocked, the spec is only not in this folder (Nigel, 2026-11-22: "saying stuck and not being
            // stuck are unacceptable"). The stage says so, and its gate is not shown as passed.
            if (stage.Stage == PipelineStage.Spec && draft.Pipeline == PipelineKind.Feature && !draft.IsBeforeKit)
            {
                return stage with { State = StageNodeState.RecordsElsewhere, Reason = NoSpecHere };
            }

            return stage with { State = StageNodeState.DoneNoRecord, Reason = null };
        })];
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
                drafts.Add(Draft(pipeline, pipelineStage, byRecord, null, isWritten,
                    StageCalculator.RecordFileIn(pipeline, pipelineStage, artefacts.Files)));

                continue;
            }

            drafts.Add(pipelineStage switch
            {
                PipelineStage.Spec => SpecDraft(pipeline, stage.GateAfter(PipelineStage.Spec), isWritten),
                PipelineStage.Verify => VerifyDraft(pipeline, stage.GateAfter(PipelineStage.Verify), artefacts, isWritten),
                _ => Draft(pipeline, pipelineStage, byRecord, null, isWritten,
                    StageCalculator.RecordFileIn(pipeline, pipelineStage, artefacts.Files)),
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
                ? Verify(StageNodeState.ToDo, NoEvidenceYet)
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
        bool isWritten, string? recordFile = null)
    {
        var kind = stage switch
        {
            PipelineStage.Spec => DraftKind.Spec,
            PipelineStage.Implement => DraftKind.Work,
            PipelineStage.Verify => DraftKind.Verify,
            _ => DraftKind.Record,
        };

        return new StageDraft(kind, stage, Name(pipeline, stage), state, reason, recordFile ?? StageCalculator.RecordFileOf(pipeline, stage),
            isWritten);
    }

    // The session lies over the records on its card's current piece only. A question or a block lies on the stage it is
    // working towards, unless the records already hold one waiting on you or stuck (1.1d AC 5). A working session is Busy
    // only on the stage it said it is at (1.1f E25, D8): with no report the records stand and nothing is guessed.
    private static List<StageDraft> LaySessionOver(List<StageDraft> drafts, PieceCard? card, bool isCurrent, string workstream,
        SessionStageReport? nowAt)
    {
        if (card is null || !isCurrent || drafts.Count == 0)
        {
            return drafts;
        }

        if (card is { Lifecycle: not SessionLifecycle.NeedsAttention, IsBlocked: false, IsWorking: true })
        {
            if (nowAt is null)
            {
                return drafts;
            }

            var at = drafts.FindIndex(draft => draft.Stage == nowAt.Stage);

            if (at < 0)
            {
                at = drafts.Count == 1 ? 0 : -1;
            }

            return at < 0
                ? drafts
                : [.. drafts.Select((draft, index) =>
                    index == at ? draft with { State = StageNodeState.Busy, Reason = nowAt.Doing, IsSessionState = true } : draft)];
        }

        (StageNodeState State, string Reason)? session = card switch
        {
            { Lifecycle: SessionLifecycle.NeedsAttention } => (StageNodeState.WaitingOnYou, $"answer {workstream} in its terminal"),
            { IsBlocked: true } => (StageNodeState.Stuck, $"{workstream} is blocked"),
            _ => null,
        };

        if (session is not { } laid)
        {
            return drafts;
        }

        var next = StageOrder.NextToDo(drafts, draft => draft.State);
        var on = next >= 0 ? next : drafts.Count - 1;

        if (drafts[on].State is StageNodeState.WaitingOnYou or StageNodeState.Stuck)
        {
            return drafts;
        }

        return [.. drafts.Select((draft, index) =>
            index == on ? draft with { State = laid.State, Reason = laid.Reason, IsSessionState = true } : draft)];
    }

    private static IReadOnlyList<StageNode> Nodes(List<StageDraft> drafts, StageNodeContext context,
        IReadOnlyList<string> verifiers) =>
        [.. drafts.Select(draft => Node(draft, context, verifiers))];

    private static StageNode Node(StageDraft draft, StageNodeContext context, IReadOnlyList<string> verifiers) =>
        draft.Kind switch
        {
            DraftKind.Spec => new SpecStageNode(draft.Stage!.Value, draft.Name, draft.State, draft.Reason, draft.RecordName,
                draft.IsWritten, draft.IsSessionState, context, draft.AwaitsAcceptance, draft.IsChangedSinceAccepted),
            DraftKind.Work or DraftKind.Elsewhere => new WorkStageNode(draft.Stage, draft.Name, draft.State, draft.Reason,
                draft.RecordName, draft.IsWritten, draft.IsSessionState, context),
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

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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

    private sealed record PieceDraft(
        string Name,
        string CardKey,
        string Workstream,
        PieceCard? Card,
        bool IsCurrent,
        PipelineKind? Pipeline,
        StageNodeContext Context,
        IReadOnlyList<string> Verifiers,
        List<StageDraft> Stages)
    {
        public ProjectObjective? Objective { get; init; }

        public ProjectWorkstreamRow? Row { get; init; }

        public string? RecordsFolder { get; init; }

        public bool IsBeforeKit { get; init; }

        public bool IsListed { get; init; } = true;

        public IReadOnlyDictionary<PipelineStage, string> Skips { get; init; } = new Dictionary<PipelineStage, string>();

        public PieceKind Kind { get; init; }

        public string Title { get; init; } = string.Empty;

        public bool AwaitsSpecAcceptance { get; init; }

        public SpecAcceptance? Acceptance { get; init; }
    }
}
