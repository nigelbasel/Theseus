using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class TerminalSelectorTests
{
    private const string Stable = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24\WindowsTerminal.exe";
    private const string Preview = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminalPreview_1.25\WindowsTerminal.exe";
    private const string Alias = @"C:\Users\dev\AppData\Local\Microsoft\WindowsApps\wt.exe";

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

    [Test]
    public void Choose_PrefersTheStablePackageWhenOneOfEachIsRunning()
    {
        Assert.That(TerminalSelector.Choose([Preview, Stable], null).ExecutablePath, Is.EqualTo(Stable),
            "preview installs alongside the stable one; a tie decided alphabetically hands it to preview");
    }

    [Test]
    public void Choose_StillFollowsPreviewWhenThatIsWhereTheUserIsWorking()
    {
        Assert.That(TerminalSelector.Choose([Preview], null).ExecutablePath, Is.EqualTo(Preview));
    }

    [Test]
    public void Choose_LetsWeightOfNumbersBeatThePreferenceForStable()
    {
        Assert.That(TerminalSelector.Choose([Preview, Preview, Stable], null).ExecutablePath,
            Is.EqualTo(Preview), "three windows open in preview is where the work is, whatever the default");
    }

    [Test]
    public void Choose_PrefersAnInstalledStableTerminalOverTheAliasWhenNothingIsRunning()
    {
        var resolution = TerminalSelector.Choose([], Alias, [Stable, Preview]);

        Assert.That(resolution.ExecutablePath, Is.EqualTo(Stable),
            "both packages claim the alias, so on the first launch after a restart it opens preview");
    }

    [Test]
    public void Choose_FallsBackToTheAliasWhenNothingIsRunningAndNothingIsInstalled()
    {
        Assert.That(TerminalSelector.Choose([], Alias, []).ExecutablePath, Is.EqualTo(Alias));
    }

    [Test]
    public void Choose_StillFollowsWhateverIsRunningAheadOfWhatIsMerelyInstalled()
    {
        Assert.That(TerminalSelector.Choose([Preview], Alias, [Stable]).ExecutablePath, Is.EqualTo(Preview),
            "a running terminal is where the work is, and only it can host a named window");
    }

    [Test]
    public void Choose_UsesTheAliasWhenOnlyOnePackageIsInstalled()
    {
        var cold = TerminalSelector.Choose([], Alias, [Preview]);
        var warm = TerminalSelector.Choose([Stable, Stable], Alias, [Stable]);

        Assert.Multiple(() =>
        {
            Assert.That((cold.ExecutablePath, cold.Source), Is.EqualTo((Alias, TerminalSource.OnlyPackageAlias)),
                "with one package the alias can open nothing else, and Windows may refuse the package's own executable");
            Assert.That((warm.ExecutablePath, warm.Source), Is.EqualTo((Alias, TerminalSource.OnlyPackageAlias)));
        });
    }

    [Test]
    public void Choose_GoesToTheAliasOnceWindowsHasRefusedAPackage()
    {
        var refused = new RefusedTerminals();
        refused.Add(Stable);

        var resolution = TerminalSelector.Choose([Stable], Alias, [Stable, Preview], refused);

        Assert.That((resolution.ExecutablePath, resolution.Source), Is.EqualTo((Alias, TerminalSource.RefusedFallback)),
            "the running instance is refused too, and the other package would be refused or would open the preview");
    }

    [Test]
    public void Choose_WithARefusalAndNoAlias_TriesWhatIsThere()
    {
        var refused = new RefusedTerminals();
        refused.Add(Stable);

        Assert.That(TerminalSelector.Choose([], null, [Stable, Preview], refused).Source, Is.EqualTo(TerminalSource.RunningInstance),
            "with no alias to fall back to, the start is tried and its refusal reported");
    }

    [TestCase(5, @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24\WindowsTerminal.exe", true)]
    [TestCase(2, @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24\WindowsTerminal.exe", false)]
    [TestCase(5, @"C:\Tools\WindowsTerminal.exe", false)]
    [TestCase(5, @"C:\Users\dev\AppData\Local\Microsoft\WindowsApps\wt.exe", false)]
    public void IsPackageRefusal_IsAccessDeniedOnAPackageExecutable(int error, string path, bool refusal)
    {
        Assert.That(RefusedTerminals.IsPackageRefusal(new System.ComponentModel.Win32Exception(error), path), Is.EqualTo(refusal));
    }
}
