using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Whose a shared handover file is: the one card it is named exactly after (handoff-names AC 1).</summary>
[TestFixture]
public class HandoffOwnerTests
{
    [Test]
    public void TheCardTheFileIsNamedAfterByKey_OwnsIt_EvenWhenAnotherKeyIsInsideTheName()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-configure-the-widget-service-in-the-catalogue.md",
        [
            ("configure-the-widget-service-in-the-catalogue", "Configure the widget service in the catalogue"),
            ("widget-service", "Widget service"),
        ]), Is.EqualTo("configure-the-widget-service-in-the-catalogue"));
    }

    [Test]
    public void ACardNamedByItsTitle_OwnsIt()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-ledger.md", [("old-key", "Ledger"), ("other", "Other work")]),
            Is.EqualTo("old-key"));
    }

    [Test]
    public void TheKeyAlone_IsEnough_WhenTheTitleDiffers()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-old-key.md", [("old-key", "Ledger"), ("other", "Other work")]),
            Is.EqualTo("old-key"));
    }

    [Test]
    public void AKeyThatIsOnlyPartOfTheName_DoesNotOwnIt()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-latest.md", [("test", "Test"), ("other", "Other")]), Is.Null,
            "a short key inside a longer name is not the file's name");
    }

    [Test]
    public void AFileThatNamesNoSharer_HasNoOwner()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF.md", [("one", "One"), ("two", "Two")]), Is.Null);
    }

    [Test]
    public void TwoSharersWithTheSameName_LeaveItWithoutAnOwner()
    {
        Assert.That(HandoffOwner.Of(@"C:\notes\HANDOFF-alpha.md", [("alpha", "Beta"), ("beta", "Alpha")]), Is.Null);
    }
}
