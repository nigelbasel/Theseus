using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Projects are read off the board's thread (review of PR 1, item 2): a refresh never waits for git, one read runs at a
/// time, the same folders are read again only after the interval, and a failure leaves the projects unreadable rather
/// than escaping.
/// </summary>
[TestFixture]
public class BackgroundProjectReadsTests
{
    private static readonly ProjectFileReading Read = ProjectFileReading.Unreadable("a-project", "a stand-in reading");

    private ManualResetEventSlim _gitAnswers = null!;
    private SemaphoreSlim _posted = null!;
    private List<IReadOnlyList<string>> _reads = null!;
    private DateTimeOffset _now;

    [SetUp]
    public void SetUp()
    {
        _gitAnswers = new ManualResetEventSlim(false);
        _posted = new SemaphoreSlim(0);
        _reads = [];
        _now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    }

    [TearDown]
    public void TearDown()
    {
        _gitAnswers.Set();
        _gitAnswers.Dispose();
        _posted.Dispose();
    }

    private BackgroundProjectReads Reads(Func<IReadOnlyList<string>, IReadOnlyList<ProjectFileReading>>? read = null) =>
        new(read ?? (folders =>
            {
                lock (_reads)
                {
                    _reads.Add(folders);
                }

                _gitAnswers.Wait(TimeSpan.FromSeconds(10));

                return [Read];
            }),
            action =>
            {
                action();
                _posted.Release();
            },
            TimeSpan.FromSeconds(30), () => _now);

    private void WaitForTheResult() => Assert.That(_posted.Wait(TimeSpan.FromSeconds(10)), Is.True, "the read never came back");

    [Test]
    public void ARequest_ReturnsWithoutWaitingForGit()
    {
        var reads = Reads();

        reads.Request([@"C:\repos\home"]);

        Assert.That(reads.IsReading, Is.True);
        Assert.That(reads.Current, Is.Empty, "git has not answered yet");

        _gitAnswers.Set();
        WaitForTheResult();

        Assert.That((reads.IsReading, reads.Current.Single()), Is.EqualTo((false, Read)));
    }

    [Test]
    public void OneReadRunsAtATime_AndFoldersChangedMeanwhileAreReadAfterIt()
    {
        var reads = Reads();
        var changed = 0;
        reads.Changed += (_, _) => changed++;

        reads.Request([@"C:\repos\home"]);
        reads.Request([@"C:\repos\other"]);
        reads.Request([@"C:\repos\other"]);

        Assert.That(_reads, Has.Count.LessThanOrEqualTo(1));

        _gitAnswers.Set();
        WaitForTheResult();
        WaitForTheResult();

        Assert.That(_reads.Select(folders => folders.Single()), Is.EqualTo(new[] { @"C:\repos\home", @"C:\repos\other" }));
        Assert.That(changed, Is.EqualTo(2));
    }

    [Test]
    public void TheSameFolders_AreReadAgainOnlyAfterTheInterval()
    {
        _gitAnswers.Set();
        var reads = Reads();

        reads.Request([@"C:\repos\home"]);
        WaitForTheResult();
        _now += TimeSpan.FromSeconds(10);
        reads.Request([@"C:\repos\home"]);
        _now += TimeSpan.FromSeconds(25);
        reads.Request([@"C:\repos\home"]);
        WaitForTheResult();

        Assert.That(_reads, Has.Count.EqualTo(2));
    }

    [Test]
    public void NoFoldersListed_RunsNoRead()
    {
        var reads = Reads();

        reads.Request([]);

        Assert.That((_reads.Count, reads.IsReading, reads.Current.Count), Is.EqualTo((0, false, 0)));
    }

    [Test]
    public void AReadThatThrows_LeavesTheFoldersUnreadable_AndNothingEscapes()
    {
        var reads = Reads(_ => throw new ArgumentOutOfRangeException("index"));

        reads.Request([@"C:\repos\home"]);
        WaitForTheResult();

        Assert.That(reads.Current.Single().Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
        Assert.That(reads.Current.Single().Reason, Does.StartWith("could not be read:"));
    }
}
