using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A box's "Show on the board" went to the board and showed nothing, because a project's cards are not drawn there (Nigel,
/// 2026-11-23). It now opens the card's details over the view, at Overview, where Start, Stop and Focus are.
/// </summary>
[TestFixture]
public class ShowTheCardTests
{
    [Test]
    public void ShowTheCard_OpensTheCardsDetails_AndLeavesTheProjectOpen()
    {
        var card = new WorkstreamCardViewModel(ProjectCards.Card(ProjectViewFixtures.Git22));
        var opened = new List<string>();

        var action = ShowTheCard.For(card, shown => opened.Add(shown.Key));
        action.Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(action.Label, Is.EqualTo("Show the card"));
            Assert.That(action.ToolTip, Does.Contain(card.Title).And.Contain("Start, Stop and Focus"));
            Assert.That(opened, Is.EqualTo(new[] { card.Key }));
        });
    }
}
