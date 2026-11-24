using Theseus.Core.Running;

namespace Theseus.Tests.Running;

[TestFixture]
public class RunOutputTextTests
{
    [Test]
    public void AToolCall_ReadsAsItsCommand()
    {
        Assert.That(RunOutputText.Render(StreamLines.Tool("m", "git log -1")), Is.EqualTo("Bash: git log -1"));
    }

    [Test]
    public void ATurnsEnd_ReadsWithItsCostSoFar()
    {
        Assert.That(RunOutputText.Render(StreamLines.Result(0.42m)), Does.StartWith("Turn ended: success · $0.42 so far"));
    }

    [Test]
    public void ALineNotShown_IsNull()
    {
        Assert.That(RunOutputText.Render(StreamLines.Hook), Is.Null);
    }
}
