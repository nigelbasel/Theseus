using Theseus.App.Services;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// How a nudge's outcome becomes an /exit result. Only the outcomes that typed nothing for certain may become ones
/// Close now can retry (close-step AC 9; verify part 1 mutation M18).
/// </summary>
[TestFixture]
public class ExitResultsMappingTests
{
    [TestCase(NudgeOutcome.Sent, ExitResult.Sent)]
    [TestCase(NudgeOutcome.TabNotConfirmed, ExitResult.TabNotConfirmed)]
    [TestCase(NudgeOutcome.FocusLost, ExitResult.FocusLost)]
    [TestCase(NudgeOutcome.Refused, ExitResult.Refused)]
    [TestCase(NudgeOutcome.BlockedByElevation, ExitResult.Elevated)]
    public void EachOutcome_MapsToItsResult(NudgeOutcome outcome, ExitResult expected)
    {
        Assert.That(ExitResults.From(outcome), Is.EqualTo(expected));
    }

    [Test]
    public void AnOutcomeNotYetKnown_IsNeverOneCloseNowRetries()
    {
        Assert.That(ExitResults.From((NudgeOutcome)999), Is.EqualTo(ExitResult.Refused));
    }
}
