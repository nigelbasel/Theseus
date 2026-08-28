using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraKeySelectionTests
{
    [Test]
    public void Visible_ShowsAnIssueTheSessionRaisedAfterTheWorkstreamWasCreated()
    {
        var keys = JiraKeySelection.Visible(["NDC-742"], "Look at the support case.", null, null);

        Assert.That(keys, Is.EqualTo(new[] { "NDC-742" }),
            "the prompt was written before the issue existed, so nothing else can know about it");
    }

    [Test]
    public void Visible_CombinesThePromptTheSessionAndTheBoardRatherThanRankingThem()
    {
        var keys = JiraKeySelection.Visible(["NDC-742"], "Continue APPS-101.", ["NDC-9"], null);

        Assert.That(keys, Is.EquivalentTo(new[] { "APPS-101", "NDC-742", "NDC-9" }));
    }

    [Test]
    public void Visible_KeepsAKeyOffTheCardOnceItHasBeenTakenOff()
    {
        var keys = JiraKeySelection.Visible(["NDC-742"], "Continue APPS-101.", null, ["NDC-742"]);

        Assert.That(keys, Is.EqualTo(new[] { "APPS-101" }),
            "the session goes on naming it, so without a record of the removal it would come back");
    }

    [Test]
    public void Visible_SaysEachKeyOnceHoweverManyPlacesNameIt()
    {
        var keys = JiraKeySelection.Visible(["APPS-101"], "Continue APPS-101.", ["APPS-101"], null);

        Assert.That(keys, Has.Count.EqualTo(1));
    }

    [Test]
    public void Visible_CopesWithAWorkstreamNobodyHasSaidAnythingAbout()
    {
        Assert.That(JiraKeySelection.Visible(null, null, null, null), Is.Empty);
    }

    [Test]
    public void Removed_RemembersWhatWasTakenOffTheCard()
    {
        var removed = JiraKeySelection.Removed(["APPS-101", "NDC-742"], ["APPS-101"], null);

        Assert.That(removed, Is.EqualTo(new[] { "NDC-742" }));
    }

    [Test]
    public void Removed_ForgetsAKeyThatHasBeenPutBack()
    {
        var removed = JiraKeySelection.Removed(["APPS-101"], ["APPS-101", "NDC-742"], ["NDC-742"]);

        Assert.That(removed, Is.Empty,
            "adding a key by hand undoes having taken it away, without anybody needing to know that");
    }

    [Test]
    public void Removed_KeepsWhatWasAlreadyDismissed()
    {
        var removed = JiraKeySelection.Removed(["APPS-101"], ["APPS-101"], ["NDC-742"]);

        Assert.That(removed, Is.EqualTo(new[] { "NDC-742" }));
    }
}
