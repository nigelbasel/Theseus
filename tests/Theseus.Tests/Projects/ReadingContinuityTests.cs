using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A project carries over one failed read (PR 2 review, item 9): its last good reading stays, marked with the failure,
/// and only a second failure in a row makes it unreadable; a folder no longer listed drops it at once.
/// </summary>
[TestFixture]
public class ReadingContinuityTests
{
    private const string Folder = @"C:\repos\home";

    private static readonly ProjectFileReading Good = ProjectFileReader.Read("p", "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n")
        with { Folder = Folder };

    private static readonly ProjectFileReading Broken = ProjectFileReading.Unreadable("p", "git did not answer within 10 s") with { Folder = Folder };

    private static readonly ProjectFileReading FolderBroken = ProjectFileReading.Unreadable("home", "git could not be run") with { Folder = Folder };

    [Test]
    public void OneFailedRead_KeepsTheLastGoodReading_MarkedWithTheFailure()
    {
        var failures = new Dictionary<string, int>();

        var shown = ReadingContinuity.Merge([Good], [Broken], failures).Single();

        Assert.That((shown.IsReadable, shown.File), Is.EqualTo((true, Good.File)));
        Assert.That(shown.LastFailure, Is.EqualTo("git did not answer within 10 s"));
    }

    [Test]
    public void TwoFailedReadsInARow_MakeItUnreadable()
    {
        var failures = new Dictionary<string, int>();
        var first = ReadingContinuity.Merge([Good], [Broken], failures);

        var second = ReadingContinuity.Merge(first, [Broken], failures);

        Assert.That(second.Single().Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
    }

    [Test]
    public void AGoodReadBetweenFailures_StartsTheCountAgain()
    {
        var failures = new Dictionary<string, int>();
        var shown = ReadingContinuity.Merge([Good], [Broken], failures);
        shown = ReadingContinuity.Merge(shown, [Good], failures);

        shown = ReadingContinuity.Merge(shown, [Broken], failures);

        Assert.That((shown.Single().IsReadable, shown.Single().LastFailure), Is.EqualTo((true, "git did not answer within 10 s")));
    }

    [Test]
    public void AFolderThatFailsAsAWhole_CarriesItsProjectsOverToo()
    {
        var failures = new Dictionary<string, int>();

        var shown = ReadingContinuity.Merge([Good], [FolderBroken], failures);

        Assert.That(shown.Single(reading => reading.Name == "p").LastFailure, Is.EqualTo("git could not be run"));
    }

    [Test]
    public void AFolderNoLongerListed_DropsItsProjectsAtOnce()
    {
        Assert.That(ReadingContinuity.Merge([Good], [], new Dictionary<string, int>()), Is.Empty);
    }

    [Test]
    public void AFreshReading_KeepsTheArtefactsItHadUntilTheyAreReadAgain()
    {
        var artefacts = new Dictionary<string, WorkstreamArtefacts> { ["a-slug"] = WorkstreamArtefacts.NotOnMachine("a-slug") };
        var before = Good with { Artefacts = artefacts };
        var fresh = ProjectFileReader.Read("p", "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n") with { Folder = Folder };

        var shown = ReadingContinuity.Merge([before], [fresh], new Dictionary<string, int>()).Single();

        Assert.That(shown.Artefacts, Is.SameAs(artefacts));
        Assert.That(ReadingContinuity.WithArtefacts([shown], [fresh with { Artefacts = new Dictionary<string, WorkstreamArtefacts>() }])
            .Single().Artefacts, Is.Empty, "the second step's artefacts replace them");
    }
}
