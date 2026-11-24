using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;
using Theseus.Core.Questions;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// The top of the project view on 1.1f's fixtures: the summary sentence, round lines, phase strip, tallies and "Waits for
/// you" (G33 to G37b, C10).
/// </summary>
[TestFixture]
public class ProjectOverviewTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectOverview Bym(IReadOnlyList<Link>? links = null, IEnumerable<WorkstreamProposal>? proposals = null) =>
        ProjectOverview.For(WriteBym(_folder).File!, BymPieces(_folder), links ?? BymLinks(), proposals ?? [], 5);

    [Test]
    public void ARoundedProject_SaysWhichRound_WhatMoves_HowManyThingsWait_AndWhatEarlierRoundsLeft()
    {
        var overview = Bym();

        Assert.That(overview.Sentence, Does.StartWith("Round 2 of 2, GUI parity: one build is under way and "));
        Assert.That(overview.Sentence, Does.Contain($"{Words(overview.Waits.Count)} things wait for you."));
        Assert.That(overview.Sentence, Does.EndWith("Round 1 has one piece of work still open and five independent checks to run."), "E1 is not in project.md, so round 1 is not finished");
    }

    [Test]
    public void ACountOfThingsWaiting_IsTheCountOfTheListThatAsksSomethingOfYou()
    {
        var overview = Bym();

        Assert.That(overview.Waits, Has.Count.GreaterThan(1));
        Assert.That(overview.Sentence, Does.Contain($"{Words(overview.Waits.Count)} things wait for you"),
            "the round's checks, run by Theseus, and lines saying what is under way ask nothing of him");
    }

    [Test]
    public void NoLineAsksYouToStartTheChecks_TheseusStartsThemItself()
    {
        // git-22, 2026-11-23: Theseus starts checks itself; Nigel only confirms or rejects their reports. The line that
        // asked him to press Start the checks… made the sentence say "nothing waits" beside "Waits for you (1)" (walkthrough
        // row 14).
        var overview = Bym();

        Assert.That(overview.Waits.Select(wait => wait.Text), Has.None.Contains("Start the checks"));
        Assert.That(overview.Waits.Select(wait => wait.Button), Has.None.EqualTo("Start the checks…"));
    }

    [Test]
    public void WrongVerifiers_WaitForYouOnOneLine_ThatClearsThemUp()
    {
        IReadOnlyList<WrongVerifier> wrong =
        [
            new("verify-w1", "Verify: W1", "w1", true, "it ran another project's scenarios, in C:\\h"),
            new("verify-w2-2", "Verify: W2 (2)", "w2", false, "a copy: Verify: W2 checks the same build"),
        ];

        var wait = ProjectOverview.For(WriteBym(_folder).File!, BymPieces(_folder), BymLinks(), [], 5, wrongVerifiers: wrong)
            .Waits.Single(item => item.Kind == WaitKind.ClearVerifiers);

        Assert.That(wait.Text, Is.EqualTo(
            "Two cards Theseus made should not be kept: a copy of a build's check, a check of another project's scenarios, or a scenario writer aimed at the wrong folder."));
        Assert.That(wait.Button, Is.EqualTo("Clear up verifiers…"));
        Assert.That(Bym().Waits.Where(item => item.Kind == WaitKind.ClearVerifiers), Is.Empty, "none wrong, no line");
    }

    [Test]
    public void AClearUp_IsANoticeUnderTheHeading_NotAWait()
    {
        const string notice = "Three cards were cleared up: stopped, archived, their reports rejected. Undo until 19:24 tomorrow.";
        var overview = ProjectOverview.For(WriteBym(_folder).File!, BymPieces(_folder), BymLinks(), [], 5, clearUpNotice: notice);

        Assert.That(overview.ClearUpNotice, Is.EqualTo(notice));
        Assert.That(overview.Waits.Select(wait => wait.Text), Has.None.EqualTo(notice),
            "Nigel, 2026-11-23: it sat under Waits for you, though nothing waits on him");
    }

    [Test]
    public void WhatIsUnderWay_IsSaid_AsksNothing_AndIsNotCountedAsAWait()
    {
        var file = WriteBym(_folder).File!;
        var without = ProjectOverview.For(file, BymPieces(_folder), BymLinks(), [], 5).Sentence;
        var overview = ProjectOverview.For(file, BymPieces(_folder), BymLinks(), [], 5, underway: ["Writing scenarios for five builds…"]);

        Assert.That(overview.Underway, Is.EqualTo(new[] { "Writing scenarios for five builds…" }));
        Assert.That(overview.Waits.Select(wait => wait.Text), Has.None.EqualTo("Writing scenarios for five builds…"),
            "git-22, 2026-11-24: it was counted in the Waits for you heading, though it names no action for the owner");
        Assert.That(overview.Sentence, Is.EqualTo(without));
    }

    [Test]
    public void TheUndoLine_IsNotCountedAsAThingThatWaits()
    {
        var file = WriteBym(_folder).File!;
        var without = ProjectOverview.For(file, BymPieces(_folder), BymLinks(), [], 5).Sentence;
        var with = ProjectOverview.For(file, BymPieces(_folder), BymLinks(), [], 5, clearUpNotice: "Three cards were cleared up.").Sentence;

        Assert.That(with, Is.EqualTo(without), "review of item 5, S7: the project read \"One thing waits for you\" until a restart");
    }

    [Test]
    public void ARejectedReport_NoLongerWaitsForYou()
    {
        var links = BymLinks().Select(link => link.Kind == LinkKind.ReportsBack && link.From == "w2"
            ? link with
            {
                LatestReport = new LinkReport(DateTimeOffset.UnixEpoch, "s", "report.md", "done", null),
                Rejection = new LinkRejection(DateTimeOffset.UnixEpoch, "wrong scenarios", DateTimeOffset.UnixEpoch),
            }
            : link).ToList();

        Assert.That(Bym(links, []).Waits.Where(wait => wait.Kind == WaitKind.ConfirmReport), Is.Empty,
            "git-22, 2026-11-23: rejected reports stayed in NEEDS YOU");
    }

    [Test]
    public void APiecesWaitItem_RepeatsTheWordsOfItsBoxsWhoActsLine()
    {
        var pieces = BymPieces(_folder);
        var asking = pieces.Single(piece => piece.Name == "bym-mcp-gui-search-reads");
        var wait = Bym().Waits.Single(item => item.Kind == WaitKind.Answer);

        Assert.That(wait.Text, Is.EqualTo($"{asking.Title}: {asking.Status.WhoActs}"));
    }

    [Test]
    public void AReportWaitingForConfirmation_AndAPendingProposal_WaitForYou()
    {
        var links = BymLinks().Select(link => link.Kind == LinkKind.ReportsBack && link.From == "w2"
            ? link with { LatestReport = new LinkReport(DateTimeOffset.UnixEpoch, "s", "report.md", "done", null) }
            : link).ToList();
        var proposal = new WorkstreamProposal("p1", ProposalStatus.Pending, SpecCard, "s",
            new ProposalRequest("A follow-up", "brief.md", false, null, "done", [], null), [], DateTimeOffset.UnixEpoch);
        var waits = Bym(links, [proposal]).Waits;

        Assert.That(waits.Single(wait => wait.Kind == WaitKind.ConfirmReport).Text, Does.StartWith("Config P5 refusal reported back"));
        Assert.That(waits.Single(wait => wait.Kind == WaitKind.Proposal).Text, Does.Contain("proposes a new piece of work, A follow-up"));
    }

    [Test]
    public void ACriterionWaitingOnTheOwner_IsListed_AndOneWaitingOnWorkIsNot()
    {
        var text = TestProjectsFolder.ProjectText("P", body: "## Phase 0: S\n\n### Objective 0.1: Data protection\n"
            + "- [ ] D7 recorded.\n      Waiting: owner: take D7 to the DPO\n- [ ] P6 recorded.\n      Waiting: work: write it up\n"
            + "- [ ] No note.\n\n**Pipeline:** freeform\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n");
        var file = ProjectFileReader.Read("p", text).File!;

        var waits = ProjectOverview.For(file, [], [], [], 0).Waits;

        Assert.That(waits.Select(wait => wait.Text), Is.EqualTo(new[]
        {
            "Objective 0.1, Data protection: take D7 to the DPO. This happens outside Theseus.",
        }));
        Assert.That(waits.Single().Button, Is.Null, "no button: outside Theseus");
    }

    [Test]
    public void AVerifierStoppedAtAPrompt_WaitsForTheOwner_NamingItAndWhatItAsks_WithFocus()
    {
        var file = ProjectFileReader.Read("p", TestProjectsFolder.ProjectText("P")).File!;
        var verifier = new PieceCard("verify-a", "Verify: A", "#2f80ed", SessionLifecycle.NeedsAttention, Role: WorkstreamRole.Verifier)
        {
            Prompt = new PermissionPrompt("s", "Claude needs your permission to use Bash", DateTimeOffset.UnixEpoch),
        };

        var waits = ProjectOverview.For(file, [], [], [], 0, others: [verifier, new PieceCard("verify-b", "Verify: B", "#2f80ed", SessionLifecycle.Running)]).Waits;

        Assert.That(waits.Select(wait => (wait.Kind, wait.Text, wait.CardKey)), Is.EqualTo(new[]
        {
            (WaitKind.Answer, "Verify: A needs your permission to use Bash; answer it in its tab.", (string?)"verify-a"),
        }));
    }

    [Test]
    public void ABuildAwaitingItsCheck_WhoseSessionWorksOn_IsMoving()
    {
        var reading = WriteBym(_folder);
        var pieces = BymPieces(_folder, live: false).ToList();
        var index = pieces.FindIndex(piece => piece.Status.State == StageNodeState.HoldoutsNotRun && piece.IsCurrentPiece && piece.Card is not null);
        pieces[index] = pieces[index] with { Card = pieces[index].Card! with { Lifecycle = Theseus.Core.Domain.SessionLifecycle.Running } };

        Assert.That(pieces[index].Status.State, Is.EqualTo(StageNodeState.HoldoutsNotRun));
        Assert.That(ProjectOverview.For(reading.File!, pieces, [], [], 0).Sentence, Does.Not.StartWith("Nothing is moving"),
            "its session is working, so something is moving (Nigel's capture, 2026-11-22)");
    }

    [Test]
    public void AOneRoundProject_OmitsTheRoundClause_AndNamesWhatIsMoving()
    {
        var reading = WriteTheseus(_folder);
        var pieces = TheseusPieces(_folder);

        var sentence = ProjectOverview.For(reading.File!, pieces, [], [], 0).Sentence;

        Assert.That(sentence, Does.Not.Contain("Round"));
        Assert.That(sentence, Is.EqualTo("Being built: “projects in use”; under way: “the project view made obvious (design)”. Nothing waits for you."),
            "Theseus runs the three builds' checks itself (git-22, 2026-11-23)");
    }

    [Test]
    public void ThePhaseStrip_HasOneCellPerPhase_PassedUnderWayNotStartedOrNotPlanned()
    {
        var cells = Bym().Phases;

        Assert.That(cells.Select(cell => (cell.Title, cell.Line)), Is.EqualTo(new[]
        {
            ("Phase 0: Specify", "✓ gate passed"),
            ("Phase 1: Build read-only, internal", "● under way · 0 of 10 objectives met"),
            ("Phase 2: Customer release", "not planned"),
        }));
    }

    [Test]
    public void TheTallies_CountGatesSpecsChecksReviewsAndRounds()
    {
        Assert.That(Bym().Tallies, Is.EqualTo(new[]
        {
            "one phase gate passed", "five specs accepted by you", "0 of 5 independent checks run", "five reviews written",
            "2 rounds: the requirements changed once",
        }));
    }

    [Test]
    public void EachRound_HasOneLineOfWhatItDid()
    {
        var lines = Bym().RoundLines;

        Assert.That(lines.Keys, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(lines[2], Does.StartWith("Four pieces of work: "));
        Assert.That(lines.Values, Has.None.Matches(@"(two|three|four|five|six|seven|eight) s "), "no stray plural: \"five s built\"");
    }

    private static string Words(int count) => count switch
    {
        2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", _ => count.ToString(),
    };
}
