using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.Tests.Projects;

/// <summary>
/// A closed or archived project is kept in Theseus's settings with what its close changed (archive-a-project,
/// 2026-11-26), survives a restart, and Reopen undoes exactly that.
/// </summary>
[TestFixture]
public class ProjectClosuresTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private static TheseusSettings QueueOn(params string[] projects) =>
        TheseusSettings.Default with { Duties = TheseusSettings.Default.Duties with { QueueProjects = projects } };

    [Test]
    public void Close_RecordsTheClosure_AndTurnsItsQueueOff_RememberingItWasOn()
    {
        var closed = ProjectClosures.Close(QueueOn("alpha", "beta"), "alpha", new ProjectClosure(ProjectClosureKind.Closed, Nine));

        Assert.That(ProjectClosures.Of(closed, "ALPHA"), Is.Not.Null, "project names are matched as folder names are");
        Assert.That(ProjectClosures.Of(closed, "alpha")!.QueueWasOn, Is.True);
        Assert.That(closed.Duties.QueueProjects, Is.EqualTo(new[] { "beta" }));
    }

    [Test]
    public void Reopen_ForgetsTheClosure_AndPutsTheQueueBackOnlyIfItWasOn()
    {
        var withQueue = ProjectClosures.Close(QueueOn("alpha"), "alpha", new ProjectClosure(ProjectClosureKind.Closed, Nine));
        var withoutQueue = ProjectClosures.Close(QueueOn(), "alpha", new ProjectClosure(ProjectClosureKind.Closed, Nine));

        var (reopened, closure) = ProjectClosures.Reopen(withQueue, "alpha");
        var (left, _) = ProjectClosures.Reopen(withoutQueue, "alpha");

        Assert.That((ProjectClosures.IsClosed(reopened, "alpha"), closure!.Kind), Is.EqualTo((false, ProjectClosureKind.Closed)));
        Assert.That(reopened.Duties.QueueIsOn("alpha"), Is.True);
        Assert.That(left.Duties.QueueIsOn("alpha"), Is.False);
    }

    [Test]
    public void ReopeningAnOpenProject_ChangesNothing()
    {
        var (settings, closure) = ProjectClosures.Reopen(TheseusSettings.Default, "alpha");

        Assert.That((settings, closure), Is.EqualTo((TheseusSettings.Default, (ProjectClosure?)null)));
    }

    [Test]
    public void OnlyAnArchive_ArchivesCards_AndOnlyThoseWithNoSessionNotArchivedAlready()
    {
        IReadOnlyList<ProjectCloseCard> cards =
        [
            new("quiet", "Quiet", HasSession: false, IsArchived: false),
            new("running", "Running", HasSession: true, IsArchived: false),
            new("put-away", "Put away", HasSession: false, IsArchived: true),
        ];

        Assert.That(ProjectClosures.DueToArchive(new ProjectClosure(ProjectClosureKind.Archived, Nine), cards), Is.EqualTo(new[] { "quiet" }));
        Assert.That(ProjectClosures.DueToArchive(new ProjectClosure(ProjectClosureKind.Closed, Nine), cards), Is.Empty);
        Assert.That(ProjectClosures.DueToArchive(null, cards), Is.Empty);
    }

    [Test]
    public void TheClosure_SurvivesARestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"theseus-closures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new SettingsStore(Path.Combine(directory, "settings.json"));
            var closure = new ProjectClosure(ProjectClosureKind.Archived, Nine) { PausedDuties = ["d-1"], ArchivedCards = ["a"] };
            store.Write(ProjectClosures.Close(QueueOn("alpha"), "alpha", closure));

            var read = ProjectClosures.Of(new SettingsStore(Path.Combine(directory, "settings.json")).Read(), "alpha")!;

            Assert.That((read.Kind, read.ClosedUtc, read.QueueWasOn), Is.EqualTo((ProjectClosureKind.Archived, Nine, true)));
            Assert.That(read.PausedDuties, Is.EqualTo(new[] { "d-1" }));
            Assert.That(read.ArchivedCards, Is.EqualTo(new[] { "a" }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void TheConfirmation_SaysWhenNothingRuns_AndNamesOneCardThatDoes()
    {
        var none = ProjectClose.For(ProjectClosureKind.Closed, "Alpha", [new("a", "A", false, false)], 0, false);
        var one = ProjectClose.For(ProjectClosureKind.Closed, "Alpha", [new("a", "A", true, false)], 2, false);

        Assert.That(none.Question, Is.EqualTo("No card of Alpha is running. It leaves the board's projects and the waits count. "
                                              + "Reopen, under Archived on the board, puts it back as it was."));
        Assert.That(one.Question, Does.StartWith("1 card is running, A: it is asked to write its handover and stop. 2 duties pause."));
    }
}
