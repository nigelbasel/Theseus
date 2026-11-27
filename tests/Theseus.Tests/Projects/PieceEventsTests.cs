using System.Text.Json.Nodes;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Mcp;
using Theseus.Core.Persistence;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// 7.3: piece-events.jsonl records every now_at stage change including skips, every criterion tick with its time and who
/// made it, spec acceptance, and finish or abandon.
/// </summary>
[TestFixture]
public class PieceEventsTests
{
    private const string Project = "p";

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private TestProjectsFolder _folder = null!;
    private PieceIdBook _ids = null!;
    private PieceEvents _events = null!;
    private ProjectTools _tools = null!;

    private static string Text =>
        TestProjectsFolder.ProjectText("P", body: "## Phase 0: Start\n\n"
            + "### Objective 0.1: Build it\n- [ ] One.\n- [ ] Two.\n\n**Pipeline:** feature\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| card | build-it | owner/build-it | r |\n");

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _folder.Write($"{Project}/project.md", Text);
        _ids = new PieceIdBook(Path.Combine(_folder.Folder, "piece-ids.json"), () => "piece-1");
        _ids.Update(ProjectCatalog.Read(_folder.Folder));
        _events = new PieceEvents(new JsonLinesFile<PieceEvent>(Path.Combine(_folder.Folder, "piece-events.jsonl")), _ids, MemberOf,
            card => card is "card" or "verify-card" ? "piece-1" : null, () => Now, TimeZoneInfo.Utc);
        _tools = new ProjectTools(MemberOf, name => ProjectCatalog.Read(_folder.Folder).SingleOrDefault(reading => reading.Name == name),
            new StageReportBook(Path.Combine(_folder.Folder, "stage-skips.json")), () => Now, TimeZoneInfo.Utc, pieceEvents: _events);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void NowAt_RecordsTheStageItEnters_WithWhoAndWhen()
    {
        _tools.Call("now_at", Session(), new JsonObject { ["stage"] = "Build", ["doing"] = "writing the reader" });

        Assert.That(_events.Read().Single(), Is.EqualTo(new PieceEvent(Now, PieceEventKind.Stage, Project, "piece-1", "build-it", "card")
        {
            Stage = nameof(PipelineStage.Implement),
            Detail = "writing the reader",
            Offset = "+00:00",
        }));
    }

    [Test]
    public void NowAt_RecordsASkip_WithItsReason()
    {
        _tools.Call("now_at", Session(), new JsonObject { ["stage"] = "Research", ["doing"] = "skipped: the spec covers it" });

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.StageSkipped, Stage: "Research", Detail: "the spec covers it" }, Is.True);
    }

    [Test]
    public void TickCriterion_RecordsTheTick_AndTheSessionThatMadeIt()
    {
        _tools.Call("tick_criterion", Session(),
            new JsonObject { ["objective"] = "0.1", ["criterion"] = 2, ["text"] = "Two.", ["evidence"] = "tests" });

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.CriterionTicked, By: "card", Objective: "0.1", Criterion: 2, PieceId: "piece-1" },
            Is.True);
    }

    [Test]
    public void Criterion_ByTheOwner_SaysSo()
    {
        _events.Criterion(Project, "0.1", 0, ticked: false, byCard: null);

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.CriterionUnticked, By: PieceEvent.Owner, Criterion: 1 }, Is.True);
    }

    [Test]
    public void SpecAccepted_IsRecordedAgainstThePiece()
    {
        _events.SpecAccepted(Project, "build-it");

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.SpecAccepted, PieceId: "piece-1", By: PieceEvent.Owner }, Is.True);
    }

    [Test]
    public void ReportConfirmed_FinishesThePiece_WhenItsVerifiersPassingReportIsConfirmed()
    {
        var link = Report("verify-card", "card", "done");

        _events.ReportConfirmed(link, Card("verify-card") with { Role = WorkstreamRole.Verifier });

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.Finished, PieceId: "piece-1", Card: "verify-card", Verdict: "done" }, Is.True);
    }

    [Test]
    public void ReportConfirmed_DoesNotFinishThePiece_OnAPartialReport()
    {
        _events.ReportConfirmed(Report("card", "lead", "partial"), Card("card"));

        Assert.That(_events.Read().Single().Kind, Is.EqualTo(PieceEventKind.ReportConfirmed));
    }

    [Test]
    public void Watch_RecordsAPieceAbandoned_WhenItsCardIsArchivedBeforeItFinished()
    {
        _events.Watch([Card("card")]);
        _events.Watch([Card("card") with { IsArchived = true }]);

        Assert.That(_events.Read().Single() is { Kind: PieceEventKind.Abandoned, PieceId: "piece-1" }, Is.True);
    }

    [Test]
    public void Watch_RecordsNothing_ForAFinishedPieceArchived_OrForCardsArchivedBeforeTheFirstLook()
    {
        _events.Watch([Card("card") with { IsArchived = true }]);
        _events.Watch([Card("card") with { IsArchived = true }]);

        Assert.That(_events.Read(), Is.Empty, "the first look only learns what is archived already");

        _events.ReportConfirmed(Report("verify-card", "card", "done"), Card("verify-card") with { Role = WorkstreamRole.Verifier });
        _events.Watch([Card("card")]);
        _events.Watch([Card("card") with { IsArchived = true }]);

        Assert.That(_events.Read().Select(each => each.Kind), Is.EqualTo(new[] { PieceEventKind.Finished }));
    }

    private static ProjectMember? MemberOf(string card) => card == "card" ? new ProjectMember(Project, "build-it") : null;

    private static McpSession Session() => new("s1", "card", "Card");

    private static Link Report(string from, string to, string verdict) =>
        new("lk-1", LinkKind.ReportsBack, from, to, Now, Link.MadeOnTheBoard, "REPORT.md",
            LatestReport: new LinkReport(Now, "s9", "REPORT.md", "summary", verdict));

    private static Workstream Card(string key) =>
        new(key, key, "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.", 1, "New",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, null, [], null, DateTimeOffset.UtcNow);
}
