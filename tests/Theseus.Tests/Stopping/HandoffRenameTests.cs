using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Renaming a card's handover file to match its title (handoff-names AC 5).</summary>
[TestFixture]
public class HandoffRenameTests
{
    [Test]
    public void TheTarget_IsTheTitleAsAKey_InTheSameFolder()
    {
        Assert.That(HandoffRename.Target(@"C:\notes\HANDOFF-git-22.md", "Theseus"), Is.EqualTo(@"C:\notes\HANDOFF-theseus.md"));
    }

    [Test]
    public void AFileAlreadyMatchingItsTitle_IsNotOffered()
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-theseus.md", "Theseus", isLive: false, fileExists: true,
            targetExists: false), Is.Null);
    }

    [Test]
    public void AFileNamedForAnOldKey_IsOffered_WhenNothingIsRunning()
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-git-22.md", "Theseus", isLive: false, fileExists: true,
            targetExists: false), Is.EqualTo(@"C:\notes\HANDOFF-theseus.md"));
    }

    [TestCase(true, true, false, Description = "a running session holds the old path")]
    [TestCase(false, false, false, Description = "no file to move")]
    [TestCase(false, true, true, Description = "the new name is taken")]
    public void ARenameIsNotOffered_WhenItCouldNotBeDoneSafely(bool isLive, bool fileExists, bool targetExists)
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-git-22.md", "Theseus", isLive, fileExists, targetExists),
            Is.Null);
    }
}
