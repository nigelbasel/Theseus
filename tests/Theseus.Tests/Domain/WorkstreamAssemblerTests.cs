using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class WorkstreamAssemblerTests
{
    [Test]
    public void Assemble_TakesTitleColourAndPromptFromThePlanBecauseTheSessionJustWroteIt()
    {
        var workstream = Single(Plan(title: "Contact form validation", color: "#C0392B"), sidecar: null);

        Assert.That(workstream.Title, Is.EqualTo("Contact form validation"));
        Assert.That(workstream.ColorHex, Is.EqualTo("#C0392B"));
        Assert.That(workstream.Prompt, Does.Contain("PROJ-101"));
    }

    [Test]
    public void Assemble_TreatsAnExistingColourAsManualSoMuscleMemoryIsNeverReallocated()
    {
        Assert.That(Single(Plan(), sidecar: null).ColorIsManual, Is.True);
    }

    [Test]
    public void Assemble_StartsAWorkstreamWithNoSidecarInTodayAndOpeningTomorrow()
    {
        var workstream = Single(Plan(), sidecar: null);

        Assert.That(workstream.Column, Is.EqualTo(BoardColumn.Today));
        Assert.That(workstream.ReopenTomorrow, Is.True);
        Assert.That(workstream.IsArchived, Is.False);
        Assert.That(workstream.IsBlocked, Is.False);
    }

    [Test]
    public void Assemble_TakesBoardStateFromTheSidecarBecauseThePlanFormatHasNoRoomForIt()
    {
        var workstream = Single(Plan(), Sidecar() with
        {
            Column = BoardColumn.Parked,
            IsBlocked = true,
            ReopenTomorrow = false,
            StatusDocumentPath = @"C:\dev\SESSION-STATUS-contact-forms.md",
        });

        Assert.That(workstream.Column, Is.EqualTo(BoardColumn.Parked));
        Assert.That(workstream.IsBlocked, Is.True);
        Assert.That(workstream.ReopenTomorrow, Is.False);
        Assert.That(workstream.StatusDocumentPath, Is.EqualTo(@"C:\dev\SESSION-STATUS-contact-forms.md"));
    }

    [Test]
    public void Assemble_HarvestsEveryJiraKeyFromThePromptWhenTheSidecarHasNone()
    {
        Assert.That(Single(Plan(), sidecar: null).JiraKeys, Is.EqualTo(new[] { "PROJ-101", "API-202" }));
    }

    [Test]
    public void Assemble_KeepsConfirmedJiraKeysRatherThanReharvestingThemEveryRead()
    {
        var workstream = Single(Plan(), Sidecar() with { JiraKeys = ["PROJ-999"] });

        Assert.That(workstream.JiraKeys, Is.EqualTo(new[] { "PROJ-999" }));
    }

    [Test]
    public void Assemble_PrefersThePlansResumeIdBecauseThePlanIsWrittenLast()
    {
        var workstream = Single(
            Plan() with { ResumeSessionId = "from-plan" },
            Sidecar() with { LastSessionId = "from-sidecar" });

        Assert.That(workstream.LastSessionId, Is.EqualTo("from-plan"));
    }

    [Test]
    public void Assemble_FallsBackToTheSidecarsSessionWhenThePlanNamesNone()
    {
        var workstream = Single(Plan(), Sidecar() with { LastSessionId = "from-sidecar" });

        Assert.That(workstream.LastSessionId, Is.EqualTo("from-sidecar"));
    }

    [Test]
    public void Assemble_OrdersByColumnThenByThePlansOwnLaunchOrder()
    {
        var plans = new Dictionary<string, SessionPlan>
        {
            ["admin-console"] = Plan() with { Order = 4 },
            ["contact-forms"] = Plan() with { Order = 1 },
            ["job-queue"] = Plan() with { Order = 2 },
        };

        var sidecars = new[] { Sidecar() with { Key = "contact-forms", Column = BoardColumn.Done } };

        Assert.That(WorkstreamAssembler.Assemble(plans, sidecars).Select(w => w.Key),
            Is.EqualTo(new[] { "job-queue", "admin-console", "contact-forms" }));
    }

    [Test]
    public void Assemble_MatchesSidecarsToPlansCaseInsensitivelyLikeTheFileSystemDoes()
    {
        var workstream = Single(Plan(), Sidecar() with { Key = "CONTACT-FORMS", Column = BoardColumn.Done });

        Assert.That(workstream.Column, Is.EqualTo(BoardColumn.Done));
    }

    [Test]
    public void Assemble_IgnoresASidecarWhosePlanHasBeenDeleted()
    {
        var workstreams = WorkstreamAssembler.Assemble(
            new Dictionary<string, SessionPlan>(),
            [Sidecar() with { Key = "deleted-workstream" }]);

        Assert.That(workstreams, Is.Empty);
    }

    [Test]
    public void ParseWrittenAt_ReadsTheFormatTheSessionsActuallyWrite()
    {
        var written = WorkstreamAssembler.ParseWrittenAt("2026-08-25T21:20:33");

        Assert.That(written.Year, Is.EqualTo(2026));
        Assert.That(written.Hour, Is.EqualTo(21));
        Assert.That(written.Minute, Is.EqualTo(20));
    }

    [Test]
    public void ParseWrittenAt_SortsAnUnreadableTimestampToTheStartRatherThanThrowing()
    {
        Assert.That(WorkstreamAssembler.ParseWrittenAt("sometime yesterday"),
            Is.EqualTo(DateTimeOffset.MinValue));
    }

    private static Workstream Single(SessionPlan plan, WorkstreamSidecar? sidecar) =>
        WorkstreamAssembler.Assemble(
            new Dictionary<string, SessionPlan> { ["contact-forms"] = plan },
            sidecar is null ? [] : [sidecar]).Single();

    private static SessionPlan Plan(string title = "Contact form validation", string color = "#C0392B") =>
        new(1, title, color, @"C:\work\repo", [@"C:\work\repo\features\contact-forms"], null,
            "2026-08-25T21:20:33", "Pick up the GDPR work (PROJ-101 / API-202). PROJ-101 again.");

    private static WorkstreamSidecar Sidecar() =>
        new("contact-forms", BoardColumn.Today, IsBlocked: false, IsArchived: false, ColorIsManual: true,
            ReopenTomorrow: true, [], null, null,
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero));
}
