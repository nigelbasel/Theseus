using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Which verifier cards Clear up verifiers… takes away (git-22, 2026-11-23: the -2 and -3 copies, and BYM's verifiers
/// that ran Theseus's scenarios because one holdout folder covered every project): a duplicate for the same build, or one
/// that ran a holdout another project owns. The newest correct verifier of each build stays.
/// </summary>
[TestFixture]
public class WrongVerifiersTests
{
    private const string TheseusHoldout = @"C:\src\Theseus-holdout";
    private const string BymHoldout = @"C:\src\bym-mcp-holdout";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheNewestCorrectVerifier_Stays_AndItsOlderCopiesGo()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-1-1e", "theseus-1-1e", TheseusHoldout, Nine),
            Verifier("verify-1-1e-2", "theseus-1-1e", TheseusHoldout, Nine.AddMinutes(1)),
            Verifier("verify-1-1e-3", "theseus-1-1e", TheseusHoldout, Nine.AddMinutes(2), running: true),
        ], OwnerOf);

        Assert.That(wrong.Select(each => each.Key), Is.EqualTo(new[] { "verify-1-1e", "verify-1-1e-2" }));
        Assert.That(wrong[0].Why, Is.EqualTo("a copy: Verify: verify-1-1e-3 checks the same build"));
    }

    [Test]
    public void AVerifierThatRanAHoldoutOwnedByAnotherProject_Goes_EvenWhenItIsTheOnlyOne()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-w1", "bym-w1", TheseusHoldout, Nine, running: true)], OwnerOf);

        Assert.That(wrong.Single().Key, Is.EqualTo("verify-w1"));
        Assert.That(wrong.Single().IsRunning, Is.True);
        Assert.That(wrong.Single().Why, Is.EqualTo($"it ran another project's scenarios, in {TheseusHoldout}"),
            "live read, 2026-11-23: with one folder set for every project, BYM's verifiers of Theseus's scenarios read as right");
    }

    [Test]
    public void AVerifierMadeBeforeChecksLines_Goes_UnlessItsReportWasConfirmed()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-old", "theseus-1-3", TheseusHoldout, Nine) with { BriefNamesChecks = false },
            Verifier("verify-kept", "theseus-1-1e", TheseusHoldout, Nine, confirmed: true) with { BriefNamesChecks = false },
        ], OwnerOf);

        Assert.That(wrong.Single().Key, Is.EqualTo("verify-old"), "git-22, 2026-11-23: it asked for Start again and could never run");
        Assert.That(wrong.Single().Why, Is.EqualTo("it was made before scenarios named their work, so it has nothing it may run"));
    }

    [Test]
    public void AVerifierWhoseReportHasArrived_IsNotStranded_ItWaitsForHisConfirmation()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-x", "theseus-1-3", TheseusHoldout, Nine) with { SubjectNamed = false, HasReport = true }], OwnerOf);

        Assert.That(wrong, Is.Empty, "review of the follow-ups, S2: Clear up rejected a report waiting under Confirm the report…");
    }

    [Test]
    public void AnOldBriefVerifierThatReported_Goes_UnlessHeConfirmedTheReport()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-read", "theseus-1-3", TheseusHoldout, Nine) with { BriefNamesChecks = false, HasReport = true },
            Verifier("verify-confirmed", "theseus-1-6", TheseusHoldout, Nine, confirmed: true) with { BriefNamesChecks = false, HasReport = true },
        ], OwnerOf);

        Assert.That(wrong.Single().Key, Is.EqualTo("verify-read"),
            "git-22, 2026-11-23 20:01: its needs-clarification report, read by hand, counted as the build's check, so no verifier was made");
        Assert.That(wrong.Single().Why, Is.EqualTo("it ran before scenarios named their work, so its report is not a check under today's rules"));
    }

    [Test]
    public void ARunningVerifierOfABuildNoScenarioNames_IsLeftToRun()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-x", "theseus-1-3", TheseusHoldout, Nine, running: true) with { SubjectNamed = false }], OwnerOf);

        Assert.That(wrong, Is.Empty, "a Checks line read mid-rewrite must not stop a running check");
    }

    [Test]
    public void AVerifierOfABuildNoScenarioNames_Goes()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-x", "theseus-1-3", TheseusHoldout, Nine) with { SubjectNamed = false }], OwnerOf);

        Assert.That(wrong.Single().Why, Is.EqualTo("no scenario names its build, so it has nothing it may run"));
    }

    [Test]
    public void AConfirmedVerifierOfAnotherProjectsHoldout_StillGoes()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-w1", "bym-w1", TheseusHoldout, Nine, confirmed: true)], OwnerOf);

        Assert.That(wrong.Single().Key, Is.EqualTo("verify-w1"), "review of item 5, M2: its confirmation is taken back with it");
    }

    [Test]
    public void AVerifierOfAProjectsOwnEarlierHoldout_IsNotAnotherProjects()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-old", "theseus-1-3", @"C:\old\Theseus-holdout", Nine, confirmed: true)], OwnerOf);

        Assert.That(wrong, Is.Empty, "review of item 5, S6: a moved holdout made every earlier verifier read as another project's");
    }

    [Test]
    public void ACorrectVerifierNewerThanAWrongOne_Stays()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-w1", "bym-w1", TheseusHoldout, Nine.AddMinutes(5)),
            Verifier("verify-w1-right", "bym-w1", BymHoldout, Nine),
        ], OwnerOf);

        Assert.That(wrong.Select(each => each.Key), Is.EqualTo(new[] { "verify-w1" }),
            "a wrong one is never the one kept, however new");
    }

    [Test]
    public void AVerifierWhoseReportWasConfirmed_Stays()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-old", "theseus-1-3", TheseusHoldout, Nine, confirmed: true),
            Verifier("verify-new", "theseus-1-3", TheseusHoldout, Nine.AddMinutes(1)),
        ], OwnerOf);

        Assert.That(wrong, Is.Empty, "its verdict is the record of that build's check");
    }

    [Test]
    public void ArchivedVerifiers_AreNotCountedAtAll()
    {
        var wrong = WrongVerifiers.Among(
        [
            Verifier("verify-a", "theseus-1-3", TheseusHoldout, Nine.AddMinutes(1), archived: true),
            Verifier("verify-b", "theseus-1-3", TheseusHoldout, Nine),
        ], OwnerOf);

        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void AHoldoutNoProjectOwns_IsJudgedOnCopiesAlone()
    {
        var wrong = WrongVerifiers.Among([Verifier("verify-x", "bym-w9", @"C:\shared\scenarios", Nine)], OwnerOf);

        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void TheHoldoutAVerifierRan_IsTheFolderAboveItsReportsResults()
    {
        Assert.That(VerifierRecord.HoldoutOfReport(@"C:\src\Theseus-holdout\results\theseus-1-3-20261123-090000.md"),
            Is.EqualTo(TheseusHoldout));
        Assert.That(VerifierRecord.HoldoutOfReport(@"C:\elsewhere\report.md"), Is.Null, "not a verifier's report path");
        Assert.That(VerifierRecord.HoldoutOfReport(null), Is.Null);
    }

    private static string? OwnerOf(string folder) =>
        string.Equals(folder, TheseusHoldout, StringComparison.OrdinalIgnoreCase) ? "theseus"
        : string.Equals(folder, BymHoldout, StringComparison.OrdinalIgnoreCase) ? "bym"
        : null;

    private static VerifierRecord Verifier(string key, string subject, string holdout, DateTimeOffset created,
        bool running = false, bool confirmed = false, bool archived = false) =>
        new(key, $"Verify: {key}", subject, subject.StartsWith("bym", StringComparison.Ordinal) ? "bym" : "theseus", running, archived, created,
            Path.Combine(holdout, "results", $"{subject}-{created:yyyyMMdd-HHmmss}.md"), confirmed);
}
