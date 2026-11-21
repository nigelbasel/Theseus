using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// Starting and stopping a whole project (1.1d AC 43; Nigel's answer to question 4, 2026-11-21): Start lists each of its
/// cards, ticked when it can start, paused ones unticked, running, blocked, done and waiting ones greyed with the reason
/// and never ticked; with nothing to start it says why. Stop asks each card with a session open.
/// </summary>
[TestFixture]
public class ProjectStartTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private IReadOnlyList<PieceOfWork> BuildYourMarketWith(Func<PieceCard, PieceCard> change) =>
        BuildYourMarketPieces(_folder, [.. BuildYourMarketBoard().Select(change)]);

    private static PieceCard Shaped(PieceCard card) => card.Key switch
    {
        "bym-mcp-w4-mcp-host-and-staff-plugin" => card with { Lifecycle = SessionLifecycle.Running },
        "bym-mcp-w5-infra-hygiene-scripts-and-pipelines" => card with { IsBlocked = true },
        "bym-mcp-w2-config-p5-refusal" => card with { IsPaused = true },
        _ => card,
    };

    [Test]
    public void Start_ListsEveryCard_TickedWhenItCanStart_PausedUnticked_RunningBlockedAndDoneGreyedWithTheReason()
    {
        var start = ProjectStart.For("BuildYourMarket MCP server", BuildYourMarketWith(Shaped));

        Assert.That(start.Entries.Select(entry => (entry.Line, entry.CanStart, entry.IsTicked)), Is.EqualTo(new[]
        {
            ($"{ServerSpec}: is done", false, false),
            ("bym-mcp-w0-preconditions-inventory", true, true),
            ("bym-mcp-w1-identityserver-resources-and-staff-client", true, true),
            ("bym-mcp-w2-config-p5-refusal: is paused", true, false),
            ("bym-mcp-w3-build-read-api", true, true),
            ("bym-mcp-w4-mcp-host-and-staff-plugin: is running", false, false),
            ("bym-mcp-w5-infra-hygiene-scripts-and-pipelines: is blocked", false, false),
            (RedirectAnchoring, true, true),
            (ClientFixes, true, true),
        }), "linked cards no project file lists come after the listed ones, in link order (PR 1 AC 13)");
        Assert.That((start.CanStart, start.ToolTip), Is.EqualTo((true, "Start BuildYourMarket MCP server: asks first, listing its cards")));
    }

    [Test]
    public void WithNothingToStart_StartIsDisabled_SayingWhyForEachCard()
    {
        var start = ProjectStart.For("Theseus bootstrap", TheseusPieces(_folder, SessionLifecycle.Running));

        Assert.That(start.CanStart, Is.False);
        Assert.That(start.ToolTip, Is.EqualTo("No card of Theseus bootstrap can start: git-22 is running"));
    }

    [Test]
    public void ACardWhoseCurrentPieceWaitsOnYou_IsGreyed_AndACardThatAsks_SaysSo()
    {
        Assert.That(ProjectStart.For("Theseus bootstrap", TheseusPieces(_folder, SessionLifecycle.NotStarted)).Entries.Single().Line,
            Is.EqualTo("git-22: is waiting on you"), "its current piece's spec waits to be accepted");
        Assert.That(ProjectStart.For("Theseus bootstrap", TheseusPieces(_folder, SessionLifecycle.NeedsAttention)).Entries.Single().Line,
            Is.EqualTo("git-22: is waiting for your answer"));
    }

    [Test]
    public void ACardThatStartWouldRefuse_IsGreyed_EvenWithNoSession()
    {
        var start = ProjectStart.For("BuildYourMarket MCP server",
            BuildYourMarketWith(card => card.Key == ClientFixes ? card with { CanStart = false } : card));

        Assert.That(start.Entries.Single(entry => entry.Key == ClientFixes).Line, Is.EqualTo($"{ClientFixes}: cannot be started from the board"));
    }

    [Test]
    public void Stop_AsksEachCardWithASessionOpen_AndWithNoneIsDisabledSayingWhy()
    {
        var stop = ProjectStop.For("BuildYourMarket MCP server", BuildYourMarketWith(card =>
            card.Key is "bym-mcp-w4-mcp-host-and-staff-plugin" or ClientFixes ? card with { Lifecycle = SessionLifecycle.Idle } : card));

        Assert.That(stop.Keys, Is.EqualTo(new[] { "bym-mcp-w4-mcp-host-and-staff-plugin", ClientFixes }));
        Assert.That(stop.ToolTip, Is.EqualTo(
            $"Stop BuildYourMarket MCP server: ask bym-mcp-w4-mcp-host-and-staff-plugin, {ClientFixes} to write a handover and exit"));

        var none = ProjectStop.For("Theseus bootstrap", TheseusPieces(_folder, SessionLifecycle.Ended));

        Assert.That((none.CanStop, none.ToolTip), Is.EqualTo((false, "No card of Theseus bootstrap is running")));
    }
}
