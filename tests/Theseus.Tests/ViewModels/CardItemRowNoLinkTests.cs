using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// What hovering a card's Jira or pull-request row says when it cannot be opened (card-branches pass 2, F9): the
/// reason that fits the item, and the title kept as the title.
/// </summary>
[TestFixture]
public class CardItemRowNoLinkTests
{
    [Test]
    public void APullRequestThatCouldBeInSeveralRepos_SaysSo_AndKeepsItsTitle()
    {
        var row = new CardItemRow("PR 42", "Fix the login", null, whyNoLink: "in one of api, ui");

        Assert.That(row.Tooltip, Is.EqualTo("Fix the login\nin one of api, ui"));
        Assert.That(row.Display, Is.EqualTo("PR 42   Fix the login"));
    }

    [Test]
    public void AJiraIssueWithNoAddress_StillSaysWhereToSetIt()
    {
        var row = new CardItemRow("ABC-1", null, null);

        Assert.That(row.Tooltip, Does.EndWith("Set your Jira address in Tools > Options to open it from here."));
    }
}
