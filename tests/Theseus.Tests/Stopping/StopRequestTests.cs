using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopRequestTests
{
    private static readonly StopTarget Target =
        new("Old name to new name", @"C:\work\HANDOFF-example.md");

    [Test]
    public void For_NamesTheFileTheSessionShouldWrite()
    {
        Assert.That(StopRequest.For(null, Target), Does.Contain(Target.HandoffPath));
    }

    [Test]
    public void For_AsksForAHandoffBeforeAnExit()
    {
        var request = StopRequest.For(null, Target);

        Assert.That(request.IndexOf("handoff", StringComparison.OrdinalIgnoreCase),
            Is.LessThan(request.IndexOf("exit", StringComparison.OrdinalIgnoreCase)),
            "a session that exits first writes nothing, which is the whole value of stopping");
    }

    [Test]
    public void For_UsesTheConfiguredWordingWhenThereIsSome()
    {
        var request = StopRequest.For("Down tools. Notes to " + StopRequest.HandoffPlaceholder + ".", Target);

        Assert.That(request, Is.EqualTo($@"Down tools. Notes to {Target.HandoffPath}."));
    }

    [Test]
    public void For_LeavesNoPlaceholderUnfilled()
    {
        Assert.That(StopRequest.For(null, Target), Does.Not.Contain(StopRequest.HandoffPlaceholder));
    }

    [Test]
    public void For_IgnoresWordingThatIsNothingButSpace()
    {
        Assert.That(StopRequest.For("   ", Target), Is.EqualTo(StopRequest.For(null, Target)));
    }

    [Test]
    public void For_AddressesTheSessionDirectlyRatherThanTalkingAboutIt()
    {
        Assert.That(StopRequest.For(null, Target), Does.Not.Contain(Target.SessionName),
            "the request is pasted into that session's own prompt, so naming it would be odd");
    }
}
