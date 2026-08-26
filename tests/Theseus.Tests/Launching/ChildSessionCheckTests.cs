using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class ChildSessionCheckTests
{
    [Test]
    public void Warning_IsSilentWhenTheEnvironmentIsClean()
    {
        Assert.That(ChildSessionCheck.Warning(_ => null), Is.Null);
    }

    [TestCase("CLAUDE_CODE_CHILD_SESSION")]
    [TestCase("CLAUDECODE")]
    public void Warning_SpeaksUpWhenStartedFromInsideASession(string marker)
    {
        var warning = ChildSessionCheck.Warning(name => name == marker ? "1" : null);

        Assert.That(warning, Is.Not.Null);
        Assert.That(warning, Does.Contain("cannot be resumed"));
    }

    [Test]
    public void Warning_SaysWhatToDoAboutIt()
    {
        var warning = ChildSessionCheck.Warning(_ => "1");

        Assert.That(warning, Does.Contain("Start Menu"));
    }

    [Test]
    public void Warning_TreatsAnEmptyVariableAsAbsent()
    {
        Assert.That(ChildSessionCheck.Warning(_ => string.Empty), Is.Null);
    }
}
