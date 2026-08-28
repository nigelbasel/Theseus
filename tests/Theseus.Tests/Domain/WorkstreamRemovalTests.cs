using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class WorkstreamRemovalTests
{
    [Test]
    public void RefusalToArchive_StopsAWorkstreamWithASessionRunningFromBeingPutAway()
    {
        Assert.That(WorkstreamRemoval.RefusalToArchive(isLive: true), Does.Contain("Stop it first"),
            "the tab would go on working for a card that is no longer on the board");
    }

    [Test]
    public void RefusalToArchive_AllowsOneThatIsNotRunning()
    {
        Assert.That(WorkstreamRemoval.RefusalToArchive(isLive: false), Is.Null);
    }

    [Test]
    public void RefusalToDelete_StopsAWorkstreamWithASessionRunningFromBeingDestroyed()
    {
        Assert.That(WorkstreamRemoval.RefusalToDelete(isLive: true), Is.Not.Null);
    }

    [Test]
    public void RefusalToDelete_AllowsOneThatIsNotRunning()
    {
        Assert.That(WorkstreamRemoval.RefusalToDelete(isLive: false), Is.Null);
    }

    [Test]
    public void Consequence_SaysWhatGoesAndWhatStays()
    {
        var said = WorkstreamRemoval.Consequence("contact-forms");

        Assert.Multiple(() =>
        {
            Assert.That(said, Does.Contain("contact-forms"));
            Assert.That(said, Does.Contain("plan file"), "what is destroyed");
            Assert.That(said, Does.Contain("left alone"), "what is not");
            Assert.That(said, Does.Contain("Archiving"), "the reversible thing, offered beside the one that is not");
        });
    }
}
