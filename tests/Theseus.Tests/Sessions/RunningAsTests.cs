using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class RunningAsTests
{
    [Test]
    public void For_SaysNothingWhenTheCardAndItsSessionAgree()
    {
        Assert.That(RunningAs.For("Contact forms", "Contact forms"), Is.Empty);
    }

    [Test]
    public void For_ExplainsTheOldNameWhenAWorkstreamHasBeenRenamed()
    {
        var said = RunningAs.For("Contact forms", "Contact form validation");

        Assert.That(said, Does.Contain("Contact form validation"));
        Assert.That(said, Does.Contain("restarted"), "the note has to say what would fix it");
    }

    [Test]
    public void For_IgnoresTheStatusGlyphASessionPutsInFrontOfItsName()
    {
        Assert.That(RunningAs.For("Contact forms", "◐ Contact forms"), Is.Empty,
            "a busy marker is not a rename, and a card that claimed it was would never stop saying so");
    }

    [Test]
    public void For_IgnoresACaseChangeAlone()
    {
        Assert.That(RunningAs.For("Contact Forms", "contact forms"), Is.Empty);
    }

    [Test]
    public void For_SaysNothingWhenEitherNameIsMissing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RunningAs.For(null, "Contact forms"), Is.Empty);
            Assert.That(RunningAs.For("Contact forms", null), Is.Empty);
            Assert.That(RunningAs.For("   ", "   "), Is.Empty);
        });
    }
}
