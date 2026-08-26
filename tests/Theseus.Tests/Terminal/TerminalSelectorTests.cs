using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class TerminalSelectorTests
{
    private const string Stable = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24\WindowsTerminal.exe";
    private const string Preview = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminalPreview_1.25\WindowsTerminal.exe";
    private const string Alias = @"C:\Users\nigel\AppData\Local\Microsoft\WindowsApps\wt.exe";

    [Test]
    public void Choose_PrefersTheExecutableHostingTheMostWindows()
    {
        var resolution = TerminalSelector.Choose([Preview, Stable, Stable], Alias);

        Assert.That(resolution.ExecutablePath, Is.EqualTo(Stable));
        Assert.That(resolution.Source, Is.EqualTo(TerminalSource.RunningInstance));
    }

    [Test]
    public void Choose_ReportsThatTwoPackagesAreRunningSoTheUserCanBeWarned()
    {
        var resolution = TerminalSelector.Choose([Preview, Stable, Stable], Alias);

        Assert.That(resolution.DistinctRunningPackages, Is.EqualTo(2));
    }

    [Test]
    public void Choose_IgnoresTheAliasWhileAnythingIsRunningBecauseNamedWindowsDoNotCrossPackages()
    {
        var resolution = TerminalSelector.Choose([Preview], Alias);

        Assert.That(resolution.ExecutablePath, Is.EqualTo(Preview));
    }

    [Test]
    public void Choose_BreaksATieDeterministicallySoTabsDoNotWanderBetweenRuns()
    {
        var first = TerminalSelector.Choose([Preview, Stable], Alias);
        var second = TerminalSelector.Choose([Stable, Preview], Alias);

        Assert.That(first.ExecutablePath, Is.EqualTo(second.ExecutablePath));
    }

    [Test]
    public void Choose_TreatsPathsCaseInsensitivelyBecauseWindowsDoes()
    {
        var resolution = TerminalSelector.Choose([Stable, Stable.ToUpperInvariant(), Preview], Alias);

        Assert.That(resolution.DistinctRunningPackages, Is.EqualTo(2));
        Assert.That(resolution.ExecutablePath, Is.EqualTo(Stable));
    }

    [Test]
    public void Choose_FallsBackToTheAliasWhenNothingIsRunning()
    {
        var resolution = TerminalSelector.Choose([], Alias);

        Assert.That(resolution.ExecutablePath, Is.EqualTo(Alias));
        Assert.That(resolution.Source, Is.EqualTo(TerminalSource.ExecutionAlias));
    }

    [Test]
    public void Choose_ReportsNoTerminalRatherThanGuessing()
    {
        var resolution = TerminalSelector.Choose([], null);

        Assert.That(resolution.Source, Is.EqualTo(TerminalSource.None));
        Assert.That(resolution.ExecutablePath, Is.Empty);
    }

    [Test]
    public void Choose_SkipsBlankPathsFromProcessesItCouldNotInspect()
    {
        var resolution = TerminalSelector.Choose(["", "   ", Stable], Alias);

        Assert.That(resolution.ExecutablePath, Is.EqualTo(Stable));
        Assert.That(resolution.DistinctRunningPackages, Is.EqualTo(1));
    }
}
