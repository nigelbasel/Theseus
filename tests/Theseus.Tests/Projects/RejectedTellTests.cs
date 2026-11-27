using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// What the card of a rejected spec is told (board-small-changes item 4): the version, that it waits again, and the
/// owner's note when he gave one.
/// </summary>
[TestFixture]
public class RejectedTellTests
{
    private static SpecAtGate Spec(bool rule = false) =>
        new("bym", "w7", SpecIdentity.Hash("v2"), "v2", new WorkstreamSource(@"<projects>\bym\work\w7")) { IsRule = rule };

    [Test]
    public void WithANote_ItSaysTheVersionWasRejected_AndGivesTheNote() =>
        Assert.That(RejectedTell.Text(Spec(), "Split criterion 2."), Is.EqualTo(
            $"Your spec (version {Spec().ShortVersion}) was rejected: change it as the owner says, then ask him to accept it again. "
            + "His note: Split criterion 2."));

    [Test]
    public void WithNoNote_ItSaysToAskWhatToChange() =>
        Assert.That(RejectedTell.Text(Spec(), null), Is.EqualTo(
            $"Your spec (version {Spec().ShortVersion}) was rejected: change it as the owner says, then ask him to accept it again. "
            + "He left no note, so ask him what to change."));

    [Test]
    public void ARule_IsCalledARule() =>
        Assert.That(RejectedTell.Text(Spec(rule: true), null), Does.StartWith("Your rule"));
}
