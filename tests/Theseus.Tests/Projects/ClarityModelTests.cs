using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Projects;

/// <summary>1.1f's model on its two fixtures: kinds, titles, rounds, hand-overs and true stage states (A, B, D, E).</summary>
[TestFixture]
public class ClarityModelTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static StageNodeState[] States(PieceOfWork piece) => [.. piece.Stages.Select(stage => stage.State)];

    [Test]
    public void OnBuildYourMarket_BothSpecsAreSpec_W0AndW8Freeform_AndW1ToW7Build()
    {
        var pieces = BymPieces(_folder);
        string KindOf(string name) => Named(pieces, name).Kind.ToString();

        Assert.That(new[] { Spec1, Spec2 }.Select(KindOf), Has.All.EqualTo("Spec"));
        Assert.That(new[] { "bym-mcp-preconditions", "bym-mcp-search-blob-access" }.Select(KindOf), Has.All.EqualTo("Freeform"));
        Assert.That(new[]
        {
            "bym-mcp-identity", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra", "bym-mcp-gui-table-reads",
            "bym-mcp-gui-search-reads",
        }.Select(KindOf), Has.All.EqualTo("Build"));
    }

    [Test]
    public void OnTheseus_TheBugsAreBug_TheKitAndHoldoutsFreeform_TheDesignSpec_AndTheRestBuild()
    {
        var pieces = TheseusPieces(_folder);
        var kinds = pieces.ToDictionary(piece => piece.Name, piece => piece.Kind);

        Assert.That(new[] { "readme-runtime", "guard-mentions", "resumed-stop" }.Select(name => kinds[name]), Has.All.EqualTo(PieceKind.Bug));
        Assert.That(new[] { "method-kit", "holdout-scenarios" }.Select(name => kinds[name]), Has.All.EqualTo(PieceKind.Freeform));
        Assert.That(kinds["project-view-clarity"], Is.EqualTo(PieceKind.Spec));
        Assert.That(kinds.Where(pair => pair.Value == PieceKind.Build).Select(pair => pair.Key), Is.EquivalentTo(new[]
        {
            "stop-timing", "card-branches", "parked-cores", "verifier-card", "project-view", "project-home-folders",
            "projects-and-method-ui", "project-view-redesign", "projects-in-use",
        }));
    }

    [Test]
    public void AVerifierCard_IsVerify_WhateverItsPipeline()
    {
        Assert.That(PieceKinds.Of(WorkstreamRole.Verifier, PipelineKind.Feature, "none"), Is.EqualTo(PieceKind.Verify));
    }

    [Test]
    public void TheKind_NeverReadsTheCardsName()
    {
        var pieces = BymPieces(_folder);

        Assert.That(Named(pieces, Spec1).Kind, Is.EqualTo(Named(pieces, Spec2).Kind), "both on the card named '… - Spec'");
        Assert.That(PieceKinds.Of(WorkstreamRole.None, PipelineKind.Feature, "owner/a-spec-branch"), Is.EqualTo(PieceKind.Build));
    }

    [Test]
    public void EachKind_HasItsIconWordAndMeaning()
    {
        Assert.That(Enum.GetValues<PieceKind>().Select(PieceKinds.Icon), Is.EqualTo(new[] { "✎", "⚒", "◎", "✱", "○" }));
        Assert.That(Enum.GetValues<PieceKind>().Select(PieceKinds.Word), Is.EqualTo(new[] { "Spec", "Build", "Verify", "Bug", "Freeform" }));
        Assert.That(Enum.GetValues<PieceKind>().Select(PieceKinds.Means), Has.All.Length.GreaterThan(20));
    }

    [Test]
    public void TheTitle_IsWhatThePieceAchieves()
    {
        var pieces = BymPieces(_folder);

        Assert.That(Named(pieces, "bym-mcp-host").Title, Is.EqualTo("MCP host and staff plugin"), "two pieces share 1.4: the card name after ' - '");
        Assert.That(Named(pieces, "bym-mcp-read-api").Title, Is.EqualTo("BuildYourMarket.Api, release 2a"), "alone under 1.3: the objective");
        Assert.That(pieces.Single(piece => !piece.IsListed).Title, Is.EqualTo("IdentityServer redirect URI anchoring (E1)"));
    }

    [Test]
    public void AWhatCell_IsTheTitle_AboveEveryOtherRule()
    {
        Assert.That(PieceTitle.For("Reads from tables", "BuildYourMarket.Api, release 2a", 1, "BYM MCP W3 - Build read API"),
            Is.EqualTo("Reads from tables"));
        Assert.That(PieceTitle.For(null, "Theseus 1.1", 3, "Theseus 1.1e: projects in use"), Is.EqualTo("projects in use"));
        Assert.That(PieceTitle.For(null, "Checks shown on cards", 6, "git-22"), Is.EqualTo("Checks shown on cards"));
    }

    [Test]
    public void ObjectivesWithARoundLine_AreInThatRound_AndTheRestInRound1()
    {
        var file = WriteBym(_folder).File!;

        Assert.That(file.Objectives.Where(objective => objective.Round == 2).Select(objective => objective.Number),
            Is.EqualTo(new[] { "1.7", "1.8", "1.9", "1.10" }));
        Assert.That(file.Rounds.Select(round => round.Name), Is.EqualTo(new[] { "Specify, then build release 2a", "GUI parity" }));
        Assert.That(BymPieces(_folder).Where(piece => piece.Round == 2).Select(piece => piece.Name), Is.EquivalentTo(new[]
        {
            "bym-mcp-gui-table-reads", "bym-mcp-gui-search-reads", "bym-mcp-search-blob-access", Spec2,
        }));
    }

    [Test]
    public void Theseus_ReadsAsOneRound_BegunOnTheEarliestDateItNames()
    {
        var round = WriteTheseus(_folder).File!.Rounds.Single();

        Assert.That((round.Number, round.Name, round.IsImplicit), Is.EqualTo((1, "Round 1", true)));
        Assert.That(round.Began, Is.EqualTo(new DateOnly(2026, 11, 18)));
    }

    [Test]
    public void Round1_HasOneRoot_TheFirstSpec_WithSevenChildren()
    {
        var round = BymTree(_folder).Rounds[0];
        var root = round.Roots.Single();

        Assert.That(root.Piece.Title, Is.EqualTo("Specify the BuildYourMarket MCP server"));
        Assert.That(root.Children, Has.Count.EqualTo(7));
    }

    [Test]
    public void Round2_HasOneRoot_TheGuiParitySpec_WithW6W7AndW8()
    {
        var root = BymTree(_folder).Rounds[1].Roots.Single();

        Assert.That(root.Piece.Title, Is.EqualTo("Specify the GUI-parity tools"));
        Assert.That(root.Children.Select(child => child.Piece.Name), Is.EqualTo(new[]
        {
            "bym-mcp-gui-table-reads", "bym-mcp-gui-search-reads", "bym-mcp-search-blob-access",
        }));
    }

    [Test]
    public void OlderHandOversWithNoParentPiece_GoToTheFromCardsPieceInTheChildsRound()
    {
        var reading = WriteBym(_folder);
        var links = BymLinks().Select(link => link with { ParentPiece = null }).ToList();
        var pieces = PiecesOfWork.Build(reading, _ => null, BymBoard(), links);
        var tree = PieceTree.Build(pieces, ProjectViewFixtures.LinksOn(links, BymBoard()), reading.File!.Rounds);

        Assert.That(tree.Rounds[0].Roots.Single().Children, Has.Count.EqualTo(7));
        Assert.That(tree.Rounds[1].Roots.Single().Children, Has.Count.EqualTo(3));
    }

    [Test]
    public void AReportBackMark_NamesThePieceThatHandedTheWorkOn_NotTheCardsCurrentPiece()
    {
        var w1 = BymTree(_folder).All.Single(node => node.Piece.Name == "bym-mcp-identity");

        Assert.That(w1.ReportBack!.ParentPiece, Is.EqualTo(Spec1));
    }

    [Test]
    public void W1_HasOneChild_E1()
    {
        var w1 = BymTree(_folder).All.Single(node => node.Piece.Name == "bym-mcp-identity");

        Assert.That(w1.Children.Select(child => child.Piece.Title), Is.EqualTo(new[] { "IdentityServer redirect URI anchoring (E1)" }));
    }

    [Test]
    public void TheRound1Spec_ShowsBuildVerifyAndReviewHandedOn_AndLessonsDone()
    {
        var spec = Named(BymPieces(_folder), Spec1);

        Assert.That(spec.Stages.Where(stage => stage.Stage is PipelineStage.Implement or PipelineStage.Verify or PipelineStage.Review)
            .Select(stage => stage.State), Has.All.EqualTo(StageNodeState.HandedOn));
        Assert.That(spec.Stages[^1].State, Is.EqualTo(StageNodeState.Done));
        Assert.That(spec.Stages[4].Reason, Does.StartWith("done by Preconditions, IdentityServer resources and staff client"));
    }

    [Test]
    public void ABuildHandedOnByASpec_HasResearchNotNeeded_TheSpecCoveredIt()
    {
        var w1 = Named(BymPieces(_folder), "bym-mcp-identity");

        Assert.That((w1.Stages[0].State, w1.Stages[0].Reason), Is.EqualTo((StageNodeState.NotNeeded, "the project's spec covered it")));
    }

    [Test]
    public void APieceWhoseRecordsAreElsewhere_ShowsOneMark()
    {
        var fixes = Named(BymPieces(_folder), "identityserver-client-api-and-cli-fixes");

        Assert.That(States(fixes), Is.EqualTo(new[] { StageNodeState.RecordsElsewhere }));
        Assert.That(fixes.Status.WhoActs, Is.EqualTo("No action needed. Its records are kept outside the project."));
    }

    [Test]
    public void W0AndW8_AreAnswered_CriteriaNotTicked()
    {
        var pieces = BymPieces(_folder);

        Assert.That(new[] { "bym-mcp-preconditions", "bym-mcp-search-blob-access" }.Select(name => Named(pieces, name).Status.State),
            Has.All.EqualTo(StageNodeState.Answered));
        Assert.That(Named(pieces, "bym-mcp-search-blob-access").Status.WhoActs,
            Is.EqualTo("The Specify the GUI-parity tools session ticks objective 1.9's criteria; nothing for you."));
    }

    [Test]
    public void W4_BusyWithNoReport_KeepsVerifyBuiltNotChecked_AndSaysToCheckAfterItStops()
    {
        var w4 = Named(BymPieces(_folder), "bym-mcp-host");

        Assert.That(w4.Stages[4].State, Is.EqualTo(StageNodeState.HoldoutsNotRun));
        Assert.That(w4.Status.WhoActs, Is.EqualTo(
            "Waiting for you to start the check, but its session is busy and has not said on what: check after it stops"));
    }

    [Test]
    public void W6_BusyAheadOfItsSpec_SaysSo()
    {
        var w6 = Named(BymPieces(_folder), "bym-mcp-gui-table-reads");

        Assert.That(w6.Status.Text, Is.EqualTo("Busy building, ahead of your acceptance of its spec (at its own risk)."));
        Assert.That(w6.Stages[0].State, Is.EqualTo(StageNodeState.NotNeeded));
    }

    [Test]
    public void W6_WithANowAtReport_IsBusyOnThatStage()
    {
        var w6 = Named(BymPieces(_folder, reports: ProjectViewFixtures.Said("w6", PipelineStage.Implement, "building tools 4.9 to 4.21")),
            "bym-mcp-gui-table-reads");

        Assert.That((w6.Stages[3].State, w6.Stages[3].Reason), Is.EqualTo((StageNodeState.Busy, "building tools 4.9 to 4.21")));
    }

    [Test]
    public void TheDesignsResearch_IsDoneWithNoRecord()
    {
        var clarity = Named(TheseusPieces(_folder), "project-view-clarity");

        Assert.That(clarity.Stages[0].State, Is.EqualTo(StageNodeState.DoneNoRecord));
        Assert.That(StageNodeStates.Meaning(StageNodeState.DoneNoRecord), Does.Contain("nothing recorded in the records folder"));
    }

    [Test]
    public void TheReadmeBugsMissingRule_IsDoneWithNoRecord()
    {
        Assert.That(Named(TheseusPieces(_folder), "readme-runtime").Stages[1].State, Is.EqualTo(StageNodeState.DoneNoRecord));
    }

    [Test]
    public void OnBothProjects_NoStageBeforeADoneIsToDo()
    {
        foreach (var piece in BymPieces(_folder).Concat(TheseusPieces(_folder)))
        {
            var lastDone = piece.Stages.ToList().FindLastIndex(stage => stage.State == StageNodeState.Done);

            Assert.That(piece.Stages.Take(Math.Max(0, lastDone)).Select(stage => stage.State), Has.None.EqualTo(StageNodeState.ToDo),
                piece.Name);
        }
    }

    [Test]
    public void ASkippedStage_ShowsItsReason_FromSkippedMd()
    {
        var piece = ProjectViewFixtures.OnePiece(_folder, "feature", new Dictionary<string, string>
        {
            ["spec.md"] = "# Spec\n",
            ["plan.md"] = "# Plan\n",
            ["skipped.md"] = "- Research: the 1.1d research still holds\n",
        }, accepted: true);

        Assert.That((piece.Stages[0].State, piece.Stages[0].Reason), Is.EqualTo((StageNodeState.NotNeeded, "the 1.1d research still holds")));
    }

    [Test]
    public void ASkippedStage_ShowsItsReason_FromWhatTheSessionSaid()
    {
        var reports = StageReports.None with
        {
            SkipsOf = (_, _) => new Dictionary<PipelineStage, string> { [PipelineStage.Research] = "nothing to research" },
        };
        var piece = ProjectViewFixtures.OnePiece(_folder, "feature", ProjectViewFixtures.Files("spec.md", "plan.md"), accepted: true,
            reports: reports);

        Assert.That(piece.Stages[0].Reason, Is.EqualTo("nothing to research"));
    }

    [Test]
    public void SkippedMd_ReadsBugWordsAndBullets_AndLeavesProseAlone()
    {
        var skipped = SkippedStages.Read("# Skipped\n\nRule: the fix needs none\n* Review: one line\nSome prose: here.\n");

        Assert.That(skipped, Is.EquivalentTo(new Dictionary<PipelineStage, string>
        {
            [PipelineStage.Spec] = "the fix needs none",
            [PipelineStage.Review] = "one line",
        }));
    }

    [Test]
    public void ASessionsSkip_NeverPassesTheSpecGateOrTheIndependentCheck()
    {
        var piece = ProjectViewFixtures.OnePiece(_folder, "feature", new Dictionary<string, string>
        {
            ["plan.md"] = "# Plan\n",
            ["review.md"] = "# Review\n",
            ["skipped.md"] = "Spec: not needed\nVerify: n/a\n",
        });

        Assert.That((piece.Stages[1].State, piece.Stages[1].Reason), Is.EqualTo((StageNodeState.RecordsElsewhere, PiecesOfWork.NoSpecHere)),
            "only the owner passes the spec gate; a missing spec.md is said as it is, never as Stuck (Nigel, 2026-11-22)");
        Assert.That(piece.Stages[4].State, Is.EqualTo(StageNodeState.ToDo), "only the independent check passes Verify");
        Assert.That(piece.Status.State, Is.Not.EqualTo(StageNodeState.Done));
        Assert.That(piece.Status.Text, Does.StartWith("Evidence missing at Verify"), "said as it is, never Stuck (G15)");
    }

    [Test]
    public void AnUnlistedGrandchild_TakesItsParentsInheritedRound()
    {
        var reading = WriteBym(_folder);
        var cards = BymBoard().Append(new PieceCard("e2", "E2 follow-up", "#d4683a", SessionLifecycle.Idle)).ToList();
        var links = BymLinks().Append(ProjectViewFixtures.NewLink(Theseus.Core.Links.LinkKind.HandsOver, "e1", "e2")).ToList();

        var pieces = PiecesOfWork.Build(reading, _ => null, cards, links);

        Assert.That(pieces.Single(piece => piece.CardKey == "e2").Round, Is.EqualTo(1), "E1 is in round 1, under W1");
    }

    [Test]
    public void ASpecPieceThatWroteNothing_NeverHandsOnItsOwnResearchOrSpec()
    {
        _folder.Write("s/project.md", TestProjectsFolder.ProjectText("S", body: "## Phase 0: Start\n\n### Objective 0.1: Specify\n- [ ] Spec.\n\n"
            + "**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| spec | the-spec | none | r |\n\n"
            + "### Objective 0.2: Build\n- [ ] Built.\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
            + "| builder | the-build | owner/b | r |\n"));
        var cards = new[] { new PieceCard("spec", "Spec", "#000000", SessionLifecycle.Idle), new PieceCard("builder", "Builder", "#000000", SessionLifecycle.Idle) };
        var spec = PiecesOfWork.Build(ProjectViewFixtures.Read(_folder, "s"), _ => null, cards,
            [ProjectViewFixtures.NewLink(Theseus.Core.Links.LinkKind.HandsOver, "spec", "builder")]).Single(piece => piece.Name == "the-spec");

        Assert.That(spec.Stages[0].State, Is.EqualTo(StageNodeState.ToDo));
        Assert.That(spec.Stages[1].State, Is.EqualTo(StageNodeState.ToDo));
        Assert.That(spec.Stages[3].State, Is.EqualTo(StageNodeState.HandedOn));
        Assert.That(spec.Status.State, Is.Not.EqualTo(StageNodeState.Done));
    }
}
