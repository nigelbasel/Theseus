using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// How a holdout scenario names the work it checks (Nigel, 2026-11-23, A2): a line "Checks: <slug>, <slug>", read by
/// Theseus itself, never by a session. Before this one folder's scenarios judged every build in it, so BYM's verifiers
/// ran Theseus's scenarios.
/// </summary>
[TestFixture]
public class ScenarioChecksTests
{
    [TestCase("Checks: close-step")]
    [TestCase("checks: Close-Step")]
    [TestCase("**Checks:** close-step")]
    [TestCase("- Checks: `close-step`")]
    [TestCase("  Checks:close-step  ")]
    [TestCase("## Checks: close-step")]
    [TestCase("1. Checks: close-step")]
    [TestCase("Checks : close-step.")]
    public void ALineSaysWhichWorkItChecks_HoweverItIsWritten(string line)
    {
        Assert.That(ScenarioChecks.NamesIn($"# Stop with handoff\n\n{line}\n\nSet up: …"), Is.EquivalentTo(new[] { "close-step" }));
    }

    [Test]
    public void OneLineMayNameSeveral_AndSeveralLinesAddUp()
    {
        Assert.That(ScenarioChecks.NamesIn("Checks: close-step, resumed-stop\r\nChecks: git-22"),
            Is.EquivalentTo(new[] { "close-step", "resumed-stop", "git-22" }));
        Assert.That(ScenarioChecks.NamesIn("Checks: close-step; resumed-stop\rChecks: git-22"),
            Is.EquivalentTo(new[] { "close-step", "resumed-stop", "git-22" }), "review of the auto-start: ; and bare CR");
    }

    [Test]
    public void AScenarioThatNamesNoWork_ChecksNothing()
    {
        Assert.That(ScenarioChecks.NamesIn("Set up a card. Then check that it resumes tomorrow. It checks the lock."), Is.Empty,
            "only a line that starts with Checks: names work");
    }
}
