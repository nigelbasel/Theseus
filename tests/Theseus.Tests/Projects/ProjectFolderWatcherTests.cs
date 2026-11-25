using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The projects folder is watched, not polled (1.1b AC 3): a change anywhere under it calls back once the changes go
/// quiet, a burst calls back once, steady writing cannot hold the call off past its limit, and a folder that is not there,
/// or has gone, is not watched until it is back, when watching starts again and says so, so the caller reads it.
/// </summary>
[TestFixture]
public class ProjectFolderWatcherTests
{
    private TestProjectsFolder _projects = null!;
    private SemaphoreSlim _calls = null!;
    private ProjectFolderWatcher _watcher = null!;

    [SetUp]
    public void SetUp()
    {
        _projects = TestProjectsFolder.Create();
        _calls = new SemaphoreSlim(0);
        _watcher = new ProjectFolderWatcher(() => _calls.Release(), TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1));
    }

    [TearDown]
    public void TearDown()
    {
        _watcher.Dispose();
        _calls.Dispose();
        _projects.Dispose();
    }

    [Test]
    public void AFileWrittenDeepInAProject_CallsBack()
    {
        _projects.Write("a-project/project.md", "# A project\n");
        Assert.That(_watcher.Watch(_projects.Folder), Is.EqualTo(ProjectFolderWatch.Started));

        _projects.Write("a-project/work/a-slug/spec.md", "# Spec\n");

        Assert.That(_calls.Wait(TimeSpan.FromSeconds(5)), Is.True, "within the spec's 2 seconds, with margin for a loaded machine");
    }

    [Test]
    public void ABurstOfChanges_CostsFarFewerReadsThanWrites()
    {
        _watcher.Watch(_projects.Folder);

        for (var file = 0; file < 10; file++)
        {
            _projects.Write($"a-project/work/a-slug/file-{file}.md", "text");
        }

        Assert.That(_calls.Wait(TimeSpan.FromSeconds(5)), Is.True);

        // Windows can hand over some of a burst's events late, after the first call; what matters is that ten writes do
        // not cost ten reads.
        var more = 0;

        while (_calls.Wait(TimeSpan.FromMilliseconds(1500)))
        {
            more++;
        }

        Assert.That(1 + more, Is.LessThanOrEqualTo(2), "a burst is read once, or twice when its events arrive late");
    }

    [Test]
    public void SteadyWriting_StillCallsBack_WithinTheLimit()
    {
        _watcher.Watch(_projects.Folder);
        var started = DateTimeOffset.UtcNow;
        var called = false;

        // A write every 100 ms never goes quiet for 200 ms; only the 1 s limit lets the call through.
        while (!called && DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5))
        {
            _projects.Write("a-project/work/a-slug/evidence.md", DateTimeOffset.UtcNow.ToString("O"));
            called = _calls.Wait(TimeSpan.FromMilliseconds(100));
        }

        Assert.That(called, Is.True, "a file written steadily cannot hold reads off (review of PR 1, item 4)");
    }

    [Test]
    public void WatchingAgain_SaysItWasAlreadyWatching()
    {
        Assert.That(_watcher.Watch(_projects.Folder), Is.EqualTo(ProjectFolderWatch.Started));
        Assert.That(_watcher.Watch(_projects.Folder), Is.EqualTo(ProjectFolderWatch.Watching));
    }

    [Test]
    public void AFolderThatIsNotThere_IsNotWatched_UntilItIs_ThenStarts()
    {
        var later = Path.Combine(_projects.Folder, "later");

        Assert.That(_watcher.Watch(later), Is.EqualTo(ProjectFolderWatch.NotWatching));

        Directory.CreateDirectory(later);

        Assert.That(_watcher.Watch(later), Is.EqualTo(ProjectFolderWatch.Started),
            "started, so the caller reads it: nothing told it of the files that arrived meanwhile (review of PR 1, item 1)");
    }

    [Test]
    public void AFolderGoneFromUnderTheWatcher_IsNoLongerWatched_AndStartsAgainWhenBack()
    {
        var folder = Path.Combine(_projects.Folder, "moving");
        Directory.CreateDirectory(folder);
        _watcher.Watch(folder);

        Directory.Move(folder, folder + "-away");

        Assert.That(_watcher.Watch(folder), Is.EqualTo(ProjectFolderWatch.NotWatching),
            "a moved folder raises nothing inside it, so it is found gone on the next refresh (review of PR 1, items 2 and 3)");

        Directory.Move(folder + "-away", folder);

        Assert.That(_watcher.Watch(folder), Is.EqualTo(ProjectFolderWatch.Started));
    }

    [Test]
    public void NoFolder_WatchesNothing() => Assert.That(_watcher.Watch(" "), Is.EqualTo(ProjectFolderWatch.NotWatching));
}
