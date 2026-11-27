using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

/// <summary>7.3: a start nobody stopped is stopped once its card has had no time for eight hours, dated at its last activity.</summary>
[TestFixture]
public class StaleStartsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Due_StopsAStartWhoseCardHasBeenIdleForEightHours_AtItsLastActivity()
    {
        var marks = new[] { Start("s1", "alpha", 0) };
        var intervals = new[] { Machine("alpha", 10, 70) };

        var due = StaleStarts.Due(marks, intervals, Nine.AddMinutes(70).AddHours(8));

        Assert.That(due, Is.EqualTo(new[] { new WorkMark("s1", "alpha", WorkMarkKind.Stop, null, StaleStarts.Summary, Nine.AddMinutes(70)) }));
    }

    [Test]
    public void Due_LeavesAStartAlone_BeforeEightHours()
    {
        var due = StaleStarts.Due([Start("s1", "alpha", 0)], [Machine("alpha", 10, 70)], Nine.AddMinutes(70).AddHours(7));

        Assert.That(due, Is.Empty);
    }

    [Test]
    public void Due_DatesAStartWithNoTimeAfterIt_AtTheStart()
    {
        var due = StaleStarts.Due([Start("s1", "alpha", 0)], [Machine("alpha", -60, -30)], Nine.AddHours(9));

        Assert.That(due.Single().At, Is.EqualTo(Nine));
    }

    [Test]
    public void Due_StopsNothingAlreadyStopped()
    {
        var marks = new[] { Start("s1", "alpha", 0), new WorkMark("s1", "alpha", WorkMarkKind.Stop, null, StaleStarts.Summary, Nine) };

        Assert.That(StaleStarts.Due(marks, [], Nine.AddDays(2)), Is.Empty, "a second sweep does not stop it again");
    }

    [Test]
    public void Due_GoesBySession_SoAnotherSessionsNewerStartIsKept()
    {
        var marks = new[] { Start("s1", "alpha", 0), Start("s2", "alpha", 9 * 60) };

        var due = StaleStarts.Due(marks, [], Nine.AddHours(10));

        Assert.That(due.Select(stop => stop.SessionId), Is.EqualTo(new[] { "s1" }));
    }

    private static WorkMark Start(string session, string card, int minutes) =>
        new(session, card, WorkMarkKind.Start, "KITE-1", null, Nine.AddMinutes(minutes));

    private static AttentionInterval Machine(string card, int from, int to) =>
        new(AttentionKind.Machine, card, Nine.AddMinutes(from), Nine.AddMinutes(to));
}
