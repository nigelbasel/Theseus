using Theseus.Core.Domain;
using Theseus.Core.Reporting;

namespace Theseus.Tests.Reporting;

[TestFixture]
public class DayReportTests
{
    private static readonly DateOnly Day = new(2026, 9, 2);
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_CountsASessionThatRanWithinTheDay()
    {
        var entries = Report(Run("research", At(9), At(11, 30)));

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(2.5)));
    }

    [Test]
    public void For_ClipsASessionThatStartedYesterdayToWhatItSpentToday()
    {
        var entries = Report(Run("research", At(9).AddDays(-1), At(2)));

        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(2)),
            "an overnight session belongs to both days, and neither should be given all of it");
    }

    [Test]
    public void For_ClipsASessionStillRunningToTheMomentAsked()
    {
        var entries = Report(Run("research", At(12), ended: null, live: true));

        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(entries[0].StillRunning, Is.True, "so the figure is not read as final");
    }

    [Test]
    public void For_LeavesOutADayTheSessionNeverTouched()
    {
        Assert.That(Report(Run("research", At(9).AddDays(-3), At(17).AddDays(-3))), Is.Empty);
    }

    [Test]
    public void For_AddsUpSeveralSessionsOfOneWorkstream()
    {
        var entries = Report(
            Run("research", At(9), At(10)),
            Run("research", At(13), At(14)));

        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(entries[0].Sessions, Is.EqualTo(2));
    }

    [Test]
    public void For_PutsTheLongestFirst()
    {
        var entries = Report(
            Run("research", At(9), At(10)),
            Run("dev", At(9), At(13)));

        Assert.That(entries[0].WorkstreamKey, Is.EqualTo("dev"));
    }

    [Test]
    public void For_CarriesTheIssuesWithoutDividingTimeBetweenThem()
    {
        var entries = Report(Run("dev", At(9), At(12)));

        Assert.That(entries[0].JiraKeys, Is.EqualTo(new[] { "NDC-1", "NDC-2" }),
            "nothing knows how to split a session between issues, so the split is not invented");
        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(3)), "the time stays whole");
    }

    [Test]
    public void For_StillNamesAWorkstreamWhoseCardHasGone()
    {
        var entries = Report(Run("deleted-since", At(9), At(10)));

        Assert.That(entries[0].Title, Is.EqualTo("deleted-since"),
            "a day's record must not lose work because its card was archived afterwards");
    }

    [Test]
    public void SafeToClose_NamesOnlyWhatIsNoLongerRunning()
    {
        var entries = Report(
            Run("research", At(9), At(10)),
            Run("dev", At(9), ended: null, live: true));

        Assert.That(DayReport.SafeToClose(entries).Select(entry => entry.WorkstreamKey),
            Is.EqualTo(new[] { "research" }));
    }

    [Test]
    public void Total_AddsUpTheWholeDay()
    {
        var entries = Report(
            Run("research", At(9), At(10)),
            Run("dev", At(9), At(12)));

        Assert.That(DayReport.Total(entries), Is.EqualTo(TimeSpan.FromHours(4)));
    }

    private static IReadOnlyList<DayReportEntry> Report(params SessionRun[] runs) =>
        DayReport.For(Day, runs, Workstreams(), key => key == "research",
            sessionId => sessionId == "live", Now);

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 9, 2, hour, minute, 0, TimeSpan.Zero);

    private static IReadOnlyList<Workstream> Workstreams() =>
    [
        Workstream("research", "Nexus research", "Research", []),
        Workstream("dev", "Job queue", "Dev", ["NDC-1", "NDC-2"]),
    ];

    private static Workstream Workstream(string key, string title, string area, string[] keys) =>
        new(key, title, "#336699", ColorIsManual: false, @"C:\work", [], "prompt", 1, "In progress",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null, keys,
            StatusDocumentPath: null, DateTimeOffset.UtcNow)
        {
            Area = area,
        };

    /// <param name="live">
    /// Whether the session is running now. A run with no ending is measurable only if it is: the real
    /// log holds weeks of runs nobody saw end, and those cannot be told apart from ones still going.
    /// </param>
    private static SessionRun Run(string key, DateTimeOffset started, DateTimeOffset? ended,
        bool live = false) =>
        new(Guid.NewGuid(), live ? "live" : "session", key, @"C:\work", null,
            SessionLaunchMode.FirstRun, "prompt", key, "#336699", started, ended, null, null, null,
            ended is null ? SessionExitReason.Unknown : SessionExitReason.ClosedCleanly);

    [Test]
    public void For_RefusesToGuessAtASessionNobodySawEnd()
    {
        // The real run log holds 112 of these, some three weeks old. Bounding them at the present
        // moment credited one real day with 287 hours of work.
        var entries = Report(Run("research", At(9), ended: null));

        Assert.Multiple(() =>
        {
            Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.Zero), "no length is claimed");
            Assert.That(entries[0].Sessions, Is.Zero, "and it is not counted among the measured ones");
            Assert.That(entries[0].UnmeasuredSessions, Is.EqualTo(1), "but it is not silently dropped");
            Assert.That(entries[0].StillRunning, Is.False, "it is not running: nobody saw it stop");
        });
    }

    [Test]
    public void For_LeavesOutAnUnendedRunFromAfterTheDay()
    {
        Assert.That(Report(Run("research", At(9).AddDays(3), ended: null)), Is.Empty,
            "a run that had not begun cannot have touched the day, measurable or not");
    }

    /// <summary>
    /// The real case from Nigel's log, 2026-09-04. A session was closed by Theseus's own /exit on
    /// 26 August, which recorded no ending, and resumed under the same identifier on 2 September.
    /// Closing the old run at the new one's start credited 2 September with eighteen hours of a
    /// session that had been dead for a week: "19h 14m over 2 sessions" for one workstream.
    ///
    /// A gap that crosses a day is not a session; it is an ending nobody saw. The old run keeps its
    /// unknown length and is reported as such, and the day gets only the run that actually happened.
    /// </summary>
    [Test]
    public void For_DoesNotCreditADeadSessionWithTheDaysBeforeItWasResumed()
    {
        var resumedOn = At(17, 59);
        var lastWeek = At(20, 43).AddDays(-7);
        var runs = new[]
        {
            Run("research", lastWeek, ended: null) with { ClaudeSessionId = "same-id" },
            Run("research", resumedOn, ended: null, live: true) with { ClaudeSessionId = "same-id" },
        };

        var entries = DayReport.For(Day, runs, Workstreams(), _ => false,
            id => id == "same-id", new DateTimeOffset(2026, 9, 2, 18, 30, 0, TimeSpan.Zero));

        Assert.Multiple(() =>
        {
            Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromMinutes(31)),
                "only the resumed run touched the day; the week-old one contributes nothing to it");
            Assert.That(entries[0].Sessions, Is.EqualTo(1));
        });
    }

    [Test]
    public void For_StillClosesAnUnendedRunAtTheNextOneWhenBothAreOnTheSameDay()
    {
        var runs = new[]
        {
            Run("research", At(9), ended: null) with { ClaudeSessionId = "same-id" },
            Run("research", At(13), At(14)) with { ClaudeSessionId = "same-id" },
        };

        var entries = Report(runs);

        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(5)),
            "a same-day resume still bounds the earlier run at the later one's start: 9-13 plus 13-14");
    }

    [Test]
    public void For_StillMeasuresTheSessionsItCanAlongsideOneItCannot()
    {
        var entries = Report(
            Run("research", At(9), At(11)),
            Run("research", At(13), ended: null));

        Assert.That(entries[0].Worked, Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(entries[0].UnmeasuredSessions, Is.EqualTo(1));
    }
}
