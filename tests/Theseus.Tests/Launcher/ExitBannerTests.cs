using Theseus.Launcher;

namespace Theseus.Tests.Launcher;

[TestFixture]
public class ExitBannerTests
{
    [Test]
    public void Build_SaysNothingWhenTheSessionEndedCleanly()
    {
        var banner = ExitBanner.Build(new ClaudeRunResult("s-1", 0, HoldTab: false));

        Assert.That(banner, Is.Empty);
    }

    [Test]
    public void Build_NamesTheExitCodeAndSessionWhenTheTabIsHeld()
    {
        var banner = ExitBanner.Build(new ClaudeRunResult("s-1", 137, HoldTab: true));

        Assert.That(banner, Has.Some.Contains("137"));
        Assert.That(banner, Has.Some.Contains("s-1"));
    }
}
