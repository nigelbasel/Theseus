using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraKeyDisplayTests
{
    // The card that prompted this: sixteen issues on one workstream.
    private static readonly string[] Many =
    [
        "APPS-2170", "NDC-202", "REQ-06", "NDC-200", "NDC-201", "NDC-639", "NDC-653", "NDC-654",
        "NDC-655", "NDC-656", "NDC-662", "NDC-663", "NDC-664", "NDC-665", "NDC-667", "NDC-669",
    ];

    [Test]
    public void Visible_ShowsOnlyAsManyAsACardCanHoldWithoutGrowing()
    {
        Assert.That(JiraKeyDisplay.Visible(Many), Has.Count.EqualTo(JiraKeyDisplay.Limit));
    }

    [Test]
    public void Visible_KeepsTheOrderTheyAreIn()
    {
        Assert.That(JiraKeyDisplay.Visible(Many)[0], Is.EqualTo("APPS-2170"),
            "the first is the one the work started from, and is the one worth seeing");
    }

    [Test]
    public void Visible_ShowsEveryKeyWhenTheyAllFit()
    {
        Assert.That(JiraKeyDisplay.Visible(["NDC-1", "NDC-2"]), Has.Count.EqualTo(2));
    }

    [Test]
    public void More_CountsWhatIsNotShown()
    {
        Assert.That(JiraKeyDisplay.More(Many), Is.EqualTo($"+{Many.Length - JiraKeyDisplay.Limit} more"));
    }

    [Test]
    public void More_SaysNothingWhenEveryKeyIsOnTheCard()
    {
        Assert.That(JiraKeyDisplay.More(["NDC-1", "NDC-2"]), Is.Empty);
    }

    [Test]
    public void More_SaysNothingForExactlyTheLimit()
    {
        Assert.That(JiraKeyDisplay.More([.. Many.Take(JiraKeyDisplay.Limit)]), Is.Empty,
            "a card that fits should not claim there is more to see");
    }

    [Test]
    public void All_NamesEveryKeySoHidingSomeDoesNotLoseThem()
    {
        var all = JiraKeyDisplay.All(Many);

        Assert.That(all, Does.Contain("NDC-669"), "the last one is reachable from the tooltip");
        Assert.That(all, Does.Contain("APPS-2170"));
    }

    [Test]
    public void All_CopesWithAWorkstreamThatNamesNone()
    {
        Assert.That(JiraKeyDisplay.All([]), Is.Empty);
        Assert.That(JiraKeyDisplay.All(null), Is.Empty);
    }

    [Test]
    public void Visible_CopesWithNothingAtAll()
    {
        Assert.That(JiraKeyDisplay.Visible(null), Is.Empty);
        Assert.That(JiraKeyDisplay.More(null), Is.Empty);
    }
}
