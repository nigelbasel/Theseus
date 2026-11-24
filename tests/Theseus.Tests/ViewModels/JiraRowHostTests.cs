using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A card's Jira row says where it opens, naming the host of the configured Jira address and never one written into
/// the product (baseline WB7; finding C22; W54; checklist G156).
/// </summary>
[TestFixture]
public class JiraRowHostTests
{
    [Test]
    public void AJiraRow_NamesTheHostOfTheConfiguredAddress()
    {
        CardItemRow row = CardItemRows.Jira(["ABC-1"], [], "https://example.atlassian.net").Single();

        Assert.That(row.Tooltip, Does.EndWith("Open ABC-1 in Jira at example.atlassian.net"));
    }

    [Test]
    public void WithNoAddress_ItStillSaysWhereToSetIt()
    {
        CardItemRow row = CardItemRows.Jira(["ABC-1"], [], null).Single();

        Assert.That(row.Tooltip, Does.EndWith("Set your Jira address in Tools > Options to open it from here."));
    }
}
