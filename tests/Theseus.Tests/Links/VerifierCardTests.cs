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
        Assert.That(For().ReportPath, Is.EqualTo(@"C:\holdout\results\close-step-20261119-180500.md"));
    }

    [Test]
    public void ItsBrief_AsksForTheScenariosTheSpecNames_AndOnlyAVerdictBack()
    {
        var brief = For().Prompt;

        Assert.That(brief, Does.Contain("You are a verifier for close-step."));
        Assert.That(brief, Does.Contain(
            "The scenarios to run are those in the holdout whose \"Checks:\" line names close-step; the checked change's spec is in its project home, under work/<slug>/."),
            "the scenarios Theseus matched, not a list in the spec (review of the auto-start)");
        Assert.That(brief, Does.Contain(
            "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout."));
        Assert.That(brief, Does.Contain("Run each named scenario."));
        Assert.That(brief, Does.Contain(
            "Ask the owner in this tab for any step only a person can do, quoting the scenario's step."));
        Assert.That(brief, Does.Contain(@"Write the details, steps and observations to C:\holdout\results\close-step-20261119-180500.md."));
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
    public void ItsHandoff_GoesInTheHoldoutsResults_WhereItMayWrite()
    {
        Assert.That(For().HandoffPath, Is.EqualTo(@"C:\holdout\results\close-step-20261119-180500-handoff.md"),
            "the stop its confirmed report starts asks for a handoff; anywhere else the guard refuses it, and it never closes (review M1)");
    }

    [Test]
    public void ARepoNamedTwice_IsInReachOnce()
    {
        var twice = Checked() with { AddDirectories = [@"C:\work\close-step\other", @"c:\WORK\close-step\other"] };

        Assert.That(VerifierCard.For(twice, Holdout, Now).AddDirectories,
            Is.EqualTo(new[] { Holdout, @"C:\work\close-step\other" }), "verify part 1 attempt 2, S9");
    }

    [Test]
    public void AProjectWorkstreamsRecords_AreInReach_AndNamedInTheBrief()
    {
        var card = VerifierCard.For(Checked(), Holdout, Now, @"C:\projects\theseus\work\close-step");

        Assert.That(card.AddDirectories, Is.EqualTo(new[] { Holdout, @"C:\work\close-step\other", @"C:\projects\theseus\work\close-step" }),
            "records are kept outside every code repo (1.1b AC 9)");
        Assert.That(card.Prompt, Does.Contain(
            @"The scenarios to run are those in the holdout whose ""Checks:"" line names close-step; the checked change's spec is in C:\projects\theseus\work\close-step."));
    }

    [Test]
    public void TwoVerificationsInOneMinute_ReportToDifferentFiles()
    {
        var later = VerifierCard.For(Checked(), Holdout, Now.AddSeconds(30));

        Assert.That(later.ReportPath, Is.Not.EqualTo(For().ReportPath), "re-verify C3");
        Assert.That(later.HandoffPath, Is.Not.EqualTo(For().HandoffPath));
    }

    [Test]
    public void TheCreatedCard_IsMadeAVerifier_ThatHandsOverInTheResults()
    {
        var made = For().Apply(Checked());

        Assert.That(made.Role, Is.EqualTo(WorkstreamRole.Verifier));
        Assert.That(made.StatusDocumentPath, Is.EqualTo(@"C:\holdout\results\close-step-20261119-180500-handoff.md"),
            "re-verify m1: without it the stop asks for a handoff the guard refuses, and the verifier never closes");
    }

    private static VerifierCard For() => VerifierCard.For(Checked(), Holdout, Now);

    private static Workstream Checked() => new("close-step", "close-step", "#C0392B", ColorIsManual: true,
        @"C:\work\close-step\repo", [@"C:\work\close-step\other", @"C:\work\close-step\repo"], "Carry on.", 1, "Today",
        IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null, [], null, Now);
}
