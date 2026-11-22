using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>Pieces of work and their typed stages, from the records and the board (1.1d AC 1 to 6 and 13).</summary>
[TestFixture]
public class PiecesOfWorkTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static string[] Names(PieceOfWork piece) => [.. piece.Stages.Select(stage => stage.Name)];

    private static StageNodeState[] States(PieceOfWork piece) => [.. piece.Stages.Select(stage => stage.State)];

    private static PieceOfWork Named(IEnumerable<PieceOfWork> pieces, string name) => pieces.Single(piece => piece.Name == name);

    [Test]
    public void EachRow_IsAPieceOfWork_InFileOrder_WithItsObjectiveWorkstreamAndRecords()
    {
        var pieces = TheseusPieces(_folder);
        var projectView = Named(pieces, "project-view");

        Assert.That(pieces.Select(piece => piece.Name), Is.EqualTo(new[]
        {
            "method-kit", "holdout-scenarios", "readme-runtime", "guard-mentions", "stop-timing", "card-branches",
            "parked-cores", "resumed-stop", "verifier-card", "project-view", "project-home-folders", "projects-and-method-ui",
        }));
        Assert.That(projectView.Workstream, Is.EqualTo(Git22));
        Assert.That(projectView.ObjectiveNumber, Is.EqualTo("1.1"));
        Assert.That(projectView.ObjectiveTitle, Is.EqualTo("Checks shown on cards"));
        Assert.That(projectView.Pipeline, Is.EqualTo(PipelineKind.Feature));
        Assert.That(projectView.RecordsFolder, Is.EqualTo(Path.Combine(_folder.Home(TheseusProject), "work", "project-view")));
        Assert.That(projectView.IsBeforeKit, Is.False);
        Assert.That(Named(pieces, "stop-timing").IsBeforeKit, Is.True);
    }

    [Test]
    public void APieceWithNoWorkFolder_HasNoRecordsFolder()
    {
        var methodKit = Named(TheseusPieces(_folder), "method-kit");

        Assert.That(methodKit.RecordsFolder, Is.Null, "method-kit's records are named only in prose (code fact 8)");
    }

    [Test]
    public void AProjectThatDoesNotRead_HasNoPieces()
    {
        _folder.Write("pinned/project.md", TestProjectsFolder.ProjectText("Pinned", kit: "method-v0.1"));

        Assert.That(PiecesOfWork.Build(Read(_folder, "pinned"), _ => null, [], []), Is.Empty);
    }

    [Test]
    public void EachStage_IsItsOwnType()
    {
        var feature = Named(TheseusPieces(_folder), "project-view");

        Assert.That(feature.Stages.Select(stage => stage.GetType()), Is.EqualTo(new[]
        {
            typeof(RecordStageNode), typeof(SpecStageNode), typeof(RecordStageNode), typeof(WorkStageNode),
            typeof(VerifyStageNode), typeof(RecordStageNode), typeof(RecordStageNode),
        }));
    }

    [Test]
    public void AFeature_HasSevenStages()
    {
        Assert.That(Names(Named(TheseusPieces(_folder), "stop-timing")),
            Is.EqualTo(new[] { "Research", "Spec", "Plan", "Implement", "Verify", "Review", "Compound" }));
    }

    [Test]
    public void ABug_HasSixStages_NamedReproduceRuleAndFix()
    {
        var readme = Named(TheseusPieces(_folder), "readme-runtime");

        Assert.That(Names(readme), Is.EqualTo(new[] { "Reproduce", "Rule", "Fix", "Verify", "Review", "Compound" }));
        Assert.That(readme.Stages.Select(stage => stage.Stage), Is.EqualTo(new PipelineStage?[]
        {
            PipelineStage.Research, PipelineStage.Spec, PipelineStage.Implement, PipelineStage.Verify,
            PipelineStage.Review, PipelineStage.Compound,
        }));
        Assert.That(readme.Stages[1], Is.TypeOf<SpecStageNode>());
        Assert.That(readme.Stages[2], Is.TypeOf<WorkStageNode>());
    }

    [Test]
    public void FreeformWork_IsOneWorkStage()
    {
        var methodKit = Named(TheseusPieces(_folder), "method-kit");

        Assert.That(methodKit.Stages, Has.Count.EqualTo(1));
        Assert.That(methodKit.Stages[0], Is.TypeOf<FreeformStageNode>());
        Assert.That(methodKit.Stages[0].Name, Is.EqualTo("Work"));
        Assert.That(methodKit.KindWord, Is.EqualTo("freeform"));
    }

    [Test]
    public void AnObjectiveWithNoPipelineChosen_IsFreeformToo()
    {
        var piece = OnePiece(_folder, "[to be chosen]", Files("plan.md"));

        Assert.That(Names(piece), Is.EqualTo(new[] { "Work" }));
        Assert.That(piece.KindWord, Is.EqualTo("freeform"));
    }

    [Test]
    public void FreeformWork_IsDone_WhenListedBeforeTheKit_OrItsObjectiveIsDone_AndToDoOtherwise()
    {
        Assert.That(OnePiece(_folder, "freeform", Files("plan.md"), beforeKit: true).Stages[0].State, Is.EqualTo(StageNodeState.Done));
        Assert.That(OnePiece(_folder, "freeform", Files("plan.md"), objectiveDone: true).Stages[0].State, Is.EqualTo(StageNodeState.Done));
        Assert.That(OnePiece(_folder, "freeform", Files("plan.md")).Stages[0].State, Is.EqualTo(StageNodeState.ToDo));
    }

    [Test]
    public void TheSpec_WaitsOnYou_UntilAccepted_ThenIsDone()
    {
        var waiting = OnePiece(_folder, "feature", Files("spec.md"));
        var spec = waiting.Stages[1];

        Assert.That(spec.State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(spec.Reason, Is.EqualTo("accept the spec"));
        Assert.That(OnePiece(_folder, "feature", Files("spec.md"), accepted: true).Stages[1].State, Is.EqualTo(StageNodeState.Done));
    }

    [Test]
    public void ASpecChangedSinceAccepted_WaitsOnYouAgain()
    {
        var reading = WriteOne(_folder, "feature", Files("spec.md"));
        var stale = new SpecAcceptance(OneProject, APiece, "an-older-hash", DateTimeOffset.UnixEpoch);
        var spec = PiecesOfWork.Build(reading, _ => stale, [Card(ACard)], []).Single().Stages[1];

        Assert.That(spec.State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(spec.Reason, Is.EqualTo("accept the spec again: changed since you accepted it"));
    }

    [Test]
    public void ABugsRule_WaitsOnYouAsARule()
    {
        var piece = OnePiece(_folder, "bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        Assert.That(piece.Stages[1].Name, Is.EqualTo("Rule"));
        Assert.That(piece.Stages[1].State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(piece.Stages[1].Reason, Is.EqualTo("accept the rule"));
    }

    [Test]
    public void ASpecNotWritten_IsToDo()
    {
        Assert.That(OnePiece(_folder, "feature", Files("research.md")).Stages[1].State, Is.EqualTo(StageNodeState.ToDo));
    }

    [Test]
    public void Verify_IsDone_OnAPassingBlock()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md", "evidence.md"), accepted: true);

        Assert.That(piece.Stages[4].State, Is.EqualTo(StageNodeState.Done));
    }

    [TestCase(OpenEvidence)]
    [TestCase("Prose with no result block.\n")]
    public void Verify_HasHoldoutsNotRunYet_WhenTheLastBlockIsOpenOrMissing(string evidence)
    {
        var records = Files("spec.md");
        records["evidence.md"] = evidence;

        Assert.That(OnePiece(_folder, "feature", records, accepted: true).Stages[4].State, Is.EqualTo(StageNodeState.HoldoutsNotRun));
    }

    [TestCase(FailedEvidence)]
    [TestCase("```theseus-evidence\n{ \"result\": \"partial\" }\n```\n")]
    public void Verify_IsStuck_OnAnyResultButPassAndOpen(string evidence)
    {
        var records = Files("spec.md");
        records["evidence.md"] = evidence;
        var verify = OnePiece(_folder, "feature", records, accepted: true).Stages[4];

        Assert.That(verify.State, Is.EqualTo(StageNodeState.Stuck));
        Assert.That(verify.Reason, Is.EqualTo("its evidence failed"));
    }

    [Test]
    public void Verify_IsToDo_NotStuck_WithNoEvidenceThoughLaterRecordsAreWritten()
    {
        var verify = OnePiece(_folder, "feature", Files("spec.md", "review.md"), accepted: true).Stages[4];

        Assert.That(verify.State, Is.EqualTo(StageNodeState.ToDo), "nothing blocks it (G15, Nigel 2026-11-22)");
        Assert.That(verify.Reason, Is.EqualTo(PiecesOfWork.NoEvidenceYet));
    }

    [Test]
    public void Verify_IsToDo_WhenNothingAfterItIsWritten()
    {
        Assert.That(OnePiece(_folder, "feature", Files("spec.md", "plan.md"), accepted: true).Stages[4].State,
            Is.EqualTo(StageNodeState.ToDo));
    }

    [Test]
    public void EveryOtherStage_IsDoneByItsRecord_AndAStageBeforeTheFurthestRecordIsDoneWithNoRecord()
    {
        var projectHome = Named(TheseusPieces(_folder), "project-home-folders");

        Assert.That(projectHome.Stages[0].State, Is.EqualTo(StageNodeState.DoneNoRecord), "research came before later work (1.1f E30a)");
        Assert.That(projectHome.Stages[2].State, Is.EqualTo(StageNodeState.Done));
        Assert.That(projectHome.Stages[6].State, Is.EqualTo(StageNodeState.ToDo));
    }

    [Test]
    public void WorkFromBeforeTheKit_IsDoneByItsRecordsAlone_WithNoGateStates()
    {
        var readme = Named(TheseusPieces(_folder), "readme-runtime");
        var verifierCard = Named(TheseusPieces(_folder), "verifier-card");

        Assert.That(States(readme), Is.EqualTo(new[]
        {
            StageNodeState.Done, StageNodeState.DoneNoRecord, StageNodeState.Done, StageNodeState.Done, StageNodeState.ToDo,
            StageNodeState.ToDo,
        }));
        Assert.That(verifierCard.Stages[4].State, Is.EqualTo(StageNodeState.DoneNoRecord), "no evidence, yet nothing is Stuck before the kit");
    }

    [Test]
    public void AStage_KnowsItsRecord_WhenWritten()
    {
        var projectView = Named(TheseusPieces(_folder), "project-view");

        Assert.That(projectView.Stages[1].RecordName, Is.EqualTo("spec.md"));
        Assert.That(projectView.Stages[1].RecordFile, Is.EqualTo(Path.Combine(projectView.RecordsFolder!, "spec.md")));
        Assert.That(projectView.Stages[6].RecordName, Is.EqualTo("lessons.md"));
        Assert.That(projectView.Stages[6].RecordFile, Is.Null);
    }

    [Test]
    public void WithGit22Running_AndNothingSaid_NoStageIsBusy_AndTheRecordsStand()
    {
        var current = Named(TheseusPieces(_folder, SessionLifecycle.Running), "projects-and-method-ui");

        Assert.That(current.Stages[1].State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(current.Stages, Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Busy));
        Assert.That(current.Stages[6].State, Is.EqualTo(StageNodeState.ToDo));
    }

    [Test]
    public void WithGit22Running_AndAtCompound_CompoundIsBusy_WithWhatItSaid()
    {
        var current = Named(TheseusPieces(_folder, SessionLifecycle.Running,
            reports: Said(Git22, PipelineStage.Compound, "writing the lessons")), "projects-and-method-ui");

        Assert.That(current.Stages[6].State, Is.EqualTo(StageNodeState.Busy));
        Assert.That(current.Stages[6].Reason, Is.EqualTo("writing the lessons"));
        Assert.That(current.Stages[6].IsSessionState, Is.True);
    }

    [Test]
    public void TheSession_LiesOnlyOverItsCardsCurrentPiece()
    {
        var pieces = TheseusPieces(_folder, SessionLifecycle.Running);

        Assert.That(pieces.Where(piece => piece.Name != "projects-and-method-ui").SelectMany(piece => piece.Stages),
            Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Busy));
    }

    [Test]
    public void ALaunchingSession_IsBusyToo_AtTheStageItSaid()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md"), Card(ACard, SessionLifecycle.Launching), accepted: true,
            reports: Said(ACard, PipelineStage.Plan, "planning"));

        Assert.That(piece.Stages[2].State, Is.EqualTo(StageNodeState.Busy));
    }

    [Test]
    public void ASessionThatIsNotWorking_IsNotBusy_WhateverItLastSaid()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md"), Card(ACard, SessionLifecycle.Idle), accepted: true,
            reports: Said(ACard, PipelineStage.Plan, "planning"));

        Assert.That(piece.Stages, Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Busy));
    }

    [Test]
    public void ASessionAsking_WaitsOnYou_ToAnswerInItsTerminal()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard, SessionLifecycle.NeedsAttention), accepted: true);

        Assert.That(piece.Stages[2].State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(piece.Stages[2].Reason, Is.EqualTo("answer a-card in its terminal"));
    }

    [Test]
    public void ABlockedCard_IsStuck_AtItsNextStage()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard, blocked: true), accepted: true);

        Assert.That(piece.Stages[2].State, Is.EqualTo(StageNodeState.Stuck));
        Assert.That(piece.Stages[2].Reason, Is.EqualTo("a-card is blocked"));
    }

    [Test]
    public void WithEverythingDone_AndASessionThatHasNotSaid_EveryStageStaysDone_AndTheLineSaysItHasNotSaid()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md", "implementation.md", "evidence.md",
            "review.md", "lessons.md"), Card(ACard, SessionLifecycle.Running), accepted: true);

        Assert.That(piece.Stages, Has.All.Matches<StageNode>(stage => stage.State == StageNodeState.Done));
        Assert.That(piece.Status.Text, Is.EqualTo("Busy, but has not said on what."));
    }

    [Test]
    public void AnIdleOrEndedSession_ChangesNoStage()
    {
        var idle = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard, SessionLifecycle.Idle), accepted: true);
        var ended = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard, SessionLifecycle.Ended), accepted: true);

        Assert.That(States(idle), Is.EqualTo(States(ended)));
        Assert.That(idle.Stages, Has.None.Matches<StageNode>(stage => stage.IsSessionState));
    }

    [Test]
    public void NothingIsStuckByTime_RecordsYearsOld_ReadAsTheyDidWhenNew()
    {
        var records = Files("research.md", "spec.md", "plan.md");
        var fresh = OnePiece(_folder, "feature", records, Card(ACard, SessionLifecycle.Ended), accepted: true);

        foreach (var file in Directory.EnumerateFiles(_folder.Folder, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetCreationTimeUtc(file, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        var old = PiecesOfWork.Build(Read(_folder, OneProject), slug => new SpecAcceptance(OneProject, slug,
            Read(_folder, OneProject).Artefacts[slug].SpecHash!, new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            [Card(ACard, SessionLifecycle.Ended)], []).Single();

        Assert.That(States(old), Is.EqualTo(States(fresh)));
        Assert.That(old.Stages, Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Stuck));
        Assert.That(old.Status.Text, Is.EqualTo("Next: Implement."));
    }

    [Test]
    public void ACardTwoProjectsName_IsCurrentOnlyInTheProjectThatOwnsIt()
    {
        _folder.Write("alpha/project.md", TestProjectsFolder.ProjectText("Alpha", body: "## Phase 0: Start\n\n### Objective 0.1: X\n"
            + "- [ ] A.\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
            + $"| {Git22} | alpha-piece | owner/x | a-repo |\n"));
        var theseus = WriteTheseus(_folder);
        var members = ProjectMembers.From(ProjectCatalog.Read(_folder.Folder));
        PieceCard[] cards = [Card(Git22, SessionLifecycle.Running)];

        var here = PiecesOfWork.Build(theseus, _ => null, cards, [], members);
        var owner = PiecesOfWork.Build(Read(_folder, "alpha"), _ => null, cards, [], members);

        Assert.That(members.Clashes, Is.EquivalentTo(new[] { "alpha", TheseusProject }));
        Assert.That(here, Has.None.Matches<PieceOfWork>(piece => piece.IsCurrentPiece));
        Assert.That(here.SelectMany(piece => piece.Stages), Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Busy));
        Assert.That(ProjectSummary.For(here).Sentence, Does.Contain("No session is working on it right now."));
        Assert.That(Named(here, "projects-and-method-ui").Stages[3].Actions.Select(action => action.Action),
            Does.Not.Contain(StageAction.Stop));
        Assert.That(Named(here, "project-view").Stages[4].Actions[0].DisabledReason, Does.StartWith("git-22 has moved on to alpha-piece;"));
        Assert.That(owner.Single().IsCurrentPiece, Is.True);
        Assert.That(ProjectSummary.For(owner).Sentence, Does.Contain("git-22 is busy on alpha-piece."));
    }

    [TestCase("```theseus-evidence\n{ \"stage\": \"verify\" }\n```\n", TestName = "ABlockWithNoResult_IsStuck")]
    [TestCase("```theseus-evidence\n{ not json\n```\n", TestName = "ABlockThatIsNotJson_IsStuck")]
    public void AnUnreadableEvidenceBlock_IsStuck_AsAnyResultButPassAndOpen(string evidence)
    {
        var records = Files("spec.md");
        records["evidence.md"] = evidence;
        var verify = OnePiece(_folder, "feature", records, accepted: true).Stages[4];

        Assert.That(verify.State, Is.EqualTo(StageNodeState.Stuck));
        Assert.That(verify.Reason, Is.EqualTo("its evidence failed"));
    }

    [Test]
    public void ACardThatCannotStart_OffersNoStart()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard) with { CanStart = false }, accepted: true);

        Assert.That(piece.Stages[3].Actions.Select(action => action.Action), Does.Not.Contain(StageAction.Start));
    }

    [Test]
    public void ACardHandedWorkButListedNowhere_IsAPieceOfItsOwn_AfterTheListedOnes_InLinkOrder()
    {
        var pieces = BuildYourMarketPieces(_folder);
        var client = Named(pieces, ClientFixes);

        Assert.That(pieces.Select(piece => piece.Name).TakeLast(2), Is.EqualTo(new[] { RedirectAnchoring, ClientFixes }));
        Assert.That(client.IsListed, Is.False);
        Assert.That(client.ObjectiveNumber, Is.Null);
        Assert.That(client.ObjectiveLabel, Is.EqualTo("not in project.md"));
        Assert.That(client.Stages, Has.All.Matches<StageNode>(stage => stage.State == StageNodeState.ToDo));
        Assert.That(client.IsCurrentPiece, Is.True);
    }

    [Test]
    public void ALinkedCardAnotherProjectLists_IsNotAPieceHere()
    {
        var other = TestProjectsFolder.ProjectText("Other", body: "## Phase 0: Start\n\n### Objective 0.1: X\n- [ ] A.\n\n**Pipeline:** feature\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n" + $"| {ClientFixes} | client-fixes | owner/x | a-repo |\n");
        _folder.Write("other/project.md", other);
        var reading = WriteBuildYourMarket(_folder);
        var members = ProjectMembers.From(ProjectCatalog.Read(_folder.Folder));

        var pieces = PiecesOfWork.Build(reading, _ => null, BuildYourMarketBoard(), BuildYourMarketLinks(), members);

        Assert.That(pieces.Select(piece => piece.Name), Does.Not.Contain(ClientFixes));
        Assert.That(pieces.Select(piece => piece.Name), Does.Contain(RedirectAnchoring));
    }

    [Test]
    public void AVerifierCard_IsNeverAPieceOfWork_ButIsNamedOnItsSubjectsVerify()
    {
        const string identity = "bym-mcp-w1-identityserver-resources-and-staff-client";
        var cards = BuildYourMarketBoard().Append(Card("a-verifier", role: WorkstreamRole.Verifier)).ToList();
        var links = BuildYourMarketLinks()
            .Append(NewLink(LinkKind.HandsOver, identity, "a-verifier"))
            .Append(NewLink(LinkKind.ReportsBack, "a-verifier", identity))
            .ToList();

        var pieces = BuildYourMarketPieces(_folder, cards, links);
        var verify = Named(pieces, "bym-mcp-identity").Stages.OfType<VerifyStageNode>().Single();

        Assert.That(pieces.Select(piece => piece.CardKey), Does.Not.Contain("a-verifier"));
        Assert.That(verify.Verifiers, Is.EqualTo(new[] { "a-verifier" }));
        Assert.That(Named(pieces, "bym-mcp-config").Stages.OfType<VerifyStageNode>().Single().Verifiers, Is.Empty);
    }

    [Test]
    public void AVerifierNamedInARow_IsNotAPiece()
    {
        var reading = WriteOne(_folder, "feature", Files("spec.md"));

        Assert.That(PiecesOfWork.Build(reading, _ => null, [Card(ACard, role: WorkstreamRole.Verifier)], []), Is.Empty);
    }

    [Test]
    public void AHandOverFromACardNotOnTheBoard_MakesNoUnlistedPiece()
    {
        var cards = BuildYourMarketBoard().Where(card => card.Key != ServerSpec).ToList();

        var pieces = BuildYourMarketPieces(_folder, cards);

        Assert.That(pieces.Select(piece => piece.Name), Does.Not.Contain(ClientFixes));
        Assert.That(pieces.Select(piece => piece.Name), Does.Contain(RedirectAnchoring));
    }
}
