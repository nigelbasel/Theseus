using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class CardEndingTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Started.AddHours(1);
    private static readonly DateTimeOffset IdleLongEnough = Now - CardEnding.Quiet - TimeSpan.FromSeconds(1);

    [Test]
    public void AVerifier_EndsOnceItHasFiledItsReport()
    {
        Assert.That(CardEnding.Decide(Facts(WorkstreamRole.Verifier, Reported(Now.AddMinutes(-5))), Now),
            Is.EqualTo(CardEndingReason.ReportFiled));
    }

    [Test]
    public void AVerifier_WhoseReportCameFromAnEarlierRun_OrWasRejected_StaysOpen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardEnding.Decide(Facts(WorkstreamRole.Verifier, Reported(Started.AddMinutes(-30))), Now), Is.Null);
            Assert.That(CardEnding.Decide(Facts(WorkstreamRole.Verifier, Reported(Now.AddMinutes(-5)) with
            {
                Rejection = new LinkRejection(Now.AddMinutes(-4), "not this", null),
            }), Now), Is.Null);
        });
    }

    [Test]
    public void ACard_EndsOnceItsReportIsAccepted()
    {
        Assert.That(CardEnding.Decide(Facts(WorkstreamRole.None, Accepted(Now.AddMinutes(-5))), Now),
            Is.EqualTo(CardEndingReason.ReportAccepted));
    }

    [Test]
    public void ACardHandedWorkAfterItsReportWasAccepted_StaysOpen()
    {
        var facts = Facts(WorkstreamRole.None, Accepted(Now.AddMinutes(-5))) with { LastHandInUtc = Now.AddMinutes(-2) };

        Assert.That(CardEnding.Decide(facts, Now), Is.Null);
    }

    [Test]
    public void ACardWhoseReportWasAcceptedBeforeThisRunStarted_StaysOpen()
    {
        Assert.That(CardEnding.Decide(Facts(WorkstreamRole.None, Accepted(Started.AddMinutes(-5))), Now), Is.Null,
            "it was started again for more work");
    }

    [Test]
    public void ACardSpokenToInItsTabAfterAcceptance_StaysOpen()
    {
        var accepted = Now.AddMinutes(-40);
        var facts = Facts(WorkstreamRole.None, Accepted(accepted)) with { IdleSinceUtc = accepted + CardEnding.FinishWindow + TimeSpan.FromMinutes(1) };

        Assert.That(CardEnding.Decide(facts, Now), Is.Null, "a turn that ended well after acceptance was somebody talking to it");
    }

    [Test]
    public void AVerifierStillWorkingLongAfterItsReport_StaysOpen()
    {
        var reported = Now.AddMinutes(-40);
        var facts = Facts(WorkstreamRole.Verifier, Reported(reported)) with { IdleSinceUtc = reported + CardEnding.FinishWindow + TimeSpan.FromMinutes(1) };

        Assert.That(CardEnding.Decide(facts, Now), Is.Null);
    }

    [Test]
    public void AScenarioWriter_EndsOnceItHasWrittenItsScenarios()
    {
        var facts = Facts(WorkstreamRole.ScenarioWriter) with { ScenariosWrittenUtc = Now.AddMinutes(-5) };

        Assert.That(CardEnding.Decide(facts, Now), Is.EqualTo(CardEndingReason.ScenariosWritten));
        Assert.That(CardEnding.Decide(facts with { ScenariosWrittenUtc = Started.AddMinutes(-20) }, Now), Is.Null,
            "scenarios from an earlier run are not this run's work");
    }

    [Test]
    public void AFinishedCard_StaysOpen_UntilItIsIdleAndQuiet()
    {
        var finished = Facts(WorkstreamRole.Verifier, Reported(Now.AddMinutes(-5)));

        Assert.Multiple(() =>
        {
            Assert.That(CardEnding.Decide(finished with { Lifecycle = SessionLifecycle.Running }, Now), Is.Null);
            Assert.That(CardEnding.Decide(finished with { Lifecycle = SessionLifecycle.NeedsAttention }, Now), Is.Null);
            Assert.That(CardEnding.Decide(finished with { IdleSinceUtc = Now.AddSeconds(-5) }, Now), Is.Null);
            Assert.That(CardEnding.Decide(finished with { IdleSinceUtc = null }, Now), Is.Null);
        });
    }

    [Test]
    public void AFinishedCard_StaysOpen_WhileSomethingWaitsForIt_ItsQuestionIsOpen_OrItLeadsItsProject()
    {
        var finished = Facts(WorkstreamRole.None, Accepted(Now.AddMinutes(-5)));

        Assert.Multiple(() =>
        {
            Assert.That(CardEnding.Decide(finished with { SomethingWaits = true }, Now), Is.Null);
            Assert.That(CardEnding.Decide(finished with { QuestionOpen = true }, Now), Is.Null);
            Assert.That(CardEnding.Decide(finished with { IsLead = true }, Now), Is.Null);
        });
    }

    [Test]
    public void AnUpdater_OrACardWithNoReport_IsNeverEndedByItself()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardEnding.Decide(Facts(WorkstreamRole.ProjectUpdater, Accepted(Now.AddMinutes(-5))), Now), Is.Null);
            Assert.That(CardEnding.Decide(Facts(WorkstreamRole.None), Now), Is.Null);
        });
    }

    [Test]
    public void AnUnattendedCard_EndsOnceItHasReportedBack_WithItsHandoffWritten()
    {
        var facts = Facts(WorkstreamRole.None, Reported(Now.AddMinutes(-5))) with
        {
            Unattended = true,
            HandoffWrittenUtc = Now.AddMinutes(-4),
        };

        Assert.That(CardEnding.Decide(facts, Now), Is.EqualTo(CardEndingReason.ReportFiled), "the report waits on the board");
    }

    [Test]
    public void AnUnattendedCard_StaysOpen_WithoutAHandoffWrittenThisRun_OrWithItsQuestionOpen()
    {
        var reported = Facts(WorkstreamRole.None, Reported(Now.AddMinutes(-5))) with { Unattended = true };

        Assert.Multiple(() =>
        {
            Assert.That(CardEnding.Decide(reported, Now), Is.Null, "no handoff");
            Assert.That(CardEnding.Decide(reported with { HandoffWrittenUtc = Started.AddMinutes(-1) }, Now), Is.Null,
                "a handoff from an earlier run");
            Assert.That(CardEnding.Decide(reported with { HandoffWrittenUtc = Now.AddMinutes(-4), QuestionOpen = true }, Now), Is.Null);
            Assert.That(CardEnding.Decide(Facts(WorkstreamRole.None, Reported(Started.AddMinutes(-5))) with
            {
                Unattended = true,
                HandoffWrittenUtc = Now.AddMinutes(-4),
            }, Now), Is.Null, "a report from an earlier run");
        });
    }

    [Test]
    public void AnAttendedCard_ThatHasOnlyReported_StaysOpen_UntilTheReportIsAccepted()
    {
        var facts = Facts(WorkstreamRole.None, Reported(Now.AddMinutes(-5))) with { HandoffWrittenUtc = Now.AddMinutes(-4) };

        Assert.That(CardEnding.Decide(facts, Now), Is.Null, "the owner may still be talking to it");
    }

    [Test]
    public void EachEnding_SaysWhyInWords()
    {
        Assert.That(Enum.GetValues<CardEndingReason>().Select(CardEnding.Text), Is.EqualTo(new[]
        {
            "Ending by itself: its report is filed",
            "Ending by itself: its report was accepted and nothing more waits for it",
            "Ending by itself: its scenarios are written",
        }));
    }

    private static CardEndingFacts Facts(WorkstreamRole role, params Link[] links) =>
        new("card", role, SessionLifecycle.Idle, IdleLongEnough, Started, links);

    private static Link Reported(DateTimeOffset at) =>
        new("lk-1", LinkKind.ReportsBack, "card", "parent", Started.AddDays(-1), Link.MadeOnTheBoard, "REPORT.md",
            LatestReport: new LinkReport(at, "session", "REPORT.md", "done", null));

    private static Link Accepted(DateTimeOffset at) => Reported(at.AddMinutes(-10)) with { SatisfiedUtc = at, SatisfiedBy = "board" };
}
