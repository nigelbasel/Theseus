using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Every owner action Theseus waits on is a line of Waits for you with its button, counted (Nigel, 2026-11-26: "I dont see
/// any acccept spec notifications in the ui", "no sign of notifications to restart the breakers"), and a search for a card
/// with no box of its own lights its line.
/// </summary>
[TestFixture]
public class OwnerActionLinesTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [TestCase("[to be chosen]", SessionLifecycle.NotStarted)]
    [TestCase("[to be chosen]", SessionLifecycle.Running)]
    [TestCase("freeform", SessionLifecycle.Idle)]
    [TestCase("feature", SessionLifecycle.Running)]
    public void AWaitingSpec_IsALine_WhoseAcceptSpecOpensTheSpec_AndIsCounted(string pipeline, SessionLifecycle lifecycle)
    {
        var view = _views.One(pipeline, Files("spec.md", "plan.md", "implementation.md"), lifecycle);
        var line = view.Waits.Single(wait => wait.Item.Kind == WaitKind.AcceptSpec);

        line.Command!.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(line.Button, Is.EqualTo("Accept spec…"));
            Assert.That(_views.Ran, Is.EqualTo(new[] { $"AcceptSpec:{APiece}" }));
            Assert.That(view.Waits, Has.Count.GreaterThanOrEqualTo(1));
        });
    }

    [Test]
    public void AWritersResetBreakerLine_ResetsAndStartsThatCard()
    {
        var ran = new List<string>();
        var actions = _views.Actions with
        {
            ResetBreaker = key => ran.Add($"Reset:{key}"),
            ResetBreakerAndStart = key => ran.Add($"ResetAndStart:{key}"),
        };

        var view = _views.BuildYourMarket(actions: actions, otherWaits: _ => [ResetLine()]);
        var shown = view.Waits.Single(wait => wait.Item.Kind == WaitKind.ResetBreaker);
        shown.Command!.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shown.Button, Is.EqualTo("Reset breaker and start"));
            Assert.That(ran, Is.EqualTo(new[] { "ResetAndStart:write-scenarios" }));
        });
    }

    [Test]
    public void ACardTheOwnerMade_HasResetBreakerOnly()
    {
        var ran = new List<string>();
        var actions = _views.Actions with
        {
            ResetBreaker = key => ran.Add($"Reset:{key}"),
            ResetBreakerAndStart = key => ran.Add($"ResetAndStart:{key}"),
        };

        var view = _views.BuildYourMarket(actions: actions, otherWaits: _ => [ResetLine() with { AndStart = false, CardKey = "own" }]);
        var shown = view.Waits.Single(wait => wait.Item.Kind == WaitKind.ResetBreaker);
        shown.Command!.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shown.Button, Is.EqualTo("Reset breaker"));
            Assert.That(ran, Is.EqualTo(new[] { "Reset:own" }));
        });
    }

    [Test]
    public void ASearchFindingACardWithNoBox_LightsItsLineOfWaitsForYou()
    {
        var view = _views.BuildYourMarket(otherWaits: _ => [ResetLine()]);

        view.ShowSearch(_ => false, key => key == "write-scenarios");

        Assert.Multiple(() =>
        {
            Assert.That(view.Waits.Single(wait => wait.Item.Kind == WaitKind.ResetBreaker).IsSearchHit, Is.True);
            Assert.That(view.Waits.Where(wait => wait.Item.Kind != WaitKind.ResetBreaker).Select(wait => wait.IsSearchHit), Has.None.True);
        });

        view.ShowSearch(null);

        Assert.That(view.Waits.Select(wait => wait.IsSearchHit), Has.None.True, "clearing the search clears the line");
    }

    private static WaitItem ResetLine() =>
        new(WaitKind.ResetBreaker, "Write scenarios for BYM MCP: Breaker tripped. Reset it from the card when fixed.", [])
        {
            CardKey = "write-scenarios",
            AndStart = true,
        };
}
