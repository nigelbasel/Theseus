using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class HandoffPathTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-handoff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void For_UsesTheDocumentTheWorkstreamNamesInPreferenceToAnythingOnDisk()
    {
        Write("HANDOFF.md");

        var chosen = @"C:\elsewhere\NOTES.md";

        Assert.That(HandoffPath.For(Workstream(status: chosen)), Is.EqualTo(chosen));
    }

    [Test]
    public void For_FindsTheNotesTheSessionsHaveBeenKeeping()
    {
        var notes = Write("HANDOFF.md");

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(notes));
    }

    [Test]
    public void For_LooksOneLevelDownBecauseNotesUsuallyLiveBesideTheWorkNotAboveIt()
    {
        var notes = Write(Path.Combine("theseus-notes", "HANDOFF.md"));

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(notes));
    }

    [Test]
    public void For_SearchesTheDirectoriesAddedToTheSessionsScope()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");
        var notes = Write(Path.Combine("elsewhere", "HANDOVER.md"));
        var workstream = Workstream(cwd: Path.Combine(_root, "empty"), addDirectories: [elsewhere]);

        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        Assert.That(HandoffPath.For(workstream), Is.EqualTo(notes));
    }

    [Test]
    public void For_PrefersTheNotesNamedForThisWorkstreamOverANewerFileThatIsNot()
    {
        Write("HANDOFF.md", DateTimeOffset.UtcNow);
        var mine = Write("HANDOFF-contact-forms.md", DateTimeOffset.UtcNow.AddDays(-7));

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(mine));
    }

    [Test]
    public void For_RefusesToGuessBetweenSeveralWorkstreamsNotesInASharedDirectory()
    {
        Write("SESSION-STATUS-2026-08-25-someone-elses-work.md", DateTimeOffset.UtcNow);
        Write("SESSION-STATUS-2026-08-25-a-third-workstream.md", DateTimeOffset.UtcNow.AddDays(-1));

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(Proposed()),
            "the most recently written belongs to the busiest workstream, never to the one asking");
    }

    [Test]
    public void For_TakesTheOnlyNotesThereAreEvenThoughTheyNameNobody()
    {
        var notes = Write("HANDOFF.md");

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(notes),
            "one candidate is unambiguous; a workstream alone in its directory need not label its notes");
    }

    [Test]
    public void For_TakesTheMostRecentOfSeveralThatDoNameIt()
    {
        Write("HANDOFF-contact-forms.md", DateTimeOffset.UtcNow.AddDays(-3));
        var newest = Write("SESSION-STATUS-2026-08-25-contact-forms.md", DateTimeOffset.UtcNow);

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(newest));
    }

    [Test]
    public void For_AcceptsNotesNamedForTheWorkstreamsTitleRatherThanItsKey()
    {
        var notes = Write("SESSION-STATUS-Contact-form-validation.md");

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(notes));
    }

    [Test]
    public void For_IgnoresTheBuildAndPackageDirectoriesADeepSearchWouldDrownIn()
    {
        Write(Path.Combine("node_modules", "HANDOFF.md"));
        Write(Path.Combine("obj", "HANDOFF.md"));

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(Proposed()));
    }

    [Test]
    public void For_FallsBackToAConventionalNameWhenNobodyHasWrittenNotesYet()
    {
        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(Proposed()));
    }

    [Test]
    public void For_DoesNotSearchBelowTheFirstLevelOfSubdirectories()
    {
        Write(Path.Combine("one", "two", "HANDOFF.md"));

        Assert.That(HandoffPath.For(Workstream()), Is.EqualTo(Proposed()));
    }

    [Test]
    public void Discover_FindsNothingWhenTheWorkingDirectoryIsGone()
    {
        Assert.That(HandoffPath.Discover(Workstream(cwd: Path.Combine(_root, "never-existed"))), Is.Null);
    }

    private string Write(string relativePath, DateTimeOffset? written = null)
    {
        var path = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "notes");

        if (written is not null)
        {
            File.SetLastWriteTimeUtc(path, written.Value.UtcDateTime);
        }

        return path;
    }

    private string Proposed() =>
        Path.Combine(_root, $"SESSION-STATUS-{DateTimeOffset.Now:yyyy-MM-dd}-contact-forms.md");

    private Workstream Workstream(string? cwd = null, string? status = null,
        IReadOnlyList<string>? addDirectories = null) =>
        new("contact-forms", "Contact form validation", "#208DD7", ColorIsManual: true,
            cwd ?? _root, addDirectories ?? [], "Add validation.", 1, "Today",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, "session-1", [], status,
            DateTimeOffset.UtcNow);
}
