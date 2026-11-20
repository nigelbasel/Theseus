using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>The project card's waiting count (project-view AC 12): one line for each thing waiting on the owner.</summary>
[TestFixture]
public class ProjectWaitingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void EachKindOfWaiting_IsCounted_ForTheProjectsCardsOnly()
    {
        var waiting = new WorkstreamCardViewModel(Card("member", "p", "a-slug")) { Lifecycle = SessionLifecycle.NeedsAttention };
        var quiet = new WorkstreamCardViewModel(Card("quiet", "p", "b-slug"));
        var spec = new SpecAtGate("p", "b-slug", "hash", "# Spec", WorkstreamSource.None);

        var lines = ProjectWaiting.For([waiting, quiet],
            [
                new Link("1", LinkKind.ReportsBack, "quiet", "elsewhere", Now, Link.MadeOnTheBoard),
                new Link("2", LinkKind.ReportsBack, "outsider", "stranger", Now, Link.MadeOnTheBoard),
            ],
            [("member", "A spike"), ("outsider", "Not ours")],
            [spec]);

        Assert.That(lines, Is.EqualTo(new[]
        {
            "member is waiting for your input",
            "A report from quiet waits for your confirmation",
            "member proposes A spike",
            "The spec of b-slug waits for you to accept it",
        }));
    }

    [Test]
    public void NothingWaiting_IsAnEmptyCount_AndSaysSo()
    {
        var card = new ProjectCardViewModel("p", "P") { Waiting = ProjectWaiting.For([], [], [], []) };

        Assert.That((card.WaitingCount, card.HasWaiting, card.WaitingText), Is.EqualTo((0, false, "Nothing is waiting on you")));
    }
}
