using Theseus.Core.Launching;
using Theseus.Core.Running;

namespace Theseus.Tests.Launching;

/// <summary>
/// A start refused by a breaker or a limit says why at once, where it was pressed, with the action that clears it (Nigel,
/// 2026-11-25: "Start again did not do anything"; the reason was only a Warning in the log).
/// </summary>
[TestFixture]
public class RefusedStartTests
{
    private const string Tripped = "Breaker tripped: 3 runs in a row with no new commit, record, tick or report.";
    private const string Spent = "Stopped at its limit: project bug-fixing's spend today $66.69 of $60.00 a day.";

    [Test]
    public void NothingBlocked_SaysNothing()
    {
        Assert.That(RefusedStart.For([]), Is.Null);
    }

    [Test]
    public void ABreaker_SaysWhy_AndOffersToResetIt()
    {
        var refused = RefusedStart.For([new StartBlock("verify-1", "verify-1", RefusalReason.BreakerTripped, Tripped)])!;

        Assert.That(refused.Title, Is.EqualTo("Couldn't start verify-1"));
        Assert.That(refused.Text, Does.Contain(Tripped).And.Contain("Reset the breaker"));
        Assert.That(refused.BreakerKeys, Is.EqualTo(new[] { "verify-1" }));
        Assert.That(refused.Action, Is.EqualTo("_Reset the breaker and start"));
    }

    [Test]
    public void ADayLimit_SaysWhy_AndWhereToChangeIt_WithNoBreakerToReset()
    {
        var refused = RefusedStart.For([new StartBlock("verify-1", "verify-1", RefusalReason.BudgetReached, Spent)])!;

        Assert.That(refused.Text, Does.Contain(Spent).And.Contain("Options › General › Headless runs"));
        Assert.That(refused.BreakerKeys, Is.Empty);
        Assert.That(refused.Action, Is.Null);
    }

    [Test]
    public void SeveralRefused_AreNamedTogether_InOneMessage()
    {
        var refused = RefusedStart.For(
        [
            new StartBlock("verify-1", "verify-1", RefusalReason.BreakerTripped, Tripped),
            new StartBlock("verify-2", "verify-2", RefusalReason.BudgetReached, Spent),
        ])!;

        Assert.That(refused.Title, Is.EqualTo("Couldn't start 2 cards"));
        Assert.That(refused.Text, Does.Contain("verify-1").And.Contain("verify-2"));
        Assert.That(refused.BreakerKeys, Is.EqualTo(new[] { "verify-1" }));
    }
}
