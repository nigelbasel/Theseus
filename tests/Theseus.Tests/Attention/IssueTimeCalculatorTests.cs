using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

[TestFixture]
public class IssueTimeCalculatorTests
{
    private static readonly DateTimeOffset Nine = new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_CountsOnlyTheTimeInsideTheStretch()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 10), Stop("s1", "gdpr", 40) };
        var intervals = new[] { Attended("gdpr", 0, 20), Machine("gdpr", 30, 60) };

        var time = IssueTimeCalculator.For("NDC-1", marks, intervals, Nine, At(120), At(120));

        Assert.That(time.Attended, Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(time.Machine, Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(time.Workstreams, Is.EqualTo(new[] { "gdpr" }));
    }

    [Test]
    public void For_GivesAWorkstreamsTimeToTheIssueStartedMostRecently()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 0), Start("s2", "gdpr", "NDC-2", 30) };
        var intervals = new[] { Attended("gdpr", 0, 60) };

        var first = IssueTimeCalculator.For("NDC-1", marks, intervals, Nine, At(60), At(60));
        var second = IssueTimeCalculator.For("NDC-2", marks, intervals, Nine, At(60), At(60));

        Assert.That(first.Attended, Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(second.Attended, Is.EqualTo(TimeSpan.FromMinutes(30)));
        Assert.That(first.Attended + second.Attended, Is.EqualTo(TimeSpan.FromMinutes(60)),
            "two sessions marking work at once never count the same minute twice");
    }

    [Test]
    public void For_EndsAStretchWhenTheSessionMovesToAnotherIssue()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 0), Start("s1", "gdpr", "NDC-2", 20) };
        var intervals = new[] { Attended("gdpr", 0, 60) };

        Assert.That(IssueTimeCalculator.For("NDC-1", marks, intervals, Nine, At(60), At(60)).Attended,
            Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void For_NeverCountsAnotherWorkstreamsTime()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 0) };
        var intervals = new[] { Attended("nexus", 0, 60), Machine("nexus", 0, 60) };

        var time = IssueTimeCalculator.For("NDC-1", marks, intervals, Nine, At(60), At(60));

        Assert.That(time.Attended + time.Machine, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void For_RunsAStretchNobodyStoppedUpToNow()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 0) };
        var intervals = new[] { Machine("gdpr", 0, 45) };

        Assert.That(IssueTimeCalculator.For("NDC-1", marks, intervals, Nine, At(45), At(45)).Machine,
            Is.EqualTo(TimeSpan.FromMinutes(45)));
    }

    [Test]
    public void For_CountsOnlyTheRangeAskedFor()
    {
        var marks = new[] { Start("s1", "gdpr", "NDC-1", 0), Stop("s1", "gdpr", 120) };
        var intervals = new[] { Attended("gdpr", 0, 120) };

        Assert.That(IssueTimeCalculator.For("NDC-1", marks, intervals, At(60), At(90), At(120)).Attended,
            Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [Test]
    public void For_MatchesTheIssueKeyWhateverItsCase()
    {
        var marks = new[] { Start("s1", "gdpr", "ndc-1", 0), Stop("s1", "gdpr", 10) };

        Assert.That(IssueTimeCalculator.For("NDC-1", marks, [Attended("gdpr", 0, 10)], Nine, At(10), At(10)).Attended,
            Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    private static WorkMark Start(string session, string workstream, string issue, int minutes) =>
        new(session, workstream, WorkMarkKind.Start, issue, null, At(minutes));

    private static WorkMark Stop(string session, string workstream, int minutes) =>
        new(session, workstream, WorkMarkKind.Stop, null, null, At(minutes));

    private static AttentionInterval Attended(string workstream, int from, int to) =>
        new(AttentionKind.Attended, workstream, At(from), At(to));

    private static AttentionInterval Machine(string workstream, int from, int to) =>
        new(AttentionKind.Machine, workstream, At(from), At(to));

    private static DateTimeOffset At(int minutes) => Nine.AddMinutes(minutes);
}
