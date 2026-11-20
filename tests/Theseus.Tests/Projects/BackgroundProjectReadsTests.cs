using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Projects are read off the board's thread: a refresh never waits for a read, one read runs at a time, the same folder
/// is read again only once it is marked stale (the watcher, 1.1b AC 3), never on a timer, and a failure leaves the
/// projects unreadable rather than escaping.
/// </summary>
[TestFixture]
public class BackgroundProjectReadsTests
{
    private static readonly ProjectFileReading Read = ProjectFileReading.Unreadable("a-project", "a stand-in reading");

    private ManualResetEventSlim _readAnswers = null!;
    private SemaphoreSlim _posted = null!;
    private List<string> _reads = null!;

    [SetUp]
    public void SetUp()
    {
        _readAnswers = new ManualResetEventSlim(false);
        _posted = new SemaphoreSlim(0);
        _reads = [];
    }

    [TearDown]
    public void TearDown()
    {
        _readAnswers.Set();
        _readAnswers.Dispose();
        _posted.Dispose();
    }

    private BackgroundProjectReads Reads(Func<string, IReadOnlyList<ProjectFileReading>>? read = null) =>
        new(read ?? (folder =>
            {
                lock (_reads)
                {
                    _reads.Add(folder);
                }

                _readAnswers.Wait(TimeSpan.FromSeconds(10));

                return [Read];
            }),
            action =>
            {
                action();
                _posted.Release();
            });

    private void WaitForTheResult() => Assert.That(_posted.Wait(TimeSpan.FromSeconds(10)), Is.True, "the read never came back");

    [Test]
    public void ARequest_ReturnsWithoutWaitingForTheRead()
    {
        var reads = Reads();

        reads.Request(@"C:\projects");

        Assert.That(reads.IsReading, Is.True);
        Assert.That(reads.Current, Is.Empty, "the read has not answered yet");

        _readAnswers.Set();
        WaitForTheResult();

        Assert.That((reads.IsReading, reads.Current.Single()), Is.EqualTo((false, Read)));
    }

    [Test]
    public void OneReadRunsAtATime_AndAFolderChangedMeanwhileIsReadAfterIt()
    {
        var reads = Reads();
        var changed = 0;
        reads.Changed += (_, _) => changed++;

        reads.Request(@"C:\projects");
        reads.Request(@"C:\other");
        reads.Request(@"C:\other");

        Assert.That(_reads, Has.Count.LessThanOrEqualTo(1));

        _readAnswers.Set();
        WaitForTheResult();
        WaitForTheResult();

        Assert.That(_reads, Is.EqualTo(new[] { @"C:\projects", @"C:\other" }));
        Assert.That(changed, Is.EqualTo(2));
    }

    [Test]
    public void AFolderChangedAndChangedBack_DuringARead_IsNotReadAgainAfterIt()
    {
        var reads = Reads();

        reads.Request(@"C:\projects");
        reads.Request(@"C:\other");
        reads.Request(@"C:\projects");

        _readAnswers.Set();
        WaitForTheResult();

        Assert.That(_posted.Wait(TimeSpan.FromMilliseconds(500)), Is.False, "no second read follows");
        Assert.That(_reads, Is.EqualTo(new[] { @"C:\projects" }));
    }

    [Test]
    public void TheSameFolder_IsNotReadAgain_UntilItIsMarkedStale()
    {
        _readAnswers.Set();
        var reads = Reads();

        reads.Request(@"C:\projects");
        WaitForTheResult();
        reads.Request(@"C:\projects");
        reads.Request(@"C:\projects");

        Assert.That(_reads, Has.Count.EqualTo(1), "no timer: an unchanged folder is not read again");

        reads.MarkStale();
        reads.Request(@"C:\projects");
        WaitForTheResult();

        Assert.That(_reads, Has.Count.EqualTo(2));
    }

    [Test]
    public void MarkedStaleDuringARead_IsReadOnceMoreAfterIt()
    {
        var reads = Reads();

        reads.Request(@"C:\projects");
        reads.MarkStale();
        reads.Request(@"C:\projects");

        _readAnswers.Set();
        WaitForTheResult();
        WaitForTheResult();

        Assert.That(_reads, Has.Count.EqualTo(2), "a file changed under the read is read again");
    }

    [Test]
    public void NoFolderSet_RunsNoRead()
    {
        var reads = Reads();

        reads.Request("  ");

        Assert.That((_reads.Count, reads.IsReading, reads.Current.Count, reads.HasLanded), Is.EqualTo((0, false, 0, true)));
    }

    [Test]
    public void AReadThatThrows_LeavesTheFolderUnreadable_AndNothingEscapes()
    {
        var reads = Reads(_ => throw new ArgumentOutOfRangeException("index"));

        reads.Request(@"C:\projects");
        WaitForTheResult();

        Assert.That(reads.Current.Single().Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
        Assert.That(reads.Current.Single().Reason, Does.StartWith("could not be read:"));
    }
}
