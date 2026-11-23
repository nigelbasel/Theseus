using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>A project card's badge: the count of what asks something of the owner, with the lines in its tooltip.</summary>
[TestFixture]
public class ProjectCardWaitingTests
{
    [Test]
    public void NothingWaiting_IsAnEmptyCount_AndSaysSo()
    {
        var card = new ProjectCardViewModel("p", "P") { Waiting = [] };

        Assert.That((card.WaitingCount, card.HasWaiting, card.WaitingText), Is.EqualTo((0, false, "Nothing is waiting on you")));
    }

    [Test]
    public void TheBadge_CountsItsLines_AndListsThemInItsTooltip()
    {
        var card = new ProjectCardViewModel("p", "P") { Waiting = ["A report waits", "A spec waits"] };

        Assert.That((card.CountText, card.WaitingText), Is.EqualTo(("2", $"A report waits{Environment.NewLine}A spec waits")));
    }
}
