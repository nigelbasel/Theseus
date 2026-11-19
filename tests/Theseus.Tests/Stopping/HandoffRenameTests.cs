using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Renaming a card's handover file to match its title (handoff-names AC 5 and 6).</summary>
[TestFixture]
public class HandoffRenameTests
{
    [Test]
    public void TheTarget_IsTheTitleAsAKey_InTheSameFolder()
    {
        Assert.That(HandoffRename.Target(@"C:\notes\HANDOFF-old-key.md", "Ledger"), Is.EqualTo(@"C:\notes\HANDOFF-ledger.md"));
    }

    [Test]
    public void AFileAlreadyMatchingItsTitle_IsNotOffered()
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-ledger.md", "Ledger", isBusy: false, isShared: false,
            fileExists: true, targetExists: false), Is.Null);
    }

    [Test]
    public void AFileNamedForAnOldKey_IsOffered_WhenNothingIsRunning()
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-old-key.md", "Ledger", isBusy: false, isShared: false,
            fileExists: true, targetExists: false), Is.EqualTo(@"C:\notes\HANDOFF-ledger.md"));
    }

    [TestCase(true, false, true, false, Description = "a running or starting session holds the old path")]
    [TestCase(false, true, true, false, Description = "another card uses the file")]
    [TestCase(false, false, false, false, Description = "no file to move")]
    [TestCase(false, false, true, true, Description = "the new name is taken")]
    public void ARenameIsNotOffered_WhenItCouldNotBeDoneSafely(bool isBusy, bool isShared, bool fileExists, bool targetExists)
    {
        Assert.That(HandoffRename.Offered(@"C:\notes\HANDOFF-old-key.md", "Ledger", isBusy, isShared, fileExists, targetExists),
            Is.Null);
    }

    [Test]
    public void Apply_MovesTheFile_AndLeavesNothingAtTheOldName()
    {
        var folder = Directory.CreateTempSubdirectory("handoff-rename-").FullName;

        try
        {
            var current = Path.Combine(folder, "HANDOFF-old-key.md");
            var target = Path.Combine(folder, "HANDOFF-ledger.md");
            File.WriteAllText(current, "notes");

            HandoffRename.Apply(current, target);

            Assert.That(File.Exists(current), Is.False);
            Assert.That(File.ReadAllText(target), Is.EqualTo("notes"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void Apply_RefusesToReplaceAFileAtTheNewName()
    {
        var folder = Directory.CreateTempSubdirectory("handoff-rename-").FullName;

        try
        {
            var current = Path.Combine(folder, "HANDOFF-old-key.md");
            var target = Path.Combine(folder, "HANDOFF-ledger.md");
            File.WriteAllText(current, "mine");
            File.WriteAllText(target, "somebody else's");

            Assert.Throws<IOException>(() => HandoffRename.Apply(current, target));
            Assert.That(File.ReadAllText(target), Is.EqualTo("somebody else's"));
            Assert.That(File.ReadAllText(current), Is.EqualTo("mine"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
