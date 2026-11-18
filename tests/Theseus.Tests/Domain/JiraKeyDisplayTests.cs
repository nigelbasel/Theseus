using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraKeyDisplayTests
{
    // The card that prompted this: sixteen issues on one workstream.
    private static readonly string[] Many =
    [
        "MOTH-2170", "KITE-202", "REQ-06", "KITE-200", "KITE-201", "KITE-639", "KITE-653", "KITE-654",
        "KITE-655", "KITE-656", "KITE-662", "KITE-663", "KITE-664", "KITE-665", "KITE-667", "KITE-669",
    ];

    [Test]
    public void Visible_ShowsOnlyAsManyAsACardCanHoldWithoutGrowing()
    {
        Assert.That(JiraKeyDisplay.Visible(Many), Has.Count.EqualTo(JiraKeyDisplay.Limit));
    }

    [Test]
    public void Visible_KeepsTheOrderTheyAreIn()
    {
        Assert.That(JiraKeyDisplay.Visible(Many)[0], Is.EqualTo("MOTH-2170"),
            "the first is the one the work started from, and is the one worth seeing");
    }

    [Test]
    public void Visible_ShowsEveryKeyWhenTheyAllFit()
    {
        Assert.That(JiraKeyDisplay.Visible(["KITE-1", "KITE-2"]), Has.Count.EqualTo(2));
    }

    [Test]
    public void More_CountsWhatIsNotShown()
    {
        Assert.That(JiraKeyDisplay.More(Many), Is.EqualTo($"+{Many.Length - JiraKeyDisplay.Limit} more"));
    }

    [Test]
    public void More_SaysNothingWhenEveryKeyIsOnTheCard()
    {
        Assert.That(JiraKeyDisplay.More(["KITE-1", "KITE-2"]), Is.Empty);
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

        Assert.That(all, Does.Contain("KITE-669"), "the last one is reachable from the tooltip");
        Assert.That(all, Does.Contain("MOTH-2170"));
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
