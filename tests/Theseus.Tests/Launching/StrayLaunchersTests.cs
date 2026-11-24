using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class StrayLaunchersTests
{
    private const string Root = @"C:\data\Theseus\launcher";
    private const string Current = @"C:\data\Theseus\launcher\2026.11.24.30000";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 25, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = Now - LauncherHold.Limit - TimeSpan.FromMinutes(1);

    [Test]
    public void ALauncherWithItsSessionGone_PastTheHold_IsAStray_WhicheverReleaseItCameFrom()
    {
        var old = Launcher(1, @"C:\data\Theseus\launcher\2026.11.23.23065\Theseus.Launcher.exe", LongAgo, hasChild: false);
        var recent = Launcher(2, @"C:\data\Theseus\launcher\2026.11.24.21928\Theseus.Launcher.exe", LongAgo, hasChild: false);

        Assert.That(StrayLaunchers.Among([old, recent], Root, Current, Now), Is.EqualTo(new[] { old, recent }));
    }

    [Test]
    public void ALauncherWithASessionRunning_IsALiveTab_AndLeftAlone_HoweverOld()
    {
        var live = Launcher(1, @"C:\data\Theseus\launcher\2026.11.23.23065\Theseus.Launcher.exe", LongAgo.AddDays(-1), hasChild: true);

        Assert.That(StrayLaunchers.Among([live], Root, Current, Now), Is.Empty);
    }

    [Test]
    public void ALauncherStartedWithinTheHold_IsLeftAlone()
    {
        var recent = Launcher(1, @"C:\data\Theseus\launcher\2026.11.24.21928\Theseus.Launcher.exe", Now.AddMinutes(-5), hasChild: false);

        Assert.That(StrayLaunchers.Among([recent], Root, Current, Now), Is.Empty);
    }

    [Test]
    public void ThisReleasesLauncher_HoldingAFailedTab_IsLeftToEndOnItsOwn_HoweverLongItsSessionRan()
    {
        var holding = Launcher(1, Current + @"\Theseus.Launcher.exe", LongAgo.AddHours(-2), hasChild: false);

        Assert.That(StrayLaunchers.Among([holding], Root, Current, Now), Is.Empty);
    }

    [TestCase(@"C:\Program Files\Theseus\Theseus.Launcher.exe")]
    [TestCase(@"C:\data\Theseus\launcher-old\x\Theseus.Launcher.exe")]
    public void ALauncherRunFromAnywhereElse_IsNotTheseusesToClear(string path)
    {
        Assert.That(StrayLaunchers.Among([Launcher(1, path, LongAgo, hasChild: false)], Root, Current, Now), Is.Empty);
    }

    private static LauncherProcess Launcher(int id, string path, DateTimeOffset started, bool hasChild) => new(id, path, started, hasChild);
}
