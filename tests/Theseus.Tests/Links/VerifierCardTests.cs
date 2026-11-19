using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// The card Verify with holdouts… creates: where it runs, what it can reach, where it reports, and its fixed brief, in
/// the spec's words (verifier-card AC 10 and 11; P17).
/// </summary>
[TestFixture]
public class VerifierCardTests
{
    private const string Holdout = @"C:\holdout";
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 18, 5, 0, TimeSpan.FromHours(0));

    [Test]
    public void ItIsTitledAfterTheCardItChecks()
    {
        Assert.That(For().Title, Is.EqualTo("Verify: close-step"));
    }

    [Test]
    public void ItRunsInTheCheckedCardsFolder_WithTheHoldoutAndItsOtherReposInReach()
    {
        var card = For();

        Assert.That(card.WorkingDirectory, Is.EqualTo(@"C:\work\close-step\repo"),
            "no shell command may run inside the holdout, so its builds run in the code (fix pass 2)");
        Assert.That(card.AddDirectories, Is.EqualTo(new[] { Holdout, @"C:\work\close-step\other" }));
    }

    [Test]
    public void ItReportsToADatedFileInTheHoldoutsResults()
    {
        Assert.That(For().ReportPath, Is.EqualTo(@"C:\holdout\results\close-step-20261119-1805.md"));
    }

    [Test]
    public void ItsBrief_AsksForTheScenariosTheSpecNames_AndOnlyAVerdictBack()
    {
        var brief = For().Prompt;

        Assert.That(brief, Does.Contain("You are a verifier for close-step."));
        Assert.That(brief, Does.Contain(
            "Find the checked change's spec under work/ in the checked card's repos, and its section naming the holdout scenarios that apply."));
        Assert.That(brief, Does.Contain(
            "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout."));
        Assert.That(brief, Does.Contain("Run each named scenario."));
        Assert.That(brief, Does.Contain(
            "Ask the owner in this tab for any step only a person can do, quoting the scenario's step."));
        Assert.That(brief, Does.Contain(@"Write the details, steps and observations to C:\holdout\results\close-step-20261119-1805.md."));
        Assert.That(brief, Does.Contain(
            "Report back with a summary that gives only, for each scenario, its name, pass or fail, and one line of reason."));
        Assert.That(brief, Does.Contain("Change nothing in the checked card's repos."));
    }

    [Test]
    public void ItIsAVerifier()
    {
        Assert.That(VerifierCard.Role, Is.EqualTo(WorkstreamRole.Verifier));
    }

    [Test]
    public void ARepoNamedTwice_IsInReachOnce()
    {
        var twice = Checked() with { AddDirectories = [@"C:\work\close-step\other", @"c:\WORK\close-step\other"] };

        Assert.That(VerifierCard.For(twice, Holdout, Now).AddDirectories,
            Is.EqualTo(new[] { Holdout, @"C:\work\close-step\other" }), "verify part 1 attempt 2, S9");
    }

    private static VerifierCard For() => VerifierCard.For(Checked(), Holdout, Now);

    private static Workstream Checked() => new("close-step", "close-step", "#C0392B", ColorIsManual: true,
        @"C:\work\close-step\repo", [@"C:\work\close-step\other", @"C:\work\close-step\repo"], "Carry on.", 1, "Today",
        IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null, [], null, Now);
}
